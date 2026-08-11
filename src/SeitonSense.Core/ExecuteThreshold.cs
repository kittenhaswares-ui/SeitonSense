namespace SeitonSense.Core;

public static class ExecuteThreshold
{
    public const uint NinjaJobId = 30;
    public const uint RearmPercent = 52;

    public static bool IsNinja(uint jobId) => jobId == NinjaJobId;

    public static bool HasValidHp(uint currentHp, uint maxHp) =>
        maxHp > 0 && currentHp > 0 && currentHp <= maxHp;

    public static bool IsBelowHalf(uint currentHp, uint maxHp) =>
        HasValidHp(currentHp, maxHp) &&
        ((ulong)currentHp * 2UL) < maxHp;

    public static bool IsAtOrAboveRearm(uint currentHp, uint maxHp) =>
        HasValidHp(currentHp, maxHp) &&
        ((ulong)currentHp * 100UL) >= ((ulong)maxHp * RearmPercent);
}
