namespace SeitonSense.Core;

/// <summary>
/// Selects the exact set of verified target protections that can nullify one
/// cataloged crowd-control action.
/// </summary>
public enum CcImmunityBrakeBlockerFamily : byte
{
    StandardPurifyCc = 0,
    Miracle = 1,
}

/// <summary>
/// One language-independent PvP action supported by the CC-immunity brake.
/// Runtime matching is always performed with the numeric job and action IDs;
/// <see cref="DisplayName"/> is presentation metadata only.
/// </summary>
public sealed record CcImmunityBrakeActionDefinition(
    uint JobId,
    uint ActionId,
    string DisplayName,
    CcImmunityBrakeBlockerFamily BlockerFamily);
