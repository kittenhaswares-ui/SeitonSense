namespace SeitonSense.Plugin.Services;

internal sealed record TrackerDiagnostics(
    bool Active,
    bool SeitonMetadataVerified,
    bool GuardMetadataVerified,
    bool RecuperateMetadataVerified,
    bool IsNinja,
    bool IsCrystallineConflict,
    bool IsWolvesDen,
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
    uint ResolvedSeitonActionId,
    int SlotCapacity,
    uint WolvesDenNativeEnemyEntityId,
    bool WolvesDenNativePlayerResolved,
    bool WolvesDenHostileFlag)
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
            0,
            5,
            0,
            false,
            false);

    public string ToChatLine() =>
        $"active={Active}, mode={(IsCrystallineConflict ? "CC" : IsWolvesDen ? "WolvesDen" : "None")}, " +
        $"PvP={IsPvP}, NIN={IsNinja}, " +
        $"metadata[S/G/MP]={SeitonMetadataVerified}/{GuardMetadataVerified}/{RecuperateMetadataVerified}, " +
        $"territory={TerritoryId}, CFC={ContentFinderConditionId}, slots={ResolvedSlots}/{SlotCapacity}, valid={ValidEnemySlots}, " +
        $"range={InRangeSlots}, Seiton={SeitonVisibleSlots}, Guard-CD={GuardUnavailableSlots}, " +
        $"low-MP={LowMpSlots}, popups={PopupCount}, action={ResolvedSeitonActionId}, " +
        $"den[id={WolvesDenNativeEnemyEntityId},resolved={WolvesDenNativePlayerResolved},hostile={WolvesDenHostileFlag}]";
}
