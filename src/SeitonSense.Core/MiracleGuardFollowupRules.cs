using System.Collections.Immutable;

namespace SeitonSense.Core;

public enum MiracleGuardFollowupPhase
{
    WaitingForGuard = 0,
    GuardPresent = 1,
    ReleaseOpportunity = 2,
    RetiredUntilGuardAbsent = 3,
}

public enum MiracleGuardFollowupDecisionKind
{
    None = 0,
    Waiting = 1,
    ReadyForPromotion = 2,
    Cancelled = 3,
}

public enum MiracleGuardFollowupCancelReason
{
    None = 0,
    ConfigurationDisabled = 1,
    OutsideCrystallineConflict = 2,
    LocalCounterJobInvalid = 3,
    ClockMovedBackwards = 4,
    HardReset = 5,
}

public readonly record struct MiracleGuardFollowupTargetIdentity(
    int EnemySlot,
    ulong GameObjectId,
    uint EntityId,
    uint JobId)
{
    public bool IsValid =>
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        TargetHighlightRules.IsValidGameObjectId(GameObjectId) &&
        MiracleGuardFollowupRules.IsValidEntityId(EntityId) &&
        JobId != 0;
}

/// <summary>
/// One immutable framework observation of an exact canonical S1-S5 enemy.
/// TeamTargetCountKnown is false for an inactive, future-dated, or stale
/// pressure publication and must never be interpreted as zero.
/// </summary>
public readonly record struct MiracleGuardFollowupCandidate(
    MiracleGuardFollowupTargetIdentity Target,
    bool IsExactCanonicalEnemy,
    bool IsAliveAndTargetable,
    int ActiveGuardStatusCount,
    uint CurrentHp,
    uint MaximumHp,
    bool TeamTargetCountKnown,
    int TeamTargetCount)
{
    public bool HasTrustedMp { get; init; }
    public uint CurrentMp { get; init; }
    public uint MaximumMp { get; init; }
    /// <summary>
    /// Advisory only. Live Guard StatusList membership remains authoritative.
    /// </summary>
    public long GuardRemainingMilliseconds { get; init; }
    public int ReservationGameplayKeyToken { get; init; }
    public bool ReservedGameplayKeyPhysicallyDown { get; init; }
    public bool CounterActionReachable { get; init; }

    public bool IsValid =>
        Target.IsValid &&
        IsExactCanonicalEnemy &&
        IsAliveAndTargetable &&
        ActiveGuardStatusCount is 0 or 1 &&
        CurrentHp > 0 &&
        MaximumHp >= CurrentHp &&
        (!TeamTargetCountKnown || TeamTargetCount >= 0) &&
        (!HasTrustedMp ||
         (MaximumMp == CombatFrameRules.ExpectedMaximumMp && CurrentMp <= MaximumMp));

    public MiracleProtectionEndRankCandidate RankCandidate => new(
        MiracleInterceptThreatKind.PostGuardCrowdControl,
        Target.EnemySlot,
        Target.GameObjectId,
        Target.EntityId,
        Target.JobId,
        TeamTargetCountKnown,
        TeamTargetCount,
        CurrentHp,
        MaximumHp,
        HasTrustedMp,
        CurrentMp,
        MaximumMp);
}

public readonly record struct MiracleGuardFollowupActorState(
    MiracleGuardFollowupTargetIdentity Target,
    MiracleGuardFollowupPhase Phase,
    long GuardObservedAtMilliseconds,
    long ReleasedAtMilliseconds)
{
    public int GameplayKeyToken { get; init; }
    public long ExpectedProtectionEndAtMilliseconds { get; init; } = -1;

    public static MiracleGuardFollowupActorState Waiting(
        MiracleGuardFollowupTargetIdentity target) =>
        new(target, MiracleGuardFollowupPhase.WaitingForGuard, -1, -1)
        {
            ExpectedProtectionEndAtMilliseconds = -1,
        };
}

