using SeitonSense.Core;

internal static class ChitenWarningSelfTests
{
    internal static void EpisodeIsOneShotAndBounded()
    {
        const long start = 10_000;
        var entered = ChitenWarningRules.ObserveEpisode(
            ChitenEpisodeState.Initial,
            true,
            5_000,
            start,
            9);
        True(entered.Active, "episode entered");
        Equal(9UL, entered.EpisodeToken, "entry token");
        Equal(start + 5_000, entered.ExpiresAtMilliseconds, "absolute deadline");

        var repeated = ChitenWarningRules.ObserveEpisode(
            entered,
            true,
            5_000,
            start + 50,
            10);
        Equal(9UL, repeated.EpisodeToken, "same status does not rearm");
        Equal(start + 5_000, repeated.ExpiresAtMilliseconds, "repeated remaining time cannot drift");

        var shortMiss = ChitenWarningRules.ObserveEpisode(
            repeated,
            false,
            0,
            start + 150,
            0);
        True(shortMiss.Active, "one short missing sample is tolerated");
        var removed = ChitenWarningRules.ObserveEpisode(
            shortMiss,
            false,
            0,
            start + 201,
            0);
        False(removed.Active, "missing beyond grace removes episode");

        var reapplied = ChitenWarningRules.ObserveEpisode(
            removed,
            true,
            5_000,
            start + 250,
            10);
        True(reapplied.Active, "later exact application rearms");
        Equal(10UL, reapplied.EpisodeToken, "later episode gets a new token");
        False(
            ChitenWarningRules.ObserveEpisode(
                ChitenEpisodeState.Initial,
                true,
                ChitenWarningRules.MaximumDurationMilliseconds + 1,
                start,
                1).Active,
            "overlong status fails closed");
    }

    internal static void DisplayRequiresExactFreshSamurai()
    {
        const long now = 20_000;
        var actor = new TargetPressureActorIdentity(100, 200);
        var observation = new ChitenWarningObservation(
            actor,
            true,
            3,
            ChitenWarningRules.SamuraiJobId,
            ChitenWarningRules.ChitenStatusId,
            ChitenWarningRules.ChitenIconId,
            now - 500,
            now + 4_500,
            now,
            7);
        True(
            ChitenWarningRules.TryBuildWarningPlan(observation, now, out var plan),
            "exact Chiten episode");
        Equal(4_500L, plan.RemainingMilliseconds, "exact countdown");
        True(
            ChitenWarningRules.TryBuildNameplatePlan(actor, now, observation, now, out _),
            "exact fresh native anchor");
        False(Try(observation with { JobId = 30 }, now), "wrong job");
        False(Try(observation with { StatusId = 1_320 }, now), "wrong status");
        False(Try(observation with { IconId = 1 }, now), "wrong icon");
        False(Try(observation with { IsEnemy = false }, now), "ally");
        False(Try(observation with { EnemySlot = 0 }, now), "invalid slot");
        False(Try(observation with { EpisodeToken = 0 }, now), "missing token");
        False(
            Try(
                observation with
                {
                    SnapshotPublishedAtMilliseconds =
                        now - ChitenWarningRules.MaximumSnapshotAgeMilliseconds - 1,
                },
                now),
            "stale snapshot");
        False(
            ChitenWarningRules.TryBuildNameplatePlan(
                new TargetPressureActorIdentity(101, 200),
                now,
                observation,
                now,
                out _),
            "anchor identity mismatch");
    }

    private static bool Try(in ChitenWarningObservation observation, long now) =>
        ChitenWarningRules.TryBuildWarningPlan(observation, now, out _);

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
