namespace SeitonSense.Core;

public enum CastedMacroRedirectDecision
{
    NotApplicable,
    RedirectReviewedSmartActionCast,
    PreserveAuthoredTarget,
    PassThroughStaleLifecycle,
    SuppressHiddenOrMissingTarget,
    SuppressStaleOwnership,
}

/// <summary>
/// Prevents a hidden macro redirect from owning a cast-time action. FFXIV may
/// resolve and auto-face a queued cast after the initial action call, so casts
/// retain only their authored visible target while instant actions keep the
/// existing one-shot redirect behavior. A separately metadata-pinned reviewed
/// cast may explicitly continue into Smart Action; callers must never apply
/// that exception to Near Assist, Near Help, or an unreviewed action.
/// </summary>
public static class CastedMacroRedirectRules
{
    public static bool ShouldPassThroughWithoutRedirect(
        CastedMacroRedirectDecision decision) =>
        decision is
            CastedMacroRedirectDecision.PreserveAuthoredTarget or
            CastedMacroRedirectDecision.PassThroughStaleLifecycle;

    public static bool ShouldTransferExactSmartActionFallbackLease(
        bool exactSmartActionTokenConsumed,
        CastedMacroRedirectDecision decision) =>
        exactSmartActionTokenConsumed &&
        decision is
            CastedMacroRedirectDecision.PreserveAuthoredTarget or
            CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget;

    public static bool CanCommitExactSmartActionFallbackLease(
        ulong consumedGeneration,
        ulong currentGeneration,
        bool newerSmartActionTokenArmed) =>
        consumedGeneration != 0 &&
        consumedGeneration == currentGeneration &&
        !newerSmartActionTokenArmed;

    public static CastedMacroRedirectDecision Evaluate(
        bool redirectTokenArmed,
        bool supportedActionType,
        bool exactActionMetadata,
        int adjustedCastTimeMilliseconds,
        uint baseCastTime100Milliseconds,
        bool authoredTargetMatchesVisibleTarget,
        bool allowReviewedSmartActionCastRedirect = false)
    {
        if (!redirectTokenArmed || !supportedActionType)
            return CastedMacroRedirectDecision.NotApplicable;

        var castTimeProven =
            adjustedCastTimeMilliseconds > 0 ||
            (exactActionMetadata && baseCastTime100Milliseconds > 0);
        if (!castTimeProven)
            return CastedMacroRedirectDecision.NotApplicable;

        if (allowReviewedSmartActionCastRedirect)
            return CastedMacroRedirectDecision.RedirectReviewedSmartActionCast;

        return authoredTargetMatchesVisibleTarget
            ? CastedMacroRedirectDecision.PreserveAuthoredTarget
            : CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget;
    }
}
