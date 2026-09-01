using SeitonSense.Core;

internal static class NearHelpCastRedirectSelfTests
{
    private const uint FriendlyCastAction = 29_254;
    private const ulong Self = 0x100;
    private const ulong InjuredAlly = 0x200;
    private const ulong AuthoredTarget = 0x300;

    public static void ExactFriendlyCastAdmissionAndDecisionsAreClosed()
    {
        True(CanRank(), "exact owned friendly PvP cast is admitted");
        var closedGates = new[]
        {
            CanRank(owned: false),
            CanRank(supported: false),
            CanRank(resolved: 0),
            CanRank(exact: false),
            CanRank(row: FriendlyCastAction + 1),
            CanRank(pvp: false),
            CanRank(friendly: false),
            CanRank(ground: true),
            CanRank(range: 0f),
            CanRank(range: -1f),
            CanRank(range: float.NaN),
            CanRank(range: float.PositiveInfinity),
            CanRank(range: float.NegativeInfinity),
        };
        foreach (var allowed in closedGates)
            False(allowed, "each exact friendly cast admission gate independently denies");

        foreach (var visibleTargetMatches in new[] { false, true })
        {
            var decision = CastDecision(visibleTargetMatches);
            Equal(CastedMacroRedirectDecision.RedirectNearHelpCast, decision,
                visibleTargetMatches
                    ? "visible <t> cannot bypass friendly ranking"
                    : "hidden <2> carrier continues into friendly ranking");
            True(CastedMacroRedirectRules.ShouldContinueThroughTargetRanking(decision),
                "Near Help cast proceeds to its ordinary action-time ranking");
            False(CastedMacroRedirectRules.ShouldPassThroughWithoutRedirect(decision),
                "Near Help cast cannot fall through to authored-target or self healing");
            False(CastedMacroRedirectRules.ShouldTransferExactSmartActionFallbackLease(
                    exactSmartActionTokenConsumed: false, decision),
                "Near Help never receives a Smart Action fallback lease");
            False(CastedMacroRedirectRules.ShouldTransferExactSmartActionFallbackLease(
                    exactSmartActionTokenConsumed: true, decision),
                "Near Help redirect decision cannot transfer a hostile fallback lease");
        }

        Equal(CastedMacroRedirectDecision.RedirectNearHelpCast,
            CastedMacroRedirectRules.Evaluate(true, true, true, 0, 15, false,
                allowNearHelpCastRedirect: true),
            "exact base cast time also admits a friendly cast");
        Equal(CastedMacroRedirectDecision.PreserveAuthoredTarget,
            CastedMacroRedirectRules.Evaluate(true, true, false, 1_500, 0, true,
                allowNearHelpCastRedirect: true),
            "Near Help permission cannot bypass missing exact metadata for visible <t>");
        Equal(CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget,
            CastedMacroRedirectRules.Evaluate(true, true, false, 1_500, 0, false,
                allowNearHelpCastRedirect: true),
            "Near Help permission cannot bypass missing exact metadata for hidden <2>");
        Equal(CastedMacroRedirectDecision.NotApplicable,
            CastedMacroRedirectRules.Evaluate(true, true, true, 0, 0, false,
                allowNearHelpCastRedirect: true),
            "instant friendly actions keep the existing path");
        Equal(CastedMacroRedirectDecision.NotApplicable,
            CastedMacroRedirectRules.Evaluate(false, true, true, 1_500, 15, false,
                allowNearHelpCastRedirect: true),
            "no armed token means no friendly cast redirect");
        Equal(CastedMacroRedirectDecision.NotApplicable,
            CastedMacroRedirectRules.Evaluate(true, false, true, 1_500, 15, false,
                allowNearHelpCastRedirect: true),
            "unsupported native action type cannot redirect");
        Equal(CastedMacroRedirectDecision.PreserveAuthoredTarget,
            CastedMacroRedirectRules.Evaluate(true, true, true, 1_500, 15, true),
            "Near Assist retains authored visible casts without Near Help ownership");
        Equal(CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget,
            CastedMacroRedirectRules.Evaluate(true, true, true, 1_500, 15, false),
            "Near Assist retains hidden cast suppression without Near Help ownership");
        Equal(CastedMacroRedirectDecision.RedirectSmartActionCast,
            CastedMacroRedirectRules.Evaluate(true, true, true, 1_500, 15, false,
                allowSmartActionCastRedirect: true),
            "hostile Smart Action casts retain their separate path");

        foreach (var decision in Enum.GetValues<CastedMacroRedirectDecision>())
        {
            Equal(decision is CastedMacroRedirectDecision.RedirectSmartActionCast or
                    CastedMacroRedirectDecision.RedirectNearHelpCast,
                CastedMacroRedirectRules.ShouldContinueThroughTargetRanking(decision),
                $"only owned cast redirects proceed to ranking: {decision}");
        }
    }

