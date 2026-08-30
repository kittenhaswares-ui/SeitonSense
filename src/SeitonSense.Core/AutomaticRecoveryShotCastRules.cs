using System.Collections.ObjectModel;

namespace SeitonSense.Core;

/// <summary>
/// One exact job/action pair whose movable PvP basic-shot cast may be
/// sacrificed by the separately opted-in automatic recovery path. The name is
/// presentation metadata only; runtime identity is always numeric.
/// </summary>
public sealed record AutomaticRecoveryShotCastDefinition(
    uint JobId,
    uint RawActionId,
    string DisplayName);

/// <summary>
/// Conservative allowlist for the two reviewed casted PvP basic shots. Instant
/// adjusted follow-ups are explicitly excluded and must never inherit the raw
/// carrier's cancellation eligibility.
/// </summary>
public static class AutomaticRecoveryShotCastRules
{
    public const uint BardJobId = 23;
    public const uint MachinistJobId = 31;

    public const uint BardPowerfulShotActionId = 29_391;
    public const uint MachinistBlastChargeActionId = 29_402;

    // Blast Charge adjusts to this instant, non-hotbar action while
    // Overheated. It is not a cast and is never cancellation-eligible.
    public const uint MachinistBlazingShotActionId = 41_468;

    // The retired instant follow-up row remains present in current metadata.
    // It is deliberately pinned as excluded rather than treated as a fallback.
    public const uint MachinistLegacyHeatBlastActionId = 29_403;

    private static readonly AutomaticRecoveryShotCastDefinition[] DefinitionArray =
    [
        new(BardJobId, BardPowerfulShotActionId, "Powerful Shot"),
        new(MachinistJobId, MachinistBlastChargeActionId, "Blast Charge"),
    ];

    private static readonly uint[] ExplicitlyExcludedActionArray =
    [
        MachinistBlazingShotActionId,
        MachinistLegacyHeatBlastActionId,
    ];

    private static readonly ReadOnlyCollection<AutomaticRecoveryShotCastDefinition>
        ReadOnlyDefinitions = Array.AsReadOnly(DefinitionArray);

    private static readonly ReadOnlyCollection<uint> ReadOnlyExplicitlyExcludedActionIds =
        Array.AsReadOnly(ExplicitlyExcludedActionArray);

    public static IReadOnlyList<AutomaticRecoveryShotCastDefinition> Definitions =>
        ReadOnlyDefinitions;

    public static IReadOnlyList<uint> ExplicitlyExcludedActionIds =>
        ReadOnlyExplicitlyExcludedActionIds;

    public static bool IsExactAllowedPair(uint jobId, uint castActionId) =>
        (jobId == BardJobId && castActionId == BardPowerfulShotActionId) ||
        (jobId == MachinistJobId && castActionId == MachinistBlastChargeActionId);

    /// <summary>
    /// Requires both the observed active cast and the current adjusted raw
    /// carrier to remain the same reviewed castable row. An adjusted instant
    /// follow-up, stale cast identity, or cross-job pair fails closed.
    /// </summary>
    public static bool IsExactAllowedPairWithAdjustedIdentity(
        uint jobId,
        uint castActionId,
        uint adjustedRawActionId) =>
        adjustedRawActionId == castActionId &&
        IsExactAllowedPair(jobId, castActionId);

    public static bool IsExplicitlyExcludedAction(uint actionId) =>
        actionId is MachinistBlazingShotActionId or
            MachinistLegacyHeatBlastActionId;

    public static bool TryGetDefinition(
        uint jobId,
        uint castActionId,
        out AutomaticRecoveryShotCastDefinition definition)
    {
        foreach (var candidate in DefinitionArray)
        {
            if (candidate.JobId != jobId || candidate.RawActionId != castActionId)
                continue;

            definition = candidate;
            return true;
        }

        definition = null!;
        return false;
    }

    public static bool TryGetRawActionId(uint jobId, out uint rawActionId)
    {
        rawActionId = jobId switch
        {
            BardJobId => BardPowerfulShotActionId,
            MachinistJobId => MachinistBlastChargeActionId,
            _ => 0,
        };
        return rawActionId != 0;
    }
}
