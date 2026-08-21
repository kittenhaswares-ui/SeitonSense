namespace SeitonSense.Core;

public readonly record struct PanicShukuchiPoint(float X, float Y, float Z)
{
    public bool IsFinite =>
        float.IsFinite(X) &&
        float.IsFinite(Y) &&
        float.IsFinite(Z);
}

/// <summary>
/// A positively observed ground collision at the requested forward probe.
/// Missing or invalid collision data is never replaced with the player's Y
/// coordinate or with a shorter fallback position.
/// </summary>
public readonly record struct PanicShukuchiGroundHit(
    bool ExactGroundHit,
    PanicShukuchiPoint Position);

public readonly record struct PanicShukuchiCandidate(
    PanicShukuchiPoint Origin,
    float RotationRadians,
    PanicShukuchiGroundHit GroundHit);

public readonly record struct PanicShukuchiIntent(
    uint ActionId,
    PanicShukuchiPoint Destination)
{
    public bool IsValid =>
        ActionId == PanicShukuchiRules.ActionId &&
        Destination.IsFinite;
}

public readonly record struct PanicShukuchiPending(
    TargetPressureActorIdentity LocalPlayer,
    uint TerritoryId,
    SupportedPvPContext Context,
    bool WolvesDenTestingEnabledAtArm,
    PanicShukuchiCandidate Candidate,
    PanicShukuchiIntent Intent,
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds)
{
    public bool IsValid =>
        LocalPlayer.IsValid &&
        TerritoryId != 0 &&
        PanicShukuchiRules.IsSupportedContext(Context, WolvesDenTestingEnabledAtArm) &&
        PanicShukuchiRules.IsValidGroundHit(Candidate) &&
        Intent.IsValid &&
        Intent.Destination == Candidate.GroundHit.Position &&
        ArmedAtMilliseconds >= 0 &&
        ExpiresAtMilliseconds > ArmedAtMilliseconds &&
        ExpiresAtMilliseconds - ArmedAtMilliseconds <=
        PanicShukuchiRules.MaximumPendingMilliseconds;
}

public readonly record struct PanicShukuchiPendingState(PanicShukuchiPending? Pending)
{
    public static PanicShukuchiPendingState Initial => new(null);

    public bool IsPending => Pending is { IsValid: true };
}

public readonly record struct PanicShukuchiArmObservation(
    long NowMilliseconds,
    bool PluginEnabled,
    bool MetadataVerified,
    SupportedPvPContext Context,
    bool WolvesDenTestingEnabled,
    uint TerritoryId,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    bool LocalPlayerAliveAndTargetable,
    bool OwnGuardClear,
    bool Incapacitated,
    uint ResolvedActionId,
    PanicShukuchiCandidate Candidate);

public enum PanicShukuchiArmDecisionKind
{
    Rejected = 0,
    Armed = 1,
    ExistingPendingPreserved = 2,
}

public readonly record struct PanicShukuchiArmDecision(
    PanicShukuchiPendingState NextState,
    PanicShukuchiArmDecisionKind Kind,
    PanicShukuchiDecisionReason Reason)
{
    public bool DidArm => Kind == PanicShukuchiArmDecisionKind.Armed;
}

public readonly record struct PanicShukuchiPendingObservation(
    long NowMilliseconds,
    bool PluginEnabled,
    bool MetadataVerified,
    SupportedPvPContext Context,
    bool WolvesDenTestingEnabled,
    uint TerritoryId,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    bool LocalPlayerAliveAndTargetable,
    bool OwnGuardClear,
    bool Incapacitated,
    bool HigherPriorityClaimed,
    bool NotCasting,
    bool NativeQueueClear,
    bool AnimationLockClear,
    uint ResolvedActionId,
    bool CooldownStateKnown,
    bool CooldownReady,
    bool ActionStructurallyReady,
    PanicShukuchiPoint RequestedDestination,
    bool HardReset = false);

public enum PanicShukuchiPendingDecisionKind
{
    None = 0,
    Waiting = 1,
    Attempt = 2,
    Cleared = 3,
}

public enum PanicShukuchiDecisionReason
{
    None = 0,
    Armed = 1,
    AlreadyPending = 2,
    NoPending = 3,
    HardReset = 4,
    InvalidPending = 5,
    InvalidClock = 6,
    ClockMovedBackwards = 7,
    Expired = 8,
    PluginDisabled = 9,
    MetadataUnverified = 10,
    UnsupportedContext = 11,
    TerritoryChanged = 12,
    InvalidLocalPlayer = 13,
    LocalPlayerIdentityChanged = 14,
    WrongJob = 15,
    OwnGuardActiveOrPropagating = 16,
    Incapacitated = 17,
    DestinationChanged = 18,
    ResolvedActionInvalid = 19,
    CooldownStateUnknown = 20,
    ActionNotReady = 21,
    ActionStructurallyUnavailable = 22,
    WaitingForHigherPriority = 23,
    WaitingForCast = 24,
    WaitingForNativeQueue = 25,
    WaitingForAnimationLock = 26,
    InvalidForwardGroundHit = 27,
    Ready = 28,
}

