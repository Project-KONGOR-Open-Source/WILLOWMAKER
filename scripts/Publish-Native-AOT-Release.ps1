#Requires -Version 7.0

# local equivalent of .github/workflows/publish-release.yml
# requires PowerShell Core minimum version 7.0

$ErrorActionPreference = 'Stop'

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

if ($IsWindows -and -not (Get-Command vswhere -ErrorAction SilentlyContinue))
{
    $VSInstallerDirectory = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'

    if (Test-Path (Join-Path $VSInstallerDirectory 'vswhere.exe'))
    {
        $env:PATH = "$VSInstallerDirectory;$env:PATH"
    }

    else
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
