namespace SeitonSense.Core;

public enum PaladinInterveneBlockReason : byte
{
    None,
    LocalPlayerUnavailable,
    GuardianMetadataUnavailable,
    OwnGuard,
    LowMp,
    ProtectingAlly,
}

public static class PaladinInterveneSafetyRules
{
    public const uint MinimumMp = 3_000;

    // Cover belongs to the protecting Paladin. Covered belongs to the ally;
    // only its source identifies which Paladin is protecting that ally.
    public static bool IsOwnGuardianLink(
        uint localEntityId,
        uint statusHolderEntityId,
        uint statusSourceEntityId,
        bool isCover,
        bool isCovered) =>
        IsEntityId(localEntityId) &&
        IsEntityId(statusHolderEntityId) &&
        ((isCover && statusHolderEntityId == localEntityId) ||
         (isCovered && statusHolderEntityId != localEntityId &&
          statusSourceEntityId == localEntityId));

    public static PaladinInterveneBlockReason Evaluate(
        uint actionId,
        bool localPlayerValid,
        bool guardianMetadataVerified,
        uint currentMp,
        bool ownGuardActiveOrPropagating,
        bool protectingAlly)
    {
        if (actionId != MiracleInterceptConfirmationRules.InterveneActionId)
            return PaladinInterveneBlockReason.None;
        if (!localPlayerValid) return PaladinInterveneBlockReason.LocalPlayerUnavailable;
        if (!guardianMetadataVerified)
            return PaladinInterveneBlockReason.GuardianMetadataUnavailable;
        if (ownGuardActiveOrPropagating) return PaladinInterveneBlockReason.OwnGuard;
        if (currentMp < MinimumMp) return PaladinInterveneBlockReason.LowMp;
        return protectingAlly
            ? PaladinInterveneBlockReason.ProtectingAlly
            : PaladinInterveneBlockReason.None;
    }

    private static bool IsEntityId(uint id) => id is not 0 and not 0xE0000000 and not uint.MaxValue;
}
