namespace WILLOWMAKER.Core.Services;

/// <summary>
///     Provides version checking against GitHub releases, asset downloading, and self-update capabilities.
/// </summary>
public static partial class VersionChecker
{
    // The "/releases/latest" Endpoint Is Chosen Deliberately: GitHub Defines It As The Most Recent Non-Pre-Release, Non-Draft Release, So Stable Users Only Get Newer Stables And Pre-Release Users Only Get The Next Stable
    // See https://docs.github.com/en/rest/releases/releases#get-the-latest-release
    // Swapping To "/releases" Would Include Pre-Releases
    private const string LatestReleaseURL = "https://api.github.com/repos/Project-KONGOR-Open-Source/WILLOWMAKER/releases/latest";

    /// <summary>
    ///     Matches a version string of the form <c>[v]Major.Minor.Patch[suffix]</c>, where the leading <c>v</c> and the trailing suffix (e.g. a Semantic Versioning pre-release like <c>-alpha-2</c> or build metadata like <c>+abc123</c>) are both optional.
    ///     Exposes the numeric <c>Major.Minor.Patch</c> portion through the <c>numeric</c> named group.
    /// </summary>
    [GeneratedRegex(@"^v?(?<numeric>\d+\.\d+\.\d+)(?<suffix>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    /// <summary>
    ///     The current application version, parsed from the build-time generated version constant. Any Semantic Versioning suffix is discarded because <see cref="System.Version"/> accepts only the numeric <c>Major.Minor[.Build[.Revision]]</c> form.
    /// </summary>
    public static Version CurrentVersion { get; } = ParseNumericVersion(GeneratedVersionInformation.VersionString);

    /// <summary>
    ///     A display-friendly string for the current version (e.g. "v1.2.0").
    /// </summary>
    public static string CurrentVersionDisplay
        => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

    /// <summary>
    ///     Queries the GitHub releases API for the latest published version of WILLOWMAKER.
    /// </summary>
    public static async Task<VersionCheckResult> CheckForLatestVersion()
    {
        using HttpClient client = new ();

        client.DefaultRequestHeaders.UserAgent.ParseAdd(DeploymentManifest.ApplicationName);
        client.Timeout = TimeSpan.FromSeconds(10);

        string json = await GetWithTransientRetry(client, LatestReleaseURL);

        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement root = document.RootElement;

        string tagName = root.GetProperty("tag_name").GetString() ?? string.Empty;
        string releasePageURL = root.GetProperty("html_url").GetString() ?? string.Empty;

        // Defensive Check In Case A Future API Change Breaks The "/releases/latest" Contract
        bool isPreRelease = root.TryGetProperty("prerelease", out JsonElement prereleaseElement) && prereleaseElement.GetBoolean();

        if (isPreRelease)
            return new VersionCheckResult(false, null, null, releasePageURL);

        if (TryParseNumericVersion(tagName, out Version? latestVersion) is false)
            return new VersionCheckResult(false, null, null, releasePageURL);

        bool updateIsAvailable = latestVersion > CurrentVersion;

        string? downloadURL = null;

        if (updateIsAvailable && root.TryGetProperty("assets", out JsonElement assets))
        {
            string platformKeyword = OperatingSystem.IsWindows() ? "windows"
                                   : OperatingSystem.IsMacOS()   ? "macos"
                                   : OperatingSystem.IsLinux()   ? "linux"
                                   : throw new ArgumentOutOfRangeException(nameof(platformKeyword), $@"Unsupported Operating System: {Environment.OSVersion.Platform}");

            foreach (JsonElement asset in assets.EnumerateArray())
            {
                string? assetName = asset.GetProperty("name").GetString();

                if (assetName?.Contains(platformKeyword, StringComparison.OrdinalIgnoreCase) is true && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    downloadURL = asset.GetProperty("browser_download_url").GetString();

                    break;
                }
            }
        }

        return new VersionCheckResult(updateIsAvailable, latestVersion, downloadURL, releasePageURL);
    }

    /// <summary>
    ///     Downloads a release asset from the specified URL to a temporary file and returns its path.
    /// </summary>
    public static async Task<string> DownloadUpdate(string downloadURL)
    {
        string archivePath = Path.Combine(Path.GetTempPath(), DeploymentManifest.UpdateArchiveFileName);

        using HttpClient client = new ();

        client.DefaultRequestHeaders.UserAgent.ParseAdd(DeploymentManifest.ApplicationName);

        await using FileStream fileStream = File.Create(archivePath);
        await using Stream downloadStream = await client.GetStreamAsync(downloadURL);

        await downloadStream.CopyToAsync(fileStream);

        return archivePath;
    }

    /// <summary>
    ///     Extracts the downloaded archive, spawns a platform-appropriate update script, and exits the current process.
    /// </summary>
    public static void ApplyUpdateAndRestart(string archivePath)
    {
        string targetDirectory = AppContext.BaseDirectory;
        string tempDirectory = Path.Combine(Path.GetTempPath(), DeploymentManifest.UpdateExtractDirectoryName);

        if (Directory.Exists(tempDirectory))
            Directory.Delete(tempDirectory, recursive: true);

        ZipFile.ExtractToDirectory(archivePath, tempDirectory);

        string[] topLevelEntries = Directory.GetFileSystemEntries(tempDirectory);

        if (topLevelEntries.Length is 1 && Directory.Exists(topLevelEntries[0]))
            tempDirectory = topLevelEntries[0];

        string executablePath = Environment.ProcessPath ?? Path.Combine(targetDirectory, DeploymentManifest.ApplicationExecutableFileName);

        // Enumerate The Files The New Release Ships So The Update Script Can Force-Delete Each Pre-Existing Counterpart (Including Read-Only Ones) Before Copying The New Files Into Place
        string[] relativePathsToReplace = Directory
            .EnumerateFiles(tempDirectory, "*", SearchOption.AllDirectories)
            .Select(absolutePath => Path.GetRelativePath(tempDirectory, absolutePath))
            .ToArray();

        if (OperatingSystem.IsWindows())
            SpawnWindowsUpdateScript(archivePath, tempDirectory, targetDirectory, executablePath, relativePathsToReplace);

        else if (OperatingSystem.IsMacOS())
            SpawnMacOSUpdateScript(archivePath, tempDirectory, targetDirectory, executablePath, relativePathsToReplace);

        else
            SpawnLinuxUpdateScript(archivePath, tempDirectory, targetDirectory, executablePath, relativePathsToReplace);

        Environment.Exit(0);
    }

    /// <summary>
    ///     Performs an HTTP GET against the specified URL, retrying transient failures with exponential backoff before surfacing the final exception.
    ///     Connection-level errors, request timeouts, HTTP 429 (rate limited by GitHub's secondary limiter), and 5xx server errors are treated as transient; every other failure is rethrown immediately because retrying would not change the outcome.
    /// </summary>
    private static async Task<string> GetWithTransientRetry(HttpClient client, string url)
    {
        TimeSpan[] backoffs = [ TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) ];

        for (int attemptIndex = 0; ; attemptIndex++)
        {
            try
            {
                return await client.GetStringAsync(url);
            }

            catch (Exception exception) when (attemptIndex < backoffs.Length && IsTransientFailure(exception))
            {
                await Task.Delay(backoffs[attemptIndex]);
            }
        }
    }

    /// <summary>
    ///     Classifies whether an exception thrown by an HTTP request is transient (worth retrying) or terminal (worth surfacing).
    /// </summary>
    private static bool IsTransientFailure(Exception exception) => exception switch
    {
        TaskCanceledException              => true,
        HttpRequestException httpException => httpException.StatusCode is null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError,
        _                                  => false
    };

    /// <summary>
    ///     Parses the numeric <c>Major.Minor.Patch</c> portion of a version string, tolerating an optional leading <c>v</c> and any trailing Semantic Versioning suffix.
    ///     Throws <see cref="FormatException"/> if the input does not contain a parseable numeric version.
    /// </summary>
    private static Version ParseNumericVersion(string versionString)
    {
        if (TryParseNumericVersion(versionString, out Version? version) is false)
            throw new FormatException($@"Version String ""{versionString}"" Does Not Match The Expected Format: 1) Optional ""v"" Prefix, 2) Then Major.Minor.Patch, 3) Then Optional Suffix");

        return version;
    }

    /// <summary>
    ///     Tries to parse the numeric <c>Major.Minor.Patch</c> portion of a version string, tolerating an optional leading <c>v</c> and any trailing Semantic Versioning suffix.
    ///     Returns <see langword="false"/> and sets <paramref name="version"/> to <see langword="null"/> on failure.
    /// </summary>
    private static bool TryParseNumericVersion(string versionString, [NotNullWhen(true)] out Version? version)
    {
        Match match = VersionRegex().Match(versionString);

        if (match.Success is false)
        {
            version = null;

            return false;
        }

        return Version.TryParse(match.Groups["numeric"].Value, out version);
    }

    private static void SpawnWindowsUpdateScript(string archivePath, string sourceDirectory, string targetDirectory, string executablePath, string[] relativePathsToReplace)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), "WILLOWMAKER-update.ps1");

        // The Relative Paths Are Embedded As A PowerShell Single-Quoted Array Literal So Each One Can Be Force-Deleted Before The New Files Are Copied In
        string pathArrayLiteral = string.Join(", ", relativePathsToReplace.Select(relativePath => $"'{relativePath}'"));

        string script =
        $$"""
            Start-Sleep -Milliseconds 3500
            $relativePathsToReplace = @({{pathArrayLiteral}})
            foreach ($relativePath in $relativePathsToReplace) {
                $stalePath = Join-Path '{{targetDirectory}}' $relativePath
                if (Test-Path -LiteralPath $stalePath) {
                    $staleItem = Get-Item -LiteralPath $stalePath -Force
                    if ($staleItem.Attributes -band [System.IO.FileAttributes]::ReadOnly) {
                        $staleItem.Attributes = $staleItem.Attributes -band -bnot [System.IO.FileAttributes]::ReadOnly
                    }
                    Remove-Item -LiteralPath $stalePath -Force
                }
            }
            Copy-Item -Path '{{sourceDirectory}}\*' -Destination '{{targetDirectory}}' -Recurse -Force
            Start-Process -FilePath '{{executablePath}}'
            Remove-Item -Path '{{sourceDirectory}}' -Recurse -Force
            Remove-Item -Path '{{archivePath}}' -Force
            Remove-Item -Path $MyInvocation.MyCommand.Source -Force
        """;

        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $@"-ExecutionPolicy Bypass -NoProfile -File ""{scriptPath}""",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static void SpawnMacOSUpdateScript(string archivePath, string sourceDirectory, string targetDirectory, string executablePath, string[] relativePathsToReplace)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), "WILLOWMAKER-update.sh");

        // The Relative Paths Are Embedded As A Bash Single-Quoted Word List So Each One Can Be Force-Deleted Before The New Files Are Copied In
        string pathWordList = string.Join(" ", relativePathsToReplace.Select(relativePath => $"'{relativePath}'"));

        string script =
        $"""
            #!/bin/bash
            sleep 3.5
            for relativePath in {pathWordList}; do
                stalePath="{targetDirectory}/$relativePath"
                if [ -e "$stalePath" ]; then
                    chmod u+w "$stalePath" 2>/dev/null || true
                    rm -f "$stalePath"
                fi
            done
            cp -Rf "{sourceDirectory}/." "{targetDirectory}/"
            chmod +x "{executablePath}"
            open "{executablePath}"
            rm -rf "{sourceDirectory}"
            rm "{archivePath}"
            rm "$0"
        """;

        File.WriteAllText(scriptPath, script);

        Process.Start("chmod", $@"+x ""{scriptPath}""")?.WaitForExit();

        Process.Start(new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = scriptPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static void SpawnLinuxUpdateScript(string archivePath, string sourceDirectory, string targetDirectory, string executablePath, string[] relativePathsToReplace)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), "WILLOWMAKER-update.sh");

        // The Relative Paths Are Embedded As A Bash Single-Quoted Word List So Each One Can Be Force-Deleted Before The New Files Are Copied In
        string pathWordList = string.Join(" ", relativePathsToReplace.Select(relativePath => $"'{relativePath}'"));

        string script =
        $"""
            #!/bin/bash
            sleep 3.5
            for relativePath in {pathWordList}; do
                stalePath="{targetDirectory}/$relativePath"
                if [ -e "$stalePath" ]; then
                    chmod u+w "$stalePath" 2>/dev/null || true
                    rm -f "$stalePath"
                fi
            done
            cp -rf "{sourceDirectory}/." "{targetDirectory}/"
            chmod +x "{executablePath}"
            "{executablePath}" &
            rm -rf "{sourceDirectory}"
            rm "{archivePath}"
            rm "$0"
        """;

        File.WriteAllText(scriptPath, script);

        Process.Start("chmod", $@"+x ""{scriptPath}""")?.WaitForExit();

        Process.Start(new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = scriptPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}

/// <summary>
///     Represents the result of a version check against the GitHub releases API.
/// </summary>
public sealed record VersionCheckResult(bool IsUpdateAvailable, Version? LatestVersion, string? DownloadURL, string? ReleasePageURL);
