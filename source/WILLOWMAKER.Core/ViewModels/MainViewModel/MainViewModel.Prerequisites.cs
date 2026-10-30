namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    /// <summary>
    ///     Runs the distribution's prerequisites check before the game or the map editor starts, and shows its report when anything is missing or the check cannot run.
    ///     Returns <see langword="true"/> only when everything the game needs is installed, because the game would otherwise fail with the operating system's own, far less helpful, message.
    /// </summary>
    private async Task<bool> CheckPrerequisites()
    {
        PrerequisitesCheckResult result = await PrerequisitesCheck.Run(Environment.CurrentDirectory);

        switch (result.Outcome)
        {
            case PrerequisitesCheckOutcome.Installed:
                Log(LogCategory.Executable, "INIT: Every Prerequisite Is Installed");

                return true;

            case PrerequisitesCheckOutcome.Missing:
                Log(LogCategory.Executable, "FAIL: Missing Prerequisites :: Launch Aborted");
                break;

            default:
                Log(LogCategory.Executable, $"FAIL: The Prerequisites Check Could Not Run :: {result.Report} :: Launch Aborted");
                break;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null)
            await new PrerequisitesDialog(result).ShowDialog(desktop.MainWindow);

        return false;
    }
}
