using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Publishes one immutable, screen-space-only CC combat-frame view. It reads the
/// existing canonical trackers and exact S-slot resolvers without retaining game
/// objects, target wrappers, native pointers, or mutable UI state.
/// </summary>
internal sealed class CombatFramesSnapshotService : IDisposable
{
    private const long UpdateIntervalMilliseconds = 50;
    private const long MaximumPressureAgeMilliseconds = 500;

    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly ExecuteTracker executeTracker;
    private readonly TargetPressureTracker pressureTracker;
    private readonly PvPMetadataValidation metadata;
    private readonly Func<bool> enabledProvider;
    private readonly Func<CombatFrameTargetSelection> targetSelectionProvider;
    private readonly Dictionary<TargetPressureActorIdentity, LowMpState> mpStates = [];
    private PersonalDebuffAlertState[] selfStatusStates = [];
    private CombatFramesSnapshot snapshot = CombatFramesSnapshot.Inactive;
    private TargetPressureActorIdentity activeLocalIdentity;
    private uint activeTerritory;
    private long nextUpdateAtMilliseconds;
    private long nextErrorLogAtMilliseconds;
    private long nextTargetErrorLogAtMilliseconds;
    private bool started;
    private bool disposed;

    internal CombatFramesSnapshotService(
        IObjectTable objectTable,
        IFramework framework,
        IPluginLog log,
        ExecuteTracker executeTracker,
        TargetPressureTracker pressureTracker,
        PvPMetadataValidation metadata,
        Func<bool> enabledProvider,
        Func<CombatFrameTargetSelection> targetSelectionProvider)
    {
        this.objectTable = objectTable;
        this.framework = framework;
        this.log = log;
        this.executeTracker = executeTracker;
        this.pressureTracker = pressureTracker;
        this.metadata = metadata;
        this.enabledProvider = enabledProvider ?? throw new ArgumentNullException(nameof(enabledProvider));
        this.targetSelectionProvider = targetSelectionProvider ??
                                       throw new ArgumentNullException(nameof(targetSelectionProvider));
    }

    internal CombatFramesSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;

        started = true;
        framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        ResetRuntime();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var now = Environment.TickCount64;
        if (disposed || now < nextUpdateAtMilliseconds) return;
        nextUpdateAtMilliseconds = now + UpdateIntervalMilliseconds;

