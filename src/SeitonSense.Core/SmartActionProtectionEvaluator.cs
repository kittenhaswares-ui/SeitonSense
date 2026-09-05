using System.Numerics;

namespace SeitonSense.Core;

/// <summary>
/// Frozen values for one exact target check. This is not an action request:
/// there is no actor reference, mutable target lookup, queue, or dispatch API.
/// </summary>
public readonly record struct SmartActionProtectionQuery(
    uint ResolvedActionId,
    Vector3 SourcePosition,
    SmartActionActorGeometry Target,
    SmartActionAttackShape AttackShape,
    float EffectRange,
    bool ActionIgnoresGuard,
    bool AllowDamageOnlyInvulnerabilityForCcUtility = false,
    bool OgiConeMetadataVerified = false);

public enum SmartActionProtectionRejection : byte
{
    UnsafeOrAmbiguousProtection,
    None,
}

/// <summary>
/// A decision is bound to the observed action and actor. It cannot substitute
/// another target, and permission here never implies native action acceptance.
/// </summary>
public readonly record struct SmartActionProtectionDecision(
    uint ResolvedActionId,
    SmartActionActorGeometry Target,
    SmartActionProtectionRejection Rejection)
{
    public bool Allowed => Rejection == SmartActionProtectionRejection.None;
}

public static class SmartActionProtectionEvaluator
{
    /// <summary>
    /// Evaluates already captured, value-only protection observations. The
    /// caller retains snapshot ownership and must not mutate it during this
    /// synchronous call. Evaluation never reads the client or ranks targets.
    /// Branch order intentionally preserves the existing Smart Action policy.
    /// </summary>
    public static SmartActionProtectionDecision Evaluate(
        SmartActionProtectionQuery query,
        IReadOnlyList<SmartActionProtectedActor> protectedActors)
    {
        bool safe;
        if (query.AllowDamageOnlyInvulnerabilityForCcUtility)
        {
            safe = query.AttackShape == SmartActionAttackShape.DirectSingleTarget &&
                   query.EffectRange == 0f &&
                   SmartActionProtectionRules.IsDirectCrowdControlUtilityTargetSafe(
                       query.Target, protectedActors);
        }
        else if (SamuraiSmartActionCastRules.IsOgiNamikiriConeAction(query.ResolvedActionId) &&
                 query.OgiConeMetadataVerified)
        {
            safe = SamuraiSmartActionCastRules.IsOgiNamikiriProtectionSafe(
                query.SourcePosition, query.Target, query.EffectRange,
                protectedActors, query.ActionIgnoresGuard);
        }
        else
        {
            safe = SmartActionProtectionRules.IsActionProtectionSafe(
                query.AttackShape, query.Target, query.EffectRange,
                protectedActors, query.ActionIgnoresGuard);
        }

        return new SmartActionProtectionDecision(
            query.ResolvedActionId, query.Target,
            safe ? SmartActionProtectionRejection.None
                : SmartActionProtectionRejection.UnsafeOrAmbiguousProtection);
    }
}
