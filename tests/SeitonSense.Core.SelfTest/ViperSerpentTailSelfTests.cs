using SeitonSense.Core;

internal static class ViperSerpentTailSelfTests
{
    private const int HeldKey = 65;
    private static readonly TargetPressureActorIdentity Local = new(0x1001, 0x101);
    private static readonly TargetPressureActorIdentity Target = new(0x2001, 0x201);

    public static void ExactCarrierFollowupsMappingsAndRangesArePinned()
    {
        Equal(39_183u, ViperSerpentTailRules.CarrierActionId, "carrier");
        Equal(541u, ViperSerpentTailRules.WolvesDenStrikingDummyNameId, "Wolves' Den dummy name ID");
        var expected = new Dictionary<uint, int>
        {
            [39_174] = 5,
            [39_175] = 5,
            [39_176] = 5,
            [39_177] = 20,
            [39_178] = 20,
            [39_179] = 5,
            [39_180] = 5,
            [39_181] = 5,
            [39_182] = 5,
        };
        foreach (var pair in expected)
        {
            True(ViperSerpentTailRules.IsExactFollowUpAction(pair.Key), $"follow-up {pair.Key}");
            Equal(pair.Value, ViperSerpentTailRules.GetMaximumRangeYalms(pair.Key), $"range {pair.Key}");
        }
        False(ViperSerpentTailRules.IsExactFollowUpAction(39_183), "carrier is never dispatched");
        False(ViperSerpentTailRules.IsExactFollowUpAction(39_173), "unknown action");

        var first = Exposure(ViperSerpentTailRules.DeathRattleActionId);
        True(first.IsValid && first.HasCurrentFollowUp, "carrier alone creates an exposure");
        Equal(1L, first.Generation, "first exposure generation");
        Equal(ViperSerpentTailRules.DeathRattleActionId, first.CurrentActionId,
            "current adjusted carrier action");
        var unchanged = ViperSerpentTailRules.ObserveCarrierExposure(
            first,
            ViperSerpentTailRules.DeathRattleActionId);
        Equal(first, unchanged, "same carrier observation keeps one generation");

        var different = ViperSerpentTailRules.ObserveCarrierExposure(
            unchanged,
            ViperSerpentTailRules.UncoiledTwinfangActionId);
        Equal(2L, different.Generation, "different exact action advances immediately");
        Equal(ViperSerpentTailRules.UncoiledTwinfangActionId, different.CurrentActionId,
            "different exact action is current");
        True(ViperSerpentTailRules.IsCurrentUnspentExposure(
            different,
            different.Generation,
            different.CurrentActionId), "current generation is directly usable");
        Equal(ViperSerpentTailExposureState.Initial,
            ViperSerpentTailRules.ObserveCarrierExposure(different, different.CurrentActionId, hardReset: true),
            "hard reset clears exposure state");
    }

    public static void ExposureSpendingIsExactAndBounded()
    {
        var exposed = Exposure();
        var wrongGeneration = ViperSerpentTailRules.MarkCarrierExposureSpent(
            exposed,
            exposed.Generation + 1,
            exposed.EpisodeActionId);
        Equal(exposed, wrongGeneration, "wrong generation cannot spend exposure");
        var wrongAction = ViperSerpentTailRules.MarkCarrierExposureSpent(
            exposed,
            exposed.Generation,
            ViperSerpentTailRules.UncoiledTwinbloodActionId);
        Equal(exposed, wrongAction, "wrong action cannot spend exposure");

        var spent = ViperSerpentTailRules.MarkCarrierExposureSpent(
            exposed,
            exposed.Generation,
            exposed.EpisodeActionId);
        True(spent.IsSpent, "exact accepted exposure is spent");
        var same = ViperSerpentTailRules.ObserveCarrierExposure(
            spent,
            spent.EpisodeActionId);
        Equal(spent.Generation, same.Generation, "same adjusted action cannot rearm");
        True(same.IsSpent, "spent latch survives same carrier action");

        var oneFlicker = ViperSerpentTailRules.ObserveCarrierExposure(
            same,
            ViperSerpentTailRules.CarrierActionId);
        Equal(1, oneFlicker.ConsecutiveNonFollowUpObservations, "first absence is only a flicker");
        False(oneFlicker.IsCurrentlyExposed, "flicker is not currently executable");
        True(oneFlicker.HasTrackedEpisode && oneFlicker.IsSpent,
            "flicker retains the spent episode");
        var returnedAfterFlicker = ViperSerpentTailRules.ObserveCarrierExposure(
            oneFlicker,
            exposed.EpisodeActionId);
        Equal(exposed.Generation, returnedAfterFlicker.Generation,
            "one flicker cannot create a generation");
        True(returnedAfterFlicker.IsSpent, "one flicker cannot clear spent state");

        var firstStableAbsence = ViperSerpentTailRules.ObserveCarrierExposure(
            returnedAfterFlicker,
            ViperSerpentTailRules.CarrierActionId);
        var stableReset = ViperSerpentTailRules.ObserveCarrierExposure(
            firstStableAbsence,
            ViperSerpentTailRules.CarrierActionId);
        False(stableReset.HasTrackedEpisode, "two absences reset the episode");
        Equal(2, stableReset.ConsecutiveNonFollowUpObservations, "stable reset count");
        var rearmed = ViperSerpentTailRules.ObserveCarrierExposure(
            stableReset,
            exposed.EpisodeActionId);
        Equal(exposed.Generation + 1, rearmed.Generation,
            "same action rearms only after stable reset");
        False(rearmed.IsSpent, "new generation starts unspent");
    }

