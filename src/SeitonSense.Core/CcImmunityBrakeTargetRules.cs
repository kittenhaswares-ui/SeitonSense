namespace SeitonSense.Core;

/// <summary>
/// Resolves the target identity carried by one incoming UseAction call. The
/// game and hotbar helpers may use zero or the default-target sentinel for the
/// currently selected target. The caller must explicitly identify a target
/// that Seiton deliberately suppressed so it can never be reinterpreted as the
/// selected target.
/// </summary>
public static class CcImmunityBrakeTargetRules
{
    public const ulong DefaultTargetSentinel = 0xE0000000UL;

    public static ulong ResolveEffectiveTargetId(
        ulong originalTargetId,
        ulong forwardedTargetId,
        ulong nativeHardTargetId,
        bool targetSuppressedByRedirect = false)
    {
        if (IsConcreteActorId(forwardedTargetId)) return forwardedTargetId;
        if (targetSuppressedByRedirect) return 0;

        var isNativeDefaultTarget = forwardedTargetId is 0 or DefaultTargetSentinel;
        if (!isNativeDefaultTarget || forwardedTargetId != originalTargetId)
            return 0;

        return IsConcreteActorId(nativeHardTargetId) ? nativeHardTargetId : 0;
    }

    public static bool IsDefaultTargetCarrier(ulong targetId) =>
        targetId is 0 or DefaultTargetSentinel;

    private static bool IsConcreteActorId(ulong targetId) =>
        targetId is not 0 and not DefaultTargetSentinel and not ulong.MaxValue;
}
