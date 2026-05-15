namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Logger _logger = new (Path.Combine(Environment.CurrentDirectory, "WILLOWMAKER.log"));

    [ObservableProperty]
    private string? _gitHubLink = "https://github.com/Project-KONGOR-Open-Source";

    [ObservableProperty]
    private string? _webPortalLink = "https://kongor.net";

    [ObservableProperty]
    private string? _redditLink = "https://www.reddit.com/r/newerth";

    [ObservableProperty]
    private string? _discordLink = "https://discord.com/invite/N6pKzGDqUH";

    [ObservableProperty]
    private string? _elementLink = "https://app.element.io/#/room/#newerth:matrix.org";

    [ObservableProperty]
    private ComboBoxItem? _masterServerAddress = new () { Content = "api.kongor.net" }; // Needs To Match The Default Value In The XAML

    [ObservableProperty]
    private string? _customMasterServerAddress;

    [ObservableProperty]
    private bool _canShowCustomMasterServerAddressField = false;

    [ObservableProperty]
    private bool _canLaunchGame = true;

    [ObservableProperty]
    private string? _logTextArea;

    [ObservableProperty]
    private int _caretIndexForAutoScroll = int.MaxValue;

    [ObservableProperty]
    private string _versionDisplay = VersionChecker.CurrentVersionDisplay;

    [ObservableProperty]
    private bool _releasesRepositoryIsUnreachable = false;

    [ObservableProperty]
    private bool _syncIsActive = false;

    [ObservableProperty]
    private double _syncProgressPercent = 0;

    [ObservableProperty]
    private string _syncStatusMessage = string.Empty;

    public MainViewModel()
    {
        Log(LogCategory.Parameters, "-masterserver api.kongor.net -webserver api.kongor.net -messageserver api.kongor.net");

        _ = CheckForUpdates();
    }

    [RelayCommand]
    private void GoToURL(string url)
        => Process.Start(new ProcessStartInfo() { FileName = url, UseShellExecute = true });

    [RelayCommand]
    private void LogCustomMasterServerAddress()
    {
        if (string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false)
            LogLaunchParameters();
    }

    partial void OnMasterServerAddressChanged(ComboBoxItem? oldValue, ComboBoxItem? newValue)
    {
        if (newValue is not null)
        {
            CanShowCustomMasterServerAddressField = newValue.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false;

            CanLaunchGame = (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is false
                ? true
                : (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is true && string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false
                    ? true : false;

            if (CanShowCustomMasterServerAddressField is false)
            {
                CustomMasterServerAddress = null;
                CanLaunchGame = true;

                LogLaunchParameters();
            }
        }
    }

    partial void OnCustomMasterServerAddressChanged(string? oldValue, string? newValue)
    {
        CanLaunchGame = (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is true && string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false
            ? true : false;
    }

    private void Log(string category, string message)
    {
        LogTextArea += _logger.Log(category, message);
    }

    private async Task CheckForUpdates()
    {
        // allow the UI to fully initialise before checking
        // TODO: find better way to do this without an arbitrary delay
        await Task.Delay(TimeSpan.FromMilliseconds(2_500));

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

            ReleasesRepositoryIsUnreachable = true;

            return;
        }

        catch (Exception exception)
        {
            Log(LogCategory.Version, $"The WILLOWMAKER Releases Repository Is Not Reachable: {exception.GetType().Name}");

            ReleasesRepositoryIsUnreachable = true;

            return;
        }

        if (result.IsUpdateAvailable is false || result.LatestVersion is null)
        {
            Log(LogCategory.Version, "WILLOWMAKER Is Up To Date");

            return;
        }

        string latestVersionDisplay = $"v{result.LatestVersion.Major}.{result.LatestVersion.Minor}.{result.LatestVersion.Build}";

        Log(LogCategory.Version, $"Update Available: {latestVersionDisplay}");

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
            return;

        UpdateDialog dialog = new ($"WILLOWMAKER {latestVersionDisplay} Is Available");

        bool shouldUpdate = await dialog.ShowDialog<bool>(desktop.MainWindow);

        if (shouldUpdate is false)
        {
            Log(LogCategory.Update, "Update Skipped By User");

            return;
        }

        if (result.DownloadURL is null)
        {
            Log(LogCategory.Update, "No Downloadable Asset Found; Opening The Releases Page ...");

            Process.Start(new ProcessStartInfo { FileName = result.ReleasePageURL, UseShellExecute = true });

            return;
        }

        try
        {
            Log(LogCategory.Update, $"Downloading Update From {result.DownloadURL} ...");

            string archivePath = await VersionChecker.DownloadUpdate(result.DownloadURL);

            Log(LogCategory.Update, "Applying Update And Restarting ...");

            VersionChecker.ApplyUpdateAndRestart(archivePath);
        }

        catch (Exception exception)
        {
            Log(LogCategory.Update, $"Update Failed: {exception.Message}");
        }
    }

    private void LogLaunchParameters()
    {
        string address = MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false
            ? CustomMasterServerAddress ?? throw new NullReferenceException("Custom Master Server Address Is NULL")
            : MasterServerAddress?.Content?.ToString() ?? throw new NullReferenceException("Master Server Address Is NULL");

        // The Game Client Does Not Understand "localhost" As A Valid Address, So We Need To Replace It With The Loopback Address "127.0.0.1" For Locally Hosted Master Servers
        // We Also Want To Wait Until Reaching The Colon Before Replacing The Local IP Address, Otherwise "localhost" Appears In The Log With The "t" Missing From The End
        address = address.Replace("localhost" + ":", IPAddress.Loopback.MapToIPv4() + ":");

        Log(LogCategory.Parameters, $"-masterserver {address} -webserver {address} -messageserver {address}");
    }

    private async Task<bool> SynchroniseContent()
    {
        SyncIsActive = true;
        SyncProgressPercent = 0;
        SyncStatusMessage = "Preparing Synchronisation ...";

        try
        {
            string variant = ContentBroker.ResolveDefaultClientVariant();

            Log(LogCategory.Content, $@"Fetching Manifest For Variant ""{variant}"" From CDN");

            Manifest manifest = await ContentBroker.FetchManifest(variant);

            Log(LogCategory.Content, $"Manifest Version {manifest.Version} Lists {manifest.Files.Count} File(s)");

            int filesDownloaded = 0;
            int filesDeleted    = 0;
            long bytesDownloaded = 0;

            SyncPlan? plan      = null;

            Progress<SyncEvent> progress = new (syncEvent =>
            {
                switch (syncEvent.Kind)
                {
                    case SyncEventKind.PlanReady:
                        plan = syncEvent.Plan;

                        if (plan is not null)
                        {
                            Log(LogCategory.Content, $"Plan: {plan}");

                            SyncStatusMessage = $"To Download: {plan.FilesToDownload}  |  To Delete: {plan.FilesToDelete}  |  Up To Date: {plan.FilesUpToDate}";

                            // If There Is No Work, Pre-Fill The Progress Bar To Avoid A Lingering Empty State
                            if (plan.FilesToDownload is 0 && plan.FilesToDelete is 0)
                                SyncProgressPercent = 100;
                        }

                        break;

                    case SyncEventKind.Downloaded:
                        filesDownloaded++;
                        bytesDownloaded += syncEvent.Size;

                        if (plan is not null)
                        {
                            SyncStatusMessage = $"Downloaded {filesDownloaded}/{plan.FilesToDownload}  |  Deleted {filesDeleted}/{plan.FilesToDelete}  |  Up To Date: {plan.FilesUpToDate}";

                            if (plan.TotalBytesToDownload > 0)
                                SyncProgressPercent = Math.Min(100, (double)bytesDownloaded / plan.TotalBytesToDownload * 100);
                        }

                        break;

                    case SyncEventKind.Deleted:
                        filesDeleted++;

                        if (plan is not null)
                            SyncStatusMessage = $"Downloaded {filesDownloaded}/{plan.FilesToDownload}  |  Deleted {filesDeleted}/{plan.FilesToDelete}  |  Up To Date: {plan.FilesUpToDate}";

                        break;

                    case SyncEventKind.DownloadFailed:
                    case SyncEventKind.DeletionFailed:
                        Log(LogCategory.Content, $"Failed | {syncEvent.Detail}");

                        break;

                    case SyncEventKind.SkippedExcluded:
                        Log(LogCategory.Content, $"Skipped (Excluded) | {syncEvent.Detail}");

                        break;

                    case SyncEventKind.Completed:
                        SyncProgressPercent = 100;
                        Log(LogCategory.Content, $"Sync Complete | {syncEvent.Detail}");

                        break;
                }
            });

            SyncSummary summary = await ContentBroker.Synchronise
            (
                manifest:        manifest,
                variant:         variant,
                targetDirectory: Environment.CurrentDirectory,
                progress:        progress
            );

            if (summary.FilesFailed > 0)
            {
                SyncStatusMessage = $"Synchronisation Failed: {summary.FilesFailed} File(s) Could Not Be Transferred";

                Log(LogCategory.Content, $"Aborting Launch: {summary.FilesFailed} File(s) Failed");

                return false;
            }

            SyncStatusMessage = $"Synchronisation Complete  |  Downloaded: {summary.FilesDownloaded}  |  Deleted: {summary.FilesDeleted}  |  Up To Date: {summary.FilesUpToDate}";

            return true;
        }

        catch (HttpRequestException exception)
        {
            string statusCode = exception.StatusCode is not null
                ? $"{(int)exception.StatusCode} ({exception.StatusCode})"
                : "Unknown Status Code";

            Log(LogCategory.Content, $"CDN Unreachable: HTTP {statusCode}");

            SyncStatusMessage = "CDN Unreachable; Synchronisation Aborted";

            return false;
        }

        catch (Exception exception)
        {
            Log(LogCategory.Content, $"Synchronisation Error: {exception.GetType().Name} | {exception.Message}");

            SyncStatusMessage = $"Synchronisation Error: {exception.Message}";

            return false;
        }

        finally
        {
            SyncIsActive = false;
        }
    }

    [RelayCommand]
    private async Task Launch()
    {
        if (await SynchroniseContent() is false)
            return;

        FileInfo[] executableMatchesWindows = new DirectoryInfo(Environment.CurrentDirectory).GetFiles("hon_x64.exe", SearchOption.TopDirectoryOnly);
        FileInfo[] executableMatchesLinux = new DirectoryInfo(Environment.CurrentDirectory).GetFiles("hon-x86_64", SearchOption.TopDirectoryOnly);
        FileInfo[] executableMatchesMacOS = new DirectoryInfo(Environment.CurrentDirectory).GetFiles("HoN64", SearchOption.TopDirectoryOnly);

        FileInfo[] executableMatches = Array.Empty<FileInfo>().Union(executableMatchesWindows).Union(executableMatchesLinux).Union(executableMatchesMacOS).ToArray();

        if (executableMatches.Length is 0)
        {
            Log(LogCategory.Executable, "Unable to locate the game executable in the current directory.");
            return;
        }

        else if (executableMatches.Length is 1)
        {
            Log(LogCategory.Executable, $@"Launching ""{executableMatches.Single().FullName}"" with set parameters ...");
        }

        else
        {
            Log(LogCategory.Executable, $"Multiple game executables were located in the current directory: {string.Join(", ", executableMatches.Select(match => match.Name))}.");
            return;
        }

        string address = MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false
            ? CustomMasterServerAddress ?? throw new NullReferenceException("Custom Master Server Address Is NULL")
            : MasterServerAddress?.Content?.ToString() ?? throw new NullReferenceException("Master Server Address Is NULL");

        // The Game Client Does Not Understand "localhost" As A Valid Address, So We Need To Replace It With The Loopback Address "127.0.0.1" For Locally Hosted Master Servers
        address = address.Replace("localhost", IPAddress.Loopback.MapToIPv4().ToString());

        string customConfigurationFilePath = Path.Combine(Environment.CurrentDirectory, "KONGOR", "configuration", "autoexec.cfg");

        string customConfigurationFileContent =
        """
        // autoexec.cfg auto-generated by WILLOWMAKER
        // load order: 1) startup.cfg, 2) login.cfg, 3) init.cfg, 4) components, 5) autoexec.cfg

        // performance
        setsave host_affinity -1

        // debugging
        setsave con_verbose true
        setsave http_printdebuginfo true
        setsave php_printdebuginfo true
        setsave sys_dumpOnFatal true

        // real-time debugging (set "con_notify" to "true" to enable)
        setsave con_notify false
        setsave con_notifyLines 48
        setsave con_notifyTime 15000

        // console (CTRL+F8)
        setsave con_height 0.50
        setsave con_alpha 0.25
        setsave con_showNet true

        // interface
        setsave ui_showQuickStart true
        setsave cg_24hourClock true

        """;

        Directory.CreateDirectory(Path.GetDirectoryName(customConfigurationFilePath) ?? throw new NullReferenceException("Custom Configuration File Path Is NULL"));

        File.WriteAllText(customConfigurationFilePath, customConfigurationFileContent);

        Log(LogCategory.Initialise, customConfigurationFilePath);

        string[] resources =
        [
            // relative to executable directory (e.g. "D:\Games\HoN Game Client v4.10.1")
            "base", // base resources; always needs to be loaded first; loaded automatically, but included for clarity
            "game", // game resources; always needs to be loaded immediately after base resources
            "KONGOR/configuration", // custom configuration files to override default configuration; configuration file load order: 1) startup.cfg, 2) login.cfg, 3) init.cfg, 4) autoexec.cfg
            "KONGOR/updates", // custom resource files to override default game resources; reserved for game updates
            "KONGOR/extensions", // custom resource files to override default game resources; reserved for mods and extensions

            // relative to configuration directory (e.g. "C:\Users\KONGOR\Documents\Heroes Of Newerth x64")
            "configuration" // the last path in the mod stack defines where user configuration files are saved to and loaded from; making this value dynamic is equivalent to having configuration profiles
        ];

        Log(LogCategory.Parameters, $"-mod {string.Join(";", resources)}");

        string[] arguments =
        [
            // services
            $"-masterserver {address}",
            $"-webserver {address}",
            $"-messageserver {address}",

            // resources
            $"-mod {string.Join(";", resources)}"
        ];

        Log(LogCategory.Command, $@"""{executableMatches.Single().FullName}"" {string.Join(" ", arguments)}");

        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = executableMatches.Single().FullName,
            Arguments = string.Join(" ", arguments),
            UseShellExecute = false
        });

        while (process?.MainWindowHandle == IntPtr.Zero)
            await Task.Delay(TimeSpan.FromMilliseconds(250));

        Environment.Exit(0);
    }
}