    public static void InitialSafetyGatesAndPriorityFailClosed()
    {
        Gate(Observation() with { ConfigurationEnabled = false }, ViperSerpentTailDecisionReason.ConfigurationDisabled);
        Gate(Observation() with { Context = SupportedPvPContext.None }, ViperSerpentTailDecisionReason.OutsideSupportedPvPContext);
        Gate(Observation() with { LocalPlayer = default }, ViperSerpentTailDecisionReason.LocalPlayerIdentityInvalid);
        Gate(Observation() with { IsLocalPlayerAlive = false }, ViperSerpentTailDecisionReason.LocalPlayerDead);
        Gate(Observation() with { LocalJobId = 30 }, ViperSerpentTailDecisionReason.LocalJobInvalid);
        Gate(Observation() with { MetadataVerified = false }, ViperSerpentTailDecisionReason.MetadataUnverified);
        Gate(Observation() with { InputProbeSucceeded = false }, ViperSerpentTailDecisionReason.InputProbeUnavailable);
        Gate(Observation() with { IsTextInputActive = true }, ViperSerpentTailDecisionReason.TextInputActive);
        Gate(Observation() with
        {
            ActionHelpersSuppressedByGuard = true,
            HigherPriorityClaimed = true,
        }, ViperSerpentTailDecisionReason.HigherPriorityClaimed);
        Gate(Observation() with { ActionHelpersSuppressedByGuard = true },
            ViperSerpentTailDecisionReason.GuardSuppressed);
        Gate(Observation() with { HigherPriorityClaimed = true },
            ViperSerpentTailDecisionReason.HigherPriorityClaimed);
        Gate(Observation(ViperSerpentTailExposureState.Initial),
            ViperSerpentTailDecisionReason.CarrierUnavailable);
        var spent = ViperSerpentTailRules.MarkCarrierExposureSpent(
            Exposure(), 1, ViperSerpentTailRules.UncoiledTwinfangActionId);
        Gate(Observation(spent), ViperSerpentTailDecisionReason.ExposureSpent);
        Gate(Observation() with { Candidate = null }, ViperSerpentTailDecisionReason.CandidateUnavailable);
        Gate(Observation() with
        {
            Candidate = Candidate() with { ExactCanonicalIdentity = false },
        }, ViperSerpentTailDecisionReason.CandidateInvalid);
        Gate(Observation() with { HeldGameplayKeyEligible = false },
            ViperSerpentTailDecisionReason.NoHeldGameplayKey);
        Gate(Observation() with { HardReset = true }, ViperSerpentTailDecisionReason.HardReset,
            ViperSerpentTailDecisionKind.Cancelled);
    }

