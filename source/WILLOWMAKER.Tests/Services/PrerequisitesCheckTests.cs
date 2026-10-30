using System.Diagnostics;
using System.Runtime.Versioning;

using TUnit.Core.Enums;

namespace WILLOWMAKER.Tests.Services;

/// <summary>
///     Verifies that the prerequisites check's outcome follows from the check program's exit code, and that a check which cannot run, or runs too long, never counts as everything being installed.
///     The check programs are stand-ins built from the system shell.
/// </summary>
public sealed class PrerequisitesCheckTests
{
    private const string MissingLine = "Missing: The Test Library";

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static ProcessStartInfo CreateCheck(int exitCode) => OperatingSystem.IsWindows()
        ? new ProcessStartInfo("cmd.exe", $"/c echo {MissingLine}& exit /b {exitCode}")
        : new ProcessStartInfo("/bin/sh") { ArgumentList = { "-c", $"echo '{MissingLine}'; exit {exitCode}" } };

    [Test]
    public async Task A_Check_That_Exits_With_0_Reports_Everything_Installed()
    {
        PrerequisitesCheckResult result = await PrerequisitesCheck.Run(CreateCheck(0), TimeSpan.FromSeconds(30));

        await Assert.That(result.Outcome).IsEqualTo(PrerequisitesCheckOutcome.Installed);
    }

    [Test]
    public async Task A_Check_That_Exits_With_1_Reports_What_Is_Missing()
    {
        PrerequisitesCheckResult result = await PrerequisitesCheck.Run(CreateCheck(1), TimeSpan.FromSeconds(30));

        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(PrerequisitesCheckOutcome.Missing);
            await Assert.That(result.Report).Contains(MissingLine);
        }
    }

    [Test]
    public async Task A_Check_That_Exits_With_Another_Code_Fails()
    {
        PrerequisitesCheckResult result = await PrerequisitesCheck.Run(CreateCheck(2), TimeSpan.FromSeconds(30));

        await Assert.That(result.Outcome).IsEqualTo(PrerequisitesCheckOutcome.Failed);
    }

    [Test]
    public async Task A_Check_That_Runs_Too_Long_Is_Stopped_And_Fails()
    {
        ProcessStartInfo hangingCheck = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("ping", "-n 30 127.0.0.1")
            : new ProcessStartInfo("sleep", "30");

        Stopwatch stopwatch = Stopwatch.StartNew();

        PrerequisitesCheckResult result = await PrerequisitesCheck.Run(hangingCheck, TimeSpan.FromSeconds(1));

        using (Assert.Multiple())
        {
            await Assert.That(result.Outcome).IsEqualTo(PrerequisitesCheckOutcome.Failed);
            await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(10));
        }
    }

    [Test]
    public async Task A_Missing_Check_Program_Fails()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            PrerequisitesCheckResult result = await PrerequisitesCheck.Run(directory);

            using (Assert.Multiple())
            {
                await Assert.That(result.Outcome).IsEqualTo(PrerequisitesCheckOutcome.Failed);
                await Assert.That(result.Report).Contains(PrerequisitesCheck.FileName);
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    [ExcludeOn(OS.Windows)]
    [UnsupportedOSPlatform("windows")]
    public async Task A_Check_Without_The_Execute_Permission_Still_Runs()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string path = Path.Combine(directory, PrerequisitesCheck.FileName);

            File.WriteAllText(path, "#!/bin/sh\nexit 0\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            PrerequisitesCheckResult result = await PrerequisitesCheck.Run(directory);

            await Assert.That(result.Outcome).IsEqualTo(PrerequisitesCheckOutcome.Installed);
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
