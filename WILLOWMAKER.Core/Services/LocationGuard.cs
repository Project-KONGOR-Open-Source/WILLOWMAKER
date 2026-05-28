namespace WILLOWMAKER.Core.Services;

/// <summary>
///     Refuses to launch WILLOWMAKER from a directory whose contents do not match either an existing Heroes Of Newerth install or a fresh WILLOWMAKER-only deployment.
///     Prevents the content synchronisation process from deleting unrelated user files when the user has accidentally placed WILLOWMAKER in a personal directory.
/// </summary>
public static class LocationGuard
{
    /// <summary>
    ///     The outcome of a location safety assessment.
    ///     <paramref name="Verdict"/> classifies the directory.
    ///     <paramref name="Reason"/> is a short, human-readable justification suitable for logging.
    ///     <paramref name="ForeignEntries"/> lists the top-level entries that caused an <see cref="LocationSafetyVerdict.Unsafe"/> verdict (empty for every other verdict).
    /// </summary>
    public sealed record Result(LocationSafetyVerdict Verdict, string Reason, IReadOnlyList<string> ForeignEntries);

    /// <summary>
    ///     Evaluates the supplied directory against the safety criteria, in order:
    ///     1) Development build → DEVELOPMENT ENVIRONMENT. The application is allowed to run, but operations that modify the directory are skipped.
    ///     2) Heroes Of Newerth executable present at the top level → SAFE. The directory is an existing game install.
    ///     3) The directory contains only the WILLOWMAKER distribution files and ignored runtime artefacts → SAFE. The directory is a fresh deployment ready for first-time synchronisation.
    ///     4) Otherwise → UNSAFE. The directory contains foreign content that the synchronisation process would put at risk.
    /// </summary>
    public static Result AssessLocationSafety(string directory)
    {
        if (DeploymentManifest.IsDevelopmentBuild)
            return new Result(LocationSafetyVerdict.DevelopmentEnvironment, "Development Environment Detected", []);

        string gameExecutableName = DeploymentManifest.HeroesOfNewerthExecutableFileName;

        if (File.Exists(Path.Combine(directory, gameExecutableName)))
            return new Result(LocationSafetyVerdict.Safe, $@"Heroes Of Newerth Directory (""{gameExecutableName}"" Is Present)", []);

        IReadOnlyList<string> foreignEntries = EnumerateForeignEntries(directory);

        if (foreignEntries.Count is 0)
            return new Result(LocationSafetyVerdict.Safe, $"Baseline {DeploymentManifest.ApplicationName} Directory", []);

        return new Result(LocationSafetyVerdict.Unsafe, "Foreign Entries Detected", foreignEntries);
    }

    /// <summary>
    ///     Returns the top-level entries in <paramref name="directory"/> that are neither part of WILLOWMAKER's distribution payload (the executable plus the native libraries that ship with the AOT publish on the current platform) nor on the ignore list (runtime artefacts such as the log file).
    ///     Subdirectory entries are reported with a trailing <see cref="Path.DirectorySeparatorChar"/> so that the calling UI can distinguish them from files.
    ///     The enumeration is configured to surface hidden and system entries as well, so that no foreign content can slip past the guard by virtue of an attribute flag.
    /// </summary>
    private static IReadOnlyList<string> EnumerateForeignEntries(string directory)
    {
        HashSet<string> recognisedNames = new (StringComparer.OrdinalIgnoreCase) { DeploymentManifest.ApplicationExecutableFileName };

        foreach (string nativeLibraryName in DeploymentManifest.NativeLibrariesFileNames)
            recognisedNames.Add(nativeLibraryName);

        foreach (string ignoredFileName in DeploymentManifest.IgnoredFileNames)
            recognisedNames.Add(ignoredFileName);

        EnumerationOptions enumerationOptions = new ()
        {
            AttributesToSkip      = FileAttributes.None,
            RecurseSubdirectories = false
        };

        List<string> foreignEntries = [];

        foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", enumerationOptions))
        {
            if (entry is DirectoryInfo)
            {
                foreignEntries.Add(entry.Name + Path.DirectorySeparatorChar);

                continue;
            }

            if (recognisedNames.Contains(entry.Name) is false)
                foreignEntries.Add(entry.Name);
        }

        return foreignEntries;
    }
}
