namespace SeitonSense.Core;

/// <summary>
/// Result of one bounded native action boundary. Only an explicit rejected
/// return proves that another call may still be made for the frozen intent.
/// Acceptance and ambiguity are both terminal for that intent epoch.
/// </summary>
public enum ClientActionAttemptOutcome : byte
{
    None = 0,
    NotInvoked = 1,
    ClientRejected = 2,
    ClientAccepted = 3,
    AcceptanceUnknown = 4,
    SoftUnavailable = 5,
}

/// <summary>
/// Native state sampled immediately around one UseAction boundary. A false
/// return is only a proven rejection when the complete observable boundary is
/// unchanged and the exact action remains structurally ready.
/// </summary>
public readonly record struct ClientActionAttemptFingerprint(
    bool Captured,
    bool ActionQueued,
    uint QueuedActionType,
    uint QueuedActionId,
    ulong QueuedTargetId,
    uint QueuedExtraParam,
    uint QueueMode,
    uint QueuedComboRouteId,
    ushort LastUsedActionSequence,
    float AnimationLockSeconds,
    uint CastActionId,
    uint AdjustedActionId,
    bool IsActionOffCooldown,
    uint ResourceStatus)
{
    public bool IsExactActionReady(uint expectedActionId) =>
        Captured &&
        expectedActionId != 0 &&
        !ActionQueued &&
        float.IsFinite(AnimationLockSeconds) &&
        AnimationLockSeconds >= 0f &&
        AnimationLockSeconds <= HeldActionRetryRules.MaximumNearQueueableAnimationLockSeconds &&
        CastActionId == 0 &&
        AdjustedActionId == expectedActionId &&
        IsActionOffCooldown &&
        ResourceStatus == 0;
}

public static class ClientActionAttemptBoundaryRules
{
    /// <summary>
    /// Classifies the synchronous client return together with native evidence.
    /// True is accepted. False is retryable only when an exact, clean-ready
    /// fingerprint remained bit-for-bit stable; any transition is ambiguous.
    /// </summary>
    public static ClientActionAttemptOutcome Classify(
        bool clientReturnedAccepted,
        uint expectedActionId,
        ClientActionAttemptFingerprint before,
        ClientActionAttemptFingerprint after)
    {
        if (clientReturnedAccepted)
            return ClientActionAttemptOutcome.ClientAccepted;

        return before.IsExactActionReady(expectedActionId) &&
               after.IsExactActionReady(expectedActionId) &&
               before == after
            ? ClientActionAttemptOutcome.ClientRejected
            : ClientActionAttemptOutcome.AcceptanceUnknown;
    }
}
