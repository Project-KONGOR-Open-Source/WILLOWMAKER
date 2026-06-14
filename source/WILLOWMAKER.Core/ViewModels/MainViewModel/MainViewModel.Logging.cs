namespace WILLOWMAKER.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private Logger Logger { get; } = new (Path.Combine(Environment.CurrentDirectory, DeploymentManifest.LogFileName));

    [RelayCommand]
    private void LogCustomMasterServerAddress()
    {
        if (string.IsNullOrWhiteSpace(CustomMasterServerAddress) is false)
            LogLaunchParameters();
    }

    private void Log(string category, string message)
    {
        Logger.Log(category, message);
    }
}
