namespace WILLOWMAKER;

public class WILLOWMAKER
{
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<ClientLauncher>().UsePlatformDetect().WithInterFont().LogToTrace();
}
