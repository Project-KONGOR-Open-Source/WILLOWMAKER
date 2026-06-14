namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
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
}
