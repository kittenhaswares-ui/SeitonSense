namespace SeitonSense.Core;

public readonly record struct MonkHeldComboCandidate(
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaxHp,
    bool ComboTargetReady,
    bool FireReplyTargetReady,
    bool WindReplyTargetReady,
    bool ThunderclapTargetReady,
    bool PhantomRushTargetReady,
    bool HasExactOwnPressurePoint);

public readonly record struct MonkHeldComboIntent(
    SupportedPvPContext Context,
    int EnemySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    uint RouteId,
    int FrozenKeyCode)
{
    public bool IsValid =>
        MonkHeldComboRules.IsContextSlotValid(Context, EnemySlot) &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        Target != LocalPlayer &&
        RouteId == MonkHeldComboRules.PhantomRushComboRouteId &&
        FrozenKeyCode > 0;
}

public enum MonkHeldComboPhase : byte
{
    Waiting = 0,
    Active = 1,
    BufferedAction = 2,
    AwaitCarrierTransition = 3,
    AwaitPressurePoint = 4,
    AwaitPhantomRange = 5,
    AwaitFireResonance = 6,
}

public enum MonkHeldComboActionPurpose : byte
{
    None = 0,
    NormalCombo = 1,
    FireReplyFallback = 2,
    WindReplySetup = 3,
    ThunderclapReturn = 4,
    RisingPhoenixBuff = 5,
    PhantomRushFinish = 6,
}

public readonly record struct MonkHeldComboState(
    MonkHeldComboPhase Phase,
    MonkHeldComboIntent? Intent,
    uint CarrierActionId,
    uint PendingActionId,
    MonkHeldComboActionPurpose PendingPurpose,
    HeldActionRetryState Retry,
    ushort ConfirmationSequenceBaseline,
    bool PressurePointConfirmed,
    bool FireResonanceConfirmed,
    bool ThunderclapUsed,
    long LastObservedAtMilliseconds,
    ClientActionAttemptOutcome LastNativeOutcome)
{
    public static MonkHeldComboState Initial => new(
        MonkHeldComboPhase.Waiting,
        null,
        0,
        0,
        MonkHeldComboActionPurpose.None,
        HeldActionRetryState.Initial,
        0,
        false,
        false,
        false,
        -1,
        ClientActionAttemptOutcome.None);

    public bool HasBufferedAction =>
        Phase == MonkHeldComboPhase.BufferedAction &&
        Intent is { IsValid: true } &&
        MonkHeldComboRules.IsDispatchableAction(PendingActionId) &&
        PendingPurpose != MonkHeldComboActionPurpose.None &&
        MonkHeldComboRules.IsExactComboAction(CarrierActionId);
}

public readonly record struct MonkHeldComboObservation(
    bool ConfigurationEnabled,
    SupportedPvPContext Context,
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
    uint ResolvedComboActionId,
    bool ComboActionLocallyReady,
    bool FireReplyLocallyReady,
    bool WindReplyLocallyReady,
    bool ThunderclapLocallyReady,
    bool RisingPhoenixLocallyReady,
    bool HasExactOwnFireResonance,
    bool ConfirmationBoundaryReopened,
    bool NativeBoundaryReady,
    MonkHeldComboCandidate? Candidate,
    bool HardReset,
    long NowMilliseconds);

public enum MonkHeldComboDecisionKind : byte
{
    None = 0,
    Armed = 1,
    Dispatch = 2,
    Cancelled = 3,
}

public enum MonkHeldComboDecisionReason : byte
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
    CarrierUnavailable,
    CarrierTransitionPending,
    CarrierDrift,
    CandidateUnavailable,
    CandidateInvalid,
    NoHeldGameplayKey,
    ExactKeyReleased,
    ActionNotReady,
    TargetNotReady,
    PressurePointPending,
    PressurePointMissing,
    FireResonancePending,
    FireResonanceMissing,
    NativeBoundaryUnavailable,
    NativeRetryThrottle,
    NativeRetryLimitReached,
    NativeAcceptanceUnknown,
}

public readonly record struct MonkHeldComboDecision(
    MonkHeldComboState NextState,
    MonkHeldComboDecisionKind Kind,
    MonkHeldComboDecisionReason Reason,
    MonkHeldComboIntent? Intent = null,
    uint ActionId = 0,
    MonkHeldComboActionPurpose Purpose = MonkHeldComboActionPurpose.None,
    bool InputClaimed = false)
{
    public bool ShouldDispatch =>
        Kind == MonkHeldComboDecisionKind.Dispatch &&
        Intent is { IsValid: true } &&
        MonkHeldComboRules.IsDispatchableAction(ActionId) &&
        Purpose != MonkHeldComboActionPurpose.None;
}

public readonly record struct MonkHeldComboNativeAttemptDecision(
    MonkHeldComboState NextState,
    MonkHeldComboDecisionReason Reason,
    HeldActionRetryDisposition Disposition,
    bool RetryScheduled,
    bool ClientAccepted,
    bool Terminal,
    bool RouteComplete,
    bool SoftWait = false);