    public static void FriendlyCastsRankAtActionTimeAndConsumeOnce()
    {
        var healthySelf = Candidate(Self, hp: 100, distance: 0f, self: true);
        var injuredAlly = Candidate(InjuredAlly, hp: 20, distance: 15f);

        foreach (var isCarrier in new[] { false, true })
        {
            var castDecision = CastDecision(visibleTargetMatches: !isCarrier);
            True(CastedMacroRedirectRules.ShouldContinueThroughTargetRanking(castDecision),
                "cast classification must lead into Near Help selection");
            var selected = NearHelpOneShotRules.Observe(
                NearHelpOneShotRules.Arm(1_000),
                Attempt(isCarrier),
                [healthySelf, injuredAlly]);
            True(selected.ShouldRewrite, "friendly cast rewrites to the selected ally");
            Equal(InjuredAlly, selected.ForwardTargetId,
                "injured reachable ally beats healthy self even when self is closest");
            True(selected.ConsumedActionIntent, "one friendly cast consumes the token");
            False(selected.NextState.IsArmed, "cast token is consumed before native dispatch");

            var second = NearHelpOneShotRules.Observe(
                selected.NextState, Attempt(isCarrier, now: 1_002),
                [healthySelf with { CurrentHp = 1 }, injuredAlly]);
            False(second.ShouldRewrite, "later cast cannot reuse a consumed Near Help intent");
            Equal(AuthoredTarget, second.ForwardTargetId,
                "later cast preserves its own authored target");
        }

        var selfAuthoredCast = NearHelpOneShotRules.Observe(
            NearHelpOneShotRules.Arm(1_000),
            Attempt(isCarrier: false) with { OriginalTargetId = Self },
            [healthySelf, injuredAlly]);
        Equal(InjuredAlly, selfAuthoredCast.ForwardTargetId,
            "a self-authored cast still selects the injured reachable ally");

        var criticalSelf = NearHelpOneShotRules.Observe(
            NearHelpOneShotRules.Arm(1_000), Attempt(isCarrier: false),
            [healthySelf with { CurrentHp = 10 }, injuredAlly],
            preferIncomingPressure: true,
            hasTrustedPressureView: true);
        Equal(Self, criticalSelf.ForwardTargetId,
            "critical self remains a legitimate winner when exact action permits self heal");
        Equal(NearHelpSelectionReason.CriticalHealthAnchor, criticalSelf.SelectionReason,
            "critical HP ordering remains unchanged");

        var notSelfTargetable = NearHelpOneShotRules.Observe(
            NearHelpOneShotRules.Arm(1_000), Attempt(isCarrier: false),
            [healthySelf with { CurrentHp = 1, IsActionSelfTargetable = false }, injuredAlly]);
        Equal(InjuredAlly, notSelfTargetable.ForwardTargetId,
            "casts cannot select self unless exact action supports self-targeting");

        foreach (var isCarrier in new[] { false, true })
        {
            var fallback = NearHelpOneShotRules.Observe(
                NearHelpOneShotRules.Arm(1_000), Attempt(isCarrier),
                [injuredAlly with { HasRangeAndLineOfSight = false }]);
            False(fallback.ShouldRewrite, "no reachable friendly candidate cannot be fabricated");
            True(fallback.ConsumedActionIntent, "fallback still consumes only this cast intent");
            False(fallback.NextState.IsArmed, "fallback cannot drift into another macro action");
            Equal(isCarrier ? NearHelpOneShotRules.InvalidFallbackCarrierTargetId : AuthoredTarget,
                fallback.ForwardTargetId,
                "hidden <2> invalidation and visible <t> fallback keep their existing policy");
        }
    }

