namespace WILLOWMAKER.Core;

/// <summary>
///     Provides dual logging to both a UI-bound string property and a log file on disc.
/// </summary>
public sealed class Logger
{
    private readonly string _filePath;
    private readonly Lock _lock = new();

    public Logger(string filePath)
    {
        _filePath = filePath;

        File.AppendAllText(_filePath, $"{Environment.NewLine}=== WILLOWMAKER Session Started At {DateTime.Now:s} ==={Environment.NewLine}");
    }

    /// <summary>
    ///     Formats a timestamped log entry, writes it to the log file, and returns the formatted string for UI display.
    /// </summary>
    public string Log(string category, string message)
    {
        string entry = $"[{DateTime.Now:s}] [{category}] {message}{Environment.NewLine}";

        lock (_lock)
            File.AppendAllText(_filePath, entry);

        return entry;
    }
}
