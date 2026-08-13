namespace SeitonSense.Core;

public readonly record struct FarHelpFallbackSuppressionToken(
    uint ActionType,
    uint RawActionId,
    uint ResolvedActionId,
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds)
{
    public bool IsValid =>
        RawActionId != 0 &&
        ResolvedActionId != 0 &&
        ArmedAtMilliseconds >= 0 &&
        ExpiresAtMilliseconds > ArmedAtMilliseconds;
}

public readonly record struct FarHelpFallbackSuppressionState(
    FarHelpFallbackSuppressionToken? Token)
{
    public static FarHelpFallbackSuppressionState Initial => new(null);

    public bool IsArmed => Token is { IsValid: true };
}

public readonly record struct FarHelpFallbackSuppressionAttempt(
    uint ActionType,
    uint RawActionId,
    uint ResolvedActionId,
    long NowMilliseconds);

public enum FarHelpFallbackSuppressionDecisionKind
{
    PassThrough = 0,
    Waiting = 1,
    Suppress = 2,
    Cleared = 3,
}

public readonly record struct FarHelpFallbackSuppressionDecision(
    FarHelpFallbackSuppressionState NextState,
    FarHelpFallbackSuppressionDecisionKind Kind)
{
    public bool ShouldSuppress => Kind == FarHelpFallbackSuppressionDecisionKind.Suppress;
}

/// <summary>
/// Protects users who still have the former four-line Far Help macro. Once Far
/// Help owns a movement action, one immediately following invocation of that
/// exact authored action is invalidated instead of falling through to &lt;t&gt;.
/// Matching calls remain suppressed for the bounded quarantine lifetime so
/// queued/turbo duplicates cannot escape. It never dispatches an action and
/// unrelated actions cannot consume it.
/// </summary>
public static class FarHelpFallbackSuppressionRules
{
    public const long DefaultLifetimeMilliseconds = 750;

    public static FarHelpFallbackSuppressionState Arm(
        uint actionType,
        uint rawActionId,
        uint resolvedActionId,
        long nowMilliseconds,
        long lifetimeMilliseconds = DefaultLifetimeMilliseconds)
    {
        if (rawActionId == 0 ||
            resolvedActionId == 0 ||
            nowMilliseconds < 0 ||
            lifetimeMilliseconds <= 0)
            return FarHelpFallbackSuppressionState.Initial;

        var expiresAt = nowMilliseconds > long.MaxValue - lifetimeMilliseconds
            ? long.MaxValue
            : nowMilliseconds + lifetimeMilliseconds;
        var token = new FarHelpFallbackSuppressionToken(
            actionType,
            rawActionId,
            resolvedActionId,
            nowMilliseconds,
            expiresAt);
        return token.IsValid
            ? new FarHelpFallbackSuppressionState(token)
            : FarHelpFallbackSuppressionState.Initial;
    }

    public static FarHelpFallbackSuppressionDecision Observe(
        FarHelpFallbackSuppressionState previous,
        FarHelpFallbackSuppressionAttempt attempt)
    {
        if (previous.Token is not { } token)
            return PassThrough();
        if (!token.IsValid || attempt.NowMilliseconds < token.ArmedAtMilliseconds)
            return Cleared();
        if (attempt.NowMilliseconds >= token.ExpiresAtMilliseconds)
            return Cleared();

        // Hook order can expose the same PvP action through different raw IDs or
        // Action/PvPAction representations. The adjusted action identity is the
        // authoritative equivalence boundary.
        if (attempt.ResolvedActionId != token.ResolvedActionId)
        {
            return new FarHelpFallbackSuppressionDecision(
                previous,
                FarHelpFallbackSuppressionDecisionKind.Waiting);
        }

        return new FarHelpFallbackSuppressionDecision(
            previous,
            FarHelpFallbackSuppressionDecisionKind.Suppress);
    }

    private static FarHelpFallbackSuppressionDecision PassThrough() =>
        new(
            FarHelpFallbackSuppressionState.Initial,
            FarHelpFallbackSuppressionDecisionKind.PassThrough);

    private static FarHelpFallbackSuppressionDecision Cleared() =>
        new(
            FarHelpFallbackSuppressionState.Initial,
            FarHelpFallbackSuppressionDecisionKind.Cleared);
}