public readonly record struct PanicShukuchiPendingDecision(
    PanicShukuchiPendingState NextState,
    PanicShukuchiPendingDecisionKind Kind,
    PanicShukuchiDecisionReason Reason,
    PanicShukuchiIntent? Intent = null)
{
    public bool ShouldAttempt =>
        Kind == PanicShukuchiPendingDecisionKind.Attempt &&
        Intent is { IsValid: true };
}

/// <summary>
/// Pure fail-closed policy for one explicit /panicshu invocation. The command
/// freezes one local identity, territory/context, and 19.5-yalm destination in
/// a short lease. Cast, native-queue, and animation-lock waits may retain that
/// exact lease. A ready decision clears it before the caller makes its sole
/// UseActionLocation call. There is no native-false retry, inward fallback,
/// destination recomputation, target substitution, or pending replacement.
/// </summary>
public static class PanicShukuchiRules
{
    public const uint NinjaJobId = 30;
    public const uint ActionId = 29_513;
    public const float NativeMaximumRangeYalms = 20f;
    public const float SafeForwardDistanceYalms = 19.5f;
    public const long MaximumPendingMilliseconds = 500;

    // Collision implementations can introduce tiny horizontal rounding while
    // returning the surface Y. This tolerance cannot turn a materially shorter
    // or off-axis destination into an eligible command intent.
    public const float MaximumGroundHorizontalErrorYalms = 0.05f;

    public static bool IsSupportedContext(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled) =>
        context == SupportedPvPContext.CrystallineConflict ||
        (wolvesDenTestingEnabled && context == SupportedPvPContext.WolvesDen);

    /// <summary>
    /// Produces the sole horizontal probe point using FFXIV's rotation axes:
    /// forward X is sin(rotation), forward Z is cos(rotation).
    /// </summary>
    public static bool TryCreateForwardProbe(
        PanicShukuchiPoint origin,
        float rotationRadians,
        out PanicShukuchiPoint probe)
    {
        probe = default;
        if (!origin.IsFinite || !float.IsFinite(rotationRadians)) return false;

        var forwardX = MathF.Sin(rotationRadians);
        var forwardZ = MathF.Cos(rotationRadians);
        if (!float.IsFinite(forwardX) || !float.IsFinite(forwardZ)) return false;

        probe = new PanicShukuchiPoint(
            origin.X + (forwardX * SafeForwardDistanceYalms),
            origin.Y,
            origin.Z + (forwardZ * SafeForwardDistanceYalms));
        if (!probe.IsFinite) return false;

        // Very large finite coordinates can erase the requested displacement
        // when rounded to float. Such telemetry is unusable and fails closed.
        return IsApproximatelySafeHorizontalDistance(origin, probe);
    }

    public static bool IsValidGroundHit(PanicShukuchiCandidate candidate)
    {
        if (!candidate.GroundHit.ExactGroundHit ||
            !candidate.Origin.IsFinite ||
            !candidate.GroundHit.Position.IsFinite ||
            !float.IsFinite(candidate.RotationRadians) ||
            !TryCreateForwardProbe(
                candidate.Origin,
                candidate.RotationRadians,
                out var probe))
        {
            return false;
        }

        var hit = candidate.GroundHit.Position;
        if (Math.Abs((double)hit.X - probe.X) > MaximumGroundHorizontalErrorYalms ||
            Math.Abs((double)hit.Z - probe.Z) > MaximumGroundHorizontalErrorYalms ||
            !IsApproximatelySafeHorizontalDistance(candidate.Origin, hit))
        {
            return false;
        }

        var deltaX = (double)hit.X - candidate.Origin.X;
        var deltaY = (double)hit.Y - candidate.Origin.Y;
        var deltaZ = (double)hit.Z - candidate.Origin.Z;
        var distanceSquared = (deltaX * deltaX) +
                              (deltaY * deltaY) +
                              (deltaZ * deltaZ);
        return double.IsFinite(distanceSquared) &&
               distanceSquared <=
               (double)NativeMaximumRangeYalms * NativeMaximumRangeYalms;
    }

