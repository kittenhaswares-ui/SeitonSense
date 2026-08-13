namespace SeitonSense.Core;

public readonly record struct FarHelpOneShotToken(
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds)
{
    public bool IsValid =>
        ArmedAtMilliseconds >= 0 &&
        ExpiresAtMilliseconds > ArmedAtMilliseconds;
}

public readonly record struct FarHelpOneShotState(FarHelpOneShotToken? Token)
{
    public static FarHelpOneShotState Initial => new(null);

    public bool IsArmed => Token is { IsValid: true };
}

public readonly record struct FarHelpActionAttempt(
    ulong OriginalTargetId,
    long NowMilliseconds,
    bool IsEligibleMacroActionAttempt,
    bool IsSupportedContext,
    bool IsSupportedAction,
    bool IsMovementAction,
    bool IsSupportedActionMode,
    bool IsFriendlyAction,
    bool IsAreaTargetedAction,
    bool IsFallbackCarrier,
    bool HardReset = false);

public enum FarHelpOneShotDecisionKind
{
    PassThrough = 0,
    Waiting = 1,
    ConsumedWithoutRewrite = 2,
    RewriteTarget = 3,
    Cleared = 4,
}

public enum FarHelpOneShotReason
{
    None = 0,
    NoToken = 1,
    NotEligibleMacroActionAttempt = 2,
    HardReset = 3,
    InvalidToken = 4,
    ClockMovedBackwards = 5,
    Expired = 6,
    OutsideSupportedContext = 7,
    UnsupportedAction = 8,
    UnsupportedActionMode = 9,
    NonFriendlyAction = 10,
    AreaTargetedAction = 11,
    NoEligibleFriendlyCandidate = 12,
    Rewritten = 13,
    NonMovementAction = 14,
}

public readonly record struct FarHelpOneShotDecision(
    FarHelpOneShotState NextState,
    ulong ForwardTargetId,
    int SelectedCandidateIndex,
    FarHelpOneShotDecisionKind Kind,
    FarHelpOneShotReason Reason)
{
    public bool ShouldRewrite => Kind == FarHelpOneShotDecisionKind.RewriteTarget;

    public bool ConsumedActionIntent =>
        Kind is FarHelpOneShotDecisionKind.ConsumedWithoutRewrite or
            FarHelpOneShotDecisionKind.RewriteTarget;
}

/// <summary>
/// Owns exactly one eligible macro action after /farhelp. Selection occurs for
/// that action so its own target-valid and native range/LoS results are used.
/// These rules return a target ID but never execute an action themselves.
/// </summary>
public static class FarHelpOneShotRules
{
    public const long DefaultLifetimeMilliseconds = 750;
    public const ulong InvalidSuppressedTargetId = 0;

    public static FarHelpOneShotState Arm(
        long nowMilliseconds,
        long lifetimeMilliseconds = DefaultLifetimeMilliseconds)
    {
        if (nowMilliseconds < 0 || lifetimeMilliseconds <= 0)
            return FarHelpOneShotState.Initial;

        var token = new FarHelpOneShotToken(
            nowMilliseconds,
            SaturatingAdd(nowMilliseconds, lifetimeMilliseconds));
        return token.IsValid
            ? new FarHelpOneShotState(token)
            : FarHelpOneShotState.Initial;
    }

    public static FarHelpOneShotDecision Observe(
        FarHelpOneShotState previous,
        FarHelpActionAttempt attempt,
        IReadOnlyList<FarHelpSelectionCandidate>? candidates)
    {
        if (attempt.HardReset)
            return Cleared(attempt.OriginalTargetId, FarHelpOneShotReason.HardReset);

        if (previous.Token is not { } token)
            return PassThrough(attempt.OriginalTargetId, FarHelpOneShotReason.NoToken);

        if (!token.IsValid)
            return Cleared(attempt.OriginalTargetId, FarHelpOneShotReason.InvalidToken);

        if (attempt.NowMilliseconds < token.ArmedAtMilliseconds)
            return Cleared(attempt.OriginalTargetId, FarHelpOneShotReason.ClockMovedBackwards);

        if (attempt.NowMilliseconds >= token.ExpiresAtMilliseconds)
            return ClearedSuppressed(FarHelpOneShotReason.Expired);

        if (!attempt.IsEligibleMacroActionAttempt)
        {
            return new FarHelpOneShotDecision(
                previous,
                attempt.OriginalTargetId,
                -1,
                FarHelpOneShotDecisionKind.Waiting,
                FarHelpOneShotReason.NotEligibleMacroActionAttempt);
        }

        // The first eligible macro action owns and consumes the token before
        // native dispatch. A failure cannot drift into a later press or line.
        var failure = GetSelectionFailure(attempt);
        if (failure != FarHelpOneShotReason.None)
            return ConsumedFallback(attempt, failure);

        var selectedIndex = FarHelpSelectionRules.SelectBestIndex(candidates);
        if (selectedIndex < 0)
            return ConsumedFallback(attempt, FarHelpOneShotReason.NoEligibleFriendlyCandidate);

        var selected = candidates![selectedIndex];
        return new FarHelpOneShotDecision(
            FarHelpOneShotState.Initial,
            selected.GameObjectId,
            selectedIndex,
            FarHelpOneShotDecisionKind.RewriteTarget,
            FarHelpOneShotReason.Rewritten);
    }

    private static FarHelpOneShotReason GetSelectionFailure(FarHelpActionAttempt attempt)
    {
        if (!attempt.IsSupportedContext)
            return FarHelpOneShotReason.OutsideSupportedContext;
        if (!attempt.IsSupportedAction)
            return FarHelpOneShotReason.UnsupportedAction;
        if (!attempt.IsMovementAction)
            return FarHelpOneShotReason.NonMovementAction;
        if (!attempt.IsSupportedActionMode)
            return FarHelpOneShotReason.UnsupportedActionMode;
        if (!attempt.IsFriendlyAction)
            return FarHelpOneShotReason.NonFriendlyAction;
        if (attempt.IsAreaTargetedAction)
            return FarHelpOneShotReason.AreaTargetedAction;

        return FarHelpOneShotReason.None;
    }

    private static FarHelpOneShotDecision ConsumedFallback(
        FarHelpActionAttempt attempt,
        FarHelpOneShotReason reason) =>
        new(
            FarHelpOneShotState.Initial,
            // Far Help is a rescue-only intent. A missing/invalid ally must never
            // degrade into the caller's current target because several supported
            // mobility actions can also target a hostile player.
            InvalidSuppressedTargetId,
            -1,
            FarHelpOneShotDecisionKind.ConsumedWithoutRewrite,
            reason);

    private static FarHelpOneShotDecision PassThrough(
        ulong originalTargetId,
        FarHelpOneShotReason reason) =>
        new(
            FarHelpOneShotState.Initial,
            originalTargetId,
            -1,
            FarHelpOneShotDecisionKind.PassThrough,
            reason);

    private static FarHelpOneShotDecision Cleared(
        ulong originalTargetId,
        FarHelpOneShotReason reason) =>
        new(
            FarHelpOneShotState.Initial,
            originalTargetId,
            -1,
            FarHelpOneShotDecisionKind.Cleared,
            reason);

    private static FarHelpOneShotDecision ClearedSuppressed(
        FarHelpOneShotReason reason) =>
        new(
            FarHelpOneShotState.Initial,
            InvalidSuppressedTargetId,
            -1,
            FarHelpOneShotDecisionKind.Cleared,
            reason);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
