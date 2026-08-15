namespace SeitonSense.Core;

public enum DefensiveUtilityActionKind
{
    None = 0,
    Guard = 1,
    Guardian = 2,
}

public enum DefensiveUtilityTrigger
{
    None = 0,
    PostPurifyHighPressureStun = 1,
    PreGuardLowHpPressure = 2,
    PaladinGuardianLowAlly = 3,
}

public readonly record struct PaladinGuardianCandidate(
    ulong GameObjectId,
    uint EntityId,
    int PartySlot,
    uint CurrentHp,
    uint MaximumHp,
    int? IncomingEnemyCount,
    float DistanceSquared,
    bool IsExactPartyMember,
    bool IsSelf,
    bool IsAlive,
    bool IsTargetable,
    bool HasValidNativeTarget,
    bool HasNativeRangeAndLineOfSight)
{
    public TargetPressureActorIdentity Actor => new(GameObjectId, EntityId);
}

public readonly record struct GuardPropagationState(
    long LastObservedAttemptAtMilliseconds,
    long ExpiresAtMilliseconds)
{
    public static GuardPropagationState Initial => new(-1, -1);
}

public readonly record struct GuardPropagationDecision(
    GuardPropagationState NextState,
    bool ExactGuardActive,
    bool PropagationLatchActive,
    long RemainingMilliseconds)
{
    public bool SuppressDirectActionHelpers =>
        ExactGuardActive || PropagationLatchActive;
}

/// <summary>
/// Pure, deterministic gates for the optional defensive held-key helpers.
/// Runtime code still revalidates actor identity, action metadata, cooldown,
/// native range, and line of sight immediately before its one action request.
/// </summary>
public static class DefensiveUtilityRules
{
    public const int RequiredIncomingEnemyCount = 3;
    public const int PreGuardHpPercent = 50;
    public const int GuardianAllyHpPercent = 20;
    public const float GuardianStrictMaximumDistance = 10f;
    public const long PostPurifyGuardWindowMilliseconds = 2_000;
    // Covers the normal client/server status-propagation and action-queue window
    // without turning one Guard request into an unbounded helper lockout.
    public const long GuardPropagationLatchMilliseconds = 1_500;

    public static GuardPropagationDecision ObserveGuardPropagation(
        GuardPropagationState previous,
        bool exactGuardActive,
        long observedGuardAttemptAtMilliseconds,
        long nowMilliseconds,
        bool hardReset = false)
    {
        if (nowMilliseconds < 0)
            return new(GuardPropagationState.Initial, exactGuardActive, false, 0);

        if (hardReset ||
            previous.LastObservedAttemptAtMilliseconds > nowMilliseconds ||
            previous.ExpiresAtMilliseconds < -1)
        {
            previous = GuardPropagationState.Initial;
        }

        var lastObservedAttempt = previous.LastObservedAttemptAtMilliseconds;
        var expiresAt = previous.ExpiresAtMilliseconds > nowMilliseconds
            ? previous.ExpiresAtMilliseconds
            : -1;

        if (observedGuardAttemptAtMilliseconds >= 0 &&
            observedGuardAttemptAtMilliseconds <= nowMilliseconds &&
            observedGuardAttemptAtMilliseconds > lastObservedAttempt)
        {
            lastObservedAttempt = observedGuardAttemptAtMilliseconds;
            expiresAt = SaturatingAdd(
                observedGuardAttemptAtMilliseconds,
                GuardPropagationLatchMilliseconds);
        }

        // Once exact Guard membership is visible, the live status owns the gate.
        // Retain only the last seen timestamp so the same observation cannot rearm
        // after Guard ends or after the bounded propagation window expires.
        if (exactGuardActive) expiresAt = -1;

        var latchActive = !exactGuardActive && expiresAt > nowMilliseconds;
        var next = new GuardPropagationState(
            lastObservedAttempt,
            latchActive ? expiresAt : -1);
        return new GuardPropagationDecision(
            next,
            exactGuardActive,
            latchActive,
            latchActive ? expiresAt - nowMilliseconds : 0);
    }