public readonly record struct MiracleGuardFollowupState(
    ImmutableArray<MiracleGuardFollowupActorState> Actors,
    long LastObservedAtMilliseconds)
{
    public static MiracleGuardFollowupState Initial => new([], -1);
}

public readonly record struct MiracleGuardFollowupObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    bool IsLocalCounterJobValid,
    bool HigherPriorityClaimed,
    IReadOnlyList<MiracleGuardFollowupCandidate>? Candidates,
    long NowMilliseconds,
    bool HardReset = false)
{
    public bool IsWolvesDenTesting { get; init; }

    public bool IsSupportedContext =>
        ReactiveCounterCcProfileRules.IsSupportedContext(
            IsCrystallineConflict,
            IsWolvesDenTesting);
}

public readonly record struct MiracleGuardFollowupIntent(
    MiracleGuardFollowupTargetIdentity Target,
    long ReleasedAtMilliseconds,
    uint CurrentHp,
    uint MaximumHp,
    int TeamTargetCount)
{
    public bool TeamTargetCountKnown { get; init; }
    public bool HasTrustedMp { get; init; }
    public uint CurrentMp { get; init; }
    public uint MaximumMp { get; init; }
    public int GameplayKeyToken { get; init; }
    public long ExpectedProtectionEndAtMilliseconds { get; init; } = -1;

    public bool IsValid =>
        Target.IsValid &&
        ReleasedAtMilliseconds >= 0 &&
        GameplayKeyToken > 0 &&
        CurrentHp > 0 &&
        MaximumHp >= CurrentHp &&
        (!TeamTargetCountKnown || TeamTargetCount >= 0) &&
        (!HasTrustedMp ||
         (MaximumMp == CombatFrameRules.ExpectedMaximumMp && CurrentMp <= MaximumMp));
}

public readonly record struct MiracleGuardFollowupDecision(
    MiracleGuardFollowupState NextState,
    MiracleGuardFollowupDecisionKind Kind,
    MiracleGuardFollowupCancelReason CancelReason,
    MiracleGuardFollowupIntent? PromotionIntent,
    int NewGuardEpisodeCount,
    int NewReleaseOpportunityCount,
    int ExpiredOpportunityCount,
    int RetiredOtherOpportunityCount)
{
    public bool ShouldPromote =>
        Kind == MiracleGuardFollowupDecisionKind.ReadyForPromotion &&
        PromotionIntent is { IsValid: true };
}

/// <summary>
/// Pure one-episode policy for counter-CC after an enemy Guard ends. An exact
/// live Guard row must first be observed on one unchanged canonical S1-S5 actor.
/// The first later framework observation with both exact Guard rows absent opens
/// one 500-ms key-acquisition opportunity. A selected exact actor/key may then
/// wait inside the shared 3-second held lease from that original release edge.
/// Absence alone can never arm an episode. Selection retires every concurrent
/// release opportunity before a priority wait or runtime dispatch, so one input
/// can never rerank into a delayed second action.
/// </summary>
public static class MiracleGuardFollowupRules
{
    public const uint GuardStatusId = 3_054;
    public const uint GuardStatusAlternateId = 3_673;
    public const long ReleaseOpportunityMilliseconds = 500;
    public const long MaximumGuardRemainingMilliseconds = 4_250;

    public static MiracleGuardFollowupDecision Observe(
        MiracleGuardFollowupState previous,
        MiracleGuardFollowupObservation observation)
    {
        previous = Normalize(previous);
        if (observation.HardReset)
        {
            return Cancelled(
                MiracleGuardFollowupState.Initial,
                MiracleGuardFollowupCancelReason.HardReset);
        }

        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(
                MiracleGuardFollowupState.Initial,
                MiracleGuardFollowupCancelReason.ClockMovedBackwards);
        }

        var gateFailure = GateFailure(observation);
        if (gateFailure != MiracleGuardFollowupCancelReason.None)
            return Cancelled(MiracleGuardFollowupState.Initial, gateFailure);

