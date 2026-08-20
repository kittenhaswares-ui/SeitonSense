using SeitonSense.Core;

internal static class CombatFrameInteractionSelfTests
{
    private static readonly TargetPressureActorIdentity Actor = new(10_001, 20_001);

    internal static void OnlyFreshRealAliveEnemyRowsCreateIntents()
    {
        var row = AliveRow();
        True(
            CombatFrameInteractionRules.TryCreateIntent(
                snapshotActive: true,
                preview: false,
                row,
                snapshotPublishedAtMilliseconds: 1_000,
                nowMilliseconds: 1_500,
                out var exact),
            "fresh 500 ms boundary");
        Equal(2, exact.EnemySlot, "exact S-slot");
        Equal(Actor, exact.Actor, "exact actor");

        False(TryCreate(row, active: false), "inactive snapshot");
        False(TryCreate(row, preview: true), "synthetic preview");
        False(TryCreate(row with { Availability = CombatFrameAvailability.Dead }), "dead row");
        False(TryCreate(row with { Availability = CombatFrameAvailability.Unknown }), "unknown row");
        False(TryCreate(row with { Slot = CombatFrameRules.SelfSlot }), "self row");
        False(TryCreate(row with { Actor = default }), "unknown identity");
        False(
            CombatFrameInteractionRules.TryCreateIntent(
                true,
                false,
                row,
                snapshotPublishedAtMilliseconds: 1_000,
                nowMilliseconds: 1_501,
                out _),
            "stale snapshot");
    }

    internal static void PressAndReleaseRequireTheSameFrozenActor()
    {
        var pressed = Intent(2, Actor);
        var laterPublication = pressed with { SnapshotPublishedAtMilliseconds = 1_200 };
        True(
            CombatFrameInteractionRules.IsSameFrozenTarget(pressed, laterPublication),
            "a refreshed snapshot may retain the same exact actor");
        False(
            CombatFrameInteractionRules.IsSameFrozenTarget(
                pressed,
                laterPublication with { EnemySlot = 3 }),
            "row drift");
        False(
            CombatFrameInteractionRules.IsSameFrozenTarget(
                pressed,
                laterPublication with { Actor = new TargetPressureActorIdentity(10_002, 20_002) }),
            "identity drift");
        False(
            CombatFrameInteractionRules.IsSameFrozenTarget(default, laterPublication),
            "missing press");
    }

    internal static void EveryFinalTargetGateFailsClosed()
    {
        var intent = Intent(2, Actor);
        var candidate = new CombatFrameTargetCandidate(
            2,
            Actor,
            ExactCanonicalSlot: true,
            ExactObjectTableIdentity: true,
            ExactCrystallineConflictContext: true,
            Alive: true,
            Targetable: true);
        True(CombatFrameInteractionRules.CanApplyIntent(intent, candidate, 1_500), "exact candidate");

        False(CanApply(intent, candidate with { EnemySlot = 3 }), "slot drift");
        False(
            CanApply(
                intent,
                candidate with { Actor = new TargetPressureActorIdentity(10_002, 20_002) }),
            "identity drift");
        False(CanApply(intent, candidate with { ExactCanonicalSlot = false }), "canonical resolver failure");
        False(CanApply(intent, candidate with { ExactObjectTableIdentity = false }), "object-table mismatch");
        False(
            CanApply(intent, candidate with { ExactCrystallineConflictContext = false }),
            "context drift");
        False(CanApply(intent, candidate with { Alive = false }), "dead target");
        False(CanApply(intent, candidate with { Targetable = false }), "untargetable target");
        False(CombatFrameInteractionRules.CanApplyIntent(intent, candidate, 1_501), "stale intent");
    }

    private static CombatFramePlanRow AliveRow() => new(
        2,
        CombatFrameAvailability.Alive,
        Actor,
        30,
        40_000,
        50_000,
        8_000,
        10_000,
        true,
        true,
        false,
        false,
        0,
        0,
        CombatFrameIncomingEvidence.None);

    private static CombatFrameTargetIntent Intent(
        int slot,
        TargetPressureActorIdentity actor) => new(slot, actor, 1_000);

    private static bool TryCreate(
        CombatFramePlanRow row,
        bool active = true,
        bool preview = false) =>
        CombatFrameInteractionRules.TryCreateIntent(active, preview, row, 1_000, 1_100, out _);

    private static bool CanApply(
        CombatFrameTargetIntent intent,
        CombatFrameTargetCandidate candidate) =>
        CombatFrameInteractionRules.CanApplyIntent(intent, candidate, 1_100);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
