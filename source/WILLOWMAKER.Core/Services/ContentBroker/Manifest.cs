namespace WILLOWMAKER.Core.Services;

/// <summary>
///     The parsed content manifest published by the GEMINI distribution pipeline.
///     Lists every file the bucket publishes along with the size and hash needed to verify each one, plus the remote-side and local-side exclusion lists that govern what the synchronisation may transfer and overwrite.
/// </summary>
public sealed record Manifest
{
    /// <summary>
    ///     The manifest version string.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    ///     The algorithm used for the <see cref="ManifestEntry.Hash"/> values.
    /// </summary>
    public required string HashAlgorithm { get; init; }

    /// <summary>
    ///     Remote-side glob patterns for files that must not be downloaded from the bucket, such as the manifest itself.
    ///     Entries that appear in <see cref="Files"/> but match one of these patterns are skipped from the download pass.
    ///     Patterns are matched against the forward-slash relative path using gitignore-style semantics: <c>*</c> matches within a single path component, <c>**</c> matches across path separators (so <c>**/foo</c> matches <c>foo</c> at any depth including the root), <c>?</c> matches one character within a component, and every other character is literal.
    /// </summary>
    public required IReadOnlyList<string> ExcludeFromSource { get; init; }

    /// <summary>
    ///     Local-side glob patterns for files that must not be changed in the target directory, such as the launcher's own files.
    ///     Matched files are neither overwritten by the download pass nor removed by the deletion pass.
    ///     Patterns are matched against the forward-slash relative path using gitignore-style semantics: <c>*</c> matches within a single path component, <c>**</c> matches across path separators (so <c>**/foo</c> matches <c>foo</c> at any depth including the root), <c>?</c> matches one character within a component, and every other character is literal.
    /// </summary>
    public required IReadOnlyList<string> ExcludeFromTarget { get; init; }

    /// <summary>
    ///     Every file the manifest declares, keyed by forward-slash relative path.
    /// </summary>
    public required IReadOnlyDictionary<string, ManifestEntry> Files { get; init; }
}

/// <summary>
///     One entry in <see cref="Manifest.Files"/>: the size in bytes and the lowercase hex hash of the file's contents.
/// </summary>
public sealed record ManifestEntry
{
    public required long Size { get; init; }
    public required string Hash { get; init; }
}

/// <summary>
///     Source-generated serialisation metadata for <see cref="Manifest"/>. Required because the solution is published with Native AOT, which strips reflection-based <see cref="JsonSerializer"/> paths.
///     The source generator walks <see cref="Manifest"/> and emits typed metadata for every reachable type, including <see cref="ManifestEntry"/> and the dictionary and list members.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(Manifest))]
internal sealed partial class ManifestJSONContext : JsonSerializerContext;
