namespace SeitonSense.Core;

public enum CastedMacroRedirectDecision
{
    NotApplicable,
    RedirectSmartActionCast,
    PreserveAuthoredTarget,
    PassThroughStaleLifecycle,
    SuppressHiddenOrMissingTarget,
    SuppressStaleOwnership,
    RedirectNearHelpCast,
}

/// <summary>
/// Separates Smart Action and Near Help cast ranking from Near Assist's policy.
/// FFXIV may resolve and auto-face a queued cast after the initial action call,
/// so Near Assist retains only its authored visible target. An exact owned PvP
/// cast may instead continue into Smart Action's hostile ranking or Near Help's
/// friendly ranking. Neither path changes an already started cast's target.
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

    public static bool CanContinueNearHelpCast(
        bool ownedByNearHelp,
        bool supportedActionType,
        uint resolvedActionId,
        bool exactActionMetadata,
        uint metadataRowId,
        bool isPvp,
        bool canTargetFriendly,
        bool isGroundTargeted,
        float range) =>
        ownedByNearHelp &&
        supportedActionType &&
        resolvedActionId != 0 &&
        exactActionMetadata &&
        metadataRowId == resolvedActionId &&
        isPvp &&
        canTargetFriendly &&
        !isGroundTargeted &&
        float.IsFinite(range) &&
        range > 0f;

    public static bool ShouldContinueThroughTargetRanking(
        CastedMacroRedirectDecision decision) =>
        decision is
            CastedMacroRedirectDecision.RedirectSmartActionCast or
            CastedMacroRedirectDecision.RedirectNearHelpCast;

    public static bool CanConsumeExactNearHelpCastClaim(
        ulong claimedGeneration,
        ulong currentGeneration,
        bool ownerAndStateMatch) =>
        claimedGeneration != 0 &&
        claimedGeneration == currentGeneration &&
        ownerAndStateMatch;

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
        bool allowSmartActionCastRedirect = false,
        bool allowNearHelpCastRedirect = false)
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

        if (allowNearHelpCastRedirect && exactActionMetadata)
            return CastedMacroRedirectDecision.RedirectNearHelpCast;

        return authoredTargetMatchesVisibleTarget
            ? CastedMacroRedirectDecision.PreserveAuthoredTarget
            : CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget;
    }
}
