using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

[Flags]
internal enum TargetPressureEvidence
{
    None = 0,
    HardTarget = 1 << 0,
    CastTarget = 1 << 1,
    RecentHarmfulAction = 1 << 2,
    MachinistLimitBreakMarker = 1 << 3,
}

internal readonly record struct CcProtectionDisplay(
    uint StatusId,
    string Name,
    uint IconId,
    CcProtectionKind Kind,
    long ExpiresAtMilliseconds);

internal sealed record TargetPressureOpponentSnapshot(
    ulong GameObjectId,
    uint EntityId,
    uint JobId,
    int EnemySlot,
    TargetPressureEvidence IncomingEvidence,
    int TeamTargetCount,
    IReadOnlyList<CcProtectionDisplay> Protections)
{
    internal bool IsIncoming => IncomingEvidence != TargetPressureEvidence.None;
    internal string SlotLabel => EnemySlot is >= 1 and <= 5 ? $"S{EnemySlot}" : string.Empty;
    internal bool HasDirectIncomingIntent =>
        (IncomingEvidence & (TargetPressureEvidence.HardTarget | TargetPressureEvidence.CastTarget)) != 0;
}

internal sealed record TargetPressureRuntimeSnapshot(
    bool Active,
    bool PressureActive,
    TargetPressureActorIdentity LocalPlayer,
    long PublishedAtMilliseconds,
    IReadOnlyList<TargetPressureOpponentSnapshot> Opponents)
{
    internal static TargetPressureRuntimeSnapshot Inactive { get; } = new(
        false,
        false,
        default,
        -1,
        []);

    internal IReadOnlyList<TargetPressureOpponentSnapshot> IncomingOpponents =>
        Opponents.Where(static opponent => opponent.IsIncoming).ToArray();

    internal TargetPressureOpponentSnapshot? Find(ulong gameObjectId, uint entityId)
    {
        TargetPressureOpponentSnapshot? match = null;
        foreach (var opponent in Opponents)
        {
            if (opponent.GameObjectId != gameObjectId || opponent.EntityId != entityId) continue;
            if (match is not null) return null;
            match = opponent;
        }

        return match;
    }
}

/// <summary>
/// One fresh, exact-self view of enemies whose current hard target or cast
/// target is the local player. Counts are taken from one immutable pressure
/// publication; the union never includes recent-damage or early-marker hints.
/// </summary>
internal readonly record struct DirectSelfPressureSnapshot(
    TargetPressureActorIdentity LocalPlayer,
    long PublishedAtMilliseconds,
    int UniqueEnemyCount,
    int HardTargetEnemyCount,
    int CastTargetEnemyCount);

internal sealed record TargetPressureDiagnostics(
    bool Active,
    int VisibleEnemies,
    int IncomingEnemies,
    int TeamTargetLinks,
    int ProtectionIndicators,
    int RecentPressureSources,
    long DroppedCaptureEvents)
{
    internal static TargetPressureDiagnostics Inactive { get; } = new(false, 0, 0, 0, 0, 0, 0);

    internal string ToChatLine() =>
        $"pressure active={Active}, enemies={VisibleEnemies}, incoming={IncomingEnemies}, " +
        $"team-links={TeamTargetLinks}, protection={ProtectionIndicators}, recent={RecentPressureSources}, " +
        $"capture-dropped={DroppedCaptureEvents}";
}
