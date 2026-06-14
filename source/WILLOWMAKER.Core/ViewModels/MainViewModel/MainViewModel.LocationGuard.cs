namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private LocationSafetyVerdict LocationSafetyVerdict { get; set; } = LocationSafetyVerdict.Unverified;

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
}
