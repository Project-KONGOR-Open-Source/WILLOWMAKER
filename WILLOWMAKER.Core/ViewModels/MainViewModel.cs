namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Logger _logger = new(Path.Combine(Environment.CurrentDirectory, "WILLOWMAKER.log"));

    [ObservableProperty]
    private string? _gitHubLink = "https://github.com/Project-KONGOR-Open-Source";

    [ObservableProperty]
    private string? _webPortalLink = "https://kongor.net";

    [ObservableProperty]
    private string? _redditLink = "https://www.reddit.com/r/newerth";

    [ObservableProperty]
    private string? _discordLink = "https://discord.com/invite/N6pKzGDqUH";

    [ObservableProperty]
    private ComboBoxItem? _masterServerAddress = new() { Content = "api.kongor.net" }; // Needs To Match The Default Value In The XAML

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

    public MainViewModel()
    {
        Log(LogCategory.Parameters, "-masterserver api.kongor.net -webserver api.kongor.net -messageserver api.kongor.net");
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
    private async Task Launch()
    {
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
