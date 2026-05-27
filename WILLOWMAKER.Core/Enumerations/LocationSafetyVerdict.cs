namespace WILLOWMAKER.Core.Enumerations;

/// <summary>
///     Classifies the directory the application is running from according to whether it is a suitable place to perform operations that modify its contents.
/// </summary>
public enum LocationSafetyVerdict
{
    Unverified,
    Safe,
    DevelopmentEnvironment,
    Unsafe
}
