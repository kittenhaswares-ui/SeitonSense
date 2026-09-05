using System.Numerics;
using SeitonSense.Core;

internal static class SmartActionProtectionEvaluatorSelfTests
{
    public static void OrdinaryProtectionDecisionsMatchLegacyPolicy()
    {
        var target = Geometry(1, new Vector3(4, 0, 0));
        var peer = Geometry(2, new Vector3(4, 0, 2));
        foreach (var shape in new[]
                 {
                     SmartActionAttackShape.DirectSingleTarget,
                     SmartActionAttackShape.TargetCenteredCircle,
                     SmartActionAttackShape.UnsupportedAreaOfEffect,
                     (SmartActionAttackShape)255,
                 })
        foreach (var range in new[] { 0f, 5f, float.NaN })
        foreach (var ignoresGuard in new[] { false, true })
        foreach (var kind in Enumerable.Range(0, 16).Select(value => (SmartActionProtectionKind)value))
        {
            SmartActionProtectedActor[] protections = kind == SmartActionProtectionKind.None
                ? [] : [new(target, kind), new(peer, SmartActionProtectionKind.Chiten)];
            var query = new SmartActionProtectionQuery(
                41_430, Vector3.Zero, target, shape, range, ignoresGuard);
            var decision = SmartActionProtectionEvaluator.Evaluate(query, protections);
            Equal(LegacyEvaluate(query, protections), decision.Allowed,
                $"ordinary shape={shape}, range={range}, protection={kind}, bypass={ignoresGuard}");
            Equal(query.ResolvedActionId, decision.ResolvedActionId, "action remains frozen");
            Equal(target, decision.Target, "target remains frozen even on rejection");
        }
    }

    public static void UtilityAndOgiBranchOrderMatchesLegacyPolicy()
    {
        var target = Geometry(1, new Vector3(4, 0, 0));
        foreach (var action in new[]
                 {
                     29_399u,
                     SamuraiSmartActionCastRules.OgiNamikiriActionId,
                     SamuraiSmartActionCastRules.OgiNamikiriFollowUpActionId,
                 })
        foreach (var utility in new[] { false, true })
        foreach (var ogiVerified in new[] { false, true })
        foreach (var shape in Enum.GetValues<SmartActionAttackShape>())
        foreach (var range in new[] { 0f, 8f })
        foreach (var peerPosition in new[] { new Vector3(5, 0, 1), new Vector3(-5, 0, 0) })
        {
            SmartActionProtectedActor[] protections =
            [
                new(target, SmartActionProtectionKind.Invulnerability),
                new(Geometry(2, peerPosition), SmartActionProtectionKind.Chiten),
            ];
            var query = new SmartActionProtectionQuery(
                action, Vector3.Zero, target, shape, range, false, utility, ogiVerified);
            Equal(LegacyEvaluate(query, protections),
                SmartActionProtectionEvaluator.Evaluate(query, protections).Allowed,
                "utility override precedes the verified Ogi branch; ordinary fallback stays unchanged");
        }

        var cone = new SmartActionProtectionQuery(
            SamuraiSmartActionCastRules.OgiNamikiriActionId,
            Vector3.Zero, target, SmartActionAttackShape.UnsupportedAreaOfEffect, 8f,
            false, OgiConeMetadataVerified: true);
        SmartActionProtectedActor[] behind =
            [new(Geometry(2, new Vector3(-5, 0, 0)), SmartActionProtectionKind.Chiten)];
        Equal(true, SmartActionProtectionEvaluator.Evaluate(cone, behind).Allowed,
            "reviewed Ogi does not acquire the unsupported-area global Chiten veto");
        Equal(false, SmartActionProtectionEvaluator.Evaluate(
            cone with { OgiConeMetadataVerified = false }, behind).Allowed,
            "unverified Ogi retains conservative unsupported-area policy");
    }

    public static void FrozenDecisionRejectsProtectionWithoutRetargeting()
    {
        Equal(false, default(SmartActionProtectionDecision).Allowed,
            "an uninitialized decision is never permission");
        var target = Geometry(1, new Vector3(4, 0, 0));
        var query = new SmartActionProtectionQuery(
            41_430, Vector3.Zero, target, SmartActionAttackShape.DirectSingleTarget, 0f, true);
        SmartActionProtectedActor[] protections = [new(target, SmartActionProtectionKind.Guard)];
        var allowed = SmartActionProtectionEvaluator.Evaluate(query, protections);
        Equal(true, allowed.Allowed, "verified bypass permits only the selected target's Guard");
        protections[0] = new(target, SmartActionProtectionKind.Guard | SmartActionProtectionKind.Covered);
        var blocked = SmartActionProtectionEvaluator.Evaluate(query, protections);
        Equal(false, blocked.Allowed, "a later Cover observation rejects the exact same target");
        Equal(SmartActionProtectionRejection.UnsafeOrAmbiguousProtection, blocked.Rejection,
            "failure is an explicit rejection, not an alternate actor");
        Equal(allowed.Target, blocked.Target, "rejection never chooses another target");
        Equal(true, allowed.Allowed, "prior decision is value-only and cannot be mutated by later observations");
        Equal(false, SmartActionProtectionEvaluator.Evaluate(
            query with { Target = target with { ExactCanonicalIdentity = false } }, protections).Allowed,
            "ambiguous identity remains closed");
        Equal(false, SmartActionProtectionEvaluator.Evaluate(
            query with { Target = target with { Position = new Vector3(float.NaN, 0, 0) } }, []).Allowed,
            "invalid world geometry remains closed");
    }

    // Deliberately independent transcription of the pre-extraction branch order.
    private static bool LegacyEvaluate(
        SmartActionProtectionQuery query,
        IReadOnlyList<SmartActionProtectedActor> protections)
    {
        if (query.AllowDamageOnlyInvulnerabilityForCcUtility)
            return query.AttackShape == SmartActionAttackShape.DirectSingleTarget &&
                   query.EffectRange == 0f &&
                   SmartActionProtectionRules.IsDirectCrowdControlUtilityTargetSafe(query.Target, protections);
        if (SamuraiSmartActionCastRules.IsOgiNamikiriConeAction(query.ResolvedActionId) &&
            query.OgiConeMetadataVerified)
            return SamuraiSmartActionCastRules.IsOgiNamikiriProtectionSafe(
                query.SourcePosition, query.Target, query.EffectRange, protections, query.ActionIgnoresGuard);
        return SmartActionProtectionRules.IsActionProtectionSafe(
            query.AttackShape, query.Target, query.EffectRange, protections, query.ActionIgnoresGuard);
    }

    private static SmartActionActorGeometry Geometry(int slot, Vector3 position) =>
        new(slot, new TargetPressureActorIdentity((ulong)(0x400 + slot), (uint)(0x300 + slot)),
            true, position, 0.5f);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
