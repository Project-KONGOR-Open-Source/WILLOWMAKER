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
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool CDNAddressIsValid { get; set; } = true;

    [ObservableProperty]
    public partial bool CDNProbeInProgress { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    public partial bool CDNProbeSucceeded { get; set; } = false;

    [ObservableProperty]
    public partial string CDNProbeStatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunchMapEditor))]
    [NotifyPropertyChangedFor(nameof(CanLaunchGameClient))]
    [NotifyPropertyChangedFor(nameof(MasterServerInputIsEnabled))]
    [NotifyPropertyChangedFor(nameof(CDNInputIsEnabled))]
    public partial bool LaunchIsInProgress { get; set; } = false;

    public bool MasterServerInputIsEnabled => UpdateCheckIsIdle && UpdateIsInstalling is false && SynchronisationIsIdle && LaunchIsInProgress is false;

    public bool CDNInputIsEnabled => MasterServerInputIsEnabled;

    public bool CanLaunchMapEditor => UpdateCheckIsIdle && UpdateIsInstalling is false && SynchronisationIsIdle && LaunchIsInProgress is false;

    public bool CanLaunchGameClient => UpdateCheckIsIdle && UpdateIsInstalling is false && MasterServerAddressIsValid && CDNAddressIsValid && CDNProbeSucceeded && SynchronisationIsIdle && LaunchIsInProgress is false;

    public string LaunchMapEditorButtonText => "Open Map Editor";

    public string LaunchGameClientButtonText => "Play Heroes Of Newerth";

    private CancellationTokenSource? probeCancellationTokenSource;
    private readonly Lock probeLock = new ();

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

        if (newValue.TargetURL == "CUSTOM")
        {
            CanShowCustomCDNAddressField = true;
            CDNAddressIsValid = AddressValidation.IsValidAddress(CustomCDNAddress);

            if (CDNAddressIsValid)
                ScheduleDebouncedCDNProbe(CustomCDNAddress);

            else
            {
                CancelProbe();
                CDNProbeSucceeded = false;
                CDNProbeStatusMessage = string.IsNullOrWhiteSpace(CustomCDNAddress) ? string.Empty : "Invalid CDN Address.";
            }
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

        CDNAddressIsValid = AddressValidation.IsValidAddress(CustomCDNAddress);

        if (CDNAddressIsValid)
            ScheduleDebouncedCDNProbe(CustomCDNAddress);

        else
        {
            CancelProbe();
            CDNProbeSucceeded = false;
            CDNProbeStatusMessage = string.IsNullOrWhiteSpace(CustomCDNAddress) ? string.Empty : "Invalid CDN Address.";
        }
    }

    private void InitialiseCDNOptions()
    {
        PopulateCDNOptions("api.kongor.net");
    }

    private void PopulateCDNOptions(string? masterServer)
    {
        AvailableCDNOptions.Clear();

        if (masterServer is not null && masterServer.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase))
        {
            CDNSelectItem customItem = new ()
            {
                DisplayText = "Custom CDN ...",
                TargetURL   = "CUSTOM",
                TooltipText = null
            };

            AvailableCDNOptions.Add(customItem);
            SelectedCDNAddressItem = customItem;
            CanShowCustomCDNAddressField = true;
            CustomCDNAddress = string.Empty;

            return;
        }

        if (masterServer is not null && (masterServer.Equals("localhost:5555", StringComparison.OrdinalIgnoreCase) || masterServer.Contains("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            CDNSelectItem localItem = new ()
            {
                DisplayText = "localhost:5555/cdn",
                TargetURL   = "localhost:5555/cdn",
                TooltipText = null
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
                TargetURL   = "CUSTOM",
                TooltipText = null
            };

            AvailableCDNOptions.Add(localItem);
            AvailableCDNOptions.Add(meshItem);
            AvailableCDNOptions.Add(servicesItem);
            AvailableCDNOptions.Add(customItem);

            SelectedCDNAddressItem = localItem;

            return;
        }

        CDNSelectItem defaultMeshItem = new ()
        {
            DisplayText = "cdn.kongor.net",
            TargetURL   = "cdn.kongor.net",
            TooltipText = "Hosted on a global mesh network."
        };

        CDNSelectItem defaultServicesItem = new ()
        {
            DisplayText = "api.kongor.net/cdn",
            TargetURL   = "api.kongor.net/cdn",
            TooltipText = "Hosted by the Project KONGOR services host and is intended as a Redundant Fault Tolerant High Availability Fallback."
        };

        CDNSelectItem defaultCustomItem = new ()
        {
            DisplayText = "Custom CDN ...",
            TargetURL   = "CUSTOM",
            TooltipText = null
        };

        AvailableCDNOptions.Add(defaultMeshItem);
        AvailableCDNOptions.Add(defaultServicesItem);
        AvailableCDNOptions.Add(defaultCustomItem);

        SelectedCDNAddressItem = defaultMeshItem;
    }

    private void CancelProbe()
    {
        lock (probeLock)
        {
            probeCancellationTokenSource?.Cancel();
            probeCancellationTokenSource?.Dispose();
            probeCancellationTokenSource = null;
        }

        RunOnUIThread(() => CDNProbeInProgress = false);
    }

    private void ScheduleDebouncedCDNProbe(string? targetAddress)
    {
        if (string.IsNullOrWhiteSpace(targetAddress))
        {
            CancelProbe();

            RunOnUIThread(() =>
            {
                CDNProbeSucceeded     = false;
                CDNProbeStatusMessage = string.Empty;
            });

            return;
        }

        lock (probeLock)
        {
            probeCancellationTokenSource?.Cancel();
            probeCancellationTokenSource?.Dispose();
            probeCancellationTokenSource = new CancellationTokenSource();
        }

        CancellationToken token = probeCancellationTokenSource.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token).ConfigureAwait(false);

                await ProbeCDN(targetAddress, token).ConfigureAwait(false);
            }

            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    [RelayCommand]
    private void TriggerImmediateCDNProbe()
    {
        string? targetAddress = SelectedCDNAddressItem?.TargetURL == "CUSTOM"
            ? CustomCDNAddress
            : SelectedCDNAddressItem?.TargetURL;

        if (string.IsNullOrWhiteSpace(targetAddress) || AddressValidation.IsValidAddress(targetAddress) is false)
            return;

        lock (probeLock)
        {
            probeCancellationTokenSource?.Cancel();
            probeCancellationTokenSource?.Dispose();
            probeCancellationTokenSource = new CancellationTokenSource();
        }

        CancellationToken token = probeCancellationTokenSource.Token;

        _ = Task.Run(() => ProbeCDN(targetAddress, token), token);
    }

    private async Task ProbeCDN(string rawAddress, CancellationToken cancellationToken)
    {
        RunOnUIThread(() =>
        {
            CDNProbeInProgress    = true;
            CDNProbeSucceeded     = false;
            CDNProbeStatusMessage = "Probing CDN Connectivity...";
        });

        string normalised = AddressValidation.NormaliseCDNURL(rawAddress);
        string variant    = ResolveDefaultClientVariant();
        string probeURL   = $"{normalised}{variant}/manifest.json";

        Log(LogCategory.Synchronise, $@"INIT: Probing CDN At ""{probeURL}""");

        try
        {
            using HttpClient client = new ();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"WILLOWMAKER/{VersionChecker.CurrentVersionDisplay}");
            client.Timeout = TimeSpan.FromSeconds(5);

            using HttpRequestMessage request = new (HttpMethod.Head, probeURL);
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                Log(LogCategory.Synchronise, "INIT: CDN Probe Succeeded (HTTP 200 OK)");

                RunOnUIThread(() =>
                {
                    CDNProbeInProgress    = false;
                    CDNProbeSucceeded     = true;
                    CDNProbeStatusMessage = "CDN Is Online (HTTP 200 OK).";
                });

                return;
            }

            string reason = $"HTTP {(int) response.StatusCode} ({response.StatusCode})";
            Log(LogCategory.Synchronise, $"WARN: CDN Probe Failed: {reason}");

            RunOnUIThread(() =>
            {
                CDNProbeInProgress    = false;
                CDNProbeSucceeded     = false;
                CDNProbeStatusMessage = $"CDN Is Unreachable: {reason}.";
            });
        }

        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }

        catch (Exception exception)
        {
            string reason = exception is HttpRequestException httpRequestException && httpRequestException.StatusCode is not null
                ? $"HTTP {(int) httpRequestException.StatusCode} ({httpRequestException.StatusCode})"
                : exception.Message;

            Log(LogCategory.Synchronise, $"WARN: CDN Probe Failed: {reason}");

            RunOnUIThread(() =>
            {
                CDNProbeInProgress    = false;
                CDNProbeSucceeded     = false;
                CDNProbeStatusMessage = $"CDN Is Unreachable: {reason}.";
            });
        }
    }

    public string ResolveActiveCDNURL()
    {
        string? rawAddress = SelectedCDNAddressItem?.TargetURL == "CUSTOM"
            ? CustomCDNAddress
            : SelectedCDNAddressItem?.TargetURL;

        string normalised = AddressValidation.NormaliseCDNURL(rawAddress);

        return string.IsNullOrWhiteSpace(normalised) ? "https://cdn.kongor.net/" : normalised;
    }

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
