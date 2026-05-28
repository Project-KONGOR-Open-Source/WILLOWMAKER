#Requires -Version 7.0

# local equivalent of .github/workflows/publish-release.yml
# detects the current OS and CPU architecture, picks the matching publish profile, runs `dotnet publish` against the WILLOWMAKER project, strips debug artefacts, and zips the output to the repo root using the same naming scheme the pipeline uses for that platform
# the version stamped into the binary and zip filename comes from <Version> in source/Directory.Build.props with no override, matching what the pipeline would produce for a tag pushed at the same version

$ErrorActionPreference = 'Stop'

# resolving paths from $PSCommandPath instead of the current working directory means the script behaves the same whether invoked from the repo root, from scripts/, or with an absolute path
$RepoRoot          = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$SolutionDirectory = Join-Path $RepoRoot 'source'
$ProjectPath       = Join-Path $SolutionDirectory 'WILLOWMAKER'

# publish profile filenames preserve OS case (Windows, Linux, macOS) while asset and publish-dir names are all lowercase
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

# native AOT on Windows shells out to vswhere.exe to locate link.exe from the installed Visual Studio toolchain; GitHub-hosted Windows runners have vswhere on PATH by default but most local PowerShell sessions do not, so prepend the standard VS Installer directory when it is present
if ($IsWindows -and -not (Get-Command vswhere -ErrorAction SilentlyContinue))
{
    $VSInstallerDirectory = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'
    if (Test-Path (Join-Path $VSInstallerDirectory 'vswhere.exe'))
    {
        $env:PATH = "$VSInstallerDirectory;$env:PATH"
    }
}

& dotnet publish $ProjectPath "-p:PublishProfile=$ProfileName"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish exited with code $LASTEXITCODE" }

# strip debug artefacts before zipping to keep the release small, mirroring the pipeline
# .pdb files on Windows, .dbg files on Linux, .dSYM bundles (which are directories) on macOS
Get-ChildItem -Path $PublishDirectory -Recurse -File      -Include '*.pdb', '*.dbg' | Remove-Item -Force
Get-ChildItem -Path $PublishDirectory -Recurse -Directory -Filter  '*.dSYM'         | Remove-Item -Recurse -Force

$PropsPath       = Join-Path $SolutionDirectory 'Directory.Build.props'
$ResolvedVersion = (Select-Xml -Path $PropsPath -XPath '/Project/PropertyGroup/Version').Node.InnerText
$ZipPath         = Join-Path $RepoRoot "$AssetName-v$ResolvedVersion.zip"

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

# Compress-Archive on Windows works without requiring zip(1) on PATH; on Linux/macOS the external zip preserves the executable bit which Compress-Archive would otherwise drop, causing the unzipped binary to refuse to launch without a manual chmod
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
