namespace SeitonSense.Core;

/// <summary>
/// Frozen identity created by a real, fresh enemy combat-frame row. It carries
/// values only and never retains a game-object wrapper or native address.
/// </summary>
public readonly record struct CombatFrameTargetIntent(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    long SnapshotPublishedAtMilliseconds)
{
    public bool IsValid =>
        EnemySlot is >= CombatFrameRules.FirstEnemySlot and <= CombatFrameRules.LastEnemySlot &&
        Actor.IsValid &&
        SnapshotPublishedAtMilliseconds >= 0;
}

/// <summary>
/// Same-thread final-preflight facts supplied by the Dalamud integration layer.
/// </summary>
public readonly record struct CombatFrameTargetCandidate(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalSlot,
    bool ExactObjectTableIdentity,
    bool ExactCrystallineConflictContext,
    bool Alive,
    bool Targetable);

public static class CombatFrameInteractionRules
{
    public static bool TryCreateIntent(
        bool snapshotActive,
        bool preview,
        CombatFramePlanRow row,
        long snapshotPublishedAtMilliseconds,
        long nowMilliseconds,
        out CombatFrameTargetIntent intent)
    {
        intent = default;
        if (!snapshotActive ||
            preview ||
            row.Slot is < CombatFrameRules.FirstEnemySlot or > CombatFrameRules.LastEnemySlot ||
            row.Availability != CombatFrameAvailability.Alive ||
            !row.Actor.IsValid ||
            !CombatFrameRules.IsSnapshotFresh(snapshotPublishedAtMilliseconds, nowMilliseconds))
        {
            return false;
        }

        intent = new CombatFrameTargetIntent(
            row.Slot,
            row.Actor,
            snapshotPublishedAtMilliseconds);
        return true;
    }

    public static bool IsSameFrozenTarget(
        CombatFrameTargetIntent pressed,
        CombatFrameTargetIntent released) =>
        pressed.IsValid &&
        released.IsValid &&
        pressed.EnemySlot == released.EnemySlot &&
        pressed.Actor == released.Actor;

    public static bool CanApplyIntent(
        CombatFrameTargetIntent intent,
        CombatFrameTargetCandidate candidate,
        long nowMilliseconds) =>
        intent.IsValid &&
        CombatFrameRules.IsSnapshotFresh(
            intent.SnapshotPublishedAtMilliseconds,
            nowMilliseconds) &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Actor &&
        candidate.ExactCanonicalSlot &&
        candidate.ExactObjectTableIdentity &&
        candidate.ExactCrystallineConflictContext &&
        candidate.Alive &&
        candidate.Targetable;
}
