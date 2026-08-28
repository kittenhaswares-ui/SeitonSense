using SeitonSense.Core;

internal static class DarkKnightShadowbringerSelfTests
{
    private const int HeldKey = 65;
    private static readonly TargetPressureActorIdentity Local =
        new(0x1001, 0x101);
    private static readonly TargetPressureActorIdentity Target =
        new(0x2001, 0x201);

    public static void ExactMetadataAndSafeFallbackBoundariesArePinned()
    {
        Equal(32u, DarkKnightShadowbringerRules.DarkKnightJobId, "DRK job");
        Equal(29_091u, DarkKnightShadowbringerRules.ShadowbringerActionId,
            "base Shadowbringer");
        Equal(29_738u,
            DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
            "Dark Arts adjusted Shadowbringer");
        Equal(29_093u, DarkKnightShadowbringerRules.TheBlackestNightActionId,
            "The Blackest Night");
        Equal(3_033u, DarkKnightShadowbringerRules.BlackbloodStatusId,
            "Blackblood status");
        Equal(3_034u, DarkKnightShadowbringerRules.DarkArtsStatusId,
            "Dark Arts status");
        Equal(213_106u,
            DarkKnightShadowbringerRules.BlackbloodStatusIconId,
            "Blackblood icon");
        Equal(12_000u, DarkKnightShadowbringerRules.ShadowbringerHpCost,
            "base HP cost");
        Equal(10, DarkKnightShadowbringerRules.MaximumRangeYalms, "range");
        Equal(0, DarkKnightShadowbringerRules.ExpectedRuntimeRecastGroupIndex,
            "runtime recast group");
        Equal(1_000,
            DarkKnightShadowbringerRules.ExpectedAdjustedRecastMilliseconds,
            "adjusted recast");
        Equal(1_800L,
            DarkKnightShadowbringerRules.AutomaticCadenceMilliseconds,
            "shared automatic cadence");
        True(DarkKnightShadowbringerRules.HasExpectedPlayerActionFlag(
                DarkKnightShadowbringerRules.ShadowbringerActionId,
                isPlayerAction: true),
            "hotbar Shadowbringer is a player action");
        False(DarkKnightShadowbringerRules.HasExpectedPlayerActionFlag(
                DarkKnightShadowbringerRules.ShadowbringerActionId,
                isPlayerAction: false),
            "hotbar Shadowbringer rejects the adjusted-row flag");
        True(DarkKnightShadowbringerRules.HasExpectedPlayerActionFlag(
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                isPlayerAction: false),
            "Dark Arts Shadowbringer is an internal adjusted row");
        False(DarkKnightShadowbringerRules.HasExpectedPlayerActionFlag(
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                isPlayerAction: true),
            "Dark Arts adjusted row is not a standalone player action");
        False(DarkKnightShadowbringerRules.HasExpectedPlayerActionFlag(
                1,
                isPlayerAction: true),
            "unknown action rows fail closed");

        False(Safe(85_000, 100_000, true, 1),
            "exactly 85 percent is not strictly above threshold");
        True(Safe(85_001, 100_000, true, 1),
            "one HP above 85 percent is eligible");
        False(Safe(85_001, 100_000, true, 2),
            "pressure two is not below the exclusive limit");
        False(Safe(85_001, 100_000, false, 0),
            "unknown pressure is not synthetic zero");
        False(Safe(12_000, 12_000, true, 0),
            "exact HP cost cannot be spent");
        False(Safe(100_001, 100_000, true, 0),
            "invalid HP sample fails closed");
        False(DarkKnightShadowbringerRules.IsSafeFallbackEligible(
                90_000,
                100_000,
                pressureKnown: true,
                incomingPressure: 0,
                minimumHpPercent: 0,
                DarkKnightShadowbringerRules.DefaultPressureLimitExclusive),
            "out-of-range HP configuration fails closed");
        False(DarkKnightShadowbringerRules.IsSafeFallbackEligible(
                90_000,
                100_000,
                pressureKnown: true,
                incomingPressure: 0,
                DarkKnightShadowbringerRules.DefaultMinimumHpPercent,
                pressureLimitExclusive: 7),
            "out-of-range pressure configuration fails closed");
    }

    public static void BlackbloodMustBeObservedThenStablyDisappear()
    {
        var manual = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            DarkKnightBlackbloodGateState.Initial,
            preservationEnabled: true,
            exactBlackbloodActive: true,
            nowMilliseconds: 1_000);
        Equal(DarkKnightBlackbloodGatePhase.AwaitingConsumption,
            manual.Phase, "pre-existing manual Blackblood blocks automation");
        False(manual.IsDispatchAllowed,
            "active manual Blackblood cannot be overwritten");

