using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using GameAction = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal enum NearAssistArmOutcome
{
    Armed,
    Disabled,
    HookUnavailable,
    NotCrystallineConflict,
    LocalPlayerUnavailable,
    NoCanonicalEnemySlots,
    NoEligibleAllyTarget,
    FailedClosed,
}

internal readonly record struct NearAssistArmResult(
    NearAssistArmOutcome Outcome,
    int EnemySlot,
    float AllyDistance)
{
    internal bool Success => Outcome == NearAssistArmOutcome.Armed;
}

internal readonly record struct NearAssistDiagnostics(
    bool HookAvailable,
    bool Started,
    bool Armed,
    int EnemySlot,
    long RemainingMilliseconds,
    long ArmedCount,
    long RedirectedCount,
    long FallbackCount,
    string LastEvent,
    string RecentTrace);

/// <summary>
/// Owns a single, short-lived target redirect selected by the /nearassist macro line.
/// It never mutates the game's hard, soft, or focus target and never dispatches an action.
/// The next bounded supported action is forwarded to the native function exactly once, with
/// either the revalidated canonical enemy ID, the caller's original target ID unchanged,
/// or an invalid ID for a failed deliberate carrier so the authored fallback can run.
/// </summary>
internal sealed unsafe class NearAssistRedirector : IDisposable
{
    internal const int TokenLifetimeMilliseconds = 750;
    internal const float MinimumAllyDistance = 5f;
    internal const float MaximumAllyDistance = 30f;

    private const ulong InvalidObjectId = 0xE0000000;
    private const ulong InvalidCarrierTargetId = 0;
    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDutyState dutyState;
    private readonly IDataManager dataManager;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly TargetPressureTracker pressureTracker;
    private readonly object tokenGate = new();
    private readonly Queue<string> recentTrace = new();
    private readonly Hook<ActionManager.Delegates.UseAction>? useActionHook;

    private ArmedNearAssistTarget? armedTarget;
    private NearAssistOneShotState oneShotState = NearAssistOneShotState.Initial;
    private uint observedTerritory;
    private long armedCount;
    private long redirectedCount;
    private long fallbackCount;
    private long nextErrorLogAt;
    private string lastEvent = "Not started";
    private bool started;
    private bool disposed;

    internal NearAssistRedirector(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IDutyState dutyState,
        IDataManager dataManager,
        IGameInteropProvider interop,
        IFramework framework,
        TargetPressureTracker pressureTracker,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.dutyState = dutyState;
        this.dataManager = dataManager;
        this.framework = framework;
        this.pressureTracker = pressureTracker;
        this.log = log;
        observedTerritory = clientState.TerritoryType;

        try
        {
            useActionHook = interop.HookFromAddress<ActionManager.Delegates.UseAction>(
                ActionManager.MemberFunctionPointers.UseAction,
                UseActionDetour);
            lastEvent = "Ready";
        }
        catch (Exception exception)
        {
            lastEvent = "Native action hook unavailable";
            LogFailure(exception, "Seiton Sense Near Assist action hook is unavailable; the feature remains off.");
        }
    }

    internal NearAssistDiagnostics Diagnostics
    {
        get
        {
            lock (tokenGate)
            {
                var now = Environment.TickCount64;
                var token = armedTarget;
                var remaining = token is null
                    ? 0
                    : Math.Max(0, token.Value.ExpiresAtMilliseconds - now);
                return new NearAssistDiagnostics(
                    useActionHook?.IsEnabled == true,
                    started && !disposed,
                    token is not null && oneShotState.IsArmed && remaining > 0,
                    token is { HasRedirectCandidate: true } ? token.Value.EnemySlot : 0,
                    remaining,
                    armedCount,
                    redirectedCount,
                    fallbackCount,
                    lastEvent,
                    string.Join(" | ", recentTrace));
            }
        }
    }

    internal void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;

        framework.Update += OnFrameworkUpdate;
        try
        {
            useActionHook?.Enable();
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense Near Assist action hook could not be enabled; the feature remains off.");
        }

