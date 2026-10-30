namespace WILLOWMAKER.Core.Views;

/// <summary>
///     Dialogue window shown when the prerequisites check finds something missing, or cannot run, so the game is not started.
/// </summary>
public partial class PrerequisitesDialog : Window
{
    public PrerequisitesDialog()
    {
        InitializeComponent();
    }

    public PrerequisitesDialog(PrerequisitesCheckResult result) : this()
    {
        ReportTextBlock.Text = result.Outcome is PrerequisitesCheckOutcome.Failed
            ? $"The prerequisites check could not run: {result.Report}"
            : result.Report;
    }

    private void OK_Click(object? sender, RoutedEventArgs arguments)
        => Close();
}