        var armed = DarkKnightShadowbringerRules
            .MarkAutomaticShadowbringerBoundary(
                DarkKnightBlackbloodGateState.Initial,
                preservationEnabled: true,
                ClientActionAttemptOutcome.ClientAccepted,
                nowMilliseconds: 2_000);
        Equal(DarkKnightBlackbloodGatePhase.AwaitingExposure,
            armed.Phase, "accepted automatic action waits for propagation");
        False(armed.IsDispatchAllowed,
            "first missing status sample cannot immediately rearm");

        var stillWaiting = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            armed,
            preservationEnabled: true,
            exactBlackbloodActive: false,
            nowMilliseconds: 2_000 +
                DarkKnightShadowbringerRules
                    .BlackbloodPropagationWaitMilliseconds - 1);
        Equal(DarkKnightBlackbloodGatePhase.AwaitingExposure,
            stillWaiting.Phase, "bounded propagation wait remains closed");

        var exposed = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            stillWaiting,
            preservationEnabled: true,
            exactBlackbloodActive: true,
            nowMilliseconds: 3_499);
        Equal(DarkKnightBlackbloodGatePhase.AwaitingConsumption,
            exposed.Phase, "exact Blackblood exposure is remembered");

        var oneMiss = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            exposed,
            preservationEnabled: true,
            exactBlackbloodActive: false,
            nowMilliseconds: 3_500);
        Equal(DarkKnightBlackbloodGatePhase.AwaitingConsumption,
            oneMiss.Phase, "one absent sample is treated as status flicker");
        False(oneMiss.IsDispatchAllowed,
            "single absence cannot overwrite a still-active buff");
        var duplicateSameFrame = DarkKnightShadowbringerRules
            .ObserveBlackbloodGate(
                oneMiss,
                preservationEnabled: true,
                exactBlackbloodActive: false,
                nowMilliseconds: 3_500);
        Equal(1, duplicateSameFrame.ConsecutiveAbsentObservations,
            "priority and deferred passes cannot count one frame twice");
        var flickerReturn = DarkKnightShadowbringerRules
            .ObserveBlackbloodGate(
                duplicateSameFrame,
                preservationEnabled: true,
                exactBlackbloodActive: true,
                nowMilliseconds: 3_501);
        Equal(0, flickerReturn.ConsecutiveAbsentObservations,
            "visible status clears the absence debounce");

        var absenceOne = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            flickerReturn,
            preservationEnabled: true,
            exactBlackbloodActive: false,
            nowMilliseconds: 3_600);
        var consumed = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            absenceOne,
            preservationEnabled: true,
            exactBlackbloodActive: false,
            nowMilliseconds: 3_601);
        True(consumed.IsDispatchAllowed,
            "stable consumption or natural expiry rearms automation");

        var inferredOneMiss = DarkKnightShadowbringerRules
            .ObserveBlackbloodGate(
            armed,
            preservationEnabled: true,
            exactBlackbloodActive: false,
            nowMilliseconds: 2_000 +
                DarkKnightShadowbringerRules
                    .BlackbloodPropagationWaitMilliseconds);
        Equal(DarkKnightBlackbloodGatePhase.AwaitingConsumption,
            inferredOneMiss.Phase,
            "accepted action can infer only the first missed absence after propagation grace");
        Equal(1, inferredOneMiss.ConsecutiveAbsentObservations,
            "missed complete status lifecycle still requires a later sample");
        False(inferredOneMiss.IsDispatchAllowed,
            "accepted missed lifecycle remains blocked at the grace boundary");
        var sameGraceFrame = DarkKnightShadowbringerRules
            .ObserveBlackbloodGate(
                inferredOneMiss,
                preservationEnabled: true,
                exactBlackbloodActive: false,
                nowMilliseconds: 2_000 +
                    DarkKnightShadowbringerRules
                        .BlackbloodPropagationWaitMilliseconds);
        Equal(1, sameGraceFrame.ConsecutiveAbsentObservations,
            "same timestamp cannot supply the inferred second absence");
        var inferredConsumed = DarkKnightShadowbringerRules
            .ObserveBlackbloodGate(
                sameGraceFrame,
                preservationEnabled: true,
                exactBlackbloodActive: false,
                nowMilliseconds: 2_000 +
                    DarkKnightShadowbringerRules
                        .BlackbloodPropagationWaitMilliseconds + 1);
        True(inferredConsumed.IsDispatchAllowed,
            "one later distinct absence rearms an accepted missed lifecycle");

        var ambiguous = DarkKnightShadowbringerRules
            .MarkAutomaticShadowbringerBoundary(
                DarkKnightBlackbloodGateState.Initial,
                preservationEnabled: true,
                ClientActionAttemptOutcome.AcceptanceUnknown,
                nowMilliseconds: 3_000);
        Equal(DarkKnightBlackbloodGatePhase.AwaitingExposure,
            ambiguous.Phase,
            "unknown acceptance uses the bounded propagation lifecycle");
        False(ambiguous.IsDispatchAllowed,
            "ambiguous native acceptance also fails closed during propagation");
        var ambiguousLater = DarkKnightShadowbringerRules
            .ObserveBlackbloodGate(
                ambiguous,
                preservationEnabled: true,
                exactBlackbloodActive: false,
                nowMilliseconds: 3_000 +
                    DarkKnightShadowbringerRules
                        .BlackbloodPropagationWaitMilliseconds);
        Equal(DarkKnightBlackbloodGatePhase.AwaitingConsumption,
            ambiguousLater.Phase,
            "unknown acceptance infers only one absence after the grace");
        var ambiguousReleased = DarkKnightShadowbringerRules
            .ObserveBlackbloodGate(
                ambiguousLater,
                preservationEnabled: true,
                exactBlackbloodActive: false,
                nowMilliseconds: 3_000 +
                    DarkKnightShadowbringerRules
                        .BlackbloodPropagationWaitMilliseconds + 1);
        True(ambiguousReleased.IsDispatchAllowed,
            "unknown acceptance avoids a permanent manual-only unlock");
        var disabled = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            ambiguous,
            preservationEnabled: false,
            exactBlackbloodActive: true,
            nowMilliseconds: 3_001);
        True(disabled.IsDispatchAllowed,
            "disabling the nested option preserves the old helper contract");
    }

    public static void BlackbloodConsumptionRearmsSafeFallbackWithoutSpam()
    {
        var fallback = Fallback();
        fallback = DarkKnightShadowbringerRules.MarkFallbackSpent(
            fallback,
            fallback.Generation);
        True(fallback.IsSpent, "first safe HP-cost opportunity is spent");

        var gate = DarkKnightShadowbringerRules
            .MarkAutomaticShadowbringerBoundary(
                DarkKnightBlackbloodGateState.Initial,
                preservationEnabled: true,
                ClientActionAttemptOutcome.ClientAccepted,
                nowMilliseconds: 10_000);
        gate = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            gate,
            preservationEnabled: true,
            exactBlackbloodActive: false,
            nowMilliseconds: 10_001);
        fallback = DarkKnightShadowbringerRules.ObserveFallback(
            fallback,
            exactFallbackEligibility: gate.IsDispatchAllowed);
        False(gate.IsDispatchAllowed,
            "propagation sample cannot reopen the helper");

        gate = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            gate,
            preservationEnabled: true,
            exactBlackbloodActive: true,
            nowMilliseconds: 10_002);
        fallback = DarkKnightShadowbringerRules.ObserveFallback(
            fallback,
            exactFallbackEligibility: gate.IsDispatchAllowed);
        False(fallback.HasTrackedEpisode,
            "blocked Blackblood interval closes the spent fallback episode");

        gate = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            gate,
            preservationEnabled: true,
            exactBlackbloodActive: false,
            nowMilliseconds: 10_003);
        fallback = DarkKnightShadowbringerRules.ObserveFallback(
            fallback,
            exactFallbackEligibility: gate.IsDispatchAllowed);
        False(gate.IsDispatchAllowed,
            "first fully absent sample remains blocked");

        gate = DarkKnightShadowbringerRules.ObserveBlackbloodGate(
            gate,
            preservationEnabled: true,
            exactBlackbloodActive: false,
            nowMilliseconds: 10_004);
        fallback = DarkKnightShadowbringerRules.ObserveFallback(
            fallback,
            exactFallbackEligibility: gate.IsDispatchAllowed);
        True(gate.IsDispatchAllowed,
            "two absent samples rearm after proven exposure");
        True(fallback.HasTrackedEpisode,
            "safe HP conditions open a distinct fallback episode");
        False(fallback.IsSpent,
            "new fallback generation is not stuck on the old spent latch");
        Equal(2L, fallback.Generation,
            "rearmed fallback has one new exact generation");
        var sameReadyEpisode = DarkKnightShadowbringerRules.ObserveFallback(
            fallback,
            exactFallbackEligibility: true);
        Equal(fallback.Generation, sameReadyEpisode.Generation,
            "later ready samples cannot create one-second recast spam");

        True(DarkKnightShadowbringerRules.TrySelectOpportunity(
                DarkKnightShadowbringerDarkArtsState.Initial,
                fallback,
                out var opportunity,
                out _,
                out _),
            "safe fallback becomes selectable again");
        Equal(DarkKnightShadowbringerOpportunityKind.SafeHpCost,
            opportunity, "rearmed opportunity is the HP-cost path");

        True(DarkKnightShadowbringerRules.TrySelectOpportunity(
                DarkArts(),
                fallback,
                out opportunity,
                out _,
                out _),
            "fresh Dark Arts and fallback can coexist");
        Equal(DarkKnightShadowbringerOpportunityKind.DarkArts,
            opportunity, "fresh Dark Arts retains first priority");

        var cadence = DarkKnightShadowbringerRules
            .MarkAutomaticCadenceBoundary(
                previousBoundaryAtMilliseconds: -1,
                ClientActionAttemptOutcome.ClientAccepted,
                nowMilliseconds: 20_000);
        False(DarkKnightShadowbringerRules.IsAutomaticCadenceReady(
                cadence,
                21_799),
            "shared cadence blocks both paths at 1799 ms");
        Equal(1L,
            DarkKnightShadowbringerRules
                .GetAutomaticCadenceRemainingMilliseconds(cadence, 21_799),
            "cadence diagnostics expose the final blocked millisecond");
        True(DarkKnightShadowbringerRules.IsAutomaticCadenceReady(
                cadence,
                21_800),
            "shared cadence opens both paths at exactly 1800 ms");

        var recurringFallback = DarkKnightShadowbringerRules
            .RetireFallbackAfterAutomaticBoundary(
                Fallback(),
                ClientActionAttemptOutcome.ClientAccepted);
        False(recurringFallback.HasTrackedEpisode,
            "accepted automatic boundary retires the prior safe cycle immediately");
        recurringFallback = DarkKnightShadowbringerRules.ObserveFallback(
            recurringFallback,
            exactFallbackEligibility:
                DarkKnightShadowbringerRules.IsAutomaticCadenceReady(
                    cadence,
                    21_799));
        Equal(1L, recurringFallback.Generation,
            "continuous safe conditions cannot rearm before cadence");
        recurringFallback = DarkKnightShadowbringerRules.ObserveFallback(
            recurringFallback,
            exactFallbackEligibility:
                DarkKnightShadowbringerRules.IsAutomaticCadenceReady(
                    cadence,
                    21_800));
        Equal(2L, recurringFallback.Generation,
            "continuous safe conditions open exactly one new cadence generation");
        var recurringSameFrame = DarkKnightShadowbringerRules.ObserveFallback(
            recurringFallback,
            exactFallbackEligibility: true);
        Equal(recurringFallback.Generation, recurringSameFrame.Generation,
            "open cadence cannot create a new fallback generation per frame");
        True(DarkKnightShadowbringerRules.TrySelectOpportunity(
                DarkArts(),
                recurringFallback,
                out opportunity,
                out _,
                out _),
            "both paths can become ready on the shared cadence boundary");
        Equal(DarkKnightShadowbringerOpportunityKind.DarkArts,
            opportunity,
            "Dark Arts wins the cross-path cadence boundary regardless of fallback thresholds");

        var rejectedCadence = DarkKnightShadowbringerRules
            .MarkAutomaticCadenceBoundary(
                cadence,
                ClientActionAttemptOutcome.ClientRejected,
                nowMilliseconds: 22_000);
        Equal(cadence, rejectedCadence,
            "explicit false does not consume the automatic cadence");
        var rejectedFallback = DarkKnightShadowbringerRules
            .RetireFallbackAfterAutomaticBoundary(
                recurringFallback,
                ClientActionAttemptOutcome.ClientRejected);
        Equal(recurringFallback, rejectedFallback,
            "explicit false retains its frozen fallback retry episode");
        var unknownCadence = DarkKnightShadowbringerRules
            .MarkAutomaticCadenceBoundary(
                cadence,
                ClientActionAttemptOutcome.AcceptanceUnknown,
                nowMilliseconds: 22_000);
        Equal(22_000L, unknownCadence,
            "unknown acceptance consumes the same global cadence");
    }

    public static void DarkArtsExposureDebouncesAndSpendsExactlyOnce()
    {
        var exposed = DarkKnightShadowbringerRules.ObserveDarkArts(
            DarkKnightShadowbringerDarkArtsState.Initial,
            exactDarkArtsExposure: true);
        Equal(1L, exposed.Generation, "first exact exposure generation");
        True(exposed.IsCurrentlyExposed, "first exposure active");

        var spent = DarkKnightShadowbringerRules.MarkDarkArtsSpent(
            exposed,
            exposed.Generation);
        True(spent.IsSpent, "exact exposure spent");
        var same = DarkKnightShadowbringerRules.ObserveDarkArts(
            spent,
            exactDarkArtsExposure: true);
        Equal(exposed.Generation, same.Generation,
            "continuously visible status cannot rearm");
        True(same.IsSpent, "spent latch remains visible");

        var oneMiss = DarkKnightShadowbringerRules.ObserveDarkArts(
            same,
            exactDarkArtsExposure: false);
        var flickerReturn = DarkKnightShadowbringerRules.ObserveDarkArts(
            oneMiss,
            exactDarkArtsExposure: true);
        Equal(exposed.Generation, flickerReturn.Generation,
            "one missing sample is only flicker");
        True(flickerReturn.IsSpent, "flicker cannot clear spent state");

        var absenceOne = DarkKnightShadowbringerRules.ObserveDarkArts(
            flickerReturn,
            exactDarkArtsExposure: false);
        var absenceTwo = DarkKnightShadowbringerRules.ObserveDarkArts(
            absenceOne,
            exactDarkArtsExposure: false);
        var next = DarkKnightShadowbringerRules.ObserveDarkArts(
            absenceTwo,
            exactDarkArtsExposure: true);
        Equal(exposed.Generation + 1, next.Generation,
            "two missing samples permit a distinct later proc");
        False(next.IsSpent, "new proc starts unspent");
    }

    public static void FallbackRequiresARealEligibilityTransition()
    {
        var eligible = DarkKnightShadowbringerRules.ObserveFallback(
            DarkKnightShadowbringerFallbackState.Initial,
            exactFallbackEligibility: true);
        Equal(1L, eligible.Generation, "first safe episode");
        var spent = DarkKnightShadowbringerRules.MarkFallbackSpent(
            eligible,
            eligible.Generation);
        var stillEligible = DarkKnightShadowbringerRules.ObserveFallback(
            spent,
            exactFallbackEligibility: true);
        Equal(eligible.Generation, stillEligible.Generation,
            "cooldown-ready frames cannot open another episode");
        True(stillEligible.IsSpent, "continuous safe level stays spent");

        var oneMiss = DarkKnightShadowbringerRules.ObserveFallback(
            stillEligible,
            exactFallbackEligibility: false);
        var flickerReturn = DarkKnightShadowbringerRules.ObserveFallback(
            oneMiss,
            exactFallbackEligibility: true);
        Equal(eligible.Generation, flickerReturn.Generation,
            "one unknown/ineligible sample cannot rearm");
        True(flickerReturn.IsSpent, "one-sample flicker stays spent");

        var absenceOne = DarkKnightShadowbringerRules.ObserveFallback(
            flickerReturn,
            exactFallbackEligibility: false);
        var absenceTwo = DarkKnightShadowbringerRules.ObserveFallback(
            absenceOne,
            exactFallbackEligibility: false);
        var next = DarkKnightShadowbringerRules.ObserveFallback(
            absenceTwo,
            exactFallbackEligibility: true);
        Equal(eligible.Generation + 1, next.Generation,
            "stable ineligibility permits a later safe episode");
        False(next.IsSpent, "later safe episode is unspent");
    }

    public static void DarkArtsAlwaysWinsAndActionIdentityIsExact()
    {
        var darkArts = DarkArts();
        var fallback = Fallback();
        False(Safe(10_000, 100_000, false, 5),
            "low HP and unknown high pressure reject only the HP-cost path");
        True(DarkKnightShadowbringerRules.TrySelectOpportunity(
                darkArts,
                DarkKnightShadowbringerFallbackState.Initial,
                out var darkArtsWithoutFallback,
                out _,
                out _),
            "Dark Arts ignores HP and pressure fallback configuration");
        Equal(DarkKnightShadowbringerOpportunityKind.DarkArts,
            darkArtsWithoutFallback,
            "free proc remains available under unsafe fallback conditions");
        True(DarkKnightShadowbringerRules.TrySelectOpportunity(
                darkArts,
                fallback,
                out var opportunity,
                out var generation,
                out var adjustedActionId),
            "at least one opportunity exists");
        Equal(DarkKnightShadowbringerOpportunityKind.DarkArts, opportunity,
            "free proc beats HP-cost fallback");
        Equal(darkArts.Generation, generation, "Dark Arts generation frozen");
        Equal(DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
            adjustedActionId, "Dark Arts adjusted action frozen");

        darkArts = DarkKnightShadowbringerRules.MarkDarkArtsSpent(
            darkArts,
            darkArts.Generation);
        True(DarkKnightShadowbringerRules.TrySelectOpportunity(
                darkArts,
                fallback,
                out opportunity,
                out generation,
                out adjustedActionId),
            "fallback remains after free proc is owned");
        Equal(DarkKnightShadowbringerOpportunityKind.SafeHpCost, opportunity,
            "safe fallback selected second");
        Equal(fallback.Generation, generation, "fallback generation frozen");
        Equal(DarkKnightShadowbringerRules.ShadowbringerActionId,
            adjustedActionId, "base action identity frozen");
    }

    public static void TwoPassPolicyKeepsFallbackAfterPlungeOnly()
    {
        var noDarkArts = DarkKnightShadowbringerRules.ObserveDarkArts(
            DarkKnightShadowbringerDarkArtsState.Initial,
            exactDarkArtsExposure: false);
        var fallback = Fallback();
        var safeObservation = Observation() with
        {
            DarkArts = noDarkArts,
            Fallback = fallback,
            ResolvedAdjustedActionId =
                DarkKnightShadowbringerRules.ShadowbringerActionId,
            DispatchPolicy =
                DarkKnightShadowbringerDispatchPolicy.DarkArtsOnly,
        };
        var prePlunge = DarkKnightShadowbringerRules.Observe(
            DarkKnightShadowbringerState.Initial,
            safeObservation);
        Equal(DarkKnightShadowbringerDecisionKind.None,
            prePlunge.Kind, "safe fallback cannot dispatch before Plunge");
        Equal(DarkKnightShadowbringerDecisionReason.OpportunityUnavailable,
            prePlunge.Reason, "Dark-Arts-only pass ignores fallback");

        var afterPlunge = DarkKnightShadowbringerRules.Observe(
            DarkKnightShadowbringerState.Initial,
            safeObservation with
            {
                DispatchPolicy =
                    DarkKnightShadowbringerDispatchPolicy.SafeHpCostOnly,
                NowMilliseconds = 1_001,
            });
        Dispatch(afterPlunge, "safe fallback after Plunge");
        Equal(DarkKnightShadowbringerOpportunityKind.SafeHpCost,
            afterPlunge.Intent!.Value.Opportunity,
            "second pass freezes HP-cost opportunity");

        var deferredNextFrame = DarkKnightShadowbringerRules.Observe(
            afterPlunge.NextState,
            safeObservation with
            {
                DispatchPolicy =
                    DarkKnightShadowbringerDispatchPolicy.DarkArtsOnly,
                NowMilliseconds = 1_002,
            });
        Equal(DarkKnightShadowbringerDecisionKind.Armed,
            deferredNextFrame.Kind,
            "buffered fallback remains armed in pre-Plunge pass");
        Equal(DarkKnightShadowbringerDecisionReason.DeferredBySchedulerPolicy,
            deferredNextFrame.Reason,
            "pre-Plunge pass explicitly policy-defers fallback");
        False(deferredNextFrame.InputClaimed,
            "policy-deferred fallback leaves frame to Plunge");

        var darkArtsSupersedes = DarkKnightShadowbringerRules.Observe(
            deferredNextFrame.NextState,
            Observation() with
            {
                DispatchPolicy =
                    DarkKnightShadowbringerDispatchPolicy.DarkArtsOnly,
                NowMilliseconds = 1_003,
            });
        Dispatch(darkArtsSupersedes,
            "new Dark Arts supersedes buffered fallback");
        Equal(DarkKnightShadowbringerOpportunityKind.DarkArts,
            darkArtsSupersedes.Intent!.Value.Opportunity,
            "free proc restores audited first DRK priority");
    }

    public static void CandidateRankingAndContextStayExact()
    {
        var candidates = new[]
        {
            Candidate(4, 0x2004, 0x204, 30, 100),
            Candidate(3, 0x2003, 0x203, 20, 100),
            Candidate(2, 0x2002, 0x202, 20, 100),
        };
        Equal(2,
            DarkKnightShadowbringerRules.SelectBestCandidateIndex(
                candidates,
                SupportedPvPContext.CrystallineConflict,
                Local),
            "lowest HP ratio then stable CC slot");

        Equal(-1,
            DarkKnightShadowbringerRules.SelectBestCandidateIndex(
                [
                    Candidate(1, 0x2001, 0x201, 20, 100),
                    Candidate(1, 0x2002, 0x202, 10, 100),
                ],
                SupportedPvPContext.CrystallineConflict,
                Local),
            "duplicate slot fails closed");
        Equal(-1,
            DarkKnightShadowbringerRules.SelectBestCandidateIndex(
                [
                    Candidate(1, 0x2001, 0x201, 20, 100),
                    Candidate(2, 0x2001, 0x201, 10, 100),
                ],
                SupportedPvPContext.CrystallineConflict,
                Local),
            "duplicate actor fails closed");
        Equal(-1,
            DarkKnightShadowbringerRules.SelectBestCandidateIndex(
                [Candidate(1, 0x2001, 0x201, 10, 100) with
                {
                    TargetGuardActive = true,
                }],
                SupportedPvPContext.CrystallineConflict,
                Local),
            "Guarded target is excluded");

        var den = Candidate(0, 0x3001, 0x301, 50, 100) with
        {
            Context = SupportedPvPContext.WolvesDen,
        };
        Equal(0,
            DarkKnightShadowbringerRules.SelectBestCandidateIndex(
                [den],
                SupportedPvPContext.WolvesDen,
                Local),
            "Wolves' Den accepts slot zero current-target carrier");
        False(DarkKnightShadowbringerRules.IsContextSlotValid(
                SupportedPvPContext.WolvesDen,
                1),
            "Wolves' Den never accepts synthetic S1/e1");
    }

    public static void HeldIntentFreezesAndDoesNotSubstituteTargets()
    {
        var observation = Observation();
        var initial = DarkKnightShadowbringerRules.Observe(
            DarkKnightShadowbringerState.Initial,
            observation);
        Dispatch(initial, "initial Dark Arts intent");
        var intent = initial.Intent!.Value;
        Equal(Target, intent.Target, "exact target frozen");

        var higherPriority = DarkKnightShadowbringerRules.Observe(
            initial.NextState,
            observation with
            {
                HigherPriorityClaimed = true,
                NowMilliseconds = 1_001,
            });
        Equal(DarkKnightShadowbringerDecisionReason.HigherPriorityClaimed,
            higherPriority.Reason, "higher action keeps exact intent armed");
        Equal(intent, higherPriority.NextState.Intent!.Value,
            "higher action cannot rerank target");

        var substitute = Candidate(2, 0x2002, 0x202, 1, 100);
        var targetDrift = DarkKnightShadowbringerRules.Observe(
            higherPriority.NextState,
            observation with
            {
                Candidate = substitute,
                NowMilliseconds = 1_002,
            });
        Equal(DarkKnightShadowbringerDecisionKind.Cancelled,
            targetDrift.Kind, "different low target terminalizes intent");
        True(targetDrift.SpendOpportunity,
            "target drift spends the frozen opportunity");

        var released = DarkKnightShadowbringerRules.Observe(
            initial.NextState,
            observation with
            {
                FrozenKeyStillDown = false,
                NowMilliseconds = 1_003,
            });
        Equal(DarkKnightShadowbringerDecisionReason.ExactKeyReleased,
            released.Reason, "key release cancels consent");
        False(released.SpendOpportunity,
            "key release does not destroy an unused proc");
    }

    public static void NativeBoundaryUsesSharedBoundedRetryPolicy()
    {
        var initial = DarkKnightShadowbringerRules.Observe(
            DarkKnightShadowbringerState.Initial,
            Observation());
        Dispatch(initial, "retry initial dispatch");
        var state = initial.NextState;
        for (var attempt = 1;
             attempt <= HeldActionRetryRules.MaximumNativeAttempts;
             attempt++)
        {
            var completion =
                DarkKnightShadowbringerRules.ApplyNativeAttemptOutcome(
                    state,
                    ClientActionAttemptOutcome.ClientRejected,
                    1_000 + attempt *
                    HeldActionRetryRules.NativeRetryThrottleMilliseconds);
            if (attempt < HeldActionRetryRules.MaximumNativeAttempts)
            {
                True(completion.RetryScheduled,
                    $"false attempt {attempt} schedules same intent");
                False(completion.SpendOpportunity,
                    "retry scheduling keeps owned lease");
                state = completion.NextState;
            }
            else
            {
                True(completion.Terminal, "eighth false is terminal");
                True(completion.SpendOpportunity,
                    "retry exhaustion spends opportunity");
                Equal(HeldActionRetryDisposition.RejectedTerminal,
                    completion.Disposition,
                    "shared rejected-terminal disposition");
            }
        }

        var soft = DarkKnightShadowbringerRules.ApplyNativeAttemptOutcome(
            initial.NextState,
            ClientActionAttemptOutcome.SoftUnavailable,
            2_000);
        True(soft.SoftWait, "cast/global boundary is a soft wait");
        False(soft.SpendOpportunity,
            "soft wait cannot spend the action opportunity");

        var ambiguous = DarkKnightShadowbringerRules.ApplyNativeAttemptOutcome(
            initial.NextState,
            ClientActionAttemptOutcome.AcceptanceUnknown,
            2_001);
        Equal(HeldActionRetryDisposition.AmbiguousTerminal,
            ambiguous.Disposition, "ambiguous acceptance is terminal");
        True(ambiguous.SpendOpportunity,
            "ambiguous boundary prevents duplicate action");
    }

    public static void FalseBoundaryRequiresStableAdjustedAndTargetState()
    {
        var ready = Fingerprint(
            DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
            sequence: 20);
        Equal(ClientActionAttemptOutcome.ClientRejected,
            DarkKnightShadowbringerRules.ClassifyBoundary(
                clientReturnedAccepted: false,
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                targetStatusBefore: 0,
                targetStatusAfter: 0,
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                ready,
                ready),
            "stable exact false is retryable");

        Equal(ClientActionAttemptOutcome.AcceptanceUnknown,
            DarkKnightShadowbringerRules.ClassifyBoundary(
                clientReturnedAccepted: false,
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                targetStatusBefore: 0,
                targetStatusAfter: 1,
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                ready,
                ready),
            "target action-status drift is ambiguous");
        Equal(ClientActionAttemptOutcome.AcceptanceUnknown,
            DarkKnightShadowbringerRules.ClassifyBoundary(
                clientReturnedAccepted: false,
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                targetStatusBefore: 0,
                targetStatusAfter: 0,
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                DarkKnightShadowbringerRules.ShadowbringerActionId,
                ready,
                ready),
            "Dark Arts consumption is ambiguous, never retryable");
        Equal(ClientActionAttemptOutcome.ClientAccepted,
            DarkKnightShadowbringerRules.ClassifyBoundary(
                clientReturnedAccepted: true,
                DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                uint.MaxValue,
                uint.MaxValue,
                0,
                0,
                default,
                default),
            "native true remains accepted");
    }

    private static bool Safe(
        uint currentHp,
        uint maximumHp,
        bool pressureKnown,
        int pressure) =>
        DarkKnightShadowbringerRules.IsSafeFallbackEligible(
            currentHp,
            maximumHp,
            pressureKnown,
            pressure,
            DarkKnightShadowbringerRules.DefaultMinimumHpPercent,
            DarkKnightShadowbringerRules.DefaultPressureLimitExclusive);

    private static DarkKnightShadowbringerDarkArtsState DarkArts() =>
        DarkKnightShadowbringerRules.ObserveDarkArts(
            DarkKnightShadowbringerDarkArtsState.Initial,
            exactDarkArtsExposure: true);

    private static DarkKnightShadowbringerFallbackState Fallback() =>
        DarkKnightShadowbringerRules.ObserveFallback(
            DarkKnightShadowbringerFallbackState.Initial,
            exactFallbackEligibility: true);

    private static DarkKnightShadowbringerObservation Observation() => new(
        ConfigurationEnabled: true,
        Context: SupportedPvPContext.CrystallineConflict,
        LocalPlayer: Local,
        IsLocalPlayerAlive: true,
        IsLocalPlayerTargetable: true,
        LocalJobId: DarkKnightShadowbringerRules.DarkKnightJobId,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        HeldGameplayKeyCode: HeldKey,
        FrozenKeyStillDown: true,
        DarkArts: DarkArts(),
        Fallback: Fallback(),
        DispatchPolicy: DarkKnightShadowbringerDispatchPolicy.Any,
        ResolvedAdjustedActionId:
            DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
        ActionLocallyReady: true,
        NativeBoundaryReady: true,
        Candidate: Candidate(3, Target.GameObjectId, Target.EntityId, 20, 100),
        HardReset: false,
        NowMilliseconds: 1_000);

    private static DarkKnightShadowbringerCandidate Candidate(
        int slot,
        ulong gameObjectId,
        uint entityId,
        uint currentHp,
        uint maximumHp) => new(
        SupportedPvPContext.CrystallineConflict,
        slot,
        new TargetPressureActorIdentity(gameObjectId, entityId),
        ExactCanonicalIdentity: true,
        Alive: true,
        Targetable: true,
        currentHp,
        maximumHp,
        TargetGuardActive: false,
        HasValidActionTarget: true,
        HasNativeRangeAndLineOfSight: true);

    private static ClientActionAttemptFingerprint Fingerprint(
        uint adjustedActionId,
        ushort sequence) => new(
        Captured: true,
        ActionQueued: false,
        QueuedActionType: 0,
        QueuedActionId: 0,
        QueuedTargetId: 0,
        QueuedExtraParam: 0,
        QueueMode: 0,
        QueuedComboRouteId: 0,
        LastUsedActionSequence: sequence,
        AnimationLockSeconds: 0,
        CastActionId: 0,
        AdjustedActionId: adjustedActionId,
        IsActionOffCooldown: true,
        ResourceStatus: 0);

    private static void Dispatch(
        DarkKnightShadowbringerDecision decision,
        string label)
    {
        Equal(DarkKnightShadowbringerDecisionKind.Dispatch,
            decision.Kind, label);
        Equal(DarkKnightShadowbringerDecisionReason.None,
            decision.Reason, $"{label} reason");
        True(decision.ShouldDispatch, $"{label} dispatch");
        True(decision.InputClaimed, $"{label} input claim");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(
            $"Expected true: {label}");
    }

    private static void False(bool condition, string label) =>
        True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{label}: expected {expected}, got {actual}");
        }
    }
}
