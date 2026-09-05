namespace SeitonSense.Core;

public static class PaladinShieldSmiteRules
{
    public const uint PaladinJobId = 19;
    public const uint ActionId = 41_430;
    public const long IntentLeaseMilliseconds = 2_000;

    public static bool CanSelectTarget(bool aliveAndTargetable, bool fullGuard,
        bool smartActionSafe, bool inRangeAndLineOfSight) =>
        aliveAndTargetable && fullGuard && smartActionSafe && inRangeAndLineOfSight;

    public static bool CanDispatch(bool enabled, SupportedPvPContext context,
        uint localJobId, bool localAlive, bool metadataVerified, bool ownGuard,
        bool textInputActive, bool higherPriorityClaimed, bool actionReady,
        bool nativeBoundaryReady, bool exactTargetValid) =>
        enabled && context is SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen &&
        localJobId == PaladinJobId && localAlive && metadataVerified && !ownGuard &&
        !textInputActive && !higherPriorityClaimed && actionReady && nativeBoundaryReady && exactTargetValid;
}
