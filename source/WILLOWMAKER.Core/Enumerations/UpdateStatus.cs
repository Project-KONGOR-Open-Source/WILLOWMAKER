namespace WILLOWMAKER.Core.Enumerations;

/// <summary>
///     Represents the stage or outcome of checking whether a newer version of the application is available.
/// </summary>
public enum UpdateStatus
{
    CheckInProgress,
    ApplicationUpToDate,
    UpdateAvailable,
    RepositoryUnreachable
}
