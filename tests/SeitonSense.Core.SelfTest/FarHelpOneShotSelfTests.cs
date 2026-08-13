using SeitonSense.Core;

internal static class FarHelpOneShotSelfTests
{
    private const ulong OwnTarget = 0x100;
    private const ulong FriendlyA = 0x200;
    private const ulong FriendlyB = 0x201;

    public static void ValidAttemptSelectsAtActionTimeAndRewritesOnce()
    {
        var decision = FarHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt(),
            [
                Candidate(FriendlyA, distance: 20f, jobId: 20, partySlot: 2),
                Candidate(FriendlyB, distance: 10f, jobId: 24, partySlot: 3),
            ]);

        True(decision.ShouldRewrite, "eligible friendly movement action rewrites");
        True(decision.ConsumedActionIntent, "one action owns the token");
        Equal(FriendlyB, decision.ForwardTargetId, "action-time preferred candidate");
        Equal(1, decision.SelectedCandidateIndex, "selected observation index");
        False(decision.NextState.IsArmed, "token is consumed before dispatch");

        var following = FarHelpOneShotRules.Observe(
            decision.NextState,
            ValidAttempt(now: 1_002),
            [Candidate(FriendlyA, 1f, 24, 2)]);
        False(following.ShouldRewrite, "following action cannot reuse token");
        Equal(OwnTarget, following.ForwardTargetId, "following action stays unchanged");
    }

    public static void MissingCandidateUsesExactCarrierFallbackPolicy()
    {
        var compact = FarHelpOneShotRules.Observe(Arm(), ValidAttempt(), []);
        ConsumedFallback(
            compact,
            FarHelpOneShotReason.NoEligibleFriendlyCandidate,
            OwnTarget,
            "compact authored target stays exact");

        var carrier = FarHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { IsFallbackCarrier = true },
            []);
        ConsumedFallback(
            carrier,
            FarHelpOneShotReason.NoEligibleFriendlyCandidate,
            FarHelpOneShotRules.InvalidFallbackCarrierTargetId,
            "exact carrier is invalidated for authored target fallback");
    }

    public static void CarrierIdentityDistinguishesAuthoredSlotFromOwnTarget()
    {
        const ulong carrierGameObjectId = 0x1234_5678_0000_0200UL;
        const uint carrierEntityId = 0x1000_0200;

        True(
            FarHelpCarrierRules.IsFallbackCarrier(
                OwnTarget,
                carrierEntityId,
                carrierGameObjectId,
                carrierEntityId),
            "authored carrier EntityId is recognized");
        True(
            FarHelpCarrierRules.IsFallbackCarrier(
                OwnTarget,
                carrierGameObjectId,
                carrierGameObjectId,
                carrierEntityId),
            "authored carrier GameObjectId is recognized");
        False(
            FarHelpCarrierRules.IsFallbackCarrier(
                carrierGameObjectId,
                carrierEntityId,
                carrierGameObjectId,
                carrierEntityId),
            "compact target on the same actor is not a carrier");
        False(
            FarHelpCarrierRules.IsFallbackCarrier(
                carrierEntityId,
                carrierGameObjectId,
                carrierGameObjectId,
                carrierEntityId),
            "mixed identity preserves a compact target");
        False(
            FarHelpCarrierRules.IsFallbackCarrier(
                OwnTarget,
                FriendlyA,
                carrierGameObjectId,
                carrierEntityId),
            "unrelated friendly target is never a carrier");

        foreach (var invalidId in new[] { 0UL, 0xE0000000UL, ulong.MaxValue })
        {
            False(
                FarHelpCarrierRules.IsFallbackCarrier(
                    OwnTarget,
                    invalidId,
                    invalidId,
                    checked((uint)Math.Min(invalidId, uint.MaxValue))),
                "invalid incoming identity fails closed");
        }
    }

    public static void ActionShapeFailuresConsumeWithoutDrift()
    {
        var cases = new[]
        {
            (ValidAttempt() with { IsSupportedContext = false }, FarHelpOneShotReason.OutsideSupportedContext),
            (ValidAttempt() with { IsSupportedAction = false }, FarHelpOneShotReason.UnsupportedAction),
            (ValidAttempt() with { IsMovementAction = false }, FarHelpOneShotReason.NonMovementAction),
            (ValidAttempt() with { IsSupportedActionMode = false }, FarHelpOneShotReason.UnsupportedActionMode),
            (ValidAttempt() with { IsFriendlyAction = false }, FarHelpOneShotReason.NonFriendlyAction),
            (ValidAttempt() with { IsAreaTargetedAction = true }, FarHelpOneShotReason.AreaTargetedAction),
        };

        foreach (var (attempt, expectedReason) in cases)
        {
            var decision = FarHelpOneShotRules.Observe(
                Arm(),
                attempt,
                [Candidate(FriendlyA, 10f, 24, 2)]);
            ConsumedFallback(decision, expectedReason, OwnTarget, expectedReason.ToString());
        }
    }

    public static void NonMacroActionWaitsAndTimeoutFailsClosed()
    {
        var waiting = FarHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { IsEligibleMacroActionAttempt = false },
            [Candidate(FriendlyA, 10f, 24, 2)]);
        Equal(FarHelpOneShotDecisionKind.Waiting, waiting.Kind, "unrelated action waits");
        True(waiting.NextState.IsArmed, "unrelated action cannot steal token");
        Equal(OwnTarget, waiting.ForwardTargetId, "unrelated target stays exact");

        var boundary = FarHelpOneShotRules.Observe(
            Arm(lifetime: 750),
            ValidAttempt(now: 1_750),
            [Candidate(FriendlyA, 10f, 24, 2)]);
        Equal(FarHelpOneShotDecisionKind.Cleared, boundary.Kind, "deadline is expired");
        Equal(FarHelpOneShotReason.Expired, boundary.Reason, "timeout reason");
        Equal(OwnTarget, boundary.ForwardTargetId, "expiry never changes target");
    }

    public static void InvalidArmsAndHardResetFailClosed()
    {
        False(FarHelpOneShotRules.Arm(-1).IsArmed, "negative clock cannot arm");
        False(FarHelpOneShotRules.Arm(1_000, 0).IsArmed, "zero lifetime cannot arm");
        False(FarHelpOneShotRules.Arm(long.MaxValue, 1).IsArmed, "saturated empty interval cannot arm");

        var reset = FarHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { HardReset = true },
            [Candidate(FriendlyA, 10f, 24, 2)]);
        Equal(FarHelpOneShotDecisionKind.Cleared, reset.Kind, "hard reset clears");
        Equal(FarHelpOneShotReason.HardReset, reset.Reason, "hard reset reason");
        False(reset.NextState.IsArmed, "hard reset drops token");

        var backwards = FarHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt(now: 999),
            [Candidate(FriendlyA, 10f, 24, 2)]);
        Equal(FarHelpOneShotReason.ClockMovedBackwards, backwards.Reason, "backward clock clears");
        False(backwards.ShouldRewrite, "backward clock never rewrites");
    }

    private static FarHelpOneShotState Arm(long now = 1_000, long lifetime = 750) =>
        FarHelpOneShotRules.Arm(now, lifetime);

    private static FarHelpActionAttempt ValidAttempt(long now = 1_001) =>
        new(
            OriginalTargetId: OwnTarget,
            NowMilliseconds: now,
            IsEligibleMacroActionAttempt: true,
            IsSupportedContext: true,
            IsSupportedAction: true,
            IsMovementAction: true,
            IsSupportedActionMode: true,
            IsFriendlyAction: true,
            IsAreaTargetedAction: false,
            IsFallbackCarrier: false);

    private static FarHelpSelectionCandidate Candidate(
        ulong gameObjectId,
        float distance,
        uint jobId,
        int partySlot) =>
        new(
            gameObjectId,
            checked((uint)gameObjectId),
            partySlot,
            CurrentHp: 100,
            MaximumHp: 100,
            distance * distance,
            FarHelpSelectionRules.ClassifyPlayableJob(jobId),
            IsExactPartyMember: true,
            IsSelf: false,
            IsTargetable: true,
            HasValidActionTarget: true,
            HasRangeAndLineOfSight: true);

    private static void ConsumedFallback(
        FarHelpOneShotDecision decision,
        FarHelpOneShotReason expectedReason,
        ulong expectedTarget,
        string label)
    {
        Equal(FarHelpOneShotDecisionKind.ConsumedWithoutRewrite, decision.Kind, $"{label} kind");
        Equal(expectedReason, decision.Reason, $"{label} reason");
        Equal(expectedTarget, decision.ForwardTargetId, $"{label} target");
        True(decision.ConsumedActionIntent, $"{label} consumes");
        False(decision.NextState.IsArmed, $"{label} cannot drift");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
