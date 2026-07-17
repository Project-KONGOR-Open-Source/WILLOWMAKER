namespace WILLOWMAKER.Core.Services;

/// <summary>
///     Synchronises a local installation directory with a content bucket described by a remote manifest.
///     The local tree becomes an exact mirror of the manifest's file list.
///     Files matching <see cref="Manifest.ExcludeFromSource"/> are remote-side paths that must not be downloaded (e.g. the manifest itself).
///     Files matching <see cref="Manifest.ExcludeFromTarget"/> are local-side paths that must not be changed in any way (e.g. the launcher's own files). They are neither overwritten by downloads nor removed by deletions.
/// </summary>
public static class ContentBroker
{
    private const string DefaultBaseURL           = "https://cdn.kongor.net/";
    private const string ManifestFileName         = "manifest.json";
    private const string PartialDownloadSuffix    = ".partial";
    private const int    DefaultParallelTransfers = 8;

    /// <summary>
    ///     Downloads and parses the manifest for the given <paramref name="variant"/> from the CDN.
    /// </summary>
    public static async Task<Manifest> FetchManifest(string variant, string baseURL = DefaultBaseURL, CancellationToken cancellationToken = default)
    {
        string manifestURL = BuildManifestURL(baseURL, variant);

        using HttpClient client = CreateClient(timeout: TimeSpan.FromSeconds(30));

        Stream stream = await client.GetStreamAsync(manifestURL, cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            Manifest? manifest = await JsonSerializer.DeserializeAsync(stream, ManifestJSONContext.Default.Manifest, cancellationToken).ConfigureAwait(false);

            return manifest ?? throw new InvalidOperationException($@"Manifest At ""{manifestURL}"" Deserialised To NULL");
        }
    }

