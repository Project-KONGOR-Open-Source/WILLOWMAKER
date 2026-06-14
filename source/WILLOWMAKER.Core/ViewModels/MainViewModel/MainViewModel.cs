namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
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
