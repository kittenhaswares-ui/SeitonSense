namespace SeitonSense.Core;

public enum AutoGuardProtectionDecisionReason : byte
{
    Inactive = 0,
    Protected = 1,
    NonCancellingInvocation = 2,
    ExplicitGuardReuse = 3,
    RuntimeDisabled = 4,
    ContextChanged = 5,
    LocalPlayerChanged = 6,
    LocalPlayerUnavailable = 7,
    ClockInvalid = 8,
    GuardEnded = 9,
    MaximumDurationReached = 10,
    HardReset = 11,
}

public readonly record struct GuardRepeatProtectionObservation(
    bool RuntimeEnabled,
    bool IsSupportedPvpContext,
    bool ExactGuardRequest,
    bool ExactLocalGuardActive,
    bool ExactOwnGuardActivationObserved,
    long OwnGuardActivatedAtMilliseconds,
    long NowMilliseconds);

public readonly record struct SyntheticGuardRepeatProtectionObservation(
    bool RuntimeEnabled,
    bool IsSupportedPvpContext,
    bool IsSyntheticRequest,
    bool ExactGuardRequest,
    bool OwnGuardActiveOrPropagating);

/// <summary>
/// Suppresses only an exact second local Guard request during the first second
/// after exact local Guard first became visible for a recent hook-observed
/// request. A provisional or ambiguous attempt always fails open; network/UI
/// propagation can no longer consume part of the protection window. Other
/// actions are deliberately outside this policy.
/// </summary>
public static class GuardRepeatProtectionRules
{
    public const bool DefaultEnabled = true;
    public const long ProtectionMilliseconds = 1_000;

    public static bool ShouldBlock(GuardRepeatProtectionObservation observation) =>
        observation.RuntimeEnabled &&
        observation.IsSupportedPvpContext &&
        observation.ExactGuardRequest &&
        observation.ExactLocalGuardActive &&
        observation.ExactOwnGuardActivationObserved &&
        observation.OwnGuardActivatedAtMilliseconds >= 0 &&
        observation.NowMilliseconds >= observation.OwnGuardActivatedAtMilliseconds &&
        observation.NowMilliseconds - observation.OwnGuardActivatedAtMilliseconds <
            ProtectionMilliseconds;

    /// <summary>
    /// A synthetic Turbo or timing-buffer replay may help the first Guard land,
    /// but it never represents a fresh player decision to toggle an existing
    /// Guard off. Physical fresh presses remain outside this policy.
    /// </summary>
    public static bool ShouldBlockSyntheticRepeat(
        SyntheticGuardRepeatProtectionObservation observation) =>
        observation.RuntimeEnabled &&
        observation.IsSupportedPvpContext &&
        observation.IsSyntheticRequest &&
        observation.ExactGuardRequest &&
        observation.OwnGuardActiveOrPropagating;
}

public readonly record struct AutoGuardProtectionState(
    long GuardAttemptGeneration,
    uint TerritoryId,
    TargetPressureActorIdentity LocalPlayer,
    long ConfirmedAtMilliseconds,
    long MaximumExpiresAtMilliseconds)
{
    public static AutoGuardProtectionState Initial => new(0, 0, default, -1, -1);

    public bool IsArmed =>
        GuardAttemptGeneration > 0 &&
        TerritoryId != 0 &&
        LocalPlayer.IsValid &&
        ConfirmedAtMilliseconds >= 0 &&
        MaximumExpiresAtMilliseconds > ConfirmedAtMilliseconds;

    // Retained in diagnostics as an explicit invariant: protection cannot be
    // armed before the exact local Guard status has already been observed.
    public bool ExactGuardObserved => IsArmed;
}

public readonly record struct AutoGuardProtectionObservation(
    bool RuntimeEnabled,
    uint TerritoryId,
    TargetPressureActorIdentity LocalPlayer,
    bool LocalPlayerLive,
    bool ExactGuardActive,
    bool ActionCanCancelGuard,
    bool IsExplicitGuardReuse,
    long NowMilliseconds,
    bool HardReset = false);

public readonly record struct AutoGuardProtectionDecision(
    AutoGuardProtectionState NextState,
    bool ShouldBlockAction,
    long RemainingMilliseconds,
    AutoGuardProtectionDecisionReason Reason);

