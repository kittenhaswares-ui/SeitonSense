namespace SeitonSense.Plugin.Services;

internal sealed record SeitonPopupSnapshot(
    ulong GameObjectId,
    int Slot,
    uint JobId,
    long StartedAtMilliseconds,
    long EndsAtMilliseconds)
{
    public string SlotLabel => $"S{Slot}";
}
