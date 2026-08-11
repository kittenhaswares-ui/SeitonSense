using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record PersonalAlertSnapshot(
    bool Active,
    SupportedPvPContext Context,
    bool LocalPlayerAlive,
    PersonalStatusSnapshot[] Statuses,
    EmergencyPurifyProbeSnapshot Purify)
{
    internal static PersonalAlertSnapshot Inactive { get; } = new(
        false,
        SupportedPvPContext.None,
        false,
        [],
        EmergencyPurifyProbeSnapshot.Initial);

    internal bool HasUrgentCleanse =>
        Statuses.Any(status => status.AlertKind == PersonalDebuffAlertKind.CleanseUrgent);
}
