namespace SeitonSense.Core;

/// <summary>
/// Narrow consent retained only for verified post-Purify and post-Guard
/// protection-end episodes. The token is supplied by the shared emergency
/// input coordinator; this policy never discovers keys from raw key levels.
/// </summary>
public readonly record struct MiracleProtectionEndHeldConsentState(int GameplayKeyToken)
{
    public static MiracleProtectionEndHeldConsentState Initial => new(0);

    public bool IsLatched => GameplayKeyToken > 0;
}

public readonly record struct MiracleProtectionEndHeldConsentObservation(
    bool Enabled,
    bool IsTextInputActive,
    int UnconsumedEligibleGameplayKeyToken,
    bool LatchedKeyPhysicallyDown,
    bool HardReset = false);

public enum MiracleProtectionEndAttemptOutcome
{
    None = 0,
    RetryScheduled = 1,
    AcceptedTerminal = 2,
    RejectedTerminal = 3,
    AmbiguousTerminal = 4,
    ExpiredTerminal = 5,
    CancelledTerminal = 6,
    SoftWait = 7,
}

public readonly record struct MiracleProtectionEndAttemptDecision(
    HeldActionRetryState NextState,
    MiracleProtectionEndAttemptOutcome Outcome)
{
    public bool IsTerminal => Outcome is
        MiracleProtectionEndAttemptOutcome.AcceptedTerminal or
        MiracleProtectionEndAttemptOutcome.RejectedTerminal or
        MiracleProtectionEndAttemptOutcome.AmbiguousTerminal or
        MiracleProtectionEndAttemptOutcome.ExpiredTerminal or
        MiracleProtectionEndAttemptOutcome.CancelledTerminal;
}

/// <summary>
/// Immutable comparison values captured from one exact, currently
/// release-ready protection-end actor. Positive fresh team pressure is an
/// optional ranking bonus. Known zero and unavailable/stale pressure are
/// equally neutral and always remain eligible for the HP/MP/identity fallback.
/// </summary>
public readonly record struct MiracleProtectionEndRankCandidate(
    MiracleInterceptThreatKind Threat,
    int EnemySlot,
    ulong GameObjectId,
    uint EntityId,
    uint JobId,
    bool TeamTargetCountKnown,
    int TeamTargetCount,
    uint CurrentHp,
    uint MaximumHp,
    bool HasTrustedMp,
    uint CurrentMp,
    uint MaximumMp)
{
    public bool IsValid =>
        Threat is MiracleInterceptThreatKind.PostPurifyCrowdControl or
            MiracleInterceptThreatKind.PostGuardCrowdControl &&
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        TargetHighlightRules.IsValidGameObjectId(GameObjectId) &&
        MiracleGuardFollowupRules.IsValidEntityId(EntityId) &&
        JobId != 0 &&
        (!TeamTargetCountKnown || TeamTargetCount >= 0) &&
        CurrentHp > 0 &&
        MaximumHp >= CurrentHp &&
        (!HasTrustedMp ||
         (MaximumMp == CombatFrameRules.ExpectedMaximumMp && CurrentMp <= MaximumMp));
}

public static class MiracleProtectionEndRules
{
    // Protection-end consent must survive one ordinary 2.5-second GCD plus the
    // release edge. This matters most for casting BRD/WHM gameplay: the exact
    // actor/key can now be frozen immediately when protection ends and wait for
    // the first clear native queue frame instead of being lost to a cast that
    // happened to overlap the old 1.5-second lease.
    public const long HeldLeaseMilliseconds = 3_000;
    // Keep the named NIN value for call-site clarity. Raiju shares the same
    // one-GCD protection-end lease as every other counter-CC action.
    public const long NinjaWeaponskillHeldLeaseMilliseconds = 3_000;
    public const long NativeRetryThrottleMilliseconds =
        HeldActionRetryRules.NativeRetryThrottleMilliseconds;
    public const int MaximumNativeAttempts = HeldActionRetryRules.MaximumNativeAttempts;

    public static bool DispatchConsumesHeldConsent(MiracleInterceptThreatKind threat) => false;

