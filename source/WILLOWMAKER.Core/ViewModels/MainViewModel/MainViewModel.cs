namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
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
    public partial string VersionDisplay { get; set; } = VersionChecker.CurrentVersionDisplay;

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
}
