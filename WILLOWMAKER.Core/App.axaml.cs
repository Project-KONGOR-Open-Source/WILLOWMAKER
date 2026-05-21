namespace WILLOWMAKER.Core;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainViewModel viewModel = new ();

            MainWindow mainWindow = new ()
            {
                DataContext = viewModel
            };

            // "Window.Opened" Fires Once The Main Window Is Realised And Able To Host UI Elements (Such As Modal Dialogs)
            // This Is The Trigger Which Signals That Start-Up Events With UI Elements Owned By The Main Window Can Start To Execute
            mainWindow.Opened += (_, _) => viewModel.OnMainWindowOpened();

            desktop.MainWindow = mainWindow;
        }

        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = new MainViewModel()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
