namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
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
