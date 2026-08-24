namespace SeitonSense.Core;

public readonly record struct ViperSerpentTailTrigger(
    long Token,
    uint TerritoryId,
    SupportedPvPContext Context,
    TargetPressureActorIdentity LocalPlayer,
    uint TriggerActionId,
    uint ExpectedAdjustedActionId,
    int EnemySlot,
    TargetPressureActorIdentity Target,
    long AcceptedAtMilliseconds,
    long ExpiresAtMilliseconds)
{
    public long AcceptedActionEpoch => Token;

    public bool IsValid =>
        Token > 0 &&
        TerritoryId != 0 &&
        Context is SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen &&
        LocalPlayer.IsValid &&
        ViperSerpentTailRules.TryGetExpectedFollowUp(
            TriggerActionId,
            out var expectedActionId) &&
        expectedActionId == ExpectedAdjustedActionId &&
        ViperSerpentTailRules.IsContextSlotValid(Context, EnemySlot) &&
        Target.IsValid &&
        Target != LocalPlayer &&
        AcceptedAtMilliseconds >= 0 &&
        ExpiresAtMilliseconds > AcceptedAtMilliseconds;
}

public readonly record struct ViperSerpentTailCandidate(
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight);

public readonly record struct ViperSerpentTailIntent(
    long TriggerToken,
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    uint ActionId,
    long ExpiresAtMilliseconds,
    int FrozenKeyCode)
{
    public long AcceptedActionEpoch => TriggerToken;

    public bool IsValid =>
        TriggerToken > 0 &&
        ViperSerpentTailRules.IsContextSlotValid(Context, EnemySlot) &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        Target != LocalPlayer &&
        ViperSerpentTailRules.IsExactFollowUpAction(ActionId) &&
        ExpiresAtMilliseconds > 0 &&
        FrozenKeyCode > 0;
}

public enum ViperSerpentTailPhase : byte
{
    Waiting = 0,
    Buffered = 1,
}

public readonly record struct ViperSerpentTailState(
    ViperSerpentTailPhase Phase,
    ViperSerpentTailIntent? Intent,
    HeldActionRetryState Retry,
    long LastObservedAtMilliseconds,
    ClientActionAttemptOutcome LastNativeOutcome)
{
    public static ViperSerpentTailState Initial => new(
        ViperSerpentTailPhase.Waiting,
        null,
        HeldActionRetryState.Initial,
        -1,
        ClientActionAttemptOutcome.None);
}

public readonly record struct ViperSerpentTailObservation(
    bool ConfigurationEnabled,
    SupportedPvPContext Context,
    uint TerritoryId,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    uint LocalJobId,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    int HeldGameplayKeyCode,
    bool FrozenKeyStillDown,
    uint ResolvedAdjustedActionId,
    bool ActionLocallyReady,
    bool NativeBoundaryReady,
    long CurrentAcceptedActionEpoch,
    ViperSerpentTailTrigger? Trigger,
    ViperSerpentTailCandidate? Candidate,
    bool HardReset,
    long NowMilliseconds);

public enum ViperSerpentTailDecisionKind : byte
{
    None = 0,
    Armed = 1,
    Dispatch = 2,
    Cancelled = 3,
}

public enum ViperSerpentTailDecisionReason : byte
{
    None = 0,
    HardReset,
    ClockMovedBackwards,
    ConfigurationDisabled,
    OutsideSupportedPvPContext,
    LocalPlayerIdentityInvalid,
    LocalPlayerDead,
    LocalJobInvalid,
    MetadataUnverified,
    InputProbeUnavailable,
    TextInputActive,
    GuardSuppressed,
    HigherPriorityClaimed,
    TriggerUnavailable,
    TriggerSuperseded,
    TriggerExpiredOrDrifted,
    AdjustedActionUnavailable,
    CandidateUnavailable,
    CandidateInvalid,
    NoHeldGameplayKey,
    ExactKeyReleased,
    ActionNotReady,
    NativeBoundaryUnavailable,
    NativeRetryThrottle,
    NativeRetryLimitReached,
    NativeAcceptanceUnknown,
}

