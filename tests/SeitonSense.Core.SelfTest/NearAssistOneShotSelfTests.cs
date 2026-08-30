using SeitonSense.Core;

internal static class NearAssistOneShotSelfTests
{
    private const ulong OwnTargetA = 0x100;
    private const ulong OwnTargetB = 0x101;
    private const ulong EnemyA = 0x200;
    private const ulong EnemyB = 0x201;

    public static void ValidAttemptRewritesExactlyOnce()
    {
        var state = Arm();
        var first = NearAssistOneShotRules.Observe(state, ValidAttempt());

        True(first.ShouldRewrite, "fully validated attempt rewrites");
        True(first.ConsumedActionIntent, "rewrite consumes before the native action");
        Equal(EnemyA, first.ForwardTargetId, "only the target ID changes");
        False(first.NextState.IsArmed, "token is spent before the caller dispatches");

        var second = NearAssistOneShotRules.Observe(first.NextState, ValidAttempt(now: 1_002));
        False(second.ShouldRewrite, "the following action cannot reuse the token");
        Equal(OwnTargetA, second.ForwardTargetId, "second call keeps its exact original target");
    }

    public static void TimeoutFailsClosedAtBoundary()
    {
        var state = Arm(lifetime: 750);
        var inside = NearAssistOneShotRules.Observe(state, ValidAttempt(now: 1_749));
        True(inside.ShouldRewrite, "token remains valid one millisecond before expiry");

        state = Arm(lifetime: 750);
        var boundary = NearAssistOneShotRules.Observe(state, ValidAttempt(now: 1_750));
        Equal(NearAssistOneShotDecisionKind.Cleared, boundary.Kind, "deadline itself is expired");
        Equal(NearAssistOneShotReason.Expired, boundary.Reason, "timeout reason");
        Equal(OwnTargetA, boundary.ForwardTargetId, "timeout preserves target bit-for-bit");
        False(boundary.NextState.IsArmed, "expired token is cleared");
    }

    public static void EnemySlotAndIdentityDriftFailClosed()
    {
        var slotDrift = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { ResolvedEnemySlot = 4 });
        ConsumedFallback(slotDrift, NearAssistOneShotReason.EnemySlotChanged, "slot drift");

