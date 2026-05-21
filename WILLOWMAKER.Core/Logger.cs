namespace WILLOWMAKER.Core;

/// <summary>
///     Provides dual logging to both a UI-bound string property and a log file on disc.
/// </summary>
public sealed class Logger
{
    private string FilePath { get; }
    private Lock FileLock { get; } = new ();

    public Logger(string filePath)
    {
        FilePath = filePath;

        File.AppendAllText(FilePath, Environment.NewLine + $"▝▚▞▚▞▚▞▚▖ WILLOWMAKER Session Started At {DateTime.Now:O} ლ(ಠ益ಠლ) BUT AT WHAT COST !? ▗▞▚▞▚▞▚▞▘" + Environment.NewLine);
    }

    /// <summary>
    ///     Formats a timestamped log entry, writes it to the log file, and returns the formatted string for UI display.
    /// </summary>
    public string Log(string category, string message)
    {
        string entry = $"[{DateTime.Now:O}] [{category}] {message}" + Environment.NewLine;

        lock (FileLock)
            File.AppendAllText(FilePath, entry);

        return entry;
    }
}
