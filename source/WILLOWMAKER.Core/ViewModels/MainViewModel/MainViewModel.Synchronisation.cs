namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SynchronisationIsIdle))]
    [NotifyPropertyChangedFor(nameof(MasterServerInputIsEnabled))]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool SynchronisationIsActive { get; set; } = false;

    [ObservableProperty]
    public partial bool SynchronisationIsScheduled { get; set; } = false;

    [ObservableProperty]
    public partial double SynchronisationProgressPercent { get; set; } = 0;

    [ObservableProperty]
    public partial string SynchronisationStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool SynchronisationIsFailed { get; set; } = false;

    [ObservableProperty]
    public partial string DownloadedFilesDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemovedFilesDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SkippedFilesDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UpToDateFilesDisplay { get; set; } = string.Empty;

    public bool SynchronisationIsIdle => SynchronisationIsActive is false;

    private async Task<bool> SynchroniseContent(bool skipSynchronisation = false)
    {
        if (skipSynchronisation)
        {
            Log(LogCategory.Synchronise, "SKIP: Synchronisation Skipped (Manual Override)");

            return true;
        }

        if (LocationSafetyVerdict is not LocationSafetyVerdict.Safe)
        {
            Log(LogCategory.Synchronise, "SKIP: Synchronisation Skipped (Unsafe Location)");

            return true;
        }

        SynchronisationIsActive = true;
        SynchronisationIsScheduled = false;
        SynchronisationIsFailed = false;
        SynchronisationProgressPercent = 0;
        SynchronisationStatusMessage = string.Empty;
        DownloadedFilesDisplay = string.Empty;
        RemovedFilesDisplay = string.Empty;
        SkippedFilesDisplay = string.Empty;
        UpToDateFilesDisplay = string.Empty;

        try
        {
            string variant = ContentBroker.ResolveDefaultClientVariant();

            Log(LogCategory.Synchronise, $@"INIT: Fetching Manifest For Variant ""{variant}"" From CDN");

            Manifest manifest = await ContentBroker.FetchManifest(variant);

            Log(LogCategory.Synchronise, $"INIT: Manifest Version {manifest.Version} Lists {manifest.Files.Count} File(s)");

            int filesDownloaded  = 0;
            int filesDeleted     = 0;
            int filesSkipped     = 0;
            long bytesDownloaded = 0;

            SynchronisationPlan? plan       = null;

            int upToDateTotal    = 0;

            Progress<SynchronisationEvent> progress = new (synchronisationEvent =>
            {
                switch (synchronisationEvent.Kind)
                {
                    case SynchronisationEventKind.PlanReady:
                    {
                        plan = synchronisationEvent.Plan;

                        if (plan is not null)
                        {
                            Log(LogCategory.Synchronise, $"PLAN: {plan}");

                            // The Up-To-Date Column Includes Local Deletions So All Four Columns Reach 100% Together When The Synchronisation Completes
                            upToDateTotal = manifest.Files.Count + plan.FilesToDelete;

                            DownloadedFilesDisplay = FormatSynchronisationColumn("DOWNLOADED", 0                                , plan.FilesToDownload);
                            RemovedFilesDisplay    = FormatSynchronisationColumn("REMOVED"   , 0                                , plan.FilesToDelete  );
                            SkippedFilesDisplay    = FormatSynchronisationColumn("SKIPPED"   , 0                                , plan.FilesToSkip    );
                            UpToDateFilesDisplay   = FormatSynchronisationColumn("UP-TO-DATE", plan.FilesUpToDate + filesSkipped, upToDateTotal       );

                            // Drives The Progress Bar's Visibility: A Plan With No Downloads And No Deletions Means There Is Nothing To Show Progress Of
                            SynchronisationIsScheduled = plan.FilesToDownload > 0 || plan.FilesToDelete > 0;
                        }

                        break;
                    }

                    case SynchronisationEventKind.ProgressUpdated:
                    {
                        bytesDownloaded = synchronisationEvent.Size;

                        if (plan is not null && plan.TotalBytesToDownload > 0)
                            SynchronisationProgressPercent = Math.Min(100, (double) bytesDownloaded / plan.TotalBytesToDownload * 100);

                        break;
                    }

                    case SynchronisationEventKind.Downloaded:
                    {
                        Log(LogCategory.Synchronise, $"PULL: {synchronisationEvent.Detail}");

                        filesDownloaded++;

                        if (plan is not null)
                        {
                            DownloadedFilesDisplay = FormatSynchronisationColumn("DOWNLOADED", filesDownloaded, plan.FilesToDownload);
                            UpToDateFilesDisplay   = FormatSynchronisationColumn("UP-TO-DATE", plan.FilesUpToDate + filesDownloaded + filesDeleted + filesSkipped, upToDateTotal);
                        }

                        break;
                    }

                    case SynchronisationEventKind.Deleted:
                    {
                        Log(LogCategory.Synchronise, $"NUKE: {synchronisationEvent.Detail}");

                        filesDeleted++;

                        if (plan is not null)
                        {
                            RemovedFilesDisplay  = FormatSynchronisationColumn("REMOVED", filesDeleted, plan.FilesToDelete);
                            UpToDateFilesDisplay = FormatSynchronisationColumn("UP-TO-DATE", plan.FilesUpToDate + filesDownloaded + filesDeleted + filesSkipped, upToDateTotal);
                        }

                        break;
                    }

                    case SynchronisationEventKind.DownloadFailed:
                    case SynchronisationEventKind.DeletionFailed:
                    {
                        Log(LogCategory.Synchronise, $"FAIL: {synchronisationEvent.Detail}");

                        break;
                    }

                    case SynchronisationEventKind.Skipped:
                    {
                        Log(LogCategory.Synchronise, $"SKIP: {synchronisationEvent.Detail}");

                        filesSkipped++;

                        if (plan is not null)
                        {
                            SkippedFilesDisplay  = FormatSynchronisationColumn("SKIPPED", filesSkipped, plan.FilesToSkip);
                            UpToDateFilesDisplay = FormatSynchronisationColumn("UP-TO-DATE", plan.FilesUpToDate + filesDownloaded + filesDeleted + filesSkipped, upToDateTotal);
                        }

                        break;
                    }

                    case SynchronisationEventKind.Completed:
                    {
                        SynchronisationProgressPercent = 100;

                        Log(LogCategory.Synchronise, $"DONE: {synchronisationEvent.Detail}");

                        break;
                    }
                }
            });

            SynchronisationSummary summary = await Task.Run(() => ContentBroker.Synchronise
            (
                manifest:        manifest,
                variant:         variant,
                targetDirectory: Environment.CurrentDirectory,
                progress:        progress
            ));

            if (summary.FilesFailed > 0)
            {
                SynchronisationIsFailed = true;
                SynchronisationStatusMessage = $"Synchronisation Failed: {summary.FilesFailed} File(s) Could Not Be Transferred";

                Log(LogCategory.Synchronise, $"FAIL: {summary.FilesFailed} File(s) Failed To Be Transferred :: Launch Aborted");

                // Query Locking Processes For Failed Files
                await CheckForLockingProcesses(summary.Failures);

                return false;
            }

            return true;
        }

        catch (HttpRequestException exception)
        {
            string statusCode = exception.StatusCode is not null
                ? $"{(int) exception.StatusCode} ({exception.StatusCode})"
                : "Unknown Status Code";

            Log(LogCategory.Synchronise, $"FAIL: CDN Unreachable :: HTTP {statusCode}");

            SynchronisationIsFailed = true;
            SynchronisationStatusMessage = "CDN Unreachable; Synchronisation Aborted";

            return false;
        }

        catch (Exception exception)
        {
            Log(LogCategory.Synchronise, $"FAIL: {exception.GetType().Name} :: {exception.Message}");

            SynchronisationIsFailed = true;
            SynchronisationStatusMessage = $"Synchronisation Error: {exception.Message}";

            return false;
        }

        finally
        {
            SynchronisationIsActive = false;
        }
    }

    /// <summary>
    ///     Formats a single status-row column as <c>LABEL XXX/YYY (ZZZ%)</c> with three-digit zero-padded counters for steady-width display.
    ///     A zero <paramref name="total"/> is treated as 100% complete (nothing to do is fully done).
    /// </summary>
    private static string FormatSynchronisationColumn(string label, int current, int total)
    {
        int percent = total > 0 ? (int) ((double) current / total * 100) : 100;

        return $"{label} {current:D3}/{total:D3} ({percent}%)";
    }

    private async Task CheckForLockingProcesses(IReadOnlyList<SynchronisationFailure> failures)
    {
        const string unidentifiedProcessGroup = "Unidentified Process";

        // Lock Scanning Touches The Restart Manager And The Filesystem, So It Runs Off The UI Thread To Keep The Window Responsive
        Dictionary<string, LockGroup> lockGroups = await Task.Run(() =>
        {
            Dictionary<string, LockGroup> groups = new Dictionary<string, LockGroup>(StringComparer.OrdinalIgnoreCase);

            LockGroup GroupFor(string applicationName)
            {
                if (groups.TryGetValue(applicationName, out LockGroup? group) is false)
                {
                    group = new LockGroup();

                    groups.Add(applicationName, group);
                }

                return group;
            }

            void AssignToGroup(LockGroup group, string filePath)
            {
                if (group.FilePaths.Contains(filePath) is false)
                    group.FilePaths.Add(filePath);
            }

            foreach (SynchronisationFailure failure in failures)
            {
                // Resolve The Full Path For Lock Scanning
                string absolutePath = Path.IsPathRooted(failure.Path)
                    ? failure.Path
                    : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, failure.Path));

                if (File.Exists(absolutePath) is false)
                    continue;

                // Relativise The Path For UI Display
                string displayPath = Path.GetRelativePath(Environment.CurrentDirectory, absolutePath);

                List<FileLockingProcess> lockingProcesses = FileLockDetector.GetLockingProcesses(absolutePath);

                if (lockingProcesses.Count is 0)
                {
                    // No Locking Process Was Identified, So The File Is Only Surfaced When It Is Genuinely Still Locked; This Filters Out Failures Caused By Other Reasons (Such As A Hash Mismatch Or An Unreachable CDN) While Still Reporting A Lock Whose Owner Could Not Be Determined
                    if (FileIsLocked(absolutePath))
                        AssignToGroup(GroupFor(unidentifiedProcessGroup), displayPath);

                    continue;
                }

                foreach (FileLockingProcess lockingProcess in lockingProcesses)
                {
                    // Every Instance Of The Same Executable Is Collapsed Into One Group Keyed By Its Application Name; Its Distinct Process IDs Are Counted So The User Knows How Many Instances Need To Be Closed
                    LockGroup group = GroupFor(lockingProcess.ApplicationName);

                    group.ProcessIDs.Add(lockingProcess.ProcessID);

                    AssignToGroup(group, displayPath);
                }
            }

            return groups;
        });

        if (lockGroups.Count > 0)
        {
            List<LockGroupDisplay> groups = lockGroups.Select(pair => new LockGroupDisplay
            {
                // A Process Count Is Appended Only When More Than One Instance Of The Application Is Holding The File, So The User Closes Every One Of Them
                ProcessName = pair.Value.ProcessIDs.Count > 1
                    ? $"{pair.Key} ({pair.Value.ProcessIDs.Count} Processes)"
                    : pair.Key,
                FilePaths = pair.Value.FilePaths
            }).ToList();

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null)
            {
                FileLockDialog dialog = new FileLockDialog(groups);

                await dialog.ShowDialog(desktop.MainWindow);
            }
        }
    }

    private static bool FileIsLocked(string path)
    {
        try
        {
            // Opening With No Sharing Fails When Any Other Handle To The File Is Already Open, Which Is The Defining Symptom Of A Lock Held By Another Process; Read Access Is Requested So The Read-Only Attribute Does Not Interfere
            // This Only Acquires A Handle And Never Reads The Contents, So The File's Size Has No Bearing On The Application's Performance; The Probe Is A Single Open/Close Regardless Of Whether The File Is A Few Bytes Or Many Gigabytes
            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);

            return false;
        }

        catch (IOException)
        {
            return true;
        }

        catch
        {
            return false; // Swallowed Deliberately: An Inability To Open The File For Reasons Other Than Sharing (Such As Insufficient Permissions) Must Not Be Misreported As A Lock
        }
    }

    /// <summary>
    ///     Accumulates the distinct locking process IDs and the locked file paths for a single application while the locking processes are being scanned.
    /// </summary>
    private sealed class LockGroup
    {
        public HashSet<int> ProcessIDs { get; } = new HashSet<int>();

        public List<string> FilePaths { get; } = new List<string>();
    }
}