    public static MiracleProtectionEndHeldConsentState ObserveHeldConsent(
        MiracleProtectionEndHeldConsentState previous,
        MiracleProtectionEndHeldConsentObservation observation)
    {
        if (observation.HardReset ||
            !observation.Enabled ||
            observation.IsTextInputActive)
        {
            return MiracleProtectionEndHeldConsentState.Initial;
        }

        if (previous.IsLatched && observation.LatchedKeyPhysicallyDown)
            return previous;

        return observation.UnconsumedEligibleGameplayKeyToken > 0
            ? new MiracleProtectionEndHeldConsentState(
                observation.UnconsumedEligibleGameplayKeyToken)
            : MiracleProtectionEndHeldConsentState.Initial;
    }

    public static bool IsInsideHeldLease(
        long observedAtMilliseconds,
        long nowMilliseconds,
        long leaseMilliseconds = HeldLeaseMilliseconds) =>
        observedAtMilliseconds >= 0 &&
        leaseMilliseconds > 0 &&
        nowMilliseconds >= observedAtMilliseconds &&
        nowMilliseconds - observedAtMilliseconds < leaseMilliseconds;

    public static bool CanAttempt(
        HeldActionRetryState state,
        long observedAtMilliseconds,
        long nowMilliseconds,
        long leaseMilliseconds = HeldLeaseMilliseconds) =>
        IsInsideHeldLease(observedAtMilliseconds, nowMilliseconds, leaseMilliseconds) &&
        (state == HeldActionRetryState.Initial ||
         HeldActionRetryRules.CanAttempt(state, nowMilliseconds));

    /// <summary>
    /// An exact hostile startup packet may replace any unattempted lower-priority
    /// reactive lease only when its dispatcher priority is strictly higher. A
    /// proven native false or equal/lower priority remains frozen and cannot be
    /// retargeted.
    /// </summary>
    public static bool CanPreemptUnattemptedLowerPriorityThreat(
        MiracleInterceptThreatKind activeThreat,
        HeldActionRetryState activeRetryState,
        MiracleInterceptThreatKind incomingThreat)
    {
        var activePriority = MiracleInterceptRules.GetDispatchPriority(activeThreat);
        var incomingPriority = MiracleInterceptRules.GetDispatchPriority(incomingThreat);
        return activeRetryState == HeldActionRetryState.Initial &&
               activePriority > 0 &&
               incomingPriority > activePriority;
    }

    public static MiracleProtectionEndAttemptDecision CompleteNativeAttempt(
        HeldActionRetryState previous,
        long observedAtMilliseconds,
        long nowMilliseconds,
        ClientActionAttemptOutcome outcome,
        long leaseMilliseconds = HeldLeaseMilliseconds)
    {
        if (observedAtMilliseconds < 0 ||
            nowMilliseconds < observedAtMilliseconds ||
            !HeldActionRetryRules.IsValidState(previous))
        {
            return new MiracleProtectionEndAttemptDecision(
                HeldActionRetryState.Initial,
                MiracleProtectionEndAttemptOutcome.ExpiredTerminal);
        }

        if (outcome == ClientActionAttemptOutcome.AcceptanceUnknown)
        {
            return new MiracleProtectionEndAttemptDecision(
                HeldActionRetryState.Initial,
                MiracleProtectionEndAttemptOutcome.AmbiguousTerminal);
        }

        if (outcome == ClientActionAttemptOutcome.ClientAccepted)
        {
            return new MiracleProtectionEndAttemptDecision(
                HeldActionRetryState.Initial,
                MiracleProtectionEndAttemptOutcome.AcceptedTerminal);
        }

        if (!IsInsideHeldLease(observedAtMilliseconds, nowMilliseconds, leaseMilliseconds))
        {
            return new MiracleProtectionEndAttemptDecision(
                HeldActionRetryState.Initial,
                MiracleProtectionEndAttemptOutcome.ExpiredTerminal);
        }

        var shared = HeldActionRetryRules.Complete(
            previous,
            nowMilliseconds,
            outcome);
        var deadline = SaturatingAdd(observedAtMilliseconds, leaseMilliseconds);
        if (shared.RetryScheduled &&
            shared.NextState.NextNativeAttemptAtMilliseconds >= deadline)
        {
            return new MiracleProtectionEndAttemptDecision(
                HeldActionRetryState.Initial,
                MiracleProtectionEndAttemptOutcome.RejectedTerminal);
        }

        return new MiracleProtectionEndAttemptDecision(
            shared.NextState,
            shared.Disposition switch
            {
                HeldActionRetryDisposition.RetryScheduled =>
                    MiracleProtectionEndAttemptOutcome.RetryScheduled,
                HeldActionRetryDisposition.AcceptedTerminal =>
                    MiracleProtectionEndAttemptOutcome.AcceptedTerminal,
                HeldActionRetryDisposition.RejectedTerminal =>
                    MiracleProtectionEndAttemptOutcome.RejectedTerminal,
                HeldActionRetryDisposition.AmbiguousTerminal =>
                    MiracleProtectionEndAttemptOutcome.AmbiguousTerminal,
                HeldActionRetryDisposition.CancelledTerminal =>
                    MiracleProtectionEndAttemptOutcome.CancelledTerminal,
                HeldActionRetryDisposition.SoftWait =>
                    MiracleProtectionEndAttemptOutcome.SoftWait,
                _ => MiracleProtectionEndAttemptOutcome.AmbiguousTerminal,
            });
    }