    public static bool IsHighPressure(bool pressureKnown, int incomingEnemyCount) =>
        pressureKnown && incomingEnemyCount >= RequiredIncomingEnemyCount;

    public static bool IsAtOrBelowHpPercent(
        uint currentHp,
        uint maximumHp,
        int thresholdPercent)
    {
        if (maximumHp == 0 || currentHp == 0 || currentHp > maximumHp) return false;
        var threshold = Math.Clamp(thresholdPercent, 1, 100);
        return (ulong)currentHp * 100UL <= (ulong)maximumHp * (ulong)threshold;
    }

    public static bool IsPreGuardRisk(
        bool pressureKnown,
        int incomingEnemyCount,
        uint currentHp,
        uint maximumHp,
        bool hasPurifyRemovableCrowdControl,
        bool guardActive) =>
        !guardActive &&
        !hasPurifyRemovableCrowdControl &&
        IsHighPressure(pressureKnown, incomingEnemyCount) &&
        IsAtOrBelowHpPercent(currentHp, maximumHp, PreGuardHpPercent);

    public static bool CanDispatchPostPurifyGuard(
        bool awaitingPurifyConfirmation,
        bool resilienceObserved,
        bool hasPurifyRemovableCrowdControl,
        long expiresAtMilliseconds,
        long nowMilliseconds) =>
        !awaitingPurifyConfirmation &&
        resilienceObserved &&
        !hasPurifyRemovableCrowdControl &&
        nowMilliseconds >= 0 &&
        expiresAtMilliseconds > nowMilliseconds;

    public static bool IsGuardianCandidate(PaladinGuardianCandidate candidate)
    {
        var strictMaximumDistanceSquared =
            GuardianStrictMaximumDistance * GuardianStrictMaximumDistance;
        return candidate.Actor.IsValid &&
               candidate.PartySlot is >= 1 and <= 8 &&
               candidate.IsExactPartyMember &&
               !candidate.IsSelf &&
               candidate.IsAlive &&
               candidate.IsTargetable &&
               candidate.HasValidNativeTarget &&
               candidate.HasNativeRangeAndLineOfSight &&
               float.IsFinite(candidate.DistanceSquared) &&
               candidate.DistanceSquared < strictMaximumDistanceSquared &&
               IsAtOrBelowHpPercent(
                   candidate.CurrentHp,
                   candidate.MaximumHp,
                   GuardianAllyHpPercent);
    }

    public static int SelectGuardianCandidateIndex(
        IReadOnlyList<PaladinGuardianCandidate>? candidates,
        IReadOnlySet<TargetPressureActorIdentity>? spentActors = null)
    {
        if (candidates is null || candidates.Count == 0) return -1;

        var selected = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsGuardianCandidate(candidate) ||
                spentActors?.Contains(candidate.Actor) == true)
            {
                continue;
            }

            if (selected < 0 || CompareGuardianCandidates(candidate, candidates[selected]) < 0)
                selected = index;
        }

        return selected;
    }

    private static int CompareGuardianCandidates(
        PaladinGuardianCandidate left,
        PaladinGuardianCandidate right)
    {
        var leftScaledHp = (ulong)left.CurrentHp * right.MaximumHp;
        var rightScaledHp = (ulong)right.CurrentHp * left.MaximumHp;
        var hp = leftScaledHp.CompareTo(rightScaledHp);
        if (hp != 0) return hp;

        var leftPressureKnown = left.IncomingEnemyCount.HasValue;
        var rightPressureKnown = right.IncomingEnemyCount.HasValue;
        if (leftPressureKnown != rightPressureKnown) return leftPressureKnown ? -1 : 1;
        if (leftPressureKnown)
        {
            var pressure = right.IncomingEnemyCount!.Value.CompareTo(
                left.IncomingEnemyCount!.Value);
            if (pressure != 0) return pressure;
        }

        var distance = left.DistanceSquared.CompareTo(right.DistanceSquared);
        if (distance != 0) return distance;

        var slot = left.PartySlot.CompareTo(right.PartySlot);
        if (slot != 0) return slot;

        var entity = left.EntityId.CompareTo(right.EntityId);
        return entity != 0 ? entity : left.GameObjectId.CompareTo(right.GameObjectId);
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
