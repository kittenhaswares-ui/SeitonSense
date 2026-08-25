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
        Equal(3_034u, DarkKnightShadowbringerRules.DarkArtsStatusId,
            "Dark Arts status");
        Equal(12_000u, DarkKnightShadowbringerRules.ShadowbringerHpCost,
            "base HP cost");
        Equal(10, DarkKnightShadowbringerRules.MaximumRangeYalms, "range");
        Equal(0, DarkKnightShadowbringerRules.ExpectedRuntimeRecastGroupIndex,
            "runtime recast group");
        Equal(1_000,
            DarkKnightShadowbringerRules.ExpectedAdjustedRecastMilliseconds,
            "adjusted recast");
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
