using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record EnemyHudSnapshot(
    int Slot,
    ulong GameObjectId,
    uint EntityId,
    uint JobId,
    SeitonCueKind SeitonCue,
    long SeitonPulseStartedAtMilliseconds,
    bool GuardUnavailable,
    float GuardCooldownRemainingSeconds,
    uint CurrentHp,
    uint MaxHp,
    bool LowMp,
    uint CurrentMp,
    uint MaxMp)
{
    public string SlotLabel => $"S{Slot}";
    public bool SeitonEligible => SeitonCue == SeitonCueKind.Execute;
}