    public static void ExactIntentFreezesActionTargetContextAndKey()
    {
        // A hold that already exists when the proc appears is sufficient; no
        // preceding action or accepted-action token exists in this API.
        var exposure = Exposure();
        var decision = Observe(Observation(exposure));
        Dispatch(decision, "carrier exposure with pre-existing hold");
        var intent = decision.Intent ?? throw new InvalidOperationException("missing intent");
        Equal(exposure.Generation, intent.ExposureGeneration, "generation frozen");
        Equal(ViperSerpentTailRules.UncoiledTwinfangActionId, intent.ActionId, "action frozen");
        Equal(Target, intent.Target, "target frozen");
        Equal(3, intent.EnemySlot, "slot frozen");
        Equal(HeldKey, intent.FrozenKeyCode, "key frozen");

        var candidate = Candidate();
        True(CanUse(intent, exposure, candidate), "exact final intent");
        False(CanUse(intent, exposure, candidate, context: SupportedPvPContext.WolvesDen), "context drift");
        False(CanUse(intent, exposure, candidate with { Actor = new(0x2002, 0x202) }), "target drift");
        False(CanUse(intent, exposure, candidate with { HasNativeRangeAndLineOfSight = false }), "range drift");
        False(CanUse(intent, exposure, candidate, currentHeldKeyCode: 66), "key substitution");
        False(CanUse(intent, exposure, candidate, guardSuppressed: true), "own Guard suppresses");
        False(CanUse(intent, exposure, candidate, higherPriorityClaimed: true), "Purify remains first");
        var newer = ViperSerpentTailRules.ObserveCarrierExposure(
            exposure,
            ViperSerpentTailRules.UncoiledTwinbloodActionId);
        False(CanUse(intent, newer, candidate), "new carrier generation invalidates frozen intent");

        // A proc may appear before consent. The same exposure remains available
        // and dispatches as soon as any eligible gameplay key, including WASD,
        // becomes held.
        var withoutKey = Observe(Observation(exposure) with
        {
            HeldGameplayKeyEligible = false,
            HeldGameplayKeyCode = 0,
        });
        Equal(ViperSerpentTailDecisionReason.NoHeldGameplayKey, withoutKey.Reason,
            "proc waits without held consent");
        var keyArrived = Observe(Observation(exposure) with { NowMilliseconds = 1_001 });
        Dispatch(keyArrived, "proc before key dispatches when hold arrives");

        var rangeWait = Observe(Observation(exposure) with
        {
            Candidate = Candidate() with { HasNativeRangeAndLineOfSight = false },
        });
        Equal(ViperSerpentTailDecisionKind.Armed, rangeWait.Kind, "range wait freezes intent");
        Equal(ViperSerpentTailDecisionReason.TargetNotReady, rangeWait.Reason, "range wait reason");
        Equal(Target, rangeWait.Intent!.Value.Target, "range wait target stays frozen");

        // Wolves' Den replaces the CC e1-e5 identity with the exact current
        // native hard target (<t>). Its synthetic slot is always zero; the
        // runtime independently proves that actor as the duel opponent (or the
        // optional verified dummy) before this Core boundary is reached.
        var wolvesCandidate = Candidate() with
        {
            Context = SupportedPvPContext.WolvesDen,
            EnemySlot = 0,
        };
        var wolvesDecision = Observe(Observation(exposure) with
        {
            Context = SupportedPvPContext.WolvesDen,
            Candidate = wolvesCandidate,
        });
        Dispatch(wolvesDecision, "Wolves' Den exact current <t> uses slot zero");
        var wolvesIntent = wolvesDecision.Intent ??
                           throw new InvalidOperationException("missing Wolves' Den intent");
        Equal(0, wolvesIntent.EnemySlot, "Wolves' Den never invents an e-slot");
        Equal(Target, wolvesIntent.Target, "Wolves' Den freezes exact <t> identity");
        True(CanUse(
                wolvesIntent,
                exposure,
                wolvesCandidate,
                context: SupportedPvPContext.WolvesDen),
            "Wolves' Den exact <t> remains usable without an e-slot");
        False(CanUse(
                wolvesIntent,
                exposure,
                wolvesCandidate with { EnemySlot = 1 },
                context: SupportedPvPContext.WolvesDen),
            "Wolves' Den rejects an invented e1 identity");
    }

