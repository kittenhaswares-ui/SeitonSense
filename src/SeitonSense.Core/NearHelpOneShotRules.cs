namespace SeitonSense.Core;

public readonly record struct NearHelpOneShotToken(
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds)
{
    public bool IsValid =>
        ArmedAtMilliseconds >= 0 &&
        ExpiresAtMilliseconds > ArmedAtMilliseconds;
}

public readonly record struct NearHelpOneShotState(NearHelpOneShotToken? Token)
{
    public static NearHelpOneShotState Initial => new(null);

    public bool IsArmed => Token is { IsValid: true };
}

public readonly record struct NearHelpActionAttempt(
    ulong OriginalTargetId,
    long NowMilliseconds,
    bool IsEligibleMacroActionAttempt,
    bool IsSupportedContext,
    bool IsSupportedAction,
    bool IsSupportedActionMode,
    bool IsFriendlyAction,
    bool IsAreaTargetedAction,
    bool IsFallbackCarrier,
    bool HardReset = false);

public enum NearHelpOneShotDecisionKind
{
    PassThrough = 0,
    Waiting = 1,
    ConsumedWithoutRewrite = 2,
    RewriteTarget = 3,
    Cleared = 4,
}

public enum NearHelpOneShotReason
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
}

public readonly record struct NearHelpOneShotDecision(
    NearHelpOneShotState NextState,
    ulong ForwardTargetId,
    int SelectedCandidateIndex,
    NearHelpOneShotDecisionKind Kind,
    NearHelpOneShotReason Reason,
    NearHelpSelectionReason SelectionReason = NearHelpSelectionReason.None)
{
    public bool ShouldRewrite => Kind == NearHelpOneShotDecisionKind.RewriteTarget;

    public bool ConsumedActionIntent =>
        Kind is NearHelpOneShotDecisionKind.ConsumedWithoutRewrite or
            NearHelpOneShotDecisionKind.RewriteTarget;
}

/// <summary>
/// Owns exactly one eligible macro action after /nearhelp. Candidate selection
/// happens for that action, not when the command is armed. These rules never
/// execute an action: they return the one target ID the caller may forward.
/// </summary>
public static class NearHelpOneShotRules
{
    public const long DefaultLifetimeMilliseconds = 750;
    public const ulong InvalidFallbackCarrierTargetId = 0;

    public static NearHelpOneShotState Arm(
        long nowMilliseconds,
        long lifetimeMilliseconds = DefaultLifetimeMilliseconds)
    {
        if (nowMilliseconds < 0 || lifetimeMilliseconds <= 0)
            return NearHelpOneShotState.Initial;

        var token = new NearHelpOneShotToken(
            nowMilliseconds,
            SaturatingAdd(nowMilliseconds, lifetimeMilliseconds));
        return token.IsValid
            ? new NearHelpOneShotState(token)
            : NearHelpOneShotState.Initial;
    }

    public static NearHelpOneShotDecision Observe(
        NearHelpOneShotState previous,
        NearHelpActionAttempt attempt,
        IReadOnlyList<NearHelpSelectionCandidate>? candidates,
        bool preferIncomingPressure = false,
        bool hasTrustedPressureView = false)
    {
        if (attempt.HardReset)
            return Cleared(attempt.OriginalTargetId, NearHelpOneShotReason.HardReset);

        if (previous.Token is not { } token)
            return PassThrough(attempt.OriginalTargetId, NearHelpOneShotReason.NoToken);

        if (!token.IsValid)
            return Cleared(attempt.OriginalTargetId, NearHelpOneShotReason.InvalidToken);

        if (attempt.NowMilliseconds < token.ArmedAtMilliseconds)
            return Cleared(attempt.OriginalTargetId, NearHelpOneShotReason.ClockMovedBackwards);

        if (attempt.NowMilliseconds >= token.ExpiresAtMilliseconds)
            return Cleared(attempt.OriginalTargetId, NearHelpOneShotReason.Expired);

        if (!attempt.IsEligibleMacroActionAttempt)
        {
            return new NearHelpOneShotDecision(
                previous,
                attempt.OriginalTargetId,
                -1,
                NearHelpOneShotDecisionKind.Waiting,
                NearHelpOneShotReason.NotEligibleMacroActionAttempt);
        }

        // The first eligible macro action owns and consumes the token before
        // the caller invokes native code. It cannot drift into a later press.
        var failure = GetSelectionFailure(attempt);
        if (failure != NearHelpOneShotReason.None)
            return ConsumedFallback(attempt, failure);

        var selection = NearHelpSelectionRules.SelectBest(
            candidates,
            preferIncomingPressure,
            hasTrustedPressureView);
        if (selection.SelectedIndex < 0)
        {
            return ConsumedFallback(
                attempt,
                NearHelpOneShotReason.NoEligibleFriendlyCandidate,
                selection.Reason);
        }

        var selected = candidates![selection.SelectedIndex];
        return new NearHelpOneShotDecision(
            NearHelpOneShotState.Initial,
            selected.GameObjectId,
            selection.SelectedIndex,
            NearHelpOneShotDecisionKind.RewriteTarget,
            NearHelpOneShotReason.Rewritten,
            selection.Reason);
    }

    private static NearHelpOneShotReason GetSelectionFailure(NearHelpActionAttempt attempt)
    {
        if (!attempt.IsSupportedContext)
            return NearHelpOneShotReason.OutsideSupportedContext;
        if (!attempt.IsSupportedAction)
            return NearHelpOneShotReason.UnsupportedAction;
        if (!attempt.IsSupportedActionMode)
            return NearHelpOneShotReason.UnsupportedActionMode;
        if (!attempt.IsFriendlyAction)
            return NearHelpOneShotReason.NonFriendlyAction;
        if (attempt.IsAreaTargetedAction)
            return NearHelpOneShotReason.AreaTargetedAction;

        return NearHelpOneShotReason.None;
    }

    private static NearHelpOneShotDecision ConsumedFallback(
        NearHelpActionAttempt attempt,
        NearHelpOneShotReason reason,
        NearHelpSelectionReason selectionReason = NearHelpSelectionReason.None) =>
        new(
            NearHelpOneShotState.Initial,
            attempt.IsFallbackCarrier
                ? InvalidFallbackCarrierTargetId
                : attempt.OriginalTargetId,
            -1,
            NearHelpOneShotDecisionKind.ConsumedWithoutRewrite,
            reason,
            selectionReason);

    private static NearHelpOneShotDecision PassThrough(
        ulong originalTargetId,
        NearHelpOneShotReason reason) =>
        new(
            NearHelpOneShotState.Initial,
            originalTargetId,
            -1,
            NearHelpOneShotDecisionKind.PassThrough,
            reason);

    private static NearHelpOneShotDecision Cleared(
        ulong originalTargetId,
        NearHelpOneShotReason reason) =>
        new(
            NearHelpOneShotState.Initial,
            originalTargetId,
            -1,
            NearHelpOneShotDecisionKind.Cleared,
            reason);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