        started = true;
        SetLastEvent(useActionHook?.IsEnabled == true ? "Ready" : "Hook unavailable");
    }

    internal NearAssistArmResult Arm()
    {
        ClearToken("Replaced or cleared by a new arm request");

        if (disposed || !started || !configuration.Enabled || !configuration.EnableNearAssistMacro)
            return ArmFailure(NearAssistArmOutcome.Disabled, "Arm ignored: feature disabled");
        if (useActionHook is null || !useActionHook.IsEnabled)
            return ArmFailure(NearAssistArmOutcome.HookUnavailable, "Arm ignored: hook unavailable");
        var context = ResolveContext();
        if (context != SupportedPvPContext.CrystallineConflict)
            return ArmFailure(NearAssistArmOutcome.NotCrystallineConflict, "Arm ignored: not in Crystalline Conflict");

        var localPlayer = objectTable.LocalPlayer;
        if (!IsLivePlayer(localPlayer))
            return ArmFailure(NearAssistArmOutcome.LocalPlayerUnavailable, "Arm ignored: local player unavailable");
        var local = localPlayer!;
        uint carrierEnemyEntityId = 0;
        ulong carrierEnemyGameObjectId = 0;

        try
        {

            var partyEntityIds = GetPartyEntityIds();
            var canonicalEnemies = ResolveCanonicalEnemies(local, partyEntityIds);
            if (canonicalEnemies.Count == 0)
                return ArmFallbackCarrier(
                    context,
                    local.EntityId,
                    local.GameObjectId,
                    carrierEnemyEntityId,
                    carrierEnemyGameObjectId,
                    "Armed fallback: no canonical enemy slots resolved");

            foreach (var canonicalEnemy in canonicalEnemies.Values)
            {
                if (canonicalEnemy.Slot != EnemySlotRules.FirstSlot) continue;
                carrierEnemyEntityId = canonicalEnemy.Player.EntityId;
                carrierEnemyGameObjectId = canonicalEnemy.Player.GameObjectId;
                break;
            }

            var maximumDistance = Math.Clamp(
                configuration.NearAssistMaxAllyDistance,
                MinimumAllyDistance,
                MaximumAllyDistance);
            var maximumDistanceSquared = maximumDistance * maximumDistance;
            var candidates = new List<AllyCandidate>(4);

            foreach (var ally in objectTable.PlayerObjects.OfType<IPlayerCharacter>())
            {
                if (!IsLivePlayer(ally) ||
                    ally.GameObjectId == local.GameObjectId ||
                    !IsAlly(ally, partyEntityIds))
                {
                    continue;
                }

                var distanceSquared = Vector3.DistanceSquared(local.Position, ally.Position);
                if (!float.IsFinite(distanceSquared) || distanceSquared > maximumDistanceSquared) continue;

                var hardTargetId = GetNativeHardTargetId(ally);
                if (!IsNetworkObjectId(hardTargetId) ||
                    !canonicalEnemies.TryGetValue(hardTargetId, out var enemy))
                {
                    continue;
                }

                var candidate = new AllyCandidate(ally, enemy, distanceSquared);
                candidates.Add(candidate);
            }

            if (candidates.Count == 0)
                return ArmFallbackCarrier(
                    context,
                    local.EntityId,
                    local.GameObjectId,
                    carrierEnemyEntityId,
                    carrierEnemyGameObjectId,
                    "Armed fallback: no nearby ally targets a canonical enemy");

            var selectionIndex = NearAssistPressureSelectionRules.SelectBestIndex(
                candidates
                    .Select(candidate => new NearAssistPressureSelectionCandidate(
                        new NearAssistAllySelectionCandidate(
                            candidate.Ally.EntityId,
                            candidate.DistanceSquared,
                            GetRolePreference(candidate.Ally)),
                        new TargetPressureActorIdentity(
                            candidate.Enemy.Player.GameObjectId,
                            candidate.Enemy.Player.EntityId),
                        pressureTracker.GetTeamTargetCount(
                            candidate.Enemy.Player.GameObjectId,
                            candidate.Enemy.Player.EntityId)))
                    .ToArray(),
                configuration.NearAssistPreferDamageRoles,
                configuration.NearAssistPreferTeamPressure);
            if (selectionIndex < 0)
                return ArmFallbackCarrier(
                    context,
                    local.EntityId,
                    local.GameObjectId,
                    carrierEnemyEntityId,
                    carrierEnemyGameObjectId,
                    "Armed fallback: no stable ally selection");
            var selected = candidates[selectionIndex];
            var now = Environment.TickCount64;
            var nextOneShotState = NearAssistOneShotRules.Arm(
                selected.Enemy.Slot,
                selected.Enemy.Player.GameObjectId,
                now,
                TokenLifetimeMilliseconds);
            if (!nextOneShotState.IsArmed)
                return ArmFallbackCarrier(
                    context,
                    local.EntityId,
                    local.GameObjectId,
                    carrierEnemyEntityId,
                    carrierEnemyGameObjectId,
                    "Armed fallback: invalid redirect state");

            var token = new ArmedNearAssistTarget(
                clientState.TerritoryType,
                local.EntityId,
                local.GameObjectId,
                true,
                selected.Ally.EntityId,
                selected.Ally.GameObjectId,
                selected.Enemy.Slot,
                selected.Enemy.Player.EntityId,
                selected.Enemy.Player.GameObjectId,
                carrierEnemyEntityId,
                carrierEnemyGameObjectId,
                now + TokenLifetimeMilliseconds);
            lock (tokenGate)
            {
                armedTarget = token;
                oneShotState = nextOneShotState;
                armedCount++;
                lastEvent = $"Armed S{token.EnemySlot}";
                RecordTraceLocked(
                    $"arm ctx={context} candidates={candidates.Count} " +
                    $"max={maximumDistance:0.#}y chosen={MathF.Sqrt(selected.DistanceSquared):0.0}y " +
                    $"slot={token.EnemySlot}");
            }

            return new NearAssistArmResult(
                NearAssistArmOutcome.Armed,
                token.EnemySlot,
                MathF.Sqrt(selected.DistanceSquared));
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense Near Assist arm failed closed.");
            return ArmFallbackCarrier(
                context,
                local.EntityId,
                local.GameObjectId,
                carrierEnemyEntityId,
                carrierEnemyGameObjectId,
                "Armed fallback: candidate scan failed closed");
        }
    }

    private NearAssistArmResult ArmFallbackCarrier(
        SupportedPvPContext context,
        uint localEntityId,
        ulong localGameObjectId,
        uint carrierEnemyEntityId,
        ulong carrierEnemyGameObjectId,
        string reason)
    {
        var now = Environment.TickCount64;
        var nextOneShotState = NearAssistOneShotRules.ArmFallback(now, TokenLifetimeMilliseconds);
        if (!nextOneShotState.IsArmed)
            return ArmFailure(NearAssistArmOutcome.FailedClosed, "Arm failed closed: invalid fallback state");

        var token = new ArmedNearAssistTarget(
            clientState.TerritoryType,
            localEntityId,
            localGameObjectId,
            false,
            0,
            0,
            0,
            0,
            0,
            carrierEnemyEntityId,
            carrierEnemyGameObjectId,
            now + TokenLifetimeMilliseconds);
        lock (tokenGate)
        {
            armedTarget = token;
            oneShotState = nextOneShotState;
            armedCount++;
            lastEvent = reason;
            RecordTraceLocked($"arm-fallback ctx={context}: {reason}");
        }

        return new NearAssistArmResult(NearAssistArmOutcome.Armed, 0, 0f);
    }

    internal void Reset() => ClearToken("Reset");

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        started = false;
        ClearToken("Disposed");
        useActionHook?.Dispose();
    }

    private bool UseActionDetour(
        ActionManager* thisPtr,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted)
    {
        var forwardedTargetId = targetId;
        var consumedFallbackCarrier = false;
        try
        {
            if (IsEligibleRedirectAction(thisPtr, actionType, actionId, mode) &&
                TryConsumeEligibleToken(
                    actionType,
                    mode,
                    targetId,
                    out var token,
                    out var previousOneShotState,
                    out consumedFallbackCarrier))
            {
                forwardedTargetId = TryResolveRedirect(
                    thisPtr,
                    actionType,
                    actionId,
                    mode,
                    targetId,
                    token,
                    previousOneShotState,
                    out var rewritten,
                    out var reason);
                if (!rewritten && consumedFallbackCarrier)
                    forwardedTargetId = InvalidCarrierTargetId;
                lock (tokenGate)
                {
                    if (!rewritten)
                    {
                        fallbackCount++;
                        lastEvent = reason;
                    }
                    else
                    {
                        redirectedCount++;
                        lastEvent = $"Redirected S{token.EnemySlot}";
                    }

                    RecordTraceLocked(
                        $"action type={(uint)actionType} id={actionId} mode={(uint)mode} " +
                        $"age={Math.Max(0, Environment.TickCount64 - (token.ExpiresAtMilliseconds - TokenLifetimeMilliseconds))}ms " +
                        $"result={(rewritten ? "redirect" : reason)}");
                }
            }
        }
        catch (Exception exception)
        {
            forwardedTargetId = consumedFallbackCarrier ? InvalidCarrierTargetId : targetId;
            lock (tokenGate)
            {
                armedTarget = null;
                oneShotState = NearAssistOneShotState.Initial;
                fallbackCount++;
                lastEvent = consumedFallbackCarrier
                    ? "Redirect failed closed; carrier invalidated for <t> fallback"
                    : "Redirect failed closed; original target preserved";
            }

            LogFailure(exception, "Seiton Sense Near Assist redirect failed closed with its authored fallback policy.");
        }

        // This is the only native call made by the detour. It is always executed once,
        // with every action argument other than the optional target substitution intact.
        return useActionHook!.Original(
            thisPtr,
            actionType,
            actionId,
            forwardedTargetId,
            extraParam,
            mode,
            comboRouteId,
            outOptAreaTargeted);
    }

    private ulong TryResolveRedirect(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ActionManager.UseActionMode mode,
        ulong originalTargetId,
        ArmedNearAssistTarget token,
        NearAssistOneShotState previousOneShotState,
        out bool rewritten,
        out string reason)
    {
        var now = Environment.TickCount64;
        var localPlayer = objectTable.LocalPlayer;
        var partyEntityIds = GetPartyEntityIds();
        var localIdentityValid = IsLivePlayer(localPlayer) &&
                                 localPlayer!.EntityId == token.LocalEntityId &&
                                 localPlayer.GameObjectId == token.LocalGameObjectId;
        var supportedContext = configuration.Enabled &&
                               configuration.EnableNearAssistMacro &&
                               token.ExpiresAtMilliseconds >= now &&
                               clientState.TerritoryType == token.TerritoryId &&
                                ResolveContext() == SupportedPvPContext.CrystallineConflict &&
                               localIdentityValid;
        var supportedMode = IsCertifiedMacroInvocationMode(mode) &&
                            mode != ActionManager.UseActionMode.Queue;

        var resolvedActionId = ResolveActionId(actionManager, actionType, actionId);
        GameAction action = default;
        var hasActionMetadata = resolvedActionId != 0 &&
                                TryGetActionMetadata(actionType, actionId, resolvedActionId, out action);
        var supportedAction = IsSupportedActionType(actionType) &&
                              hasActionMetadata &&
                              action.IsPvP &&
                              action.Range > 0;
        var hostileAction = hasActionMetadata && action.CanTargetHostile;
        var areaTargetedAction = hasActionMetadata && action.TargetArea;

        var enemy = token.HasRedirectCandidate
            ? EnemySlotResolver.Resolve(objectTable, token.EnemySlot)
            : null;
        var resolvedEnemyValid = token.HasRedirectCandidate &&
                                 IsLivePlayer(enemy) &&
                                 enemy!.EntityId == token.EnemyEntityId &&
                                 enemy.GameObjectId == token.EnemyGameObjectId &&
                                 !IsAlly(enemy, partyEntityIds);
        var resolvedEnemyId = enemy?.GameObjectId ?? 0;

        var sourceObject = localIdentityValid ? GetNativeObject(localPlayer!) : null;
        var targetObject = resolvedEnemyValid ? GetNativeObject(enemy!) : null;
        var hasValidActionTarget = actionManager != null &&
                                   token.HasRedirectCandidate &&
                                   supportedAction &&
                                   hostileAction &&
                                   !areaTargetedAction &&
                                   sourceObject != null &&
                                   targetObject != null;
        // CanUseActionOnTarget also reflects transient execution state on current
        // clients. Calling it here would incorrectly fall back during GCD/animation
        // lock even though the following native macro call is queueable. Exact action
        // metadata plus the canonical actor pointer proves target compatibility; the
        // native range/LoS probe supplies the spatial gate without that false-negative.
        var rangeResult = hasValidActionTarget
            ? ActionManager.GetActionInRangeOrLoS(resolvedActionId, sourceObject, targetObject)
            : uint.MaxValue;

        var attempt = new NearAssistActionAttempt(
            originalTargetId,
            now,
            IsEligibleMacroActionAttempt: true,
            supportedContext,
            supportedAction,
            supportedMode,
            hostileAction,
            areaTargetedAction,
            token.EnemySlot,
            resolvedEnemyId,
            resolvedEnemyValid,
            hasValidActionTarget,
            SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult));
        var decision = NearAssistOneShotRules.Observe(previousOneShotState, attempt);

        // The one-shot state is committed before the detour invokes Original. Even a
        // native rejection or exception can therefore never retry on a later action.
        lock (tokenGate) oneShotState = decision.NextState;
        rewritten = decision.ShouldRewrite;
        reason = decision.ShouldRewrite
            ? $"Redirected S{token.EnemySlot}, resolved={resolvedActionId}, range={rangeResult}"
            : $"Fallback: {decision.Reason}, resolved={resolvedActionId}, range={rangeResult}";
        return decision.ForwardTargetId;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed || !started) return;

        try
        {
            UpdateLifecycle();
        }
        catch (Exception exception)
        {
            ClearToken("Cleared: lifecycle check failed closed");
            LogFailure(exception, "Seiton Sense Near Assist lifecycle check failed closed.");
        }
    }

    private void UpdateLifecycle()
    {

        var territory = clientState.TerritoryType;
        if (territory != observedTerritory)
        {
            observedTerritory = territory;
            ClearToken("Cleared: territory changed");
            return;
        }

        var shouldClear = !configuration.Enabled ||
                          !configuration.EnableNearAssistMacro ||
                          !clientState.IsLoggedIn ||
                           ResolveContext() != SupportedPvPContext.CrystallineConflict ||
                          !IsLivePlayer(objectTable.LocalPlayer);
        lock (tokenGate)
        {
            if (armedTarget is { } token)
            {
                shouldClear |= token.ExpiresAtMilliseconds <= Environment.TickCount64;
            }
        }

        if (shouldClear) ClearToken("Cleared: context, player, or token lifetime changed");
    }

    private Dictionary<ulong, CanonicalEnemy> ResolveCanonicalEnemies(
        IPlayerCharacter localPlayer,
        HashSet<uint> partyEntityIds)
    {
        var result = new Dictionary<ulong, CanonicalEnemy>(5);
        var seenEntityIds = new HashSet<uint>();
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var enemy = EnemySlotResolver.Resolve(objectTable, slot);
            if (!IsLivePlayer(enemy) ||
                enemy!.GameObjectId == localPlayer.GameObjectId ||
                IsAlly(enemy, partyEntityIds) ||
                !seenEntityIds.Add(enemy.EntityId) ||
                !result.TryAdd(enemy.GameObjectId, new CanonicalEnemy(slot, enemy)))
            {
                continue;
            }

            // Character.GetTargetId() is a native actor identity. Depending on the
            // client path, Dalamud may expose the same player through the 64-bit
            // GameObjectId or its 32-bit network EntityId. Both exact identities point
            // at the already revalidated canonical <eN> player; never use object order.
            if (enemy.EntityId != enemy.GameObjectId)
                result.TryAdd(enemy.EntityId, new CanonicalEnemy(slot, enemy));
        }

        return result;
    }

    private bool TryGetActionMetadata(
        ActionType actionType,
        uint originalActionId,
        uint resolvedActionId,
        out GameAction action)
    {
        var actions = dataManager.GetExcelSheet<GameAction>();
        if (actions.TryGetRow(resolvedActionId, out var resolved) && resolved.IsPvP)
        {
            action = resolved;
            return true;
        }

        if (actionType == ActionType.Action &&
            resolvedActionId != originalActionId &&
            actions.TryGetRow(originalActionId, out var original))
        {
            action = original;
            return true;
        }

        if (actions.TryGetRow(resolvedActionId, out resolved))
        {
            action = resolved;
            return true;
        }

        action = default;
        return false;
    }

    private uint ResolveActionId(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId)
    {
        if (actionManager == null || actionId == 0) return 0;
        if (actionType == ActionType.Action) return actionManager->GetAdjustedActionId(actionId);
        if (actionType != ActionType.PvPAction) return 0;

        var pvpActions = dataManager.GetExcelSheet<PvPAction>();
        if (pvpActions.TryGetRow(actionId, out var pvpAction) && pvpAction.Action.IsValid)
            return pvpAction.Action.RowId;

        // Current PvP skills live in the normal Action sheet. Some client paths
        // have historically retained PvPAction as the use type while forwarding
        // that modern Action row ID directly, so preserve that exact ID only when
        // the current sheet confirms it is a PvP action.
        var actions = dataManager.GetExcelSheet<GameAction>();
        return actions.TryGetRow(actionId, out var action) && action.IsPvP
            ? actionId
            : 0;
    }

    private SupportedPvPContext ResolveContext()
    {
        var condition = dutyState.ContentFinderCondition;
        var conditionValid = condition.IsValid;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            includeWolvesDenTesting: false,
            clientState.TerritoryType,
            conditionValid,
            conditionValid && condition.Value.PvP,
            conditionValid ? condition.Value.ContentUICategory.RowId : 0,
            conditionValid && condition.Value.CrystallineConflictCasualRoulette,
            conditionValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private HashSet<uint> GetPartyEntityIds() => partyList
        .Select(member => member.EntityId)
        .Where(IsNetworkEntityId)
        .ToHashSet();

    private bool TryConsumeEligibleToken(
        ActionType actionType,
        ActionManager.UseActionMode mode,
        ulong incomingTargetId,
        out ArmedNearAssistTarget token,
        out NearAssistOneShotState previousOneShotState,
        out bool fallbackCarrier)
    {
        lock (tokenGate)
        {
            if (armedTarget is not { } candidate ||
                !oneShotState.IsArmed ||
                !IsPotentialMacroAction(actionType, mode))
            {
                token = default;
                previousOneShotState = NearAssistOneShotState.Initial;
                fallbackCarrier = false;
                return false;
            }

            token = candidate;
            previousOneShotState = oneShotState;
            var currentPlayer = objectTable.LocalPlayer;
            var currentHardTargetId = IsLivePlayer(currentPlayer)
                ? GetNativeHardTargetId(currentPlayer!)
                : 0;
            fallbackCarrier = NearAssistCarrierRules.IsFallbackCarrier(
                currentHardTargetId,
                incomingTargetId,
                candidate.CarrierEnemyGameObjectId,
                candidate.CarrierEnemyEntityId);
            armedTarget = null;
            oneShotState = NearAssistOneShotState.Initial;
            lastEvent = $"Consumed S{token.EnemySlot}; validating";
            return true;
        }
    }

    private void ClearToken(string reason)
    {
        lock (tokenGate)
        {
            var hadToken = armedTarget is not null || oneShotState.IsArmed;
            armedTarget = null;
            oneShotState = NearAssistOneShotState.Initial;
            lastEvent = reason;
            if (hadToken) RecordTraceLocked(reason);
        }
    }

    private NearAssistArmResult ArmFailure(NearAssistArmOutcome outcome, string reason)
    {
        lock (tokenGate)
        {
            lastEvent = reason;
            RecordTraceLocked($"arm-fail {outcome}: {reason}");
        }
        return new NearAssistArmResult(outcome, 0, 0f);
    }

    private void SetLastEvent(string value)
    {
        lock (tokenGate) lastEvent = value;
    }

    private void LogFailure(Exception exception, string message)
    {
        var now = Environment.TickCount64;
        lock (tokenGate)
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
            // Logging must never alter the action path.
        }
    }

    private static bool IsPotentialMacroAction(
        ActionType actionType,
        ActionManager.UseActionMode mode) =>
        IsSupportedActionType(actionType) &&
        mode != ActionManager.UseActionMode.Queue &&
        (mode == ActionManager.UseActionMode.Macro ||
         mode == ActionManager.UseActionMode.None ||
         (uint)mode == 100);

    private static bool IsCertifiedMacroInvocationMode(ActionManager.UseActionMode mode) =>
        mode == ActionManager.UseActionMode.Macro ||
        mode == ActionManager.UseActionMode.None ||
        (uint)mode == 100;

    private static bool IsSupportedActionType(ActionType actionType) =>
        actionType is ActionType.Action or ActionType.PvPAction;

    private bool IsEligibleRedirectAction(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ActionManager.UseActionMode mode)
    {
        if (!IsPotentialMacroAction(actionType, mode)) return false;

        var resolvedActionId = ResolveActionId(actionManager, actionType, actionId);
        return resolvedActionId != 0 &&
               TryGetActionMetadata(actionType, actionId, resolvedActionId, out var action) &&
               action.IsPvP &&
               action.CanTargetHostile &&
               !action.TargetArea &&
               action.Range > 0;
    }

    private static bool IsAlly(IPlayerCharacter player, HashSet<uint> partyEntityIds) =>
        partyEntityIds.Contains(player.EntityId) ||
        (player.StatusFlags & (StatusFlags.PartyMember | StatusFlags.AllianceMember)) != 0;

    private void RecordTraceLocked(string value)
    {
        while (recentTrace.Count >= 8) recentTrace.Dequeue();
        recentTrace.Enqueue(value);
    }

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != 0 &&
        IsNetworkEntityId(player.EntityId) &&
        IsNetworkObjectId(player.GameObjectId) &&
        player.IsTargetable &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000;

    private static bool IsNetworkObjectId(ulong objectId) =>
        objectId is not 0 and not InvalidObjectId;

    private static ulong GetNativeHardTargetId(IPlayerCharacter player)
    {
        var character = (Character*)player.Address;
        var gameObject = (GameObject*)player.Address;
        return character == null || gameObject == null || gameObject->EntityId != player.EntityId
            ? 0
            : character->GetTargetId().Id;
    }

    private static GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var gameObject = (GameObject*)player.Address;
        return gameObject != null && gameObject->EntityId == player.EntityId
            ? gameObject
            : null;
    }

    private static NearAssistAllyRole GetRolePreference(IPlayerCharacter player)
    {
        var jobId = player.ClassJob.IsValid ? player.ClassJob.RowId : 0;
        return NearAssistSelectionRules.ClassifyPlayableJob(jobId);
    }

    private readonly record struct CanonicalEnemy(int Slot, IPlayerCharacter Player);

    private readonly record struct AllyCandidate(
        IPlayerCharacter Ally,
        CanonicalEnemy Enemy,
        float DistanceSquared);

    private readonly record struct ArmedNearAssistTarget(
        uint TerritoryId,
        uint LocalEntityId,
        ulong LocalGameObjectId,
        bool HasRedirectCandidate,
        uint AllyEntityId,
        ulong AllyGameObjectId,
        int EnemySlot,
        uint EnemyEntityId,
        ulong EnemyGameObjectId,
        uint CarrierEnemyEntityId,
        ulong CarrierEnemyGameObjectId,
        long ExpiresAtMilliseconds);
}
