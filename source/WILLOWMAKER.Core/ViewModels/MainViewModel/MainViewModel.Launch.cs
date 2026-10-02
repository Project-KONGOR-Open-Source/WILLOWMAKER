namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ComboBoxItem? MasterServerAddress { get; set; } = new () { Content = "api.kongor.net" }; // Needs To Match The Default Value In The XAML

    [ObservableProperty]
    public partial string? CustomMasterServerAddress { get; set; }

    [ObservableProperty]
    public partial bool CanShowCustomMasterServerAddressField { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool MasterServerAddressIsValid { get; set; } = true;

    public ObservableCollection<CDNSelectItem> AvailableCDNOptions { get; } = [];

    [ObservableProperty]
    public partial CDNSelectItem? SelectedCDNAddressItem { get; set; }

    [ObservableProperty]
    public partial string? CustomCDNAddress { get; set; }

    [ObservableProperty]
    public partial bool CanShowCustomCDNAddressField { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool CDNAddressIsValid { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CDNProbeFailed))]
    public partial bool CDNProbeInProgress { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CDNProbeFailed))]
    public partial bool CDNProbeSucceeded { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CDNProbeFailed))]
    public partial string CDNProbeStatusMessage { get; set; } = string.Empty;

    public bool CDNProbeFailed => CDNProbeInProgress is false && CDNProbeSucceeded is false && string.IsNullOrEmpty(CDNProbeStatusMessage) is false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    [NotifyPropertyChangedFor(nameof(MasterServerInputIsEnabled))]
    [NotifyPropertyChangedFor(nameof(CDNInputIsEnabled))]
    public partial bool LaunchIsInProgress { get; set; } = false;

    public bool MasterServerInputIsEnabled => UpdateCheckIsIdle && UpdateIsInstalling is false && SynchronisationIsIdle && LaunchIsInProgress is false;

    public bool CDNInputIsEnabled => MasterServerInputIsEnabled;

    // The Map Editor Runs Without A Master Server, But It Still Synchronises Its Resources From The Selected CDN
    public bool CanLaunchMapEditor => UpdateCheckIsIdle && UpdateIsInstalling is false && CDNAddressIsValid && SynchronisationIsIdle && LaunchIsInProgress is false;

    public bool CanLaunchGameClient => UpdateCheckIsIdle && UpdateIsInstalling is false && MasterServerAddressIsValid && CDNAddressIsValid && SynchronisationIsIdle && LaunchIsInProgress is false;

    public string LaunchMapEditorButtonText => "Open Map Editor";

    public string LaunchGameClientButtonText => "Play Heroes Of Newerth";

    private CancellationTokenSource? probeCancellationTokenSource;

    partial void OnMasterServerAddressChanged(ComboBoxItem? oldValue, ComboBoxItem? newValue)
    {
        if (newValue is not null)
        {
            string addressText = newValue.Content?.ToString() ?? string.Empty;
            bool isCustom = addressText.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase);

            CanShowCustomMasterServerAddressField = isCustom;

            if (isCustom)
            {
                CustomMasterServerAddress = string.Empty;
                CustomCDNAddress = string.Empty;
                MasterServerAddressIsValid = AddressValidation.IsValidAddress(CustomMasterServerAddress);
            }

            else
            {
                CustomMasterServerAddress = null;
                MasterServerAddressIsValid = true;

                LogLaunchParameters();
            }

            PopulateCDNOptions(addressText);
        }
    }

    partial void OnCustomMasterServerAddressChanged(string? oldValue, string? newValue)
    {
        bool isCustom = MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false;

        MasterServerAddressIsValid = isCustom
            ? AddressValidation.IsValidAddress(CustomMasterServerAddress)
            : true;
    }

    partial void OnSelectedCDNAddressItemChanged(CDNSelectItem? oldValue, CDNSelectItem? newValue)
    {
        if (newValue is null)
            return;

        if (newValue.IsCustom)
        {
            CanShowCustomCDNAddressField = true;

            ValidateAndProbeCustomCDNAddress();
        }

        else
        {
            CanShowCustomCDNAddressField = false;
            CustomCDNAddress = null;
            CDNAddressIsValid = true;

            ScheduleDebouncedCDNProbe(newValue.TargetURL);
        }
    }

    partial void OnCustomCDNAddressChanged(string? oldValue, string? newValue)
    {
        if (CanShowCustomCDNAddressField is false)
            return;

        ValidateAndProbeCustomCDNAddress();
    }

    private void ValidateAndProbeCustomCDNAddress()
    {
        CDNAddressIsValid = AddressValidation.IsValidAddress(CustomCDNAddress);

        if (CDNAddressIsValid)
            ScheduleDebouncedCDNProbe(CustomCDNAddress);

        else
        {
            CancelProbe();
            CDNProbeSucceeded = false;
            CDNProbeStatusMessage = string.IsNullOrWhiteSpace(CustomCDNAddress) ? string.Empty : "Invalid CDN Address";
        }
    }

    private void InitialiseCDNOptions()
    {
        PopulateCDNOptions("api.kongor.net");
    }

    private void PopulateCDNOptions(string masterServer)
    {
        CDNSelectItem localItem = new ()
        {
            DisplayText = "localhost:5555/cdn",
            TargetURL   = "localhost:5555/cdn"
        };

        CDNSelectItem meshItem = new ()
        {
            DisplayText = "cdn.kongor.net",
            TargetURL   = "cdn.kongor.net",
            TooltipText = "Hosted on a global mesh network."
        };

        CDNSelectItem servicesItem = new ()
        {
            DisplayText = "api.kongor.net/cdn",
            TargetURL   = "api.kongor.net/cdn",
            TooltipText = "Hosted by the Project KONGOR services host and is intended as a Redundant Fault Tolerant High Availability Fallback."
        };

        CDNSelectItem customItem = new ()
        {
            DisplayText = "Custom CDN ...",
            TargetURL   = null
        };

        // A Custom Master Server Is Expected To Come With A Custom CDN, So That Is The Only Option Offered For It
        CDNSelectItem[] options = masterServer.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ? [customItem]
            : masterServer.Contains("localhost", StringComparison.OrdinalIgnoreCase) ? [localItem, meshItem, servicesItem, customItem]
            : [meshItem, servicesItem, customItem];

        AvailableCDNOptions.Clear();

        foreach (CDNSelectItem option in options)
            AvailableCDNOptions.Add(option);

        SelectedCDNAddressItem = options[0];
    }

    private void CancelProbe()
    {
        probeCancellationTokenSource?.Cancel();
        probeCancellationTokenSource?.Dispose();
        probeCancellationTokenSource = null;

        CDNProbeInProgress = false;
    }

    private void ScheduleDebouncedCDNProbe(string? targetAddress)
    {
        CancelProbe();

        // The Previous Result Is Cleared Straight Away, So That It Is Never Shown For The New Address While The Debounce Delay Elapses
        CDNProbeSucceeded     = false;
        CDNProbeStatusMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(targetAddress))
            return;

        StartCDNProbe(targetAddress, delay: TimeSpan.FromMilliseconds(350));
    }

    [RelayCommand]
    private void TriggerImmediateCDNProbe()
    {
        string? targetAddress = ActiveCDNAddress;

        if (string.IsNullOrWhiteSpace(targetAddress) || AddressValidation.IsValidAddress(targetAddress) is false)
            return;

        CancelProbe();

        StartCDNProbe(targetAddress, delay: TimeSpan.Zero);
    }

    private void StartCDNProbe(string targetAddress, TimeSpan delay)
    {
        probeCancellationTokenSource = new CancellationTokenSource();

        CancellationToken cancellationToken = probeCancellationTokenSource.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                await ProbeCDN(targetAddress, cancellationToken).ConfigureAwait(false);
            }

            catch (OperationCanceledException)
            {
            }
        }, cancellationToken);
    }

    private async Task ProbeCDN(string rawAddress, CancellationToken cancellationToken)
    {
        SetCDNProbeState(inProgress: true, succeeded: false, statusMessage: "Probing CDN Connectivity ...", cancellationToken);

        bool succeeded = false;
        string probeURL = rawAddress;
        string result;

        try
        {
            string normalised = AddressValidation.NormaliseCDNURL(rawAddress);
            string variant    = ResolveDefaultClientVariant();

            probeURL = $"{normalised}{variant}/manifest.json";

            using HttpClient client = new ();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"WILLOWMAKER/{VersionChecker.CurrentVersionDisplay}");
            client.Timeout = TimeSpan.FromSeconds(5);

            using HttpRequestMessage request = new (HttpMethod.Head, probeURL);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            succeeded = response.IsSuccessStatusCode;
            result    = $"HTTP {(int) response.StatusCode} ({response.StatusCode})";
        }

        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        catch (Exception exception)
        {
            result = exception is HttpRequestException httpRequestException && httpRequestException.StatusCode is not null
                ? $"HTTP {(int) httpRequestException.StatusCode} ({httpRequestException.StatusCode})"
                : exception.Message;
        }

        // The Final State Is Applied Before Anything Is Logged, So That A Failure To Write To The Log Can Neither Leave The Probe In Progress Nor Change Its Result
        SetCDNProbeState(inProgress: false, succeeded: succeeded, statusMessage: succeeded ? $"CDN Is Online: {result}" : $"CDN Is Unreachable: {result}", cancellationToken);

        Log(LogCategory.Synchronise, $@"INIT: Probing CDN At ""{probeURL}""");
        Log(LogCategory.Synchronise, succeeded ? $"INIT: CDN Probe Succeeded: {result}" : $"WARN: CDN Probe Failed: {result}");
    }

    private void SetCDNProbeState(bool inProgress, bool succeeded, string statusMessage, CancellationToken cancellationToken)
    {
        RunOnUIThread(() =>
        {
            // A Probe Which Was Superseded While Its Result Was Being Dispatched Must Not Overwrite The State Of The Probe Which Replaced It
            if (cancellationToken.IsCancellationRequested)
                return;

            CDNProbeInProgress    = inProgress;
            CDNProbeSucceeded     = succeeded;
            CDNProbeStatusMessage = statusMessage;
        });
    }

    private string? ActiveCDNAddress => SelectedCDNAddressItem?.IsCustom is true ? CustomCDNAddress : SelectedCDNAddressItem?.TargetURL;

    public string ResolveActiveCDNURL()
        => AddressValidation.NormaliseCDNURL(ActiveCDNAddress);

    private static void RunOnUIThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
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

    [RelayCommand]
    private Task LaunchGameClient()
        => LaunchGameClientCore(skipSynchronisation: false);

    [RelayCommand]
    private async Task LaunchGameClientWithoutSynchronisation()
    {
        if (await ConfirmLaunchWithoutSynchronisation() is false)
            return;

        await LaunchGameClientCore(skipSynchronisation: true);
    }

    private async Task LaunchGameClientCore(bool skipSynchronisation)
    {
        LaunchIsInProgress = true;

        try
        {
            Log(LogCategory.Executable, "Game Launch Initiated");

            if (await SynchroniseContent(skipSynchronisation) is false)
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
                $"{FileSystem.RuntimeDirectoryName}/configuration", // custom configuration files to override default configuration; configuration file load order: 1) startup.cfg, 2) login.cfg, 3) init.cfg, 4) autoexec.cfg
                $"{FileSystem.RuntimeDirectoryName}/updates", // custom resource files to override default game resources; reserved for game updates
                $"{FileSystem.RuntimeDirectoryName}/extensions", // custom resource files to override default game resources; reserved for mods and extensions

                // relative to configuration directory (e.g. "C:\Users\KONGOR\Documents\Heroes Of Newerth x64")
                "client" // the last path in the mod stack defines where user configuration files are saved to and loaded from
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

    private async Task<bool> ConfirmLaunchWithoutSynchronisation()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
            return false;

        string message = new StringBuilder()
            .Append($"{DeploymentManifest.ApplicationName} will launch Heroes Of Newerth without synchronising content from the Content Delivery Network." + " ")
            .Append("This is only intended for development purposes." + " ")
            .Append("Running an out-of-date distribution can have negative consequences, such as not being able to connect to match servers.")
            .AppendLine().AppendLine()
            .Append("Continue?")
            .ToString();

        SynchronisationBypassDialog dialog = new (message);

        bool shouldBypass = await dialog.ShowDialog<bool>(desktop.MainWindow);

        if (shouldBypass is false)
            Log(LogCategory.Synchronise, "SKIP: Launch Without Synchronisation Cancelled By User");

        return shouldBypass;
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
                $"{FileSystem.RuntimeDirectoryName}/configuration", // custom configuration files to override default configuration; configuration file load order: 1) startup.cfg, 2) login.cfg, 3) init.cfg, 4) autoexec.cfg
                $"{FileSystem.RuntimeDirectoryName}/updates", // custom resource files to override default game resources; reserved for game updates
                $"{FileSystem.RuntimeDirectoryName}/extensions", // custom resource files to override default game resources; reserved for mods and extensions

                // relative to configuration directory (e.g. "C:\Users\KONGOR\Documents\Heroes Of Newerth x64")
                "editor" // the last path in the mod stack defines where user configuration files are saved to and loaded from
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
        string customConfigurationFilePath = Path.Combine(Environment.CurrentDirectory, FileSystem.RuntimeDirectoryName, "configuration", "autoexec.cfg");

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
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                UnixFileMode currentMode = File.GetUnixFileMode(executable.FullName);

                // POSIX Systems Require The Execute Permission Bit To Be Explicitly Set For Downloaded Binaries To Run
                File.SetUnixFileMode(executable.FullName, currentMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }

            catch (Exception exception)
            {
                Log(LogCategory.Executable, $@"Failed To Set POSIX Execution Permissions: {exception.Message}");
            }
        }

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
