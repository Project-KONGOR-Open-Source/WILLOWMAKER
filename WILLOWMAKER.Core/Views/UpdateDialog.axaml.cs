namespace WILLOWMAKER.Core.Views;

public partial class UpdateDialog : Window
{
    public UpdateDialog()
    {
        InitializeComponent();
    }

    public UpdateDialog(string message) : this()
    {
        MessageText.Text = message;
    }

    private void Update_Click(object? sender, RoutedEventArgs arguments)
        => Close(true);

    private void Skip_Click(object? sender, RoutedEventArgs arguments)
        => Close(false);
}