public readonly record struct ViperSerpentTailDecision(
    ViperSerpentTailState NextState,
    ViperSerpentTailDecisionKind Kind,
    ViperSerpentTailDecisionReason Reason,
    ViperSerpentTailIntent? Intent = null,
    bool InputClaimed = false,
    bool ConsumeTrigger = false)
{
    public bool ShouldDispatch =>
        Kind == ViperSerpentTailDecisionKind.Dispatch &&
        Intent is { IsValid: true };
}

public readonly record struct ViperSerpentTailNativeAttemptDecision(
    ViperSerpentTailState NextState,
    ViperSerpentTailDecisionReason Reason,
    HeldActionRetryDisposition Disposition,
    bool RetryScheduled,
    bool ClientAccepted,
    bool Terminal,
    bool SoftWait = false);

public enum ViperSerpentTailTriggerPromotionDisposition : byte
{
    UnsupportedInvocationMode = 0,
    ClientRejected = 1,
    NativeQueueOwned = 2,
    AcceptanceUnknown = 3,
    ExecutedAccepted = 4,
}

public enum ViperSerpentTailTriggerInvocationKind : byte
{
    Unsupported = 0,
    Direct = 1,
    ProvenNativeQueueDrain = 2,
}

public static class ViperSerpentTailRules
{
    public const uint DirectUseActionMode = 0;
    public const uint QueueUseActionMode = 1;
    public const uint MacroUseActionMode = 2;
    public const uint ComboUseActionMode = 3;
    public const uint LegacyMacroUseActionMode = 100;
    public const uint ViperJobId = 41;
    public const uint WolvesDenStrikingDummyNameId = 541;
    public const uint CarrierActionId = 39_183;
    public const uint DeathRattleActionId = 39_174;
    public const uint TwinfangBiteActionId = 39_175;
    public const uint TwinbloodBiteActionId = 39_176;
    public const uint UncoiledTwinfangActionId = 39_177;
    public const uint UncoiledTwinbloodActionId = 39_178;
    public const uint FirstLegacyActionId = 39_179;
    public const uint SecondLegacyActionId = 39_180;
    public const uint ThirdLegacyActionId = 39_181;
    public const uint FourthLegacyActionId = 39_182;
    public const long TriggerLifetimeMilliseconds = 5_000;

    public static bool IsExactFollowUpAction(uint actionId) => actionId is
        DeathRattleActionId or
        TwinfangBiteActionId or
        TwinbloodBiteActionId or
        UncoiledTwinfangActionId or
        UncoiledTwinbloodActionId or
        FirstLegacyActionId or
        SecondLegacyActionId or
        ThirdLegacyActionId or
        FourthLegacyActionId;

    public static int GetMaximumRangeYalms(uint actionId) => actionId switch
    {
        UncoiledTwinfangActionId or UncoiledTwinbloodActionId => 20,
        DeathRattleActionId or TwinfangBiteActionId or TwinbloodBiteActionId or
            FirstLegacyActionId or SecondLegacyActionId or ThirdLegacyActionId or
            FourthLegacyActionId => 5,
        _ => 0,
    };

    public static bool TryGetExpectedFollowUp(uint triggerActionId, out uint actionId)
    {
        actionId = triggerActionId switch
        {
            39_161 or 39_163 => DeathRattleActionId,
            39_166 => TwinfangBiteActionId,
            39_167 => TwinbloodBiteActionId,
            39_168 => UncoiledTwinfangActionId,
            UncoiledTwinfangActionId => UncoiledTwinbloodActionId,
            39_169 => FirstLegacyActionId,
            39_170 => SecondLegacyActionId,
            39_171 => ThirdLegacyActionId,
            39_172 => FourthLegacyActionId,
            _ => 0,
        };
        return IsExactFollowUpAction(actionId);
    }

