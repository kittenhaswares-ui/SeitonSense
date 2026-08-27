namespace SeitonSense.Core;

/// <summary>
/// Stable runtime scope for native logical-hotbar repeating. The platform
/// adapter proves the broader player/input context and supplies it as
/// <see cref="ContextValid"/>. Outside-combat repeating is an explicit opt-in;
/// combat repeating remains available in PvE, PvP, and Wolves' Den without a
/// territory-specific gate.
/// </summary>
public readonly record struct LogicalHotbarRepeatPolicyInput(
    bool FeatureEnabled,
    bool ContextValid,
    bool InCombat,
    bool AllowOutsideCombat,
    bool InternalPriorityClaimed = false);

public static class LogicalHotbarRepeatPolicy
{
    /// <summary>
    /// Stable fingerprint for settings whose transition must begin a new
    /// physical-hold lifecycle. Transient combat and priority state are
    /// deliberately excluded so an already-certified hold may resume after a
    /// temporary pause; widening the configured outside-combat domain is not.
    /// </summary>
    public static int GetConfigurationFingerprint(
        bool featureEnabled,
        bool allowOutsideCombat) =>
        !featureEnabled
            ? 0
            : allowOutsideCombat
                ? 3
                : 1;

    public static bool IsRepeatDomainActive(LogicalHotbarRepeatPolicyInput input) =>
        input.FeatureEnabled
        && input.ContextValid
        && (input.InCombat || input.AllowOutsideCombat);

    public static bool IsRepeatEnabled(LogicalHotbarRepeatPolicyInput input) =>
        IsRepeatDomainActive(input)
        && !input.InternalPriorityClaimed;

    /// <summary>
    /// Requests suppression only while SeitonSense owns a higher-priority
    /// internal action. The repeat engine still requires the native pulse to be
    /// attributable to its exact current physical owner; unproven input remains
    /// fail-open.
    /// </summary>
    public static bool ShouldSuppressAttributedExternalRepeat(
        LogicalHotbarRepeatPolicyInput input) =>
        IsRepeatDomainActive(input)
        && input.InternalPriorityClaimed;
}
