namespace WILLOWMAKER.Core.Views;

public partial class SynchronisationBypassDialog : Window
{
    public SynchronisationBypassDialog()
    {
        InitializeComponent();
    }

    public SynchronisationBypassDialog(string message) : this()
    {
        MessageText.Text = message;
    }

    private void OK_Click(object? sender, RoutedEventArgs arguments)
        => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs arguments)
        => Close(false);
}
