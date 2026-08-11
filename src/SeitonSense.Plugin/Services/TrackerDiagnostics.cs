namespace SeitonSense.Plugin.Services;

internal sealed record TrackerDiagnostics(
    bool Active,
    bool SeitonMetadataVerified,
    bool GuardMetadataVerified,
    bool RecuperateMetadataVerified,
    bool IsNinja,
    bool IsCrystallineConflict,
    bool IsPvP,
    uint TerritoryId,
    uint ContentFinderConditionId,
    int ResolvedSlots,
    int ValidEnemySlots,
    int InRangeSlots,
    int SeitonVisibleSlots,
    int GuardUnavailableSlots,
    int LowMpSlots,
    int PopupCount,
    uint ResolvedSeitonActionId)
{
    public static TrackerDiagnostics Inactive(
        uint territoryId = 0,
        PvPMetadataValidation? metadata = null) =>
        new(
            false,
            metadata?.SeitonVerified ?? false,
            metadata?.GuardVerified ?? false,
            metadata?.RecuperateVerified ?? false,
            false,
            false,
            false,
            territoryId,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);

    public string ToChatLine() =>
        $"active={Active}, CC={IsCrystallineConflict}, PvP={IsPvP}, NIN={IsNinja}, " +
        $"metadata[S/G/MP]={SeitonMetadataVerified}/{GuardMetadataVerified}/{RecuperateMetadataVerified}, " +
        $"territory={TerritoryId}, CFC={ContentFinderConditionId}, slots={ResolvedSlots}/5, valid={ValidEnemySlots}, " +
        $"range={InRangeSlots}, Seiton={SeitonVisibleSlots}, Guard-CD={GuardUnavailableSlots}, " +
        $"low-MP={LowMpSlots}, popups={PopupCount}, action={ResolvedSeitonActionId}";
}
