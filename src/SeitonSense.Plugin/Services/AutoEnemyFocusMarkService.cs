using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal enum AutoEnemyFocusMarkPhase
{
    Inactive,
    Suppressed,
    NoCandidate,
    MarkerOccupied,
    Ready,
    PendingMarkConfirmation,
    Owned,
    PendingClearConfirmation,
}

internal sealed record AutoEnemyFocusMarkDiagnostics(
    bool Configured,
    bool ActiveInCurrentContext,
    bool TextInputActive,
    AutoEnemyFocusMarkPhase Phase,
    int CandidateCount,
    int DesiredEnemySlot,
    ulong DesiredGameObjectId,
    ulong ObservedMarkerGameObjectId,
    long ObservedMarkerTime,
    bool OwnsMarker,
    int OwnedEnemySlot,
    long MarkCommands,
    long ClearCommands,
    long OwnershipConfirmations,
    long OwnershipRelinquishments,
    string LastEvent)
{
    internal static AutoEnemyFocusMarkDiagnostics Inactive { get; } = new(
        false,
        false,
        false,
        AutoEnemyFocusMarkPhase.Inactive,
        0,
        0,
        0,
        0,
        0,
        false,
        0,
        0,
        0,
        0,
        0,
        "Not observed");

    internal string ToChatLine() =>
        $"configured={Configured},active={ActiveInCurrentContext},text={TextInputActive}," +
        $"phase={Phase},candidates={CandidateCount},desired=e{DesiredEnemySlot}/{DesiredGameObjectId:X}," +
        $"marker={ObservedMarkerGameObjectId:X}@{ObservedMarkerTime},owned={OwnsMarker}/e{OwnedEnemySlot}," +
        $"commands={MarkCommands}/{ClearCommands},confirm={OwnershipConfirmations}," +
        $"relinquish={OwnershipRelinquishments},last={LastEvent}";
}

internal sealed class AutoEnemyFocusMarkService : IDisposable
{
    private const long UpdateIntervalMilliseconds = 100;
    private const long MinimumCommandIntervalMilliseconds = 1_000;
    private const long ConfirmationTimeoutMilliseconds = 1_500;

    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IDutyState dutyState;
    private readonly IPluginLog log;
    private readonly PvPMetadataValidation metadata;
    private readonly ExecuteTracker executeTracker;
    private readonly TargetPressureTracker pressureTracker;
    private readonly ReviewedPvpCommandDispatcher commands;

    private AutoEnemyFocusMarkDiagnostics diagnostics = AutoEnemyFocusMarkDiagnostics.Inactive;
    private MarkerOwnership? ownership;
    private PendingMarkerCommand? pending;
    private MarkerCandidateIdentity lastDesired;
    private MarkerCandidateIdentity blockedMarkCandidate;
    private ulong activeLocalGameObjectId;
    private uint activeLocalEntityId;
    private uint activeTerritory;
    private long nextUpdateAt;
    private long nextErrorLogAt;
    private long lastCommandAt = -MinimumCommandIntervalMilliseconds;
    private long markCommands;
    private long clearCommands;
    private long ownershipConfirmations;
    private long ownershipRelinquishments;
    private string lastEvent = "Not observed";
    private bool started;
    private bool disposed;

