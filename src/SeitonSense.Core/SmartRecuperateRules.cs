namespace SeitonSense.Core;

public enum SmartRecuperateTriggerKind : byte
{
    None = 0,
    HeldGameplayKey = 1,
    Automatic = 2,
}

/// <summary>
/// One exact self-only Recuperate episode. The action, local actor, consent
/// source, and health event are frozen once and are never substituted by a retry.
/// </summary>
public readonly record struct SmartRecuperateIntent(
    uint ActionId,
    TargetPressureActorIdentity LocalPlayer,
    SupportedPvPContext Context,
    int FrozenKeyCode,
    uint TriggerCurrentHp,
    uint TriggerMaximumHp,
    ulong HealthEventToken,
    SmartRecuperateTriggerKind TriggerKind =
        SmartRecuperateTriggerKind.HeldGameplayKey)
{
    public bool IsAutomatic =>
        TriggerKind == SmartRecuperateTriggerKind.Automatic;

    public bool IsValid =>
        ActionId == SmartRecuperateRules.ActionId &&
        LocalPlayer.IsValid &&
        Context is SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen &&
        ((IsAutomatic && FrozenKeyCode == 0) ||
         (TriggerKind == SmartRecuperateTriggerKind.HeldGameplayKey &&
          FrozenKeyCode > 0)) &&
        HealthEventToken != 0 &&
        SmartRecuperateRules.HasMinimumMissingHp(
            TriggerCurrentHp,
            TriggerMaximumHp);
}

public enum SmartRecuperatePhase : byte
{
    Waiting = 0,
    Buffered = 1,
    WaitingForAcceptedCooldownUnavailable = 2,
    WaitingForAcceptedCooldownReady = 3,
    // Held intents remain spent until the exact key is released. Automatic
    // intents remain spent until their low-HP opportunity has ended. Keeping
    // both consent kinds in one terminal phase prevents parallel retry lanes.
    SpentUntilKeyRelease = 4,
}

public readonly record struct SmartRecuperateState(
    SmartRecuperatePhase Phase,
    SmartRecuperateIntent? Intent,
    HeldActionRetryState Retry,
    ulong NextHealthEventToken,
    long LastObservedAtMilliseconds,
    ClientActionAttemptOutcome LastNativeOutcome)
{
    public static SmartRecuperateState Initial => new(
        SmartRecuperatePhase.Waiting,
        null,
        HeldActionRetryState.Initial,
        1,
        -1,
        ClientActionAttemptOutcome.None);
}

public readonly record struct SmartRecuperateObservation(
    bool ConfigurationEnabled,
    SupportedPvPContext Context,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    bool IsLocalPlayerTargetable,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    uint ResolvedActionId,
    bool ActionLocallyReady,
    uint CurrentHp,
    uint MaximumHp,
    uint CurrentMp,
    uint MaximumMp,
    bool HardReset = false,
    int HeldGameplayKeyCode = 0,
    bool FrozenKeyStillDown = true,
    bool NativeBoundaryReady = true,
    bool ActionCooldownReady = true,
    long NowMilliseconds = 0,
    bool HeldModeEnabled = true,
    bool AutomaticModeEnabled = false);

public enum SmartRecuperateDecisionKind
{
    None = 0,
    Dispatch = 1,
    Cancelled = 2,
    Armed = 3,
}

public enum SmartRecuperateDecisionReason
{
    None = 0,
    HardReset = 1,
    ConfigurationDisabled = 2,
    OutsideSupportedPvPContext = 3,
    LocalPlayerIdentityInvalid = 4,
    LocalPlayerDead = 5,
    LocalPlayerUntargetable = 6,
    MetadataUnverified = 7,
    GuardSuppressed = 8,
    HigherPriorityClaimed = 9,
    InputProbeUnavailable = 10,
    TextInputActive = 11,
    NoHeldGameplayKey = 12,
    ResolvedActionInvalid = 13,
    ActionNotReady = 14,
    HealthTelemetryInvalid = 15,
    MissingHealthBelowThreshold = 16,
    MpTelemetryInvalid = 17,
    InsufficientMp = 18,
    ExactKeyReleased = 19,
    NativeBoundaryUnavailable = 20,
    NativeRetryThrottle = 21,
    NativeRetryLimitReached = 22,
    NativeAcceptanceUnknown = 23,
    WaitingForAcceptedCooldownUnavailable = 24,
    WaitingForAcceptedCooldownReady = 25,
    ClockMovedBackwards = 26,
    ContextChanged = 27,
    TriggerModeDisabled = 28,
}

