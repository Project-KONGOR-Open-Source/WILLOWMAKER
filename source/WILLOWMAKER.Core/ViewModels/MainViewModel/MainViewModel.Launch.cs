namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public IReadOnlyList<AddressSelectItem> AvailableMasterServerOptions { get; } =
    [
        new () { DisplayText = "api.kongor.net", TargetURL = "api.kongor.net" },
        new () { DisplayText = "localhost:5555", TargetURL = "localhost:5555" },
        new () { DisplayText = "Custom Address ...", TargetURL = null }
    ];

    [ObservableProperty]
    public partial AddressSelectItem? SelectedMasterServerAddressItem { get; set; }

    [ObservableProperty]
    public partial string? CustomMasterServerAddress { get; set; }

    [ObservableProperty]
    public partial bool CanShowCustomMasterServerAddressField { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool MasterServerAddressIsValid { get; set; } = true;

    public ConnectivityProbe MasterServerProbe { get; }

    public ObservableCollection<AddressSelectItem> AvailableCDNOptions { get; } = [];

    [ObservableProperty]
    public partial AddressSelectItem? SelectedCDNAddressItem { get; set; }

    [ObservableProperty]
    public partial string? CustomCDNAddress { get; set; }

    [ObservableProperty]
    public partial bool CanShowCustomCDNAddressField { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool CDNAddressIsValid { get; set; } = true;

    public ConnectivityProbe CDNProbe { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    [NotifyPropertyChangedFor(nameof(MasterServerInputIsEnabled))]
    [NotifyPropertyChangedFor(nameof(CDNInputIsEnabled))]
    public partial bool LaunchIsInProgress { get; set; } = false;

    public bool MasterServerInputIsEnabled => UpdateCheckIsIdle && UpdateIsInstalling is false && SynchronisationIsIdle && LaunchIsInProgress is false;

    public bool CDNInputIsEnabled => MasterServerInputIsEnabled;

    public bool CanLaunchMapEditor => UpdateCheckIsIdle && UpdateIsInstalling is false && SynchronisationIsIdle && LaunchIsInProgress is false;

    public bool CanLaunchGameClient => UpdateCheckIsIdle && UpdateIsInstalling is false && MasterServerAddressIsValid && CDNAddressIsValid && SynchronisationIsIdle && LaunchIsInProgress is false;

    public string LaunchMapEditorButtonText => "Open Map Editor";

    public string LaunchGameClientButtonText => "Play Heroes Of Newerth";

    private static TimeSpan ProbeDebounceDelay { get; } = TimeSpan.FromMilliseconds(350);

    partial void OnSelectedMasterServerAddressItemChanged(AddressSelectItem? oldValue, AddressSelectItem? newValue)
    {
        if (newValue is null)
            return;

        if (newValue.IsCustom)
        {
            CanShowCustomMasterServerAddressField = true;
            CustomMasterServerAddress = string.Empty;
            CustomCDNAddress = string.Empty;

            ValidateAndProbeCustomMasterServerAddress();
        }

        else
        {
            CanShowCustomMasterServerAddressField = false;
            CustomMasterServerAddress = null;
            MasterServerAddressIsValid = true;

            ScheduleMasterServerProbe(newValue.TargetURL, ProbeDebounceDelay);

            LogLaunchParameters();
        }

        PopulateCDNOptions(newValue);
    }

    partial void OnCustomMasterServerAddressChanged(string? oldValue, string? newValue)
    {
        if (CanShowCustomMasterServerAddressField is false)
            return;

        ValidateAndProbeCustomMasterServerAddress();
    }

    private void ValidateAndProbeCustomMasterServerAddress()
    {
        MasterServerAddressIsValid = AddressValidation.IsValidAddress(CustomMasterServerAddress);

        if (MasterServerAddressIsValid)
            ScheduleMasterServerProbe(CustomMasterServerAddress, ProbeDebounceDelay);

        else
            MasterServerProbe.ReportInvalidAddress(CustomMasterServerAddress);
    }

    partial void OnSelectedCDNAddressItemChanged(AddressSelectItem? oldValue, AddressSelectItem? newValue)
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

            ScheduleCDNProbe(newValue.TargetURL, ProbeDebounceDelay);
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
            ScheduleCDNProbe(CustomCDNAddress, ProbeDebounceDelay);

        else
            CDNProbe.ReportInvalidAddress(CustomCDNAddress);
    }

    private void PopulateCDNOptions(AddressSelectItem masterServer)
    {
        AddressSelectItem localItem = new ()
        {
            DisplayText = "localhost:5555/cdn",
            TargetURL   = "localhost:5555/cdn"
        };

        AddressSelectItem meshItem = new ()
        {
            DisplayText = "cdn.kongor.net",
            TargetURL   = "cdn.kongor.net",
            TooltipText = "Hosted on a global mesh network."
        };

        AddressSelectItem servicesItem = new ()
        {
            DisplayText = "api.kongor.net/cdn",
            TargetURL   = "api.kongor.net/cdn",
            TooltipText = "Hosted by the Project KONGOR services host and is intended as a redundant fault-tolerant highly-available fallback."
        };

        AddressSelectItem customItem = new ()
        {
            DisplayText = "Custom Address ...",
            TargetURL   = null
        };

        // A Custom Master Server Is Expected To Come With A Custom CDN, So That Is The Only Option Offered For It
        AddressSelectItem[] options = masterServer.IsCustom ? [customItem]
            : masterServer.TargetURL?.Contains("localhost", StringComparison.OrdinalIgnoreCase) is true ? [localItem, meshItem, servicesItem, customItem]
            : [meshItem, servicesItem, customItem];

        AvailableCDNOptions.Clear();

        foreach (AddressSelectItem option in options)
            AvailableCDNOptions.Add(option);

        SelectedCDNAddressItem = options[0];
    }

    private void ScheduleMasterServerProbe(string? address, TimeSpan delay)
    {
        if (string.IsNullOrWhiteSpace(address))
            MasterServerProbe.Reset();

        else
            MasterServerProbe.Start(BuildMasterServerProbeURL(address), delay);
    }

    private void ScheduleCDNProbe(string? address, TimeSpan delay)
    {
        if (string.IsNullOrWhiteSpace(address))
            CDNProbe.Reset();

        else
            CDNProbe.Start($"{AddressValidation.NormaliseCDNURL(address)}{ResolveDefaultClientVariant()}/manifest.json", delay);
    }

    [RelayCommand]
    private void TriggerImmediateMasterServerProbe()
    {
        if (AddressValidation.IsValidAddress(ActiveMasterServerAddress))
            ScheduleMasterServerProbe(ActiveMasterServerAddress, TimeSpan.Zero);
    }

    [RelayCommand]
    private void TriggerImmediateCDNProbe()
    {
        if (AddressValidation.IsValidAddress(ActiveCDNAddress))
            ScheduleCDNProbe(ActiveCDNAddress, TimeSpan.Zero);
    }

    // The Game Client Talks To The Master Server Over Plain HTTP, So An Address Without A Scheme Is Probed Over HTTP Too, At The Master Server's Health Endpoint
    private static string BuildMasterServerProbeURL(string address)
    {
        string baseURL = address.Contains("://", StringComparison.Ordinal) ? address : $"http://{address}";

        return $"{baseURL.TrimEnd('/')}/health";
    }

    private string? ActiveMasterServerAddress => SelectedMasterServerAddressItem?.IsCustom is true ? CustomMasterServerAddress : SelectedMasterServerAddressItem?.TargetURL;

    private string? ActiveCDNAddress => SelectedCDNAddressItem?.IsCustom is true ? CustomCDNAddress : SelectedCDNAddressItem?.TargetURL;

    public string ResolveActiveCDNURL()
        => AddressValidation.NormaliseCDNURL(ActiveCDNAddress);

    private void LogLaunchParameters()
    {
        string address = ActiveMasterServerAddress ?? throw new NullReferenceException("Master Server Address Is NULL");

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

    private async Task<bool> ConfirmMapEditorLaunchWithoutSynchronisation()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow is null)
            return false;

        string problem = CDNAddressIsValid
            ? $"The map editor's resources could not be synchronised from the Content Delivery Network ({SynchronisationStatusMessage})."
            : string.IsNullOrWhiteSpace(ActiveCDNAddress)
                ? "No CDN address has been entered, so the map editor's resources cannot be synchronised from the Content Delivery Network."
                : $@"The CDN address ""{ActiveCDNAddress}"" is not valid, so the map editor's resources cannot be synchronised from the Content Delivery Network.";

        string message = new StringBuilder()
            .Append(problem + " ")
            .Append("The map editor can still be opened, but it may not have the latest resources.")
            .AppendLine().AppendLine()
            .Append("Continue?")
            .ToString();

        SynchronisationBypassDialog dialog = new (message);

        bool shouldContinue = await dialog.ShowDialog<bool>(desktop.MainWindow);

        Log(LogCategory.Synchronise, shouldContinue
            ? "WARN: Opening The Map Editor Without Synchronised Resources"
            : "SKIP: Map Editor Launch Cancelled By User");

        return shouldContinue;
    }

    [RelayCommand]
    private async Task LaunchMapEditor()
    {
        LaunchIsInProgress = true;

        try
        {
            Log(LogCategory.Executable, "Map Editor Launch Initiated");

            // The Map Editor Works Offline, So A Synchronisation Problem Does Not Prevent It From Being Opened, But The User Is Told About The Problem And Can Choose To Fix It First
            if (await SynchroniseContent() is false && await ConfirmMapEditorLaunchWithoutSynchronisation() is false)
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
        string address = ActiveMasterServerAddress ?? throw new NullReferenceException("Master Server Address Is NULL");

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
