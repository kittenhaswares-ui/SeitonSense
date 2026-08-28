namespace SeitonSense.Core;

/// <summary>
/// Read-only presentation metadata for the local Crystalline Conflict
/// rotation. Artwork IDs are the game's own 376x120 custom-match duty
/// banners; no bundled copies or runtime downloads are required.
/// </summary>
public static class CrystallineConflictRotationPresentationRules
{
    public const float CardReorderSeconds = 0.65f;

    public static uint GetDutyArtworkIconId(CrystallineConflictArena arena) => arena switch
    {
        CrystallineConflictArena.ThePalaistra => 112473,
        CrystallineConflictArena.TheVolcanicHeart => 112474,
        CrystallineConflictArena.CloudNine => 112475,
        CrystallineConflictArena.TheClockworkCastletown => 112517,
        CrystallineConflictArena.TheRedSands => 112548,
        CrystallineConflictArena.TheBaysideBattleground => 112629,
        CrystallineConflictArena.ArcheiaHarmonias => 112669,
        _ => 0,
    };

    public static CrystallineConflictArena GetArenaAtForwardSlot(
        CrystallineConflictArena currentArena,
        int forwardSlot) =>
        CrystallineConflictRotationRules.GetArena(GetPublishedIndex(currentArena) + forwardSlot);

    /// <summary>
    /// Resolves an arena's animated vertical card position. At a rotation
    /// boundary the former first card travels to the bottom while all later
    /// cards move one position upward.
    /// </summary>
    public static float ResolveAnimatedCardSlot(
        CrystallineConflictArena previousCurrentArena,
        CrystallineConflictArena currentArena,
        CrystallineConflictArena arena,
        float progress)
    {
        var start = GetForwardSlot(previousCurrentArena, arena);
        var end = GetForwardSlot(currentArena, arena);
        var eased = EaseInOutCubic(Math.Clamp(progress, 0f, 1f));
        return start + ((end - start) * eased);
    }

    private static int GetForwardSlot(
        CrystallineConflictArena currentArena,
        CrystallineConflictArena arena)
    {
        var slot = (GetPublishedIndex(arena) - GetPublishedIndex(currentArena)) %
                   CrystallineConflictRotationRules.ArenaCount;
        return slot < 0 ? slot + CrystallineConflictRotationRules.ArenaCount : slot;
    }

    private static int GetPublishedIndex(CrystallineConflictArena arena) => arena switch
    {
        CrystallineConflictArena.ThePalaistra => 0,
        CrystallineConflictArena.TheVolcanicHeart => 1,
        CrystallineConflictArena.TheBaysideBattleground => 2,
        CrystallineConflictArena.CloudNine => 3,
        CrystallineConflictArena.TheClockworkCastletown => 4,
        CrystallineConflictArena.ArcheiaHarmonias => 5,
        CrystallineConflictArena.TheRedSands => 6,
        _ => 0,
    };

    private static float EaseInOutCubic(float value) => value < 0.5f
        ? 4f * value * value * value
        : 1f - (MathF.Pow((-2f * value) + 2f, 3f) * 0.5f);
}