/// <summary>
/// Protects only an automatic Guard whose exact live 3054/3673 status has
/// already confirmed the matching hook-observed request. A client true return
/// is deliberately insufficient: the confirmation wait belongs to the caller
/// and cannot cancel input or create an activation popup.
/// </summary>
public static class AutoGuardProtectionRules
{
    public const long MaximumOwnedDurationMilliseconds = 6_000;

    public static bool CanArmFromConfirmedAttempt(
        long latestGuardAttemptGeneration,
        long generationBeforeCall,
        uint latestTerritoryId,
        uint currentTerritoryId,
        TargetPressureActorIdentity latestLocalPlayer,
        TargetPressureActorIdentity currentLocalPlayer,
        long observedAtMilliseconds,
        long nowMilliseconds,
        bool exactGuardActive)
    {
        var expectedGeneration = generationBeforeCall == long.MaxValue
            ? 1
            : generationBeforeCall + 1;
        return exactGuardActive &&
               latestGuardAttemptGeneration > 0 &&
               generationBeforeCall >= 0 &&
               latestGuardAttemptGeneration == expectedGeneration &&
               latestTerritoryId != 0 &&
               latestTerritoryId == currentTerritoryId &&
               latestLocalPlayer.IsValid &&
               latestLocalPlayer == currentLocalPlayer &&
               observedAtMilliseconds >= 0 &&
               observedAtMilliseconds <= nowMilliseconds;
    }

    public static AutoGuardProtectionState ArmConfirmed(
        long guardAttemptGeneration,
        uint territoryId,
        TargetPressureActorIdentity localPlayer,
        long confirmedAtMilliseconds,
        bool exactGuardActive)
    {
        if (!exactGuardActive ||
            guardAttemptGeneration <= 0 ||
            territoryId == 0 ||
            !localPlayer.IsValid ||
            confirmedAtMilliseconds < 0)
        {
            return AutoGuardProtectionState.Initial;
        }

        return new AutoGuardProtectionState(
            guardAttemptGeneration,
            territoryId,
            localPlayer,
            confirmedAtMilliseconds,
            SaturatingAdd(confirmedAtMilliseconds, MaximumOwnedDurationMilliseconds));
    }

    public static AutoGuardProtectionDecision Observe(
        AutoGuardProtectionState previous,
        AutoGuardProtectionObservation observation)
    {
        if (!previous.IsArmed)
            return Released(AutoGuardProtectionDecisionReason.Inactive);
        if (observation.HardReset)
            return Released(AutoGuardProtectionDecisionReason.HardReset);
        if (observation.NowMilliseconds < 0 ||
            observation.NowMilliseconds < previous.ConfirmedAtMilliseconds)
        {
            return Released(AutoGuardProtectionDecisionReason.ClockInvalid);
        }

        if (!observation.RuntimeEnabled)
            return Released(AutoGuardProtectionDecisionReason.RuntimeDisabled);
        if (observation.TerritoryId != previous.TerritoryId)
            return Released(AutoGuardProtectionDecisionReason.ContextChanged);
        if (!observation.LocalPlayerLive)
            return Released(AutoGuardProtectionDecisionReason.LocalPlayerUnavailable);
        if (observation.LocalPlayer != previous.LocalPlayer)
            return Released(AutoGuardProtectionDecisionReason.LocalPlayerChanged);
        if (observation.NowMilliseconds >= previous.MaximumExpiresAtMilliseconds)
            return Released(AutoGuardProtectionDecisionReason.MaximumDurationReached);
        if (!observation.ExactGuardActive)
            return Released(AutoGuardProtectionDecisionReason.GuardEnded);

        if (observation.IsExplicitGuardReuse)
            return Released(AutoGuardProtectionDecisionReason.ExplicitGuardReuse);

        var remaining = previous.MaximumExpiresAtMilliseconds - observation.NowMilliseconds;
        return new AutoGuardProtectionDecision(
            previous,
            observation.ActionCanCancelGuard,
            Math.Max(0, remaining),
            observation.ActionCanCancelGuard
                ? AutoGuardProtectionDecisionReason.Protected
                : AutoGuardProtectionDecisionReason.NonCancellingInvocation);
    }

    private static AutoGuardProtectionDecision Released(
        AutoGuardProtectionDecisionReason reason) =>
        new(AutoGuardProtectionState.Initial, false, 0, reason);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
