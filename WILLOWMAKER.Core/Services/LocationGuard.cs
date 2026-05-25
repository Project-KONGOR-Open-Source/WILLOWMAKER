namespace WILLOWMAKER.Core.Services;

/// <summary>
///     Refuses to launch WILLOWMAKER from a directory whose contents do not match either an existing Heroes Of Newerth install or a fresh WILLOWMAKER-only deployment.
///     Prevents the content synchronisation process from deleting unrelated user files when the user has accidentally placed WILLOWMAKER in a personal directory.
/// </summary>
public static class LocationGuard
{
    /// <summary>
    ///     The outcome of a location check.
    ///     <paramref name="IsSafe"/> indicates whether the directory is acceptable.
    ///     <paramref name="Reason"/> is a short, human-readable justification suitable for logging.
    /// </summary>
    public sealed record Result(bool IsSafe, string Reason);

    /// <summary>
    ///     Evaluates the supplied directory against the safety criteria, in order:
    ///     1) Development build (JIT runtime) → SAFE. The guard does not interfere with <c>dotnet run</c>.
    ///     2) Heroes Of Newerth executable present at the top level → SAFE. The directory is an existing game install.
    ///     3) The directory contains only the WILLOWMAKER distribution files and nothing else → SAFE. The directory is a fresh deployment ready for first-time synchronisation.
    ///     4) Otherwise → UNSAFE. The directory contains foreign content that the synchronisation process would put at risk.
    /// </summary>
    public static Result Check(string directory)
    {
        if (DeploymentManifest.IsDevelopmentBuild)
            return new Result(true, "Skipped (Development Build)");

        string gameExecutableName = DeploymentManifest.HeroesOfNewerthExecutable;

        if (File.Exists(Path.Combine(directory, gameExecutableName)))
            return new Result(true, $@"Heroes Of Newerth Directory (""{gameExecutableName}"" Present)");

        if (ContainsOnlyDistributionFiles(directory))
            return new Result(true, $"Baseline {DeploymentManifest.ApplicationName} Directory");

        return new Result(false, "Foreign Entries Detected");
    }

    /// <summary>
    ///     Returns <see langword="true"/> if every top-level entry in <paramref name="directory"/> is part of WILLOWMAKER's distribution (the executable plus the native libraries that ship with the AOT publish on the current platform).
    ///     Any subdirectory at the top level, and any file not on the distribution whitelist, causes this to return <see langword="false"/>.
    /// </summary>
    private static bool ContainsOnlyDistributionFiles(string directory)
    {
        HashSet<string> distributionFileNames = new (StringComparer.OrdinalIgnoreCase) { DeploymentManifest.ExecutableName };

        foreach (string nativeLibraryName in DeploymentManifest.NativeLibraryFileNames)
            distributionFileNames.Add(nativeLibraryName);

        foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo)
                return false;

            if (distributionFileNames.Contains(entry.Name) is false)
                return false;
        }

        return true;
    }
}
