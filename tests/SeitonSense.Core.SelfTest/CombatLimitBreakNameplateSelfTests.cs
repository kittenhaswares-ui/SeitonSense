using SeitonSense.Core;

internal static class CombatLimitBreakNameplateSelfTests
{
    internal static void DisplayRequiresFreshExactEnemyIdentity()
    {
        const long now = 10_000;
        var actor = new TargetPressureActorIdentity(100, 200);
        var observation = Observation(actor, now);

        True(Try(actor, now, observation, now, out var plan), "fresh exact enemy");
        Equal(actor, plan.Actor, "plan actor");
        Equal(3, plan.EnemySlot, "plan slot");
        Equal(9_610u, plan.IconId, "plan icon");
        True(plan.ShowCountdown, "confirmed duration countdown");
        Equal(5_000L, plan.RemainingMilliseconds, "exact remaining duration");

        False(Try(new TargetPressureActorIdentity(101, 200), now, observation, now, out _), "wrong GOID");
        False(Try(new TargetPressureActorIdentity(100, 201), now, observation, now, out _), "wrong entity ID");
        False(Try(actor, now, observation with { IsEnemy = false }, now, out _), "ally side");
        False(Try(actor, now, observation with { EnemySlot = 0 }, now, out _), "self slot");
        False(Try(actor, now, observation with { IconId = 0 }, now, out _), "missing icon");
        False(Try(actor, now - 251, observation, now, out _), "stale anchor");
        False(Try(actor, now + 1, observation, now, out _), "future anchor");
        False(Try(actor, now, observation with { SnapshotPublishedAtMilliseconds = now - 501 }, now, out _), "stale runtime");
        False(Try(actor, now, observation with { SnapshotPublishedAtMilliseconds = now + 1 }, now, out _), "future runtime");
        False(Try(actor, now, observation with { ExpiresAtMilliseconds = now }, now, out _), "expired episode");
    }

    internal static void CountdownRequiresConfirmedDurationAndFlashIsBounded()
    {
        const long now = 10_000;
        var actor = new TargetPressureActorIdentity(100, 200);
        var confirmed = Observation(actor, now);

        True(Try(actor, now, confirmed, now, out var duration), "confirmed duration");
        True(duration.ShowCountdown, "confirmed duration shows countdown");

        var instant = confirmed with
        {
            Presentation = CombatLimitBreakPresentationKind.Instant,
            DurationConfirmed = true,
            ActivatedAtMilliseconds = now - 500,
            ExpiresAtMilliseconds = now + 1_300,
        };
        True(Try(actor, now, instant, now, out var instantPlan), "instant flash");
        False(instantPlan.ShowCountdown, "instant never shows countdown");

        var unconfirmed = instant with
        {
            Presentation = CombatLimitBreakPresentationKind.Duration,
            DurationConfirmed = false,
        };
        True(Try(actor, now, unconfirmed, now, out var unconfirmedPlan), "unconfirmed duration flash");
        False(unconfirmedPlan.ShowCountdown, "unconfirmed duration never invents countdown");

        False(
            Try(
                actor,
                now,
                unconfirmed with { ExpiresAtMilliseconds = now + 1_301 },
                now,
                out _),
            "overlong unconfirmed flash");
        False(
            Try(
                actor,
                now,
                unconfirmed with { ActivatedAtMilliseconds = now + 1 },
                now,
                out _),
            "future activation");
    }

    internal static void VerticalStackIsDeterministicAndNeverOverlaps()
    {
        var requests = new[]
        {
            new NameplateVerticalStackRequest(100f, 60f),
            new NameplateVerticalStackRequest(100f, 60f),
        };

        True(
            CombatLimitBreakNameplateRules.TryBuildVerticalStack(
                250f,
                10f,
                10f,
                8f,
                requests,
                out var full),
            "full stack");
        Equal(2, full.Length, "full stack count");
        NearlyEqual(140f, full[0].Top, "LB top");
        NearlyEqual(240f, full[0].Bottom, "LB bottom");
        NearlyEqual(32f, full[1].Top, "CC top");
        NearlyEqual(132f, full[1].Bottom, "CC bottom");
        True(full[1].Bottom < full[0].Top, "vertical gap prevents overlap");
        NearlyEqual(1f, full[0].Scale, "full scale");

        True(
            CombatLimitBreakNameplateRules.TryBuildVerticalStack(
                160f,
                10f,
                10f,
                8f,
                requests,
                out var shrunk),
            "shrunk stack");
        True(shrunk[0].Scale is > 0.65f and < 0.67f, "shared shrink factor");
        NearlyEqual(shrunk[0].Scale, shrunk[1].Scale, "same scale for every block");
        True(shrunk[1].Bottom < shrunk[0].Top, "shrunk stack does not overlap");

        False(
            CombatLimitBreakNameplateRules.TryBuildVerticalStack(
                130f,
                10f,
                10f,
                8f,
                requests,
                out _),
            "below minimum stack");
        False(
            CombatLimitBreakNameplateRules.TryBuildVerticalStack(
                250f,
                10f,
                10f,
                8f,
                [new NameplateVerticalStackRequest(float.NaN, 10f)],
                out _),
            "invalid geometry");
    }

    private static CombatLimitBreakNameplateObservation Observation(
        TargetPressureActorIdentity actor,
        long now) => new(
        actor,
        true,
        3,
        9_610,
        CombatLimitBreakPresentationKind.Duration,
        true,
        now - 1_000,
        now + 5_000,
        now);

    private static bool Try(
        TargetPressureActorIdentity anchor,
        long anchorCapturedAt,
        in CombatLimitBreakNameplateObservation observation,
        long now,
        out CombatLimitBreakNameplateDisplayPlan plan) =>
        CombatLimitBreakNameplateRules.TryBuildDisplayPlan(
            anchor,
            anchorCapturedAt,
            observation,
            now,
            out plan);

    private static void NearlyEqual(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void Equal<T>(T expected, T actual, string message)
        where T : IEquatable<T>
    {
        if (!actual.Equals(expected))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool value, string message)
    {
        if (value) throw new InvalidOperationException($"Expected false: {message}");
    }
}