public static class MonkHeldComboRules
{
    public const uint MonkJobId = 20;
    public const uint MonkClassJobCategoryId = 21;
    public const uint PhantomRushComboRouteId = 55;
    public const uint WolvesDenStrikingDummyNameId = 541;

    public const uint ComboCarrierActionId = 29_475;
    public const uint DragonKickActionId = 29_475;
    public const uint TwinSnakesActionId = 29_476;
    public const uint DemolishActionId = 29_477;
    public const uint LeapingOpoActionId = 41_444;
    public const uint RisingRaptorActionId = 41_445;
    public const uint PouncingCoeurlActionId = 41_446;
    public const uint PhantomRushActionId = 29_478;

    public const uint FireReplyActionId = 41_448;
    public const uint WindReplyActionId = 41_509;
    public const uint RisingPhoenixActionId = 29_481;
    public const uint ThunderclapActionId = 29_484;

    public const uint PressurePointStatusId = 3_172;
    public const uint FireResonanceStatusId = 3_170;
    public const uint WindResonanceStatusId = 2_007;

    public static bool IsExactComboAction(uint actionId) => actionId is
        DragonKickActionId or
        TwinSnakesActionId or
        DemolishActionId or
        LeapingOpoActionId or
        RisingRaptorActionId or
        PouncingCoeurlActionId or
        PhantomRushActionId;

    public static bool IsNormalComboAction(uint actionId) =>
        IsExactComboAction(actionId) && actionId != PhantomRushActionId;

    public static bool IsDispatchableAction(uint actionId) =>
        IsExactComboAction(actionId) ||
        actionId is FireReplyActionId or WindReplyActionId or
            RisingPhoenixActionId or ThunderclapActionId;

    /// <summary>
    /// PvP combo-route actions after Dragon Kick are not standalone hotbar
    /// actions. They must be submitted through ActionManager's combo mode with
    /// the exact ActionComboRoute row; a normal UseAction call can execute the
    /// first action but cannot advance the single-button route.
    /// </summary>
    public static uint GetNativeComboRouteId(
        uint actionId,
        MonkHeldComboActionPurpose purpose) =>
        IsExactComboAction(actionId) &&
        purpose is MonkHeldComboActionPurpose.NormalCombo or
            MonkHeldComboActionPurpose.PhantomRushFinish
            ? PhantomRushComboRouteId
            : 0;

    public static uint GetExpectedPreviousComboAction(uint actionId) => actionId switch
    {
        DragonKickActionId => 0,
        TwinSnakesActionId => DragonKickActionId,
        DemolishActionId => TwinSnakesActionId,
        LeapingOpoActionId => DemolishActionId,
        RisingRaptorActionId => LeapingOpoActionId,
        PouncingCoeurlActionId => RisingRaptorActionId,
        PhantomRushActionId => PouncingCoeurlActionId,
        _ => uint.MaxValue,
    };

    public static uint GetExpectedNextComboAction(uint actionId) => actionId switch
    {
        DragonKickActionId => TwinSnakesActionId,
        TwinSnakesActionId => DemolishActionId,
        DemolishActionId => LeapingOpoActionId,
        LeapingOpoActionId => RisingRaptorActionId,
        RisingRaptorActionId => PouncingCoeurlActionId,
        PouncingCoeurlActionId => PhantomRushActionId,
        PhantomRushActionId => 0,
        _ => uint.MaxValue,
    };

    public static bool IsContextSlotValid(
        SupportedPvPContext context,
        int enemySlot) =>
        context switch
        {
            SupportedPvPContext.CrystallineConflict =>
                EnemySlotRules.IsValidSlot(enemySlot),
            SupportedPvPContext.WolvesDen => enemySlot == 0,
            _ => false,
        };

    public static MonkHeldComboCandidate? SelectBestCandidate(
        SupportedPvPContext context,
        uint resolvedComboActionId,
        bool fireReplyLocallyReady,
        bool windReplyLocallyReady,
        bool thunderclapLocallyReady,
        bool hasExactOwnFireResonance,
        IReadOnlyList<MonkHeldComboCandidate> candidates)
    {
        if (!IsExactComboAction(resolvedComboActionId) ||
            context is not (SupportedPvPContext.CrystallineConflict or
                            SupportedPvPContext.WolvesDen) ||
            candidates is null)
        {
            return null;
        }

        if (context == SupportedPvPContext.WolvesDen)
        {
            MonkHeldComboCandidate? only = null;
            foreach (var candidate in candidates)
            {
                if (!IsStructurallyValidCandidate(context, candidate)) continue;
                if (!IsRouteReachable(
                        resolvedComboActionId,
                        fireReplyLocallyReady,
                        windReplyLocallyReady,
                        thunderclapLocallyReady,
                        hasExactOwnFireResonance,
                        candidate))
                {
                    continue;
                }

                if (only.HasValue) return null;
                only = candidate;
            }

            return only;
        }

        var preferNormalMelee =
            IsNormalComboAction(resolvedComboActionId) &&
            HasAny(candidates, context, static candidate =>
                candidate.ComboTargetReady);
        MonkHeldComboCandidate? best = null;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsStructurallyValidCandidate(context, candidate)) continue;
            for (var otherIndex = index + 1;
                 otherIndex < candidates.Count;
                 otherIndex++)
            {
                var other = candidates[otherIndex];
                if (IsStructurallyValidCandidate(context, other) &&
                    candidate.Actor == other.Actor)
                {
                    return null;
                }
            }

