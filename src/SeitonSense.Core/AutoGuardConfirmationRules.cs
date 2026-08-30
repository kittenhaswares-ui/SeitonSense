namespace SeitonSense.Core;

public enum AutoGuardRetryReadiness : byte
{
    Unknown = 0,
    Ready = 1,
    NativeBoundaryBusy = 2,
    CooldownUnavailable = 3,
}

public enum AutoGuardConfirmationReason : byte
{
    Inactive = 0,
    WaitingForExactStatus = 1,
    Confirmed = 2,
    RetryReady = 3,
    RetryBoundaryBusy = 4,
    RetryAlreadySpent = 5,
    RuntimeDisabled = 6,
    ContextChanged = 7,
    LocalPlayerChanged = 8,
    LocalPlayerUnavailable = 9,
    ClockInvalid = 10,
    OpportunityExpired = 11,
    CooldownUnavailable = 12,
    ReadinessUnknown = 13,
    HardReset = 14,
}

public readonly record struct AutoGuardConfirmationState(
    long GenerationBeforeCall,
    uint TerritoryId,
    TargetPressureActorIdentity LocalPlayer,
    long RequestedAtMilliseconds,
    long ConfirmationDeadlineMilliseconds,
    long OpportunityExpiresAtMilliseconds,
    bool ConfirmationRetrySpent)
{
    public static AutoGuardConfirmationState Initial =>
        new(-1, 0, default, -1, -1, -1, false);

    public bool IsPending =>
        GenerationBeforeCall >= 0 &&
        TerritoryId != 0 &&
        LocalPlayer.IsValid &&
        RequestedAtMilliseconds >= 0 &&
        ConfirmationDeadlineMilliseconds > RequestedAtMilliseconds &&
        OpportunityExpiresAtMilliseconds > RequestedAtMilliseconds;
}

public readonly record struct AutoGuardConfirmationObservation(
    bool RuntimeEnabled,
    uint TerritoryId,
    TargetPressureActorIdentity LocalPlayer,
    bool LocalPlayerLive,
    bool ExactGuardActive,
    AutoGuardRetryReadiness RetryReadiness,
    long NowMilliseconds,
    bool HardReset = false);

public readonly record struct AutoGuardConfirmationDecision(
    AutoGuardConfirmationState NextState,
    bool Confirmed,
    bool ShouldRetry,
    AutoGuardConfirmationReason Reason);

/// <summary>
/// Separates a provisional client-true Guard return from a server-visible
/// activation. One exact status confirms it. If no status appears, only one
/// retry may cross the native boundary, and only while the original post-Purify
/// lease is alive and exact readiness proves Guard is still available.
/// </summary>
public static class AutoGuardConfirmationRules
{
    public const long StatusConfirmationMilliseconds = 1_500;

    public static bool ShouldRetainUnspentRetry(
        ClientActionAttemptOutcome outcome) =>
        outcome == ClientActionAttemptOutcome.SoftUnavailable;

    public static AutoGuardConfirmationState ArmProvisional(
        long generationBeforeCall,
        uint territoryId,
        TargetPressureActorIdentity localPlayer,
        long requestedAtMilliseconds,
        long opportunityExpiresAtMilliseconds,
        bool confirmationRetrySpent)
    {
        if (generationBeforeCall < 0 ||
            territoryId == 0 ||
            !localPlayer.IsValid ||
            requestedAtMilliseconds < 0 ||
            opportunityExpiresAtMilliseconds <= requestedAtMilliseconds)
        {
            return AutoGuardConfirmationState.Initial;
        }

        var confirmationDeadline = Math.Min(
            SaturatingAdd(requestedAtMilliseconds, StatusConfirmationMilliseconds),
            opportunityExpiresAtMilliseconds);
        if (confirmationDeadline <= requestedAtMilliseconds)
            return AutoGuardConfirmationState.Initial;

        return new AutoGuardConfirmationState(
            generationBeforeCall,
            territoryId,
            localPlayer,
            requestedAtMilliseconds,
            confirmationDeadline,
            opportunityExpiresAtMilliseconds,
            confirmationRetrySpent);
    }

    public static AutoGuardConfirmationDecision Observe(
        AutoGuardConfirmationState previous,
        AutoGuardConfirmationObservation observation)
    {
        if (!previous.IsPending)
            return Retired(AutoGuardConfirmationReason.Inactive);
        if (observation.HardReset)
            return Retired(AutoGuardConfirmationReason.HardReset);
        if (observation.NowMilliseconds < 0 ||
            observation.NowMilliseconds < previous.RequestedAtMilliseconds)
        {
            return Retired(AutoGuardConfirmationReason.ClockInvalid);
        }
        if (!observation.RuntimeEnabled)
            return Retired(AutoGuardConfirmationReason.RuntimeDisabled);
        if (observation.TerritoryId != previous.TerritoryId)
            return Retired(AutoGuardConfirmationReason.ContextChanged);
        if (!observation.LocalPlayerLive)
            return Retired(AutoGuardConfirmationReason.LocalPlayerUnavailable);
        if (observation.LocalPlayer != previous.LocalPlayer)
            return Retired(AutoGuardConfirmationReason.LocalPlayerChanged);
        if (observation.ExactGuardActive)
        {
            return new AutoGuardConfirmationDecision(
                AutoGuardConfirmationState.Initial,
                Confirmed: true,
                ShouldRetry: false,
                AutoGuardConfirmationReason.Confirmed);
        }
        if (observation.NowMilliseconds >= previous.OpportunityExpiresAtMilliseconds)
            return Retired(AutoGuardConfirmationReason.OpportunityExpired);
        if (observation.NowMilliseconds < previous.ConfirmationDeadlineMilliseconds)
        {
            return new AutoGuardConfirmationDecision(
                previous,
                Confirmed: false,
                ShouldRetry: false,
                AutoGuardConfirmationReason.WaitingForExactStatus);
        }
        if (previous.ConfirmationRetrySpent)
            return Retired(AutoGuardConfirmationReason.RetryAlreadySpent);

        return observation.RetryReadiness switch
        {
            AutoGuardRetryReadiness.Ready => new AutoGuardConfirmationDecision(
                previous,
                Confirmed: false,
                ShouldRetry: true,
                AutoGuardConfirmationReason.RetryReady),
            AutoGuardRetryReadiness.NativeBoundaryBusy => new AutoGuardConfirmationDecision(
                previous,
                Confirmed: false,
                ShouldRetry: false,
                AutoGuardConfirmationReason.RetryBoundaryBusy),
            AutoGuardRetryReadiness.CooldownUnavailable =>
                Retired(AutoGuardConfirmationReason.CooldownUnavailable),
            _ => Retired(AutoGuardConfirmationReason.ReadinessUnknown),
        };
    }

    private static AutoGuardConfirmationDecision Retired(
        AutoGuardConfirmationReason reason) =>
        new(AutoGuardConfirmationState.Initial, false, false, reason);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
