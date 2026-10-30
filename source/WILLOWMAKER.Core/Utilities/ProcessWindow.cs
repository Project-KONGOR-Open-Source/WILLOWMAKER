namespace WILLOWMAKER.Core.Utilities;

/// <summary>
///     Keeps the launcher open until the program it started shows its window, so that the launcher does not vanish before the game appears.
/// </summary>
public static class ProcessWindow
{
    /// <summary>
    ///     Returns once <paramref name="process"/> shows its main window, or once it exits without one.
    ///     On Linux it returns at once, because .NET reports no window there, so waiting for one would keep the launcher open for good.
    /// </summary>
    public static async Task WaitUntilShown(Process process)
    {
        if (OperatingSystem.IsWindows() is false)
            return;

        while (process.HasExited is false)
        {
            try
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                    return;
            }

            // The Program Exited Between The Two Checks, So There Is No Window Left To Wait For
            catch (InvalidOperationException)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }
    }
}
