namespace SeitonSense.Core;

/// <summary>
/// Closed current-PvP catalog for the two reviewed Samurai cast starters which
/// may deliberately retain Smart Action's frozen hidden target. Follow-up
/// actions are instant and remain on the ordinary Smart Action path.
/// </summary>
public static class SamuraiSmartActionCastRules
{
    public const uint SamuraiJobId = 34;

    public const uint OgiNamikiriActionId = 29_530;
    public const uint OgiNamikiriFollowUpActionId = 29_531;

    public const uint TendoSetsugekkaCarrierActionId = 29_536;
    public const uint TendoSetsugekkaActionId = 41_454;
    public const uint TendoSetsugekkaFollowUpActionId = 41_455;

    public static bool IsReviewedBaseCastPair(
        uint rawActionId,
        uint resolvedActionId) =>
        (rawActionId == OgiNamikiriActionId &&
         resolvedActionId == OgiNamikiriActionId) ||
        ((rawActionId == TendoSetsugekkaCarrierActionId ||
          rawActionId == TendoSetsugekkaActionId) &&
         resolvedActionId == TendoSetsugekkaActionId);
}
