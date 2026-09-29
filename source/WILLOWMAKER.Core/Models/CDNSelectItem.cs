namespace WILLOWMAKER.Core.Models;

/// <summary>
///     Represents a selectable content delivery network option in the user interface.
/// </summary>
public sealed class CDNSelectItem
{
    /// <summary>
    ///     The human-readable label displayed in the content delivery network selection picker.
    /// </summary>
    public required string DisplayText { get; init; }

    /// <summary>
    ///     The target address or URL string represented by this option.
    /// </summary>
    public required string TargetURL { get; init; }

    /// <summary>
    ///     Whether this option has descriptive tooltip text to display.
    /// </summary>
    public bool HasTooltip => string.IsNullOrWhiteSpace(TooltipText) is false;

    /// <summary>
    ///     The optional descriptive tooltip explaining the role or host of this content delivery network endpoint.
    /// </summary>
    public string? TooltipText { get; init; }
}