    public static void KnownWaitsAreFreeAndCleanFalseRetriesAreBounded()
    {
        var exposure = Exposure();
        var actionWait = Observe(Observation(exposure) with { ActionLocallyReady = false });
        Equal(ViperSerpentTailDecisionKind.Armed, actionWait.Kind, "action wait is buffered");
        False(actionWait.InputClaimed, "action wait yields lower helpers");
        var actionReady = ViperSerpentTailRules.Observe(
            actionWait.NextState,
            Observation(exposure) with { NowMilliseconds = 1_001 });
        Dispatch(actionReady, "action readiness resumes same frozen intent");

        var armed = Observe(Observation(exposure) with { NativeBoundaryReady = false });
        Equal(ViperSerpentTailDecisionKind.Armed, armed.Kind, "boundary wait is armed");
        True(armed.InputClaimed, "native boundary wait reserves job-priority frame");
        var firstFlicker = ViperSerpentTailRules.ObserveCarrierExposure(
            exposure,
            ViperSerpentTailRules.CarrierActionId);
        var flickerWait = ViperSerpentTailRules.Observe(
            armed.NextState,
            Observation(firstFlicker) with { NowMilliseconds = 1_001 });
        Equal(ViperSerpentTailDecisionKind.Armed, flickerWait.Kind,
            "one carrier flicker retains buffered intent");
        Equal(ViperSerpentTailDecisionReason.CarrierUnavailable, flickerWait.Reason,
            "flicker waits without dispatch");
        var restored = ViperSerpentTailRules.ObserveCarrierExposure(
            firstFlicker,
            exposure.EpisodeActionId);
        var afterFlicker = ViperSerpentTailRules.Observe(
            flickerWait.NextState,
            Observation(restored) with { NowMilliseconds = 1_002 });
        Dispatch(afterFlicker, "one-frame carrier flicker resumes same generation");

        var priorityWait = ViperSerpentTailRules.Observe(
            armed.NextState,
            Observation(exposure) with
            {
                HigherPriorityClaimed = true,
                NowMilliseconds = 1_001,
            });
        Equal(ViperSerpentTailPhase.Buffered, priorityWait.NextState.Phase,
            "Purify wait preserves buffered Viper intent");
        False(priorityWait.InputClaimed, "Purify owns its frame");

        var soft = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
            armed.NextState, ClientActionAttemptOutcome.SoftUnavailable, 1_000);
        True(soft.SoftWait, "known native wait is free");
        False(soft.SpendExposure, "soft wait does not spend proc");
        Equal(0, soft.NextState.Retry.NativeAttemptCount, "soft wait spends no retry");

        // There is deliberately no wall-clock proc expiry. The carrier itself
        // remains the authoritative lease.
        var muchLater = Observe(Observation(exposure) with { NowMilliseconds = 1_000_000 });
        Dispatch(muchLater, "current carrier exposure has no invented expiry");

