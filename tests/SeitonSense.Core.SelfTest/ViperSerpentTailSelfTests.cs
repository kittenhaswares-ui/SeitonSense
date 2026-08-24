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

        var mappings = new Dictionary<uint, uint>
        {
            [39_161] = 39_174,
            [39_163] = 39_174,
            [39_166] = 39_175,
            [39_167] = 39_176,
            [39_168] = 39_177,
            [39_177] = 39_178,
            [39_169] = 39_179,
            [39_170] = 39_180,
            [39_171] = 39_181,
            [39_172] = 39_182,
        };
        foreach (var pair in mappings)
        {
            True(ViperSerpentTailRules.TryGetExpectedFollowUp(pair.Key, out var action),
                $"trigger {pair.Key}");
            Equal(pair.Value, action, $"mapping {pair.Key}");
        }
        False(ViperSerpentTailRules.TryGetExpectedFollowUp(1, out _), "unknown trigger");

        Equal(ViperSerpentTailTriggerInvocationKind.Direct,
            ViperSerpentTailRules.ClassifyTriggerInvocationMode(
                ViperSerpentTailRules.DirectUseActionMode, false),
            "None is a direct invocation");
        Equal(ViperSerpentTailTriggerInvocationKind.Direct,
            ViperSerpentTailRules.ClassifyTriggerInvocationMode(
                ViperSerpentTailRules.MacroUseActionMode, false),
            "Macro is a direct invocation");
        Equal(ViperSerpentTailTriggerInvocationKind.Direct,
            ViperSerpentTailRules.ClassifyTriggerInvocationMode(
                ViperSerpentTailRules.ComboUseActionMode, false),
            "single-button Combo is a direct invocation");
        Equal(ViperSerpentTailTriggerInvocationKind.Direct,
            ViperSerpentTailRules.ClassifyTriggerInvocationMode(
                ViperSerpentTailRules.LegacyMacroUseActionMode, false),
            "legacy raw 100 is a direct invocation");
        Equal(ViperSerpentTailTriggerInvocationKind.Unsupported,
            ViperSerpentTailRules.ClassifyTriggerInvocationMode(
                ViperSerpentTailRules.QueueUseActionMode, false),
            "Queue without exact native provenance is unsupported");
        Equal(ViperSerpentTailTriggerInvocationKind.ProvenNativeQueueDrain,
            ViperSerpentTailRules.ClassifyTriggerInvocationMode(
                ViperSerpentTailRules.QueueUseActionMode, true),
            "Queue with exact native provenance is a proven drain");
        Equal(ViperSerpentTailTriggerInvocationKind.Unsupported,
            ViperSerpentTailRules.ClassifyTriggerInvocationMode(4, true),
            "unknown execution mode stays closed");

        var boundaryBefore = Fingerprint(sequence: 10);
        var executedAfter = Fingerprint(sequence: 11);
        Equal(ViperSerpentTailTriggerPromotionDisposition.ExecutedAccepted,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                ViperSerpentTailTriggerInvocationKind.Direct,
                clientReturnedAccepted: true,
                boundaryBefore,
                executedAfter),
            "direct normal hotbar execution promotes");
        var comboInvocation = ViperSerpentTailRules.ClassifyTriggerInvocationMode(
            ViperSerpentTailRules.ComboUseActionMode,
            exactNativeQueueDrainProvenance: false);
        Equal(ViperSerpentTailTriggerPromotionDisposition.ExecutedAccepted,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                comboInvocation,
                clientReturnedAccepted: true,
                boundaryBefore,
                executedAfter),
            "direct single-button Combo execution promotes");
        Equal(ViperSerpentTailTriggerPromotionDisposition.NativeQueueOwned,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                comboInvocation,
                clientReturnedAccepted: true,
                boundaryBefore,
                executedAfter with { ActionQueued = true }),
            "Combo call that initially queues never promotes");
        Equal(ViperSerpentTailTriggerPromotionDisposition.NativeQueueOwned,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                comboInvocation,
                clientReturnedAccepted: true,
                Fingerprint(sequence: 10, actionQueued: true),
                executedAfter),
            "Combo cannot bypass a queue already owned before the call");
        Equal(ViperSerpentTailTriggerPromotionDisposition.NativeQueueOwned,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                ViperSerpentTailTriggerInvocationKind.Direct,
                clientReturnedAccepted: true,
                boundaryBefore,
                executedAfter with { ActionQueued = true }),
            "initial direct true that queues never promotes");
        Equal(ViperSerpentTailTriggerPromotionDisposition.UnsupportedInvocationMode,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                ViperSerpentTailTriggerInvocationKind.Unsupported,
                clientReturnedAccepted: true,
                boundaryBefore,
                executedAfter),
            "arbitrary unsupported invocation never promotes");
        Equal(ViperSerpentTailTriggerPromotionDisposition.AcceptanceUnknown,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                ViperSerpentTailTriggerInvocationKind.Direct,
                clientReturnedAccepted: true,
                boundaryBefore,
                boundaryBefore),
            "true without an executed sequence transition never promotes");
        Equal(ViperSerpentTailTriggerPromotionDisposition.AcceptanceUnknown,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                ViperSerpentTailTriggerInvocationKind.Direct,
                clientReturnedAccepted: true,
                boundaryBefore,
                Fingerprint(sequence: 0)),
            "zero post-call sequence is never execution proof");

        var queuedBefore = Fingerprint(
            sequence: 10,
            actionQueued: true,
            queuedActionId: 39_168);
        True(ViperSerpentTailRules.HasExactNativeQueueDrainProvenance(
            isQueueInvocation: true,
            actionTypeSupported: true,
            incomingActionType: 1,
            incomingResolvedActionId: 39_168,
            incomingEffectiveTargetId: Target.GameObjectId,
            incomingExtraParam: 0,
            incomingComboRouteId: 0,
            queuedResolvedActionId: 39_168,
            Target,
            queuedBefore), "exact native Queue drain provenance");
        Equal(ViperSerpentTailTriggerPromotionDisposition.ExecutedAccepted,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                ViperSerpentTailTriggerInvocationKind.ProvenNativeQueueDrain,
                clientReturnedAccepted: true,
                queuedBefore,
                executedAfter),
            "proven native Queue drain promotes after queue clears");
        Equal(ViperSerpentTailTriggerPromotionDisposition.NativeQueueOwned,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                ViperSerpentTailTriggerInvocationKind.ProvenNativeQueueDrain,
                clientReturnedAccepted: true,
                queuedBefore,
                executedAfter with { ActionQueued = true }),
            "Queue drain that remains queued never promotes");
        Equal(ViperSerpentTailTriggerPromotionDisposition.ClientRejected,
            ViperSerpentTailRules.ClassifyAcceptedTriggerBoundary(
                ViperSerpentTailTriggerInvocationKind.ProvenNativeQueueDrain,
                clientReturnedAccepted: false,
                queuedBefore,
                executedAfter),
            "rejected Queue drain never promotes");
        False(ViperSerpentTailRules.HasExactNativeQueueDrainProvenance(
            false, true, 1, 39_168, Target.GameObjectId, 0, 0, 39_168, Target,
            queuedBefore), "non-Queue call cannot claim Queue provenance");
        False(ViperSerpentTailRules.HasExactNativeQueueDrainProvenance(
            true, true, 1, 39_168, Target.GameObjectId, 0, 0, 39_168, Target,
            boundaryBefore), "arbitrary Queue call without native queued state fails closed");
        False(ViperSerpentTailRules.HasExactNativeQueueDrainProvenance(
            true, true, 1, 39_168, Target.GameObjectId, 0, 0, 39_168, Target,
            queuedBefore with { QueuedActionType = 2 }),
            "Queue action type drift fails closed");
        False(ViperSerpentTailRules.HasExactNativeQueueDrainProvenance(
            true, true, 1, 39_168, Target.GameObjectId, 0, 0, 39_169, Target,
            queuedBefore), "Queue action ID drift fails closed");
        False(ViperSerpentTailRules.HasExactNativeQueueDrainProvenance(
            true, true, 1, 39_168, Target.GameObjectId, 0, 0, 39_168, Target,
            queuedBefore with { QueuedTargetId = 0xDEAD }),
            "Queue target drift fails closed");
        False(ViperSerpentTailRules.HasExactNativeQueueDrainProvenance(
            true, true, 1, 39_168, Target.GameObjectId, 0, 0, 39_168, Target,
            queuedBefore with { QueuedExtraParam = 1 }),
            "Queue extra parameter drift fails closed");
        False(ViperSerpentTailRules.HasExactNativeQueueDrainProvenance(
            true, true, 1, 39_168, Target.GameObjectId, 0, 0, 39_168, Target,
            queuedBefore with { QueuedComboRouteId = 1 }),
            "Queue combo route drift fails closed");
    }

    public static void AcceptedTriggerIsExactAndBounded()
    {
        True(ViperSerpentTailRules.TryCreateAcceptedTrigger(
            1, 1032, SupportedPvPContext.CrystallineConflict, Local, 39_168, 3, Target, 1_000,
            out var trigger), "exact accepted trigger");
        Equal(39_177u, trigger.ExpectedAdjustedActionId, "expected adjusted action");
        True(ViperSerpentTailRules.IsTriggerCurrent(
            trigger, 1_000, 1032, SupportedPvPContext.CrystallineConflict, Local), "inclusive start");
        True(ViperSerpentTailRules.IsTriggerCurrent(
            trigger, 5_999, 1032, SupportedPvPContext.CrystallineConflict, Local), "inside lifetime");
        False(ViperSerpentTailRules.IsTriggerCurrent(
            trigger, 6_000, 1032, SupportedPvPContext.CrystallineConflict, Local), "exclusive expiry");
        False(ViperSerpentTailRules.IsTriggerCurrent(
            trigger, 1_001, 250, SupportedPvPContext.CrystallineConflict, Local), "territory drift");
        False(ViperSerpentTailRules.IsTriggerCurrent(
            trigger, 1_001, 1032, SupportedPvPContext.WolvesDen, Local), "context drift");

        True(ViperSerpentTailRules.TryCreateAcceptedTrigger(
            2, 250, SupportedPvPContext.WolvesDen, Local, 39_168, 0, Target, 1_000,
            out _), "Den uses explicit no-slot target");
        False(ViperSerpentTailRules.TryCreateAcceptedTrigger(
            3, 250, SupportedPvPContext.WolvesDen, Local, 39_168, 1, Target, 1_000,
            out _), "Den never invents S1");
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
        Gate(Observation() with { ActionHelpersSuppressedByGuard = true }, ViperSerpentTailDecisionReason.GuardSuppressed);
        Gate(Observation() with { HigherPriorityClaimed = true }, ViperSerpentTailDecisionReason.HigherPriorityClaimed);
        Gate(Observation() with { Trigger = null }, ViperSerpentTailDecisionReason.TriggerUnavailable);
        Gate(Observation() with { CurrentAcceptedActionEpoch = 2 },
            ViperSerpentTailDecisionReason.TriggerSuperseded);
        Gate(Observation() with { ResolvedAdjustedActionId = 39_178 }, ViperSerpentTailDecisionReason.AdjustedActionUnavailable);
        Gate(Observation() with { Candidate = null }, ViperSerpentTailDecisionReason.CandidateUnavailable);
        Gate(Observation() with { HeldGameplayKeyEligible = false }, ViperSerpentTailDecisionReason.NoHeldGameplayKey);
        Gate(Observation() with { HardReset = true }, ViperSerpentTailDecisionReason.HardReset,
            ViperSerpentTailDecisionKind.Cancelled);
    }

    public static void ExactIntentFreezesActionTargetContextAndKey()
    {
        var decision = Observe(Observation());
        Dispatch(decision, "initial follow-up");
        True(decision.ConsumeTrigger, "trigger is consumed when intent freezes");
        var intent = decision.Intent ?? throw new InvalidOperationException("missing intent");
        Equal(39_177u, intent.ActionId, "action frozen");
        Equal(Target, intent.Target, "target frozen");
        Equal(3, intent.EnemySlot, "slot frozen");
        Equal(5_900L, intent.ExpiresAtMilliseconds, "trigger expiry frozen");
        Equal(HeldKey, intent.FrozenKeyCode, "key frozen");

        var candidate = Candidate();
        True(CanUse(intent, candidate), "exact final intent");
        False(CanUse(intent, candidate, context: SupportedPvPContext.WolvesDen), "context drift");
        False(CanUse(intent, candidate, adjustedActionId: 39_178), "action drift");
        False(CanUse(intent, candidate with { Actor = new(0x2002, 0x202) }), "target drift");
        False(CanUse(intent, candidate with { HasNativeRangeAndLineOfSight = false }), "range drift");
        False(CanUse(intent, candidate, currentHeldKeyCode: 66), "key substitution");
        False(CanUse(intent, candidate, nowMilliseconds: 5_900), "expired intent");
        False(CanUse(intent, candidate, guardSuppressed: true), "own Guard suppresses");
        False(CanUse(intent, candidate, higherPriorityClaimed: true), "Purify remains first");
        False(CanUse(intent, candidate, currentAcceptedActionEpoch: 2),
            "newer accepted action epoch invalidates frozen intent");
    }

    public static void KnownWaitsAreFreeAndCleanFalseRetriesAreBounded()
    {
        var actionWait = Observe(Observation() with { ActionLocallyReady = false });
        Equal(ViperSerpentTailDecisionKind.Armed, actionWait.Kind, "action wait is buffered");
        False(actionWait.InputClaimed, "action wait yields lower helpers");
        True(actionWait.ConsumeTrigger, "action wait still freezes the exact trigger");

        var armed = Observe(Observation() with { NativeBoundaryReady = false });
        Equal(ViperSerpentTailDecisionKind.Armed, armed.Kind, "boundary wait is armed");
        True(armed.InputClaimed, "VPR reserves its job-priority frame");
        var rangeWait = ViperSerpentTailRules.Observe(
            armed.NextState,
            Observation() with
            {
                Trigger = null,
                Candidate = Candidate() with { HasNativeRangeAndLineOfSight = false },
                NowMilliseconds = 1_001,
            });
        Equal(ViperSerpentTailDecisionKind.Armed, rangeWait.Kind, "range wait retains intent");
        False(rangeWait.InputClaimed, "range wait yields lower helpers");

        var soft = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
            armed.NextState, ClientActionAttemptOutcome.SoftUnavailable, 1_000);
        True(soft.SoftWait, "known native wait is free");
        Equal(HeldActionRetryDisposition.SoftWait, soft.Disposition, "soft disposition preserved");
        Equal(0, soft.NextState.Retry.NativeAttemptCount, "soft wait spends no retry");

        var expired = ViperSerpentTailRules.Observe(
            armed.NextState,
            Observation() with { Trigger = null, NowMilliseconds = 5_900 });
        Equal(ViperSerpentTailDecisionKind.Cancelled, expired.Kind, "buffered expiry is terminal");
        Equal(ViperSerpentTailDecisionReason.TriggerExpiredOrDrifted,
            expired.Reason, "buffered expiry reason");

        var state = Observe(Observation()).NextState;
        for (var attempt = 1; attempt <= HeldActionRetryRules.MaximumNativeAttempts; attempt++)
        {
            var now = 1_000L + ((attempt - 1) * HeldActionRetryRules.NativeRetryThrottleMilliseconds);
            var completion = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
                state, ClientActionAttemptOutcome.ClientRejected, now);
            state = completion.NextState;
            if (attempt < HeldActionRetryRules.MaximumNativeAttempts)
            {
                True(completion.RetryScheduled, $"retry {attempt}");
                var next = ViperSerpentTailRules.Observe(
                    state,
                    Observation() with { Trigger = null, NowMilliseconds = now + 50 });
                Dispatch(next, $"retry dispatch {attempt}");
                False(next.ConsumeTrigger, "frozen retry never consumes another trigger");
                state = next.NextState;
            }
            else
            {
                True(completion.Terminal, "eighth false is terminal");
                Equal(HeldActionRetryDisposition.RejectedTerminal,
                    completion.Disposition, "retry exhaustion disposition preserved");
                True(HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(completion.Disposition),
                    "retry exhaustion latches the exact key");
                Equal(ViperSerpentTailState.Initial, completion.NextState, "terminal state clears");
            }
        }

        var ambiguous = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
            Observe(Observation()).NextState,
            ClientActionAttemptOutcome.AcceptanceUnknown,
            1_000);
        Equal(HeldActionRetryDisposition.AmbiguousTerminal,
            ambiguous.Disposition, "ambiguous disposition preserved");
        True(HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(ambiguous.Disposition),
            "ambiguous acceptance latches the exact key");

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
        Equal(ClientActionAttemptOutcome.AcceptanceUnknown,
            ViperSerpentTailRules.ClassifyFollowUpBoundary(
                false,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                targetStatusBefore: 0,
                targetStatusAfter: 0,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                ViperSerpentTailRules.UncoiledTwinfangActionId,
                stable,
                stable with { LastUsedActionSequence = 21 }),
            "false after full fingerprint drift is terminal ambiguous");
    }

    public static void ContinuousHoldUsesDistinctAcceptedFollowupTriggersOnly()
    {
        True(ViperSerpentTailRules.CanReserveChainedAcceptedActionEpoch(1, 1, 0),
            "accepted 39177 may reserve its next epoch after predecessor consumption");
        True(ViperSerpentTailRules.CanReserveChainedAcceptedActionEpoch(1, 1, 1),
            "accepted 39177 may invalidate only its matching pending predecessor");
        False(ViperSerpentTailRules.CanReserveChainedAcceptedActionEpoch(1, 2, 2),
            "newer accepted VPR epoch blocks stale 39177 chain without overwrite");
        False(ViperSerpentTailRules.CanReserveChainedAcceptedActionEpoch(1, 1, 2),
            "mismatched pending epoch is never invalidated by chained arm");
        False(ViperSerpentTailRules.CanReserveChainedAcceptedActionEpoch(0, 0, 0),
            "invalid accepted epoch cannot start a chain");

        var firstBuffered = Observe(Observation() with { NativeBoundaryReady = false });
        True(ViperSerpentTailRules.TryCreateAcceptedTrigger(
            2, 1032, SupportedPvPContext.CrystallineConflict, Local,
            39_168, 3, Target, 1_001, out var newerSameFollowUp),
            "newer accepted action may resolve to the same follow-up");
        var superseded = ViperSerpentTailRules.Observe(
            firstBuffered.NextState,
            Observation() with
            {
                CurrentAcceptedActionEpoch = 2,
                Trigger = newerSameFollowUp,
                NowMilliseconds = 1_002,
            });
        Equal(ViperSerpentTailDecisionKind.Cancelled, superseded.Kind,
            "newer same-follow-up epoch cancels old buffered intent");
        Equal(ViperSerpentTailDecisionReason.TriggerSuperseded, superseded.Reason,
            "superseded epoch reason");
        var replacement = ViperSerpentTailRules.Observe(
            superseded.NextState,
            Observation() with
            {
                CurrentAcceptedActionEpoch = 2,
                Trigger = newerSameFollowUp,
                NowMilliseconds = 1_003,
            });
        Dispatch(replacement, "newer same-follow-up epoch replaces old intent");
        Equal(2L, replacement.Intent!.Value.AcceptedActionEpoch,
            "replacement carries monotonic accepted epoch");

        var first = Observe(Observation());
        var accepted = ViperSerpentTailRules.ApplyNativeAttemptOutcome(
            first.NextState, ClientActionAttemptOutcome.ClientAccepted, 1_000);
        True(accepted.ClientAccepted, "first follow-up accepted");
        Equal(ViperSerpentTailState.Initial, accepted.NextState, "accepted episode retires");

        var unchanged = ViperSerpentTailRules.Observe(
            accepted.NextState,
            Observation() with { Trigger = null, NowMilliseconds = 1_001 });
        False(unchanged.ShouldDispatch, "unchanged adjusted action cannot duplicate");

        True(ViperSerpentTailRules.TryCreateAcceptedTrigger(
            2, 1032, SupportedPvPContext.CrystallineConflict, Local,
            ViperSerpentTailRules.UncoiledTwinfangActionId, 3, Target, 1_001,
            out var secondTrigger), "accepted Twinfang creates Twinblood trigger");
        var second = ViperSerpentTailRules.Observe(
            accepted.NextState,
            Observation() with
            {
                Trigger = secondTrigger,
                CurrentAcceptedActionEpoch = secondTrigger.Token,
                ResolvedAdjustedActionId = ViperSerpentTailRules.UncoiledTwinbloodActionId,
                NowMilliseconds = 1_002,
            });
        Dispatch(second, "same hold may execute distinct Twinblood epoch");
        Equal(ViperSerpentTailRules.UncoiledTwinbloodActionId,
            second.Intent!.Value.ActionId, "second adjusted action frozen");
    }

    private static ViperSerpentTailObservation Observation()
    {
        ViperSerpentTailRules.TryCreateAcceptedTrigger(
            1, 1032, SupportedPvPContext.CrystallineConflict, Local,
            39_168, 3, Target, 900, out var trigger);
        return new ViperSerpentTailObservation(
            ConfigurationEnabled: true,
            Context: SupportedPvPContext.CrystallineConflict,
            TerritoryId: 1032,
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
            ResolvedAdjustedActionId: ViperSerpentTailRules.UncoiledTwinfangActionId,
            ActionLocallyReady: true,
            NativeBoundaryReady: true,
            CurrentAcceptedActionEpoch: trigger.Token,
            Trigger: trigger,
            Candidate: Candidate(),
            HardReset: false,
            NowMilliseconds: 1_000);
    }

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
        ViperSerpentTailCandidate candidate,
        SupportedPvPContext context = SupportedPvPContext.CrystallineConflict,
        uint adjustedActionId = ViperSerpentTailRules.UncoiledTwinfangActionId,
        int currentHeldKeyCode = HeldKey,
        long nowMilliseconds = 1_000,
        bool guardSuppressed = false,
        bool higherPriorityClaimed = false,
        long currentAcceptedActionEpoch = 1) =>
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
            adjustedActionId,
            actionLocallyReady: true,
            currentAcceptedActionEpoch,
            nowMilliseconds,
            currentHeldKeyCode,
            frozenKeyStillDown: true,
            candidate);

    private static ClientActionAttemptFingerprint Fingerprint(
        ushort sequence,
        bool actionQueued = false,
        uint queuedActionType = 1,
        uint queuedActionId = 39_168,
        ulong queuedTargetId = 0,
        uint queuedExtraParam = 0,
        uint queuedComboRouteId = 0) => new(
        Captured: true,
        ActionQueued: actionQueued,
        QueuedActionType: actionQueued ? queuedActionType : 0,
        QueuedActionId: actionQueued ? queuedActionId : 0,
        QueuedTargetId: actionQueued
            ? (queuedTargetId == 0 ? Target.GameObjectId : queuedTargetId)
            : 0,
        QueuedExtraParam: actionQueued ? queuedExtraParam : 0,
        QueueMode: actionQueued ? 1u : 0,
        QueuedComboRouteId: actionQueued ? queuedComboRouteId : 0,
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
