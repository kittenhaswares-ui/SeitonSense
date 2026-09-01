namespace SeitonSense.Core;

public enum CastedMacroRedirectDecision
{
    NotApplicable,
    RedirectSmartActionCast,
    PreserveAuthoredTarget,
    PassThroughStaleLifecycle,
    SuppressHiddenOrMissingTarget,
    SuppressStaleOwnership,
}

/// <summary>
/// Separates Smart Action cast ranking from the assist helpers' cast policy.
/// FFXIV may resolve and auto-face a queued cast after the initial action call,
/// so Near Assist and Near Help retain only their authored visible target. An
/// exact Smart Action-owned hostile PvP cast may instead continue into ordinary
/// Smart Target ranking; callers must never apply that path to the assist helpers.
/// </summary>
public static class CastedMacroRedirectRules
{
    public static bool CanContinueSmartActionCast(
        bool ownedBySmartAction,
        bool supportedActionType,
        uint resolvedActionId,
        bool exactActionMetadata,
        uint metadataRowId,
        bool isPvp,
        bool canTargetHostile,
        bool isGroundTargeted,
        float range) =>
        ownedBySmartAction &&
        supportedActionType &&
        resolvedActionId != 0 &&
        exactActionMetadata &&
        metadataRowId == resolvedActionId &&
        isPvp &&
        canTargetHostile &&
        !isGroundTargeted &&
        float.IsFinite(range) &&
        range > 0f;

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
        bool allowSmartActionCastRedirect = false)
    {
        if (!redirectTokenArmed || !supportedActionType)
            return CastedMacroRedirectDecision.NotApplicable;

        var castTimeProven =
            adjustedCastTimeMilliseconds > 0 ||
            (exactActionMetadata && baseCastTime100Milliseconds > 0);
        if (!castTimeProven)
            return CastedMacroRedirectDecision.NotApplicable;

        if (allowSmartActionCastRedirect && exactActionMetadata)
            return CastedMacroRedirectDecision.RedirectSmartActionCast;

        return authoredTargetMatchesVisibleTarget
            ? CastedMacroRedirectDecision.PreserveAuthoredTarget
            : CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget;
    }
}
