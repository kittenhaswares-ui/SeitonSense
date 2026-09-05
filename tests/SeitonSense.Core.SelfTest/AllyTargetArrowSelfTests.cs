using SeitonSense.Core;

internal static class AllyTargetArrowSelfTests
{
    private static readonly TargetPressureActorIdentity Local = new(100, 10);
    private static readonly TargetPressureActorIdentity Ally = new(200, 20);
    private static readonly TargetPressureActorIdentity Enemy = new(300, 30);
    private static readonly TargetPressureActorIdentity OtherEnemy = new(400, 40);

    internal static void TargetChangesAreOneShotAndNeverPointAtFriends()
    {
        var tracker = new AllyTargetArrowTracker();
        Equal(0, Observe(tracker, 1_000, null).Count, "startup is a silent baseline");
        Pulse(Observe(tracker, 1_100, Enemy), Enemy, 1_100);
        Pulse(Observe(tracker, 1_200, Enemy), Enemy, 1_100);
        Pulse(Observe(tracker, 1_300, OtherEnemy), OtherEnemy, 1_300);
        Equal(0, Observe(tracker, 1_400, null).Count, "friendly/unknown target is absent and removes old cue");
        Equal(0, Observe(tracker, 1_500, Local).Count, "local target never becomes ally arrow");
        Equal(0, Observe(tracker, 1_600, Ally).Count, "self target never becomes ally arrow");
        Pulse(Observe(tracker, 1_700, Enemy), Enemy, 1_700);
        var expires = 1_700 + AggressorArrowRules.MaximumPulseRetentionMilliseconds;
        for (var now = 1_900L; now < expires; now += 200)
            Pulse(Observe(tracker, now, Enemy), Enemy, 1_700);
        Equal(0, Observe(tracker, expires, Enemy).Count, "steady focus never refreshes an expired cue");
    }

    internal static void ContextDeathDuplicatesAndIdentityReuseFailClosed()
    {
        var tracker = new AllyTargetArrowTracker();
        Observe(tracker, 1_000, null);
        Observe(tracker, 1_100, Enemy);
        Equal(0, tracker.Observe(true, Local, 1_200,
            [new(Ally, Enemy, true), new(Ally, OtherEnemy, true)]).Count, "duplicate ally is ambiguous");
        Equal(0, Observe(tracker, 1_300, Enemy).Count, "returned identity is a silent baseline");
        Equal(0, tracker.Observe(true, Local, 1_400, [new(Ally, Enemy, false)]).Count,
            "dead or ineligible ally removes cue");
        Equal(0, Observe(tracker, 1_500, Enemy).Count, "respawn does not flash");
        Observe(tracker, 1_600, null);
        Pulse(Observe(tracker, 1_700, Enemy), Enemy, 1_700);
        Equal(0, Observe(tracker, 2_201, OtherEnemy).Count, "stale publication resets baseline");
        Equal(0, Observe(tracker, 2_100, Enemy).Count, "backward clock resets baseline");
        Equal(0, Observe(tracker, 2_200, new(Enemy.GameObjectId, 99)).Count,
            "target half-ID reuse cannot create a new-target arrow");
        Equal(0, tracker.Observe(false, Local, 2_300, [new(Ally, Enemy, true)]).Count,
            "feature off resets state");
        Equal(0, Observe(tracker, 2_400, Enemy).Count, "re-enable baseline is silent");
        Equal(0, tracker.Observe(true, new(101, 11), 2_500, [new(Ally, OtherEnemy, true)]).Count,
            "local identity change is a new context");
    }

    internal static void MultipleAlliesMayFocusTheSameExactEnemy()
    {
        var tracker = new AllyTargetArrowTracker();
        var secondAlly = new TargetPressureActorIdentity(201, 21);
        tracker.Observe(true, Local, 1_000, [new(Ally, null, true), new(secondAlly, null, true)]);
        var pulses = tracker.Observe(true, Local, 1_100,
            [new(Ally, Enemy, true), new(secondAlly, Enemy, true)]);
        Equal(2, pulses.Count, "shared enemy focus is not a duplicate-source conflict");
        Equal(true, pulses.All(p => p.Target == Enemy && p.StartedAtMilliseconds == 1_100),
            "both exact allies point to the same hostile target");
        var newAlly = new TargetPressureActorIdentity(202, 22);
        pulses = tracker.Observe(true, Local, 1_200,
            [new(Ally, Enemy, true), new(secondAlly, Enemy, true), new(newAlly, OtherEnemy, true)]);
        Equal(3, pulses.Count, "genuinely new ally after initial publication can signal");
    }

    internal static void LongerDurationMigrationPreservesCustomSettings()
    {
        Equal(2f, AggressorArrowRules.MigrateLegacyDuration(.75f), "old default upgrades once");
        foreach (var custom in new[] { .35f, .8f, 1f, 1.5f, 3.5f })
            Equal(custom, AggressorArrowRules.MigrateLegacyDuration(custom), "custom duration stays unchanged");
        Equal(2f, AggressorArrowRules.MigrateLegacyDuration(float.NaN), "invalid duration gets new default");
        Equal(true, AggressorArrowRules.MaximumPulseRetentionMilliseconds >=
            AggressorArrowRules.MaximumDurationSeconds * 1_000f, "retention covers longest configured duration");
        Equal(true, AggressorArrowRules.PulseAlpha(1_000, 2_000, 2f, false) > 0f,
            "default remains visible after one second");
        Equal(0f, AggressorArrowRules.PulseAlpha(1_000, 3_000, 2f, false),
            "longer default still expires exactly");
    }

    private static IReadOnlyList<AllyTargetArrowPulse> Observe(AllyTargetArrowTracker tracker,
        long now, TargetPressureActorIdentity? target) =>
        tracker.Observe(true, Local, now, [new(Ally, target, true)]);

    private static void Pulse(IReadOnlyList<AllyTargetArrowPulse> pulses,
        TargetPressureActorIdentity target, long started)
    {
        Equal(1, pulses.Count, "one current ally pulse");
        Equal(Ally, pulses[0].Ally, "source stays exact");
        Equal(target, pulses[0].Target, "target stays exact");
        Equal(started, pulses[0].StartedAtMilliseconds, "same target does not refresh pulse");
    }

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