        try
        {
            UpdateSnapshot(now);
        }
        catch (Exception exception)
        {
            ResetRuntime();
            if (now < nextErrorLogAtMilliseconds) return;
            nextErrorLogAtMilliseconds = now + 10_000;
            log.Error(exception, "Seiton Sense combat-frame snapshot failed closed.");
        }
    }

    private void UpdateSnapshot(long nowMilliseconds)
    {
        if (!enabledProvider())
        {
            ResetRuntime();
            return;
        }

        var diagnostics = executeTracker.Diagnostics;
        var localPlayer = objectTable.LocalPlayer;
        var localIdentity = CreateIdentity(localPlayer);
        if (!diagnostics.Active ||
            !diagnostics.IsCrystallineConflict ||
            localPlayer is null ||
            !localIdentity.IsValid)
        {
            ResetRuntime();
            return;
        }

        if (diagnostics.TerritoryId != activeTerritory || localIdentity != activeLocalIdentity)
        {
            mpStates.Clear();
            activeTerritory = diagnostics.TerritoryId;
            activeLocalIdentity = localIdentity;
        }

        var selection = CaptureTargetSelection(nowMilliseconds);
        var pressure = pressureTracker.Snapshot;
        var pressureFresh = pressure.Active &&
                            pressure.LocalPlayer == localIdentity &&
                            CombatFrameRules.IsSnapshotFresh(
                                pressure.PublishedAtMilliseconds,
                                nowMilliseconds,
                                MaximumPressureAgeMilliseconds);
        var pressureUsable = pressureFresh && pressure.PressureActive;
        var hudByIdentity = BuildExactHudLookup(executeTracker.Enemies);
        var observations = new List<CombatFrameObservation>(CombatFrameRules.EnemySlotCount);
        var presentations = new Dictionary<int, EnemyPresentation>();
        var seenMpActors = new HashSet<TargetPressureActorIdentity> { localIdentity };

        for (var slot = CombatFrameRules.FirstEnemySlot; slot <= CombatFrameRules.LastEnemySlot; slot++)
        {
            var player = EnemySlotResolver.Resolve(objectTable, slot);
            var identity = CreateIdentity(player);
            if (player is null ||
                !identity.IsValid ||
                SharesEitherId(identity, localIdentity))
            {
                continue;
            }

            var exactHud = hudByIdentity.GetValueOrDefault(identity);
            if (exactHud?.Slot != slot) exactHud = null;
            var hostile = (player.StatusFlags & StatusFlags.Hostile) != 0;
            if (!hostile && exactHud is null) continue;

            seenMpActors.Add(identity);
            var mpTrusted = ObserveMpTrust(player, identity, nowMilliseconds);
            var exactPressure = pressureFresh
                ? pressure.Find(identity.GameObjectId, identity.EntityId)
                : null;
            if (exactPressure?.EnemySlot != slot) exactPressure = null;
            var statuses = exactPressure is null
                ? Array.Empty<CombatFrameStatusSnapshot>()
                : BuildProtectionStatuses(exactPressure, nowMilliseconds);
            presentations[slot] = new EnemyPresentation(
                SafeName(player),
                exactHud?.GuardUnavailable == true,
                exactHud?.GuardUnavailable == true
                    ? SaturatingAdd(
                        nowMilliseconds,
                        (long)Math.Ceiling(Math.Max(0f, exactHud.GuardCooldownRemainingSeconds) * 1000f))
                    : -1,
                exactHud?.SeitonEligible == true,
                statuses);

            observations.Add(new CombatFrameObservation(
                slot,
                identity,
                player.ClassJob.IsValid ? player.ClassJob.RowId : 0,
                player.CurrentHp,
                player.MaxHp,
                player.CurrentMp,
                player.MaxMp,
                mpTrusted,
                player.IsDead,
                player.IsTargetable,
                pressureUsable && exactPressure is not null,
                selection.CurrentTarget.Equals(identity),
                selection.FocusTarget.Equals(identity),
                0,
                pressureUsable ? exactPressure?.TeamTargetCount ?? 0 : 0,
                pressureUsable && exactPressure is not null
                    ? ToCoreEvidence(exactPressure.IncomingEvidence)
                    : CombatFrameIncomingEvidence.None));
        }

        var enemyRows = CombatFrameRules.BuildEnemyRows(observations);
        var enemies = enemyRows
            .Select(row =>
            {
                if (!presentations.TryGetValue(row.Slot, out var presentation) ||
                    row.Availability == CombatFrameAvailability.Unknown)
                {
                    return CombatFrameActorSnapshot.Unknown(row.Slot);
                }

                return new CombatFrameActorSnapshot(
                    row,
                    presentation.Name,
                    presentation.GuardUnavailable && row.Availability == CombatFrameAvailability.Alive,
                    presentation.GuardReadyAtMilliseconds,
                    presentation.SeitonEligible && row.Availability == CombatFrameAvailability.Alive,
                    presentation.Statuses);
            })
            .ToArray();

        var selfMpTrusted = ObserveMpTrust(localPlayer, localIdentity, nowMilliseconds);
        var directPressureCount = 0;
        DirectSelfPressureSnapshot directPressure = default;
        var directPressureTrusted = pressureUsable &&
                                    pressureTracker.TryGetFreshSelfDirectIncomingPressure(
                localIdentity,
                nowMilliseconds,
                MaximumPressureAgeMilliseconds,
                out directPressure);
        if (directPressureTrusted)
        {
            directPressureCount = directPressure.UniqueEnemyCount;
        }

        var selfRow = CombatFrameRules.BuildSelfRow(new CombatFrameObservation(
            CombatFrameRules.SelfSlot,
            localIdentity,
            localPlayer.ClassJob.IsValid ? localPlayer.ClassJob.RowId : 0,
            localPlayer.CurrentHp,
            localPlayer.MaxHp,
            localPlayer.CurrentMp,
            localPlayer.MaxMp,
            selfMpTrusted,
            localPlayer.IsDead,
            localPlayer.IsTargetable,
            directPressureTrusted,
            selection.CurrentTarget.Equals(localIdentity),
            selection.FocusTarget.Equals(localIdentity),
            directPressureCount,
            0,
            CombatFrameIncomingEvidence.None));
        var self = new CombatFrameActorSnapshot(
            selfRow,
            SafeName(localPlayer),
            false,
            -1,
            false,
            ScanExactSelfStatuses(localPlayer, nowMilliseconds));

        foreach (var stale in mpStates.Keys.Where(actor => !seenMpActors.Contains(actor)).ToArray())
            mpStates.Remove(stale);

        Interlocked.Exchange(
            ref snapshot,
            new CombatFramesSnapshot(true, Environment.TickCount64, self, enemies));
    }

    private CombatFrameTargetSelection CaptureTargetSelection(long nowMilliseconds)
    {
        try
        {
            var selection = targetSelectionProvider();
            return new CombatFrameTargetSelection(
                selection.CurrentTarget.IsValid ? selection.CurrentTarget : default,
                selection.FocusTarget.IsValid ? selection.FocusTarget : default);
        }
        catch (Exception exception)
        {
            if (nowMilliseconds >= nextTargetErrorLogAtMilliseconds)
            {
                nextTargetErrorLogAtMilliseconds = nowMilliseconds + 10_000;
                log.Error(exception, "Seiton Sense combat-frame target accents failed closed.");
            }

            return default;
        }
    }

    private bool ObserveMpTrust(
        IPlayerCharacter player,
        TargetPressureActorIdentity identity,
        long nowMilliseconds)
    {
        mpStates.TryGetValue(identity, out var state);
        var trusted = CombatFrameRules.AdvanceMpTrust(
            state,
            player.CurrentMp,
            player.MaxMp,
            nowMilliseconds,
            out state);
        mpStates[identity] = state;
        return trusted;
    }

    private static Dictionary<TargetPressureActorIdentity, EnemyHudSnapshot?> BuildExactHudLookup(
        IReadOnlyList<EnemyHudSnapshot> enemies)
    {
        var result = new Dictionary<TargetPressureActorIdentity, EnemyHudSnapshot?>();
        foreach (var group in enemies.GroupBy(static enemy =>
                     new TargetPressureActorIdentity(enemy.GameObjectId, enemy.EntityId)))
        {
            result[group.Key] = group.Count() == 1 ? group.Single() : null;
        }

        return result;
    }

    private static IReadOnlyList<CombatFrameStatusSnapshot> BuildProtectionStatuses(
        TargetPressureOpponentSnapshot pressure,
        long nowMilliseconds) =>
        Array.AsReadOnly(
            pressure.Protections
                .Where(protection => protection.ExpiresAtMilliseconds > nowMilliseconds)
                .OrderBy(static protection => protection.Kind)
                .ThenByDescending(static protection => protection.ExpiresAtMilliseconds)
                .ThenBy(static protection => protection.StatusId)
                .Select(static protection => new CombatFrameStatusSnapshot(
                    protection.StatusId,
                    protection.Name,
                    protection.IconId,
                    CombatFrameStatusCategory.Protection,
                    protection.ExpiresAtMilliseconds))
                .ToArray());

    private IReadOnlyList<CombatFrameStatusSnapshot> ScanExactSelfStatuses(
        IPlayerCharacter localPlayer,
        long nowMilliseconds)
    {
        var observations = new List<PersonalDebuffObservation>(8);
        foreach (var status in localPlayer.StatusList)
        {
            var definition = PersonalStatusDefinitions.Find(status.StatusId);
            if (definition is null ||
                !PersonalStatusDefinitions.IsMetadataVerified(definition, metadata) ||
                !float.IsFinite(status.RemainingTime) ||
                status.RemainingTime <= 0f)
            {
                continue;
            }

            var remainingMilliseconds = Math.Max(
                1,
                (long)Math.Round(Math.Min(status.RemainingTime, 3_600f) * 1000f));
            observations.Add(new PersonalDebuffObservation(
                definition.StatusId,
                definition.AlertKind,
                SaturatingAdd(nowMilliseconds, remainingMilliseconds)));
        }

        var decision = PersonalDebuffAlertRules.Observe(
            selfStatusStates,
            observations,
            nowMilliseconds);
        selfStatusStates = decision.NextStates;

        return Array.AsReadOnly(
            decision.Alerts
                .Select(alert =>
                {
                    var definition = PersonalStatusDefinitions.Find(alert.StatusId)!;
                    return new CombatFrameStatusSnapshot(
                        alert.StatusId,
                        definition.Name,
                        definition.IconId,
                        alert.Kind == PersonalDebuffAlertKind.CleanseUrgent
                            ? CombatFrameStatusCategory.CrowdControl
                            : CombatFrameStatusCategory.Danger,
                        SaturatingAdd(nowMilliseconds, alert.RemainingMilliseconds));
                })
                .ToArray());
    }

    private static CombatFrameIncomingEvidence ToCoreEvidence(TargetPressureEvidence evidence)
    {
        var result = CombatFrameIncomingEvidence.None;
        if ((evidence & TargetPressureEvidence.HardTarget) != 0)
            result |= CombatFrameIncomingEvidence.HardTarget;
        if ((evidence & TargetPressureEvidence.CastTarget) != 0)
            result |= CombatFrameIncomingEvidence.CastTarget;
        if ((evidence & TargetPressureEvidence.RecentHarmfulAction) != 0)
            result |= CombatFrameIncomingEvidence.RecentHarmfulAction;
        if ((evidence & TargetPressureEvidence.MachinistLimitBreakMarker) != 0)
            result |= CombatFrameIncomingEvidence.LimitBreakMarker;
        return result;
    }

    private static TargetPressureActorIdentity CreateIdentity(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != nint.Zero &&
        player.IsValid()
            ? new TargetPressureActorIdentity(player.GameObjectId, player.EntityId)
            : default;

    private static bool SharesEitherId(
        TargetPressureActorIdentity left,
        TargetPressureActorIdentity right) =>
        left.GameObjectId == right.GameObjectId || left.EntityId == right.EntityId;

    private static string SafeName(IPlayerCharacter player) =>
        player.Name.TextValue?.Trim() ?? string.Empty;

    private void ResetRuntime()
    {
        mpStates.Clear();
        selfStatusStates = [];
        activeLocalIdentity = default;
        activeTerritory = 0;
        Interlocked.Exchange(ref snapshot, CombatFramesSnapshot.Inactive);
    }

    private static long SaturatingAdd(long left, long right) =>
        right > 0 && left > long.MaxValue - right ? long.MaxValue : left + right;

    private sealed record EnemyPresentation(
        string Name,
        bool GuardUnavailable,
        long GuardReadyAtMilliseconds,
        bool SeitonEligible,
        IReadOnlyList<CombatFrameStatusSnapshot> Statuses);
}