        var state = Observe(Observation(exposure)).NextState;
        for (var attempt = 1; attempt <= HeldActionRetryRules.MaximumNativeAttempts; attempt++)
        {
            var now = 1_000L + ((attempt - 1) * HeldActionRetryRules.NativeRetryThrottleMilliseconds);
            var completion = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
                state, ClientActionAttemptOutcome.ClientRejected, now);
            state = completion.NextState;
            if (attempt < HeldActionRetryRules.MaximumNativeAttempts)
            {
                True(completion.RetryScheduled, $"retry {attempt}");
                False(completion.SpendExposure, "scheduled retry keeps proc unspent");
                var next = ViperSerpentTailRules.Observe(
                    state,
                    Observation(exposure) with { NowMilliseconds = now + 50 });
                Dispatch(next, $"retry dispatch {attempt}");
                state = next.NextState;
            }
            else
            {
                True(completion.Terminal, "eighth false is terminal");
                True(completion.SpendExposure, "retry exhaustion spends exact proc");
                Equal(HeldActionRetryDisposition.RejectedTerminal,
                    completion.Disposition, "retry exhaustion disposition preserved");
                Equal(ViperSerpentTailState.Initial, completion.NextState, "terminal state clears");
            }
        }

        var accepted = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
            Observe(Observation(exposure)).NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_000);
        True(accepted.ClientAccepted && accepted.SpendExposure,
            "accepted action tells caller to spend exact exposure");

        var ambiguous = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
            Observe(Observation(exposure)).NextState,
            ClientActionAttemptOutcome.AcceptanceUnknown,
            1_000);
        Equal(HeldActionRetryDisposition.AmbiguousTerminal,
            ambiguous.Disposition, "ambiguous disposition preserved");
        True(ambiguous.SpendExposure, "ambiguous terminal spends exposure safely");

        var stable = Fingerprint(sequence: 20);
        Equal(ClientActionAttemptOutcome.ClientRejected,
            ViperSerpentTailRules.ClassifyFollowUpBoundary(
                false,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                targetStatusBefore: 0,
                targetStatusAfter: 0,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                stable,
                stable),
            "false with stable full and target-aware readiness is retryable");
        Equal(ClientActionAttemptOutcome.AcceptanceUnknown,
            ViperSerpentTailRules.ClassifyFollowUpBoundary(
                false,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                targetStatusBefore: 0,
                targetStatusAfter: 1,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                stable,
                stable),
            "false after target-aware readiness drift is terminal ambiguous");
        Equal(ClientActionAttemptOutcome.AcceptanceUnknown,
            ViperSerpentTailRules.ClassifyFollowUpBoundary(
                false,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                targetStatusBefore: 0,
                targetStatusAfter: 0,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                ViperSerpentTailRules.UncoiledTwinbloodActionId,
                stable,
                stable),
            "false after carrier drift is terminal ambiguous");
    }

    public static void ContinuousHoldUsesDistinctCarrierExposuresOnly()
    {
        var exposure = Exposure();
        var first = Observe(Observation(exposure));
        Dispatch(first, "initial direct carrier exposure");
        var intent = first.Intent!.Value;
        var accepted = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
            first.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_000);
        True(accepted.SpendExposure, "accepted completion requests spent latch");
        var spent = ViperSerpentTailRules.MarkCarrierExposureSpent(
            exposure,
            intent.ExposureGeneration,
            intent.ActionId);

        var same = ViperSerpentTailRules.ObserveCarrierExposure(spent, intent.ActionId);
        var duplicate = Observe(Observation(same) with { NowMilliseconds = 1_001 });
        False(duplicate.ShouldDispatch, "same adjusted action cannot duplicate after success");
        Equal(ViperSerpentTailDecisionReason.ExposureSpent, duplicate.Reason,
            "spent exposure explains duplicate suppression");

        var flicker = ViperSerpentTailRules.ObserveCarrierExposure(
            same,
            ViperSerpentTailRules.CarrierActionId);
        var returned = ViperSerpentTailRules.ObserveCarrierExposure(flicker, intent.ActionId);
        Equal(intent.ExposureGeneration, returned.Generation,
            "one false carrier sample does not rearm");
        True(returned.IsSpent, "spent latch survives one false sample");

        var absenceOne = ViperSerpentTailRules.ObserveCarrierExposure(
            returned,
            ViperSerpentTailRules.CarrierActionId);
        var absenceTwo = ViperSerpentTailRules.ObserveCarrierExposure(
            absenceOne,
            ViperSerpentTailRules.CarrierActionId);
        var sameNewProc = ViperSerpentTailRules.ObserveCarrierExposure(absenceTwo, intent.ActionId);
        Equal(intent.ExposureGeneration + 1, sameNewProc.Generation,
            "stable absence allows same action as distinct proc");
        Dispatch(Observe(Observation(sameNewProc) with { NowMilliseconds = 1_002 }),
            "same ID after stable reset is a new episode");

        var acceptedTwinfang = Exposure(ViperSerpentTailRules.UncoiledTwinfangActionId);
        var acceptedTwinfangDecision = Observe(Observation(acceptedTwinfang));
        var acceptedTwinfangIntent = acceptedTwinfangDecision.Intent!.Value;
        var acceptedTwinfangCompletion = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
            acceptedTwinfangDecision.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_003);
        False(HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                acceptedTwinfangCompletion.Disposition),
            "accepted 39177 keeps continuous hold eligible");
        var spentTwinfang = ViperSerpentTailRules.MarkCarrierExposureSpent(
            acceptedTwinfang,
            acceptedTwinfangIntent.ExposureGeneration,
            acceptedTwinfangIntent.ActionId);
        var exposedTwinblood = ViperSerpentTailRules.ObserveCarrierExposure(
            spentTwinfang,
            ViperSerpentTailRules.UncoiledTwinbloodActionId);
        var continuousTwinblood = Observe(
            Observation(exposedTwinblood) with { NowMilliseconds = 1_004 });
        Dispatch(continuousTwinblood,
            "accepted 39177 flows to 39178 under the same continuous hold");
        Equal(HeldKey, continuousTwinblood.Intent!.Value.FrozenKeyCode,
            "39178 reuses the still-eligible held generation");

        var twinfang = Exposure(ViperSerpentTailRules.UncoiledTwinfangActionId);
        var bufferedTwinfang = Observe(Observation(twinfang) with { NativeBoundaryReady = false });
        Equal(ViperSerpentTailDecisionKind.Armed, bufferedTwinfang.Kind,
            "39177 waits at native boundary");
        var twinblood = ViperSerpentTailRules.ObserveCarrierExposure(
            twinfang,
            ViperSerpentTailRules.UncoiledTwinbloodActionId);
        Equal(twinfang.Generation + 1, twinblood.Generation,
            "39177 to 39178 advances generation immediately");
        var immediateReplacement = ViperSerpentTailRules.Observe(
            bufferedTwinfang.NextState,
            Observation(twinblood) with { NowMilliseconds = 1_001 });
        Dispatch(immediateReplacement, "39178 replaces buffered 39177 in the same observation");
        Equal(ViperSerpentTailRules.UncoiledTwinbloodActionId,
            immediateReplacement.Intent!.Value.ActionId,
            "replacement action frozen");
        Equal(twinblood.Generation,
            immediateReplacement.Intent.Value.ExposureGeneration,
            "replacement generation frozen");
    }

    private static ViperSerpentTailExposureState Exposure(
        uint actionId = ViperSerpentTailRules.UncoiledTwinfangActionId) =>
        ViperSerpentTailRules.ObserveCarrierExposure(
            ViperSerpentTailExposureState.Initial,
            actionId);

    private static ViperSerpentTailObservation Observation(
        ViperSerpentTailExposureState? exposure = null) => new(
        ConfigurationEnabled: true,
        Context: SupportedPvPContext.CrystallineConflict,
        LocalPlayer: Local,
        IsLocalPlayerAlive: true,
        LocalJobId: ViperSerpentTailRules.ViperJobId,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        HeldGameplayKeyCode: HeldKey,
        FrozenKeyStillDown: true,
        Exposure: exposure ?? Exposure(),
        ActionLocallyReady: true,
        NativeBoundaryReady: true,
        Candidate: Candidate(),
        HardReset: false,
        NowMilliseconds: 1_000);

    private static ViperSerpentTailCandidate Candidate() => new(
        SupportedPvPContext.CrystallineConflict,
        3,
        Target,
        ExactCanonicalIdentity: true,
        Alive: true,
        Targetable: true,
        HasValidActionTarget: true,
        HasNativeRangeAndLineOfSight: true);

    private static ViperSerpentTailDecision Observe(ViperSerpentTailObservation observation) =>
        ViperSerpentTailRules.Observe(ViperSerpentTailState.Initial, observation);

    private static bool CanUse(
        ViperSerpentTailIntent intent,
        ViperSerpentTailExposureState exposure,
        ViperSerpentTailCandidate candidate,
        SupportedPvPContext context = SupportedPvPContext.CrystallineConflict,
        int currentHeldKeyCode = HeldKey,
        bool guardSuppressed = false,
        bool higherPriorityClaimed = false) =>
        ViperSerpentTailRules.CanUseFrozenIntent(
            intent,
            configurationEnabled: true,
            context,
            Local,
            localAlive: true,
            ViperSerpentTailRules.ViperJobId,
            metadataVerified: true,
            guardSuppressed,
            higherPriorityClaimed,
            exposure,
            actionLocallyReady: true,
            currentHeldKeyCode,
            frozenKeyStillDown: true,
            candidate);

    private static ClientActionAttemptFingerprint Fingerprint(ushort sequence) => new(
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
        AdjustedActionId: ViperSerpentTailRules.UncoiledTwinfangActionId,
        IsActionOffCooldown: true,
        ResourceStatus: 0);

    private static void Gate(
        ViperSerpentTailObservation observation,
        ViperSerpentTailDecisionReason reason,
        ViperSerpentTailDecisionKind kind = ViperSerpentTailDecisionKind.None)
    {
        var decision = Observe(observation);
        Equal(kind, decision.Kind, reason.ToString());
        Equal(reason, decision.Reason, $"{reason} reason");
        False(decision.ShouldDispatch, $"{reason} dispatch");
    }

    private static void Dispatch(ViperSerpentTailDecision decision, string label)
    {
        Equal(ViperSerpentTailDecisionKind.Dispatch, decision.Kind, label);
        Equal(ViperSerpentTailDecisionReason.None, decision.Reason, $"{label} reason");
        True(decision.ShouldDispatch, $"{label} dispatch");
        True(decision.InputClaimed, $"{label} claim");
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