    /// <summary>
    ///     Mirrors the manifest's file list into <paramref name="targetDirectory"/>: downloads missing or mismatched files, and removes local files that are not in the manifest.
    ///     For each manifest entry the download pass asks two questions in order: first, may the file be fetched from the bucket (a remote-side check against <see cref="Manifest.ExcludeFromSource"/>); second, would writing it change a local path that must not be changed (a local-side check against <see cref="Manifest.ExcludeFromTarget"/>).
    ///     The deletion pass asks the local-side question alone, against <see cref="Manifest.ExcludeFromTarget"/>.
    ///     Leftover partial files from an interrupted run are removed up-front, before any downloads begin.
    ///     Downloads run in parallel and are written atomically via a <c>.partial</c> temporary file that is renamed on success.
    /// </summary>
    public static async Task<SynchronisationSummary> Synchronise
    (
        Manifest manifest,
        string variant,
        string targetDirectory,
        string baseURL = DefaultBaseURL,
        int parallelTransfers = DefaultParallelTransfers,
        IProgress<SynchronisationEvent>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(variant);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        if (parallelTransfers < 1)
            throw new ArgumentOutOfRangeException(nameof(parallelTransfers), parallelTransfers, "At Least One Parallel Transfer Is Required");

        Directory.CreateDirectory(targetDirectory);

        Matcher sourceExclusions = BuildMatcher(manifest.ExcludeFromSource);
        Matcher targetExclusions = BuildMatcher(manifest.ExcludeFromTarget);

        // First Pass: Decide Which Files Listed In The Manifest Need To Be Downloaded

        List<PendingDownload> pendingDownloads = new ();

        long totalBytesToDownload              = 0;
        int filesUpToDate                      = 0;
        int filesToSkip                        = 0;

        foreach ((string rawRelativePath, ManifestEntry entry) in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = NormaliseRelativePath(rawRelativePath);

            // Decision Gate One (Remote Side): May We Download This File From The Bucket?
            if (MatchesAny(relativePath, sourceExclusions))
            {
                filesToSkip++;

                progress?.Report(new SynchronisationEvent(SynchronisationEventKind.Skipped, relativePath, entry.Size));

                continue;
            }

            // Decision Gate Two (Local Side): Would Writing It Change A Local File That Must Not Be Changed?
            if (MatchesAny(relativePath, targetExclusions))
            {
                filesToSkip++;

                progress?.Report(new SynchronisationEvent(SynchronisationEventKind.Skipped, relativePath, entry.Size));

                continue;
            }

            string localPath = Path.Combine(targetDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (await LocalFileMatchesManifestEntry(localPath, entry, cancellationToken).ConfigureAwait(false))
            {
                filesUpToDate++;

                continue;
            }

            pendingDownloads.Add(new PendingDownload(relativePath, localPath, entry));
            totalBytesToDownload += entry.Size;
        }

        // Second Pass: Decide Which Local Files Are No Longer In The Manifest And Should Be Deleted

        HashSet<string> expectedRelativePaths = new (manifest.Files.Keys.Select(NormaliseRelativePath), PathComparer);

        List<string> pendingDeletions = new ();

        int filesDeleted = 0;
        int filesFailed  = 0;

        ConcurrentBag<SynchronisationFailure> failures = new ();

        if (Directory.Exists(targetDirectory))
        {
            foreach (string fullPath in Directory.EnumerateFiles(targetDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = NormaliseRelativePath(Path.GetRelativePath(targetDirectory, fullPath));

                // A Leftover Partial From An Interrupted Run Is Removed Immediately, Before Any Downloads Begin: A Re-Download Of The Same File Renames Its Fresh Partial Into Place, So A Deferred Deletion Of The Old Partial Would Find The Path Already Gone And Report A Spurious Failure
                if (relativePath.EndsWith(PartialDownloadSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        ForceDelete(fullPath);

                        filesDeleted++;

                        progress?.Report(new SynchronisationEvent(SynchronisationEventKind.Deleted, relativePath, 0));
                    }

                    catch (Exception exception)
                    {
                        filesFailed++;

                        failures.Add(new SynchronisationFailure(fullPath, exception.Message));

                        progress?.Report(new SynchronisationEvent(SynchronisationEventKind.DeletionFailed, $@"{fullPath}: {exception.Message}", 0));
                    }

                    continue;
                }

                if (expectedRelativePaths.Contains(relativePath))
                    continue;

                if (MatchesAny(relativePath, targetExclusions))
                    continue;

                pendingDeletions.Add(fullPath);
            }
        }

        SynchronisationPlan plan = new
        (
            FilesToDownload:      pendingDownloads.Count,
            FilesToDelete:        filesDeleted + pendingDeletions.Count,
            FilesToSkip:          filesToSkip,
            FilesUpToDate:        filesUpToDate,
            TotalBytesToDownload: totalBytesToDownload
        );

        progress?.Report(new SynchronisationEvent(SynchronisationEventKind.PlanReady, plan.ToString(), totalBytesToDownload, plan));

        using HttpClient client = CreateClient(timeout: Timeout.InfiniteTimeSpan);
        using SemaphoreSlim semaphore = new (parallelTransfers, parallelTransfers);

        int filesDownloaded                = 0;
        long bytesDownloaded               = 0;

        long totalBytesDownloaded          = 0;
        long lastReportTicks               = 0;

        Action<int> reportBytesRead = bytesRead =>
        {
            long currentTotal = Interlocked.Add(ref totalBytesDownloaded, bytesRead);
            long now          = Environment.TickCount64;
            long last         = Volatile.Read(ref lastReportTicks);

            if (now - last > 100)
            {
                if (Interlocked.CompareExchange(ref lastReportTicks, now, last) == last)
                {
                    progress?.Report(new SynchronisationEvent(SynchronisationEventKind.ProgressUpdated, string.Empty, currentTotal));
                }
            }
        };

        IEnumerable<Task> downloadTasks = pendingDownloads.Select(async download =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                progress?.Report(new SynchronisationEvent(SynchronisationEventKind.DownloadStarted, download.RelativePath, download.Entry.Size));

                await DownloadOne(client, baseURL, variant, download, manifest.HashAlgorithm, reportBytesRead, cancellationToken).ConfigureAwait(false);

                Interlocked.Add(ref bytesDownloaded, download.Entry.Size);
                Interlocked.Increment(ref filesDownloaded);

                progress?.Report(new SynchronisationEvent(SynchronisationEventKind.Downloaded, download.RelativePath, download.Entry.Size));
            }

            catch (OperationCanceledException)
            {
                throw;
            }

            catch (Exception exception)
            {
                Interlocked.Increment(ref filesFailed);
                failures.Add(new SynchronisationFailure(download.RelativePath, exception.Message));

                progress?.Report(new SynchronisationEvent(SynchronisationEventKind.DownloadFailed, $@"{download.RelativePath}: {exception.Message}", download.Entry.Size));
            }

            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(downloadTasks).ConfigureAwait(false);

        // Report The Final Absolute Total Downloaded Bytes
        progress?.Report(new SynchronisationEvent(SynchronisationEventKind.ProgressUpdated, string.Empty, bytesDownloaded));

        foreach (string fullPath in pendingDeletions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                ForceDelete(fullPath);

                filesDeleted++;

                progress?.Report(new SynchronisationEvent(SynchronisationEventKind.Deleted, NormaliseRelativePath(Path.GetRelativePath(targetDirectory, fullPath)), 0));
            }

            catch (Exception exception)
            {
                filesFailed++;

                failures.Add(new SynchronisationFailure(fullPath, exception.Message));

                progress?.Report(new SynchronisationEvent(SynchronisationEventKind.DeletionFailed, $@"{fullPath}: {exception.Message}", 0));
            }
        }

        RemoveEmptyDirectories(targetDirectory);

        SynchronisationSummary summary = new
        (
            FilesDownloaded: filesDownloaded,
            FilesDeleted:    filesDeleted,
            FilesUpToDate:   filesUpToDate,
            FilesFailed:     filesFailed,
            BytesDownloaded: bytesDownloaded,
            Failures:        [.. failures]
        );

        progress?.Report(new SynchronisationEvent(SynchronisationEventKind.Completed, summary.ToString(), bytesDownloaded));

        return summary;
    }

    private static async Task DownloadOne(HttpClient client, string baseURL, string variant, PendingDownload download, string hashAlgorithm, Action<int>? progressCallback, CancellationToken cancellationToken)
    {
        string fileURL    = BuildFileURL(baseURL, variant, download.RelativePath);
        string partialPath = download.LocalPath + PartialDownloadSuffix;

        string? parentDirectory = Path.GetDirectoryName(download.LocalPath);

        if (string.IsNullOrEmpty(parentDirectory) is false)
            Directory.CreateDirectory(parentDirectory);

        long bytesWritten = 0;
        string actualHashHex;

        using (IncrementalHash incrementalHash = CreateIncrementalHash(hashAlgorithm))
        {
            FileStream fileStream = new (partialPath, FileMode.Create, FileAccess.Write, FileShare.None);

            await using (fileStream.ConfigureAwait(false))
            {
                Stream downloadStream = await client.GetStreamAsync(fileURL, cancellationToken).ConfigureAwait(false);

                await using (downloadStream.ConfigureAwait(false))
                {
                    byte[] buffer = new byte[81920];

                    while (true)
                    {
                        int bytesRead = await downloadStream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);

                        if (bytesRead is 0)
                            break;

                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

                        incrementalHash.AppendData(buffer, 0, bytesRead);

                        bytesWritten += bytesRead;

                        progressCallback?.Invoke(bytesRead);
                    }
                }
            }

            actualHashHex = Convert.ToHexStringLower(incrementalHash.GetHashAndReset());
        }

        if (bytesWritten != download.Entry.Size)
        {
            TryDeleteQuietly(partialPath);

            throw new IOException($@"Size Mismatch For ""{download.RelativePath}"" (Expected {download.Entry.Size:N0}, Got {bytesWritten:N0})");
        }

        if (string.Equals(actualHashHex, download.Entry.Hash, StringComparison.OrdinalIgnoreCase) is false)
        {
            TryDeleteQuietly(partialPath);

            throw new IOException($@"Hash Mismatch For ""{download.RelativePath}"" (Expected ""{download.Entry.Hash}"", Got ""{actualHashHex}"")");
        }

        if (File.Exists(download.LocalPath))
            File.Delete(download.LocalPath);

        File.Move(partialPath, download.LocalPath);
    }

