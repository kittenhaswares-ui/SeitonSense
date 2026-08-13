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

internal enum NearHelpArmOutcome
{
    Armed,
    Disabled,
    HookUnavailable,
    NotCrystallineConflict,
    LocalPlayerUnavailable,
    FailedClosed,
}

internal readonly record struct NearHelpArmResult(NearHelpArmOutcome Outcome)
{
    internal bool Success => Outcome == NearHelpArmOutcome.Armed;
}

internal readonly record struct NearHelpDiagnostics(
    bool Armed,
    long RemainingMilliseconds,
    long ArmedCount,
    long RedirectedCount,
    long FallbackCount,
    string LastEvent);

internal enum FarHelpArmOutcome
{
    Armed,
    Disabled,
    HookUnavailable,
    NotCrystallineConflict,
    LocalPlayerUnavailable,
    FailedClosed,
}

internal readonly record struct FarHelpArmResult(FarHelpArmOutcome Outcome)
{
    internal bool Success => Outcome == FarHelpArmOutcome.Armed;
}

internal readonly record struct FarHelpDiagnostics(
    bool Armed,
    long RemainingMilliseconds,
    long ArmedCount,
    long RedirectedCount,
    long FallbackCount,
    int LastPartySlot,
    float LastDistance,
    string LastEvent);

/// <summary>
/// Owns mutually exclusive, short-lived target redirects selected by the /nearassist
/// /nearhelp, and /farhelp macro lines.
/// It never mutates the game's hard, soft, or focus target and never dispatches an action.
/// The next bounded supported action is forwarded to the native function exactly once, with
/// either the revalidated canonical enemy ID, the caller's original target ID unchanged,
/// or an invalid ID for a failed deliberate carrier so the authored fallback can run.
/// </summary>
internal sealed unsafe class NearAssistRedirector : IDisposable
{
    [ThreadStatic]
    private static int internalRedirectBypassDepth;

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
    private ArmedNearHelpTarget? armedHelpTarget;
    private NearHelpOneShotState nearHelpState = NearHelpOneShotState.Initial;
    private ArmedFarHelpTarget? armedFarHelpTarget;
    private FarHelpOneShotState farHelpState = FarHelpOneShotState.Initial;
    private uint observedTerritory;
    private long armedCount;
    private long redirectedCount;
    private long fallbackCount;
    private long helpArmedCount;
    private long helpRedirectedCount;
    private long helpFallbackCount;
    private string helpLastEvent = "Not started";
    private long farHelpArmedCount;
    private long farHelpRedirectedCount;
    private long farHelpFallbackCount;
    private int farHelpLastPartySlot;
    private float farHelpLastDistance;
    private string farHelpLastEvent = "Not started";
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

    internal NearHelpDiagnostics HelpDiagnostics
    {
        get
        {
            lock (tokenGate)
            {
                var now = Environment.TickCount64;
                var token = armedHelpTarget;
                var remaining = token is null
                    ? 0
                    : Math.Max(0, token.Value.ExpiresAtMilliseconds - now);
                return new NearHelpDiagnostics(
                    token is not null && nearHelpState.IsArmed && remaining > 0,
                    remaining,
                    helpArmedCount,
                    helpRedirectedCount,
                    helpFallbackCount,
                    helpLastEvent);
            }
        }
    }

    internal FarHelpDiagnostics FarHelpDiagnostics
    {
        get
        {
            lock (tokenGate)
            {
                var now = Environment.TickCount64;
                var token = armedFarHelpTarget;
                var remaining = token is null
                    ? 0
                    : Math.Max(0, token.Value.ExpiresAtMilliseconds - now);
                return new FarHelpDiagnostics(
                    token is not null && farHelpState.IsArmed && remaining > 0,
                    remaining,
                    farHelpArmedCount,
                    farHelpRedirectedCount,
                    farHelpFallbackCount,
                    farHelpLastPartySlot,
                    farHelpLastDistance,
                    farHelpLastEvent);
            }
        }
    }

