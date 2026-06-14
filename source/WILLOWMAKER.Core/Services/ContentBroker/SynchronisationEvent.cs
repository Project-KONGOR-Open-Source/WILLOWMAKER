namespace WILLOWMAKER.Core.Services;

/// <summary>
///     A single observable step in the synchronisation pipeline. <see cref="ContentBroker"/> reports one of these per file transition so the UI can render progress.
///     <see cref="Plan"/> is populated only on the initial <see cref="SynchronisationEventKind.PlanReady"/> event.
/// </summary>
public sealed record SynchronisationEvent(SynchronisationEventKind Kind, string Detail, long Size, SynchronisationPlan? Plan = null);

/// <summary>
///     The work the synchronisation has decided to do, calculated up-front and reported once via the <see cref="SynchronisationEventKind.PlanReady"/> event.
///     The UI uses this to display "X to download, Y to delete, Z up to date" before downloads begin.
/// </summary>
public sealed record SynchronisationPlan(int FilesToDownload, int FilesToDelete, int FilesToSkip, int FilesUpToDate, long TotalBytesToDownload)
{
    public override string ToString()
        => $"{FilesToDownload} To Download ({TotalBytesToDownload:N0} Bytes), {FilesToDelete} To Delete, {FilesToSkip} To Skip, {FilesUpToDate} Up To Date";
}

/// <summary>
///     A single non-fatal failure that occurred during synchronisation. Multiple failures are collected and returned via <see cref="SynchronisationSummary.Failures"/>.
/// </summary>
public sealed record SynchronisationFailure(string Path, string Reason);

/// <summary>
///     The final outcome of a synchronisation run.
/// </summary>
public sealed record SynchronisationSummary(int FilesDownloaded, int FilesDeleted, int FilesUpToDate, int FilesFailed, long BytesDownloaded, IReadOnlyList<SynchronisationFailure> Failures)
{
    public override string ToString()
        => $"{FilesDownloaded} Downloaded, {FilesDeleted} Deleted, {FilesUpToDate} Up To Date, {FilesFailed} Failed, {BytesDownloaded:N0} Bytes Transferred";
}
