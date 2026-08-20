namespace SeitonSense.Core;

/// <summary>
/// The exact self-only action frozen for one physical held-key generation.
/// Runtime code must never substitute another action or target after this
/// intent is created.
/// </summary>
public readonly record struct SmartRecuperateIntent(
    uint ActionId,
    TargetPressureActorIdentity LocalPlayer)
{
    public bool IsValid =>
        ActionId == SmartRecuperateRules.ActionId &&
        LocalPlayer.IsValid;
}

public readonly record struct SmartRecuperateObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    bool IsLocalPlayerTargetable,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    uint ResolvedActionId,
    bool ActionLocallyReady,
    uint CurrentHp,
    uint MaximumHp,
    uint CurrentMp,
    uint MaximumMp,
    bool HardReset = false);

public enum SmartRecuperateDecisionKind
{
    None = 0,
    Dispatch = 1,
    Cancelled = 2,
}

public enum SmartRecuperateDecisionReason
{
    None = 0,
    HardReset = 1,
    ConfigurationDisabled = 2,
    OutsideCrystallineConflict = 3,
    LocalPlayerIdentityInvalid = 4,
    LocalPlayerDead = 5,
    LocalPlayerUntargetable = 6,
    MetadataUnverified = 7,
    GuardSuppressed = 8,
    HigherPriorityClaimed = 9,
    InputProbeUnavailable = 10,
    TextInputActive = 11,
    NoHeldGameplayKey = 12,
    ResolvedActionInvalid = 13,
    ActionNotReady = 14,
    HealthTelemetryInvalid = 15,
    MissingHealthBelowThreshold = 16,
    MpTelemetryInvalid = 17,
    InsufficientMp = 18,
}

public readonly record struct SmartRecuperateDecision(
    SmartRecuperateDecisionKind Kind,
    SmartRecuperateDecisionReason Reason,
    SmartRecuperateIntent? Intent = null)
{
    public bool ShouldDispatch =>
        Kind == SmartRecuperateDecisionKind.Dispatch &&
        Intent is { IsValid: true };

    /// <summary>
    /// Consumption happens before terminal native revalidation. A rejection,
    /// exception, or state drift after this point is terminal for this physical
    /// generation and must never be retried.
    /// </summary>
    public bool ShouldConsumeInputGeneration => ShouldDispatch;
}

/// <summary>
/// Pure policy for the optional held-gameplay-key Smart Recuperate helper.
/// It freezes exactly PvP Recuperate on the exact local actor when at least
/// 16,000 HP is missing and at least the exact 2,000 MP action cost is present.
/// It does not reserve additional MP, buffer an action, change targets, or retry.
/// </summary>
public static class SmartRecuperateRules
{
    public const uint ActionId = 29_711;
    public const uint MinimumMissingHp = 16_000;
    public const uint MpCost = 2_000;

    public static SmartRecuperateDecision Observe(
        SmartRecuperateObservation observation)
    {
        var failure = GetGateFailure(observation);
        if (failure != SmartRecuperateDecisionReason.None)
        {
            return new SmartRecuperateDecision(
                observation.HardReset
                    ? SmartRecuperateDecisionKind.Cancelled
                    : SmartRecuperateDecisionKind.None,
                failure);
        }

        return new SmartRecuperateDecision(
            SmartRecuperateDecisionKind.Dispatch,
            SmartRecuperateDecisionReason.None,
            new SmartRecuperateIntent(
                observation.ResolvedActionId,
                observation.LocalPlayer));
    }

    public static bool HasValidHealth(uint currentHp, uint maximumHp) =>
        currentHp > 0 && maximumHp > 0 && currentHp <= maximumHp;

    public static bool HasMinimumMissingHp(
        uint currentHp,
        uint maximumHp) =>
        HasValidHealth(currentHp, maximumHp) &&
        (ulong)maximumHp - currentHp >= MinimumMissingHp;

    public static uint GetMissingHp(uint currentHp, uint maximumHp) =>
        HasValidHealth(currentHp, maximumHp)
            ? maximumHp - currentHp
            : 0;

    public static bool HasValidMp(uint currentMp, uint maximumMp) =>
        maximumMp > 0 && currentMp <= maximumMp;

    public static bool HasMinimumMp(uint currentMp, uint maximumMp) =>
        HasValidMp(currentMp, maximumMp) && currentMp >= MpCost;

    /// <summary>
    /// Revalidates the one frozen self-only intent immediately before the sole
    /// native request. Input ownership is deliberately absent: the caller must
    /// already have consumed the generation before invoking this method.
    /// </summary>
    public static bool CanUseFrozenIntent(
        SmartRecuperateIntent intent,
        bool configurationEnabled,
        bool isCrystallineConflict,
        TargetPressureActorIdentity currentLocalPlayer,
        bool isLocalPlayerAlive,
        bool isLocalPlayerTargetable,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        uint resolvedActionId,
        bool actionLocallyReady,
        uint currentHp,
        uint maximumHp,
        uint currentMp,
        uint maximumMp) =>
        intent.IsValid &&
        configurationEnabled &&
        isCrystallineConflict &&
        currentLocalPlayer == intent.LocalPlayer &&
        isLocalPlayerAlive &&
        isLocalPlayerTargetable &&
        metadataVerified &&
        !actionHelpersSuppressedByGuard &&
        !higherPriorityClaimed &&
        resolvedActionId == intent.ActionId &&
        actionLocallyReady &&
        HasMinimumMissingHp(currentHp, maximumHp) &&
        HasMinimumMp(currentMp, maximumMp);

    private static SmartRecuperateDecisionReason GetGateFailure(
        SmartRecuperateObservation observation)
    {
        if (observation.HardReset)
            return SmartRecuperateDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return SmartRecuperateDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return SmartRecuperateDecisionReason.OutsideCrystallineConflict;
        if (!observation.LocalPlayer.IsValid)
            return SmartRecuperateDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return SmartRecuperateDecisionReason.LocalPlayerDead;
        if (!observation.IsLocalPlayerTargetable)
            return SmartRecuperateDecisionReason.LocalPlayerUntargetable;
        if (!observation.MetadataVerified)
            return SmartRecuperateDecisionReason.MetadataUnverified;
        if (observation.ActionHelpersSuppressedByGuard)
            return SmartRecuperateDecisionReason.GuardSuppressed;
        if (observation.HigherPriorityClaimed)
            return SmartRecuperateDecisionReason.HigherPriorityClaimed;
        if (!observation.InputProbeSucceeded)
            return SmartRecuperateDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return SmartRecuperateDecisionReason.TextInputActive;
        if (!observation.HeldGameplayKeyEligible)
            return SmartRecuperateDecisionReason.NoHeldGameplayKey;
        if (observation.ResolvedActionId != ActionId)
            return SmartRecuperateDecisionReason.ResolvedActionInvalid;
        if (!observation.ActionLocallyReady)
            return SmartRecuperateDecisionReason.ActionNotReady;
        if (!HasValidHealth(observation.CurrentHp, observation.MaximumHp))
            return SmartRecuperateDecisionReason.HealthTelemetryInvalid;
        if (!HasMinimumMissingHp(observation.CurrentHp, observation.MaximumHp))
            return SmartRecuperateDecisionReason.MissingHealthBelowThreshold;
        if (!HasValidMp(observation.CurrentMp, observation.MaximumMp))
            return SmartRecuperateDecisionReason.MpTelemetryInvalid;
        if (!HasMinimumMp(observation.CurrentMp, observation.MaximumMp))
            return SmartRecuperateDecisionReason.InsufficientMp;
        return SmartRecuperateDecisionReason.None;
    }
}
