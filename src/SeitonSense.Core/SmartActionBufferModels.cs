namespace SeitonSense.Core;

/// <summary>
/// Immutable identity captured from the user's original action attempt.
/// The buffer never substitutes either action ID or the target.
/// </summary>
public readonly record struct SmartActionBufferAction(
    uint RequestedActionId,
    uint ResolvedActionId,
    ulong TargetId,
    uint TerritoryId,
    ulong InstanceId);

/// <summary>
/// Current runtime facts used to invalidate an exact buffered action.
/// </summary>
public readonly record struct SmartActionBufferSafety(
    bool Enabled,
    bool ConflictDetected,
    bool LoggedIn,
    bool IsAlive,
    bool IsMounted,
    bool IsStunned,
    bool IsKnockbackActive,
    uint TerritoryId,
    ulong InstanceId,
    ulong TargetId,
    uint RequestedActionId,
    uint ResolvedActionId)
{
    public static SmartActionBufferSafety SafeFor(SmartActionBufferAction action) => new(
        Enabled: true,
        ConflictDetected: false,
        LoggedIn: true,
        IsAlive: true,
        IsMounted: false,
        IsStunned: false,
        IsKnockbackActive: false,
        action.TerritoryId,
        action.InstanceId,
        action.TargetId,
        action.RequestedActionId,
        action.ResolvedActionId);
}

public enum SmartActionBufferFailure
{
    Unknown = 0,
    GlobalCooldown,
    AnimationLock,
    Cooldown,
    InvalidTarget,
    OutOfRange,
    InsufficientResource,
    NotLearned,
    ServerRejected,
}

public readonly record struct SmartActionBufferIntent(
    SmartActionBufferAction Action,
    SmartActionBufferFailure OriginalFailure,
    bool IsEligibleForBuffering);

/// <summary>
/// A pause affects only final dispatch. Safety checks and expiry continue while
/// either a caller pause or Seiton's internal-priority claim is active.
/// </summary>
public readonly record struct SmartActionBufferContext(
    SmartActionBufferSafety Safety,
    bool ActionIsExecutable,
    bool DispatchPaused = false,
    bool InternalPriorityClaimed = false)
{
    public bool IsFinalDispatchPaused => DispatchPaused || InternalPriorityClaimed;
}

public enum SmartActionBufferCancelReason
{
    None = 0,
    Replaced,
    Explicit,
    Disabled,
    Conflict,
    Logout,
    Death,
    Mounted,
    Stun,
    Knockback,
    TerritoryChange,
    InstanceChange,
    TargetChange,
    RequestedActionChange,
    ResolvedActionChange,
    Ineligible,
    NonTransientFailure,
    ServerRejected,
    Expired,
    Dispatched,
}

public enum SmartActionBufferDecisionKind
{
    None = 0,
    Dispatch,
    Cancelled,
    Expired,
}

public readonly record struct SmartActionBufferDecision(
    SmartActionBufferDecisionKind Kind,
    SmartActionBufferIntent? Intent,
    SmartActionBufferCancelReason Reason)
{
    public static SmartActionBufferDecision None => new(
        SmartActionBufferDecisionKind.None,
        null,
        SmartActionBufferCancelReason.None);
}
