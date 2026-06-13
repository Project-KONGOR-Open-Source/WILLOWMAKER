namespace WILLOWMAKER.Core.Enumerations;

/// <summary>
///     Classifies the entries reported by <see cref="ContentBroker"/> via the <see cref="IProgress{T}"/> callback.
/// </summary>
public enum SynchronisationEventKind
{
    PlanReady,
    DownloadStarted,
    Downloaded,
    Skipped,
    Deleted,
    DownloadFailed,
    DeletionFailed,
    Completed,
    ProgressUpdated
}
