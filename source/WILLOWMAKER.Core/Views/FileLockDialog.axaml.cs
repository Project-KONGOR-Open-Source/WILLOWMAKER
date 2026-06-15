namespace WILLOWMAKER.Core.Views;

/// <summary>
///     Represents the binding model for a group of locked file paths associated with a single process.
/// </summary>
public sealed class LockGroupDisplay
{
    public required string ProcessName { get; init; }

    public required List<string> FilePaths { get; init; }
}

/// <summary>
///     Dialogue window shown when files are locked by other processes, preventing synchronisation from proceeding.
/// </summary>
public partial class FileLockDialog : Window
{
    public FileLockDialog()
    {
        InitializeComponent();
    }

    public FileLockDialog(List<LockGroupDisplay> lockGroups) : this()
    {
        LockGroupsItemsControl.ItemsSource = lockGroups;
    }

    private void OK_Click(object? sender, RoutedEventArgs arguments)
        => Close();
}
