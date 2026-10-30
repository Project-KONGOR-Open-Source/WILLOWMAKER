namespace WILLOWMAKER.Core.Services;

/// <summary>
///     The outcome of the prerequisites check, with its report: what is missing for <see cref="PrerequisitesCheckOutcome.Missing"/>, or why the check could not run for <see cref="PrerequisitesCheckOutcome.Failed"/>.
/// </summary>
public sealed record PrerequisitesCheckResult(PrerequisitesCheckOutcome Outcome, string Report);

/// <summary>
///     Runs the distribution's prerequisites check, which reports every library and runtime that the game needs from the operating system and does not find.
///     The check exits with 0 when everything is installed and with 1 when something is missing; anything else, including a check that cannot start or does not finish in time, is a failure.
/// </summary>
public static class PrerequisitesCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     The name of the check program in a distribution's root on the current platform.
    /// </summary>
    public static string FileName => OperatingSystem.IsWindows() ? "hon_check.exe" : "hon_check";

    /// <summary>
    ///     Runs the check of the distribution in <paramref name="installationDirectory"/>, giving it the execute permission first, since a downloaded check arrives without one on Linux.
    /// </summary>
    public static async Task<PrerequisitesCheckResult> Run(string installationDirectory, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(installationDirectory, FileName);

        if (File.Exists(path) is false)
            return new PrerequisitesCheckResult(PrerequisitesCheckOutcome.Failed, $@"""{FileName}"" Is Missing From ""{installationDirectory}""");

        try
        {
            ExecutePermission.Grant(path);
        }

        catch (Exception exception)
        {
            return new PrerequisitesCheckResult(PrerequisitesCheckOutcome.Failed, $@"""{FileName}"" Could Not Be Given The Execute Permission: {exception.Message}");
        }

        return await Run(new ProcessStartInfo(path) { WorkingDirectory = installationDirectory }, Timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Runs the check that <paramref name="startInfo"/> describes, with its output read, and no window, and stops it once <paramref name="timeout"/> has passed.
    /// </summary>
    internal static async Task<PrerequisitesCheckResult> Run(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        startInfo.UseShellExecute        = false;
        startInfo.CreateNoWindow         = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError  = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding  = Encoding.UTF8;

        Process process;

        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("No Process Was Started");
        }

        catch (Exception exception)
        {
            return new PrerequisitesCheckResult(PrerequisitesCheckOutcome.Failed, $@"""{startInfo.FileName}"" Could Not Be Started: {exception.Message}");
        }

        using (process)
        {
            // Both Streams Are Read While The Check Runs, So That A Long Report Cannot Fill A Pipe And Stall The Check
            Task<string> output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> error  = process.StandardError.ReadToEndAsync(CancellationToken.None);

            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            deadline.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);

                // A Program That The Check Started Can Keep Its Output Open After The Check Has Exited, So Reading The Output Is Held To The Same Deadline
                await Task.WhenAll(output, error).WaitAsync(deadline.Token).ConfigureAwait(false);
            }

            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }

                // The Check Itself May Already Have Exited, Leaving Only The Program It Started, Which Is No Longer Part Of Its Process Tree
                catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                }

                cancellationToken.ThrowIfCancellationRequested();

                return new PrerequisitesCheckResult(PrerequisitesCheckOutcome.Failed, $"The Check Did Not Finish Within {timeout.TotalSeconds:0} Second(s) And Was Stopped");
            }

            string report = string.Join(Environment.NewLine, new[] { await output.ConfigureAwait(false), await error.ConfigureAwait(false) }.Where(text => string.IsNullOrWhiteSpace(text) is false)).Trim();

            return process.ExitCode switch
            {
                0 => new PrerequisitesCheckResult(PrerequisitesCheckOutcome.Installed, report),
                1 => new PrerequisitesCheckResult(PrerequisitesCheckOutcome.Missing,   report),
                _ => new PrerequisitesCheckResult(PrerequisitesCheckOutcome.Failed,    $"The Check Exited With Code {process.ExitCode}{(report.Length > 0 ? $": {report}" : string.Empty)}")
            };
        }
    }
}
