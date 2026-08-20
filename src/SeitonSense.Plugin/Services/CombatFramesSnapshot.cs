using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum CombatFrameStatusCategory : byte
{
    Protection = 0,
    CrowdControl = 1,
    Danger = 2,
}

internal sealed record CombatFrameStatusSnapshot(
    uint StatusId,
    string Name,
    uint IconId,
    CombatFrameStatusCategory Category,
    long ExpiresAtMilliseconds);

internal sealed record CombatFrameActorSnapshot(
    CombatFramePlanRow Frame,
    string DisplayName,
    bool GuardUnavailable,
    long GuardReadyAtMilliseconds,
    bool SeitonEligible,
    IReadOnlyList<CombatFrameStatusSnapshot> Statuses)
{
    internal static CombatFrameActorSnapshot Unknown(int slot) => new(
        slot == CombatFrameRules.SelfSlot
            ? CombatFrameRules.BuildSelfRow(null)
            : CombatFrameRules.CreateUnknownEnemyRows()[slot - CombatFrameRules.FirstEnemySlot],
        string.Empty,
        false,
        -1,
        false,
        Array.Empty<CombatFrameStatusSnapshot>());
}

internal sealed class CombatFramesSnapshot
{
    internal static CombatFramesSnapshot Inactive { get; } = new(
        false,
        -1,
        CombatFrameActorSnapshot.Unknown(CombatFrameRules.SelfSlot),
        CombatFrameRules.CreateUnknownEnemyRows()
            .Select(static row => CombatFrameActorSnapshot.Unknown(row.Slot))
            .ToArray());

    internal CombatFramesSnapshot(
        bool active,
        long publishedAtMilliseconds,
        CombatFrameActorSnapshot self,
        IEnumerable<CombatFrameActorSnapshot> enemies)
    {
        ArgumentNullException.ThrowIfNull(enemies);
        var exactEnemies = enemies.ToArray();
        if (exactEnemies.Length != CombatFrameRules.EnemySlotCount)
            throw new ArgumentException("Combat frames require exactly S1-S5.", nameof(enemies));

        Active = active;
        PublishedAtMilliseconds = publishedAtMilliseconds;
        Self = self;
        Enemies = Array.AsReadOnly(exactEnemies);
    }

    internal bool Active { get; }
    internal long PublishedAtMilliseconds { get; }
    internal CombatFrameActorSnapshot Self { get; }
    internal IReadOnlyList<CombatFrameActorSnapshot> Enemies { get; }
}

/// <summary>
/// Value-only target state supplied by the integration layer. The provider must
/// resolve and discard Dalamud target wrappers inside its own call.
/// </summary>
internal readonly record struct CombatFrameTargetSelection(
    TargetPressureActorIdentity CurrentTarget,
    TargetPressureActorIdentity FocusTarget);
