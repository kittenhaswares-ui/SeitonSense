using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record PersonalStatusSnapshot(
    uint StatusId,
    string Name,
    uint IconId,
    PersonalDebuffAlertKind AlertKind,
    uint SourceId,
    ulong InstanceToken,
    long RemainingMilliseconds,
    long ExpiresAtMilliseconds,
    long PulseStartedAtMilliseconds,
    bool TriggerEntryPulse)
{
    internal const long EntryPulseDurationMilliseconds = 400;

    public bool CanTriggerPurifyBuffer =>
        AlertKind == PersonalDebuffAlertKind.CleanseUrgent && InstanceToken != 0;

    public bool IsEntryPulseActive(long nowMilliseconds) =>
        PulseStartedAtMilliseconds >= 0 &&
        nowMilliseconds >= PulseStartedAtMilliseconds &&
        nowMilliseconds - PulseStartedAtMilliseconds < EntryPulseDurationMilliseconds;
}