    public static bool TryCreateAcceptedTrigger(
        long token,
        uint territoryId,
        SupportedPvPContext context,
        TargetPressureActorIdentity localPlayer,
        uint triggerActionId,
        int enemySlot,
        TargetPressureActorIdentity target,
        long acceptedAtMilliseconds,
        out ViperSerpentTailTrigger trigger)
    {
        trigger = default;
        if (!TryGetExpectedFollowUp(triggerActionId, out var expectedActionId) ||
            token <= 0 || territoryId == 0 || !localPlayer.IsValid || !target.IsValid ||
            target == localPlayer || !IsContextSlotValid(context, enemySlot) ||
            acceptedAtMilliseconds < 0 ||
            acceptedAtMilliseconds > long.MaxValue - TriggerLifetimeMilliseconds)
        {
            return false;
        }

        trigger = new ViperSerpentTailTrigger(
            token,
            territoryId,
            context,
            localPlayer,
            triggerActionId,
            expectedActionId,
            enemySlot,
            target,
            acceptedAtMilliseconds,
            acceptedAtMilliseconds + TriggerLifetimeMilliseconds);
        return trigger.IsValid;
    }

    public static bool IsTriggerCurrent(
        ViperSerpentTailTrigger trigger,
        long nowMilliseconds,
        uint territoryId,
        SupportedPvPContext context,
        TargetPressureActorIdentity localPlayer) =>
        trigger.IsValid &&
        nowMilliseconds >= trigger.AcceptedAtMilliseconds &&
        nowMilliseconds < trigger.ExpiresAtMilliseconds &&
        territoryId == trigger.TerritoryId &&
        context == trigger.Context &&
        localPlayer == trigger.LocalPlayer;

    public static bool IsCurrentAcceptedActionEpoch(
        long expectedEpoch,
        long currentEpoch) =>
        expectedEpoch > 0 && currentEpoch == expectedEpoch;

    /// <summary>
    /// A plugin-owned accepted follow-up may advance the chain only while the
    /// accepted action that produced it is still the current epoch. A pending
    /// trigger may be absent (the predecessor was already consumed) or belong
    /// to that exact predecessor; a replacement epoch is never invalidated.
    /// </summary>
    public static bool CanReserveChainedAcceptedActionEpoch(
        long completedAcceptedActionEpoch,
        long currentAcceptedActionEpoch,
        long pendingAcceptedActionEpoch) =>
        IsCurrentAcceptedActionEpoch(
            completedAcceptedActionEpoch,
            currentAcceptedActionEpoch) &&
        (pendingAcceptedActionEpoch == 0 ||
         pendingAcceptedActionEpoch == completedAcceptedActionEpoch);

    public static ViperSerpentTailTriggerInvocationKind ClassifyTriggerInvocationMode(
        uint useActionMode,
        bool exactNativeQueueDrainProvenance) =>
        useActionMode switch
        {
            DirectUseActionMode or MacroUseActionMode or ComboUseActionMode or
                LegacyMacroUseActionMode => ViperSerpentTailTriggerInvocationKind.Direct,
            QueueUseActionMode when exactNativeQueueDrainProvenance =>
                ViperSerpentTailTriggerInvocationKind.ProvenNativeQueueDrain,
            _ => ViperSerpentTailTriggerInvocationKind.Unsupported,
        };

