namespace SeitonSense.Core;

/// <summary>
/// A verified, active protection status ready for presentation above an actor.
/// </summary>
public readonly record struct CcProtectionIndicator(
    uint StatusId,
    string Name,
    uint IconId,
    CcProtectionKind Kind,
    float RemainingTime);
