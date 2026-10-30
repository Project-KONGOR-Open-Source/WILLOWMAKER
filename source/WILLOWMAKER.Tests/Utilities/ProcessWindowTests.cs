using System.Diagnostics;

namespace WILLOWMAKER.Tests.Utilities;

/// <summary>
///     Verifies that waiting for a started program's window never keeps the launcher open for good: on Linux, where .NET reports no window, and on Windows, for a program that ends without showing one.
/// </summary>
public sealed class ProcessWindowTests
{
    [Test]
    public async Task A_Process_That_Shows_No_Window_Does_Not_Keep_The_Launcher_Waiting()
    {
        ProcessStartInfo startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", "/c ping -n 3 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false }
            : new ProcessStartInfo("sleep", "30") { UseShellExecute = false };

        using Process process = Process.Start(startInfo)!;

        try
        {
            Task wait = ProcessWindow.WaitUntilShown(process);

            Task finished = await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(10)));

            await Assert.That(finished).IsSameReferenceAs(wait);
        }

        finally
        {
            if (process.HasExited is false)
                process.Kill(entireProcessTree: true);
        }
    }
}
