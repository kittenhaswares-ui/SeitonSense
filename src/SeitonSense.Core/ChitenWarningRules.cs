namespace SeitonSense.Core;

public readonly record struct ChitenEpisodeState(
    bool Active,
    long ActivatedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long LastSeenAtMilliseconds,
    ulong EpisodeToken)
{
    public static ChitenEpisodeState Initial { get; } = new(false, -1, -1, -1, 0);
}

public readonly record struct ChitenWarningObservation(
    TargetPressureActorIdentity Actor,
    bool IsEnemy,
    int EnemySlot,
    uint JobId,
    uint StatusId,
    uint IconId,
    long ActivatedAtMilliseconds,
    long ExpiresAtMilliseconds,
    long SnapshotPublishedAtMilliseconds,
    ulong EpisodeToken);

public readonly record struct ChitenWarningPlan(
    TargetPressureActorIdentity Actor,
    int EnemySlot,
    uint IconId,
    long ActivatedAtMilliseconds,
    long RemainingMilliseconds,
    ulong EpisodeToken);

/// <summary>
/// Exact, status-driven episode and display admission for enemy Samurai Chiten.
/// The caller owns actor identity and metadata verification; these rules prevent
/// repeated RemainingTime samples from extending an episode indefinitely.
/// </summary>
public static class ChitenWarningRules
{
    public const uint SamuraiJobId = 34;
    public const uint ChitenStatusId = 1_240;
    public const uint ChitenIconId = 214_820;
    public const long MaximumDurationMilliseconds = 10_000;
    public const long MissingGraceMilliseconds = 150;
    public const long MaximumSnapshotAgeMilliseconds = 500;
    public const long MaximumAnchorAgeMilliseconds = 250;

    public static ChitenEpisodeState ObserveEpisode(
        in ChitenEpisodeState previous,
        bool hasExactStatus,
        long remainingMilliseconds,
        long nowMilliseconds,
        ulong nextEpisodeToken,
        bool hardReset = false)
    {
        if (hardReset || nowMilliseconds < 0) return ChitenEpisodeState.Initial;

        var validStatus = hasExactStatus &&
                          remainingMilliseconds is > 0 and <= MaximumDurationMilliseconds;
        if (validStatus)
        {
            var observedExpiry = SaturatingAdd(nowMilliseconds, remainingMilliseconds);
            if (!previous.Active ||
                previous.ExpiresAtMilliseconds <= 0 ||
                previous.LastSeenAtMilliseconds < 0 ||
                nowMilliseconds - previous.LastSeenAtMilliseconds > MissingGraceMilliseconds)
            {
                if (nextEpisodeToken == 0) return ChitenEpisodeState.Initial;
                return new ChitenEpisodeState(
                    true,
                    nowMilliseconds,
                    observedExpiry,
                    nowMilliseconds,
                    nextEpisodeToken);
            }

            // Adjacent framework frames can expose the same RemainingTime. Keep
            // the earliest absolute deadline so that the warning cannot drift.
            return previous with
            {
                ExpiresAtMilliseconds = Math.Min(previous.ExpiresAtMilliseconds, observedExpiry),
                LastSeenAtMilliseconds = nowMilliseconds,
            };
        }

        if (previous.Active &&
            previous.ExpiresAtMilliseconds > nowMilliseconds &&
            previous.LastSeenAtMilliseconds >= 0 &&
            nowMilliseconds - previous.LastSeenAtMilliseconds <= MissingGraceMilliseconds)
        {
            return previous;
        }

        return ChitenEpisodeState.Initial;
    }

    public static bool TryBuildWarningPlan(
        in ChitenWarningObservation observation,
        long nowMilliseconds,
        out ChitenWarningPlan plan)
    {
        plan = default;
        if (!observation.Actor.IsValid ||
            !observation.IsEnemy ||
            observation.EnemySlot is < 1 or > 5 ||
            observation.JobId != SamuraiJobId ||
            observation.StatusId != ChitenStatusId ||
            observation.IconId != ChitenIconId ||
            observation.ActivatedAtMilliseconds < 0 ||
            observation.ActivatedAtMilliseconds > nowMilliseconds ||
            observation.ExpiresAtMilliseconds <= nowMilliseconds ||
            observation.ExpiresAtMilliseconds - observation.ActivatedAtMilliseconds >
            MaximumDurationMilliseconds ||
            observation.EpisodeToken == 0 ||
            !IsFresh(observation.SnapshotPublishedAtMilliseconds, nowMilliseconds, MaximumSnapshotAgeMilliseconds))
        {
            return false;
        }

        plan = new ChitenWarningPlan(
            observation.Actor,
            observation.EnemySlot,
            observation.IconId,
            observation.ActivatedAtMilliseconds,
            observation.ExpiresAtMilliseconds - nowMilliseconds,
            observation.EpisodeToken);
        return true;
    }

    public static bool TryBuildNameplatePlan(
        TargetPressureActorIdentity anchorActor,
        long anchorCapturedAtMilliseconds,
        in ChitenWarningObservation observation,
        long nowMilliseconds,
        out ChitenWarningPlan plan)
    {
        plan = default;
        return anchorActor.IsValid &&
               observation.Actor == anchorActor &&
               IsFresh(anchorCapturedAtMilliseconds, nowMilliseconds, MaximumAnchorAgeMilliseconds) &&
               TryBuildWarningPlan(observation, nowMilliseconds, out plan);
    }

    private static bool IsFresh(long observedAtMilliseconds, long nowMilliseconds, long maximumAge) =>
        observedAtMilliseconds >= 0 &&
        nowMilliseconds >= observedAtMilliseconds &&
        nowMilliseconds - observedAtMilliseconds <= maximumAge;

    private static long SaturatingAdd(long left, long right) =>
        right > long.MaxValue - left ? long.MaxValue : left + right;
}