    public static PanicShukuchiArmDecision Arm(
        PanicShukuchiPendingState previous,
        PanicShukuchiArmObservation observation,
        long lifetimeMilliseconds = MaximumPendingMilliseconds)
    {
        if (previous.Pending is { } existing)
        {
            if (!existing.IsValid)
                return RejectedArm(PanicShukuchiDecisionReason.InvalidPending);
            if (observation.NowMilliseconds < existing.ArmedAtMilliseconds)
                return RejectedArm(PanicShukuchiDecisionReason.ClockMovedBackwards);
            if (observation.NowMilliseconds < existing.ExpiresAtMilliseconds)
            {
                return new PanicShukuchiArmDecision(
                    previous,
                    PanicShukuchiArmDecisionKind.ExistingPendingPreserved,
                    PanicShukuchiDecisionReason.AlreadyPending);
            }

            // The old lease is provably expired. The current explicit command
            // may now arm a new independent lease if all of its inputs pass.
        }

        var failure = GetArmFailure(observation, lifetimeMilliseconds);
        if (failure != PanicShukuchiDecisionReason.None)
            return RejectedArm(failure);

        var expiresAt = observation.NowMilliseconds + lifetimeMilliseconds;
        var intent = new PanicShukuchiIntent(
            observation.ResolvedActionId,
            observation.Candidate.GroundHit.Position);
        var pending = new PanicShukuchiPending(
            observation.LocalPlayer,
            observation.TerritoryId,
            observation.Context,
            observation.WolvesDenTestingEnabled,
            observation.Candidate,
            intent,
            observation.NowMilliseconds,
            expiresAt);
        if (!pending.IsValid)
            return RejectedArm(PanicShukuchiDecisionReason.InvalidPending);

        return new PanicShukuchiArmDecision(
            new PanicShukuchiPendingState(pending),
            PanicShukuchiArmDecisionKind.Armed,
            PanicShukuchiDecisionReason.Armed);
    }

    public static PanicShukuchiPendingDecision ObservePending(
        PanicShukuchiPendingState previous,
        PanicShukuchiPendingObservation observation)
    {
        if (observation.HardReset)
            return Cleared(PanicShukuchiDecisionReason.HardReset);
        if (previous.Pending is not { } pending)
        {
            return new PanicShukuchiPendingDecision(
                PanicShukuchiPendingState.Initial,
                PanicShukuchiPendingDecisionKind.None,
                PanicShukuchiDecisionReason.NoPending);
        }

        if (!pending.IsValid)
            return Cleared(PanicShukuchiDecisionReason.InvalidPending);
        if (observation.NowMilliseconds < 0)
            return Cleared(PanicShukuchiDecisionReason.InvalidClock);
        if (observation.NowMilliseconds < pending.ArmedAtMilliseconds)
            return Cleared(PanicShukuchiDecisionReason.ClockMovedBackwards);
        if (observation.NowMilliseconds >= pending.ExpiresAtMilliseconds)
            return Cleared(PanicShukuchiDecisionReason.Expired);

        var failure = GetPendingTerminalFailure(pending, observation);
        if (failure != PanicShukuchiDecisionReason.None)
            return Cleared(failure);

        // These are the only cross-frame waits. They never modify, extend, or
        // recompute the frozen lease and therefore cannot spend an attempt.
        if (observation.HigherPriorityClaimed)
            return Waiting(previous, PanicShukuchiDecisionReason.WaitingForHigherPriority);
        if (!observation.NotCasting)
            return Waiting(previous, PanicShukuchiDecisionReason.WaitingForCast);
        if (!observation.NativeQueueClear)
            return Waiting(previous, PanicShukuchiDecisionReason.WaitingForNativeQueue);
        if (!observation.AnimationLockClear)
            return Waiting(previous, PanicShukuchiDecisionReason.WaitingForAnimationLock);

        if (!observation.CooldownStateKnown)
            return Cleared(PanicShukuchiDecisionReason.CooldownStateUnknown);
        if (!observation.CooldownReady)
            return Cleared(PanicShukuchiDecisionReason.ActionNotReady);
        if (!observation.ActionStructurallyReady)
            return Cleared(PanicShukuchiDecisionReason.ActionStructurallyUnavailable);

        // State is cleared in the decision before the native call. Client false,
        // exceptions, or lost acknowledgements therefore cannot retry it.
        return new PanicShukuchiPendingDecision(
            PanicShukuchiPendingState.Initial,
            PanicShukuchiPendingDecisionKind.Attempt,
            PanicShukuchiDecisionReason.Ready,
            pending.Intent);
    }