    /// <summary>
    /// Promotes only a synchronously executed action. Unsupported invocations
    /// are rejected; Queue is eligible only after its exact native provenance
    /// was proven by <see cref="HasExactNativeQueueDrainProvenance"/>. A direct
    /// call that merely fills the queue is not execution. Both direct and
    /// proven-drain paths require a clear post-call queue and an advanced
    /// native action sequence.
    /// </summary>
    public static ViperSerpentTailTriggerPromotionDisposition
        ClassifyAcceptedTriggerBoundary(
            ViperSerpentTailTriggerInvocationKind invocationKind,
            bool clientReturnedAccepted,
            ClientActionAttemptFingerprint before,
            ClientActionAttemptFingerprint after)
    {
        if (invocationKind is not (ViperSerpentTailTriggerInvocationKind.Direct or
            ViperSerpentTailTriggerInvocationKind.ProvenNativeQueueDrain))
            return ViperSerpentTailTriggerPromotionDisposition.UnsupportedInvocationMode;
        if (!clientReturnedAccepted)
            return ViperSerpentTailTriggerPromotionDisposition.ClientRejected;
        if (!before.Captured || !after.Captured)
            return ViperSerpentTailTriggerPromotionDisposition.AcceptanceUnknown;
        if (after.ActionQueued ||
            (invocationKind == ViperSerpentTailTriggerInvocationKind.Direct &&
             before.ActionQueued) ||
            (invocationKind == ViperSerpentTailTriggerInvocationKind.ProvenNativeQueueDrain &&
             !before.ActionQueued))
        {
            return ViperSerpentTailTriggerPromotionDisposition.NativeQueueOwned;
        }
        return after.LastUsedActionSequence != 0 &&
               before.LastUsedActionSequence != after.LastUsedActionSequence
            ? ViperSerpentTailTriggerPromotionDisposition.ExecutedAccepted
            : ViperSerpentTailTriggerPromotionDisposition.AcceptanceUnknown;
    }

    public static bool HasExactNativeQueueDrainProvenance(
        bool isQueueInvocation,
        bool actionTypeSupported,
        uint incomingActionType,
        uint incomingResolvedActionId,
        ulong incomingEffectiveTargetId,
        uint incomingExtraParam,
        uint incomingComboRouteId,
        uint queuedResolvedActionId,
        TargetPressureActorIdentity canonicalTarget,
        ClientActionAttemptFingerprint before) =>
        isQueueInvocation &&
        actionTypeSupported &&
        incomingActionType != 0 &&
        before.Captured &&
        before.ActionQueued &&
        before.QueuedActionType == incomingActionType &&
        before.QueuedActionId != 0 &&
        TryGetExpectedFollowUp(incomingResolvedActionId, out _) &&
        queuedResolvedActionId == incomingResolvedActionId &&
        ActorIdMatches(incomingEffectiveTargetId, canonicalTarget) &&
        ActorIdMatches(before.QueuedTargetId, canonicalTarget) &&
        before.QueuedExtraParam == incomingExtraParam &&
        before.QueuedComboRouteId == incomingComboRouteId;

    /// <summary>
    /// A false return is retryable only when the exact target-aware status is
    /// ready both before and after, the carrier still resolves to the frozen
    /// action, and the complete native fingerprint stayed stable.
    /// </summary>
    public static ClientActionAttemptOutcome ClassifyFollowUpBoundary(
        bool clientReturnedAccepted,
        uint expectedActionId,
        uint targetStatusBefore,
        uint targetStatusAfter,
        uint carrierBefore,
        uint carrierAfter,
        ClientActionAttemptFingerprint before,
        ClientActionAttemptFingerprint after)
    {
        if (clientReturnedAccepted)
            return ClientActionAttemptOutcome.ClientAccepted;
        if (expectedActionId == 0 ||
            targetStatusBefore != 0 ||
            targetStatusAfter != 0 ||
            carrierBefore != expectedActionId ||
            carrierAfter != expectedActionId)
        {
            return ClientActionAttemptOutcome.AcceptanceUnknown;
        }

        return ClientActionAttemptBoundaryRules.Classify(
            false,
            expectedActionId,
            before,
            after);
    }

    public static bool IsContextSlotValid(SupportedPvPContext context, int enemySlot) =>
        context switch
        {
            SupportedPvPContext.CrystallineConflict => EnemySlotRules.IsValidSlot(enemySlot),
            SupportedPvPContext.WolvesDen => enemySlot == 0,
            _ => false,
        };

