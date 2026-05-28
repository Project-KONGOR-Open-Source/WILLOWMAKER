# local equivalent of .github/workflows/publish-release.yml

$ErrorActionPreference = 'Stop'

# the script uses PowerShell 7-era automatic variables like $IsWindows; check explicitly so Windows PowerShell 5.1 users get an actionable message instead of a confused failure later
if ($PSVersionTable.PSVersion.Major -lt 7)
{
    throw (@(
        'This script requires PowerShell 7 or later, but version $($PSVersionTable.PSVersion) was detected instead.'
        ''
        'Install the latest version of PowerShell from https://learn.microsoft.com/en-gb/powershell/scripting/install/install-powershell.'
    ) -join [Environment]::NewLine)
}

$RepoRoot          = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$SolutionDirectory = Join-Path $RepoRoot 'source'
$ProjectPath       = Join-Path $SolutionDirectory 'WILLOWMAKER'

$OperatingSystem = if     ($IsWindows) { 'Windows' }
                   elseif ($IsLinux)   { 'Linux'   }
                   elseif ($IsMacOS)   { 'macOS'   }
                   else                { throw 'Unsupported Operating System' }

$Architecture = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture)
{
    'X64'   { 'x64'   }
    'Arm64' { 'arm64' }
    default { throw "Unsupported Processor Architecture: $_" }
}

$AssetName        = "$($OperatingSystem.ToLowerInvariant())-$Architecture"
$ProfileName      = "$OperatingSystem.$Architecture.NativeAOT.pubxml"
$PublishDirectory = Join-Path $ProjectPath "bin/Publish/$AssetName-native-aot"

if ($IsWindows)
{
    # the .NET AOT MSBuild targets call vswhere unqualified, so it has to be on PATH for the dotnet publish child process
    if (-not (Get-Command vswhere -ErrorAction SilentlyContinue))
    {
        # install the standalone Visual Studio Locator package if vswhere is not already reachable; it is a portable winget install (no administrator permissions required)
        winget install --id Microsoft.VisualStudio.Locator --exact --silent --accept-source-agreements --accept-package-agreements

        if ($LASTEXITCODE -ne 0) { throw "winget install Microsoft.VisualStudio.Locator exited with code $LASTEXITCODE" }

        # portable winget installs add to user PATH for new shell sessions only, so reload the current session's PATH from the registry to pick up the new entry
        $env:PATH = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
    }

    # dotnet publish's native AOT link step needs MSVC's link.exe along with the MSVC runtime and Windows SDK import libraries
    # require an installation carrying the VC.Tools.x86.x64 component, which is the umbrella component that bundles all three
    # the -prerelease flag widens the search to include prerelease installs (e.g. Visual Studio Insiders) alongside stable ones rather than restricting the result set
    $VCToolsInstall = & vswhere -latest -prerelease -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath

    if ([string]::IsNullOrWhiteSpace($VCToolsInstall))
    {
        throw (@(
            'Native AOT publishing on Windows requires Visual Studio 2026 Build Tools, or any Visual Studio 2026 edition with the "Desktop Development With C++" workload.'
            ''
            'Install via winget in a PowerShell session with administrator permissions:'
            ''
            '    winget install --id Microsoft.VisualStudio.BuildTools --override "--quiet --wait --add Microsoft.VisualStudio.Workload.VCTools"'
            ''
            'Or download manually:'
            ''
            '    Visual Studio 2026 Build Tools: https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2026'
            '    Visual Studio 2026:             https://visualstudio.microsoft.com/downloads/'
        ) -join [Environment]::NewLine)
    }
}

& dotnet publish $ProjectPath "-p:PublishProfile=$ProfileName"

if ($LASTEXITCODE -ne 0) { throw "dotnet publish exited with code $LASTEXITCODE" }

Get-ChildItem -Path $PublishDirectory -Recurse -File      -Include '*.pdb', '*.dbg' | Remove-Item -Force
Get-ChildItem -Path $PublishDirectory -Recurse -Directory -Filter  '*.dSYM'         | Remove-Item -Recurse -Force

$PropsPath       = Join-Path $SolutionDirectory 'Directory.Build.props'
$ResolvedVersion = (Select-Xml -Path $PropsPath -XPath '/Project/PropertyGroup/Version').Node.InnerText
$ZipPath         = Join-Path $RepoRoot "$AssetName-v$ResolvedVersion.zip"

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

Push-Location $PublishDirectory

try
{
    if ($IsWindows)
    {
        Compress-Archive -Path .\* -DestinationPath $ZipPath
    }

    else
    {
        zip -r $ZipPath .

        if ($LASTEXITCODE -ne 0) { throw "zip exited with code $LASTEXITCODE" }
    }
}

finally
{
    Pop-Location
}

Write-Host "Published Release Asset: $ZipPath"