    internal AutoEnemyFocusMarkService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IDutyState dutyState,
        IPluginLog log,
        PvPMetadataValidation metadata,
        ExecuteTracker executeTracker,
        TargetPressureTracker pressureTracker,
        ReviewedPvpCommandDispatcher commands)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.dutyState = dutyState;
        this.log = log;
        this.metadata = metadata;
        this.executeTracker = executeTracker;
        this.pressureTracker = pressureTracker;
        this.commands = commands;
    }

    internal AutoEnemyFocusMarkDiagnostics Diagnostics => Volatile.Read(ref diagnostics);

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
        TryClearOwnedOnDispose();
        Relinquish("Disposed");
        Publish(false, false, false, AutoEnemyFocusMarkPhase.Inactive, 0, default, 0, 0);
    }

    private unsafe void TryClearOwnedOnDispose()
    {
        // No confirmation loop is possible during unload, so this is deliberately
        // a single best-effort transition. Any uncertainty leaves the marker alone.
        try
        {
            if (ownership is not { } owned || pending is not null) return;

            var now = Environment.TickCount64;
            if (!CanIssueCommand(now) || !HasExactNativeIdentity(objectTable.LocalPlayer)) return;

            var condition = dutyState.ContentFinderCondition;
            var context = PvPMatchRules.ResolveSupportedContext(
                clientState.IsPvP,
                clientState.IsPvPExcludingDen,
                configuration.EnableWolvesDenTesting,
                clientState.TerritoryType,
                condition.IsValid,
                condition.IsValid && condition.Value.PvP,
                condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
                condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
                condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
            if (context != SupportedPvPContext.CrystallineConflict ||
                !metadata.GuardVerified ||
                !TryGetTextInputState(out var textInputActive) ||
                textInputActive)
            {
                return;
            }

            var marking = MarkingController.Instance();
            if (marking == null || marking->Markers.Length == 0 || marking->MarkerTimes.Length == 0)
                return;

            var observedMarker = (ulong)marking->Markers[0];
            var observedMarkerTime = marking->MarkerTimes[0];
            if (!TryResolveExactSlotIdentity(owned.EnemySlot, out var exact) ||
                !AutoEnemyFocusMarkRules.CanClearOwnedMarker(
                    owned.EnemySlot,
                    owned.GameObjectId,
                    owned.EntityId,
                    owned.MarkerTime,
                    exact.EnemySlot,
                    exact.GameObjectId,
                    exact.EntityId,
                    observedMarker,
                    observedMarkerTime))
            {
                return;
            }

            if (commands.TryClearAttack1(owned.EnemySlot, now) !=
                ReviewedPvpCommandDispatchResult.Invoked)
            {
                return;
            }
            lastCommandAt = now;
            clearCommands++;
            lastEvent = $"Owned Attack-1 clear issued once during dispose for e{owned.EnemySlot}";
        }
        catch
        {
            // Plugin unload must never be blocked by an optional marker cleanup.
        }
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var now = Environment.TickCount64;
        if (disposed || now < nextUpdateAt) return;
        nextUpdateAt = now + UpdateIntervalMilliseconds;

        try
        {
            Update(now);
        }
        catch (Exception exception)
        {
            Relinquish("Exception; ownership relinquished");
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                false,
                true,
                AutoEnemyFocusMarkPhase.Suppressed,
                0,
                default,
                0,
                0);
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
            log.Error(exception, "Seiton Sense auto enemy focus mark failed closed.");
        }
    }

    private unsafe void Update(long now)
    {
        if (now < lastCommandAt)
        {
            Relinquish("Monotonic clock reset");
            lastCommandAt = now - MinimumCommandIntervalMilliseconds;
        }

        var local = objectTable.LocalPlayer;
        var trackerDiagnostics = executeTracker.Diagnostics;
        var localIdentityValid = HasExactNativeIdentity(local);
        var sessionChanged = clientState.TerritoryType != activeTerritory ||
                             local?.GameObjectId != activeLocalGameObjectId ||
                             local?.EntityId != activeLocalEntityId;
        if (sessionChanged)
        {
            Relinquish("Context or local identity changed");
            lastDesired = default;
            blockedMarkCandidate = default;
            activeTerritory = clientState.TerritoryType;
            activeLocalGameObjectId = local?.GameObjectId ?? 0;
            activeLocalEntityId = local?.EntityId ?? 0;
        }

        var condition = dutyState.ContentFinderCondition;
        var context = PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            configuration.EnableWolvesDenTesting,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
        var structuralExactContext = localIdentityValid &&
                                     context == SupportedPvPContext.CrystallineConflict &&
                                     metadata.GuardVerified;
        if (!structuralExactContext)
        {
            Relinquish("Exact CC context or metadata unavailable");
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                false,
                false,
                AutoEnemyFocusMarkPhase.Suppressed,
                0,
                default,
                0,
                0);
            return;
        }

        var marking = MarkingController.Instance();
        if (marking == null || marking->Markers.Length == 0 || marking->MarkerTimes.Length == 0)
        {
            Relinquish("Attack-1 marker telemetry unavailable");
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                false,
                false,
                AutoEnemyFocusMarkPhase.Suppressed,
                0,
                default,
                0,
                0);
            return;
        }

        var observedMarker = (ulong)marking->Markers[0];
        var observedMarkerTime = marking->MarkerTimes[0];
        var textProbeSucceeded = TryGetTextInputState(out var textInputActive);
        if (!textProbeSucceeded)
        {
            HandleSelectionUncertainty(
                "Text-input state unavailable",
                true,
                observedMarker,
                observedMarkerTime);
            return;
        }

        if (HandlePendingCommand(now, observedMarker, observedMarkerTime, textInputActive))
            return;

        if (ownership is { } currentOwnership &&
            (observedMarker != currentOwnership.GameObjectId ||
             observedMarkerTime != currentOwnership.MarkerTime))
        {
            Relinquish("Attack-1 changed outside this helper");
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                true,
                textInputActive,
                AutoEnemyFocusMarkPhase.Suppressed,
                0,
                default,
                observedMarker,
                observedMarkerTime);
            return;
        }

        // Turning either the module or the plugin off must not strand a marker
        // that this exact service instance demonstrably owns. This path needs no
        // pressure snapshot, but it retains every slot/timestamp/text/rate gate.
        if (AutoEnemyFocusMarkRules.ShouldClearConfirmedOwnership(
                configuration.Enabled,
                configuration.EnableAutoEnemyFocusMark,
                ownership is not null))
        {
            HandleOwnedClear(
                now,
                observedMarker,
                observedMarkerTime,
                textInputActive,
                0,
                default);
            return;
        }

        if (!configuration.Enabled || !configuration.EnableAutoEnemyFocusMark)
        {
            lastEvent = configuration.Enabled ? "Feature disabled" : "Plugin disabled";
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                true,
                textInputActive,
                AutoEnemyFocusMarkPhase.Inactive,
                0,
                default,
                observedMarker,
                observedMarkerTime);
            return;
        }

        var selectionContextExact = local is { IsDead: false, CurrentHp: > 0 } &&
                                    trackerDiagnostics.Active &&
                                    trackerDiagnostics.IsPvP &&
                                    trackerDiagnostics.IsCrystallineConflict &&
                                    !trackerDiagnostics.IsWolvesDen &&
                                    trackerDiagnostics.TerritoryId == clientState.TerritoryType &&
                                    trackerDiagnostics.ResolvedSlots == 5 &&
                                    trackerDiagnostics.SlotCapacity == 5 &&
                                    trackerDiagnostics.GuardMetadataVerified;
        if (!selectionContextExact)
        {
            HandleSelectionUncertainty(
                "Canonical CC selection snapshot unavailable",
                textInputActive,
                observedMarker,
                observedMarkerTime);
            return;
        }

        var pressure = pressureTracker.Snapshot;
        var exactPressure = pressure.Active && pressure.PressureActive
            ? pressure
            : null;
        var buildSucceeded = TryBuildCandidates(exactPressure, out var candidates);
        if (!buildSucceeded)
        {
            HandleSelectionUncertainty(
                "Canonical enemy or pressure identity uncertain",
                textInputActive,
                observedMarker,
                observedMarkerTime);
            return;
        }

        var desired = configuration.EnableAutoEnemyFocusMark
            ? AutoEnemyFocusMarkRules.Select(candidates)
            : null;
        var desiredIdentity = desired is { } selected
            ? new MarkerCandidateIdentity(selected.EnemySlot, selected.GameObjectId, selected.EntityId)
            : default;
        if (desiredIdentity != lastDesired)
        {
            lastDesired = desiredIdentity;
            blockedMarkCandidate = default;
        }

        if (ownership is { } owned)
        {
            var keepOwned = configuration.EnableAutoEnemyFocusMark && desiredIdentity == owned.Identity;
            if (keepOwned)
            {
                lastEvent = "Owned Attack-1 remains the selected candidate";
                Publish(true, true, textInputActive, AutoEnemyFocusMarkPhase.Owned, candidates.Count, desiredIdentity, observedMarker, observedMarkerTime);
                return;
            }

            HandleOwnedClear(
                now,
                observedMarker,
                observedMarkerTime,
                textInputActive,
                candidates.Count,
                desiredIdentity);
            return;
        }

        if (desired is null)
        {
            lastEvent = "No Guard-down low-resource candidate";
            Publish(true, true, textInputActive, AutoEnemyFocusMarkPhase.NoCandidate, 0, default, observedMarker, observedMarkerTime);
            return;
        }

        if (observedMarker != 0)
        {
            lastEvent = "Attack-1 is occupied; no overwrite";
            Publish(true, true, textInputActive, AutoEnemyFocusMarkPhase.MarkerOccupied, candidates.Count, desiredIdentity, observedMarker, observedMarkerTime);
            return;
        }

        if (blockedMarkCandidate == desiredIdentity)
        {
            lastEvent = "This candidate transition was already attempted";
            Publish(true, true, textInputActive, AutoEnemyFocusMarkPhase.Suppressed, candidates.Count, desiredIdentity, observedMarker, observedMarkerTime);
            return;
        }

        if (textInputActive || !CanIssueCommand(now))
        {
            lastEvent = textInputActive ? "Mark suppressed while text input is active" : "Mark waiting for command rate limit";
            Publish(true, true, textInputActive, AutoEnemyFocusMarkPhase.Ready, candidates.Count, desiredIdentity, observedMarker, observedMarkerTime);
            return;
        }

        if (!TryResolveExactSlotIdentity(desired.Value.EnemySlot, out var revalidated) ||
            revalidated != desiredIdentity)
        {
            lastEvent = "Selected e-slot identity changed before command";
            Publish(true, true, textInputActive, AutoEnemyFocusMarkPhase.Suppressed, candidates.Count, desiredIdentity, observedMarker, observedMarkerTime);
            return;
        }

        var markResult = commands.TryMarkAttack1(desired.Value.EnemySlot, now);
        if (markResult == ReviewedPvpCommandDispatchResult.MarkerRateLimited)
        {
            lastEvent = "Mark waiting for the shared marker command reservation";
            Publish(true, true, textInputActive, AutoEnemyFocusMarkPhase.Ready, candidates.Count, desiredIdentity, observedMarker, observedMarkerTime);
            return;
        }

        blockedMarkCandidate = desiredIdentity;
        if (markResult != ReviewedPvpCommandDispatchResult.Invoked)
        {
            lastEvent = "Mark command could not be issued";
            Publish(true, true, textInputActive, AutoEnemyFocusMarkPhase.Suppressed, candidates.Count, desiredIdentity, observedMarker, observedMarkerTime);
            return;
        }

        lastCommandAt = now;
        markCommands++;
        pending = new PendingMarkerCommand(
            MarkerCommandKind.Mark,
            desiredIdentity,
            observedMarkerTime,
            SaturatingAdd(now, ConfirmationTimeoutMilliseconds));
        lastEvent = $"Attack-1 command issued for e{desired.Value.EnemySlot}";
        Publish(true, true, textInputActive, AutoEnemyFocusMarkPhase.PendingMarkConfirmation, candidates.Count, desiredIdentity, observedMarker, observedMarkerTime);
    }

    private void HandleOwnedClear(
        long now,
        ulong observedMarker,
        long observedMarkerTime,
        bool textInputActive,
        int candidateCount,
        MarkerCandidateIdentity desiredIdentity)
    {
        if (ownership is not { } owned) return;

        if (textInputActive || !CanIssueCommand(now))
        {
            lastEvent = textInputActive
                ? "Clear suppressed while text input is active"
                : "Clear waiting for command rate limit";
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                true,
                textInputActive,
                AutoEnemyFocusMarkPhase.Owned,
                candidateCount,
                desiredIdentity,
                observedMarker,
                observedMarkerTime);
            return;
        }

        if (!TryResolveExactSlotIdentity(owned.EnemySlot, out var currentSlotIdentity) ||
            !AutoEnemyFocusMarkRules.CanClearOwnedMarker(
                owned.EnemySlot,
                owned.GameObjectId,
                owned.EntityId,
                owned.MarkerTime,
                currentSlotIdentity.EnemySlot,
                currentSlotIdentity.GameObjectId,
                currentSlotIdentity.EntityId,
                observedMarker,
                observedMarkerTime))
        {
            Relinquish("Owned target slot or marker timestamp drifted; clear suppressed");
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                true,
                textInputActive,
                AutoEnemyFocusMarkPhase.Suppressed,
                candidateCount,
                desiredIdentity,
                observedMarker,
                observedMarkerTime);
            return;
        }

        var clearResult = commands.TryClearAttack1(owned.EnemySlot, now);
        if (clearResult == ReviewedPvpCommandDispatchResult.MarkerRateLimited)
        {
            lastEvent = "Clear waiting for the shared marker command reservation";
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                true,
                textInputActive,
                AutoEnemyFocusMarkPhase.Owned,
                candidateCount,
                desiredIdentity,
                observedMarker,
                observedMarkerTime);
            return;
        }

        if (clearResult != ReviewedPvpCommandDispatchResult.Invoked)
        {
            Relinquish("Clear command could not be issued");
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                true,
                textInputActive,
                AutoEnemyFocusMarkPhase.Suppressed,
                candidateCount,
                desiredIdentity,
                observedMarker,
                observedMarkerTime);
            return;
        }

        lastCommandAt = now;
        clearCommands++;
        pending = new PendingMarkerCommand(
            MarkerCommandKind.Clear,
            owned.Identity,
            owned.MarkerTime,
            SaturatingAdd(now, ConfirmationTimeoutMilliseconds));
        lastEvent = $"Clear command issued for owned Attack-1 e{owned.EnemySlot}";
        Publish(
            configuration.EnableAutoEnemyFocusMark,
            true,
            textInputActive,
            AutoEnemyFocusMarkPhase.PendingClearConfirmation,
            candidateCount,
            desiredIdentity,
            observedMarker,
            observedMarkerTime);
    }

    private void HandleSelectionUncertainty(
        string reason,
        bool textInputActive,
        ulong observedMarker,
        long observedMarkerTime)
    {
        if (ownership is { } owned &&
            TryResolveExactSlotIdentity(owned.EnemySlot, out var exact) &&
            exact == owned.Identity)
        {
            lastEvent = $"{reason}; owned Attack-1 retained without a command";
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                false,
                textInputActive,
                AutoEnemyFocusMarkPhase.Owned,
                0,
                default,
                observedMarker,
                observedMarkerTime);
            return;
        }

        Relinquish(reason);
        Publish(
            configuration.EnableAutoEnemyFocusMark,
            false,
            textInputActive,
            AutoEnemyFocusMarkPhase.Suppressed,
            0,
            default,
            observedMarker,
            observedMarkerTime);
    }

    private bool HandlePendingCommand(
        long now,
        ulong observedMarker,
        long observedMarkerTime,
        bool textInputActive)
    {
        if (pending is not { } current) return false;

        if (current.Kind == MarkerCommandKind.Mark)
        {
            if (AutoEnemyFocusMarkRules.CanConfirmOwnership(
                    current.Identity.GameObjectId,
                    current.MarkerTimeBeforeCommand,
                    observedMarker,
                    observedMarkerTime) &&
                TryResolveExactSlotIdentity(current.Identity.EnemySlot, out var exact) &&
                exact == current.Identity)
            {
                ownership = new MarkerOwnership(
                    current.Identity.EnemySlot,
                    current.Identity.GameObjectId,
                    current.Identity.EntityId,
                    observedMarkerTime);
                pending = null;
                ownershipConfirmations++;
                lastEvent = $"Attack-1 ownership confirmed on e{current.Identity.EnemySlot}";
                Publish(
                    configuration.EnableAutoEnemyFocusMark,
                    true,
                    textInputActive,
                    AutoEnemyFocusMarkPhase.Owned,
                    0,
                    current.Identity,
                    observedMarker,
                    observedMarkerTime);
                return true;
            }

            if (observedMarker != 0 || now >= current.ExpiresAtMilliseconds)
            {
                pending = null;
                lastEvent = observedMarker != 0
                    ? "Mark changed without exact ownership confirmation"
                    : "Mark ownership confirmation timed out";
                ownershipRelinquishments++;
                Publish(
                    configuration.EnableAutoEnemyFocusMark,
                    true,
                    textInputActive,
                    AutoEnemyFocusMarkPhase.Suppressed,
                    0,
                    current.Identity,
                    observedMarker,
                    observedMarkerTime);
                return true;
            }

            Publish(
                configuration.EnableAutoEnemyFocusMark,
                true,
                textInputActive,
                AutoEnemyFocusMarkPhase.PendingMarkConfirmation,
                0,
                current.Identity,
                observedMarker,
                observedMarkerTime);
            return true;
        }

        if (observedMarker == 0)
        {
            pending = null;
            ownership = null;
            lastEvent = $"Owned Attack-1 clear confirmed for e{current.Identity.EnemySlot}";
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                true,
                textInputActive,
                AutoEnemyFocusMarkPhase.NoCandidate,
                0,
                lastDesired,
                observedMarker,
                observedMarkerTime);
            return true;
        }

        if (observedMarker != current.Identity.GameObjectId ||
            observedMarkerTime != current.MarkerTimeBeforeCommand ||
            now >= current.ExpiresAtMilliseconds)
        {
            Relinquish("Clear confirmation drifted or timed out");
            Publish(
                configuration.EnableAutoEnemyFocusMark,
                true,
                textInputActive,
                AutoEnemyFocusMarkPhase.Suppressed,
                0,
                lastDesired,
                observedMarker,
                observedMarkerTime);
            return true;
        }

        Publish(
            configuration.EnableAutoEnemyFocusMark,
            true,
            textInputActive,
            AutoEnemyFocusMarkPhase.PendingClearConfirmation,
            0,
            lastDesired,
            observedMarker,
            observedMarkerTime);
        return true;
    }

    private bool TryBuildCandidates(
        TargetPressureRuntimeSnapshot? pressure,
        out List<AutoEnemyFocusMarkCandidate> candidates)
    {
        candidates = [];
        foreach (var enemy in executeTracker.Enemies)
        {
            if (!TryResolveExactSlotIdentity(enemy.Slot, out var exact) ||
                exact.GameObjectId != enemy.GameObjectId ||
                exact.EntityId != enemy.EntityId)
            {
                candidates.Clear();
                return false;
            }

            var player = EnemySlotResolver.Resolve(objectTable, enemy.Slot);
            if (player is null) return false;
            var pressureEnemy = pressure?.Find(enemy.GameObjectId, enemy.EntityId);

            var guardActive = player.StatusList.Any(status =>
                status.StatusId is EnemyCombatConstants.GuardStatusId or EnemyCombatConstants.GuardStatusAlternateId &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f);
            candidates.Add(new AutoEnemyFocusMarkCandidate(
                enemy.Slot,
                enemy.GameObjectId,
                enemy.EntityId,
                true,
                !player.IsDead && player.CurrentHp > 0,
                player.IsTargetable,
                enemy.GuardUnavailable && !guardActive,
                player.CurrentHp,
                player.MaxHp,
                enemy.LowMp,
                player.CurrentMp,
                player.MaxMp,
                pressureEnemy?.TeamTargetCount ?? 0));
        }

        return true;
    }

    private bool TryResolveExactSlotIdentity(int enemySlot, out MarkerCandidateIdentity identity)
    {
        var player = EnemySlotResolver.Resolve(objectTable, enemySlot);
        if (!HasExactNativeIdentity(player))
        {
            identity = default;
            return false;
        }

        identity = new MarkerCandidateIdentity(enemySlot, player!.GameObjectId, player.EntityId);
        return identity.IsValid;
    }

    private static unsafe bool HasExactNativeIdentity(IPlayerCharacter? player)
    {
        if (player is null || player.Address == nint.Zero ||
            player.GameObjectId is 0 or 0xE0000000UL ||
            player.EntityId is 0 or 0xE0000000u)
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId;
    }

    private static unsafe bool TryGetTextInputState(out bool active)
    {
        try
        {
            var atkModule = RaptureAtkModule.Instance();
            if (atkModule == null)
            {
                active = true;
                return false;
            }

            active = atkModule->IsTextInputActive() || ImGui.GetIO().WantTextInput;
            return true;
        }
        catch
        {
            active = true;
            return false;
        }
    }

    private bool CanIssueCommand(long now) =>
        now >= lastCommandAt && now - lastCommandAt >= MinimumCommandIntervalMilliseconds;

    private void Relinquish(string reason)
    {
        if (ownership is not null || pending is not null)
            ownershipRelinquishments++;
        ownership = null;
        pending = null;
        lastEvent = reason;
    }

    private void Publish(
        bool configured,
        bool active,
        bool textInputActive,
        AutoEnemyFocusMarkPhase phase,
        int candidateCount,
        MarkerCandidateIdentity desired,
        ulong observedMarker,
        long observedMarkerTime)
    {
        Volatile.Write(ref diagnostics, new AutoEnemyFocusMarkDiagnostics(
            configured,
            active,
            textInputActive,
            phase,
            candidateCount,
            desired.EnemySlot,
            desired.GameObjectId,
            observedMarker,
            observedMarkerTime,
            ownership is not null,
            ownership?.EnemySlot ?? 0,
            markCommands,
            clearCommands,
            ownershipConfirmations,
            ownershipRelinquishments,
            lastEvent));
    }

    private static long SaturatingAdd(long value, long addition) =>
        addition > 0 && value > long.MaxValue - addition ? long.MaxValue : value + addition;

    private enum MarkerCommandKind
    {
        Mark,
        Clear,
    }

    private readonly record struct MarkerCandidateIdentity(int EnemySlot, ulong GameObjectId, uint EntityId)
    {
        internal bool IsValid =>
            EnemySlot is >= 1 and <= 5 &&
            GameObjectId is not 0 and not 0xE0000000UL &&
            EntityId is not 0 and not 0xE0000000u;
    }

    private sealed record MarkerOwnership(
        int EnemySlot,
        ulong GameObjectId,
        uint EntityId,
        long MarkerTime)
    {
        internal MarkerCandidateIdentity Identity => new(EnemySlot, GameObjectId, EntityId);
    }

    private sealed record PendingMarkerCommand(
        MarkerCommandKind Kind,
        MarkerCandidateIdentity Identity,
        long MarkerTimeBeforeCommand,
        long ExpiresAtMilliseconds);
}
