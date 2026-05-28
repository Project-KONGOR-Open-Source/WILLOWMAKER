namespace WILLOWMAKER.Core.Services;

/// <summary>
///     Single source of truth for product-name string literals, deployment file naming conventions, and the platform-specific executable and native library names that WILLOWMAKER ships with and launches.
/// </summary>
public static class DeploymentManifest
{
    public const string ApplicationName            = "WILLOWMAKER";
    public const string LogFileName                = "WILLOWMAKER.log";
    public const string RuntimeDirectoryName       = "KONGOR";
    public const string UpdateArchiveFileName      = "WILLOWMAKER.update.zip";
    public const string UpdateExtractDirectoryName = "WILLOWMAKER.update";

    /// <summary>
    ///     The name of the WILLOWMAKER executable on the current platform.
    /// </summary>
    public static string ApplicationExecutableFileName =>
          OperatingSystem.IsWindows() ? $"{ApplicationName}.exe"
        : OperatingSystem.IsLinux()   ? ApplicationName
        : OperatingSystem.IsMacOS()   ? ApplicationName
        : throw new PlatformNotSupportedException($@"Unsupported Operating System: {Environment.OSVersion.Platform}");

    /// <summary>
    ///     The exact native library file names that the Native AOT release archive ships alongside the executable on the current platform.
    ///     Used by the location guard's ship-files whitelist.
    ///     Any file in the working directory not on this list (and not the executable itself) is treated as foreign.
    /// </summary>
    public static string[] NativeLibrariesFileNames =>
          OperatingSystem.IsWindows() ? [ "av_libglesv2.dll", "libHarfBuzzSharp.dll", "libSkiaSharp.dll" ]
        : OperatingSystem.IsLinux()   ? [ "libHarfBuzzSharp.so", "libSkiaSharp.so" ]
        : OperatingSystem.IsMacOS()   ? [ "libAvaloniaNative.dylib", "libHarfBuzzSharp.dylib", "libSkiaSharp.dylib" ]
        : throw new PlatformNotSupportedException($@"Unsupported Operating System: {Environment.OSVersion.Platform}");

    /// <summary>
    ///     File names that the location guard ignores when assessing a baseline deployment directory.
    ///     These are not part of the distribution payload and the guard does not require their presence; their presence simply does not classify the directory as unsafe.
    /// </summary>
    public static string[] IgnoredFileNames => [ LogFileName ];

    /// <summary>
    ///     The Heroes Of Newerth game client executable name on the current platform.
    /// </summary>
    public static string HeroesOfNewerthExecutableFileName =>
          OperatingSystem.IsWindows() ? "hon_x64.exe"
        : OperatingSystem.IsLinux()   ? "hon-x86_64"
        : OperatingSystem.IsMacOS()   ? "HoN64"
        : throw new PlatformNotSupportedException($@"Unsupported Operating System: {Environment.OSVersion.Platform}");

    /// <summary>
    ///     Indicates whether the current build is a development build (any configuration other than Release) rather than a release build (the Release configuration, which is how releases are published).
    /// </summary>
    public static bool IsDevelopmentBuild =>
    # if DEVELOPMENT
    true;
    # else
    false;
    # endif
}
