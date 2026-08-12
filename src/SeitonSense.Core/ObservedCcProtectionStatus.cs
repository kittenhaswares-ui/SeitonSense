namespace SeitonSense.Core;

/// <summary>
/// Minimal status data read from an actor. Unknown status IDs are discarded by
/// <see cref="CcProtectionStatusCatalog.BuildIndicators"/>.
/// </summary>
public readonly record struct ObservedCcProtectionStatus(uint StatusId, float RemainingTime);