    public static void ExactCastClaimGenerationPreservesNewerIntent()
    {
        const ulong generation = 42;
        True(CastedMacroRedirectRules.CanConsumeExactNearHelpCastClaim(generation, generation, true),
            "same nonzero generation and exact owner/state permit consumption");
        var rejectedClaims = new (ulong Claimed, ulong Current, bool Exact)[]
        {
            (0, 0, true),
            (0, generation, true),
            (generation, 0, true),
            (generation, generation + 1, true),
            (generation + 1, generation, true),
            (generation, generation, false),
            (ulong.MaxValue, generation, true),
        };
        var newerIntent = NearHelpOneShotRules.Arm(2_000);
        foreach (var claim in rejectedClaims)
        {
            var current = newerIntent;
            var consume = CastedMacroRedirectRules.CanConsumeExactNearHelpCastClaim(
                claim.Claimed, claim.Current, claim.Exact);
            False(consume, "stale or mismatched claim cannot consume current Near Help intent");
            if (consume)
                current = NearHelpOneShotState.Initial;
            Equal(newerIntent, current, "denied claim leaves newer owner/state untouched");
            True(current.IsArmed, "denied old cast cannot clear newly armed macro");
        }

        True(CastedMacroRedirectRules.CanConsumeExactNearHelpCastClaim(
                ulong.MaxValue, ulong.MaxValue, true),
            "full-width generation comparison has no truncation");
        var first = NearHelpOneShotRules.Observe(
            newerIntent,
            Attempt(isCarrier: true, now: 2_001),
            [Candidate(InjuredAlly, hp: 20, distance: 15f)]);
        False(first.NextState.IsArmed, "valid claim can consume its exact intent once");
        False(CastedMacroRedirectRules.CanConsumeExactNearHelpCastClaim(
                generation, generation, ownerAndStateMatch: first.NextState.IsArmed),
            "the consumed state prevents duplicate consumption even at same generation");
    }

    private static bool CanRank(
        bool owned = true,
        bool supported = true,
        uint resolved = FriendlyCastAction,
        bool exact = true,
        uint row = FriendlyCastAction,
        bool pvp = true,
        bool friendly = true,
        bool ground = false,
        float range = 30f) =>
        CastedMacroRedirectRules.CanContinueNearHelpCast(
            owned, supported, resolved, exact, row, pvp, friendly, ground, range);

    private static CastedMacroRedirectDecision CastDecision(bool visibleTargetMatches) =>
        CastedMacroRedirectRules.Evaluate(
            redirectTokenArmed: true,
            supportedActionType: true,
            exactActionMetadata: true,
            adjustedCastTimeMilliseconds: 1_500,
            baseCastTime100Milliseconds: 15,
            authoredTargetMatchesVisibleTarget: visibleTargetMatches,
            allowNearHelpCastRedirect: CanRank());

    private static NearHelpActionAttempt Attempt(bool isCarrier, long now = 1_001) =>
        new(
            OriginalTargetId: AuthoredTarget,
            NowMilliseconds: now,
            IsEligibleMacroActionAttempt: true,
            IsSupportedContext: true,
            IsSupportedAction: true,
            IsSupportedActionMode: true,
            IsFriendlyAction: true,
            IsAreaTargetedAction: false,
            IsFallbackCarrier: isCarrier);

    private static NearHelpSelectionCandidate Candidate(
        ulong identity, uint hp, float distance, bool self = false) =>
        new(
            GameObjectId: identity,
            EntityId: (uint)identity,
            PartySlot: self ? 1 : 2,
            CurrentHp: hp,
            MaximumHp: 100,
            DistanceSquared: distance * distance,
            IsExactFriendly: true,
            IsSelf: self,
            HasValidActionTarget: true,
            HasRangeAndLineOfSight: true,
            UniqueIncomingEnemyPressureCount: 0,
            IsActionSelfTargetable: true);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
