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
        True(
            SamuraiSmartActionCastRules.IsOgiNamikiriConeAction(29_530),
            "Ogi base uses the reviewed cone protection policy");
        True(
            SamuraiSmartActionCastRules.IsOgiNamikiriConeAction(29_531),
            "Kaeshi Namikiri uses the same reviewed cone protection policy");
        False(
            SamuraiSmartActionCastRules.IsOgiNamikiriConeAction(41_455),
            "Tendo Kaeshi stays on direct-target protection");
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

    public static void OgiConeProtectionIsCandidateLocalAndTendoRemainsDirect()
    {
        var source = Vector3.Zero;
        var target = Geometry(1, 0x201, 0x301, 6f, 0f);

        var ogiShape = SmartActionProtectionRules.ClassifyAttackShape(
            effectRange: 8,
            castType: 3);
        Equal(SmartActionAttackShape.UnsupportedAreaOfEffect, ogiShape,
            "only the exact reviewed Ogi action opts into its cone policy");
        True(SmartActionProtectionRules.RequiresCompleteHostileSnapshot(ogiShape),
            "Ogi cone safety still requires every hostile actor geometry");

        var incidentalProtections = new[]
        {
            new SmartActionProtectedActor(
                Geometry(2, 0x202, 0x302, 3f, 0f),
                SmartActionProtectionKind.Guard),
            new SmartActionProtectedActor(
                Geometry(3, 0x203, 0x303, 4f, 1f),
                SmartActionProtectionKind.Covered),
            new SmartActionProtectedActor(
                Geometry(4, 0x204, 0x304, 5f, -1f),
                SmartActionProtectionKind.Invulnerability),
        };
        True(SamuraiSmartActionCastRules.IsOgiNamikiriProtectionSafe(
                source,
                target,
                SamuraiSmartActionCastRules.OgiNamikiriEffectRangeYalms,
                incidentalProtections),
            "incidental Guard, Cover, and invulnerability actors do not globally stall Ogi");

        var outsideConeChiten = new SmartActionProtectedActor(
            Geometry(2, 0x212, 0x312, 0f, 6f),
            SmartActionProtectionKind.Chiten);
        True(SamuraiSmartActionCastRules.IsOgiNamikiriProtectionSafe(
                source,
                target,
                SamuraiSmartActionCastRules.OgiNamikiriEffectRangeYalms,
                [outsideConeChiten]),
            "an out-of-cone Chiten actor does not veto this candidate");

        var intersectingChiten = new SmartActionProtectedActor(
            Geometry(2, 0x222, 0x322, 4f, 0f),
            SmartActionProtectionKind.Chiten);
        False(SamuraiSmartActionCastRules.IsOgiNamikiriProtectionSafe(
                source,
                target,
                SamuraiSmartActionCastRules.OgiNamikiriEffectRangeYalms,
                [intersectingChiten]),
            "a Chiten actor intersecting the candidate cone still vetoes Ogi");
        var edgeIntersectingChiten = new SmartActionProtectedActor(
            Geometry(2, 0x232, 0x332, 4f, 5.2f),
            SmartActionProtectionKind.Chiten);
        False(SamuraiSmartActionCastRules.IsOgiNamikiriProtectionSafe(
                source,
                target,
                SamuraiSmartActionCastRules.OgiNamikiriEffectRangeYalms,
                [edgeIntersectingChiten]),
            "Chiten hitbox intersection at the cone edge is conservative");
        False(SamuraiSmartActionCastRules.IsOgiNamikiriProtectionSafe(
                source,
                target,
                SamuraiSmartActionCastRules.OgiNamikiriEffectRangeYalms,
                [new SmartActionProtectedActor(target, SmartActionProtectionKind.Guard)]),
            "Ogi never selects a protected primary target");
        False(SamuraiSmartActionCastRules.IsOgiNamikiriProtectionSafe(
                source,
                target,
                effectRange: 7f,
                []),
            "drifted Ogi cone range fails closed");

        var tendoShape = SmartActionProtectionRules.ClassifyAttackShape(
            effectRange: 0,
            castType: 1);
        Equal(SmartActionAttackShape.DirectSingleTarget, tendoShape,
            "Tendo is an exact direct action");
        True(SmartActionProtectionRules.IsActionProtectionSafe(
                tendoShape,
                target,
                effectRange: 0f,
                [outsideConeChiten]),
            "an unrelated protected enemy cannot block direct Tendo");
        False(SmartActionProtectionRules.IsActionProtectionSafe(
                tendoShape,
                target,
                effectRange: 0f,
                [outsideConeChiten with { Geometry = target }]),
            "Tendo never selects its protected exact target");
    }

    private static SmartActionActorGeometry Geometry(
        int slot,
        ulong gameObjectId,
        uint entityId,
        float x,
        float z) =>
        new(
            slot,
            new TargetPressureActorIdentity(gameObjectId, entityId),
            ExactCanonicalIdentity: true,
            new Vector3(x, 0f, z),
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
