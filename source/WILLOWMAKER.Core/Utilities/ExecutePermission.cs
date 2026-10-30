namespace WILLOWMAKER.Core.Utilities;

/// <summary>
///     Gives a program the execute permission that Linux needs to start it, since files downloaded from the content delivery network arrive without one.
/// </summary>
public static class ExecutePermission
{
    private const UnixFileMode ExecuteBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>
    ///     Adds the user, group, and other execute bits that <paramref name="path"/> lacks, keeping the rest of its mode, and does nothing on Windows.
    ///     A failure surfaces as the file system's exception, for the caller to report.
    /// </summary>
    public static void Grant(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        UnixFileMode mode = File.GetUnixFileMode(path);

        if ((mode & ExecuteBits) != ExecuteBits)
            File.SetUnixFileMode(path, mode | ExecuteBits);
    }
}