        var candidates = ExactCandidates(observation.Candidates);
        var previousBySlot = previous.Actors
            .Where(static actor => actor.Target.IsValid)
            .GroupBy(static actor => actor.Target.EnemySlot)
            .Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.Single());
        var nextActors = ImmutableArray.CreateBuilder<MiracleGuardFollowupActorState>(
            candidates.Count);
        var newGuardEpisodes = 0;
        var newReleaseOpportunities = 0;
        var expiredOpportunities = 0;

        foreach (var candidate in candidates.Values.OrderBy(static value => value.Target.EnemySlot))
        {
            var actor = previousBySlot.TryGetValue(candidate.Target.EnemySlot, out var prior) &&
                        prior.Target == candidate.Target
                ? prior
                : MiracleGuardFollowupActorState.Waiting(candidate.Target);
            actor = ObserveActor(
                actor,
                candidate,
                observation.NowMilliseconds,
                out var newEpisode,
                out var newRelease,
                out var expired);
            if (newEpisode) newGuardEpisodes++;
            if (newRelease) newReleaseOpportunities++;
            if (expired) expiredOpportunities++;
            nextActors.Add(actor);
        }

        // Unknown/ambiguous telemetry terminally retires an active Guard
        // reservation but preserves a tombstone. It can neither synthesize an
        // absence release nor let a different key resurrect the same episode.
        foreach (var retained in previousBySlot.Values
                     .Where(static actor =>
                         actor.Phase is MiracleGuardFollowupPhase.GuardPresent or
                             MiracleGuardFollowupPhase.RetiredUntilGuardAbsent)
                     .Where(actor => !candidates.ContainsKey(actor.Target.EnemySlot))
                     .Where(actor => ShouldRetainUncertainActor(
                         actor,
                         observation.Candidates))
                     .OrderBy(static actor => actor.Target.EnemySlot))
        {
            nextActors.Add(retained.Phase == MiracleGuardFollowupPhase.GuardPresent
                ? retained with
                {
                    Phase = MiracleGuardFollowupPhase.RetiredUntilGuardAbsent,
                    ReleasedAtMilliseconds = -1,
                    GameplayKeyToken = 0,
                }
                : retained);
        }

        var state = new MiracleGuardFollowupState(
            nextActors
                .ToImmutable()
                .OrderBy(static actor => actor.Target.EnemySlot)
                .ToImmutableArray(),
            observation.NowMilliseconds);
        var releaseReady = state.Actors
            .Where(actor => actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity)
            .Select(actor => (Actor: actor, Candidate: candidates[actor.Target.EnemySlot]))
            .Where(pair => IsInsideReleaseOrHeldWindow(
                pair.Actor,
                observation.NowMilliseconds))
            .ToArray();

        // A previously selected release owns this lease. If its exact actor/key
        // disappears or expires, no concurrent or later release may replace it.
        var previouslyFrozen = previous.Actors
            .Where(static actor =>
                actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity &&
                actor.GameplayKeyToken > 0)
            .OrderBy(static actor => actor.ReleasedAtMilliseconds)
            .ThenBy(static actor => actor.Target.EnemySlot)
            .FirstOrDefault();
        var hadPreviouslyFrozen = previouslyFrozen.Target.IsValid;

        // Select once before honoring dispatcher priority. Unbound releases may
        // acquire a key only inside the original 500-ms edge; after selection,
        // the exact actor/key waits only inside the 3-second held lease measured
        // from that same ReleasedAt timestamp.
        var selected = hadPreviouslyFrozen
            ? releaseReady.FirstOrDefault(pair =>
                pair.Actor.Target == previouslyFrozen.Target &&
                pair.Actor.GameplayKeyToken == previouslyFrozen.GameplayKeyToken &&
                pair.Actor.ReleasedAtMilliseconds == previouslyFrozen.ReleasedAtMilliseconds)
            : releaseReady
                .Where(static pair => pair.Actor.GameplayKeyToken > 0)
                .OrderBy(static pair => pair.Candidate, ProtectionEndRankComparer.Instance)
                .FirstOrDefault();
        if (hadPreviouslyFrozen && !selected.Candidate.IsValid)
        {
            var retiredReleaseActors = state.Actors
                .Select(actor => actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity
                    ? MiracleGuardFollowupActorState.Waiting(actor.Target)
                    : actor)
                .ToImmutableArray();
            return Waiting(
                new MiracleGuardFollowupState(
                    retiredReleaseActors,
                    observation.NowMilliseconds),
                newGuardEpisodes,
                newReleaseOpportunities,
                expiredOpportunities,
                releaseReady.Length);
        }

        if (!selected.Candidate.IsValid)
        {
            return Waiting(
                state,
                newGuardEpisodes,
                newReleaseOpportunities,
                expiredOpportunities);
        }

        var retiredOtherOpportunities = Math.Max(0, releaseReady.Length - 1);
        var frozenActors = state.Actors
            .Select(actor =>
                actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity &&
                actor.Target != selected.Actor.Target
                    ? MiracleGuardFollowupActorState.Waiting(actor.Target)
                    : actor)
            .ToImmutableArray();
        var frozenState = new MiracleGuardFollowupState(
            frozenActors,
            observation.NowMilliseconds);
        if (observation.HigherPriorityClaimed)
        {
            return Waiting(
                frozenState,
                newGuardEpisodes,
                newReleaseOpportunities,
                expiredOpportunities,
                retiredOtherOpportunities);
        }

        var retiredActors = frozenState.Actors
            .Select(actor => actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity
                ? MiracleGuardFollowupActorState.Waiting(actor.Target)
                : actor)
            .ToImmutableArray();
        var intent = new MiracleGuardFollowupIntent(
            selected.Candidate.Target,
            selected.Actor.ReleasedAtMilliseconds,
            selected.Candidate.CurrentHp,
            selected.Candidate.MaximumHp,
            selected.Candidate.TeamTargetCount)
        {
            TeamTargetCountKnown = selected.Candidate.TeamTargetCountKnown,
            HasTrustedMp = selected.Candidate.HasTrustedMp,
            CurrentMp = selected.Candidate.CurrentMp,
            MaximumMp = selected.Candidate.MaximumMp,
            GameplayKeyToken = selected.Actor.GameplayKeyToken,
            ExpectedProtectionEndAtMilliseconds =
                selected.Actor.ExpectedProtectionEndAtMilliseconds,
        };
        return new MiracleGuardFollowupDecision(
            new MiracleGuardFollowupState(retiredActors, observation.NowMilliseconds),
            MiracleGuardFollowupDecisionKind.ReadyForPromotion,
            MiracleGuardFollowupCancelReason.None,
            intent,
            newGuardEpisodes,
            newReleaseOpportunities,
            expiredOpportunities,
            retiredOtherOpportunities);
    }

    public static bool IsExactGuardStatus(uint statusId) =>
        statusId is GuardStatusId or GuardStatusAlternateId;

    public static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static MiracleGuardFollowupActorState ObserveActor(
        MiracleGuardFollowupActorState previous,
        MiracleGuardFollowupCandidate candidate,
        long nowMilliseconds,
        out bool newEpisode,
        out bool newRelease,
        out bool expired)
    {
        newEpisode = false;
        newRelease = false;
        expired = false;
        var guardPresent = candidate.ActiveGuardStatusCount == 1;
        if (previous.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity &&
            previous.GameplayKeyToken > 0 &&
            !candidate.ReservedGameplayKeyPhysicallyDown)
        {
            return MiracleGuardFollowupActorState.Waiting(candidate.Target);
        }

        if (previous.Phase == MiracleGuardFollowupPhase.RetiredUntilGuardAbsent)
        {
            return guardPresent
                ? previous
                : MiracleGuardFollowupActorState.Waiting(candidate.Target);
        }

        if (guardPresent)
        {
            var firstPresence = previous.Phase != MiracleGuardFollowupPhase.GuardPresent;
            if (firstPresence) newEpisode = true;
            return new MiracleGuardFollowupActorState(
                candidate.Target,
                MiracleGuardFollowupPhase.GuardPresent,
                firstPresence ? nowMilliseconds : previous.GuardObservedAtMilliseconds,
                -1)
            {
                // The enemy episode is remembered now; held consent is sampled
                // only after authoritative Guard absence.
                GameplayKeyToken = 0,
                ExpectedProtectionEndAtMilliseconds = UpdateExpectedProtectionEnd(
                    firstPresence ? -1 : previous.ExpectedProtectionEndAtMilliseconds,
                    candidate.GuardRemainingMilliseconds,
                    nowMilliseconds),
            };
        }

        if (previous.Phase == MiracleGuardFollowupPhase.GuardPresent)
        {
            newRelease = true;
            return previous with
            {
                Phase = MiracleGuardFollowupPhase.ReleaseOpportunity,
                ReleasedAtMilliseconds = nowMilliseconds,
                GameplayKeyToken = candidate.ReservationGameplayKeyToken > 0 &&
                                   candidate.ReservedGameplayKeyPhysicallyDown
                    ? candidate.ReservationGameplayKeyToken
                    : 0,
            };
        }

        if (previous.Phase != MiracleGuardFollowupPhase.ReleaseOpportunity)
            return MiracleGuardFollowupActorState.Waiting(candidate.Target);

        if (IsInsideReleaseOrHeldWindow(previous, nowMilliseconds))
        {
            return previous.GameplayKeyToken == 0 &&
                   candidate.ReservationGameplayKeyToken > 0 &&
                   candidate.ReservedGameplayKeyPhysicallyDown
                ? previous with
                {
                    GameplayKeyToken = candidate.ReservationGameplayKeyToken,
                }
                : previous;
        }
        expired = true;
        return MiracleGuardFollowupActorState.Waiting(candidate.Target);
    }

    private static bool IsInsideReleaseWindow(
        MiracleGuardFollowupActorState actor,
        long nowMilliseconds) =>
        actor.ReleasedAtMilliseconds >= 0 &&
        nowMilliseconds >= actor.ReleasedAtMilliseconds &&
        nowMilliseconds - actor.ReleasedAtMilliseconds < ReleaseOpportunityMilliseconds;

    private static bool IsInsideReleaseOrHeldWindow(
        MiracleGuardFollowupActorState actor,
        long nowMilliseconds) =>
        actor.GameplayKeyToken > 0
            ? MiracleProtectionEndRules.IsInsideHeldLease(
                actor.ReleasedAtMilliseconds,
                nowMilliseconds)
            : IsInsideReleaseWindow(actor, nowMilliseconds);

    private static long UpdateExpectedProtectionEnd(
        long currentExpectedEndMilliseconds,
        long remainingMilliseconds,
        long nowMilliseconds)
    {
        if (remainingMilliseconds <= 0 ||
            remainingMilliseconds > MaximumGuardRemainingMilliseconds ||
            nowMilliseconds < 0)
        {
            return currentExpectedEndMilliseconds;
        }

        var observedEnd = nowMilliseconds > long.MaxValue - remainingMilliseconds
            ? long.MaxValue
            : nowMilliseconds + remainingMilliseconds;
        return currentExpectedEndMilliseconds > 0
            ? Math.Min(currentExpectedEndMilliseconds, observedEnd)
            : observedEnd;
    }

    private static Dictionary<int, MiracleGuardFollowupCandidate> ExactCandidates(
        IReadOnlyList<MiracleGuardFollowupCandidate>? candidates)
    {
        if (candidates is null || candidates.Count == 0) return [];
        var valid = candidates.Where(static candidate => candidate.IsValid).ToArray();
        var ambiguousSlots = valid
            .GroupBy(static candidate => candidate.Target.EnemySlot)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var ambiguousGameObjectIds = valid
            .GroupBy(static candidate => candidate.Target.GameObjectId)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet();
        var ambiguousEntityIds = valid
            .GroupBy(static candidate => candidate.Target.EntityId)
            .Where(static group => group.Count() != 1)
            .Select(static group => group.Key)
            .ToHashSet();

        return valid
            .Where(candidate =>
                !ambiguousSlots.Contains(candidate.Target.EnemySlot) &&
                !ambiguousGameObjectIds.Contains(candidate.Target.GameObjectId) &&
                !ambiguousEntityIds.Contains(candidate.Target.EntityId))
            .ToDictionary(static candidate => candidate.Target.EnemySlot);
    }

    private static bool ShouldRetainUncertainActor(
        MiracleGuardFollowupActorState actor,
        IReadOnlyList<MiracleGuardFollowupCandidate>? observedCandidates)
    {
        if (observedCandidates is null || observedCandidates.Count == 0)
            return true;

        var sameSlot = observedCandidates
            .Where(candidate => candidate.Target.EnemySlot == actor.Target.EnemySlot)
            .ToArray();
        if (sameSlot.Length == 0) return true;

        var sameIdentity = sameSlot
            .Where(candidate => candidate.Target == actor.Target)
            .ToArray();
        if (sameIdentity.Length == 0) return false;

        // A non-canonical row is ambiguity, not proof that the actor vanished.
        // Duplicate rows and malformed HP/status telemetry are likewise
        // unknown. Only one exact same-identity row with proven life loss may
        // remove the tombstone; every other uncertain frame retains it.
        if (sameIdentity.Length != 1 ||
            !sameIdentity[0].IsExactCanonicalEnemy)
        {
            return true;
        }

        return sameIdentity[0].IsAliveAndTargetable &&
               sameIdentity[0].CurrentHp > 0;
    }

    private static MiracleGuardFollowupCancelReason GateFailure(
        MiracleGuardFollowupObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return MiracleGuardFollowupCancelReason.ConfigurationDisabled;
        if (!observation.IsSupportedContext)
            return MiracleGuardFollowupCancelReason.OutsideCrystallineConflict;
        if (!observation.IsLocalCounterJobValid)
            return MiracleGuardFollowupCancelReason.LocalCounterJobInvalid;
        return MiracleGuardFollowupCancelReason.None;
    }

    private static MiracleGuardFollowupState Normalize(MiracleGuardFollowupState state) =>
        state.Actors.IsDefault
            ? state with { Actors = [] }
            : state;

    private static MiracleGuardFollowupDecision Waiting(
        MiracleGuardFollowupState state,
        int newGuardEpisodes,
        int newReleaseOpportunities,
        int expiredOpportunities,
        int retiredOtherOpportunityCount = 0) =>
        new(
            state,
            state.Actors.Any(static actor =>
                actor.Phase != MiracleGuardFollowupPhase.WaitingForGuard)
                ? MiracleGuardFollowupDecisionKind.Waiting
                : MiracleGuardFollowupDecisionKind.None,
            MiracleGuardFollowupCancelReason.None,
            null,
            newGuardEpisodes,
            newReleaseOpportunities,
            expiredOpportunities,
            retiredOtherOpportunityCount);

    private static MiracleGuardFollowupDecision Cancelled(
        MiracleGuardFollowupState state,
        MiracleGuardFollowupCancelReason reason) =>
        new(
            state,
            MiracleGuardFollowupDecisionKind.Cancelled,
            reason,
            null,
            0,
            0,
            0,
            0);

    private sealed class ProtectionEndRankComparer : IComparer<MiracleGuardFollowupCandidate>
    {
        internal static ProtectionEndRankComparer Instance { get; } = new();

        public int Compare(
            MiracleGuardFollowupCandidate left,
            MiracleGuardFollowupCandidate right) =>
            MiracleProtectionEndRules.Compare(left.RankCandidate, right.RankCandidate);
    }
}
