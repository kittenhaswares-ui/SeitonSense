using System.Numerics;
using SeitonSense.Core;

internal static class SamuraiSmartActionCastSelfTests
{
    public static void ExactRawAndAdjustedPairsAreClosed()
    {
        Equal(34u, SamuraiSmartActionCastRules.SamuraiJobId, "SAM job row");
        Equal(29_530u, SamuraiSmartActionCastRules.OgiNamikiriActionId,
            "Ogi Namikiri raw/resolved action");
        Equal(29_531u, SamuraiSmartActionCastRules.OgiNamikiriFollowUpActionId,
            "Kaeshi Namikiri follow-up");
        Equal(29_536u, SamuraiSmartActionCastRules.TendoSetsugekkaCarrierActionId,
            "Meikyo Shisui raw carrier");
        Equal(41_454u, SamuraiSmartActionCastRules.TendoSetsugekkaActionId,
            "Tendo Setsugekka adjusted action");
        Equal(41_455u, SamuraiSmartActionCastRules.TendoSetsugekkaFollowUpActionId,
            "Tendo Kaeshi Setsugekka follow-up");

        True(SamuraiSmartActionCastRules.IsReviewedBaseCastPair(29_530, 29_530),
            "Ogi raw and resolved pair is reviewed");
        True(SamuraiSmartActionCastRules.IsReviewedBaseCastPair(29_536, 41_454),
            "Tendo carrier and adjusted cast pair is reviewed");
        True(SamuraiSmartActionCastRules.IsReviewedBaseCastPair(41_454, 41_454),
            "direct adjusted Tendo macro row is reviewed");

        foreach (var (raw, resolved) in new (uint Raw, uint Resolved)[]
                 {
                     (0, 0),
                     (29_530, 29_531),
                     (29_531, 29_531),
                     (29_536, 29_536),
                     (29_536, 41_455),
                     (41_454, 41_455),
                     (41_455, 41_455),
                 })
        {
            False(SamuraiSmartActionCastRules.IsReviewedBaseCastPair(raw, resolved),
                $"unreviewed SAM cast pair {raw}->{resolved} fails closed");
        }
    }

    public static void ReviewedCastDecisionPreservesEveryOtherCastPolicy()
    {
        Equal(
            CastedMacroRedirectDecision.RedirectReviewedSmartActionCast,
            CastedMacroRedirectRules.Evaluate(
                redirectTokenArmed: true,
                supportedActionType: true,
                exactActionMetadata: true,
                adjustedCastTimeMilliseconds: 1_500,
                baseCastTime100Milliseconds: 15,
                authoredTargetMatchesVisibleTarget: false,
                allowReviewedSmartActionCastRedirect: true),
            "one caller-proven reviewed cast continues into Smart Action");
        False(
            CastedMacroRedirectRules.ShouldPassThroughWithoutRedirect(
                CastedMacroRedirectDecision.RedirectReviewedSmartActionCast),
            "reviewed SAM cast does not enter the vanilla authored-target path");

        Equal(
            CastedMacroRedirectDecision.PreserveAuthoredTarget,
            CastedMacroRedirectRules.Evaluate(true, true, true, 1_500, 15, true),
            "ordinary visible cast retains v0.42 anti-spin behavior");
        Equal(
            CastedMacroRedirectDecision.SuppressHiddenOrMissingTarget,
            CastedMacroRedirectRules.Evaluate(true, true, true, 1_500, 15, false),
            "ordinary hidden cast retains v0.42 suppression");
        Equal(
            CastedMacroRedirectDecision.NotApplicable,
            CastedMacroRedirectRules.Evaluate(
                true,
                true,
                true,
                adjustedCastTimeMilliseconds: 0,
                baseCastTime100Milliseconds: 0,
                authoredTargetMatchesVisibleTarget: false,
                allowReviewedSmartActionCastRedirect: true),
            "reviewed-cast permission cannot turn an instant action into a cast");
    }

    public static void OgiConeAndTendoDirectProtectionFailClosed()
    {
        var target = Geometry(1, 0x201, 0x301, 0f);
        var farProtected = new SmartActionProtectedActor(
            Geometry(2, 0x202, 0x302, 100f),
            SmartActionProtectionKind.Chiten);

        var ogiShape = SmartActionProtectionRules.ClassifyAttackShape(
            effectRange: 8,
            castType: 3);
        Equal(SmartActionAttackShape.UnsupportedAreaOfEffect, ogiShape,
            "Ogi CastType 3 is not mislabeled as a target-centered circle");
        True(SmartActionProtectionRules.RequiresCompleteHostileSnapshot(ogiShape),
            "Ogi retains the complete hostile protection snapshot");
        False(SmartActionProtectionRules.IsActionProtectionSafe(
                ogiShape,
                target,
                effectRange: 8f,
                [farProtected]),
            "without reviewed cone-angle geometry any protected enemy blocks Ogi");
        True(SmartActionProtectionRules.IsActionProtectionSafe(
                ogiShape,
                target,
                effectRange: 8f,
                []),
            "Ogi is protection-safe only when the complete snapshot has no protection");

        var tendoShape = SmartActionProtectionRules.ClassifyAttackShape(
            effectRange: 0,
            castType: 1);
        Equal(SmartActionAttackShape.DirectSingleTarget, tendoShape,
            "Tendo is an exact direct action");
        True(SmartActionProtectionRules.IsActionProtectionSafe(
                tendoShape,
                target,
                effectRange: 0f,
                [farProtected]),
            "an unrelated protected enemy cannot block direct Tendo");
        False(SmartActionProtectionRules.IsActionProtectionSafe(
                tendoShape,
                target,
                effectRange: 0f,
                [farProtected with { Geometry = target }]),
            "Tendo never selects its protected exact target");
    }

    private static SmartActionActorGeometry Geometry(
        int slot,
        ulong gameObjectId,
        uint entityId,
        float x) =>
        new(
            slot,
            new TargetPressureActorIdentity(gameObjectId, entityId),
            ExactCanonicalIdentity: true,
            new Vector3(x, 0f, 0f),
            HitboxRadius: 1f);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(
                $"{message}: expected {expected}, got {actual}");
    }
}
