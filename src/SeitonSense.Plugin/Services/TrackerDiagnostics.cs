namespace SeitonSense.Plugin.Services;

internal sealed record TrackerDiagnostics(
    bool Active,
    bool MetadataVerified,
    bool IsNinja,
    bool IsCrystallineConflict,
    bool IsPvP,
    uint TerritoryId,
    uint ContentFinderConditionId,
    int ResolvedSlots,
    int ValidEnemySlots,
    int InRangeSlots,
    int ReadySlots,
    int BelowHalfSlots,
    uint ResolvedSeitonActionId,
    bool FlashActive)
{
    public static TrackerDiagnostics Inactive(uint territoryId = 0, bool metadataVerified = true) =>
        new(false, metadataVerified, false, false, false, territoryId, 0, 0, 0, 0, 0, 0, 0, false);

    public string ToChatLine() =>
        $"active={Active}, metadata={MetadataVerified}, NIN={IsNinja}, CC={IsCrystallineConflict}, PvP={IsPvP}, " +
        $"territory={TerritoryId}, CFC={ContentFinderConditionId}, " +
        $"slots={ResolvedSlots}/5, valid={ValidEnemySlots}, in-range={InRangeSlots}, " +
        $"ready={ReadySlots}, below-50={BelowHalfSlots}, Seiton={ResolvedSeitonActionId}, flash={FlashActive}";
}
