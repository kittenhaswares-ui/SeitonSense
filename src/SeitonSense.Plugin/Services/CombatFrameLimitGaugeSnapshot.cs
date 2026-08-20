using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum CombatFrameLimitGaugeNativeMapping : byte
{
    Unknown = 0,
    Node3TrackNode4Fill = 1,
    Node4TrackNode3Fill = 2,
}

internal sealed record CombatFrameLimitGaugeRuntimeDiagnostics(
    bool ExactCrystallineConflictContext,
    bool SelfControllerValid,
    bool AllyAddonValid,
    bool EnemyAddonValid,
    int LocalPartySlot,
    CombatFrameLimitGaugeNativeMapping Mapping,
    int KnownEnemyCount,
    CombatFrameLimitGaugeInvalidationReason LastRuntimeInvalidation,
    CombatFrameLimitGaugeCalibrationDiagnostics Node3TrackCalibration,
    CombatFrameLimitGaugeCalibrationDiagnostics Node4TrackCalibration)
{
    internal static CombatFrameLimitGaugeRuntimeDiagnostics Inactive { get; } =
        new(
            false,
            false,
            false,
            false,
            0,
            CombatFrameLimitGaugeNativeMapping.Unknown,
            0,
            CombatFrameLimitGaugeInvalidationReason.ContextLost,
            default,
            default);

    internal string ToTraceLine() =>
        $"context={ExactCrystallineConflictContext},self={SelfControllerValid}," +
        $"allyAddon={AllyAddonValid},enemyAddon={EnemyAddonValid},localSlot={LocalPartySlot}," +
        $"mapping={Mapping},knownEnemies={KnownEnemyCount},runtimeReset={LastRuntimeInvalidation}," +
        $"3to4[bound={Node3TrackCalibration.Bound},cal={Node3TrackCalibration.Calibrated}," +
        $"zero={Node3TrackCalibration.SeenZero},full={Node3TrackCalibration.SeenFull}," +
        $"partial={Node3TrackCalibration.DistinctNonTerminalSamples}," +
        $"reset={Node3TrackCalibration.LastInvalidationReason}]," +
        $"4to3[bound={Node4TrackCalibration.Bound},cal={Node4TrackCalibration.Calibrated}," +
        $"zero={Node4TrackCalibration.SeenZero},full={Node4TrackCalibration.SeenFull}," +
        $"partial={Node4TrackCalibration.DistinctNonTerminalSamples}," +
        $"reset={Node4TrackCalibration.LastInvalidationReason}]";
}

internal sealed class CombatFrameLimitGaugeSnapshot
{
    private readonly CombatFrameLimitGaugeReading[] enemies;

    internal CombatFrameLimitGaugeSnapshot(
        bool active,
        long publishedAtMilliseconds,
        CombatFrameLimitGaugeReading self,
        IReadOnlyList<CombatFrameLimitGaugeReading> enemies,
        CombatFrameLimitGaugeRuntimeDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(enemies);
        if (enemies.Count != CombatFrameLimitGaugeRules.LastEnemySlot)
            throw new ArgumentException("Combat-frame LB telemetry requires exactly S1-S5.", nameof(enemies));

        Active = active;
        PublishedAtMilliseconds = publishedAtMilliseconds;
        Self = self;
        this.enemies = enemies.ToArray();
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    internal static CombatFrameLimitGaugeSnapshot Inactive { get; } =
        new(
            false,
            0,
            CombatFrameLimitGaugeReading.Unknown(CombatFrameLimitGaugeRules.SelfSlot),
            CombatFrameLimitGaugeRules.UnknownEnemies(),
            CombatFrameLimitGaugeRuntimeDiagnostics.Inactive);

    internal bool Active { get; }
    internal long PublishedAtMilliseconds { get; }
    internal CombatFrameLimitGaugeReading Self { get; }
    internal IReadOnlyList<CombatFrameLimitGaugeReading> Enemies => Array.AsReadOnly(enemies);
    internal CombatFrameLimitGaugeRuntimeDiagnostics Diagnostics { get; }

    internal CombatFrameLimitGaugeReading FindEnemy(int slot) =>
        slot is >= CombatFrameLimitGaugeRules.FirstEnemySlot and <= CombatFrameLimitGaugeRules.LastEnemySlot
            ? enemies[slot - CombatFrameLimitGaugeRules.FirstEnemySlot]
            : CombatFrameLimitGaugeReading.Unknown(slot);
}
