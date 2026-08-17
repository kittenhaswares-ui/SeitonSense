using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using DalamudBattleChara = Dalamud.Game.ClientState.Objects.Types.IBattleChara;
using DalamudBattleNpc = Dalamud.Game.ClientState.Objects.Types.IBattleNpc;
using DalamudGameObject = Dalamud.Game.ClientState.Objects.Types.IGameObject;
using DalamudObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using DalamudBattleNpcSubKind = Dalamud.Game.ClientState.Objects.Enums.BattleNpcSubKind;
using GameAction = Lumina.Excel.Sheets.Action;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace SeitonSense.Plugin.Services;

internal enum DarkKnightShadowbringerArmOutcome
{
    Armed,
    Disabled,
    HookUnavailable,
    MetadataMismatch,
    WolvesDenDummyMetadataMismatch,
    NotMacroInvocation,
    NotSupportedPvPContext,
    LocalDarkKnightInvalid,
    CycleUnknown,
    CycleSpent,
    FailedClosed,
}

internal readonly record struct DarkKnightShadowbringerArmResult(
    DarkKnightShadowbringerArmOutcome Outcome)
{
    internal bool Success => Outcome == DarkKnightShadowbringerArmOutcome.Armed;
}

internal readonly record struct DarkKnightShadowbringerMacroDiagnostics(
    bool Started,
    bool Enabled,
    bool MetadataVerified,
    bool WolvesDenDummyMetadataVerified,
    bool Armed,
    long ArmRemainingMilliseconds,
    int MacroLine,
    ulong CycleToken,
    ulong SpentCycleToken,
    int RecastGroupIndex,
    uint RecastActionId,
    float RecastElapsedSeconds,
    float RecastTotalSeconds,
    float RecastRemainingSeconds,
    ushort LastUsedActionSequence,
    uint LastRawCarrierActionId,
    uint LastAdjustedCarrierActionId,
    uint LastMode,
    uint LastComboRouteId,
    SupportedPvPContext LastContext,
    int LastEnemySlot,
    ulong LastTargetGameObjectId,
    uint LastTargetEntityId,
    uint LastShadowbringerAdjustedActionId,
    uint LastHp,
    bool LastDarkArts,
    bool LastOwnGuardBlocked,
    bool LastTargetGuardBlocked,
    bool LastQueueOwned,
    float LastAnimationLockSeconds,
    long ArmedCount,
    long PairedCount,
    long ClaimedCount,
    long AttemptCount,
    long AcceptedCount,
    string LastEvent)
{
    internal string ToChatLine()
    {
        var targetLabel = LastTargetEntityId == 0
            ? "none"
            : LastContext switch
            {
                SupportedPvPContext.CrystallineConflict when LastEnemySlot > 0 => $"CC/S{LastEnemySlot}",
                SupportedPvPContext.WolvesDen => "Den/dummy",
                _ => "unknown",
            };
        return $"active={Started},enabled={Enabled},meta={MetadataVerified}," +
               $"denDummyMeta={WolvesDenDummyMetadataVerified},armed={Armed}," +
               $"ttl={ArmRemainingMilliseconds},line={MacroLine},cycle={CycleToken}/{SpentCycleToken}," +
               $"gcd={RecastGroupIndex}/{RecastActionId}/{RecastElapsedSeconds:0.000}/" +
               $"{RecastTotalSeconds:0.000}/{RecastRemainingSeconds:0.000},seq={LastUsedActionSequence}," +
               $"carrier={LastRawCarrierActionId}/{LastAdjustedCarrierActionId}/m{LastMode}/r{LastComboRouteId}," +
               $"target={targetLabel}/{LastTargetGameObjectId:X}/{LastTargetEntityId:X}," +
               $"shadow={LastShadowbringerAdjustedActionId},hp/da={LastHp}/{LastDarkArts}," +
               $"guard={LastOwnGuardBlocked}/{LastTargetGuardBlocked},queue/lock=" +
               $"{LastQueueOwned}/{LastAnimationLockSeconds:0.000},count=" +
               $"{ArmedCount}/{PairedCount}/{ClaimedCount}/{AttemptCount}/{AcceptedCount},last={LastEvent}";
    }
}

internal readonly record struct DarkKnightShadowbringerPairedCarrier(
    DarkKnightShadowbringerMacroArm Arm,
    SupportedPvPContext Context,
    uint RawActionId,
    uint AdjustedActionId,
    uint UseActionMode,
    uint ComboRouteId,
    ulong EffectiveTargetId,
    bool UsedDefaultTargetCarrier,
    ulong NativeHardTargetId,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    nint TargetAddress,
    DalamudObjectKind TargetObjectKind,
    byte TargetSubKind,
    uint TargetNameId);

internal sealed unsafe class DarkKnightShadowbringerMacroService : IDisposable
{
    internal const string Command = "/seitonbringer";

    private const ulong InvalidObjectId = 0xE0000000;
    private const float AnimationLockEpsilonSeconds = 0.0005f;

    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDutyState dutyState;
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly bool metadataVerified;
    private readonly bool wolvesDenDummyMetadataVerified;
    private readonly object stateGate = new();

    private DarkKnightGcdCycleState cycleState = DarkKnightGcdCycleState.Initial;
    private DarkKnightShadowbringerMacroArm? armedMacro;
    private bool hasObservedLifetime;
    private uint observedTerritoryId;
    private ulong observedLocalGameObjectId;
    private uint observedLocalEntityId;
    private nint observedLocalAddress;
    private int lastRecastGroupIndex = -1;
    private uint lastRecastActionId;
    private float lastRecastElapsedSeconds;
    private float lastRecastTotalSeconds;
    private float lastRecastRemainingSeconds;
    private ushort lastUsedActionSequence;
    private uint lastRawCarrierActionId;
    private uint lastAdjustedCarrierActionId;
    private uint lastMode;
    private uint lastComboRouteId;
    private SupportedPvPContext lastContext;
    private int lastEnemySlot;
    private ulong lastTargetGameObjectId;
    private uint lastTargetEntityId;
    private uint lastShadowbringerAdjustedActionId;
    private uint lastHp;
    private bool lastDarkArts;
    private bool lastOwnGuardBlocked;
    private bool lastTargetGuardBlocked;
    private bool lastQueueOwned;
    private float lastAnimationLockSeconds;
    private long armedCount;
    private long pairedCount;
    private long claimedCount;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;
    private string lastEvent = "Not started";
    private bool started;
    private bool disposed;

