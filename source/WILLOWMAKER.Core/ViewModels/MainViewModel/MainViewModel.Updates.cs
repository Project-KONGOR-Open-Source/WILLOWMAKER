namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private async Task CheckForUpdates()
    {
        Log(LogCategory.Version, $"Current Version: {VersionChecker.CurrentVersionDisplay}");

        Log(LogCategory.Version, "Checking For Updates ...");

        VersionCheckResult result;

        try
        {
            result = await VersionChecker.CheckForLatestVersion();
        }

        catch (HttpRequestException httpException)
        {
            string statusCode = httpException.StatusCode is not null
                ? $"{(int)httpException.StatusCode} ({httpException.StatusCode})"
                : "Unknown Status Code";

            Log(LogCategory.Version, $"The WILLOWMAKER Releases Repository Is Not Reachable: HTTP {statusCode}");

            UpdateStatus = UpdateStatus.RepositoryUnreachable;

            return;
        }

        catch (Exception exception)
        {
            Log(LogCategory.Version, $"The WILLOWMAKER Releases Repository Is Not Reachable: {exception.GetType().Name}");

            UpdateStatus = UpdateStatus.RepositoryUnreachable;

            return;
        }

        if (result.IsUpdateAvailable is false || result.LatestVersion is null)
        {
            Log(LogCategory.Version, "WILLOWMAKER Is Up To Date");

            UpdateStatus = UpdateStatus.ApplicationUpToDate;

            return;
        }

        LatestAvailableVersionDisplay = $"v{result.LatestVersion.Major}.{result.LatestVersion.Minor}.{result.LatestVersion.Build}";

        UpdateStatus = UpdateStatus.UpdateAvailable;

        Log(LogCategory.Version, $"Update Available: {LatestAvailableVersionDisplay}");

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
            return;

        UpdateDialog dialog = new ($"WILLOWMAKER {LatestAvailableVersionDisplay} Is Available");

        bool shouldUpdate = await dialog.ShowDialog<bool>(desktop.MainWindow);

        if (shouldUpdate is false)
        {
            Log(LogCategory.Update, "Update Skipped By User");

            return;
        }

        Log(LogCategory.Update, "Update Accepted By User");

        if (result.DownloadURL is null)
        {
            Log(LogCategory.Update, "No Downloadable Asset Found; Opening The Releases Page ...");

            Process.Start(new ProcessStartInfo { FileName = result.ReleasePageURL, UseShellExecute = true });

            return;
        }

        try
        {
            Log(LogCategory.Update, $"Downloading Update From {result.DownloadURL} ...");

            UpdateDownloadPercent = 0;
            UpdateStatus = UpdateStatus.UpdateDownloading;

            Progress<double> downloadProgress = new (percent => UpdateDownloadPercent = percent);

            string archivePath = await VersionChecker.DownloadUpdate(result.DownloadURL, downloadProgress);

            // Force The 100% Frame To Paint Before The State Flips Away From Downloading
            // The Last Progress Report Can Be Coalesced With The State Change, So Without This Hold The User Sees A Stale Sub-100% Value Right Up To The Window Closing
            UpdateDownloadPercent = 100;

            await Task.Delay(TimeSpan.FromMilliseconds(100));

            Log(LogCategory.Update, "Restarting Into The Update Script ...");

            UpdateStatus = UpdateStatus.UpdateRestarting;

            VersionChecker.ApplyUpdateAndRestart(archivePath);
        }

        catch (Exception exception)
        {
            Log(LogCategory.Update, $"Update Failed: {exception.Message}");

            UpdateStatus = UpdateStatus.UpdateAvailable;
        }
    }
}