public readonly record struct SmartRecuperateDecision(
    SmartRecuperateState NextState,
    SmartRecuperateDecisionKind Kind,
    SmartRecuperateDecisionReason Reason,
    SmartRecuperateIntent? Intent = null,
    bool InputClaimed = false)
{
    public bool ShouldDispatch =>
        Kind == SmartRecuperateDecisionKind.Dispatch &&
        Intent is { IsValid: true };

    /// <summary>
    /// This claims only the current framework frame. It deliberately does not
    /// consume the physical key generation through release.
    /// </summary>
    public bool ShouldConsumeInputGeneration => InputClaimed;
}

public readonly record struct SmartRecuperateNativeAttemptDecision(
    SmartRecuperateState NextState,
    SmartRecuperateDecisionReason Reason,
    bool RetryScheduled,
    bool ClientAccepted,
    bool Terminal,
    bool SoftWait = false);

/// <summary>
/// Stateful policy shared by held and automatic Smart Recuperate. A proven
/// client false is retried at the shared 50 ms cadence. Automatic consent
/// freezes the currently configured latency-response budget when its exact
/// health episode is created. Known local unavailability spends no retry budget. A
/// successful request cannot repeat until its accepted cooldown has first been
/// observed unavailable and then ready again.
/// </summary>
public static class SmartRecuperateRules
{
    public const uint ActionId = 29_711;
    public const uint MinimumMissingHp = 16_000;
    public const uint MpCost = 2_000;

    /// <summary>
    /// Higher-priority recovery yields only to exact live Guard. A provisional
    /// hook observation is deliberately not an input to this rule.
    /// </summary>
    public static bool ShouldSuppressForOwnGuard(bool exactGuardActive) =>
        exactGuardActive;

    public static SmartRecuperateDecision Observe(
        SmartRecuperateObservation observation) =>
        Observe(SmartRecuperateState.Initial, observation);

