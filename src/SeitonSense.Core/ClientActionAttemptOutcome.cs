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

    /// <summary>
    /// Critical self-recovery may cross an already occupied native queue only
    /// when its caller has explicitly opted into that boundary. The complete
    /// queue tuple is still part of this fingerprint, so a later classifier can
    /// prove that the pre-existing queue was not replaced, cleared, or mutated.
    /// </summary>
    public bool IsCriticalRecoveryActionReady(
        uint expectedActionId,
        bool allowOccupiedQueue) =>
        Captured &&
        expectedActionId != 0 &&
        (allowOccupiedQueue || !ActionQueued) &&
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
    /// A response edge is a transition from an unreadable/not-ready critical
    /// boundary to a ready one. Ordinary timer movement while both samples are
    /// already ready is deliberately not an edge and cannot bypass the retry
    /// throttle every rendered frame.
    /// </summary>
    public static bool BecameCriticalRecoveryReady(
        uint expectedActionId,
        ClientActionAttemptFingerprint previous,
        ClientActionAttemptFingerprint current,
        bool allowOccupiedQueue) =>
        current.Captured &&
        (!previous.Captured ||
         !previous.IsCriticalRecoveryActionReady(
             expectedActionId,
             allowOccupiedQueue)) &&
        current.IsCriticalRecoveryActionReady(
            expectedActionId,
            allowOccupiedQueue);

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

    /// <summary>
    /// Critical-recovery variant of <see cref="Classify"/>. An occupied queue is
    /// admissible only when explicitly requested and only when every observed
    /// queue field, action sequence, and readiness field remains bit-for-bit
    /// unchanged around the synchronous call. Any queue transition is
    /// acceptance-ambiguous and therefore terminal for the frozen intent.
    /// </summary>
    public static ClientActionAttemptOutcome ClassifyCriticalRecovery(
        bool clientReturnedAccepted,
        uint expectedActionId,
        ClientActionAttemptFingerprint before,
        ClientActionAttemptFingerprint after,
        bool allowOccupiedQueue)
    {
        if (clientReturnedAccepted)
            return ClientActionAttemptOutcome.ClientAccepted;

        return before.IsCriticalRecoveryActionReady(
                   expectedActionId,
                   allowOccupiedQueue) &&
               after.IsCriticalRecoveryActionReady(
                   expectedActionId,
                   allowOccupiedQueue) &&
               before == after
            ? ClientActionAttemptOutcome.ClientRejected
            : ClientActionAttemptOutcome.AcceptanceUnknown;
    }
}