            var eligible = preferNormalMelee
                ? candidate.ComboTargetReady
                : IsRouteReachable(
                    resolvedComboActionId,
                    fireReplyLocallyReady,
                    windReplyLocallyReady,
                    thunderclapLocallyReady,
                    hasExactOwnFireResonance,
                    candidate);
            if (!eligible) continue;
            if (!best.HasValue || CompareCandidates(candidate, best.Value) < 0)
                best = candidate;
        }

        return best;
    }

    public static ClientActionAttemptOutcome ClassifyActionBoundary(
        bool clientReturnedAccepted,
        uint expectedActionId,
        uint expectedCarrierActionId,
        uint targetStatusBefore,
        uint targetStatusAfter,
        uint carrierBefore,
        uint carrierAfter,
        bool pressurePointBefore,
        bool pressurePointAfter,
        bool fireResonanceBefore,
        bool fireResonanceAfter,
        ClientActionAttemptFingerprint before,
        ClientActionAttemptFingerprint after)
    {
        if (clientReturnedAccepted)
            return ClientActionAttemptOutcome.ClientAccepted;
        if (!IsDispatchableAction(expectedActionId) ||
            !IsExactComboAction(expectedCarrierActionId) ||
            targetStatusBefore != 0 ||
            targetStatusAfter != 0 ||
            carrierBefore != expectedCarrierActionId ||
            carrierAfter != expectedCarrierActionId ||
            pressurePointBefore != pressurePointAfter ||
            fireResonanceBefore != fireResonanceAfter)
        {
            return ClientActionAttemptOutcome.AcceptanceUnknown;
        }

        return ClientActionAttemptBoundaryRules.Classify(
            false,
            expectedActionId,
            before,
            after);
    }

    public static MonkHeldComboDecision Observe(
        MonkHeldComboState previous,
        MonkHeldComboObservation observation)
    {
        if (observation.HardReset)
            return Cancelled(InitialStamped(observation), MonkHeldComboDecisionReason.HardReset);
        if (observation.NowMilliseconds < 0 ||
            previous.LastObservedAtMilliseconds >= 0 &&
            observation.NowMilliseconds < previous.LastObservedAtMilliseconds)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.ClockMovedBackwards);
        }

        var permanentFailure = GetPermanentGateFailure(observation);
        if (permanentFailure != MonkHeldComboDecisionReason.None)
            return None(MonkHeldComboState.Initial, permanentFailure);

        if (previous.Phase == MonkHeldComboPhase.Waiting)
            return TryCreateIntent(observation);
        if (previous.Intent is not { IsValid: true } intent)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.NativeAcceptanceUnknown);
        }

        if (!observation.FrozenKeyStillDown)
            return None(MonkHeldComboState.Initial, MonkHeldComboDecisionReason.ExactKeyReleased);
        if (observation.HigherPriorityClaimed)
            return None(Stamp(previous, observation), MonkHeldComboDecisionReason.HigherPriorityClaimed);
        if (observation.ActionHelpersSuppressedByGuard)
            return None(Stamp(previous, observation), MonkHeldComboDecisionReason.GuardSuppressed);
        if (observation.Candidate is not { } candidate ||
            !IsExactCandidate(intent, candidate))
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.CandidateUnavailable);
        }

        if (previous.Phase == MonkHeldComboPhase.BufferedAction)
            return ObserveBuffered(previous, observation, candidate);
        if (!IsExactComboAction(observation.ResolvedComboActionId))
        {
            return Armed(
                Stamp(previous, observation),
                MonkHeldComboDecisionReason.CarrierUnavailable,
                inputClaimed: false);
        }

        return previous.Phase switch
        {
            MonkHeldComboPhase.AwaitCarrierTransition =>
                ObserveCarrierTransition(previous, observation, candidate),
            MonkHeldComboPhase.AwaitPressurePoint =>
                ObservePressurePoint(previous, observation, candidate),
            MonkHeldComboPhase.AwaitPhantomRange =>
                ObservePhantomRange(previous, observation, candidate),
            MonkHeldComboPhase.AwaitFireResonance =>
                ObserveFireResonance(previous, observation, candidate),
            _ => EvaluateActive(
                Stamp(previous with
                {
                    Phase = MonkHeldComboPhase.Active,
                    CarrierActionId = observation.ResolvedComboActionId,
                }, observation),
                observation,
                candidate),
        };
    }

    public static MonkHeldComboNativeAttemptDecision ApplyNativeAttemptOutcome(
        MonkHeldComboState current,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds,
        ushort confirmationSequenceBaseline)
    {
        if (!current.HasBufferedAction || nowMilliseconds < 0)
            return TerminalUnknown();

        var shared = HeldActionRetryRules.Complete(
            current.Retry,
            nowMilliseconds,
            outcome);
        if (shared.Disposition == HeldActionRetryDisposition.SoftWait)
        {
            return new(
                current with
                {
                    LastObservedAtMilliseconds = nowMilliseconds,
                    LastNativeOutcome = outcome,
                },
                MonkHeldComboDecisionReason.NativeBoundaryUnavailable,
                shared.Disposition,
                false, false, false, false, true);
        }

        if (shared.Disposition == HeldActionRetryDisposition.RetryScheduled)
        {
            return new(
                current with
                {
                    Retry = shared.NextState,
                    LastObservedAtMilliseconds = nowMilliseconds,
                    LastNativeOutcome = outcome,
                },
                MonkHeldComboDecisionReason.NativeRetryThrottle,
                shared.Disposition,
                true, false, false, false);
        }

        if (shared.Disposition == HeldActionRetryDisposition.AcceptedTerminal)
        {
            var accepted = CompleteAcceptedAction(
                current,
                nowMilliseconds,
                confirmationSequenceBaseline);
            return new(
                accepted,
                MonkHeldComboDecisionReason.None,
                shared.Disposition,
                false,
                true,
                current.PendingPurpose ==
                    MonkHeldComboActionPurpose.PhantomRushFinish,
                current.PendingPurpose ==
                    MonkHeldComboActionPurpose.PhantomRushFinish);
        }

        if (shared.Disposition == HeldActionRetryDisposition.RejectedTerminal)
        {
            return new(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.NativeRetryLimitReached,
                shared.Disposition,
                false, false, true, false);
        }

        return TerminalUnknown();
    }

    public static bool CanUseFrozenIntent(
        MonkHeldComboState state,
        MonkHeldComboObservation observation,
        MonkHeldComboCandidate candidate) =>
        state.HasBufferedAction &&
        state.Intent is { IsValid: true } intent &&
        observation.ConfigurationEnabled &&
        observation.Context == intent.Context &&
        observation.LocalPlayer == intent.LocalPlayer &&
        observation.IsLocalPlayerAlive &&
        observation.LocalJobId == MonkJobId &&
        observation.MetadataVerified &&
        !observation.ActionHelpersSuppressedByGuard &&
        !observation.HigherPriorityClaimed &&
        observation.ResolvedComboActionId == state.CarrierActionId &&
        observation.HeldGameplayKeyCode == intent.FrozenKeyCode &&
        observation.FrozenKeyStillDown &&
        IsExactCandidate(intent, candidate) &&
        IsPendingActionReady(state, observation, candidate);

    private static MonkHeldComboDecision TryCreateIntent(
        MonkHeldComboObservation observation)
    {
        if (observation.HigherPriorityClaimed)
            return None(InitialStamped(observation), MonkHeldComboDecisionReason.HigherPriorityClaimed);
        if (observation.ActionHelpersSuppressedByGuard)
            return None(InitialStamped(observation), MonkHeldComboDecisionReason.GuardSuppressed);
        if (!IsExactComboAction(observation.ResolvedComboActionId))
            return None(InitialStamped(observation), MonkHeldComboDecisionReason.CarrierUnavailable);
        if (observation.Candidate is not { } candidate)
            return None(InitialStamped(observation), MonkHeldComboDecisionReason.CandidateUnavailable);
        if (!IsInitialCandidate(observation, candidate))
            return None(InitialStamped(observation), MonkHeldComboDecisionReason.CandidateInvalid);
        if (!observation.HeldGameplayKeyEligible ||
            observation.HeldGameplayKeyCode <= 0)
        {
            return None(InitialStamped(observation), MonkHeldComboDecisionReason.NoHeldGameplayKey);
        }

        var intent = new MonkHeldComboIntent(
            observation.Context,
            candidate.EnemySlot,
            observation.LocalPlayer,
            candidate.Actor,
            PhantomRushComboRouteId,
            observation.HeldGameplayKeyCode);
        if (!intent.IsValid)
            return Cancelled(InitialStamped(observation), MonkHeldComboDecisionReason.CandidateInvalid);

        var active = new MonkHeldComboState(
            MonkHeldComboPhase.Active,
            intent,
            observation.ResolvedComboActionId,
            0,
            MonkHeldComboActionPurpose.None,
            HeldActionRetryState.Initial,
            0,
            false,
            false,
            false,
            observation.NowMilliseconds,
            ClientActionAttemptOutcome.None);
        return EvaluateActive(active, observation, candidate);
    }

    private static MonkHeldComboDecision ObserveBuffered(
        MonkHeldComboState state,
        MonkHeldComboObservation observation,
        MonkHeldComboCandidate candidate)
    {
        if (observation.ResolvedComboActionId != state.CarrierActionId)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.CarrierDrift);
        }

        if (state.PressurePointConfirmed &&
            state.PendingPurpose is
                MonkHeldComboActionPurpose.ThunderclapReturn or
                MonkHeldComboActionPurpose.RisingPhoenixBuff or
                MonkHeldComboActionPurpose.PhantomRushFinish &&
            !candidate.HasExactOwnPressurePoint)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.PressurePointMissing);
        }

        if (state.FireResonanceConfirmed &&
            state.PendingPurpose ==
                MonkHeldComboActionPurpose.PhantomRushFinish &&
            !observation.HasExactOwnFireResonance)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.FireResonanceMissing);
        }

        if (!IsPendingActionReady(state, observation, candidate))
        {
            var reason = IsPendingTargetReady(state, candidate)
                ? MonkHeldComboDecisionReason.ActionNotReady
                : MonkHeldComboDecisionReason.TargetNotReady;
            return Armed(Stamp(state, observation), reason, inputClaimed: false);
        }

        return EvaluateBufferedBoundary(Stamp(state, observation), observation);
    }

    private static MonkHeldComboDecision ObserveCarrierTransition(
        MonkHeldComboState state,
        MonkHeldComboObservation observation,
        MonkHeldComboCandidate candidate)
    {
        if (observation.ResolvedComboActionId == state.CarrierActionId)
        {
            return Armed(
                Stamp(state, observation),
                MonkHeldComboDecisionReason.CarrierTransitionPending,
                inputClaimed: false);
        }

        if (observation.ResolvedComboActionId !=
            GetExpectedNextComboAction(state.CarrierActionId))
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.CarrierDrift);
        }

        var active = Stamp(state with
        {
            Phase = MonkHeldComboPhase.Active,
            CarrierActionId = observation.ResolvedComboActionId,
            PendingActionId = 0,
            PendingPurpose = MonkHeldComboActionPurpose.None,
            Retry = HeldActionRetryState.Initial,
            ConfirmationSequenceBaseline = 0,
            PressurePointConfirmed = false,
            FireResonanceConfirmed = false,
            ThunderclapUsed = false,
        }, observation);
        return EvaluateActive(active, observation, candidate);
    }

    private static MonkHeldComboDecision ObservePressurePoint(
        MonkHeldComboState state,
        MonkHeldComboObservation observation,
        MonkHeldComboCandidate candidate)
    {
        if (observation.ResolvedComboActionId != PhantomRushActionId)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.CarrierDrift);
        }
        if (candidate.HasExactOwnPressurePoint)
        {
            return ObservePhantomRange(
                Stamp(state with
                {
                    Phase = MonkHeldComboPhase.AwaitPhantomRange,
                    PressurePointConfirmed = true,
                    ConfirmationSequenceBaseline = 0,
                }, observation),
                observation,
                candidate);
        }

        if (observation.ConfirmationBoundaryReopened)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.PressurePointMissing);
        }

        return Armed(
            Stamp(state, observation),
            MonkHeldComboDecisionReason.PressurePointPending,
            inputClaimed: false);
    }

    private static MonkHeldComboDecision ObservePhantomRange(
        MonkHeldComboState state,
        MonkHeldComboObservation observation,
        MonkHeldComboCandidate candidate)
    {
        if (observation.ResolvedComboActionId != PhantomRushActionId)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.CarrierDrift);
        }
        if (state.PressurePointConfirmed &&
            !candidate.HasExactOwnPressurePoint)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.PressurePointMissing);
        }
        if (candidate.PhantomRushTargetReady)
        {
            if (observation.HasExactOwnFireResonance)
            {
                return BufferAction(
                    state with { FireResonanceConfirmed = true },
                    observation,
                    PhantomRushActionId,
                    MonkHeldComboActionPurpose.PhantomRushFinish);
            }

            if (observation.RisingPhoenixLocallyReady)
            {
                return BufferAction(
                    state,
                    observation,
                    RisingPhoenixActionId,
                    MonkHeldComboActionPurpose.RisingPhoenixBuff);
            }

            // Never stall or spend Thunderclap merely because the reserved
            // Phoenix charge is unavailable.
            return BufferAction(
                state,
                observation,
                PhantomRushActionId,
                MonkHeldComboActionPurpose.PhantomRushFinish);
        }

        if (state.PressurePointConfirmed &&
            candidate.HasExactOwnPressurePoint &&
            !state.ThunderclapUsed &&
            observation.ThunderclapLocallyReady &&
            candidate.ThunderclapTargetReady)
        {
            return BufferAction(
                state,
                observation,
                ThunderclapActionId,
                MonkHeldComboActionPurpose.ThunderclapReturn);
        }

        return Armed(
            Stamp(state with { Phase = MonkHeldComboPhase.AwaitPhantomRange }, observation),
            MonkHeldComboDecisionReason.TargetNotReady,
            inputClaimed: false);
    }

    private static MonkHeldComboDecision ObserveFireResonance(
        MonkHeldComboState state,
        MonkHeldComboObservation observation,
        MonkHeldComboCandidate candidate)
    {
        if (observation.ResolvedComboActionId != PhantomRushActionId)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.CarrierDrift);
        }
        if (state.PressurePointConfirmed &&
            !candidate.HasExactOwnPressurePoint)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.PressurePointMissing);
        }
        if (observation.HasExactOwnFireResonance)
        {
            if (!candidate.PhantomRushTargetReady)
            {
                return Armed(
                    Stamp(state, observation),
                    MonkHeldComboDecisionReason.TargetNotReady,
                    inputClaimed: false);
            }

            return BufferAction(
                state with { FireResonanceConfirmed = true },
                observation,
                PhantomRushActionId,
                MonkHeldComboActionPurpose.PhantomRushFinish);
        }

        if (observation.ConfirmationBoundaryReopened)
        {
            return Cancelled(
                MonkHeldComboState.Initial,
                MonkHeldComboDecisionReason.FireResonanceMissing);
        }

        return Armed(
            Stamp(state, observation),
            MonkHeldComboDecisionReason.FireResonancePending,
            inputClaimed: false);
    }

    private static MonkHeldComboDecision EvaluateActive(
        MonkHeldComboState state,
        MonkHeldComboObservation observation,
        MonkHeldComboCandidate candidate)
    {
        if (IsNormalComboAction(observation.ResolvedComboActionId))
        {
            if (observation.ComboActionLocallyReady &&
                candidate.ComboTargetReady)
            {
                return BufferAction(
                    state,
                    observation,
                    observation.ResolvedComboActionId,
                    MonkHeldComboActionPurpose.NormalCombo);
            }

            if (observation.FireReplyLocallyReady &&
                !candidate.ComboTargetReady &&
                candidate.FireReplyTargetReady &&
                !observation.HasExactOwnFireResonance)
            {
                return BufferAction(
                    state,
                    observation,
                    FireReplyActionId,
                    MonkHeldComboActionPurpose.FireReplyFallback);
            }

            return Armed(
                state,
                observation.ComboActionLocallyReady ||
                observation.FireReplyLocallyReady
                    ? MonkHeldComboDecisionReason.TargetNotReady
                    : MonkHeldComboDecisionReason.ActionNotReady,
                inputClaimed: false);
        }

        if (observation.ResolvedComboActionId != PhantomRushActionId)
        {
            return Armed(
                state,
                MonkHeldComboDecisionReason.CarrierUnavailable,
                inputClaimed: false);
        }

        if (observation.HasExactOwnFireResonance)
        {
            return ObserveFireResonance(
                state with { Phase = MonkHeldComboPhase.AwaitFireResonance },
                observation,
                candidate);
        }

        if (candidate.HasExactOwnPressurePoint)
        {
            return ObservePhantomRange(
                state with
                {
                    Phase = MonkHeldComboPhase.AwaitPhantomRange,
                    PressurePointConfirmed = true,
                },
                observation,
                candidate);
        }

        if (observation.WindReplyLocallyReady &&
            candidate.WindReplyTargetReady)
        {
            return BufferAction(
                state,
                observation,
                WindReplyActionId,
                MonkHeldComboActionPurpose.WindReplySetup);
        }

        return ObservePhantomRange(
            state with
            {
                Phase = MonkHeldComboPhase.AwaitPhantomRange,
                PressurePointConfirmed = false,
            },
            observation,
            candidate);
    }

    private static MonkHeldComboDecision BufferAction(
        MonkHeldComboState state,
        MonkHeldComboObservation observation,
        uint actionId,
        MonkHeldComboActionPurpose purpose)
    {
        var buffered = Stamp(state with
        {
            Phase = MonkHeldComboPhase.BufferedAction,
            CarrierActionId = observation.ResolvedComboActionId,
            PendingActionId = actionId,
            PendingPurpose = purpose,
            Retry = HeldActionRetryState.Initial,
            LastNativeOutcome = ClientActionAttemptOutcome.None,
        }, observation);
        return EvaluateBufferedBoundary(buffered, observation);
    }

    private static MonkHeldComboDecision EvaluateBufferedBoundary(
        MonkHeldComboState state,
        MonkHeldComboObservation observation)
    {
        if (!observation.NativeBoundaryReady)
        {
            return Armed(
                state,
                MonkHeldComboDecisionReason.NativeBoundaryUnavailable,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    state.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: true,
                    targetSpecificReady: true));
        }

        if (!HeldActionRetryRules.CanAttemptFrozenIntent(
                state.Retry,
                observation.NowMilliseconds))
        {
            return Armed(
                state,
                MonkHeldComboDecisionReason.NativeRetryThrottle,
                HeldActionRetryRules.RetainsSchedulerFrame(
                    state.Retry,
                    observation.NowMilliseconds,
                    exactIntentValid: true,
                    actionSpecificReady: true,
                    targetSpecificReady: true));
        }

        return new MonkHeldComboDecision(
            state,
            MonkHeldComboDecisionKind.Dispatch,
            MonkHeldComboDecisionReason.None,
            state.Intent,
            state.PendingActionId,
            state.PendingPurpose,
            InputClaimed: true);
    }

    private static MonkHeldComboState CompleteAcceptedAction(
        MonkHeldComboState state,
        long nowMilliseconds,
        ushort confirmationSequenceBaseline)
    {
        var common = state with
        {
            PendingActionId = 0,
            PendingPurpose = MonkHeldComboActionPurpose.None,
            Retry = HeldActionRetryState.Initial,
            LastObservedAtMilliseconds = nowMilliseconds,
            LastNativeOutcome = ClientActionAttemptOutcome.ClientAccepted,
        };
        return state.PendingPurpose switch
        {
            MonkHeldComboActionPurpose.NormalCombo => common with
            {
                Phase = MonkHeldComboPhase.AwaitCarrierTransition,
            },
            MonkHeldComboActionPurpose.FireReplyFallback => common with
            {
                Phase = MonkHeldComboPhase.Active,
            },
            MonkHeldComboActionPurpose.WindReplySetup => common with
            {
                Phase = MonkHeldComboPhase.AwaitPressurePoint,
                ConfirmationSequenceBaseline = confirmationSequenceBaseline,
                PressurePointConfirmed = false,
                FireResonanceConfirmed = false,
                ThunderclapUsed = false,
            },
            MonkHeldComboActionPurpose.ThunderclapReturn => common with
            {
                Phase = MonkHeldComboPhase.AwaitPhantomRange,
                ThunderclapUsed = true,
            },
            MonkHeldComboActionPurpose.RisingPhoenixBuff => common with
            {
                Phase = MonkHeldComboPhase.AwaitFireResonance,
                ConfirmationSequenceBaseline = confirmationSequenceBaseline,
                FireResonanceConfirmed = false,
            },
            MonkHeldComboActionPurpose.PhantomRushFinish =>
                MonkHeldComboState.Initial,
            _ => MonkHeldComboState.Initial,
        };
    }

    private static bool IsPendingActionReady(
        MonkHeldComboState state,
        MonkHeldComboObservation observation,
        MonkHeldComboCandidate candidate) =>
        state.PendingPurpose switch
        {
            MonkHeldComboActionPurpose.NormalCombo =>
                state.PendingActionId == observation.ResolvedComboActionId &&
                observation.ComboActionLocallyReady &&
                candidate.ComboTargetReady,
            MonkHeldComboActionPurpose.FireReplyFallback =>
                state.PendingActionId == FireReplyActionId &&
                observation.FireReplyLocallyReady &&
                !observation.HasExactOwnFireResonance &&
                candidate.FireReplyTargetReady,
            MonkHeldComboActionPurpose.WindReplySetup =>
                state.PendingActionId == WindReplyActionId &&
                observation.ResolvedComboActionId == PhantomRushActionId &&
                observation.WindReplyLocallyReady &&
                candidate.WindReplyTargetReady,
            MonkHeldComboActionPurpose.ThunderclapReturn =>
                state.PendingActionId == ThunderclapActionId &&
                observation.ResolvedComboActionId == PhantomRushActionId &&
                observation.ThunderclapLocallyReady &&
                (!state.PressurePointConfirmed ||
                 candidate.HasExactOwnPressurePoint) &&
                candidate.HasExactOwnPressurePoint &&
                candidate.ThunderclapTargetReady,
            MonkHeldComboActionPurpose.RisingPhoenixBuff =>
                state.PendingActionId == RisingPhoenixActionId &&
                observation.ResolvedComboActionId == PhantomRushActionId &&
                observation.RisingPhoenixLocallyReady &&
                !observation.HasExactOwnFireResonance &&
                (!state.PressurePointConfirmed ||
                 candidate.HasExactOwnPressurePoint) &&
                candidate.PhantomRushTargetReady,
            MonkHeldComboActionPurpose.PhantomRushFinish =>
                state.PendingActionId == PhantomRushActionId &&
                observation.ResolvedComboActionId == PhantomRushActionId &&
                observation.ComboActionLocallyReady &&
                (!state.PressurePointConfirmed ||
                 candidate.HasExactOwnPressurePoint) &&
                (!state.FireResonanceConfirmed ||
                 observation.HasExactOwnFireResonance) &&
                candidate.PhantomRushTargetReady,
            _ => false,
        };

    private static bool IsPendingTargetReady(
        MonkHeldComboState state,
        MonkHeldComboCandidate candidate) =>
        state.PendingPurpose switch
        {
            MonkHeldComboActionPurpose.NormalCombo => candidate.ComboTargetReady,
            MonkHeldComboActionPurpose.FireReplyFallback => candidate.FireReplyTargetReady,
            MonkHeldComboActionPurpose.WindReplySetup => candidate.WindReplyTargetReady,
            MonkHeldComboActionPurpose.ThunderclapReturn =>
                candidate.HasExactOwnPressurePoint &&
                candidate.ThunderclapTargetReady,
            MonkHeldComboActionPurpose.RisingPhoenixBuff =>
                candidate.PhantomRushTargetReady,
            MonkHeldComboActionPurpose.PhantomRushFinish =>
                candidate.PhantomRushTargetReady,
            _ => false,
        };

    private static MonkHeldComboDecisionReason GetPermanentGateFailure(
        MonkHeldComboObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return MonkHeldComboDecisionReason.ConfigurationDisabled;
        if (observation.Context is not
            (SupportedPvPContext.CrystallineConflict or
             SupportedPvPContext.WolvesDen))
        {
            return MonkHeldComboDecisionReason.OutsideSupportedPvPContext;
        }
        if (!observation.LocalPlayer.IsValid)
            return MonkHeldComboDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return MonkHeldComboDecisionReason.LocalPlayerDead;
        if (observation.LocalJobId != MonkJobId)
            return MonkHeldComboDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified)
            return MonkHeldComboDecisionReason.MetadataUnverified;
        if (!observation.InputProbeSucceeded)
            return MonkHeldComboDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return MonkHeldComboDecisionReason.TextInputActive;
        return MonkHeldComboDecisionReason.None;
    }

    private static bool IsInitialCandidate(
        MonkHeldComboObservation observation,
        MonkHeldComboCandidate candidate) =>
        IsStructurallyValidCandidate(observation.Context, candidate) &&
        IsRouteReachable(
            observation.ResolvedComboActionId,
            observation.FireReplyLocallyReady,
            observation.WindReplyLocallyReady,
            observation.ThunderclapLocallyReady,
            observation.HasExactOwnFireResonance,
            candidate);

    private static bool IsExactCandidate(
        MonkHeldComboIntent intent,
        MonkHeldComboCandidate candidate) =>
        candidate.Context == intent.Context &&
        candidate.EnemySlot == intent.EnemySlot &&
        candidate.Actor == intent.Target &&
        IsStructurallyValidCandidate(intent.Context, candidate);

    private static bool IsStructurallyValidCandidate(
        SupportedPvPContext context,
        MonkHeldComboCandidate candidate) =>
        candidate.Context == context &&
        IsContextSlotValid(context, candidate.EnemySlot) &&
        candidate.Actor.IsValid &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.CurrentHp is > 0 &&
        candidate.MaxHp > 0 &&
        candidate.CurrentHp <= candidate.MaxHp;

    private static bool IsRouteReachable(
        uint resolvedComboActionId,
        bool fireReplyLocallyReady,
        bool windReplyLocallyReady,
        bool thunderclapLocallyReady,
        bool hasExactOwnFireResonance,
        MonkHeldComboCandidate candidate)
    {
        if (IsNormalComboAction(resolvedComboActionId))
        {
            return candidate.ComboTargetReady ||
                   fireReplyLocallyReady &&
                   !hasExactOwnFireResonance &&
                   candidate.FireReplyTargetReady;
        }

        if (resolvedComboActionId != PhantomRushActionId) return false;
        if (candidate.PhantomRushTargetReady) return true;
        if (hasExactOwnFireResonance) return false;
        return windReplyLocallyReady && candidate.WindReplyTargetReady ||
               candidate.HasExactOwnPressurePoint &&
               thunderclapLocallyReady &&
               candidate.ThunderclapTargetReady;
    }

    private static bool HasAny(
        IReadOnlyList<MonkHeldComboCandidate> candidates,
        SupportedPvPContext context,
        Func<MonkHeldComboCandidate, bool> predicate)
    {
        foreach (var candidate in candidates)
        {
            if (IsStructurallyValidCandidate(context, candidate) &&
                predicate(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static int CompareCandidates(
        MonkHeldComboCandidate left,
        MonkHeldComboCandidate right)
    {
        var leftRatio = (ulong)left.CurrentHp * right.MaxHp;
        var rightRatio = (ulong)right.CurrentHp * left.MaxHp;
        var ratio = leftRatio.CompareTo(rightRatio);
        if (ratio != 0) return ratio;
        var slot = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slot != 0) return slot;
        var objectId = left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId);
        return objectId != 0
            ? objectId
            : left.Actor.EntityId.CompareTo(right.Actor.EntityId);
    }

    private static MonkHeldComboState Stamp(
        MonkHeldComboState state,
        MonkHeldComboObservation observation) =>
        state with { LastObservedAtMilliseconds = observation.NowMilliseconds };

    private static MonkHeldComboState InitialStamped(
        MonkHeldComboObservation observation) =>
        MonkHeldComboState.Initial with
        {
            LastObservedAtMilliseconds = observation.NowMilliseconds,
        };

    private static MonkHeldComboNativeAttemptDecision TerminalUnknown() => new(
        MonkHeldComboState.Initial,
        MonkHeldComboDecisionReason.NativeAcceptanceUnknown,
        HeldActionRetryDisposition.AmbiguousTerminal,
        false, false, true, false);

    private static MonkHeldComboDecision Armed(
        MonkHeldComboState state,
        MonkHeldComboDecisionReason reason,
        bool inputClaimed) => new(
        state,
        MonkHeldComboDecisionKind.Armed,
        reason,
        state.Intent,
        state.PendingActionId,
        state.PendingPurpose,
        inputClaimed);

    private static MonkHeldComboDecision None(
        MonkHeldComboState state,
        MonkHeldComboDecisionReason reason) => new(
        state,
        MonkHeldComboDecisionKind.None,
        reason);

    private static MonkHeldComboDecision Cancelled(
        MonkHeldComboState state,
        MonkHeldComboDecisionReason reason) => new(
        state,
        MonkHeldComboDecisionKind.Cancelled,
        reason);
}