    public static ViperSerpentTailDecision Observe(
        ViperSerpentTailState previous,
        ViperSerpentTailObservation observation)
    {
        if (observation.HardReset)
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.HardReset);
        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(
                ViperSerpentTailState.Initial,
                ViperSerpentTailDecisionReason.ClockMovedBackwards);
        }

        var permanentFailure = GetPermanentGateFailure(observation);
        if (permanentFailure != ViperSerpentTailDecisionReason.None)
            return None(ViperSerpentTailState.Initial, permanentFailure);

        return previous.Phase == ViperSerpentTailPhase.Buffered
            ? ObserveBuffered(previous, observation)
            : TryCreateIntent(observation);
    }

    public static ViperSerpentTailNativeAttemptDecision ApplyNativeAttemptOutcome(
        ViperSerpentTailState current,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        if (current.Phase != ViperSerpentTailPhase.Buffered ||
            current.Intent is not { IsValid: true } || nowMilliseconds < 0)
        {
            return TerminalUnknown(current, nowMilliseconds);
        }

        var shared = HeldActionRetryRules.Complete(current.Retry, nowMilliseconds, outcome);
        return shared.Disposition switch
        {
            HeldActionRetryDisposition.SoftWait => new(
                Stamp(current with { LastNativeOutcome = outcome }, nowMilliseconds),
                ViperSerpentTailDecisionReason.NativeBoundaryUnavailable,
                shared.Disposition,
                false, false, false, true),
            HeldActionRetryDisposition.AcceptedTerminal => new(
                ViperSerpentTailState.Initial,
                ViperSerpentTailDecisionReason.None,
                shared.Disposition,
                false, true, true),
            HeldActionRetryDisposition.RetryScheduled => new(
                Stamp(current with
                {
                    Retry = shared.NextState,
                    LastNativeOutcome = outcome,
                }, nowMilliseconds),
                ViperSerpentTailDecisionReason.NativeRetryThrottle,
                shared.Disposition,
                true, false, false),
            HeldActionRetryDisposition.RejectedTerminal => new(
                ViperSerpentTailState.Initial,
                ViperSerpentTailDecisionReason.NativeRetryLimitReached,
                shared.Disposition,
                false, false, true),
            _ => TerminalUnknown(current, nowMilliseconds, shared.Disposition),
        };
    }

    public static bool CanUseFrozenIntent(
        ViperSerpentTailIntent intent,
        bool configurationEnabled,
        SupportedPvPContext context,
        TargetPressureActorIdentity localPlayer,
        bool localAlive,
        uint localJobId,
        bool metadataVerified,
        bool guardSuppressed,
        bool higherPriorityClaimed,
        uint adjustedActionId,
        bool actionLocallyReady,
        long currentAcceptedActionEpoch,
        long nowMilliseconds,
        int currentHeldKeyCode,
        bool frozenKeyStillDown,
        ViperSerpentTailCandidate candidate) =>
        intent.IsValid && configurationEnabled && context == intent.Context &&
        localPlayer == intent.LocalPlayer && localAlive && localJobId == ViperJobId &&
        metadataVerified && !guardSuppressed && !higherPriorityClaimed &&
        IsCurrentAcceptedActionEpoch(
            intent.AcceptedActionEpoch,
            currentAcceptedActionEpoch) &&
        adjustedActionId == intent.ActionId && actionLocallyReady &&
        nowMilliseconds >= 0 && nowMilliseconds < intent.ExpiresAtMilliseconds &&
        currentHeldKeyCode == intent.FrozenKeyCode && frozenKeyStillDown &&
        IsExactCandidate(intent, candidate) && candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight;

    private static ViperSerpentTailDecision TryCreateIntent(
        ViperSerpentTailObservation observation)
    {
        if (observation.ActionHelpersSuppressedByGuard)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.GuardSuppressed);
        if (observation.HigherPriorityClaimed)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.HigherPriorityClaimed);
        if (observation.Trigger is not { IsValid: true } trigger)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.TriggerUnavailable);
        if (!IsCurrentAcceptedActionEpoch(
                trigger.AcceptedActionEpoch,
                observation.CurrentAcceptedActionEpoch))
        {
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.TriggerSuperseded);
        }
        if (!IsTriggerCurrent(
                trigger,
                observation.NowMilliseconds,
                observation.TerritoryId,
                observation.Context,
                observation.LocalPlayer))
        {
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.TriggerExpiredOrDrifted);
        }
        if (observation.ResolvedAdjustedActionId != trigger.ExpectedAdjustedActionId)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.AdjustedActionUnavailable);
        if (observation.Candidate is not { } candidate)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.CandidateUnavailable);
        if (!IsExactCandidate(trigger, candidate) ||
            !candidate.HasValidActionTarget ||
            !candidate.HasNativeRangeAndLineOfSight)
        {
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.CandidateInvalid);
        }
        if (!observation.HeldGameplayKeyEligible || observation.HeldGameplayKeyCode <= 0)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.NoHeldGameplayKey);

        var intent = new ViperSerpentTailIntent(
            trigger.Token,
            trigger.Context,
            trigger.EnemySlot,
            trigger.LocalPlayer,
            trigger.Target,
            trigger.ExpectedAdjustedActionId,
            trigger.ExpiresAtMilliseconds,
            observation.HeldGameplayKeyCode);
        if (!intent.IsValid)
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.TriggerExpiredOrDrifted);

        var buffered = new ViperSerpentTailState(
            ViperSerpentTailPhase.Buffered,
            intent,
            HeldActionRetryState.Initial,
            observation.NowMilliseconds,
            ClientActionAttemptOutcome.None);
        if (!observation.ActionLocallyReady)
            return Armed(
                buffered,
                ViperSerpentTailDecisionReason.ActionNotReady,
                inputClaimed: false,
                consumeTrigger: true);
        return observation.NativeBoundaryReady
            ? Dispatch(buffered, intent, consumeTrigger: true)
            : Armed(
                buffered,
                ViperSerpentTailDecisionReason.NativeBoundaryUnavailable,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    buffered.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: true,
                    targetSpecificReady: true),
                consumeTrigger: true);
    }

    private static ViperSerpentTailDecision ObserveBuffered(
        ViperSerpentTailState previous,
        ViperSerpentTailObservation observation)
    {
        var intent = previous.Intent;
        if (intent is not { IsValid: true })
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.NativeAcceptanceUnknown);
        if (!IsCurrentAcceptedActionEpoch(
                intent.Value.AcceptedActionEpoch,
                observation.CurrentAcceptedActionEpoch))
        {
            return Cancelled(
                ViperSerpentTailState.Initial,
                ViperSerpentTailDecisionReason.TriggerSuperseded);
        }
        if (observation.NowMilliseconds >= intent.Value.ExpiresAtMilliseconds)
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.TriggerExpiredOrDrifted);
        if (!observation.FrozenKeyStillDown)
            return None(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.ExactKeyReleased);
        if (observation.HigherPriorityClaimed)
            return None(Stamp(previous, observation.NowMilliseconds), ViperSerpentTailDecisionReason.HigherPriorityClaimed);
        if (observation.ActionHelpersSuppressedByGuard)
            return None(Stamp(previous, observation.NowMilliseconds), ViperSerpentTailDecisionReason.GuardSuppressed);
        if (observation.ResolvedAdjustedActionId != intent.Value.ActionId)
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.AdjustedActionUnavailable);
        if (observation.Candidate is not { } candidate || !IsExactCandidate(intent.Value, candidate))
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.CandidateUnavailable);
        if (!candidate.Alive || !candidate.Targetable || !candidate.ExactCanonicalIdentity)
            return Cancelled(ViperSerpentTailState.Initial, ViperSerpentTailDecisionReason.CandidateInvalid);
        if (!candidate.HasValidActionTarget || !candidate.HasNativeRangeAndLineOfSight ||
            !observation.ActionLocallyReady)
        {
            return Armed(
                Stamp(previous, observation.NowMilliseconds),
                observation.ActionLocallyReady
                    ? ViperSerpentTailDecisionReason.CandidateInvalid
                    : ViperSerpentTailDecisionReason.ActionNotReady,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    previous.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: observation.ActionLocallyReady,
                    targetSpecificReady: candidate.HasValidActionTarget &&
                                         candidate.HasNativeRangeAndLineOfSight));
        }
        if (!observation.NativeBoundaryReady)
            return Armed(
                Stamp(previous, observation.NowMilliseconds),
                ViperSerpentTailDecisionReason.NativeBoundaryUnavailable,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    previous.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: true,
                    targetSpecificReady: true));
        if (!HeldActionRetryRules.CanAttemptFrozenIntent(previous.Retry, observation.NowMilliseconds))
            return Armed(
                Stamp(previous, observation.NowMilliseconds),
                ViperSerpentTailDecisionReason.NativeRetryThrottle,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    previous.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: true,
                    targetSpecificReady: true));
        return Dispatch(Stamp(previous, observation.NowMilliseconds), intent.Value);
    }

    private static ViperSerpentTailDecisionReason GetPermanentGateFailure(
        ViperSerpentTailObservation observation)
    {
        if (!observation.ConfigurationEnabled) return ViperSerpentTailDecisionReason.ConfigurationDisabled;
        if (observation.Context is not (SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen))
            return ViperSerpentTailDecisionReason.OutsideSupportedPvPContext;
        if (!observation.LocalPlayer.IsValid) return ViperSerpentTailDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive) return ViperSerpentTailDecisionReason.LocalPlayerDead;
        if (observation.LocalJobId != ViperJobId) return ViperSerpentTailDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified) return ViperSerpentTailDecisionReason.MetadataUnverified;
        if (!observation.InputProbeSucceeded) return ViperSerpentTailDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive) return ViperSerpentTailDecisionReason.TextInputActive;
        return ViperSerpentTailDecisionReason.None;
    }

    private static bool IsExactCandidate(
        ViperSerpentTailTrigger trigger,
        ViperSerpentTailCandidate candidate) =>
        candidate.Context == trigger.Context && candidate.EnemySlot == trigger.EnemySlot &&
        candidate.Actor == trigger.Target && candidate.ExactCanonicalIdentity &&
        candidate.Alive && candidate.Targetable;

    private static bool IsExactCandidate(
        ViperSerpentTailIntent intent,
        ViperSerpentTailCandidate candidate) =>
        candidate.Context == intent.Context && candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target && candidate.ExactCanonicalIdentity &&
        candidate.Alive && candidate.Targetable;

    private static bool ActorIdMatches(
        ulong actorId,
        TargetPressureActorIdentity actor) =>
        actor.IsValid &&
        (actorId == actor.GameObjectId ||
         actorId <= uint.MaxValue && (uint)actorId == actor.EntityId);

    private static ViperSerpentTailState Stamp(ViperSerpentTailState state, long now) =>
        state with { LastObservedAtMilliseconds = now };

    private static ViperSerpentTailNativeAttemptDecision TerminalUnknown(
        ViperSerpentTailState current,
        long now,
        HeldActionRetryDisposition disposition =
            HeldActionRetryDisposition.AmbiguousTerminal) => new(
        ViperSerpentTailState.Initial,
        ViperSerpentTailDecisionReason.NativeAcceptanceUnknown,
        disposition,
        false, false, true);

    private static ViperSerpentTailDecision Dispatch(
        ViperSerpentTailState state,
        ViperSerpentTailIntent intent,
        bool consumeTrigger = false) => new(
        state,
        ViperSerpentTailDecisionKind.Dispatch,
        ViperSerpentTailDecisionReason.None,
        intent,
        InputClaimed: true,
        ConsumeTrigger: consumeTrigger);

    private static ViperSerpentTailDecision Armed(
        ViperSerpentTailState state,
        ViperSerpentTailDecisionReason reason,
        bool inputClaimed,
        bool consumeTrigger = false) => new(
        state,
        ViperSerpentTailDecisionKind.Armed,
        reason,
        state.Intent,
        InputClaimed: inputClaimed,
        ConsumeTrigger: consumeTrigger);

    private static ViperSerpentTailDecision None(
        ViperSerpentTailState state,
        ViperSerpentTailDecisionReason reason) => new(
        state,
        ViperSerpentTailDecisionKind.None,
        reason);

    private static ViperSerpentTailDecision Cancelled(
        ViperSerpentTailState state,
        ViperSerpentTailDecisionReason reason) => new(
        state,
        ViperSerpentTailDecisionKind.Cancelled,
        reason);
}
