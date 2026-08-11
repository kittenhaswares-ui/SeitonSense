namespace SeitonSense.Core;

public enum SupportedPvPContext
{
    None,
    CrystallineConflict,
    WolvesDen,
}

public static class PvPMatchRules
{
    public const uint WolvesDenPierTerritoryId = 250;

    public static bool IsPublicCrystallineConflictTerritory(uint territoryId) => territoryId is
        1032 or // The Palaistra
        1033 or // The Volcanic Heart
        1034 or // Cloud Nine
        1116 or // The Clockwork Castletown
        1138 or // The Red Sands
        1293 or // Bayside Battleground
        1357;   // The Archeia Harmonias

    public static bool IsKnownCrystallineConflictTerritory(uint territoryId) =>
        IsPublicCrystallineConflictTerritory(territoryId) ||
        territoryId is
            1058 or // The Palaistra (custom)
            1059 or // The Volcanic Heart (custom)
            1060 or // Cloud Nine (custom)
            1117 or // The Clockwork Castletown (custom)
            1139 or // The Red Sands (custom)
            1294 or // Bayside Battleground (custom)
            1358;   // The Archeia Harmonias (custom)

    public static bool IsCrystallineConflict(
        bool isPvPExcludingWolvesDen,
        uint territoryId,
        bool conditionValid,
        bool conditionPvP,
        uint contentUiCategoryId,
        bool casualRoulette,
        bool rankedRoulette) =>
        isPvPExcludingWolvesDen &&
        (IsKnownCrystallineConflictTerritory(territoryId) ||
         (conditionValid && conditionPvP &&
          (contentUiCategoryId is 43 or 44 || casualRoulette || rankedRoulette)));

    public static SupportedPvPContext ResolveSupportedContext(
        bool isPvP,
        bool isPvPExcludingWolvesDen,
        bool includeWolvesDenTesting,
        uint territoryId,
        bool conditionValid,
        bool conditionPvP,
        uint contentUiCategoryId,
        bool casualRoulette,
        bool rankedRoulette)
    {
        if (IsCrystallineConflict(
                isPvPExcludingWolvesDen,
                territoryId,
                conditionValid,
                conditionPvP,
                contentUiCategoryId,
                casualRoulette,
                rankedRoulette))
        {
            return SupportedPvPContext.CrystallineConflict;
        }

        return includeWolvesDenTesting &&
               isPvP &&
               !isPvPExcludingWolvesDen &&
               territoryId == WolvesDenPierTerritoryId
            ? SupportedPvPContext.WolvesDen
            : SupportedPvPContext.None;
    }
}
