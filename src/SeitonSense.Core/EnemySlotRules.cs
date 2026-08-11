namespace SeitonSense.Core;

public static class EnemySlotRules
{
    public const int FirstSlot = 1;
    public const int LastSlot = 5;

    public static bool IsValidSlot(int slot) => slot is >= FirstSlot and <= LastSlot;

    public static string Label(int slot) => IsValidSlot(slot)
        ? $"S{slot}"
        : string.Empty;

    public static bool CanUseResolvedEnemy(
        bool isSelf,
        bool isPartyOrAllianceMember,
        bool hasHostileFlag,
        bool hasCompleteCcPartyFallback,
        bool isAlive,
        bool isTargetable,
        uint currentHp,
        uint maxHp) =>
        !isSelf &&
        !isPartyOrAllianceMember &&
        (hasHostileFlag || hasCompleteCcPartyFallback) &&
        isAlive &&
        isTargetable &&
        ExecuteThreshold.HasValidHp(currentHp, maxHp);
}
