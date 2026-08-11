using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record PersonalAlertSnapshot(
    bool Active,
    bool IsCrystallineConflict,
    bool LocalPlayerAlive,
    PersonalStatusSnapshot[] Statuses,
    EmergencyPurifyProbeSnapshot Purify)
{
    internal static PersonalAlertSnapshot Inactive { get; } = new(
        false,
        false,
        false,
        [],
        EmergencyPurifyProbeSnapshot.Initial);

    internal bool HasUrgentCleanse =>
        Statuses.Any(status => status.AlertKind == PersonalDebuffAlertKind.CleanseUrgent);
}