        var identityDrift = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { ResolvedEnemyGameObjectId = EnemyB });
        ConsumedFallback(identityDrift, NearAssistOneShotReason.EnemyIdentityChanged, "identity drift");
    }

    public static void RangeAndLineOfSightFailureConsumes()
    {
        var nativeReject = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { HasValidActionTarget = false });
        ConsumedFallback(nativeReject, NearAssistOneShotReason.ActionRejectedForTarget, "native target rejection");

        var rangeOrLos = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { HasRangeAndLineOfSight = false });
        ConsumedFallback(rangeOrLos, NearAssistOneShotReason.OutOfRangeOrLineOfSight, "range or line of sight");
    }

    public static void ActionShapeAndModeFailuresConsume()
    {
        var nonHostile = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { IsHostileAction = false });
        ConsumedFallback(nonHostile, NearAssistOneShotReason.NonHostileAction, "non-hostile action");

        var area = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { IsAreaTargetedAction = true });
        ConsumedFallback(area, NearAssistOneShotReason.AreaTargetedAction, "area action");

        var unsupported = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { IsSupportedAction = false });
        ConsumedFallback(unsupported, NearAssistOneShotReason.UnsupportedAction, "unsupported action");

        var mode = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { IsSupportedActionMode = false });
        ConsumedFallback(mode, NearAssistOneShotReason.UnsupportedActionMode, "unsupported mode");

        Equal(
            CastedMacroRedirectDecision.PreserveAuthoredTarget,
            CastedMacroRedirectRules.Evaluate(true, true, true, 1_500, 15, true),
            "adjusted cast keeps authored target");
        Equal(
            CastedMacroRedirectDecision.PreserveAuthoredTarget,
            CastedMacroRedirectRules.Evaluate(true, true, true, 0, 15, true),
            "base cast metadata keeps authored target when adjusted timing is ambiguous");
        Equal(
            CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget,
            CastedMacroRedirectRules.Evaluate(true, true, true, 1_500, 15, false),
            "cast with hidden or missing target suppresses");
        Equal(
            CastedMacroRedirectDecision.NotApplicable,
            CastedMacroRedirectRules.Evaluate(true, true, true, 0, 0, true),
            "instant action keeps redirect path");
        Equal(
            CastedMacroRedirectDecision.NotApplicable,
            CastedMacroRedirectRules.Evaluate(false, true, true, 1_500, 15, true),
            "cast cannot consume a missing token");
        Equal(
            CastedMacroRedirectDecision.NotApplicable,
            CastedMacroRedirectRules.Evaluate(true, false, true, 1_500, 15, true),
            "unsupported action type cannot consume cast token");
        Equal(
            CastedMacroRedirectDecision.NotApplicable,
            CastedMacroRedirectRules.Evaluate(true, true, false, 0, 15, true),
            "unverified base cast metadata cannot prove a cast");
        Equal(
            CastedMacroRedirectDecision.PreserveAuthoredTarget,
            CastedMacroRedirectRules.Evaluate(true, true, false, 1_500, 0, true),
            "adjusted cast time independently proves a cast");
        True(
            CastedMacroRedirectRules.ShouldPassThroughWithoutRedirect(
                CastedMacroRedirectDecision.PassThroughStaleLifecycle),
            "stale lifecycle disposition bypasses every legacy redirect");
        True(
            CastedMacroRedirectRules.ShouldPassThroughWithoutRedirect(
                CastedMacroRedirectDecision.PreserveAuthoredTarget),
            "visible authored cast bypasses every legacy redirect");
        False(
            CastedMacroRedirectRules.ShouldPassThroughWithoutRedirect(
                CastedMacroRedirectDecision.NotApplicable),
            "not-applicable cast classification keeps the normal redirect path");
        False(
            CastedMacroRedirectRules.ShouldPassThroughWithoutRedirect(
                CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget),
            "hidden cast suppression never passes through");
        False(
            CastedMacroRedirectRules.ShouldPassThroughWithoutRedirect(
                CastedMacroRedirectDecision.SuppressStaleOwnership),
            "stale ownership suppression never passes through");
    }

    public static void NonMacroCallsDoNotStealTheToken()
    {
        var state = Arm();
        var unrelated = NearAssistOneShotRules.Observe(
            state,
            ValidAttempt() with { IsEligibleMacroActionAttempt = false });

        Equal(NearAssistOneShotDecisionKind.Waiting, unrelated.Kind, "unrelated action waits");
        False(unrelated.ConsumedActionIntent, "unrelated action does not consume");
        Equal(OwnTargetA, unrelated.ForwardTargetId, "unrelated target is untouched");
        True(unrelated.NextState.IsArmed, "token remains armed");

        var nextMacro = NearAssistOneShotRules.Observe(unrelated.NextState, ValidAttempt(now: 1_002));
        True(nextMacro.ShouldRewrite, "next eligible macro attempt still owns the token");
    }

    public static void OwnTargetDriftPreservesTheActualCallTarget()
    {
        var decision = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { OriginalTargetId = OwnTargetB });

        True(decision.ShouldRewrite, "a changed own target does not invalidate the snapshotted assist intent");
        Equal(EnemyA, decision.ForwardTargetId, "assist target wins when a candidate is valid");
        False(decision.NextState.IsArmed, "changed-target attempt still consumes once");

        var noOwnTargetState = NearAssistOneShotRules.Arm(
            3,
            EnemyA,
            1_000,
            500);
        var noOwnTarget = NearAssistOneShotRules.Observe(
            noOwnTargetState,
            ValidAttempt() with { OriginalTargetId = 0xE0000000UL });
        True(noOwnTarget.ShouldRewrite, "missing own target can still use the proven ally target");
        Equal(EnemyA, noOwnTarget.ForwardTargetId, "missing own target rewrites to enemy");
    }

    public static void MissingCandidateArmsOneFallbackGuard()
    {
        var state = NearAssistOneShotRules.ArmFallback(1_000, 750);
        True(state.IsArmed, "missing candidate still arms a bounded carrier guard");

        var decision = NearAssistOneShotRules.Observe(state, ValidAttempt());
        ConsumedFallback(
            decision,
            NearAssistOneShotReason.NoRedirectCandidate,
            "missing candidate carrier guard");
    }

    public static void CarrierIdentityDoesNotDependOnMacroLineTiming()
    {
        False(
            NearAssistCarrierRules.IsFallbackCarrier(OwnTargetA, OwnTargetA, EnemyA, (uint)EnemyA),
            "normal current-target line is not a carrier");
        True(
            NearAssistCarrierRules.IsFallbackCarrier(OwnTargetA, EnemyA, EnemyA, (uint)EnemyA),
            "a concrete enemy-slot carrier differs from the current target");
        True(
            NearAssistCarrierRules.IsFallbackCarrier(0, EnemyA, EnemyA, (uint)EnemyA),
            "enemy-slot carrier works without an own target");
        True(
            NearAssistCarrierRules.IsFallbackCarrier(0xE0000000UL, EnemyA, 0xABCDEFUL, (uint)EnemyA),
            "network entity identity also recognizes the exact e1 carrier");
        False(
            NearAssistCarrierRules.IsFallbackCarrier(OwnTargetA, 0, EnemyA, (uint)EnemyA),
            "invalid incoming target is not reclassified");
        False(
            NearAssistCarrierRules.IsFallbackCarrier(OwnTargetA, 0xE0000000UL, EnemyA, (uint)EnemyA),
            "native invalid sentinel is not reclassified");
        False(
            NearAssistCarrierRules.IsFallbackCarrier(OwnTargetA, EnemyB, EnemyA, (uint)EnemyA),
            "an unrelated changed target is never mistaken for the e1 carrier");
        False(
            NearAssistCarrierRules.IsFallbackCarrier(EnemyA, EnemyA, EnemyA, (uint)EnemyA),
            "e1 already selected is ordinary current-target fallback");
        False(
            NearAssistCarrierRules.IsFallbackCarrier(0xABCDEFUL, EnemyA, 0xABCDEFUL, (uint)EnemyA),
            "dual game-object and entity representations still describe the same selected e1 actor");
    }

    public static void ReplacementUsesOnlyTheNewestToken()
    {
        var first = Arm(slot: 2, enemy: EnemyA);
        True(first.IsArmed, "first token arms");

        var replacement = NearAssistOneShotRules.Arm(
            enemySlot: 5,
            enemyGameObjectId: EnemyB,
            nowMilliseconds: 1_100);

        var staleAttempt = NearAssistOneShotRules.Observe(
            replacement,
            ValidAttempt(now: 1_101) with
            {
                ResolvedEnemySlot = 2,
                ResolvedEnemyGameObjectId = EnemyA,
            });
        ConsumedFallback(staleAttempt, NearAssistOneShotReason.EnemySlotChanged, "replaced token ignores stale slot");

        replacement = NearAssistOneShotRules.Arm(5, EnemyB, 1_100);
        var currentAttempt = NearAssistOneShotRules.Observe(
            replacement,
            ValidAttempt(now: 1_101) with
            {
                ResolvedEnemySlot = 5,
                ResolvedEnemyGameObjectId = EnemyB,
            });
        True(currentAttempt.ShouldRewrite, "newest token rewrites");
        Equal(EnemyB, currentAttempt.ForwardTargetId, "newest enemy owns the rewrite");
    }

    public static void InvalidStateAndResetsPreserveOriginalBits()
    {
        var invalidArms = new[]
        {
            NearAssistOneShotRules.Arm(0, EnemyA, 1_000),
            NearAssistOneShotRules.Arm(6, EnemyA, 1_000),
            NearAssistOneShotRules.Arm(3, 0, 1_000),
            NearAssistOneShotRules.Arm(3, 0xE0000000, 1_000),
            NearAssistOneShotRules.Arm(3, EnemyA, -1),
            NearAssistOneShotRules.Arm(3, EnemyA, 1_000, 0),
        };

        True(invalidArms.All(state => !state.IsArmed), "invalid arm input always clears");

        var reset = NearAssistOneShotRules.Observe(
            Arm(),
            ValidAttempt() with { HardReset = true, OriginalTargetId = ulong.MaxValue });
        Equal(ulong.MaxValue, reset.ForwardTargetId, "hard reset preserves every target bit");
        Equal(NearAssistOneShotReason.HardReset, reset.Reason, "hard reset reason");

        var backwards = NearAssistOneShotRules.Observe(Arm(), ValidAttempt(now: 999));
        Equal(OwnTargetA, backwards.ForwardTargetId, "clock rollback preserves target");
        Equal(NearAssistOneShotReason.ClockMovedBackwards, backwards.Reason, "clock rollback reason");
        False(backwards.NextState.IsArmed, "clock rollback clears token");
    }

    private static NearAssistOneShotState Arm(
        int slot = 3,
        ulong enemy = EnemyA,
        long lifetime = NearAssistOneShotRules.DefaultLifetimeMilliseconds) =>
        NearAssistOneShotRules.Arm(
            slot,
            enemy,
            nowMilliseconds: 1_000,
            lifetimeMilliseconds: lifetime);

    private static NearAssistActionAttempt ValidAttempt(long now = 1_001) =>
        new(
            OriginalTargetId: OwnTargetA,
            NowMilliseconds: now,
            IsEligibleMacroActionAttempt: true,
            IsSupportedContext: true,
            IsSupportedAction: true,
            IsSupportedActionMode: true,
            IsHostileAction: true,
            IsAreaTargetedAction: false,
            ResolvedEnemySlot: 3,
            ResolvedEnemyGameObjectId: EnemyA,
            IsResolvedEnemyValid: true,
            HasValidActionTarget: true,
            HasRangeAndLineOfSight: true);

    private static void ConsumedFallback(
        NearAssistOneShotDecision decision,
        NearAssistOneShotReason expectedReason,
        string label,
        ulong expectedOriginalTarget = OwnTargetA)
    {
        False(decision.ShouldRewrite, $"{label} does not rewrite");
        True(decision.ConsumedActionIntent, $"{label} consumes the one-shot");
        Equal(expectedOriginalTarget, decision.ForwardTargetId, $"{label} preserves original target exactly");
        Equal(expectedReason, decision.Reason, $"{label} reason");
        False(decision.NextState.IsArmed, $"{label} cannot drift into the next action");
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