    /// <summary>
    /// Returns a negative value when <paramref name="left"/> ranks first.
    /// Positive pressure is descending. Known zero and unknown/stale pressure
    /// are neutral peers. HP and trusted MP ratios are ascending; a known MP
    /// sample ranks before an unknown one. Exact slot/IDs close every tie.
    /// </summary>
    public static int Compare(
        MiracleProtectionEndRankCandidate left,
        MiracleProtectionEndRankCandidate right)
    {
        if (!left.IsValid) return right.IsValid ? 1 : 0;
        if (!right.IsValid) return -1;

        var leftHasPositivePressure = left.TeamTargetCountKnown &&
                                      left.TeamTargetCount > 0;
        var rightHasPositivePressure = right.TeamTargetCountKnown &&
                                       right.TeamTargetCount > 0;
        var positivePressure = rightHasPositivePressure.CompareTo(leftHasPositivePressure);
        if (positivePressure != 0) return positivePressure;
        if (leftHasPositivePressure)
        {
            var pressure = right.TeamTargetCount.CompareTo(left.TeamTargetCount);
            if (pressure != 0) return pressure;
        }

        var hpRatio = CompareRatio(
            left.CurrentHp,
            left.MaximumHp,
            right.CurrentHp,
            right.MaximumHp);
        if (hpRatio != 0) return hpRatio;

        var mpTrust = right.HasTrustedMp.CompareTo(left.HasTrustedMp);
        if (mpTrust != 0) return mpTrust;
        if (left.HasTrustedMp)
        {
            var mpRatio = CompareRatio(
                left.CurrentMp,
                left.MaximumMp,
                right.CurrentMp,
                right.MaximumMp);
            if (mpRatio != 0) return mpRatio;
        }

        var slot = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slot != 0) return slot;
        var entity = left.EntityId.CompareTo(right.EntityId);
        if (entity != 0) return entity;
        var gameObject = left.GameObjectId.CompareTo(right.GameObjectId);
        if (gameObject != 0) return gameObject;
        var job = left.JobId.CompareTo(right.JobId);
        if (job != 0) return job;
        return left.Threat.CompareTo(right.Threat);
    }

    public static int SelectBestIndex(
        IReadOnlyList<MiracleProtectionEndRankCandidate>? candidates)
    {
        if (candidates is null || candidates.Count == 0) return -1;
        var selected = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            if (!candidates[index].IsValid) continue;
            if (selected < 0 || Compare(candidates[index], candidates[selected]) < 0)
                selected = index;
        }

        return selected;
    }

    private static int CompareRatio(
        uint leftCurrent,
        uint leftMaximum,
        uint rightCurrent,
        uint rightMaximum) =>
        ((UInt128)leftCurrent * rightMaximum).CompareTo(
            (UInt128)rightCurrent * leftMaximum);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