    public static SmartRecuperateDecision Observe(
        SmartRecuperateState previous,
        SmartRecuperateObservation observation)
    {
        if (observation.HardReset)
            return Cancelled(
                SmartRecuperateState.Initial,
                SmartRecuperateDecisionReason.HardReset);

        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds))
        {
            return Cancelled(
                SmartRecuperateState.Initial,
                SmartRecuperateDecisionReason.ClockMovedBackwards);
        }

        if (previous.Phase is
            SmartRecuperatePhase.WaitingForAcceptedCooldownUnavailable or
            SmartRecuperatePhase.WaitingForAcceptedCooldownReady)
        {
            return ObserveAcceptedCooldown(previous, observation);
        }

        var permanentFailure = GetPermanentGateFailure(observation);
        if (permanentFailure != SmartRecuperateDecisionReason.None)
            return None(
                SmartRecuperateState.Initial,
                permanentFailure);

        if (previous.Phase == SmartRecuperatePhase.SpentUntilKeyRelease)
        {
            if (previous.Intent is { IsValid: true } spentIntent)
            {
                if (spentIntent.IsAutomatic)
                {
                    if (!observation.AutomaticModeEnabled)
                    {
                        return None(
                            Waiting(
                                previous.NextHealthEventToken,
                                observation.NowMilliseconds),
                            SmartRecuperateDecisionReason.TriggerModeDisabled);
                    }

                    if (!HasValidHealth(
                            observation.CurrentHp,
                            observation.MaximumHp))
                    {
                        return None(
                            Stamp(previous, observation.NowMilliseconds),
                            SmartRecuperateDecisionReason.HealthTelemetryInvalid);
                    }

                    if (!HasMinimumMissingHp(
                            observation.CurrentHp,
                            observation.MaximumHp))
                    {
                        return None(
                            Waiting(
                                previous.NextHealthEventToken,
                                observation.NowMilliseconds),
                            SmartRecuperateDecisionReason.MissingHealthBelowThreshold);
                    }
                }
                else if (!observation.HeldModeEnabled ||
                         !observation.FrozenKeyStillDown ||
                         spentIntent.FrozenKeyCode <= 0)
                {
                    return None(
                        Waiting(
                            previous.NextHealthEventToken,
                            observation.NowMilliseconds),
                        !observation.HeldModeEnabled
                            ? SmartRecuperateDecisionReason.TriggerModeDisabled
                            : SmartRecuperateDecisionReason.ExactKeyReleased);
                }

                return None(
                    Stamp(previous, observation.NowMilliseconds),
                    previous.LastNativeOutcome ==
                    ClientActionAttemptOutcome.AcceptanceUnknown
                        ? SmartRecuperateDecisionReason.NativeAcceptanceUnknown
                        : SmartRecuperateDecisionReason.NativeRetryLimitReached);
            }

            return None(
                Waiting(previous.NextHealthEventToken, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.NativeAcceptanceUnknown);
        }

        if (previous.Phase == SmartRecuperatePhase.Buffered)
            return ObserveBuffered(previous, observation);

        return TryCreateIntent(previous, observation);
    }

    public static SmartRecuperateNativeAttemptDecision ApplyNativeAttemptOutcome(
        SmartRecuperateState current,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds)
    {
        if (current.Phase != SmartRecuperatePhase.Buffered ||
            current.Intent is not { IsValid: true } ||
            nowMilliseconds < 0 ||
            (current.LastObservedAtMilliseconds >= 0 &&
             nowMilliseconds < current.LastObservedAtMilliseconds))
        {
            return TerminalUnknown(current, nowMilliseconds);
        }

        var shared = HeldActionRetryRules.Complete(
            current.Retry,
            nowMilliseconds,
            outcome);
        if (shared.Disposition == HeldActionRetryDisposition.SoftWait)
        {
            return new SmartRecuperateNativeAttemptDecision(
                Stamp(current with { LastNativeOutcome = outcome }, nowMilliseconds),
                SmartRecuperateDecisionReason.NativeBoundaryUnavailable,
                false,
                false,
                false,
                true);
        }

        if (shared.Disposition == HeldActionRetryDisposition.AcceptedTerminal)
        {
            return new SmartRecuperateNativeAttemptDecision(
                Stamp(current with
                {
                    Phase = SmartRecuperatePhase.WaitingForAcceptedCooldownUnavailable,
                    Retry = HeldActionRetryState.Initial,
                    LastNativeOutcome = outcome,
                }, nowMilliseconds),
                SmartRecuperateDecisionReason.None,
                false,
                true,
                true);
        }

        if (shared.Disposition == HeldActionRetryDisposition.RetryScheduled)
        {
            return new SmartRecuperateNativeAttemptDecision(
                Stamp(current with
                {
                    Retry = shared.NextState,
                    LastNativeOutcome = outcome,
                }, nowMilliseconds),
                SmartRecuperateDecisionReason.NativeRetryThrottle,
                true,
                false,
                false);
        }

        if (shared.Disposition == HeldActionRetryDisposition.RejectedTerminal)
        {
            return new SmartRecuperateNativeAttemptDecision(
                Stamp(current with
                {
                    Phase = SmartRecuperatePhase.SpentUntilKeyRelease,
                    Retry = HeldActionRetryState.Initial,
                    LastNativeOutcome = outcome,
                }, nowMilliseconds),
                SmartRecuperateDecisionReason.NativeRetryLimitReached,
                false,
                false,
                true);
        }

        return TerminalUnknown(current, nowMilliseconds);
    }

    public static bool HasValidHealth(uint currentHp, uint maximumHp) =>
        currentHp > 0 && maximumHp > 0 && currentHp <= maximumHp;

    public static bool HasMinimumMissingHp(
        uint currentHp,
        uint maximumHp) =>
        HasValidHealth(currentHp, maximumHp) &&
        (ulong)maximumHp - currentHp >= MinimumMissingHp;

    public static uint GetMissingHp(uint currentHp, uint maximumHp) =>
        HasValidHealth(currentHp, maximumHp)
            ? maximumHp - currentHp
            : 0;

    public static bool HasValidMp(uint currentMp, uint maximumMp) =>
        maximumMp > 0 && currentMp <= maximumMp;

    public static bool HasMinimumMp(uint currentMp, uint maximumMp) =>
        HasValidMp(currentMp, maximumMp) && currentMp >= MpCost;

    /// <summary>
    /// Final exact-intent validation. Local cooldown/resources and the native
    /// queue boundary are checked separately by the runtime so known temporary
    /// unavailability can remain a zero-budget soft wait.
    /// </summary>
    public static bool CanUseFrozenIntent(
        SmartRecuperateIntent intent,
        bool configurationEnabled,
        SupportedPvPContext currentContext,
        TargetPressureActorIdentity currentLocalPlayer,
        bool isLocalPlayerAlive,
        bool isLocalPlayerTargetable,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        uint resolvedActionId,
        bool actionLocallyReady,
        uint currentHp,
        uint maximumHp,
        uint currentMp,
        uint maximumMp,
        int currentHeldKeyCode,
        bool frozenKeyStillDown,
        bool heldModeEnabled = true,
        bool automaticModeEnabled = false) =>
        intent.IsValid &&
        configurationEnabled &&
        (intent.IsAutomatic
            ? automaticModeEnabled
            : heldModeEnabled) &&
        currentContext == intent.Context &&
        currentLocalPlayer == intent.LocalPlayer &&
        isLocalPlayerAlive &&
        isLocalPlayerTargetable &&
        metadataVerified &&
        !actionHelpersSuppressedByGuard &&
        !higherPriorityClaimed &&
        resolvedActionId == intent.ActionId &&
        actionLocallyReady &&
        (intent.IsAutomatic ||
         (currentHeldKeyCode == intent.FrozenKeyCode &&
          frozenKeyStillDown)) &&
        maximumHp == intent.TriggerMaximumHp &&
        HasMinimumMissingHp(currentHp, maximumHp) &&
        HasMinimumMp(currentMp, maximumMp);

    private static SmartRecuperateDecision ObserveBuffered(
        SmartRecuperateState previous,
        SmartRecuperateObservation observation)
    {
        var intent = previous.Intent;
        if (intent is not { IsValid: true })
            return Cancelled(
                SmartRecuperateState.Initial,
                SmartRecuperateDecisionReason.NativeAcceptanceUnknown);

        if (observation.Context != intent.Value.Context)
        {
            return Cancelled(
                Waiting(previous.NextHealthEventToken, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.ContextChanged);
        }

        if ((intent.Value.IsAutomatic && !observation.AutomaticModeEnabled) ||
            (!intent.Value.IsAutomatic && !observation.HeldModeEnabled))
        {
            return None(
                Waiting(previous.NextHealthEventToken, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.TriggerModeDisabled);
        }

        if (!intent.Value.IsAutomatic && !observation.FrozenKeyStillDown)
        {
            return None(
                Waiting(previous.NextHealthEventToken, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.ExactKeyReleased);
        }

        if (observation.HigherPriorityClaimed)
            return None(
                Stamp(previous, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.HigherPriorityClaimed);
        if (observation.ActionHelpersSuppressedByGuard)
            return None(
                Stamp(previous, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.GuardSuppressed);
        if (observation.ResolvedActionId != intent.Value.ActionId)
            return Spent(
                previous,
                observation.NowMilliseconds,
                ClientActionAttemptOutcome.NotInvoked,
                SmartRecuperateDecisionReason.ResolvedActionInvalid);
        if (!HasValidHealth(observation.CurrentHp, observation.MaximumHp) ||
            observation.MaximumHp != intent.Value.TriggerMaximumHp)
            return Spent(
                previous,
                observation.NowMilliseconds,
                ClientActionAttemptOutcome.NotInvoked,
                SmartRecuperateDecisionReason.HealthTelemetryInvalid);
        if (!HasMinimumMissingHp(observation.CurrentHp, observation.MaximumHp))
        {
            return None(
                Waiting(previous.NextHealthEventToken, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.MissingHealthBelowThreshold);
        }
        if (!HasValidMp(observation.CurrentMp, observation.MaximumMp))
            return Spent(
                previous,
                observation.NowMilliseconds,
                ClientActionAttemptOutcome.NotInvoked,
                SmartRecuperateDecisionReason.MpTelemetryInvalid);
        if (!observation.ActionLocallyReady ||
            !HasMinimumMp(observation.CurrentMp, observation.MaximumMp))
        {
            return None(
                Stamp(previous, observation.NowMilliseconds),
                !HasMinimumMp(observation.CurrentMp, observation.MaximumMp)
                    ? SmartRecuperateDecisionReason.InsufficientMp
                    : SmartRecuperateDecisionReason.ActionNotReady);
        }
        if (!observation.NativeBoundaryReady)
        {
            return Armed(
                Stamp(previous, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.NativeBoundaryUnavailable);
        }
        if (!CanAttemptFrozenRetryBudget(
                previous.Retry,
                observation.NowMilliseconds))
        {
            return Armed(
                Stamp(previous, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.NativeRetryThrottle);
        }

        return Dispatch(Stamp(previous, observation.NowMilliseconds), intent.Value);
    }

    private static SmartRecuperateDecision ObserveAcceptedCooldown(
        SmartRecuperateState previous,
        SmartRecuperateObservation observation)
    {
        var intent = previous.Intent;
        if (intent is not { IsValid: true })
        {
            return Cancelled(
                SmartRecuperateState.Initial,
                SmartRecuperateDecisionReason.NativeAcceptanceUnknown);
        }

        if (observation.Context != intent.Value.Context)
        {
            return Cancelled(
                Waiting(previous.NextHealthEventToken, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.ContextChanged);
        }

        // This is a passive anti-duplicate latch. Temporary configuration,
        // input, Guard, priority, resource, health, and key changes must not
        // hide the accepted cooldown's unavailable edge. An unreadable or
        // changed action identity therefore waits instead of spending the
        // latch or guessing that the cooldown became unavailable.
        if (observation.ResolvedActionId != intent.Value.ActionId)
        {
            return None(
                Stamp(previous, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.ResolvedActionInvalid);
        }

        if (previous.Phase ==
            SmartRecuperatePhase.WaitingForAcceptedCooldownUnavailable)
        {
            if (observation.ActionCooldownReady)
            {
                return None(
                    Stamp(previous, observation.NowMilliseconds),
                    SmartRecuperateDecisionReason.WaitingForAcceptedCooldownUnavailable);
            }

            return None(
                Stamp(previous with
                {
                    Phase = SmartRecuperatePhase.WaitingForAcceptedCooldownReady,
                }, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.WaitingForAcceptedCooldownReady);
        }

        if (!observation.ActionCooldownReady)
        {
            return None(
                Stamp(previous, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.WaitingForAcceptedCooldownReady);
        }

        if (!intent.Value.IsAutomatic &&
            !observation.AutomaticModeEnabled &&
            !observation.FrozenKeyStillDown)
        {
            return None(
                Waiting(previous.NextHealthEventToken, observation.NowMilliseconds),
                SmartRecuperateDecisionReason.ExactKeyReleased);
        }

        // A completed cooldown is a new action opportunity, not permission to
        // resurrect the key generation that authorized the accepted action.
        // Continuous holds remain eligible through the input coordinator;
        // disabled/re-enabled or otherwise retired generations must wait for
        // an actual new key edge.
        return TryCreateIntent(previous, observation);
    }

    private static SmartRecuperateDecision TryCreateIntent(
        SmartRecuperateState previous,
        SmartRecuperateObservation observation)
    {
        var permanentFailure = GetPermanentGateFailure(observation);
        if (permanentFailure != SmartRecuperateDecisionReason.None)
        {
            var preserveAcceptedReady = previous.Phase ==
                SmartRecuperatePhase.WaitingForAcceptedCooldownReady;
            return None(
                preserveAcceptedReady
                    ? Stamp(previous, observation.NowMilliseconds)
                    : Waiting(
                        previous.NextHealthEventToken == 0
                            ? 1
                            : previous.NextHealthEventToken,
                        observation.NowMilliseconds),
                permanentFailure);
        }

        var failure = GetDispatchGateFailure(observation);
        if (failure != SmartRecuperateDecisionReason.None)
        {
            var preserveAcceptedReady = previous.Phase ==
                SmartRecuperatePhase.WaitingForAcceptedCooldownReady;
            return None(
                preserveAcceptedReady
                    ? Stamp(previous, observation.NowMilliseconds)
                    : Waiting(
                        previous.NextHealthEventToken == 0
                            ? 1
                            : previous.NextHealthEventToken,
                        observation.NowMilliseconds),
                failure);
        }

        var token = previous.NextHealthEventToken == 0
            ? 1
            : previous.NextHealthEventToken;
        var intent = new SmartRecuperateIntent(
            observation.ResolvedActionId,
            observation.LocalPlayer,
            observation.Context,
            observation.AutomaticModeEnabled
                ? 0
                : observation.HeldGameplayKeyCode,
            observation.CurrentHp,
            observation.MaximumHp,
            token,
            observation.AutomaticModeEnabled
                ? SmartRecuperateTriggerKind.Automatic
                : SmartRecuperateTriggerKind.HeldGameplayKey);
        if (!intent.IsValid)
            return Cancelled(
                SmartRecuperateState.Initial,
                SmartRecuperateDecisionReason.NativeAcceptanceUnknown);

        var buffered = new SmartRecuperateState(
            SmartRecuperatePhase.Buffered,
            intent,
            InitialRetryFor(intent),
            IncrementToken(token),
            observation.NowMilliseconds,
            ClientActionAttemptOutcome.None);
        return observation.NativeBoundaryReady
            ? Dispatch(buffered, intent)
            : Armed(buffered, SmartRecuperateDecisionReason.NativeBoundaryUnavailable);
    }

    private static SmartRecuperateDecisionReason GetPermanentGateFailure(
        SmartRecuperateObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return SmartRecuperateDecisionReason.ConfigurationDisabled;
        if (observation.Context is not (SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen))
        {
            return SmartRecuperateDecisionReason.OutsideSupportedPvPContext;
        }
        if (!observation.LocalPlayer.IsValid)
            return SmartRecuperateDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return SmartRecuperateDecisionReason.LocalPlayerDead;
        if (!observation.IsLocalPlayerTargetable)
            return SmartRecuperateDecisionReason.LocalPlayerUntargetable;
        if (!observation.MetadataVerified)
            return SmartRecuperateDecisionReason.MetadataUnverified;
        if (!observation.HeldModeEnabled &&
            !observation.AutomaticModeEnabled)
        {
            return SmartRecuperateDecisionReason.ConfigurationDisabled;
        }
        if (!observation.AutomaticModeEnabled &&
            !observation.InputProbeSucceeded)
            return SmartRecuperateDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return SmartRecuperateDecisionReason.TextInputActive;
        return SmartRecuperateDecisionReason.None;
    }

    private static SmartRecuperateDecisionReason GetDispatchGateFailure(
        SmartRecuperateObservation observation)
    {
        if (observation.HigherPriorityClaimed)
            return SmartRecuperateDecisionReason.HigherPriorityClaimed;
        if (observation.ActionHelpersSuppressedByGuard)
            return SmartRecuperateDecisionReason.GuardSuppressed;
        if (!observation.AutomaticModeEnabled &&
            (!observation.HeldGameplayKeyEligible ||
             observation.HeldGameplayKeyCode <= 0))
            return SmartRecuperateDecisionReason.NoHeldGameplayKey;
        if (observation.ResolvedActionId != ActionId)
            return SmartRecuperateDecisionReason.ResolvedActionInvalid;
        if (!observation.ActionLocallyReady)
            return SmartRecuperateDecisionReason.ActionNotReady;
        if (!HasValidHealth(observation.CurrentHp, observation.MaximumHp))
            return SmartRecuperateDecisionReason.HealthTelemetryInvalid;
        if (!HasMinimumMissingHp(observation.CurrentHp, observation.MaximumHp))
            return SmartRecuperateDecisionReason.MissingHealthBelowThreshold;
        if (!HasValidMp(observation.CurrentMp, observation.MaximumMp))
            return SmartRecuperateDecisionReason.MpTelemetryInvalid;
        if (!HasMinimumMp(observation.CurrentMp, observation.MaximumMp))
            return SmartRecuperateDecisionReason.InsufficientMp;
        return SmartRecuperateDecisionReason.None;
    }

    private static SmartRecuperateNativeAttemptDecision TerminalUnknown(
        SmartRecuperateState current,
        long nowMilliseconds) =>
        new(
            Stamp(current with
            {
                Phase = SmartRecuperatePhase.SpentUntilKeyRelease,
                Retry = HeldActionRetryState.Initial,
                LastNativeOutcome = ClientActionAttemptOutcome.AcceptanceUnknown,
            }, Math.Max(0, nowMilliseconds)),
            SmartRecuperateDecisionReason.NativeAcceptanceUnknown,
            false,
            false,
            true);

    private static SmartRecuperateDecision Spent(
        SmartRecuperateState previous,
        long nowMilliseconds,
        ClientActionAttemptOutcome outcome,
        SmartRecuperateDecisionReason reason) =>
        None(
            Stamp(previous with
            {
                Phase = SmartRecuperatePhase.SpentUntilKeyRelease,
                Retry = HeldActionRetryState.Initial,
                LastNativeOutcome = outcome,
            }, nowMilliseconds),
            reason);

    private static SmartRecuperateState Waiting(
        ulong nextToken,
        long nowMilliseconds) =>
        new(
            SmartRecuperatePhase.Waiting,
            null,
            HeldActionRetryState.Initial,
            nextToken == 0 ? 1 : nextToken,
            nowMilliseconds,
            ClientActionAttemptOutcome.None);

    private static SmartRecuperateState Stamp(
        SmartRecuperateState state,
        long nowMilliseconds) =>
        state with { LastObservedAtMilliseconds = nowMilliseconds };

    private static HeldActionRetryState InitialRetryFor(
        SmartRecuperateIntent intent) =>
        intent.IsAutomatic
            ? new HeldActionRetryState(
                NativeAttemptCount: 0,
                NextNativeAttemptAtMilliseconds: -1,
                NativeAttemptLimit: HeldActionRetryRules.CurrentMaximumNativeAttempts)
            : HeldActionRetryState.Initial;

    private static bool CanAttemptFrozenRetryBudget(
        HeldActionRetryState retry,
        long nowMilliseconds) =>
        (nowMilliseconds >= 0 &&
         retry.NativeAttemptCount == 0 &&
         retry.NextNativeAttemptAtMilliseconds == -1 &&
         HeldActionRetryRules.ResolveAttemptLimit(retry) > 0) ||
        HeldActionRetryRules.CanAttemptFrozenIntent(retry, nowMilliseconds);

    private static SmartRecuperateDecision Dispatch(
        SmartRecuperateState state,
        SmartRecuperateIntent intent) =>
        new(
            state,
            SmartRecuperateDecisionKind.Dispatch,
            SmartRecuperateDecisionReason.None,
            intent,
            true);

    private static SmartRecuperateDecision Armed(
        SmartRecuperateState state,
        SmartRecuperateDecisionReason reason) =>
        new(
            state,
            SmartRecuperateDecisionKind.Armed,
            reason,
            state.Intent,
            true);

    private static SmartRecuperateDecision None(
        SmartRecuperateState state,
        SmartRecuperateDecisionReason reason) =>
        new(
            state,
            SmartRecuperateDecisionKind.None,
            reason);

    private static SmartRecuperateDecision Cancelled(
        SmartRecuperateState state,
        SmartRecuperateDecisionReason reason) =>
        new(
            state,
            SmartRecuperateDecisionKind.Cancelled,
            reason);

    private static ulong IncrementToken(ulong token) =>
        token == ulong.MaxValue ? 1 : token + 1;
}
