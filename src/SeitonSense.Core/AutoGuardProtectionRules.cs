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
    GuardReuseProtected = 12,
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
    public const long GuardReuseProtectionMilliseconds = 2_000;
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
        {
            var reuseProtectionEndsAt = SaturatingAdd(
                previous.ConfirmedAtMilliseconds,
                GuardReuseProtectionMilliseconds);
            if (observation.NowMilliseconds < reuseProtectionEndsAt)
            {
                return new AutoGuardProtectionDecision(
                    previous,
                    ShouldBlockAction: true,
                    Math.Max(0, reuseProtectionEndsAt - observation.NowMilliseconds),
                    AutoGuardProtectionDecisionReason.GuardReuseProtected);
            }

            return Released(AutoGuardProtectionDecisionReason.ExplicitGuardReuse);
        }

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
