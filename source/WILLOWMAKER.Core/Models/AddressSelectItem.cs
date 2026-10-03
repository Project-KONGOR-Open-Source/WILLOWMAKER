namespace WILLOWMAKER.Core.Models;

/// <summary>
///     Represents a selectable address option in the user interface, such as a master server or a content delivery network.
/// </summary>
public sealed class AddressSelectItem
{
    /// <summary>
    ///     The human-readable label displayed in the address selection picker.
    /// </summary>
    public required string DisplayText { get; init; }

    /// <summary>
    ///     The target address or URL string represented by this option, or <see langword="null"/> for the custom option, whose address is entered separately.
    /// </summary>
    public required string? TargetURL { get; init; }

    /// <summary>
    ///     Whether this option is the custom option, whose address is entered separately.
    /// </summary>
    public bool IsCustom => TargetURL is null;

    /// <summary>
    ///     Whether this option has descriptive tooltip text to display.
    /// </summary>
    public bool HasTooltip => string.IsNullOrWhiteSpace(TooltipText) is false;

    /// <summary>
    ///     The optional descriptive tooltip explaining the role or host of the address.
    /// </summary>
    public string? TooltipText { get; init; }
}
