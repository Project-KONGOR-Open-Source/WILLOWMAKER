namespace WILLOWMAKER.Tests.Infrastructure;

/// <summary>
///     A bare Avalonia application on the headless platform, which provides the UI thread and the Avalonia services that the view models depend on.
/// </summary>
public sealed class HeadlessApplication : Application
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
