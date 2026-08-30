namespace SeitonSense.Core;

public enum CastedMacroRedirectDecision
{
    NotApplicable,
    PreserveAuthoredTarget,
    PassThroughStaleLifecycle,
    SuppressHiddenOrMissingTarget,
    SuppressStaleOwnership,
}

/// <summary>
/// Prevents a hidden macro redirect from owning a cast-time action. FFXIV may
/// resolve and auto-face a queued cast after the initial action call, so casts
/// retain only their authored visible target while instant actions keep the
/// existing one-shot redirect behavior.
/// </summary>
public static class CastedMacroRedirectRules
{
    public static bool ShouldPassThroughWithoutRedirect(
        CastedMacroRedirectDecision decision) =>
        decision is
            CastedMacroRedirectDecision.PreserveAuthoredTarget or
            CastedMacroRedirectDecision.PassThroughStaleLifecycle;

    public static CastedMacroRedirectDecision Evaluate(
        bool redirectTokenArmed,
        bool supportedActionType,
        bool exactActionMetadata,
        int adjustedCastTimeMilliseconds,
        uint baseCastTime100Milliseconds,
        bool authoredTargetMatchesVisibleTarget)
    {
        if (!redirectTokenArmed || !supportedActionType)
            return CastedMacroRedirectDecision.NotApplicable;

        var castTimeProven =
            adjustedCastTimeMilliseconds > 0 ||
            (exactActionMetadata && baseCastTime100Milliseconds > 0);
        if (!castTimeProven)
            return CastedMacroRedirectDecision.NotApplicable;

        return authoredTargetMatchesVisibleTarget
            ? CastedMacroRedirectDecision.PreserveAuthoredTarget
            : CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget;
    }
}
