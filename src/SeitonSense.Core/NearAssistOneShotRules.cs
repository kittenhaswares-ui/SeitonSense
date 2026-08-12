namespace SeitonSense.Core;

public readonly record struct NearAssistOneShotToken(
    bool HasRedirectCandidate,
    int EnemySlot,
    ulong EnemyGameObjectId,
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds)
{
    public bool IsValid =>
        ArmedAtMilliseconds >= 0 &&
        ExpiresAtMilliseconds > ArmedAtMilliseconds &&
        (HasRedirectCandidate
            ? EnemySlotRules.IsValidSlot(EnemySlot) &&
              TargetHighlightRules.IsValidGameObjectId(EnemyGameObjectId)
            : EnemySlot == 0 && EnemyGameObjectId == 0);
}

public readonly record struct NearAssistOneShotState(NearAssistOneShotToken? Token)
{
    public static NearAssistOneShotState Initial => new(null);

    public bool IsArmed => Token is { IsValid: true };
}

public readonly record struct NearAssistActionAttempt(
    ulong OriginalTargetId,
    long NowMilliseconds,
    bool IsEligibleMacroActionAttempt,
    bool IsSupportedContext,
    bool IsSupportedAction,
    bool IsSupportedActionMode,
    bool IsHostileAction,
    bool IsAreaTargetedAction,
    int ResolvedEnemySlot,
    ulong ResolvedEnemyGameObjectId,
    bool IsResolvedEnemyValid,
    bool HasValidActionTarget,
    bool HasRangeAndLineOfSight,
    bool HardReset = false);

public enum NearAssistOneShotDecisionKind
{
    PassThrough = 0,
    Waiting = 1,
    ConsumedWithoutRewrite = 2,
    RewriteTarget = 3,
    Cleared = 4,
}

public enum NearAssistOneShotReason
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
    NonHostileAction = 10,
    AreaTargetedAction = 11,
    EnemySlotChanged = 12,
    EnemyIdentityChanged = 13,
    InvalidResolvedEnemy = 14,
    ActionRejectedForTarget = 15,
    OutOfRangeOrLineOfSight = 16,
    Rewritten = 17,
    NoRedirectCandidate = 18,
}

public readonly record struct NearAssistOneShotDecision(
    NearAssistOneShotState NextState,
    ulong ForwardTargetId,
    NearAssistOneShotDecisionKind Kind,
    NearAssistOneShotReason Reason)
{
    public bool ShouldRewrite => Kind == NearAssistOneShotDecisionKind.RewriteTarget;

    public bool ConsumedActionIntent =>
        Kind is NearAssistOneShotDecisionKind.ConsumedWithoutRewrite or
            NearAssistOneShotDecisionKind.RewriteTarget;
}

public static class NearAssistOneShotRules
{
    public const long DefaultLifetimeMilliseconds = 750;

    public static NearAssistOneShotState Arm(
        int enemySlot,
        ulong enemyGameObjectId,
        long nowMilliseconds,
        long lifetimeMilliseconds = DefaultLifetimeMilliseconds)
    {
        if (!EnemySlotRules.IsValidSlot(enemySlot) ||
            !TargetHighlightRules.IsValidGameObjectId(enemyGameObjectId) ||
            nowMilliseconds < 0 ||
            lifetimeMilliseconds <= 0)
        {
            return NearAssistOneShotState.Initial;
        }

        var expiresAt = SaturatingAdd(nowMilliseconds, lifetimeMilliseconds);
        var token = new NearAssistOneShotToken(
            true,
            enemySlot,
            enemyGameObjectId,
            nowMilliseconds,
            expiresAt);

        return token.IsValid
            ? new NearAssistOneShotState(token)
            : NearAssistOneShotState.Initial;
    }

    public static NearAssistOneShotState ArmFallback(
        long nowMilliseconds,
        long lifetimeMilliseconds = DefaultLifetimeMilliseconds)
    {
        if (nowMilliseconds < 0 || lifetimeMilliseconds <= 0)
            return NearAssistOneShotState.Initial;

        var expiresAt = SaturatingAdd(nowMilliseconds, lifetimeMilliseconds);
        var token = new NearAssistOneShotToken(
            false,
            0,
            0,
            nowMilliseconds,
            expiresAt);
        return token.IsValid
            ? new NearAssistOneShotState(token)
            : NearAssistOneShotState.Initial;
    }

