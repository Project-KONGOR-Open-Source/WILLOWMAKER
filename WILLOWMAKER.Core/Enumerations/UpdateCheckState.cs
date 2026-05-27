namespace WILLOWMAKER.Core.Enumerations;

/// <summary>
///     Represents the stage or outcome of checking whether a newer version of the application is available.
///     The values are mutually exclusive, so exactly one of them describes the situation at any given moment.
/// </summary>
public enum UpdateCheckState
{
    CheckInProgress,
    UpToDate,
    UpdateAvailable,
    RepositoryUnreachable
}
