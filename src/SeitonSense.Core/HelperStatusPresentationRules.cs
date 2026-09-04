namespace SeitonSense.Core;

public enum HelperStatusTone : byte
{
    Neutral,
    Paused,
    Accepted,
    Attention,
}

public readonly record struct HelperStatusPresentation(
    string State,
    string Detail,
    HelperStatusTone Tone);

/// <summary>Presentation only: never changes helper eligibility or dispatch.</summary>
public static class HelperStatusPresentationRules
{
    public static bool ShowJob(uint currentJobId, uint sectionJobId, bool showAll) =>
        showAll || currentJobId == 0 || currentJobId == sectionJobId;

    public static HelperStatusPresentation Describe(
        bool enabled,
        bool supportedContext,
        bool ownGuard,
        bool accepted,
        bool attempted,
        string? reason,
        string waitingDetail)
    {
        if (!enabled) return new("Off", "Enable this helper in its settings to use it.", HelperStatusTone.Neutral);
        if (!supportedContext) return Paused("This helper is not available in the current mode.");
        if (ownGuard) return Paused("Your Guard is protected. Waiting for it to end.");
        if (accepted) return new("Accepted", "FFXIV accepted the request; the final effect is not confirmed here.", HelperStatusTone.Accepted);
        if (attempted) return new("Attempted", "A request was sent but was not accepted in this sample.", HelperStatusTone.Attention);

        return reason switch
        {
            "ConfigurationDisabled" or "TriggerModeDisabled" => Paused("The helper is enabled, but this trigger is not currently active. Check its options below."),
            "OutsideSupportedPvPContext" or "OutsideCrystallineConflict" or "OutsideSupportedContext" => Paused("This helper is not available in the current mode."),
            "GuardSuppressed" or "OwnGuardActive" or "GuardActive" => Paused("Your Guard is protected. Waiting for it to end."),
            "RecoveryProtected" => Paused("Recovery is paused to preserve Guard or Ninja stealth."),
            "TextInputActive" => Paused("Finish typing to allow the helper to run."),
            "PlayerDead" or "LocalPlayerDead" or "LocalPlayerUntargetable" => Paused("Waiting until your character can act again."),
            "HigherPriorityClaimed" or "HigherPriorityHelper" => Waiting("A higher-priority helper has this action slot."),
            "NoHeldGameplayKey" or "NoEligibleInput" or "ExactKeyReleased" or "HeldKeyReleased" or "WaitingForFreshKey" => Waiting("Hold a gameplay key such as WASD, or press the configured input."),
            "ActionNotReady" or "ActionUnavailable" or "CarrierUnavailable" or "CarrierNotExposed" or "NoAvailableExposure" or "CooldownActive" => Waiting("The ability or its required proc is not ready."),
            "NativeBoundaryUnavailable" or "NativeQueueBusy" or "GlobalQueueBusy" or "GlobalQueue" => Waiting("Waiting for the current cast, animation, or queued action."),
            "NativeRetryThrottle" => Waiting("The previous request was rejected. Waiting for its next allowed retry."),
            "NativeRetryLimitReached" => Paused("This attempt ended. A new input or opportunity is needed."),
            "NativeAcceptanceUnknown" => new("Uncertain", "FFXIV did not give a clear result. This attempt has stopped.", HelperStatusTone.Attention),
            "MissingHealthBelowThreshold" or "HealthAboveThreshold" => Waiting("HP has not reached the configured healing threshold."),
            "InsufficientMp" => Waiting("Not enough MP for this action."),
            "NoExactEligibleTarget" or "NoEligibleCandidate" or "CandidateUnavailable" or "CandidateInvalid" or "TargetUnavailable" => Waiting("No suitable reachable target is available."),
            "RangeOrLineOfSight" or "TargetOutOfRange" or "RangeUnavailable" => Waiting("The target is out of range or behind an obstacle."),
            "TargetProtected" or "OtherProtection" or "ProtectionActive" => Waiting("The target currently has a protection that blocks this helper."),
            "MetadataUnverified" or "ResolvedActionInvalid" or "InputProbeUnavailable" or "LocalPlayerIdentityInvalid" => new("Unavailable", "Required game information is not ready. More detail is below.", HelperStatusTone.Attention),
            "WaitingForAcceptedCooldownUnavailable" or "WaitingForAcceptedCooldownReady" or "AvailabilityEpochClosed" => Waiting("The previous request is finished; waiting for the next ready opportunity."),
            "ResilienceActive" => Waiting("CC immunity is active; no cleanse is needed right now."),
            "TimedOut" or "ContextChanged" or "HardReset" or "ClockMovedBackwards" => Waiting("The previous opportunity ended. Watching for the next one."),
            _ => Waiting(waitingDetail),
        };
    }

    private static HelperStatusPresentation Waiting(string detail) => new("Waiting", detail, HelperStatusTone.Neutral);
    private static HelperStatusPresentation Paused(string detail) => new("Paused", detail, HelperStatusTone.Paused);
}
