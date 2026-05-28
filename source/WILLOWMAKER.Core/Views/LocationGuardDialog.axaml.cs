namespace WILLOWMAKER.Core.Views;

public partial class LocationGuardDialog : Window
{
    public LocationGuardDialog()
    {
        InitializeComponent();
    }

    public LocationGuardDialog(string message, string reason, IReadOnlyList<string> foreignEntries) : this()
    {
        MessageText.Text = message;
        ReasonText.Text  = reason;

        if (foreignEntries.Count is 0)
        {
            ForeignEntriesHeading.IsVisible = false;
            ForeignEntriesScroll.IsVisible  = false;
        }

        else
        {
            ForeignEntriesText.Text = string.Join(Environment.NewLine, LocationGuard.ApplyForeignEntriesDisplayCap(foreignEntries));
        }
    }

    private void OK_Click(object? sender, RoutedEventArgs arguments)
        => Close();

    // The Guard Dialog Terminates The Process On Any Close Path: OK Button, Title-Bar Close, Alt+F4, Escape Button, Or Programmatic Close
    // This Ensures The User Cannot Dismiss The Dialog And Then Interact With The Main Window Underneath
    protected override void OnClosed(EventArgs arguments)
    {
        base.OnClosed(arguments);

        Environment.Exit(0);
    }
}
