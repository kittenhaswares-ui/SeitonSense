using SeitonSense.Core;

internal static class NearHelpOneShotSelfTests
{
    private const ulong OwnTarget = 0x100;
    private const ulong FriendlyA = 0x200;
    private const ulong FriendlyB = 0x201;

    public static void ValidAttemptSelectsAtActionTimeAndRewritesOnce()
    {
        var decision = NearHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt(),
            [
                Candidate(FriendlyA, currentHp: 80, distance: 1f),
                Candidate(FriendlyB, currentHp: 20, distance: 10f),
            ]);

        True(decision.ShouldRewrite, "eligible friendly action rewrites");
        True(decision.ConsumedActionIntent, "one action owns the token");
        Equal(FriendlyB, decision.ForwardTargetId, "action-time lowest health candidate");
        Equal(1, decision.SelectedCandidateIndex, "selected observation index");
        False(decision.NextState.IsArmed, "token is consumed before dispatch");

        var following = NearHelpOneShotRules.Observe(
            decision.NextState,
            ValidAttempt(now: 1_002),
            [Candidate(FriendlyA, 1, 1f)]);
        False(following.ShouldRewrite, "following action cannot reuse token");
        Equal(OwnTarget, following.ForwardTargetId, "following action remains unchanged");
    }

    public static void PressureSelectionRemainsActionTimeAndOneShot()
    {
        var decision = NearHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt(),
            [
                Candidate(FriendlyA, currentHp: 30, distance: 1f, pressure: 0),
                Candidate(FriendlyB, currentHp: 40, distance: 10f, pressure: 3),
            ],
            preferIncomingPressure: true,
            hasTrustedPressureView: true);

        True(decision.ShouldRewrite, "pressure-refined candidate rewrites once");
        Equal(FriendlyB, decision.ForwardTargetId, "pressure refines inside the exact health window");
        Equal(
            NearHelpSelectionReason.IncomingPressure,
            decision.SelectionReason,
            "one-shot decision exposes the selection reason");
        False(decision.NextState.IsArmed, "pressure refinement still consumes before dispatch");
    }

    public static void MissingCandidateUsesExactCarrierFallbackPolicy()
    {
        var compact = NearHelpOneShotRules.Observe(Arm(), ValidAttempt(), []);
        ConsumedFallback(
            compact,
            NearHelpOneShotReason.NoEligibleFriendlyCandidate,
            OwnTarget,
            "compact authored target remains exact");

        var carrier = NearHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { IsFallbackCarrier = true },
            []);
        ConsumedFallback(
            carrier,
            NearHelpOneShotReason.NoEligibleFriendlyCandidate,
            NearHelpOneShotRules.InvalidFallbackCarrierTargetId,
            "exact <2> carrier is invalidated for authored <t> fallback");
    }

    public static void CarrierIdentityDistinguishesAuthoredSlotFromOwnTarget()
    {
        const ulong carrierGameObjectId = 0x1234_5678_0000_0200UL;
        const uint carrierEntityId = 0x1000_0200;

        True(
            NearHelpCarrierRules.IsFallbackCarrier(OwnTarget, carrierEntityId, carrierGameObjectId, carrierEntityId),
            "authored <2> entity ID is a carrier");
        True(
            NearHelpCarrierRules.IsFallbackCarrier(OwnTarget, carrierGameObjectId, carrierGameObjectId, carrierEntityId),
            "authored <2> game object ID is a carrier");
        False(
            NearHelpCarrierRules.IsFallbackCarrier(carrierGameObjectId, carrierEntityId, carrierGameObjectId, carrierEntityId),
            "compact <t> on party slot 2 is not mistaken for a carrier");
        False(
            NearHelpCarrierRules.IsFallbackCarrier(carrierEntityId, carrierGameObjectId, carrierGameObjectId, carrierEntityId),
            "mixed actor identities still preserve compact <t>");
        False(
            NearHelpCarrierRules.IsFallbackCarrier(OwnTarget, FriendlyA, carrierGameObjectId, carrierEntityId),
            "unrelated friendly target is never a carrier");
    }

    public static void ActionShapeFailuresConsumeWithoutDrift()
    {
        var cases = new[]
        {
            (ValidAttempt() with { IsSupportedContext = false }, NearHelpOneShotReason.OutsideSupportedContext),
            (ValidAttempt() with { IsSupportedAction = false }, NearHelpOneShotReason.UnsupportedAction),
            (ValidAttempt() with { IsSupportedActionMode = false }, NearHelpOneShotReason.UnsupportedActionMode),
            (ValidAttempt() with { IsFriendlyAction = false }, NearHelpOneShotReason.NonFriendlyAction),
            (ValidAttempt() with { IsAreaTargetedAction = true }, NearHelpOneShotReason.AreaTargetedAction),
        };

        foreach (var (attempt, expectedReason) in cases)
        {
            var decision = NearHelpOneShotRules.Observe(
                Arm(),
                attempt,
                [Candidate(FriendlyA, 1, 1f)]);
            ConsumedFallback(decision, expectedReason, OwnTarget, expectedReason.ToString());
        }
    }

    public static void NonMacroActionWaitsAndTimeoutFailsClosed()
    {
        var waiting = NearHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { IsEligibleMacroActionAttempt = false },
            [Candidate(FriendlyA, 1, 1f)]);
        Equal(NearHelpOneShotDecisionKind.Waiting, waiting.Kind, "unrelated native action waits");
        True(waiting.NextState.IsArmed, "unrelated action cannot steal token");
        Equal(OwnTarget, waiting.ForwardTargetId, "unrelated target remains exact");

        var boundary = NearHelpOneShotRules.Observe(
            Arm(lifetime: 750),
            ValidAttempt(now: 1_750),
            [Candidate(FriendlyA, 1, 1f)]);
        Equal(NearHelpOneShotDecisionKind.Cleared, boundary.Kind, "deadline is expired");
        Equal(NearHelpOneShotReason.Expired, boundary.Reason, "timeout reason");
        Equal(OwnTarget, boundary.ForwardTargetId, "expiry does not mutate a call target");
        False(boundary.NextState.IsArmed, "expiry clears token");
    }

    public static void InvalidArmsAndHardResetFailClosed()
    {
        False(NearHelpOneShotRules.Arm(-1).IsArmed, "negative clock cannot arm");
        False(NearHelpOneShotRules.Arm(1_000, 0).IsArmed, "zero lifetime cannot arm");

        var reset = NearHelpOneShotRules.Observe(
            Arm(),
            ValidAttempt() with
            {
                OriginalTargetId = ulong.MaxValue,
                HardReset = true,
            },
            [Candidate(FriendlyA, 1, 1f)]);
        Equal(NearHelpOneShotDecisionKind.Cleared, reset.Kind, "hard reset clears");
        Equal(ulong.MaxValue, reset.ForwardTargetId, "hard reset preserves all target bits");
        False(reset.NextState.IsArmed, "hard reset disarms");
    }

    private static NearHelpOneShotState Arm(
        long lifetime = NearHelpOneShotRules.DefaultLifetimeMilliseconds) =>
        NearHelpOneShotRules.Arm(1_000, lifetime);

    private static NearHelpActionAttempt ValidAttempt(long now = 1_001) =>
        new(
            OriginalTargetId: OwnTarget,
            NowMilliseconds: now,
            IsEligibleMacroActionAttempt: true,
            IsSupportedContext: true,
            IsSupportedAction: true,
            IsSupportedActionMode: true,
            IsFriendlyAction: true,
            IsAreaTargetedAction: false,
            IsFallbackCarrier: false);

    private static NearHelpSelectionCandidate Candidate(
        ulong gameObjectId,
        uint currentHp,
        float distance,
        int? pressure = null) =>
        new(
            gameObjectId,
            (uint)gameObjectId,
            PartySlot: 1,
            currentHp,
            MaximumHp: 100,
            DistanceSquared: distance * distance,
            IsExactFriendly: true,
            IsSelf: false,
            HasValidActionTarget: true,
            HasRangeAndLineOfSight: true,
            UniqueIncomingEnemyPressureCount: pressure);

    private static void ConsumedFallback(
        NearHelpOneShotDecision decision,
        NearHelpOneShotReason expectedReason,
        ulong expectedTarget,
        string label)
    {
        False(decision.ShouldRewrite, $"{label} does not rewrite");
        True(decision.ConsumedActionIntent, $"{label} consumes one action intent");
        Equal(expectedTarget, decision.ForwardTargetId, $"{label} target policy");
        Equal(expectedReason, decision.Reason, $"{label} reason");
        Equal(-1, decision.SelectedCandidateIndex, $"{label} has no selection");
        False(decision.NextState.IsArmed, $"{label} cannot drift to a later action");
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