    internal DarkKnightShadowbringerMacroService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IDutyState dutyState,
        IDataManager dataManager,
        IFramework framework,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.dutyState = dutyState;
        this.dataManager = dataManager;
        this.framework = framework;
        this.log = log;
        metadataVerified = ValidateMetadata(dataManager, log);
        wolvesDenDummyMetadataVerified = ValidateWolvesDenDummyMetadata(dataManager, log);
    }

    internal DarkKnightShadowbringerMacroDiagnostics Diagnostics
    {
        get
        {
            lock (stateGate)
            {
                var now = Environment.TickCount64;
                var remaining = armedMacro is { } arm
                    ? Math.Max(0, arm.ExpiresAtMilliseconds - now)
                    : 0;
                return new DarkKnightShadowbringerMacroDiagnostics(
                    started && !disposed,
                    configuration.Enabled && configuration.EnableDarkKnightShadowbringerMacro,
                    metadataVerified,
                    wolvesDenDummyMetadataVerified,
                    armedMacro is not null && remaining > 0,
                    remaining,
                    armedMacro?.MacroLine ?? 0,
                    cycleState.CurrentCycleToken,
                    cycleState.SpentCycleToken,
                    lastRecastGroupIndex,
                    lastRecastActionId,
                    lastRecastElapsedSeconds,
                    lastRecastTotalSeconds,
                    lastRecastRemainingSeconds,
                    lastUsedActionSequence,
                    lastRawCarrierActionId,
                    lastAdjustedCarrierActionId,
                    lastMode,
                    lastComboRouteId,
                    lastContext,
                    lastEnemySlot,
                    lastTargetGameObjectId,
                    lastTargetEntityId,
                    lastShadowbringerAdjustedActionId,
                    lastHp,
                    lastDarkArts,
                    lastOwnGuardBlocked,
                    lastTargetGuardBlocked,
                    lastQueueOwned,
                    lastAnimationLockSeconds,
                    armedCount,
                    pairedCount,
                    claimedCount,
                    attemptCount,
                    acceptedCount,
                    lastEvent);
            }
        }
    }

    internal void Start()
    {
        if (disposed || started) return;
        started = true;
        framework.Update += OnFrameworkUpdate;
        lock (stateGate)
        {
            lastEvent = metadataVerified
                ? wolvesDenDummyMetadataVerified
                    ? "Ready"
                    : "Ready for CC; Wolves' Den dummy metadata mismatch"
                : "Metadata mismatch; disabled";
        }
    }

    internal DarkKnightShadowbringerArmResult Arm(string arguments, bool hookAvailable)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(arguments))
                return RecordArmFailure(DarkKnightShadowbringerArmOutcome.NotMacroInvocation);
            if (!configuration.Enabled || !configuration.EnableDarkKnightShadowbringerMacro)
                return RecordArmFailure(DarkKnightShadowbringerArmOutcome.Disabled);
            if (!hookAvailable)
                return RecordArmFailure(DarkKnightShadowbringerArmOutcome.HookUnavailable);
            if (!metadataVerified)
                return RecordArmFailure(DarkKnightShadowbringerArmOutcome.MetadataMismatch);

            var shell = RaptureShellModule.Instance();
            if (shell == null ||
                !shell->MacroLocked ||
                shell->MacroCurrentLine is < 0 or >= 15 ||
                !string.Equals(
                    shell->MacroLineText.ToString().Trim(),
                    Command,
                    StringComparison.OrdinalIgnoreCase))
            {
                return RecordArmFailure(DarkKnightShadowbringerArmOutcome.NotMacroInvocation);
            }

            var local = objectTable.LocalPlayer;
            var context = ResolveContext();
            if (!DarkKnightShadowbringerMacroRules.CanExecuteInContext(
                    context,
                    configuration.EnableWolvesDenTesting))
            {
                return RecordArmFailure(DarkKnightShadowbringerArmOutcome.NotSupportedPvPContext);
            }
            if (context == SupportedPvPContext.WolvesDen && !wolvesDenDummyMetadataVerified)
            {
                return RecordArmFailure(
                    DarkKnightShadowbringerArmOutcome.WolvesDenDummyMetadataMismatch);
            }
            if (!IsExactLocalDarkKnight(local))
                return RecordArmFailure(DarkKnightShadowbringerArmOutcome.LocalDarkKnightInvalid);

            ObserveCycleNow(ActionManager.Instance());
            DarkKnightGcdCycleState currentCycle;
            lock (stateGate) currentCycle = cycleState;
            if (!currentCycle.HasProvenCycle)
                return RecordArmFailure(DarkKnightShadowbringerArmOutcome.CycleUnknown);
            if (currentCycle.CurrentCycleSpent)
                return RecordArmFailure(DarkKnightShadowbringerArmOutcome.CycleSpent);

            var now = Environment.TickCount64;
            var arm = new DarkKnightShadowbringerMacroArm(
                shell->MacroCurrentLine,
                shell->MacroName.ToString(),
                SaturatingAdd(now, DarkKnightShadowbringerMacroRules.MacroTokenLifetimeMilliseconds),
                clientState.TerritoryType,
                local!.GameObjectId,
                local.EntityId,
                local.Address,
                currentCycle.CurrentCycleToken);
            lock (stateGate)
            {
                armedMacro = arm;
                armedCount++;
                lastEvent = $"Armed cycle {arm.CycleToken} from macro line {arm.MacroLine}";
            }

            return new DarkKnightShadowbringerArmResult(DarkKnightShadowbringerArmOutcome.Armed);
        }
        catch (Exception exception)
        {
            lock (stateGate)
            {
                armedMacro = null;
                lastEvent = "Arm failed closed";
            }
            LogFailure(exception, "Seiton Sense Shadowbringer macro arm failed closed.");
            return new DarkKnightShadowbringerArmResult(DarkKnightShadowbringerArmOutcome.FailedClosed);
        }
    }

    internal bool TryConsumePairedCarrier(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        out DarkKnightShadowbringerPairedCarrier pairedCarrier)
    {
        pairedCarrier = default;
        DarkKnightShadowbringerMacroArm arm;
        lock (stateGate)
        {
            if (armedMacro is not { } currentArm) return false;
            arm = currentArm;
            armedMacro = null;
        }

        try
        {
            ObserveCycleNow(actionManager);
            var local = objectTable.LocalPlayer;
            var shell = RaptureShellModule.Instance();
            DarkKnightGcdCycleState currentCycle;
            lock (stateGate) currentCycle = cycleState;
            var adjustedActionId = actionManager == null || actionType != ActionType.Action
                ? 0
                : actionManager->GetAdjustedActionId(actionId);
            var pair = DarkKnightShadowbringerMacroRules.EvaluatePair(
                arm,
                new DarkKnightShadowbringerPairObservation(
                    Environment.TickCount64,
                    shell != null && shell->MacroLocked,
                    shell == null ? 0 : shell->MacroCurrentLine,
                    shell == null ? string.Empty : shell->MacroName.ToString(),
                    clientState.TerritoryType,
                    local?.GameObjectId ?? 0,
                    local?.EntityId ?? 0,
                    local?.Address ?? 0,
                    currentCycle.CurrentCycleToken,
                    (uint)actionType,
                    actionId,
                    adjustedActionId,
                    (uint)mode,
                    comboRouteId,
                     extraParam));
            RecordCarrier(actionId, adjustedActionId, mode, comboRouteId, pair.Decision.ToString());
            var context = ResolveContext();
            if (!pair.IsPaired ||
                !CanExecuteInContext(context) ||
                !IsExactLocalDarkKnight(local))
            {
                return false;
            }

            var usedDefaultTargetCarrier = CcImmunityBrakeTargetRules.IsDefaultTargetCarrier(targetId);
            var nativeHardTargetId = usedDefaultTargetCarrier || context == SupportedPvPContext.WolvesDen
                ? GetNativeHardTargetId(local)
                : 0;
            var effectiveTargetId = CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(
                targetId,
                targetId,
                nativeHardTargetId);
            if (!TryResolveExactTarget(
                    context,
                    local,
                    effectiveTargetId,
                    nativeHardTargetId,
                    out var target,
                    out var enemySlot,
                    out var resolution) ||
                target is null ||
                ((usedDefaultTargetCarrier || context == SupportedPvPContext.WolvesDen) &&
                 GetNativeHardTargetId(local) != nativeHardTargetId))
            {
                lock (stateGate) lastEvent = $"Pair target rejected: {resolution}";
                return false;
            }

            pairedCarrier = new DarkKnightShadowbringerPairedCarrier(
                arm,
                context,
                actionId,
                adjustedActionId,
                (uint)mode,
                comboRouteId,
                effectiveTargetId,
                usedDefaultTargetCarrier,
                nativeHardTargetId,
                enemySlot,
                target.GameObjectId,
                target.EntityId,
                target.Address,
                target.ObjectKind,
                target.SubKind,
                target.NameId);
            lock (stateGate)
            {
                pairedCount++;
                lastContext = context;
                lastEnemySlot = enemySlot;
                lastTargetGameObjectId = target.GameObjectId;
                lastTargetEntityId = target.EntityId;
                lastEvent = context == SupportedPvPContext.CrystallineConflict
                    ? $"Paired S{enemySlot} for cycle {arm.CycleToken}"
                    : $"Paired exact Den hard target for cycle {arm.CycleToken}";
            }
            return true;
        }
        catch (Exception exception)
        {
            lock (stateGate) lastEvent = "Pair failed closed";
            LogFailure(exception, "Seiton Sense Shadowbringer macro pairing failed closed.");
            return false;
        }
    }

    internal bool TryAttemptOnce(
        ActionManager* actionManager,
        DarkKnightShadowbringerPairedCarrier pairedCarrier,
        bool safeCarrierPath,
        Func<bool> isOwnGuardActiveOrPropagating,
        Func<bool> dispatch)
    {
        ArgumentNullException.ThrowIfNull(isOwnGuardActiveOrPropagating);
        ArgumentNullException.ThrowIfNull(dispatch);
        try
        {
            var preliminary = CaptureAttempt(
                actionManager,
                pairedCarrier,
                safeCarrierPath,
                isOwnGuardActiveOrPropagating,
                baseline: null,
                cycleOwnedByThisAttempt: false);
            var preliminaryDecision = DarkKnightShadowbringerMacroRules.EvaluateAttempt(
                preliminary.Observation);
            RecordAttemptSnapshot(preliminary, preliminaryDecision.Decision.ToString());
            if (!preliminaryDecision.ShouldAttempt) return false;

            lock (stateGate)
            {
                if (!DarkKnightShadowbringerMacroRules.TrySpendCycle(
                        cycleState,
                        pairedCarrier.Arm.CycleToken,
                        out var spentState))
                {
                    lastEvent = "Cycle ownership changed before claim";
                    return false;
                }

                cycleState = spentState;
                claimedCount++;
                lastEvent = $"Claimed cycle {pairedCarrier.Arm.CycleToken}";
            }

            var final = CaptureAttempt(
                actionManager,
                pairedCarrier,
                safeCarrierPath,
                isOwnGuardActiveOrPropagating,
                preliminary,
                cycleOwnedByThisAttempt: true);
            var finalDecision = DarkKnightShadowbringerMacroRules.EvaluateAttempt(final.Observation);
            RecordAttemptSnapshot(final, $"Final {finalDecision.Decision}");
            if (!finalDecision.ShouldAttempt) return false;

            Interlocked.Increment(ref attemptCount);
            bool accepted;
            try
            {
                accepted = dispatch();
            }
            catch (Exception exception)
            {
                accepted = false;
                LogFailure(exception, "Seiton Sense Shadowbringer native attempt failed closed and will not retry this GCD.");
            }

            if (accepted) Interlocked.Increment(ref acceptedCount);
            lock (stateGate)
            {
                lastEvent = accepted
                    ? $"Shadowbringer accepted for cycle {pairedCarrier.Arm.CycleToken}"
                    : $"Shadowbringer rejected for spent cycle {pairedCarrier.Arm.CycleToken}";
            }
            return true;
        }
        catch (Exception exception)
        {
            lock (stateGate) lastEvent = "Attempt failed closed";
            LogFailure(exception, "Seiton Sense Shadowbringer attempt failed closed.");
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        started = false;
        lock (stateGate)
        {
            armedMacro = null;
            cycleState = DarkKnightGcdCycleState.Initial;
            lastEvent = "Disposed";
        }
    }

    private void OnFrameworkUpdate(IFramework _) => ObserveCycleNow(ActionManager.Instance());

    private void ObserveCycleNow(ActionManager* actionManager)
    {
        try
        {
            var local = objectTable.LocalPlayer;
            var context = ResolveContext();
            if (!CanExecuteInContext(context) ||
                !IsExactLocalDarkKnight(local))
            {
                HardReset("Context, player, job, or life changed");
                return;
            }

            var identityChanged = false;
            lock (stateGate)
            {
                identityChanged = hasObservedLifetime &&
                    (observedTerritoryId != clientState.TerritoryType ||
                     observedLocalGameObjectId != local!.GameObjectId ||
                     observedLocalEntityId != local.EntityId ||
                     observedLocalAddress != local.Address);
                hasObservedLifetime = true;
                observedTerritoryId = clientState.TerritoryType;
                observedLocalGameObjectId = local!.GameObjectId;
                observedLocalEntityId = local.EntityId;
                observedLocalAddress = local.Address;
            }
            if (identityChanged)
            {
                HardReset("Local identity changed");
                lock (stateGate)
                {
                    hasObservedLifetime = true;
                    observedTerritoryId = clientState.TerritoryType;
                    observedLocalGameObjectId = local!.GameObjectId;
                    observedLocalEntityId = local.EntityId;
                    observedLocalAddress = local.Address;
                }
            }

            var groupIndex = actionManager == null
                ? -1
                : actionManager->GetRecastGroup(
                    (int)ActionType.Action,
                    DarkKnightShadowbringerMacroRules.HardSlashActionId);
            var detail = actionManager == null || groupIndex < 0
                ? null
                : actionManager->GetRecastGroupDetail(groupIndex);
            var adjustedRecast = actionManager == null
                ? 0
                : ActionManager.GetAdjustedRecastTime(
                    ActionType.Action,
                    DarkKnightShadowbringerMacroRules.HardSlashActionId,
                    true);
            var observation = new DarkKnightGcdObservation(
                HardReset: false,
                Known: actionManager != null && detail != null,
                RecastGroupIndex: groupIndex,
                IsActive: detail != null && detail->IsActive,
                ActionId: detail == null ? 0 : detail->ActionId,
                ElapsedSeconds: detail == null ? 0f : detail->Elapsed,
                TotalSeconds: detail == null ? 0f : detail->Total,
                AdjustedRecastMilliseconds: adjustedRecast,
                LastUsedActionSequence: actionManager == null ? (ushort)0 : actionManager->LastUsedActionSequence);
            lock (stateGate)
            {
                var result = DarkKnightShadowbringerMacroRules.ObserveCycle(cycleState, observation);
                cycleState = result.State;
                lastRecastGroupIndex = groupIndex;
                lastRecastActionId = observation.ActionId;
                lastRecastElapsedSeconds = observation.ElapsedSeconds;
                lastRecastTotalSeconds = observation.TotalSeconds;
                lastRecastRemainingSeconds = observation.IsActive &&
                                            float.IsFinite(observation.TotalSeconds) &&
                                            float.IsFinite(observation.ElapsedSeconds)
                    ? Math.Max(0f, observation.TotalSeconds - observation.ElapsedSeconds)
                    : 0f;
                lastUsedActionSequence = observation.LastUsedActionSequence;
                if (result.Outcome == DarkKnightGcdObservationOutcome.OpenedCycle)
                    lastEvent = $"Observed GCD cycle {cycleState.CurrentCycleToken}";
            }
        }
        catch (Exception exception)
        {
            lock (stateGate) lastEvent = "GCD observation unknown";
            LogFailure(exception, "Seiton Sense Shadowbringer GCD observation failed closed.");
        }
    }

    private RuntimeAttemptSnapshot CaptureAttempt(
        ActionManager* actionManager,
        DarkKnightShadowbringerPairedCarrier carrier,
        bool safeCarrierPath,
        Func<bool> isOwnGuardActiveOrPropagating,
        RuntimeAttemptSnapshot? baseline,
        bool cycleOwnedByThisAttempt)
    {
        ObserveCycleNow(actionManager);
        var local = objectTable.LocalPlayer;
        var context = ResolveContext();
        var exactContext = context == carrier.Context &&
                           CanExecuteInContext(context) &&
                           clientState.TerritoryType == carrier.Arm.TerritoryId;
        var localIdentityStable = IsExactLocalDarkKnight(local) &&
                                  local!.GameObjectId == carrier.Arm.LocalGameObjectId &&
                                  local.EntityId == carrier.Arm.LocalEntityId &&
                                  local.Address == carrier.Arm.LocalAddress;
        var localAliveAndTargetable = localIdentityStable &&
                                      !local!.IsDead &&
                                      local.CurrentHp > 0 &&
                                      local.IsTargetable;
        var localIsDarkKnight = local?.ClassJob.IsValid == true &&
                                local.ClassJob.RowId == DarkKnightShadowbringerMacroRules.DarkKnightJobId;

        DarkKnightGcdCycleState currentCycle;
        lock (stateGate) currentCycle = cycleState;

        var groupIndex = actionManager == null
            ? -1
            : actionManager->GetRecastGroup(
                (int)ActionType.Action,
                DarkKnightShadowbringerMacroRules.HardSlashActionId);
        var detail = actionManager == null || groupIndex < 0
            ? null
            : actionManager->GetRecastGroupDetail(groupIndex);
        var adjustedComboRecast = actionManager == null
            ? 0
            : ActionManager.GetAdjustedRecastTime(
                ActionType.Action,
                DarkKnightShadowbringerMacroRules.HardSlashActionId,
                true);
        var exactCycleSnapshot = detail != null &&
                                 DarkKnightShadowbringerMacroRules.IsComboCarrierAction(detail->ActionId) &&
                                 DarkKnightShadowbringerMacroRules.IsExactComboTiming(
                                     groupIndex,
                                     detail->Total,
                                     adjustedComboRecast) &&
                                 float.IsFinite(detail->Elapsed) &&
                                 detail->Elapsed >= 0f;
        var cycleActive = exactCycleSnapshot && detail->IsActive;
        var remaining = cycleActive ? Math.Max(0f, detail->Total - detail->Elapsed) : 0f;

        var queue = CaptureNativeQueue(actionManager);
        var sequence = actionManager == null ? (ushort)0 : actionManager->LastUsedActionSequence;
        var queueStable = !queue.Active && (baseline is null || queue == baseline.Value.Queue);
        var sequenceStable = baseline is null || sequence == baseline.Value.Sequence;
        var animationLock = actionManager == null ? float.PositiveInfinity : actionManager->AnimationLock;
        var animationLockClear = float.IsFinite(animationLock) &&
                                 animationLock >= 0f &&
                                 animationLock <= AnimationLockEpsilonSeconds;
        var notCasting = localIdentityStable &&
                         !local!.IsCasting &&
                         actionManager != null &&
                         actionManager->CastActionId == 0;
        var ownGuardBlocked = true;
        try
        {
            ownGuardBlocked = isOwnGuardActiveOrPropagating();
        }
        catch
        {
            // Unknown Guard ownership fails closed.
        }

        var exactTarget = TryResolveExactTarget(
            context,
            local,
            carrier.EffectiveTargetId,
            carrier.NativeHardTargetId,
            out var target,
            out var enemySlot,
            out _);
        var targetIdentityStable = exactTarget &&
                                   target is not null &&
                                   enemySlot == carrier.EnemySlot &&
                                    target.GameObjectId == carrier.TargetGameObjectId &&
                                    target.EntityId == carrier.TargetEntityId &&
                                    target.Address == carrier.TargetAddress &&
                                    (carrier.Context != SupportedPvPContext.WolvesDen ||
                                     target.ObjectKind == carrier.TargetObjectKind &&
                                     target.SubKind == carrier.TargetSubKind &&
                                     target.NameId == carrier.TargetNameId) &&
                                    (!(carrier.UsedDefaultTargetCarrier ||
                                       carrier.Context == SupportedPvPContext.WolvesDen) ||
                                     GetNativeHardTargetId(local) == carrier.NativeHardTargetId);
        var targetAliveAndTargetable = targetIdentityStable &&
                                       !target!.IsDead &&
                                       target.CurrentHp > 0 &&
                                       target.IsTargetable;
        var targetGuardBlocked = targetIdentityStable &&
                                 (HasActiveStatus(target!, EnemyCombatConstants.GuardStatusId) ||
                                  HasActiveStatus(target!, EnemyCombatConstants.GuardStatusAlternateId));

        var currentAdjustedCarrier = actionManager == null
            ? 0
            : actionManager->GetAdjustedActionId(carrier.RawActionId);
        var sourceObject = localIdentityStable ? GetNativeObject(local!) : null;
        var targetObject = targetIdentityStable ? GetNativeObject(target!) : null;
        var comboRange = sourceObject != null &&
                         targetObject != null &&
                         currentAdjustedCarrier == carrier.AdjustedActionId &&
                         SeitonRangeRules.HasNativeRangeAndLineOfSight(
                             ActionManager.GetActionInRangeOrLoS(
                                 currentAdjustedCarrier,
                                 sourceObject,
                                 targetObject));
        var comboStructurallyReady = actionManager != null &&
                                     currentAdjustedCarrier == carrier.AdjustedActionId &&
                                     DarkKnightShadowbringerMacroRules.IsComboCarrierAction(currentAdjustedCarrier) &&
                                     actionManager->GetActionStatus(
                                         ActionType.Action,
                                         currentAdjustedCarrier,
                                         carrier.EffectiveTargetId,
                                         checkRecastActive: false,
                                         checkCastingActive: true) == 0;

        var shadowAdjusted = actionManager == null
            ? 0
            : actionManager->GetAdjustedActionId(
                DarkKnightShadowbringerMacroRules.ShadowbringerActionId);
        var darkArts = localIdentityStable &&
                       HasActiveStatus(local!, DarkKnightShadowbringerMacroRules.DarkArtsStatusId);
        var shadowRange = sourceObject != null &&
                          targetObject != null &&
                          (shadowAdjusted is DarkKnightShadowbringerMacroRules.ShadowbringerActionId or
                              DarkKnightShadowbringerMacroRules.DarkArtsShadowbringerActionId) &&
                          SeitonRangeRules.HasNativeRangeAndLineOfSight(
                              ActionManager.GetActionInRangeOrLoS(
                                  shadowAdjusted,
                                  sourceObject,
                                  targetObject));
        var shadowGroupIndex = actionManager == null || shadowAdjusted == 0
            ? -1
            : actionManager->GetRecastGroup((int)ActionType.Action, shadowAdjusted);
        var shadowDetail = actionManager == null || shadowGroupIndex < 0
            ? null
            : actionManager->GetRecastGroupDetail(shadowGroupIndex);
        var shadowCooldownReady = actionManager != null &&
                                  shadowGroupIndex == DarkKnightShadowbringerMacroRules.ShadowbringerRecastGroupIndex &&
                                  shadowDetail != null &&
                                  !shadowDetail->IsActive &&
                                  actionManager->GetAdditionalRecastGroup(ActionType.Action, shadowAdjusted) < 0 &&
                                  ActionManager.GetAdjustedRecastTime(
                                      ActionType.Action,
                                      shadowAdjusted,
                                      true) ==
                                  DarkKnightShadowbringerMacroRules.ShadowbringerAdjustedRecastMilliseconds;
        var shadowActionReady = actionManager != null &&
                                targetIdentityStable &&
                                actionManager->GetActionStatus(
                                    ActionType.Action,
                                    shadowAdjusted,
                                    carrier.EffectiveTargetId,
                                    checkRecastActive: true,
                                    checkCastingActive: true) == 0;
        var shadowResourcesReady = actionManager != null &&
                                   actionManager->CheckActionResources(
                                       ActionType.Action,
                                       shadowAdjusted) == 0;

        var observation = new DarkKnightShadowbringerAttemptObservation(
            configuration.Enabled,
            configuration.EnableDarkKnightShadowbringerMacro,
            metadataVerified,
            exactContext,
            localIdentityStable,
            localAliveAndTargetable,
            localIsDarkKnight,
            safeCarrierPath &&
            carrier.ComboRouteId == DarkKnightShadowbringerMacroRules.SouleaterComboRouteId &&
            carrier.UseActionMode is 0 or 100,
            exactCycleSnapshot,
            cycleActive,
            carrier.Arm.CycleToken,
            currentCycle.CurrentCycleToken,
            currentCycle.SpentCycleToken,
            cycleOwnedByThisAttempt,
            remaining,
            queueStable,
            sequenceStable,
            animationLockClear,
            notCasting,
            !ownGuardBlocked,
            targetIdentityStable,
            targetAliveAndTargetable,
            !targetGuardBlocked,
            comboRange,
            shadowRange,
            comboStructurallyReady,
            shadowAdjusted,
            darkArts,
            local?.CurrentHp ?? 0,
            shadowCooldownReady,
            shadowActionReady,
            shadowResourcesReady);
        return new RuntimeAttemptSnapshot(
            observation,
            queue,
            sequence,
            detail == null ? 0 : detail->ActionId,
            detail == null ? 0f : detail->Elapsed,
            detail == null ? 0f : detail->Total,
            remaining,
            shadowAdjusted,
            local?.CurrentHp ?? 0,
            darkArts,
            ownGuardBlocked,
            targetGuardBlocked,
            animationLock);
    }

    private void RecordAttemptSnapshot(
        RuntimeAttemptSnapshot snapshot,
        string reason)
    {
        lock (stateGate)
        {
            lastRecastActionId = snapshot.RecastActionId;
            lastRecastElapsedSeconds = snapshot.RecastElapsedSeconds;
            lastRecastTotalSeconds = snapshot.RecastTotalSeconds;
            lastRecastRemainingSeconds = snapshot.RecastRemainingSeconds;
            lastUsedActionSequence = snapshot.Sequence;
            lastShadowbringerAdjustedActionId = snapshot.ShadowbringerAdjustedActionId;
            lastHp = snapshot.CurrentHp;
            lastDarkArts = snapshot.DarkArts;
            lastOwnGuardBlocked = snapshot.OwnGuardBlocked;
            lastTargetGuardBlocked = snapshot.TargetGuardBlocked;
            lastQueueOwned = snapshot.Queue.Active;
            lastAnimationLockSeconds = snapshot.AnimationLockSeconds;
            lastEvent = reason;
        }
    }

    private DarkKnightShadowbringerArmResult RecordArmFailure(
        DarkKnightShadowbringerArmOutcome outcome)
    {
        lock (stateGate)
        {
            armedMacro = null;
            lastEvent = outcome.ToString();
        }
        return new DarkKnightShadowbringerArmResult(outcome);
    }

    private void RecordCarrier(
        uint rawActionId,
        uint adjustedActionId,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        string reason)
    {
        lock (stateGate)
        {
            lastRawCarrierActionId = rawActionId;
            lastAdjustedCarrierActionId = adjustedActionId;
            lastMode = (uint)mode;
            lastComboRouteId = comboRouteId;
            lastEvent = reason;
        }
    }

    private void HardReset(string reason)
    {
        lock (stateGate)
        {
            cycleState = DarkKnightGcdCycleState.Initial;
            armedMacro = null;
            hasObservedLifetime = false;
            lastRecastGroupIndex = -1;
            lastRecastActionId = 0;
            lastRecastElapsedSeconds = 0f;
            lastRecastTotalSeconds = 0f;
            lastRecastRemainingSeconds = 0f;
            lastUsedActionSequence = 0;
            lastEvent = reason;
        }
    }

    private bool TryResolveExactTarget(
        SupportedPvPContext context,
        IPlayerCharacter? localPlayer,
        ulong targetId,
        ulong nativeHardTargetId,
        out DalamudBattleChara? target,
        out int enemySlot,
        out string resolution)
    {
        target = null;
        enemySlot = 0;
        if (context == SupportedPvPContext.CrystallineConflict)
        {
            var resolved = TryResolveExactCanonicalEnemy(
                localPlayer,
                targetId,
                out var enemy,
                out enemySlot,
                out resolution);
            target = enemy;
            return resolved;
        }

        if (context == SupportedPvPContext.WolvesDen && CanExecuteInContext(context))
        {
            return TryResolveExactWolvesDenHardTarget(
                localPlayer,
                targetId,
                nativeHardTargetId,
                out target,
                out resolution);
        }

        resolution = "Unsupported PvP context";
        return false;
    }

    private bool TryResolveExactWolvesDenHardTarget(
        IPlayerCharacter? localPlayer,
        ulong targetId,
        ulong nativeHardTargetId,
        out DalamudBattleChara? target,
        out string resolution)
    {
        target = null;
        resolution = "Local player invalid";
        if (!HasValidNativeIdentity(localPlayer)) return false;
        resolution = "Native hard target ID invalid";
        if (!IsNetworkObjectId(nativeHardTargetId)) return false;
        resolution = "Incoming macro target ID invalid";
        if (!IsNetworkObjectId(targetId)) return false;

        var byObjectId = objectTable.SearchById(nativeHardTargetId) as DalamudBattleChara;
        var byEntityId = nativeHardTargetId <= uint.MaxValue
            ? objectTable.SearchByEntityId((uint)nativeHardTargetId) as DalamudBattleChara
            : null;
        if (byObjectId is not null &&
            byEntityId is not null &&
            !HasSameNativeIdentity(byObjectId, byEntityId))
        {
            resolution = "Native hard target ID resolved ambiguously";
            return false;
        }

        var candidate = byObjectId ?? byEntityId;
        resolution = "Native hard target is not a battle character";
        if (!HasValidNativeIdentity(candidate)) return false;
        resolution = "Native hard target ID does not match the resolved object";
        if (!ActorIdMatches(nativeHardTargetId, candidate!)) return false;
        resolution = "Incoming macro target does not match the native hard target";
        if (!ActorIdMatches(targetId, candidate!)) return false;
        var isSelf = candidate!.GameObjectId == localPlayer!.GameObjectId ||
                     candidate.EntityId == localPlayer.EntityId;
        var battleNpcCombatant = candidate is DalamudBattleNpc
        {
            BattleNpcKind: DalamudBattleNpcSubKind.Combatant,
        } && candidate.ObjectKind == DalamudObjectKind.BattleNpc;
        var aliveWithPositiveHp = !candidate.IsDead &&
                                  candidate.CurrentHp > 0 &&
                                  candidate.MaxHp > 0;
        resolution = "Den hard target is not the exact live striking dummy";
        if (!DarkKnightShadowbringerMacroRules.IsExactWolvesDenStrikingDummy(
                wolvesDenDummyMetadataVerified,
                battleNpcCombatant,
                candidate.NameId,
                nativeIdentityValid: true,
                isSelf: isSelf,
                aliveWithPositiveHp: aliveWithPositiveHp,
                targetable: candidate.IsTargetable))
        {
            return false;
        }

        var canonicalByObjectId = objectTable.SearchById(candidate.GameObjectId) as DalamudBattleChara;
        var canonicalByEntityId = objectTable.SearchByEntityId(candidate.EntityId) as DalamudBattleChara;
        resolution = "Den hard target object-table identity changed";
        if (!HasSameNativeIdentity(candidate, canonicalByObjectId) ||
            !HasSameNativeIdentity(candidate, canonicalByEntityId))
        {
            return false;
        }

        resolution = "Native hard target changed during capture";
        if (GetNativeHardTargetId(localPlayer) != nativeHardTargetId) return false;

        target = candidate;
        resolution = "Exact native Wolves' Den striking-dummy hard target";
        return true;
    }

    private bool TryResolveExactCanonicalEnemy(
        IPlayerCharacter? localPlayer,
        ulong targetId,
        out IPlayerCharacter? target,
        out int enemySlot,
        out string resolution)
    {
        target = null;
        enemySlot = 0;
        resolution = "Local player invalid";
        if (!HasValidNativeIdentity(localPlayer)) return false;
        resolution = "Target ID invalid";
        if (!IsNetworkObjectId(targetId)) return false;

        var partyEntityIds = partyList
            .Select(static member => member.EntityId)
            .Where(IsNetworkEntityId)
            .ToHashSet();
        var visibleEntityIds = objectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .Select(static player => player.EntityId)
            .Where(IsNetworkEntityId)
            .ToHashSet();
        var completePublicCcPartyFallback =
            PvPMatchRules.IsPublicCrystallineConflictTerritory(clientState.TerritoryType) &&
            partyEntityIds.Count == 5 &&
            partyEntityIds.Contains(localPlayer!.EntityId) &&
            partyEntityIds.IsSubsetOf(visibleEntityIds);
        var matches = new List<(int Slot, IPlayerCharacter Player)>(1);
        var seen = new HashSet<(ulong GameObjectId, uint EntityId)>();
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var candidate = EnemySlotResolver.Resolve(objectTable, slot);
            var isSelf = candidate is not null &&
                         (candidate.GameObjectId == localPlayer!.GameObjectId ||
                          candidate.EntityId == localPlayer.EntityId);
            var isPartyOrAlliance = candidate is not null &&
                                    (partyEntityIds.Contains(candidate.EntityId) ||
                                     (candidate.StatusFlags &
                                      (StatusFlags.PartyMember | StatusFlags.AllianceMember)) != 0);
            var hostile = candidate is not null &&
                          (candidate.StatusFlags & StatusFlags.Hostile) != 0;
            if (!HasValidNativeIdentity(candidate) ||
                !candidate!.ClassJob.IsValid ||
                !EnemySlotRules.CanUseResolvedEnemy(
                    isSelf,
                    isPartyOrAlliance,
                    hostile,
                    completePublicCcPartyFallback,
                    !candidate.IsDead && candidate.CurrentHp > 0,
                    candidate.IsTargetable,
                    candidate.CurrentHp,
                    candidate.MaxHp))
            {
                continue;
            }

            if (!seen.Add((candidate.GameObjectId, candidate.EntityId)))
            {
                resolution = "Duplicate canonical enemy identity";
                return false;
            }
            if (targetId == candidate.GameObjectId || targetId == candidate.EntityId)
                matches.Add((slot, candidate));
        }

        if (matches.Count != 1)
        {
            resolution = matches.Count == 0
                ? "Target is not an exact live canonical S1-S5 enemy"
                : "Target matched multiple canonical enemies";
            return false;
        }

        var match = matches[0];
        var tableCandidate = objectTable.SearchByEntityId(match.Player.EntityId) as IPlayerCharacter;
        if (tableCandidate is null ||
            tableCandidate.Address != match.Player.Address ||
            tableCandidate.GameObjectId != match.Player.GameObjectId ||
            tableCandidate.EntityId != match.Player.EntityId)
        {
            resolution = "Object-table identity changed";
            return false;
        }

        target = match.Player;
        enemySlot = match.Slot;
        resolution = "Exact canonical enemy";
        return true;
    }

    private SupportedPvPContext ResolveContext()
    {
        var condition = dutyState.ContentFinderCondition;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            configuration.EnableWolvesDenTesting,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private bool CanExecuteInContext(SupportedPvPContext context) =>
        DarkKnightShadowbringerMacroRules.CanExecuteInContext(
            context,
            configuration.EnableWolvesDenTesting) &&
        (context != SupportedPvPContext.WolvesDen || wolvesDenDummyMetadataVerified);

    private static bool IsExactLocalDarkKnight(IPlayerCharacter? player) =>
        HasValidNativeIdentity(player) &&
        !player!.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp > 0 &&
        player.ClassJob.IsValid &&
        player.ClassJob.RowId == DarkKnightShadowbringerMacroRules.DarkKnightJobId;

    private static bool HasValidNativeIdentity(DalamudGameObject? actor)
    {
        if (actor is null ||
            actor.Address == 0 ||
            !IsNetworkObjectId(actor.GameObjectId) ||
            !IsNetworkEntityId(actor.EntityId))
        {
            return false;
        }

        var native = (GameObject*)actor.Address;
        return native != null && native->EntityId == actor.EntityId;
    }

    private static bool HasSameNativeIdentity(
        DalamudGameObject? left,
        DalamudGameObject? right) =>
        HasValidNativeIdentity(left) &&
        HasValidNativeIdentity(right) &&
        left!.GameObjectId == right!.GameObjectId &&
        left.EntityId == right.EntityId &&
        left.Address == right.Address &&
        left.ObjectKind == right.ObjectKind &&
        left.SubKind == right.SubKind &&
        (left is not DalamudBattleChara leftBattleChara ||
         right is DalamudBattleChara rightBattleChara &&
         leftBattleChara.NameId == rightBattleChara.NameId);

    private static bool ActorIdMatches(ulong actorId, DalamudGameObject actor) =>
        IsNetworkObjectId(actorId) &&
        (actorId == actor.GameObjectId || actorId == actor.EntityId);

    private static GameObject* GetNativeObject(DalamudGameObject actor)
    {
        var native = (GameObject*)actor.Address;
        return native != null && native->EntityId == actor.EntityId ? native : null;
    }

    private static ulong GetNativeHardTargetId(IPlayerCharacter? localPlayer)
    {
        if (!HasValidNativeIdentity(localPlayer)) return 0;
        var character = (Character*)localPlayer!.Address;
        return character == null ? 0 : character->GetTargetId().Id;
    }

    private static bool HasActiveStatus(DalamudBattleChara actor, uint statusId)
    {
        foreach (var status in actor.StatusList)
        {
            if (status.StatusId == statusId &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static NativeQueueFingerprint CaptureNativeQueue(ActionManager* actionManager) =>
        actionManager == null
            ? NativeQueueFingerprint.Invalid
            : new NativeQueueFingerprint(
                actionManager->ActionQueued,
                (uint)actionManager->QueuedActionType,
                actionManager->QueuedActionId,
                (ulong)actionManager->QueuedTargetId,
                actionManager->QueuedExtraParam,
                (uint)actionManager->QueueType,
                actionManager->QueuedComboRouteId);

    private static bool ValidateMetadata(IDataManager dataManager, IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<GameStatus>(ClientLanguage.English);
            var routes = dataManager.GetExcelSheet<ActionComboRoute>(ClientLanguage.English);
            var valid =
                actions.TryGetRow(DarkKnightShadowbringerMacroRules.HardSlashActionId, out var hardSlash) &&
                actions.TryGetRow(DarkKnightShadowbringerMacroRules.SyphonStrikeActionId, out var syphonStrike) &&
                actions.TryGetRow(DarkKnightShadowbringerMacroRules.SouleaterActionId, out var souleater) &&
                actions.TryGetRow(DarkKnightShadowbringerMacroRules.ScarletDeliriumActionId, out var scarlet) &&
                actions.TryGetRow(DarkKnightShadowbringerMacroRules.ComeuppanceActionId, out var comeuppance) &&
                actions.TryGetRow(DarkKnightShadowbringerMacroRules.TorcleaverActionId, out var torcleaver) &&
                actions.TryGetRow(DarkKnightShadowbringerMacroRules.ShadowbringerActionId, out var shadowbringer) &&
                actions.TryGetRow(DarkKnightShadowbringerMacroRules.DarkArtsShadowbringerActionId, out var darkShadowbringer) &&
                descriptions.TryGetRow(DarkKnightShadowbringerMacroRules.ShadowbringerActionId, out var shadowDescription) &&
                descriptions.TryGetRow(DarkKnightShadowbringerMacroRules.DarkArtsShadowbringerActionId, out var darkShadowDescription) &&
                statuses.TryGetRow(DarkKnightShadowbringerMacroRules.DarkArtsStatusId, out var darkArts) &&
                routes.TryGetRow(DarkKnightShadowbringerMacroRules.SouleaterComboRouteId, out var route) &&
                IsExpectedComboAction(
                    hardSlash,
                    "Hard Slash",
                    9142,
                    0,
                    0,
                    expectedSecondaryCostType: 0,
                    preservesCombo: false) &&
                IsExpectedComboAction(
                    syphonStrike,
                    "Syphon Strike",
                    9145,
                    0,
                    0,
                    DarkKnightShadowbringerMacroRules.StandardComboSecondaryCostType,
                    preservesCombo: false) &&
                IsExpectedComboAction(
                    souleater,
                    "Souleater",
                    9146,
                    0,
                    0,
                    DarkKnightShadowbringerMacroRules.StandardComboSecondaryCostType,
                    preservesCombo: false) &&
                IsExpectedComboAction(
                    scarlet,
                    "Scarlet Delirium",
                    9766,
                    10,
                    DarkKnightShadowbringerMacroRules.DeliriumStatusId,
                    DarkKnightShadowbringerMacroRules.DeliriumComboSecondaryCostType,
                    preservesCombo: true) &&
                IsExpectedComboAction(
                    comeuppance,
                    "Comeuppance",
                    9767,
                    10,
                    DarkKnightShadowbringerMacroRules.DeliriumStatusId,
                    DarkKnightShadowbringerMacroRules.DeliriumComboSecondaryCostType,
                    preservesCombo: true) &&
                IsExpectedComboAction(
                    torcleaver,
                    "Torcleaver",
                    9768,
                    10,
                    DarkKnightShadowbringerMacroRules.DeliriumStatusId,
                    DarkKnightShadowbringerMacroRules.DeliriumComboSecondaryCostType,
                    preservesCombo: true) &&
                IsExpectedShadowbringer(
                    shadowbringer,
                    shadowDescription,
                    isPlayerAction: true,
                    primaryCostType: 105,
                    primaryCostValue: DarkKnightShadowbringerMacroRules.ShadowbringerHpCost) &&
                IsExpectedShadowbringer(
                    darkShadowbringer,
                    darkShadowDescription,
                    isPlayerAction: false,
                    primaryCostType: 10,
                    primaryCostValue: DarkKnightShadowbringerMacroRules.DarkArtsStatusId) &&
                route.Name.ToString() == "Souleater Combo" &&
                route.Action.Count >= 3 &&
                route.Action[0].RowId == DarkKnightShadowbringerMacroRules.HardSlashActionId &&
                route.Action[1].RowId == DarkKnightShadowbringerMacroRules.SyphonStrikeActionId &&
                route.Action[2].RowId == DarkKnightShadowbringerMacroRules.SouleaterActionId &&
                route.Unknown4 &&
                darkArts.Name.ToString() == "Dark Arts" &&
                darkArts.Icon == DarkKnightShadowbringerMacroRules.DarkArtsStatusIconId &&
                darkArts.StatusCategory == 1 &&
                darkArts.ClassJobCategory.IsValid &&
                darkArts.ClassJobCategory.RowId == DarkKnightShadowbringerMacroRules.DarkKnightClassJobCategoryId &&
                !darkArts.IsPermanent &&
                !darkArts.CanDispel &&
                !darkArts.LockMovement;
            if (!valid) log.Warning("Seiton Sense DRK Shadowbringer metadata failed closed.");
            return valid;
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Seiton Sense DRK Shadowbringer metadata lookup failed closed.");
            return false;
        }
    }

    private static bool ValidateWolvesDenDummyMetadata(
        IDataManager dataManager,
        IPluginLog log)
    {
        try
        {
            var names = dataManager.GetExcelSheet<BNpcName>(ClientLanguage.English);
            var valid = names.TryGetRow(
                            DarkKnightShadowbringerMacroRules.WolvesDenStrikingDummyNameId,
                            out var strikingDummy) &&
                        strikingDummy.Singular.ToString() == "striking dummy" &&
                        strikingDummy.Plural.ToString() == "striking dummies";
            if (!valid)
            {
                log.Warning(
                    "Seiton Sense Wolves' Den striking-dummy metadata failed closed; " +
                    "Crystalline Conflict DRK support remains available.");
            }

            return valid;
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense Wolves' Den striking-dummy metadata lookup failed closed; " +
                "Crystalline Conflict DRK support remains available.");
            return false;
        }
    }

    private static bool IsExpectedComboAction(
        GameAction action,
        string name,
        uint icon,
        byte primaryCostType,
        uint primaryCostValue,
        byte expectedSecondaryCostType,
        bool preservesCombo) =>
        action.Name.ToString() == name &&
        action.Icon == icon &&
        action.IsPvP &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == DarkKnightShadowbringerMacroRules.DarkKnightJobId &&
        action.ClassJobCategory.IsValid &&
        action.ClassJobCategory.RowId == DarkKnightShadowbringerMacroRules.DarkKnightClassJobCategoryId &&
        action.ActionCategory.IsValid &&
        action.ActionCategory.RowId == 3 &&
        action.CastType == 1 &&
        action.Range == 5 &&
        action.EffectRange == 0 &&
        action.Cast100ms == 0 &&
        action.Recast100ms == 25 &&
        !action.IsPlayerAction &&
        action.PrimaryCostType == primaryCostType &&
        action.PrimaryCostValue == primaryCostValue &&
        action.SecondaryCostType == expectedSecondaryCostType &&
        action.SecondaryCostValue.RowId == 0 &&
        action.CooldownGroup == 58 &&
        action.AdditionalCooldownGroup == 0 &&
        action.MaxCharges == 0 &&
        !action.CanTargetSelf &&
        !action.CanTargetParty &&
        !action.CanTargetAlly &&
        !action.CanTargetAlliance &&
        action.CanTargetHostile &&
        !action.CanTargetOwnPet &&
        !action.CanTargetPartyPet &&
        !action.TargetArea &&
        action.RequiresLineOfSight &&
        action.NeedToFaceTarget &&
        action.PreservesCombo == preservesCombo &&
        !action.AffectsPosition;

    private static bool IsExpectedShadowbringer(
        GameAction action,
        ActionTransient transient,
        bool isPlayerAction,
        byte primaryCostType,
        uint primaryCostValue)
    {
        var description = transient.Description.ToString();
        return action.Name.ToString() == "Shadowbringer" &&
               action.Icon == DarkKnightShadowbringerMacroRules.ShadowbringerIconId &&
               action.IsPvP &&
               action.IsPlayerAction == isPlayerAction &&
               action.ClassJob.IsValid &&
               action.ClassJob.RowId == DarkKnightShadowbringerMacroRules.DarkKnightJobId &&
               action.ClassJobCategory.IsValid &&
               action.ClassJobCategory.RowId == DarkKnightShadowbringerMacroRules.DarkKnightClassJobCategoryId &&
               action.ActionCategory.IsValid &&
               action.ActionCategory.RowId == 4 &&
               action.CastType == 4 &&
               action.Range == 10 &&
               action.EffectRange == 10 &&
               action.Cast100ms == 0 &&
               action.Recast100ms == 10 &&
               action.PrimaryCostType == primaryCostType &&
               action.PrimaryCostValue == primaryCostValue &&
               action.SecondaryCostType == 0 &&
               action.SecondaryCostValue.RowId == 0 &&
               action.CooldownGroup == 1 &&
               action.AdditionalCooldownGroup == 0 &&
               action.MaxCharges == 0 &&
               !action.CanTargetSelf &&
               !action.CanTargetParty &&
               !action.CanTargetAlly &&
               !action.CanTargetAlliance &&
               action.CanTargetHostile &&
               !action.CanTargetOwnPet &&
               !action.CanTargetPartyPet &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.NeedToFaceTarget &&
               action.PreservesCombo &&
               !action.AffectsPosition &&
               description.Contains("Consumes 12,000 HP when executed", StringComparison.Ordinal) &&
               description.Contains("Dark Arts", StringComparison.Ordinal);
    }

    private void LogFailure(Exception exception, string message)
    {
        var now = Environment.TickCount64;
        lock (stateGate)
        {
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
        }

        try
        {
            log.Error(exception, message);
        }
        catch
        {
            // Diagnostics must never alter the action path.
        }
    }

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong objectId) =>
        objectId is not 0 and not InvalidObjectId and not ulong.MaxValue;

    private static long SaturatingAdd(long value, long increment) =>
        value > long.MaxValue - increment ? long.MaxValue : value + increment;

    private readonly record struct NativeQueueFingerprint(
        bool Active,
        uint ActionType,
        uint ActionId,
        ulong TargetId,
        uint ExtraParam,
        uint Mode,
        uint ComboRouteId)
    {
        internal static NativeQueueFingerprint Invalid => new(
            Active: true,
            ActionType: 0,
            ActionId: 0,
            TargetId: 0,
            ExtraParam: 0,
            Mode: 0,
            ComboRouteId: 0);
    }

    private readonly record struct RuntimeAttemptSnapshot(
        DarkKnightShadowbringerAttemptObservation Observation,
        NativeQueueFingerprint Queue,
        ushort Sequence,
        uint RecastActionId,
        float RecastElapsedSeconds,
        float RecastTotalSeconds,
        float RecastRemainingSeconds,
        uint ShadowbringerAdjustedActionId,
        uint CurrentHp,
        bool DarkArts,
        bool OwnGuardBlocked,
        bool TargetGuardBlocked,
        float AnimationLockSeconds);
}
