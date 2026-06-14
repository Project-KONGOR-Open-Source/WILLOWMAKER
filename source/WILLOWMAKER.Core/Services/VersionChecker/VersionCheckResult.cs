namespace WILLOWMAKER.Core.Services;

/// <summary>
///     Represents the result of a version check against the GitHub releases API.
/// </summary>
public sealed record VersionCheckResult(bool IsUpdateAvailable, Version? LatestVersion, string? DownloadURL, string? ReleasePageURL);
