namespace WILLOWMAKER.Core.Enumerations;

/// <summary>
///     What the prerequisites check found: everything the game needs is installed, something is missing, or the check itself could not run to the end.
/// </summary>
public enum PrerequisitesCheckOutcome
{
    Installed,
    Missing,
    Failed
}
