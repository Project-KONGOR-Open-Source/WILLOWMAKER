namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private Logger Logger { get; } = new (Path.Combine(Environment.CurrentDirectory, DeploymentManifest.LogFileName));

    private LocationSafetyVerdict LocationSafetyVerdict { get; set; } = LocationSafetyVerdict.Unverified;

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
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool MasterServerAddressIsValid { get; set; } = true;

    [ObservableProperty]
    public partial string VersionDisplay { get; set; } = VersionChecker.CurrentVersionDisplay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateIsPendingMessage))]
    public partial string? LatestAvailableVersionDisplay { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReleasesRepositoryIsUnreachable))]
    [NotifyPropertyChangedFor(nameof(UpdateIsUnavailable))]
    [NotifyPropertyChangedFor(nameof(UpdateIsPending))]
    [NotifyPropertyChangedFor(nameof(UpdateCheckIsInProgress))]
    [NotifyPropertyChangedFor(nameof(UpdateCheckIsIdle))]
    [NotifyPropertyChangedFor(nameof(MasterServerInputIsEnabled))]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial UpdateStatus UpdateStatus { get; set; } = UpdateStatus.CheckInProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SynchronisationIsIdle))]
    [NotifyPropertyChangedFor(nameof(MasterServerInputIsEnabled))]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    [NotifyPropertyChangedFor(nameof(LaunchMapEditorButtonText))]
    [NotifyPropertyChangedFor(nameof(LaunchGameClientButtonText))]
    public partial bool SynchronisationIsActive { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool LaunchIsInProgress { get; set; } = false;

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

    public bool ReleasesRepositoryIsUnreachable => UpdateStatus is UpdateStatus.RepositoryUnreachable;

    public bool UpdateIsUnavailable => UpdateStatus is UpdateStatus.ApplicationUpToDate;

    public bool UpdateIsPending => UpdateStatus is UpdateStatus.UpdateAvailable;

    public string UpdateIsPendingMessage => $"{DeploymentManifest.ApplicationName} {LatestAvailableVersionDisplay} Is Available";

    public bool UpdateCheckIsInProgress => UpdateStatus is UpdateStatus.CheckInProgress;

    public bool UpdateCheckIsIdle => UpdateStatus is not UpdateStatus.CheckInProgress;

    public bool SynchronisationIsIdle => SynchronisationIsActive is false;

    public bool MasterServerInputIsEnabled => UpdateCheckIsIdle && SynchronisationIsIdle;

    public bool CanLaunchMapEditor => UpdateCheckIsIdle && SynchronisationIsIdle && LaunchIsInProgress is false;

    public bool CanLaunchGameClient => UpdateCheckIsIdle && MasterServerAddressIsValid && SynchronisationIsIdle && LaunchIsInProgress is false;

    public string LaunchMapEditorButtonText => SynchronisationIsActive ? "Updating ..." : "Open Map Editor";

    public string LaunchGameClientButtonText => SynchronisationIsActive ? "Updating ..." : "Play Heroes Of Newerth";

    public MainViewModel()
    {
        Log(LogCategory.Parameters, "-masterserver api.kongor.net -webserver api.kongor.net -messageserver api.kongor.net");
    }

    /// <summary>
    ///     Invoked by <see cref="ClientLauncher"/> from the MainWindow's <see cref="Window.Opened"/> event once the window is realised and able to host a modal dialog.
    /// </summary>
    internal void OnMainWindowOpened()
    {
        _ = OnMainWindowOpenedAsync();
    }

    private async Task OnMainWindowOpenedAsync()
    {
        if (await CheckLocationGuard() is false)
            return;

        await CheckForUpdates();
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

    /// <summary>
    ///     Assesses the location safety status of the current working directory and decides whether the launcher may proceed or not.
    ///     Returns <see langword="true"/> for a safe location or a development environment, otherwise displays a terminal modal dialog (which exits the process on any dismissal) and returns <see langword="false"/>.
    /// </summary>
    private async Task<bool> CheckLocationGuard()
    {
        LocationGuard.Result result = LocationGuard.AssessLocationSafety(Environment.CurrentDirectory);

        Log(LogCategory.Guard, result.Reason);

        LocationSafetyVerdict = result.Verdict;

        if (result.Verdict is not LocationSafetyVerdict.Unsafe)
        {
            return true;
        }

        foreach (string entry in LocationGuard.ApplyForeignEntriesDisplayCap(result.ForeignEntries))
            Log(LogCategory.Guard, entry);

        // Avalonia Applications Can Run Under Different Lifetimes: Classic-Desktop, Single-View (Mobile And Browser), Or Controlled
        // Owner-Parented Modal Dialogs Require The Classic-Desktop Lifetime And A Realised MainWindow To Use As The Modal's Owner
        // In Practice, This Branch Is Unreachable Because OnMainWindowOpened Only Fires After MainWindow Has Been Assigned, But Exiting Is The Safest Fallback If The Dialog Cannot Be Shown
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
        {
            Environment.Exit(0);

            return false;
        }

        string message =
            $"The directory from which {DeploymentManifest.ApplicationName} is currently running appears to be a dangerous location." + " " +
            $"The distribution synchronisation process can delete files in this directory so, to ensure that no unrelated files are deleted, {DeploymentManifest.ApplicationName} will now exit." + " " +
            $"Please run {DeploymentManifest.ApplicationName} from an empty directory or a Heroes Of Newerth directory.";

        LocationGuardDialog dialog = new (message, result.Reason, result.ForeignEntries);

        await dialog.ShowDialog(desktop.MainWindow);

        return false;
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

                            DownloadedFilesDisplay = FormatSynchronisationColumn("DOWNLOADED",                  0, plan.FilesToDownload);
                            RemovedFilesDisplay    = FormatSynchronisationColumn("REMOVED"   ,                  0, plan.FilesToDelete  );
                            SkippedFilesDisplay    = FormatSynchronisationColumn("SKIPPED"   ,                  0, plan.FilesToSkip    );
                            UpToDateFilesDisplay   = FormatSynchronisationColumn("UP-TO-DATE", plan.FilesUpToDate, upToDateTotal       );

                            // Drives The Progress Bar's Visibility: A Plan With No Downloads And No Deletions Means There Is Nothing To Show Progress Of
                            SynchronisationIsScheduled = plan.FilesToDownload > 0 || plan.FilesToDelete > 0;
                        }

                        break;
                    }

                    case SynchronisationEventKind.Downloaded:
                    {
                        Log(LogCategory.Synchronise, $"PULL: {synchronisationEvent.Detail}");

                        filesDownloaded++;

                        bytesDownloaded += synchronisationEvent.Size;

                        if (plan is not null)
                        {
                            DownloadedFilesDisplay = FormatSynchronisationColumn("DOWNLOADED", filesDownloaded, plan.FilesToDownload);
                            UpToDateFilesDisplay   = FormatSynchronisationColumn("UP-TO-DATE", plan.FilesUpToDate + filesDownloaded + filesDeleted + filesSkipped, upToDateTotal);

                            if (plan.TotalBytesToDownload > 0)
                                SynchronisationProgressPercent = Math.Min(100, (double) bytesDownloaded / plan.TotalBytesToDownload * 100);
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

            SynchronisationSummary summary = await ContentBroker.Synchronise
            (
                manifest:        manifest,
                variant:         variant,
                targetDirectory: Environment.CurrentDirectory,
                progress:        progress
            );

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

    [RelayCommand]
    private async Task LaunchGameClient()
    {
        LaunchIsInProgress = true;

        try
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

        finally
        {
            LaunchIsInProgress = false;
        }
    }

    [RelayCommand]
    private async Task LaunchMapEditor()
    {
        LaunchIsInProgress = true;

        try
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

        finally
        {
            LaunchIsInProgress = false;
        }
    }

    private bool TryResolveGameExecutable([NotNullWhen(true)] out FileInfo? executable)
    {
        FileInfo[] executableMatches = new DirectoryInfo(Environment.CurrentDirectory).GetFiles(DeploymentManifest.HeroesOfNewerthExecutableFileName, SearchOption.TopDirectoryOnly);

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
