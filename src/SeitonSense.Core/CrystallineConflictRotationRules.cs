namespace SeitonSense.Core;

public enum CrystallineConflictArena
{
    ThePalaistra,
    TheVolcanicHeart,
    TheBaysideBattleground,
    CloudNine,
    TheClockworkCastletown,
    ArcheiaHarmonias,
    TheRedSands,
}

public readonly record struct CrystallineConflictRotationSnapshot(
    CrystallineConflictArena CurrentArena,
    CrystallineConflictArena NextArena,
    long SlotStartUnixSeconds,
    long SlotEndUnixSeconds,
    int RemainingSeconds,
    int EffectiveOffsetSlots);

/// <summary>
/// Local, deterministic Crystalline Conflict duty rotation. Patch 7.5 changed
/// the published sequence to seven maps at one-hour intervals. The bundled
/// reference instant matches the public community calendar's first
/// post-maintenance Palaistra slot. A persisted
/// whole-slot correction lets the user calibrate map phase without adding a
/// network dependency or changing slot boundaries.
/// </summary>
public static class CrystallineConflictRotationRules
{
    public const long Patch75ReferenceUnixSeconds = 1_777_381_200; // 2026-04-28 13:00:00 UTC
    public const int RotationSeconds = 60 * 60;
    public const int ArenaCount = 7;

    private static readonly CrystallineConflictArena[] Rotation =
    [
        CrystallineConflictArena.ThePalaistra,
        CrystallineConflictArena.TheVolcanicHeart,
        CrystallineConflictArena.TheBaysideBattleground,
        CrystallineConflictArena.CloudNine,
        CrystallineConflictArena.TheClockworkCastletown,
        CrystallineConflictArena.ArcheiaHarmonias,
        CrystallineConflictArena.TheRedSands,
    ];

    public static bool IsExactWolvesDenContext(
        bool isPvP,
        bool isPvPExcludingWolvesDen,
        uint territoryId) =>
        isPvP &&
        !isPvPExcludingWolvesDen &&
        territoryId == PvPMatchRules.WolvesDenPierTerritoryId;

    public static bool TryResolve(
        bool isPvP,
        bool isPvPExcludingWolvesDen,
        uint territoryId,
        long unixTimeSeconds,
        out CrystallineConflictRotationSnapshot snapshot,
        int phaseOffsetSlots = 0)
    {
        snapshot = default;
        if (!IsExactWolvesDenContext(isPvP, isPvPExcludingWolvesDen, territoryId) ||
            unixTimeSeconds < Patch75ReferenceUnixSeconds)
        {
            return false;
        }

        var elapsedSeconds = unixTimeSeconds - Patch75ReferenceUnixSeconds;
        var absoluteSlot = elapsedSeconds / RotationSeconds;
        var slotStart = Patch75ReferenceUnixSeconds + (absoluteSlot * RotationSeconds);
        if (slotStart > long.MaxValue - RotationSeconds) return false;

        var slotEnd = slotStart + RotationSeconds;
        var baseIndex = (int)(absoluteSlot % ArenaCount);
        var effectiveOffset = NormalizeIndex(phaseOffsetSlots);
        var currentIndex = NormalizeIndex(baseIndex + effectiveOffset);
        var nextIndex = NormalizeIndex(currentIndex + 1);
        var remaining = (int)Math.Clamp(slotEnd - unixTimeSeconds, 0L, RotationSeconds);

        snapshot = new CrystallineConflictRotationSnapshot(
            Rotation[currentIndex],
            Rotation[nextIndex],
            slotStart,
            slotEnd,
            remaining,
            effectiveOffset);
        return true;
    }

    public static CrystallineConflictArena GetArena(int index) => Rotation[NormalizeIndex(index)];

    public static string GetDisplayName(CrystallineConflictArena arena) => arena switch
    {
        CrystallineConflictArena.ThePalaistra => "The Palaistra",
        CrystallineConflictArena.TheVolcanicHeart => "The Volcanic Heart",
        CrystallineConflictArena.TheBaysideBattleground => "The Bayside Battleground",
        CrystallineConflictArena.CloudNine => "Cloud Nine",
        CrystallineConflictArena.TheClockworkCastletown => "The Clockwork Castletown",
        CrystallineConflictArena.ArcheiaHarmonias => "Archeia Harmonias",
        CrystallineConflictArena.TheRedSands => "The Red Sands",
        _ => "Unknown arena",
    };

    public static string FormatCountdown(int remainingSeconds)
    {
        var safeSeconds = Math.Clamp(remainingSeconds, 0, RotationSeconds);
        return $"{safeSeconds / 60:00}:{safeSeconds % 60:00}";
    }

    private static int NormalizeIndex(int index)
    {
        var normalized = index % ArenaCount;
        return normalized < 0 ? normalized + ArenaCount : normalized;
    }
}
