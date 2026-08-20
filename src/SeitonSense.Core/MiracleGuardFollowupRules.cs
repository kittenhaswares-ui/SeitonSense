using System.Collections.Immutable;

namespace SeitonSense.Core;

public enum MiracleGuardFollowupPhase
{
    WaitingForGuard = 0,
    GuardPresent = 1,
    ReleaseOpportunity = 2,
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
    public bool IsValid =>
        Target.IsValid &&
        IsExactCanonicalEnemy &&
        IsAliveAndTargetable &&
        ActiveGuardStatusCount is 0 or 1 &&
        CurrentHp > 0 &&
        MaximumHp >= CurrentHp &&
        (!TeamTargetCountKnown || TeamTargetCount >= 0);

    public bool HasExactTeamFocus =>
        TeamTargetCountKnown &&
        TeamTargetCount >= MiracleGuardFollowupRules.RequiredTeamTargetCount;
}

public readonly record struct MiracleGuardFollowupActorState(
    MiracleGuardFollowupTargetIdentity Target,
    MiracleGuardFollowupPhase Phase,
    long GuardObservedAtMilliseconds,
    long ReleasedAtMilliseconds)
{
    public static MiracleGuardFollowupActorState Waiting(
        MiracleGuardFollowupTargetIdentity target) =>
        new(target, MiracleGuardFollowupPhase.WaitingForGuard, -1, -1);
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
    bool HardReset = false);

public readonly record struct MiracleGuardFollowupIntent(
    MiracleGuardFollowupTargetIdentity Target,
    long ReleasedAtMilliseconds,
    uint CurrentHp,
    uint MaximumHp,
    int TeamTargetCount)
{
    public bool IsValid =>
        Target.IsValid &&
        ReleasedAtMilliseconds >= 0 &&
        CurrentHp > 0 &&
        MaximumHp >= CurrentHp &&
        TeamTargetCount >= MiracleGuardFollowupRules.RequiredTeamTargetCount;
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
/// one 500-ms opportunity. Absence alone can never arm an episode. A promotion
/// retires every concurrently release-ready Guard opportunity before runtime
/// dispatch, so one input can never produce a delayed second action.
/// </summary>
public static class MiracleGuardFollowupRules
{
    public const uint GuardStatusId = 3_054;
    public const uint GuardStatusAlternateId = 3_673;
    public const long ReleaseOpportunityMilliseconds = 500;
    public const int RequiredTeamTargetCount = 2;

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

        var state = new MiracleGuardFollowupState(
            nextActors.ToImmutable(),
            observation.NowMilliseconds);
        var releaseReady = state.Actors
            .Where(actor => actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity)
            .Select(actor => (Actor: actor, Candidate: candidates[actor.Target.EnemySlot]))
            .Where(pair => IsInsideReleaseWindow(pair.Actor, observation.NowMilliseconds))
            .ToArray();
        if (observation.HigherPriorityClaimed)
        {
            return Waiting(
                state,
                newGuardEpisodes,
                newReleaseOpportunities,
                expiredOpportunities);
        }

        var selected = releaseReady
            .Where(static pair => pair.Candidate.HasExactTeamFocus)
            .OrderBy(static pair => pair.Candidate, HealthRatioComparer.Instance)
            .ThenBy(static pair => pair.Candidate.Target.EnemySlot)
            .ThenBy(static pair => pair.Candidate.Target.EntityId)
            .ThenBy(static pair => pair.Candidate.Target.GameObjectId)
            .FirstOrDefault();
        if (!selected.Candidate.IsValid || !selected.Candidate.HasExactTeamFocus)
        {
            return Waiting(
                state,
                newGuardEpisodes,
                newReleaseOpportunities,
                expiredOpportunities);
        }

        var retiredActors = state.Actors
            .Select(actor => actor.Phase == MiracleGuardFollowupPhase.ReleaseOpportunity
                ? MiracleGuardFollowupActorState.Waiting(actor.Target)
                : actor)
            .ToImmutableArray();
        var intent = new MiracleGuardFollowupIntent(
            selected.Candidate.Target,
            selected.Actor.ReleasedAtMilliseconds,
            selected.Candidate.CurrentHp,
            selected.Candidate.MaximumHp,
            selected.Candidate.TeamTargetCount);
        return new MiracleGuardFollowupDecision(
            new MiracleGuardFollowupState(retiredActors, observation.NowMilliseconds),
            MiracleGuardFollowupDecisionKind.ReadyForPromotion,
            MiracleGuardFollowupCancelReason.None,
            intent,
            newGuardEpisodes,
            newReleaseOpportunities,
            expiredOpportunities,
            Math.Max(0, releaseReady.Length - 1));
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
        if (guardPresent)
        {
            if (previous.Phase != MiracleGuardFollowupPhase.GuardPresent)
                newEpisode = true;
            return new MiracleGuardFollowupActorState(
                candidate.Target,
                MiracleGuardFollowupPhase.GuardPresent,
                nowMilliseconds,
                -1);
        }

        if (previous.Phase == MiracleGuardFollowupPhase.GuardPresent)
        {
            newRelease = true;
            return previous with
            {
                Phase = MiracleGuardFollowupPhase.ReleaseOpportunity,
                ReleasedAtMilliseconds = nowMilliseconds,
            };
        }

        if (previous.Phase != MiracleGuardFollowupPhase.ReleaseOpportunity)
            return MiracleGuardFollowupActorState.Waiting(candidate.Target);

        if (IsInsideReleaseWindow(previous, nowMilliseconds)) return previous;
        expired = true;
        return MiracleGuardFollowupActorState.Waiting(candidate.Target);
    }

    private static bool IsInsideReleaseWindow(
        MiracleGuardFollowupActorState actor,
        long nowMilliseconds) =>
        actor.ReleasedAtMilliseconds >= 0 &&
        nowMilliseconds >= actor.ReleasedAtMilliseconds &&
        nowMilliseconds - actor.ReleasedAtMilliseconds < ReleaseOpportunityMilliseconds;

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

    private static MiracleGuardFollowupCancelReason GateFailure(
        MiracleGuardFollowupObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return MiracleGuardFollowupCancelReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
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
        int expiredOpportunities) =>
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
            0);

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

    private sealed class HealthRatioComparer : IComparer<MiracleGuardFollowupCandidate>
    {
        internal static HealthRatioComparer Instance { get; } = new();

        public int Compare(
            MiracleGuardFollowupCandidate left,
            MiracleGuardFollowupCandidate right) =>
            ((UInt128)left.CurrentHp * right.MaximumHp).CompareTo(
                (UInt128)right.CurrentHp * left.MaximumHp);
    }
}