    private static async Task<bool> LocalFileMatchesManifestEntry(string localPath, ManifestEntry entry, CancellationToken cancellationToken)
    {
        FileInfo info = new (localPath);

        if (info.Exists is false)
            return false;

        if (info.Length != entry.Size)
            return false;

        FileStream fileStream = File.OpenRead(localPath);

        await using (fileStream.ConfigureAwait(false))
        {
            using SHA256 sha256 = SHA256.Create();

            byte[] hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken).ConfigureAwait(false);

            string actualHashHex = Convert.ToHexStringLower(hashBytes);

            return string.Equals(actualHashHex, entry.Hash, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IncrementalHash CreateIncrementalHash(string hashAlgorithm)
    {
        return hashAlgorithm.ToUpperInvariant() switch
        {
            "SHA256" or "SHA-256" => IncrementalHash.CreateHash(HashAlgorithmName.SHA256),
            "SHA384" or "SHA-384" => IncrementalHash.CreateHash(HashAlgorithmName.SHA384),
            "SHA512" or "SHA-512" => IncrementalHash.CreateHash(HashAlgorithmName.SHA512),
            "SHA1"   or "SHA-1"   => IncrementalHash.CreateHash(HashAlgorithmName.SHA1),
            "MD5"    or "MD-5"    => IncrementalHash.CreateHash(HashAlgorithmName.MD5),
            _                     => throw new NotSupportedException($@"Unsupported Manifest Hash Algorithm ""{hashAlgorithm}""")
        };
    }

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        HttpClient client = new ();

        client.DefaultRequestHeaders.UserAgent.ParseAdd($"WILLOWMAKER/{VersionChecker.CurrentVersionDisplay}");
        client.Timeout = timeout;

        return client;
    }

    private static string BuildManifestURL(string baseURL, string variant)
    {
        string normalisedBase = baseURL.EndsWith('/') ? baseURL : baseURL + '/';

        return $"{normalisedBase}{Uri.EscapeDataString(variant)}/{ManifestFileName}";
    }

    private static string BuildFileURL(string baseURL, string variant, string relativePath)
    {
        string normalisedBase  = baseURL.EndsWith('/') ? baseURL : baseURL + '/';
        string escapedVariant  = Uri.EscapeDataString(variant);
        string escapedRelative = string.Join('/', relativePath.Split('/').Select(Uri.EscapeDataString));

        return $"{normalisedBase}{escapedVariant}/{escapedRelative}";
    }

    private static string NormaliseRelativePath(string relativePath)
        => relativePath.Replace('\\', '/');

    // The Deletion Pass Must Mirror The Filesystem's Own Case Sensitivity: A Fixed Case-Insensitive Comparer Would Wrongly Keep A Stale File That Differs From A Manifest Entry Only In Case On A Case-Sensitive Filesystem
    private static StringComparer PathComparer =>
          OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase
        : OperatingSystem.IsMacOS()   ? StringComparer.OrdinalIgnoreCase
        : OperatingSystem.IsLinux()   ? StringComparer.Ordinal
        : throw new PlatformNotSupportedException($@"Unsupported Operating System: {Environment.OSVersion.Platform}");

    private static bool MatchesAny(string relativePath, Matcher matcher)
        => matcher.Match(relativePath).HasMatches;

    private static Matcher BuildMatcher(IReadOnlyList<string> patterns)
    {
        Matcher matcher = new (StringComparison.OrdinalIgnoreCase);

        foreach (string pattern in patterns)
            matcher.AddInclude(pattern);

        return matcher;
    }

    private static void RemoveEmptyDirectories(string targetDirectory)
    {
        if (Directory.Exists(targetDirectory) is false)
            return;

        // Walk Bottom-Up So Inner Empty Directories Are Removed Before Their Now-Empty Parents
        foreach (string directory in Directory.EnumerateDirectories(targetDirectory, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
        {
            try
            {
                if (Directory.EnumerateFileSystemEntries(directory).Any() is false)
                    Directory.Delete(directory, recursive: false);
            }

            catch
            {
                // Best-Effort Only: A Failure To Remove An Empty Directory Is Not A Synchronisation Failure
            }
        }
    }

    private static void TryDeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        catch
        {
            // Swallowed Deliberately: Cleanup Of A Failed Download's Partial File Must Not Mask The Original Error
        }
    }

    private static void ForceDelete(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);

        // "File.Delete" Throws "UnauthorizedAccessException" When The Target Carries The Read-Only Attribute, So We Clear The Attribute First
        if (attributes.HasFlag(FileAttributes.ReadOnly))
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);

        File.Delete(path);
    }

    private sealed record PendingDownload(string RelativePath, string LocalPath, ManifestEntry Entry);
}
