namespace SeitonSense.Plugin.Services;

internal sealed record EnemyHudSnapshot(
    int Slot,
    ulong GameObjectId,
    uint EntityId,
    uint JobId,
    bool SeitonEligible,
    bool GuardUnavailable,
    float GuardCooldownRemainingSeconds,
    bool LowMp,
    uint CurrentMp,
    uint MaxMp)
{
    public string SlotLabel => $"S{Slot}";
}
