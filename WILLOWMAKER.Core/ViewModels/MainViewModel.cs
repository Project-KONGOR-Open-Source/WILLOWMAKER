namespace WILLOWMAKER.Core.ViewModels;

// This class needs to be partial, to create an injection point for the auto-generated code produced by CommunityToolkit.Mvvm.
// Dependencies > Analyzers > CommunityToolkit.Mvvm.SourceGenerators > CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator > WILLOWMAKER.Core.ViewModels.MainViewModel.g.cs

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _gitHubLink = "https://github.com/Project-KONGOR-Open-Source";

    [ObservableProperty]
    private string? _webPortalLink = "https://kongor.online";

    [ObservableProperty]
    private string? _redditLink = "https://www.reddit.com/r/Project_KONGOR";

    [ObservableProperty]
    private string? _discordLink = "https://discord.com/invite/b5bsjK7ej3";

    [ObservableProperty]
    private ComboBoxItem? _masterServerAddress = new() { Content = "api.kongor.online" }; // Needs To Match The Default Value In The XAML

    [ObservableProperty]
    private string? _customMasterServerAddress;

    [ObservableProperty]
    private bool _canShowCustomMasterServerAddressField = false;

    [ObservableProperty]
    private bool _canLaunchGame = true;

    [ObservableProperty]
    private string? _logTextArea = $"[{DateTime.Now:s}] [PARAMETERS] -masterserver api.kongor.online -webserver api.kongor.online -messageserver api.kongor.online" + Environment.NewLine;

    [ObservableProperty]
    private int _caretIndexForAutoScroll = int.MaxValue;

    [RelayCommand]
    private void GoToURL(string url)
        => Process.Start(new ProcessStartInfo() { FileName = url, UseShellExecute = true });

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

                LogTextArea += LogLaunchParameters();
            }
        }
    }

    partial void OnCustomMasterServerAddressChanged(string? oldValue, string? newValue)
    {
        CanLaunchGame = (MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false) is true && string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false
            ? true : false;

        if (string.IsNullOrWhiteSpace(newValue) is false)
            LogTextArea += LogLaunchParameters();
    }

    private string LogLaunchParameters()
    {
        string address = MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false
            ? CustomMasterServerAddress ?? throw new NullReferenceException("Custom Master Server Address Is NULL")
            : MasterServerAddress?.Content?.ToString() ?? throw new NullReferenceException("Master Server Address Is NULL");

        // The Game Client Does Not Understand "localhost" As A Valid Address, So We Need To Replace It With The Loopback Address "127.0.0.1" For Locally Hosted Master Servers
        address = address.Replace("localhost", IPAddress.Loopback.MapToIPv4().ToString());

        return $"[{DateTime.Now:s}] [PARAMETERS] -masterserver {address} -webserver {address} -messageserver {address}" + Environment.NewLine;
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
            LogTextArea += $"[{DateTime.Now:s}] [EXECUTABLE] Unable to locate the game executable in the current directory." + Environment.NewLine;
            return;
        }

        else if (executableMatches.Length is 1)
        {
            LogTextArea += $@"[{DateTime.Now:s}] [EXECUTABLE] Launching ""{executableMatches.Single().FullName}"" with set parameters ..." + Environment.NewLine;
        }

        else
        {
            LogTextArea += $"[{DateTime.Now:s}] [EXECUTABLE] Multiple game executables were located in the current directory: {string.Join(", ", executableMatches.Select(match => match.Name))}." + Environment.NewLine;
            return;
        }

        string address = MasterServerAddress?.Content?.ToString()?.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase) ?? false
            ? CustomMasterServerAddress ?? throw new NullReferenceException("Custom Master Server Address Is NULL")
            : MasterServerAddress?.Content?.ToString() ?? throw new NullReferenceException("Master Server Address Is NULL");

        // The Game Client Does Not Understand "localhost" As A Valid Address, So We Need To Replace It With The Loopback Address "127.0.0.1" For Locally Hosted Master Servers
        address = address.Replace("localhost", IPAddress.Loopback.MapToIPv4().ToString());

        string[] arguments =
        [
            // services
            $"-masterserver {address}",
            $"-webserver {address}",
            $"-messageserver {address}",

            // debug
            @"-execute ""setsave http_printdebuginfo true""",
            @"-execute ""setsave php_printdebuginfo true""",

            // console
            @"-execute ""con_height 0.50""",
            @"-execute ""con_alpha 0.25"""
        ];

        LogTextArea += $"[{DateTime.Now:s}] [PARAMETERS] {arguments.Single(argument => argument.Contains("http_printdebuginfo"))} {arguments.Single(argument => argument.Contains("php_printdebuginfo"))}" + Environment.NewLine;
        LogTextArea += $"[{DateTime.Now:s}] [PARAMETERS] {arguments.Single(argument => argument.Contains("con_height"))} {arguments.Single(argument => argument.Contains("con_alpha"))}" + Environment.NewLine;

        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = executableMatches.Single().FullName,
            Arguments = string.Join(" ", arguments),
            UseShellExecute = false
        });

        while (process?.MainWindowHandle == IntPtr.Zero)
            await Task.Delay(250);

        Environment.Exit(0);
    }
}
