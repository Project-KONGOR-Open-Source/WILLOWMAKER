namespace WILLOWMAKER.Core.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void CustomMasterServerAddress_LostFocus(object? sender, RoutedEventArgs arguments)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.LogCustomMasterServerAddressCommand.Execute(null);
        }
    }

    private void CustomMasterServerAddress_KeyDown(object? sender, KeyEventArgs arguments)
    {
        if (arguments.Key == Key.Enter && sender is InputElement inputElement)
        {
            Visual? parent = inputElement.GetVisualParent();

            while (parent is not null)
            {
                if (parent is InputElement focusableParent && focusableParent.Focusable)
                {
                    focusableParent.Focus();

                    break;
                }

                parent = parent.GetVisualParent();
            }

            arguments.Handled = true;
        }
    }

    private void Background_PointerPressed(object? sender, RoutedEventArgs arguments)
    {
        if (sender is InputElement element)
        {
            element.Focus();
        }
    }
}