    private static PanicShukuchiDecisionReason GetArmFailure(
        PanicShukuchiArmObservation observation,
        long lifetimeMilliseconds)
    {
        if (observation.NowMilliseconds < 0 ||
            lifetimeMilliseconds <= 0 ||
            lifetimeMilliseconds > MaximumPendingMilliseconds ||
            observation.NowMilliseconds > long.MaxValue - lifetimeMilliseconds)
        {
            return PanicShukuchiDecisionReason.InvalidClock;
        }

        if (!observation.PluginEnabled)
            return PanicShukuchiDecisionReason.PluginDisabled;
        if (!observation.MetadataVerified)
            return PanicShukuchiDecisionReason.MetadataUnverified;
        if (!IsSupportedContext(observation.Context, observation.WolvesDenTestingEnabled))
            return PanicShukuchiDecisionReason.UnsupportedContext;
        if (observation.TerritoryId == 0 || !observation.LocalPlayer.IsValid ||
            !observation.LocalPlayerAliveAndTargetable)
        {
            return PanicShukuchiDecisionReason.InvalidLocalPlayer;
        }

        if (observation.LocalJobId != NinjaJobId)
            return PanicShukuchiDecisionReason.WrongJob;
        if (!observation.OwnGuardClear)
            return PanicShukuchiDecisionReason.OwnGuardActiveOrPropagating;
        if (observation.Incapacitated)
            return PanicShukuchiDecisionReason.Incapacitated;
        if (observation.ResolvedActionId != ActionId)
            return PanicShukuchiDecisionReason.ResolvedActionInvalid;
        if (!IsValidGroundHit(observation.Candidate))
            return PanicShukuchiDecisionReason.InvalidForwardGroundHit;
        return PanicShukuchiDecisionReason.None;
    }

    private static PanicShukuchiDecisionReason GetPendingTerminalFailure(
        PanicShukuchiPending pending,
        PanicShukuchiPendingObservation observation)
    {
        if (!observation.PluginEnabled)
            return PanicShukuchiDecisionReason.PluginDisabled;
        if (!observation.MetadataVerified)
            return PanicShukuchiDecisionReason.MetadataUnverified;
        if (observation.Context != pending.Context ||
            !IsSupportedContext(observation.Context, observation.WolvesDenTestingEnabled))
        {
            return PanicShukuchiDecisionReason.UnsupportedContext;
        }

        if (observation.TerritoryId != pending.TerritoryId)
            return PanicShukuchiDecisionReason.TerritoryChanged;
        if (!observation.LocalPlayer.IsValid || !observation.LocalPlayerAliveAndTargetable)
            return PanicShukuchiDecisionReason.InvalidLocalPlayer;
        if (observation.LocalPlayer != pending.LocalPlayer)
            return PanicShukuchiDecisionReason.LocalPlayerIdentityChanged;
        if (observation.LocalJobId != NinjaJobId)
            return PanicShukuchiDecisionReason.WrongJob;
        if (!observation.OwnGuardClear)
            return PanicShukuchiDecisionReason.OwnGuardActiveOrPropagating;
        if (observation.Incapacitated)
            return PanicShukuchiDecisionReason.Incapacitated;
        if (!observation.RequestedDestination.IsFinite ||
            observation.RequestedDestination != pending.Intent.Destination)
        {
            return PanicShukuchiDecisionReason.DestinationChanged;
        }

        if (observation.ResolvedActionId != pending.Intent.ActionId ||
            observation.ResolvedActionId != ActionId)
        {
            return PanicShukuchiDecisionReason.ResolvedActionInvalid;
        }
        return PanicShukuchiDecisionReason.None;
    }

    private static PanicShukuchiArmDecision RejectedArm(
        PanicShukuchiDecisionReason reason) =>
        new(
            PanicShukuchiPendingState.Initial,
            PanicShukuchiArmDecisionKind.Rejected,
            reason);

    private static PanicShukuchiPendingDecision Cleared(
        PanicShukuchiDecisionReason reason) =>
        new(
            PanicShukuchiPendingState.Initial,
            PanicShukuchiPendingDecisionKind.Cleared,
            reason);

    private static PanicShukuchiPendingDecision Waiting(
        PanicShukuchiPendingState state,
        PanicShukuchiDecisionReason reason) =>
        new(state, PanicShukuchiPendingDecisionKind.Waiting, reason);

    private static bool IsApproximatelySafeHorizontalDistance(
        PanicShukuchiPoint origin,
        PanicShukuchiPoint destination)
    {
        var deltaX = (double)destination.X - origin.X;
        var deltaZ = (double)destination.Z - origin.Z;
        var horizontalDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
        if (!double.IsFinite(horizontalDistanceSquared)) return false;

        var minimum = SafeForwardDistanceYalms - MaximumGroundHorizontalErrorYalms;
        var maximum = SafeForwardDistanceYalms + MaximumGroundHorizontalErrorYalms;
        return horizontalDistanceSquared >= (double)minimum * minimum &&
               horizontalDistanceSquared <= (double)maximum * maximum;
    }
}