    public static NearAssistOneShotDecision Observe(
        NearAssistOneShotState previous,
        NearAssistActionAttempt attempt)
    {
        if (attempt.HardReset)
            return Cleared(attempt.OriginalTargetId, NearAssistOneShotReason.HardReset);

        if (previous.Token is not { } token)
            return PassThrough(attempt.OriginalTargetId, NearAssistOneShotReason.NoToken);

        if (!token.IsValid)
            return Cleared(attempt.OriginalTargetId, NearAssistOneShotReason.InvalidToken);

        if (attempt.NowMilliseconds < token.ArmedAtMilliseconds)
            return Cleared(attempt.OriginalTargetId, NearAssistOneShotReason.ClockMovedBackwards);

        if (attempt.NowMilliseconds >= token.ExpiresAtMilliseconds)
            return Cleared(attempt.OriginalTargetId, NearAssistOneShotReason.Expired);

        if (!attempt.IsEligibleMacroActionAttempt)
        {
            return new NearAssistOneShotDecision(
                previous,
                attempt.OriginalTargetId,
                NearAssistOneShotDecisionKind.Waiting,
                NearAssistOneShotReason.NotEligibleMacroActionAttempt);
        }

        // From this point onward the one eligible macro action owns the token.
        // Every failure consumes before the caller invokes the original action,
        // so a rejection can never drift into or retry on a later action.
        var failure = GetRewriteFailure(token, attempt);
        if (failure != NearAssistOneShotReason.None)
        {
            return new NearAssistOneShotDecision(
                NearAssistOneShotState.Initial,
                attempt.OriginalTargetId,
                NearAssistOneShotDecisionKind.ConsumedWithoutRewrite,
                failure);
        }

        return new NearAssistOneShotDecision(
            NearAssistOneShotState.Initial,
            token.EnemyGameObjectId,
            NearAssistOneShotDecisionKind.RewriteTarget,
            NearAssistOneShotReason.Rewritten);
    }

    private static NearAssistOneShotReason GetRewriteFailure(
        NearAssistOneShotToken token,
        NearAssistActionAttempt attempt)
    {
        if (!token.HasRedirectCandidate)
            return NearAssistOneShotReason.NoRedirectCandidate;
        if (!attempt.IsSupportedContext)
            return NearAssistOneShotReason.OutsideSupportedContext;
        if (!attempt.IsSupportedAction)
            return NearAssistOneShotReason.UnsupportedAction;
        if (!attempt.IsSupportedActionMode)
            return NearAssistOneShotReason.UnsupportedActionMode;
        if (!attempt.IsHostileAction)
            return NearAssistOneShotReason.NonHostileAction;
        if (attempt.IsAreaTargetedAction)
            return NearAssistOneShotReason.AreaTargetedAction;
        if (attempt.ResolvedEnemySlot != token.EnemySlot)
            return NearAssistOneShotReason.EnemySlotChanged;
        if (attempt.ResolvedEnemyGameObjectId != token.EnemyGameObjectId)
            return NearAssistOneShotReason.EnemyIdentityChanged;
        if (!attempt.IsResolvedEnemyValid ||
            !TargetHighlightRules.IsValidGameObjectId(attempt.ResolvedEnemyGameObjectId))
        {
            return NearAssistOneShotReason.InvalidResolvedEnemy;
        }

        if (!attempt.HasValidActionTarget)
            return NearAssistOneShotReason.ActionRejectedForTarget;
        if (!attempt.HasRangeAndLineOfSight)
            return NearAssistOneShotReason.OutOfRangeOrLineOfSight;

        return NearAssistOneShotReason.None;
    }

    private static NearAssistOneShotDecision PassThrough(
        ulong originalTargetId,
        NearAssistOneShotReason reason) =>
        new(
            NearAssistOneShotState.Initial,
            originalTargetId,
            NearAssistOneShotDecisionKind.PassThrough,
            reason);

    private static NearAssistOneShotDecision Cleared(
        ulong originalTargetId,
        NearAssistOneShotReason reason) =>
        new(
            NearAssistOneShotState.Initial,
            originalTargetId,
            NearAssistOneShotDecisionKind.Cleared,
            reason);

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
