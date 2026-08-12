namespace SeitonSense.Core;

/// <summary>
/// Immutable metadata for one explicitly verified PvP protection status.
/// </summary>
public sealed record CcProtectionDefinition(
    uint StatusId,
    string Name,
    uint IconId,
    CcProtectionKind Kind,
    float MaximumRemainingTime,
    string ExpectedDescriptionFragment);
