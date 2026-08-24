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
    PropagationExpired = 9,
    GuardEnded = 10,
    MaximumDurationReached = 11,
    HardReset = 12,
}

public readonly record struct AutoGuardProtectionState(
    long GuardAttemptGeneration,
    uint TerritoryId,
    TargetPressureActorIdentity LocalPlayer,
    long AcceptedAtMilliseconds,
    long MaximumExpiresAtMilliseconds,
    bool ExactGuardObserved)
{
    public static AutoGuardProtectionState Initial => new(0, 0, default, -1, -1, false);

    public bool IsArmed =>
        GuardAttemptGeneration > 0 &&
        TerritoryId != 0 &&
        LocalPlayer.IsValid &&
        AcceptedAtMilliseconds >= 0 &&
        MaximumExpiresAtMilliseconds > AcceptedAtMilliseconds;
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
/// Owns only a client-accepted Guard request produced by the optional automatic
/// Guard helper. The short propagation interval bridges the native call to the
/// first exact status sample; once that status is visible, protection follows it
/// until it ends. Guard reuse is always an explicit native escape hatch and a
/// hard maximum prevents stale state from trapping later input.
/// </summary>
public static class AutoGuardProtectionRules
{
    public const long StatusPropagationMilliseconds = 1_500;
    public const long MaximumOwnedDurationMilliseconds = 6_000;

    public static bool CanArmFromAcceptedAttempt(
        long latestGuardAttemptGeneration,
        long generationBeforeCall,
        uint latestTerritoryId,
        uint currentTerritoryId,
        TargetPressureActorIdentity latestLocalPlayer,
        TargetPressureActorIdentity currentLocalPlayer,
        long observedAtMilliseconds,
        long nowMilliseconds)
    {
        var expectedGeneration = generationBeforeCall == long.MaxValue
            ? 1
            : generationBeforeCall + 1;
        return latestGuardAttemptGeneration > 0 &&
               generationBeforeCall >= 0 &&
               latestGuardAttemptGeneration == expectedGeneration &&
               latestTerritoryId != 0 &&
               latestTerritoryId == currentTerritoryId &&
               latestLocalPlayer.IsValid &&
               latestLocalPlayer == currentLocalPlayer &&
               observedAtMilliseconds >= 0 &&
               observedAtMilliseconds <= nowMilliseconds;
    }

    public static AutoGuardProtectionState Arm(
        long guardAttemptGeneration,
        uint territoryId,
        TargetPressureActorIdentity localPlayer,
        long acceptedAtMilliseconds)
    {
        if (guardAttemptGeneration <= 0 ||
            territoryId == 0 ||
            !localPlayer.IsValid ||
            acceptedAtMilliseconds < 0)
        {
            return AutoGuardProtectionState.Initial;
        }

        return new AutoGuardProtectionState(
            guardAttemptGeneration,
            territoryId,
            localPlayer,
            acceptedAtMilliseconds,
            SaturatingAdd(acceptedAtMilliseconds, MaximumOwnedDurationMilliseconds),
            ExactGuardObserved: false);
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
            observation.NowMilliseconds < previous.AcceptedAtMilliseconds)
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

        // Reusing Guard is the game's deliberate release path. It must never be
        // mistaken for a random action and atomically gives up plugin ownership.
        if (observation.IsExplicitGuardReuse)
            return Released(AutoGuardProtectionDecisionReason.ExplicitGuardReuse);

        var exactGuardObserved = previous.ExactGuardObserved || observation.ExactGuardActive;
        if (previous.ExactGuardObserved && !observation.ExactGuardActive)
            return Released(AutoGuardProtectionDecisionReason.GuardEnded);

        if (!exactGuardObserved &&
            observation.NowMilliseconds >=
            SaturatingAdd(previous.AcceptedAtMilliseconds, StatusPropagationMilliseconds))
        {
            return Released(AutoGuardProtectionDecisionReason.PropagationExpired);
        }

        var next = previous with { ExactGuardObserved = exactGuardObserved };
        var remaining = exactGuardObserved
            ? previous.MaximumExpiresAtMilliseconds - observation.NowMilliseconds
            : Math.Min(
                previous.MaximumExpiresAtMilliseconds,
                SaturatingAdd(previous.AcceptedAtMilliseconds, StatusPropagationMilliseconds)) -
              observation.NowMilliseconds;
        return new AutoGuardProtectionDecision(
            next,
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