    /// <summary>
    /// Runs one plugin-owned exact-target action through the existing hook without
    /// consuming or rewriting an armed macro token. The detour still reaches its
    /// single Original call with every incoming argument unchanged.
    /// </summary>
    internal T RunWithoutRedirect<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        internalRedirectBypassDepth++;
        try
        {
            return action();
        }
        finally
        {
            internalRedirectBypassDepth--;
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
        var readyState = useActionHook?.IsEnabled == true ? "Ready" : "Hook unavailable";
        SetLastEvent(readyState);
        lock (tokenGate)
        {
            helpLastEvent = readyState;
            farHelpLastEvent = readyState;
        }
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

    internal NearHelpArmResult ArmHelp()
    {
        ClearToken("Replaced or cleared by a new Near Help arm request");

        if (disposed || !started || !configuration.Enabled || !configuration.EnableNearAssistMacro)
            return HelpArmFailure(NearHelpArmOutcome.Disabled, "Near Help arm ignored: feature disabled");
        if (useActionHook is null || !useActionHook.IsEnabled)
            return HelpArmFailure(NearHelpArmOutcome.HookUnavailable, "Near Help arm ignored: hook unavailable");
        var context = ResolveContext();
        if (context != SupportedPvPContext.CrystallineConflict)
            return HelpArmFailure(NearHelpArmOutcome.NotCrystallineConflict, "Near Help arm ignored: not in Crystalline Conflict");

        var localPlayer = objectTable.LocalPlayer;
        if (!IsLivePlayer(localPlayer))
            return HelpArmFailure(NearHelpArmOutcome.LocalPlayerUnavailable, "Near Help arm ignored: local player unavailable");
        var local = localPlayer!;

        try
        {
            var carrier = PartySlotResolver.Resolve(objectTable, 2);
            var carrierValid = IsLivePlayer(carrier) && carrier!.GameObjectId != local.GameObjectId;
            var now = Environment.TickCount64;
            var nextState = NearHelpOneShotRules.Arm(now, TokenLifetimeMilliseconds);
            if (!nextState.IsArmed)
                return HelpArmFailure(NearHelpArmOutcome.FailedClosed, "Near Help arm failed closed: invalid state");

            var token = new ArmedNearHelpTarget(
                clientState.TerritoryType,
                local.EntityId,
                local.GameObjectId,
                carrierValid ? carrier!.EntityId : 0,
                carrierValid ? carrier!.GameObjectId : 0,
                now + TokenLifetimeMilliseconds);
            lock (tokenGate)
            {
                armedHelpTarget = token;
                nearHelpState = nextState;
                helpArmedCount++;
                helpLastEvent = "Armed";
                RecordTraceLocked($"help-arm ctx={context} carrier={(carrierValid ? "<2>" : "none")}");
            }

            return new NearHelpArmResult(NearHelpArmOutcome.Armed);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense Near Help arm failed closed.");
            return HelpArmFailure(NearHelpArmOutcome.FailedClosed, "Near Help arm failed closed: setup failed");
        }
    }

    internal FarHelpArmResult ArmFarHelp()
    {
        ClearToken("Replaced or cleared by a new Far Help arm request");

        if (disposed || !started || !configuration.Enabled || !configuration.EnableNearAssistMacro)
            return FarHelpArmFailure(FarHelpArmOutcome.Disabled, "Far Help arm ignored: feature disabled");
        if (useActionHook is null || !useActionHook.IsEnabled)
            return FarHelpArmFailure(FarHelpArmOutcome.HookUnavailable, "Far Help arm ignored: hook unavailable");
        var context = ResolveContext();
        if (context != SupportedPvPContext.CrystallineConflict)
            return FarHelpArmFailure(FarHelpArmOutcome.NotCrystallineConflict, "Far Help arm ignored: not in Crystalline Conflict");

        var localPlayer = objectTable.LocalPlayer;
        if (!IsLivePlayer(localPlayer))
            return FarHelpArmFailure(FarHelpArmOutcome.LocalPlayerUnavailable, "Far Help arm ignored: local player unavailable");
        var local = localPlayer!;

        try
        {
            // The authored first action line uses <2>. Snapshot that exact native
            // carrier now so a later no-candidate result can invalidate only that
            // deliberate line, allowing the following authored <t> line to run.
            var carrier = PartySlotResolver.Resolve(objectTable, 2);
            var carrierValid = IsLivePlayer(carrier) && carrier!.GameObjectId != local.GameObjectId;
            var now = Environment.TickCount64;
            var nextState = FarHelpOneShotRules.Arm(now, TokenLifetimeMilliseconds);
            if (!nextState.IsArmed)
                return FarHelpArmFailure(FarHelpArmOutcome.FailedClosed, "Far Help arm failed closed: invalid state");

            var token = new ArmedFarHelpTarget(
                clientState.TerritoryType,
                local.EntityId,
                local.GameObjectId,
                carrierValid ? carrier!.EntityId : 0,
                carrierValid ? carrier!.GameObjectId : 0,
                now + TokenLifetimeMilliseconds);
            lock (tokenGate)
            {
                armedFarHelpTarget = token;
                farHelpState = nextState;
                farHelpArmedCount++;
                farHelpLastEvent = "Armed";
                RecordTraceLocked($"far-arm ctx={context} carrier={(carrierValid ? "<2>" : "none")}");
            }

            return new FarHelpArmResult(FarHelpArmOutcome.Armed);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense Far Help arm failed closed.");
            return FarHelpArmFailure(FarHelpArmOutcome.FailedClosed, "Far Help arm failed closed: setup failed");
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
        var handlingNearHelp = false;
        var handlingFarHelp = false;
        var bypassRedirect = internalRedirectBypassDepth > 0;
        try
        {
            if (!bypassRedirect &&
                IsEligibleRedirectAction(thisPtr, actionType, actionId, mode) &&
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
            else if (!bypassRedirect &&
                     IsEligibleHelpAction(thisPtr, actionType, actionId, mode) &&
                     TryConsumeEligibleHelpToken(
                         actionType,
                         mode,
                         targetId,
                         out var helpToken,
                         out var previousHelpState,
                         out consumedFallbackCarrier))
            {
                handlingNearHelp = true;
                forwardedTargetId = TryResolveHelpRedirect(
                    thisPtr,
                    actionType,
                    actionId,
                    mode,
                    targetId,
                    helpToken,
                    previousHelpState,
                    consumedFallbackCarrier,
                    out var rewritten,
                    out var reason);
                if (!rewritten && consumedFallbackCarrier)
                    forwardedTargetId = InvalidCarrierTargetId;
                lock (tokenGate)
                {
                    if (rewritten)
                    {
                        helpRedirectedCount++;
                        helpLastEvent = reason;
                    }
                    else
                    {
                        helpFallbackCount++;
                        helpLastEvent = reason;
                    }

                    RecordTraceLocked(
                        $"help-action type={(uint)actionType} id={actionId} mode={(uint)mode} " +
                        $"age={Math.Max(0, Environment.TickCount64 - (helpToken.ExpiresAtMilliseconds - TokenLifetimeMilliseconds))}ms " +
                        $"result={(rewritten ? "redirect" : reason)}");
                }
            }
            else if (!bypassRedirect &&
                     IsEligibleFarHelpAction(thisPtr, actionType, actionId, mode) &&
                     TryConsumeEligibleFarHelpToken(
                         actionType,
                         mode,
                         targetId,
                         out var farHelpToken,
                         out var previousFarHelpState,
                         out consumedFallbackCarrier))
            {
                handlingFarHelp = true;
                forwardedTargetId = TryResolveFarHelpRedirect(
                    thisPtr,
                    actionType,
                    actionId,
                    mode,
                    targetId,
                    farHelpToken,
                    previousFarHelpState,
                    consumedFallbackCarrier,
                    out var rewritten,
                    out var selectedPartySlot,
                    out var selectedDistance,
                    out var reason);
                lock (tokenGate)
                {
                    if (rewritten)
                    {
                        farHelpRedirectedCount++;
                        farHelpLastPartySlot = selectedPartySlot;
                        farHelpLastDistance = selectedDistance;
                    }
                    else
                    {
                        farHelpFallbackCount++;
                        farHelpLastPartySlot = 0;
                        farHelpLastDistance = 0f;
                    }

                    farHelpLastEvent = reason;
                    RecordTraceLocked(
                        $"far-action type={(uint)actionType} id={actionId} mode={(uint)mode} " +
                        $"age={Math.Max(0, Environment.TickCount64 - (farHelpToken.ExpiresAtMilliseconds - TokenLifetimeMilliseconds))}ms " +
                        $"result={(rewritten ? "redirect" : reason)}");
                }
            }
        }
        catch (Exception exception)
        {
            var failedNearHelp = handlingNearHelp;
            var failedFarHelp = handlingFarHelp;
            ArmedFarHelpTarget? pendingFarHelpToken;
            lock (tokenGate) pendingFarHelpToken = armedFarHelpTarget;
            if (!consumedFallbackCarrier && pendingFarHelpToken is { } pendingFar)
            {
                try
                {
                    var currentPlayer = objectTable.LocalPlayer;
                    var currentHardTargetId = IsLivePlayer(currentPlayer)
                        ? GetNativeHardTargetId(currentPlayer!)
                        : 0;
                    consumedFallbackCarrier = FarHelpCarrierRules.IsFallbackCarrier(
                        currentHardTargetId,
                        targetId,
                        pendingFar.CarrierGameObjectId,
                        pendingFar.CarrierEntityId);
                }
                catch
                {
                    // The recovery probe is best effort. Never let it prevent the
                    // detour's one mandatory Original call.
                }
            }

            forwardedTargetId = consumedFallbackCarrier ? InvalidCarrierTargetId : targetId;
            lock (tokenGate)
            {
                failedNearHelp |= armedHelpTarget is not null || nearHelpState.IsArmed;
                failedFarHelp |= armedFarHelpTarget is not null || farHelpState.IsArmed;
                armedTarget = null;
                oneShotState = NearAssistOneShotState.Initial;
                armedHelpTarget = null;
                nearHelpState = NearHelpOneShotState.Initial;
                armedFarHelpTarget = null;
                farHelpState = FarHelpOneShotState.Initial;
                if (failedFarHelp)
                {
                    farHelpFallbackCount++;
                    farHelpLastPartySlot = 0;
                    farHelpLastDistance = 0f;
                    farHelpLastEvent = consumedFallbackCarrier
                        ? "Redirect failed closed; carrier invalidated for <t> fallback"
                        : "Redirect failed closed; original target preserved";
                }
                else if (failedNearHelp)
                {
                    helpFallbackCount++;
                    helpLastEvent = consumedFallbackCarrier
                        ? "Redirect failed closed; carrier invalidated for <t> fallback"
                        : "Redirect failed closed; original target preserved";
                }
                else
                {
                    fallbackCount++;
                    lastEvent = consumedFallbackCarrier
                        ? "Redirect failed closed; carrier invalidated for <t> fallback"
                        : "Redirect failed closed; original target preserved";
                }
            }

            LogFailure(
                exception,
                failedFarHelp
                    ? "Seiton Sense Far Help redirect failed closed with its authored fallback policy."
                    : failedNearHelp
                        ? "Seiton Sense Near Help redirect failed closed with its authored fallback policy."
                        : "Seiton Sense Near Assist redirect failed closed with its authored fallback policy.");
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

    private ulong TryResolveHelpRedirect(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ActionManager.UseActionMode mode,
        ulong originalTargetId,
        ArmedNearHelpTarget token,
        NearHelpOneShotState previousState,
        bool isFallbackCarrier,
        out bool rewritten,
        out string reason)
    {
        var now = Environment.TickCount64;
        var localPlayer = objectTable.LocalPlayer;
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
        var friendlyAction = hasActionMetadata &&
                             (action.CanTargetParty || action.CanTargetAlly || action.CanTargetAlliance);
        var areaTargetedAction = hasActionMetadata && action.TargetArea;
        var supportedAction = IsSupportedActionType(actionType) &&
                              hasActionMetadata &&
                              action.IsPvP &&
                              friendlyAction &&
                              !areaTargetedAction &&
                              action.Range > 0;

        var candidates = new List<NearHelpSelectionCandidate>(8);
        if (supportedContext && supportedAction && actionManager != null && localIdentityValid)
        {
            var partySlots = GetPartySlots();
            var sourceObject = GetNativeObject(localPlayer!);
            if (sourceObject != null)
            {
                foreach (var ally in objectTable.PlayerObjects.OfType<IPlayerCharacter>())
                {
                    if (!IsLivePlayer(ally) ||
                        ally.GameObjectId == localPlayer!.GameObjectId ||
                        !partySlots.ContainsKey(ally.EntityId))
                    {
                        continue;
                    }

                    var targetObject = GetNativeObject(ally);
                    var distanceSquared = Vector3.DistanceSquared(localPlayer.Position, ally.Position);
                    var hasValidActionTarget = targetObject != null;
                    var rangeResult = hasValidActionTarget
                        ? ActionManager.GetActionInRangeOrLoS(resolvedActionId, sourceObject, targetObject)
                        : uint.MaxValue;
                    candidates.Add(new NearHelpSelectionCandidate(
                        ally.GameObjectId,
                        ally.EntityId,
                        partySlots.GetValueOrDefault(ally.EntityId),
                        ally.CurrentHp,
                        ally.MaxHp,
                        distanceSquared,
                        IsExactFriendly: true,
                        IsSelf: false,
                        hasValidActionTarget,
                        SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult)));
                }
            }
        }

        var attempt = new NearHelpActionAttempt(
            originalTargetId,
            now,
            IsEligibleMacroActionAttempt: true,
            supportedContext,
            supportedAction,
            supportedMode,
            friendlyAction,
            areaTargetedAction,
            IsFallbackCarrier: isFallbackCarrier);
        var decision = NearHelpOneShotRules.Observe(previousState, attempt, candidates);

        lock (tokenGate) nearHelpState = decision.NextState;
        rewritten = decision.ShouldRewrite;
        if (rewritten && decision.SelectedCandidateIndex >= 0)
        {
            var selected = candidates[decision.SelectedCandidateIndex];
            reason = $"Redirected lowest HP ally {selected.CurrentHp}/{selected.MaximumHp}, " +
                     $"distance={MathF.Sqrt(selected.DistanceSquared):0.0}y";
        }
        else
        {
            reason = $"Fallback: {decision.Reason}, candidates={candidates.Count}, resolved={resolvedActionId}";
        }

        return decision.ForwardTargetId;
    }

    private ulong TryResolveFarHelpRedirect(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ActionManager.UseActionMode mode,
        ulong originalTargetId,
        ArmedFarHelpTarget token,
        FarHelpOneShotState previousState,
        bool isFallbackCarrier,
        out bool rewritten,
        out int selectedPartySlot,
        out float selectedDistance,
        out string reason)
    {
        var now = Environment.TickCount64;
        var localPlayer = objectTable.LocalPlayer;
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
        var hasExactMovementDefinition =
            TryGetFarHelpMovementDefinition(resolvedActionId, out var expectedJobId, out var maximumDistance);
        var friendlyAction = hasActionMetadata &&
                             (action.CanTargetParty || action.CanTargetAlly || action.CanTargetAlliance);
        var movementAction = hasActionMetadata && action.AffectsPosition;
        var areaTargetedAction = hasActionMetadata && action.TargetArea;
        var supportedAction = IsSupportedActionType(actionType) &&
                              hasActionMetadata &&
                              hasExactMovementDefinition &&
                              action.IsPvP &&
                              !action.CanTargetSelf &&
                              action.Range > 0 &&
                              action.RequiresLineOfSight &&
                              action.ClassJob.RowId == expectedJobId;

        var candidates = new List<FarHelpSelectionCandidate>(8);
        if (supportedContext &&
            supportedAction &&
            movementAction &&
            friendlyAction &&
            !areaTargetedAction &&
            actionManager != null &&
            localIdentityValid)
        {
            var sourceObject = GetNativeObject(localPlayer!);
            var seenEntityIds = new HashSet<uint>();
            if (sourceObject != null)
            {
                for (var slot = FarHelpSelectionRules.FirstPartySlot;
                     slot <= FarHelpSelectionRules.LastPartySlot;
                     slot++)
                {
                    var ally = PartySlotResolver.Resolve(objectTable, slot);
                    if (!IsLivePlayer(ally) ||
                        ally!.GameObjectId == localPlayer!.GameObjectId ||
                        !seenEntityIds.Add(ally.EntityId))
                    {
                        continue;
                    }

                    var targetObject = GetNativeObject(ally);
                    var distanceSquared = Vector3.DistanceSquared(localPlayer.Position, ally.Position);
                    var hasValidActionTarget = targetObject != null;
                    var rangeResult = hasValidActionTarget
                        ? ActionManager.GetActionInRangeOrLoS(resolvedActionId, sourceObject, targetObject)
                        : uint.MaxValue;
                    var insideActionSpecificLimit =
                        !float.IsFinite(maximumDistance) ||
                        (float.IsFinite(distanceSquared) &&
                         distanceSquared < maximumDistance * maximumDistance);
                    var jobId = ally.ClassJob.IsValid ? ally.ClassJob.RowId : 0;
                    candidates.Add(new FarHelpSelectionCandidate(
                        ally.GameObjectId,
                        ally.EntityId,
                        slot,
                        ally.CurrentHp,
                        ally.MaxHp,
                        distanceSquared,
                        FarHelpSelectionRules.ClassifyPlayableJob(jobId),
                        IsExactPartyMember: true,
                        IsSelf: false,
                        ally.IsTargetable,
                        hasValidActionTarget,
                        insideActionSpecificLimit &&
                        SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult)));
                }
            }
        }

        var attempt = new FarHelpActionAttempt(
            originalTargetId,
            now,
            IsEligibleMacroActionAttempt: true,
            supportedContext,
            supportedAction,
            movementAction,
            supportedMode,
            friendlyAction,
            areaTargetedAction,
            IsFallbackCarrier: isFallbackCarrier);
        var decision = FarHelpOneShotRules.Observe(previousState, attempt, candidates);

        // Commit consumption before Original: a rejection or exception cannot move
        // this single intent to a later action or macro press.
        lock (tokenGate) farHelpState = decision.NextState;
        rewritten = decision.ShouldRewrite;
        selectedPartySlot = 0;
        selectedDistance = 0f;
        if (rewritten && decision.SelectedCandidateIndex >= 0)
        {
            var selected = candidates[decision.SelectedCandidateIndex];
            selectedPartySlot = selected.PartySlot;
            selectedDistance = MathF.Sqrt(selected.DistanceSquared);
            reason = $"Redirected farthest preferred ally P{selected.PartySlot}, " +
                     $"distance={selectedDistance:0.0}y, role={selected.Role}";
        }
        else
        {
            reason = $"Fallback: {decision.Reason}, candidates={candidates.Count}, resolved={resolvedActionId}";
        }

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
            if (armedHelpTarget is { } helpToken)
            {
                shouldClear |= helpToken.ExpiresAtMilliseconds <= Environment.TickCount64;
            }
            if (armedFarHelpTarget is { } farHelpToken)
            {
                shouldClear |= farHelpToken.ExpiresAtMilliseconds <= Environment.TickCount64;
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

    private Dictionary<uint, int> GetPartySlots()
    {
        var result = new Dictionary<uint, int>(8);
        for (var slot = NearHelpSelectionRules.FirstPartySlot;
             slot <= NearHelpSelectionRules.LastPartySlot;
             slot++)
        {
            var player = PartySlotResolver.Resolve(objectTable, slot);
            if (IsLivePlayer(player)) result.TryAdd(player!.EntityId, slot);
        }

        return result;
    }

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

    private bool TryConsumeEligibleHelpToken(
        ActionType actionType,
        ActionManager.UseActionMode mode,
        ulong incomingTargetId,
        out ArmedNearHelpTarget token,
        out NearHelpOneShotState previousState,
        out bool fallbackCarrier)
    {
        lock (tokenGate)
        {
            if (armedHelpTarget is not { } candidate ||
                !nearHelpState.IsArmed ||
                !IsPotentialMacroAction(actionType, mode))
            {
                token = default;
                previousState = NearHelpOneShotState.Initial;
                fallbackCarrier = false;
                return false;
            }

            token = candidate;
            previousState = nearHelpState;
            var currentPlayer = objectTable.LocalPlayer;
            var currentHardTargetId = IsLivePlayer(currentPlayer)
                ? GetNativeHardTargetId(currentPlayer!)
                : 0;
            fallbackCarrier = NearHelpCarrierRules.IsFallbackCarrier(
                currentHardTargetId,
                incomingTargetId,
                candidate.CarrierGameObjectId,
                candidate.CarrierEntityId);
            armedHelpTarget = null;
            nearHelpState = NearHelpOneShotState.Initial;
            helpLastEvent = $"Consumed target={incomingTargetId:X}; validating";
            return true;
        }
    }

    private bool TryConsumeEligibleFarHelpToken(
        ActionType actionType,
        ActionManager.UseActionMode mode,
        ulong incomingTargetId,
        out ArmedFarHelpTarget token,
        out FarHelpOneShotState previousState,
        out bool fallbackCarrier)
    {
        lock (tokenGate)
        {
            if (armedFarHelpTarget is not { } candidate ||
                !farHelpState.IsArmed ||
                !IsPotentialMacroAction(actionType, mode))
            {
                token = default;
                previousState = FarHelpOneShotState.Initial;
                fallbackCarrier = false;
                return false;
            }

            token = candidate;
            previousState = farHelpState;
            var currentPlayer = objectTable.LocalPlayer;
            var currentHardTargetId = IsLivePlayer(currentPlayer)
                ? GetNativeHardTargetId(currentPlayer!)
                : 0;
            fallbackCarrier = FarHelpCarrierRules.IsFallbackCarrier(
                currentHardTargetId,
                incomingTargetId,
                candidate.CarrierGameObjectId,
                candidate.CarrierEntityId);
            armedFarHelpTarget = null;
            farHelpState = FarHelpOneShotState.Initial;
            farHelpLastEvent = $"Consumed target={incomingTargetId:X}; validating";
            return true;
        }
    }

    private void ClearToken(string reason)
    {
        lock (tokenGate)
        {
            var hadToken = armedTarget is not null || oneShotState.IsArmed ||
                           armedHelpTarget is not null || nearHelpState.IsArmed ||
                           armedFarHelpTarget is not null || farHelpState.IsArmed;
            armedTarget = null;
            oneShotState = NearAssistOneShotState.Initial;
            armedHelpTarget = null;
            nearHelpState = NearHelpOneShotState.Initial;
            armedFarHelpTarget = null;
            farHelpState = FarHelpOneShotState.Initial;
            lastEvent = reason;
            helpLastEvent = reason;
            farHelpLastEvent = reason;
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

    private NearHelpArmResult HelpArmFailure(NearHelpArmOutcome outcome, string reason)
    {
        lock (tokenGate)
        {
            helpLastEvent = reason;
            RecordTraceLocked($"help-arm-fail {outcome}: {reason}");
        }

        return new NearHelpArmResult(outcome);
    }

    private FarHelpArmResult FarHelpArmFailure(FarHelpArmOutcome outcome, string reason)
    {
        lock (tokenGate)
        {
            farHelpLastEvent = reason;
            RecordTraceLocked($"far-arm-fail {outcome}: {reason}");
        }

        return new FarHelpArmResult(outcome);
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

    private bool IsEligibleHelpAction(
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
               (action.CanTargetParty || action.CanTargetAlly || action.CanTargetAlliance) &&
               !action.TargetArea &&
               action.Range > 0;
    }

    private bool IsEligibleFarHelpAction(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ActionManager.UseActionMode mode)
    {
        if (!IsPotentialMacroAction(actionType, mode)) return false;

        var resolvedActionId = ResolveActionId(actionManager, actionType, actionId);
        // Consume only an exact known movement-action intent. Its complete current
        // metadata is deliberately revalidated after consumption so metadata drift
        // fails the deliberate <2> carrier closed instead of executing it vanilla.
        return resolvedActionId != 0 &&
               TryGetFarHelpMovementDefinition(resolvedActionId, out _, out _);
    }

    private static bool TryGetFarHelpMovementDefinition(
        uint actionId,
        out uint expectedJobId,
        out float maximumDistance)
    {
        // Exact current PvP movement-to-target family. Unknown future or
        // transformed actions wait without consuming the armed token.
        switch (actionId)
        {
            case 29066: // PLD Guardian. Official execution condition is within 10y.
                expectedJobId = 19;
                maximumDistance = 10f;
                return true;
            case 29261: // SGE Icarus
                expectedJobId = 40;
                maximumDistance = float.PositiveInfinity;
                return true;
            case 29484: // MNK Thunderclap
                expectedJobId = 20;
                maximumDistance = float.PositiveInfinity;
                return true;
            case 29660: // BLM Aetherial Manipulation
                expectedJobId = 25;
                maximumDistance = float.PositiveInfinity;
                return true;
            case 39184: // VPR Slither
                expectedJobId = 41;
                maximumDistance = float.PositiveInfinity;
                return true;
            default:
                expectedJobId = 0;
                maximumDistance = 0f;
                return false;
        }
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

    private readonly record struct ArmedNearHelpTarget(
        uint TerritoryId,
        uint LocalEntityId,
        ulong LocalGameObjectId,
        uint CarrierEntityId,
        ulong CarrierGameObjectId,
        long ExpiresAtMilliseconds);

    private readonly record struct ArmedFarHelpTarget(
        uint TerritoryId,
        uint LocalEntityId,
        ulong LocalGameObjectId,
        uint CarrierEntityId,
        ulong CarrierGameObjectId,
        long ExpiresAtMilliseconds);
}
