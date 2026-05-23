namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private Logger Logger { get; } = new (Path.Combine(Environment.CurrentDirectory, "WILLOWMAKER.log"));

    [ObservableProperty]
    public partial string? GitHubLink { get; set; } = "https://github.com/Project-KONGOR-Open-Source";

    [ObservableProperty]
    public partial string? WebPortalLink { get; set; } = "https://kongor.net";

    [ObservableProperty]
    public partial string? RedditLink { get; set; } = "https://www.reddit.com/r/newerth";

    [ObservableProperty]
    public partial string? DiscordLink { get; set; } = "https://discord.com/invite/N6pKzGDqUH";

    [ObservableProperty]
    public partial string? ElementLink { get; set; } = "https://app.element.io/#/room/#newerth:matrix.org";

    [ObservableProperty]
    public partial ComboBoxItem? MasterServerAddress { get; set; } = new () { Content = "api.kongor.net" }; // Needs To Match The Default Value In The XAML

    [ObservableProperty]
    public partial string? CustomMasterServerAddress { get; set; }

    [ObservableProperty]
    public partial bool CanShowCustomMasterServerAddressField { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchGame))]
    public partial bool MasterServerAddressIsValid { get; set; } = true;

    [ObservableProperty]
    public partial string VersionDisplay { get; set; } = VersionChecker.CurrentVersionDisplay;

    [ObservableProperty]
    public partial bool ReleasesRepositoryIsUnreachable { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SyncIsIdle))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGame))]
    [NotifyPropertyChangedFor(nameof(PlayButtonText))]
    public partial bool SyncIsActive { get; set; } = false;

    [ObservableProperty]
    public partial bool SyncIsScheduled { get; set; } = false;

    [ObservableProperty]
    public partial double SyncProgressPercent { get; set; } = 0;

    [ObservableProperty]
    public partial string SyncStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool SyncIsFailed { get; set; } = false;

    [ObservableProperty]
    public partial string DownloadedFilesDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemovedFilesDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SkippedFilesDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UpToDateFilesDisplay { get; set; } = string.Empty;

    public bool SyncIsIdle => SyncIsActive is false;

    public bool CanLaunchGame => MasterServerAddressIsValid && SyncIsIdle;

    public string PlayButtonText => SyncIsActive ? "Updating ..." : "Play Heroes Of Newerth";

    public MainViewModel()
    {
        Log(LogCategory.Parameters, "-masterserver api.kongor.net -webserver api.kongor.net -messageserver api.kongor.net");
    }

    /// <summary>
    ///     Invoked by <see cref="ClientLauncher"/> from the MainWindow's <see cref="Window.Opened"/> event once the window is realised and able to host a modal dialog.
    /// </summary>
    internal void OnMainWindowOpened()
    {
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

            MasterServerAddressIsValid = (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is false
                ? true
                : (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is true && string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false
                    ? true : false;

            if (CanShowCustomMasterServerAddressField is false)
            {
                CustomMasterServerAddress = null;
                MasterServerAddressIsValid = true;

                LogLaunchParameters();
            }
        }
    }

    partial void OnCustomMasterServerAddressChanged(string? oldValue, string? newValue)
    {
        MasterServerAddressIsValid = (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is true && string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false
            ? true : false;
    }

    private void Log(string category, string message)
    {
        Logger.Log(category, message);
    }

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
        SyncIsScheduled = false;
        SyncIsFailed = false;
        SyncProgressPercent = 0;
        SyncStatusMessage = string.Empty;
        DownloadedFilesDisplay = string.Empty;
        RemovedFilesDisplay = string.Empty;
        SkippedFilesDisplay = string.Empty;
        UpToDateFilesDisplay = string.Empty;

        try
        {
            string variant = ContentBroker.ResolveDefaultClientVariant();

            Log(LogCategory.Sync, $@"INIT: Fetching Manifest For Variant ""{variant}"" From CDN");

            Manifest manifest = await ContentBroker.FetchManifest(variant);

            Log(LogCategory.Sync, $"INIT: Manifest Version {manifest.Version} Lists {manifest.Files.Count} File(s)");

            int filesDownloaded  = 0;
            int filesDeleted     = 0;
            int filesSkipped     = 0;
            long bytesDownloaded = 0;

            SyncPlan? plan       = null;

            int upToDateTotal    = 0;

            Progress<SyncEvent> progress = new (syncEvent =>
            {
                switch (syncEvent.Kind)
                {
                    case SyncEventKind.PlanReady:
                    {
                        plan = syncEvent.Plan;

                        if (plan is not null)
                        {
                            Log(LogCategory.Sync, $"PLAN: {plan}");

                            // The Up-To-Date Column Includes Local Deletions So All Four Columns Reach 100% Together When The Sync Completes
                            upToDateTotal = manifest.Files.Count + plan.FilesToDelete;

                            DownloadedFilesDisplay = FormatSyncColumn("DOWNLOADED",                  0, plan.FilesToDownload);
                            RemovedFilesDisplay    = FormatSyncColumn("REMOVED"   ,                  0, plan.FilesToDelete  );
                            SkippedFilesDisplay    = FormatSyncColumn("SKIPPED"   ,                  0, plan.FilesToSkip    );
                            UpToDateFilesDisplay   = FormatSyncColumn("UP-TO-DATE", plan.FilesUpToDate, upToDateTotal       );

                            // Drives The Progress Bar's Visibility: A Plan With No Downloads And No Deletions Means There Is Nothing To Show Progress Of
                            SyncIsScheduled = plan.FilesToDownload > 0 || plan.FilesToDelete > 0;
                        }

                        break;
                    }

                    case SyncEventKind.Downloaded:
                    {
                        Log(LogCategory.Sync, $"PULL: {syncEvent.Detail}");

                        filesDownloaded++;

                        bytesDownloaded += syncEvent.Size;

                        if (plan is not null)
                        {
                            DownloadedFilesDisplay = FormatSyncColumn("DOWNLOADED", filesDownloaded, plan.FilesToDownload);
                            UpToDateFilesDisplay   = FormatSyncColumn("UP-TO-DATE", plan.FilesUpToDate + filesDownloaded + filesDeleted + filesSkipped, upToDateTotal);

                            if (plan.TotalBytesToDownload > 0)
                                SyncProgressPercent = Math.Min(100, (double) bytesDownloaded / plan.TotalBytesToDownload * 100);
                        }

                        break;
                    }

                    case SyncEventKind.Deleted:
                    {
                        Log(LogCategory.Sync, $"NUKE: {syncEvent.Detail}");

                        filesDeleted++;

                        if (plan is not null)
                        {
                            RemovedFilesDisplay  = FormatSyncColumn("REMOVED", filesDeleted, plan.FilesToDelete);
                            UpToDateFilesDisplay = FormatSyncColumn("UP-TO-DATE", plan.FilesUpToDate + filesDownloaded + filesDeleted + filesSkipped, upToDateTotal);
                        }

                        break;
                    }

                    case SyncEventKind.DownloadFailed:
                    case SyncEventKind.DeletionFailed:
                    {
                        Log(LogCategory.Sync, $"FAIL: {syncEvent.Detail}");

                        break;
                    }

                    case SyncEventKind.Skipped:
                    {
                        Log(LogCategory.Sync, $"SKIP: {syncEvent.Detail}");

                        filesSkipped++;

                        if (plan is not null)
                        {
                            SkippedFilesDisplay  = FormatSyncColumn("SKIPPED", filesSkipped, plan.FilesToSkip);
                            UpToDateFilesDisplay = FormatSyncColumn("UP-TO-DATE", plan.FilesUpToDate + filesDownloaded + filesDeleted + filesSkipped, upToDateTotal);
                        }

                        break;
                    }

                    case SyncEventKind.Completed:
                    {
                        SyncProgressPercent = 100;

                        Log(LogCategory.Sync, $"DONE: {syncEvent.Detail}");

                        break;
                    }
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
                SyncIsFailed = true;
                SyncStatusMessage = $"Synchronisation Failed: {summary.FilesFailed} File(s) Could Not Be Transferred";

                Log(LogCategory.Sync, $"FAIL: {summary.FilesFailed} File(s) Failed To Be Transferred :: Launch Aborted");

                return false;
            }

            return true;
        }

        catch (HttpRequestException exception)
        {
            string statusCode = exception.StatusCode is not null
                ? $"{(int) exception.StatusCode} ({exception.StatusCode})"
                : "Unknown Status Code";

            Log(LogCategory.Sync, $"FAIL: CDN Unreachable :: HTTP {statusCode}");

            SyncIsFailed = true;
            SyncStatusMessage = "CDN Unreachable; Synchronisation Aborted";

            return false;
        }

        catch (Exception exception)
        {
            Log(LogCategory.Sync, $"FAIL: {exception.GetType().Name} :: {exception.Message}");

            SyncIsFailed = true;
            SyncStatusMessage = $"Synchronisation Error: {exception.Message}";

            return false;
        }

        finally
        {
            SyncIsActive = false;
        }
    }

    /// <summary>
    ///     Formats a single status-row column as <c>LABEL XXX/YYY (ZZZ%)</c> with three-digit zero-padded counters for steady-width display.
    ///     A zero <paramref name="total"/> is treated as 100% complete (nothing to do is fully done).
    /// </summary>
    private static string FormatSyncColumn(string label, int current, int total)
    {
        int percent = total > 0 ? (int) ((double) current / total * 100) : 100;

        return $"{label} {current:D3}/{total:D3} ({percent}%)";
    }

    [RelayCommand]
    private async Task Launch()
    {
        Log(LogCategory.Executable, "Game Launch Initiated");

        if (await SynchroniseContent() is false)
            return;

        if (TryResolveGameExecutable(out FileInfo? executable) is false)
            return;

        string address = BuildMasterServerAddress();

        WriteCustomConfiguration();

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

        await LaunchProcessAndExit(executable, arguments);
    }

    [RelayCommand]
    private async Task LaunchEditor()
    {
        Log(LogCategory.Executable, "Map Editor Launch Initiated");

        if (await SynchroniseContent() is false)
            return;

        if (TryResolveGameExecutable(out FileInfo? executable) is false)
            return;

        string[] resources =
        [
            // relative to executable directory (e.g. "D:\Games\HoN Game Client v4.10.1")
            "base", // base resources; always needs to be loaded first; loaded automatically, but included for clarity
            "game", // game resources; always needs to be loaded immediately after base resources
            "editor", // editor resources; loaded immediately after game resources to overlay editor tooling onto the game stack
            "KONGOR/configuration", // custom configuration files to override default configuration; configuration file load order: 1) startup.cfg, 2) login.cfg, 3) init.cfg, 4) autoexec.cfg
            "KONGOR/updates", // custom resource files to override default game resources; reserved for game updates
            "KONGOR/extensions", // custom resource files to override default game resources; reserved for mods and extensions

            // relative to configuration directory (e.g. "C:\Users\KONGOR\Documents\Heroes Of Newerth x64")
            "configuration" // the last path in the mod stack defines where user configuration files are saved to and loaded from; making this value dynamic is equivalent to having configuration profiles
        ];

        Log(LogCategory.Parameters, $"-mod {string.Join(";", resources)}");

        string[] arguments =
        [
            // resources only; the editor has no use for the service endpoints
            $"-mod {string.Join(";", resources)}"
        ];

        await LaunchProcessAndExit(executable, arguments);
    }

    private bool TryResolveGameExecutable([NotNullWhen(true)] out FileInfo? executable)
    {
        FileInfo[] executableMatchesWindows = new DirectoryInfo(Environment.CurrentDirectory).GetFiles("hon_x64.exe", SearchOption.TopDirectoryOnly);
        FileInfo[] executableMatchesLinux   = new DirectoryInfo(Environment.CurrentDirectory).GetFiles("hon-x86_64", SearchOption.TopDirectoryOnly);
        FileInfo[] executableMatchesMacOS   = new DirectoryInfo(Environment.CurrentDirectory).GetFiles("HoN64",      SearchOption.TopDirectoryOnly);

        FileInfo[] executableMatches = Array.Empty<FileInfo>().Union(executableMatchesWindows).Union(executableMatchesLinux).Union(executableMatchesMacOS).ToArray();

        if (executableMatches.Length is 0)
        {
            Log(LogCategory.Executable, "Unable To Locate The Game Executable In The Current Directory");

            executable = null;

            return false;
        }

        if (executableMatches.Length > 1)
        {
            Log(LogCategory.Executable, $"Multiple Game Executables Were Located In The Current Directory: {string.Join(", ", executableMatches.Select(match => match.Name))}");

            executable = null;

            return false;
        }

        executable = executableMatches.Single();

        Log(LogCategory.Executable, $@"Resolved Game Executable: ""{executable.FullName}""");

        return true;
    }

    private string BuildMasterServerAddress()
    {
        string address = MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false
            ? CustomMasterServerAddress ?? throw new NullReferenceException("Custom Master Server Address Is NULL")
            : MasterServerAddress?.Content?.ToString() ?? throw new NullReferenceException("Master Server Address Is NULL");

        // The Game Client Does Not Understand "localhost" As A Valid Address, So We Need To Replace It With The Loopback Address "127.0.0.1" For Locally Hosted Master Servers
        return address.Replace("localhost", IPAddress.Loopback.MapToIPv4().ToString());
    }

    private void WriteCustomConfiguration()
    {
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
    }

    private async Task LaunchProcessAndExit(FileInfo executable, string[] arguments)
    {
        Log(LogCategory.Command, $@"""{executable.FullName}"" {string.Join(" ", arguments)}");

        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName        = executable.FullName,
            Arguments       = string.Join(" ", arguments),
            UseShellExecute = false
        });

        if (process is null)
        {
            Log(LogCategory.Executable, $@"Process Failed To Start: ""{executable.FullName}""");

            return;
        }

        while (process.MainWindowHandle == IntPtr.Zero)
            await Task.Delay(TimeSpan.FromMilliseconds(250));

        Environment.Exit(0);
    }
}
