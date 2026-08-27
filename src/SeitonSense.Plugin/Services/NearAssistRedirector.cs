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

internal readonly record struct SmartTargetDiagnostics(
    bool Armed,
    long RemainingMilliseconds,
    long ArmedCount,
    long RedirectedCount,
    long FallbackCount,
    int LastEnemySlot,
    string LastEvent);

internal enum SmartActionSafetyInspectionOutcome : byte
{
    NotApplicable = 0,
    Safe = 1,
    Unsafe = 2,
}

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

internal readonly record struct LocalGuardActionAttempt(
    uint TerritoryId,
    ulong LocalGameObjectId,
    uint LocalEntityId,
    long ObservedAtMilliseconds,
    long Generation);

internal readonly record struct AutoGuardProtectionDiagnostics(
    bool HookAvailable,
    bool Armed,
    bool ExactGuardObserved,
    long RemainingMilliseconds,
    long ArmedCount,
    long BlockedActionCount,
    long ReleasedCount,
    string LastEvent);

/// <summary>
/// Exact immutable identity of one generic-buffer replay. The protection flag
/// is carried only when the original physical call was owned by Smart Action or
/// its exact fallback safety path.
/// </summary>
internal readonly record struct IntegratedBufferedReplayIntent(
    ActionType ActionType,
    uint RequestedActionId,
    uint ResolvedActionId,
    ulong TargetId,
    bool RequiresSmartActionProtectionRecheck)
{
    internal bool IsValid =>
        ActionType is ActionType.Action or ActionType.PvPAction &&
        RequestedActionId != 0 &&
        ResolvedActionId != 0 &&
        (!RequiresSmartActionProtectionRecheck ||
         TargetId is not (0 or 0xE0000000));
}

/// <summary>
/// Owns mutually exclusive, short-lived target redirects selected by the /nearassist,
/// /smartaction, /nearhelp, and /farhelp macro lines.
/// It never mutates the game's hard, soft, or focus target and never dispatches an action.
/// The next bounded supported action is forwarded to the native function exactly once, with
/// either the revalidated canonical enemy ID, the caller's original target ID unchanged,
/// or an invalid ID for a failed deliberate carrier. Near Assist/Near Help may
/// then reach their authored fallback; Far Help deliberately never does.
/// </summary>
internal sealed unsafe class NearAssistRedirector : IDisposable
{
    [ThreadStatic]
    private static int internalRedirectBypassDepth;

    [ThreadStatic]
    private static int integratedBufferReplayDepth;

    [ThreadStatic]
    private static IntegratedBufferedReplayScope? integratedBufferedReplayScope;

    [ThreadStatic]
    private static int explicitAutoGuardBreakBypassDepth;

    [ThreadStatic]
    private static PredictiveCcBrakeBypassScope? predictiveCcBrakeBypassScope;

    [ThreadStatic]
    private static AstrologianOwnGuardVetoScope? astrologianOwnGuardVetoScope;

    internal const int TokenLifetimeMilliseconds = 750;
    internal const float MinimumAllyDistance = 5f;
    internal const float MaximumAllyDistance = 30f;
    private const long MaximumSmartTargetPressureAgeMilliseconds = 250;

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
    private readonly ExecuteTracker executeTracker;
    private readonly SmartWardensPaeanService smartWardensPaean;
    private readonly CcImmunityBrakeService ccImmunityBrake;
    private readonly bool smartActionProtectionMetadataVerified;
    private readonly SmartActionGuardBypassCatalog smartActionGuardBypassActions;
    private readonly bool chitenMetadataVerified;
    private readonly object tokenGate = new();
    private readonly object guardAttemptGate = new();
    private readonly object smartKardiaTriggerGate = new();
    private readonly Queue<string> recentTrace = new();
    private readonly Hook<ActionManager.Delegates.UseAction>? useActionHook;
    private readonly Hook<ActionManager.Delegates.UseActionLocation>? useActionLocationHook;
    private IntegratedInputRuntime? integratedInputRuntime;

    private ArmedNearAssistTarget? armedTarget;
    private NearAssistOneShotState oneShotState = NearAssistOneShotState.Initial;
    private ArmedSmartTarget? armedSmartTarget;
    private SmartActionSafetyLeaseState smartActionSafetyLeaseState =
        SmartActionSafetyLeaseState.Initial;
    private ArmedNearHelpTarget? armedHelpTarget;
    private NearHelpOneShotState nearHelpState = NearHelpOneShotState.Initial;
    private ArmedFarHelpTarget? armedFarHelpTarget;
    private FarHelpOneShotState farHelpState = FarHelpOneShotState.Initial;
    private FarHelpFallbackSuppressionState farHelpFallbackSuppressionState =
        FarHelpFallbackSuppressionState.Initial;
    private LocalGuardActionAttempt? latestLocalGuardActionAttempt;
    private long localGuardActionAttemptGeneration;
    private AutoGuardProtectionState autoGuardProtectionState = AutoGuardProtectionState.Initial;
    private long autoGuardProtectionArmedCount;
    private long autoGuardProtectionBlockedActionCount;
    private long autoGuardProtectionReleasedCount;
    private string autoGuardProtectionLastEvent = "Not armed";
    private SmartKardiaEukrasiaTrigger? pendingSmartKardiaTrigger;
    private long smartKardiaTriggerSequence;
    private uint observedTerritory;
    private long armedCount;
    private long redirectedCount;
    private long fallbackCount;
    private long smartTargetArmedCount;
    private long smartTargetRedirectedCount;
    private long smartTargetFallbackCount;
    private int smartTargetLastEnemySlot;
    private string smartTargetLastEvent = "Not started";
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
        ExecuteTracker executeTracker,
        SmartWardensPaeanService smartWardensPaean,
        CcImmunityBrakeService ccImmunityBrake,
        bool smartActionProtectionMetadataVerified,
        SmartActionGuardBypassCatalog smartActionGuardBypassActions,
        bool chitenMetadataVerified,
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
        this.executeTracker = executeTracker;
        this.smartWardensPaean = smartWardensPaean;
        this.ccImmunityBrake = ccImmunityBrake;
        this.smartActionProtectionMetadataVerified = smartActionProtectionMetadataVerified;
        this.smartActionGuardBypassActions = smartActionGuardBypassActions;
        this.chitenMetadataVerified = chitenMetadataVerified;
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

        try
        {
            useActionLocationHook =
                interop.HookFromAddress<ActionManager.Delegates.UseActionLocation>(
                    ActionManager.MemberFunctionPointers.UseActionLocation,
                    UseActionLocationDetour);
        }
        catch (Exception exception)
        {
            autoGuardProtectionLastEvent = "Native location-action hook unavailable";
            LogFailure(
                exception,
                "Seiton Sense automatic Guard location-action hook is unavailable; Auto-Guard remains fail-closed.");
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

    internal CcImmunityBrakeDiagnostics CcBrakeDiagnostics => ccImmunityBrake.Diagnostics;
    internal SmartWardensPaeanDiagnostics SmartWardensPaeanDiagnostics =>
        smartWardensPaean.Diagnostics;
    internal IReadOnlySet<uint> VerifiedCcBrakeStatusIds => ccImmunityBrake.VerifiedStatusIds;
    internal IReadOnlySet<uint> VerifiedCcBrakeActionIds => ccImmunityBrake.VerifiedActionIds;

    internal bool TryGetRecentExactLocalGuardAttempt(
        uint territoryId,
        ulong localGameObjectId,
        uint localEntityId,
        long nowMilliseconds,
        long maximumAgeMilliseconds,
        out long observedAtMilliseconds)
    {
        observedAtMilliseconds = -1;
        if (nowMilliseconds < 0 ||
            maximumAgeMilliseconds <= 0 ||
            !IsNetworkObjectId(localGameObjectId) ||
            !IsNetworkEntityId(localEntityId))
        {
            return false;
        }

        lock (guardAttemptGate)
        {
            if (latestLocalGuardActionAttempt is not { } attempt ||
                attempt.TerritoryId != territoryId ||
                attempt.LocalGameObjectId != localGameObjectId ||
                attempt.LocalEntityId != localEntityId ||
                attempt.ObservedAtMilliseconds < 0 ||
                attempt.ObservedAtMilliseconds > nowMilliseconds ||
                nowMilliseconds - attempt.ObservedAtMilliseconds >= maximumAgeMilliseconds)
            {
                return false;
            }

            observedAtMilliseconds = attempt.ObservedAtMilliseconds;
            return true;
        }
    }

    internal SmartTargetDiagnostics SmartTargetDiagnostics
    {
        get
        {
            lock (tokenGate)
            {
                var now = Environment.TickCount64;
                var token = armedSmartTarget;
                var remaining = token is null
                    ? 0
                    : Math.Max(0, token.Value.ExpiresAtMilliseconds - now);
                return new SmartTargetDiagnostics(
                    token is not null && remaining > 0,
                    remaining,
                    smartTargetArmedCount,
                    smartTargetRedirectedCount,
                    smartTargetFallbackCount,
                    smartTargetLastEnemySlot,
                    smartTargetLastEvent);
            }
        }
    }

    internal long CaptureLocalGuardAttemptGeneration()
    {
        lock (guardAttemptGate) return localGuardActionAttemptGeneration;
    }

    internal bool CanProtectAutomaticGuard =>
        !disposed &&
        started &&
        useActionHook?.IsEnabled == true &&
        useActionLocationHook?.IsEnabled == true;

    internal AutoGuardProtectionDiagnostics AutoGuardProtectionDiagnostics
    {
        get
        {
            lock (guardAttemptGate)
            {
                var now = Environment.TickCount64;
                var state = autoGuardProtectionState;
                var deadline = !state.IsArmed
                    ? -1
                    : state.ExactGuardObserved
                        ? state.MaximumExpiresAtMilliseconds
                        : Math.Min(
                            state.MaximumExpiresAtMilliseconds,
                            state.AcceptedAtMilliseconds +
                            AutoGuardProtectionRules.StatusPropagationMilliseconds);
                return new AutoGuardProtectionDiagnostics(
                    CanProtectAutomaticGuard,
                    state.IsArmed && deadline > now,
                    state.ExactGuardObserved,
                    state.IsArmed ? Math.Max(0, deadline - now) : 0,
                    autoGuardProtectionArmedCount,
                    autoGuardProtectionBlockedActionCount,
                    autoGuardProtectionReleasedCount,
                    autoGuardProtectionLastEvent);
            }
        }
    }

    /// <summary>
    /// Retracts only the exact local Guard observation synchronously created by
    /// the immediately completed client-rejected call. A true return,
    /// exception/ambiguity, identity drift, or any intervening Guard call keeps
    /// the propagation observation intact.
    /// </summary>
    internal bool TryRetractClientRejectedLocalGuardAttempt(
        ulong localGameObjectId,
        uint localEntityId,
        long generationBeforeCall)
    {
        if (!IsNetworkObjectId(localGameObjectId) ||
            !IsNetworkEntityId(localEntityId) ||
            generationBeforeCall < 0)
        {
            return false;
        }

        lock (guardAttemptGate)
        {
            if (latestLocalGuardActionAttempt is not { } attempt ||
                !DefensiveUtilityRules.CanRetractRejectedGuardAttempt(
                    attempt.Generation,
                    generationBeforeCall,
                    clientExplicitlyRejected: true,
                    acceptanceAmbiguous: false,
                    identityMatches:
                        attempt.LocalGameObjectId == localGameObjectId &&
                        attempt.LocalEntityId == localEntityId))
            {
                return false;
            }

            latestLocalGuardActionAttempt = null;
            return true;
        }
    }

    /// <summary>
    /// Runs one plugin-owned exact-target action through the existing hook without
    /// consuming or rewriting an armed macro token. The detour still reaches its
    /// single Original call with every incoming argument unchanged. A caller may
    /// additionally scope one already validated protection-end prediction to one
    /// exact action and target; nested or mismatched calls keep the normal brake.
    /// </summary>
    internal T RunWithoutRedirect<T>(
        Func<T> action,
        PredictiveCcBrakeBypassIntent? predictiveCcBrakeBypass = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var previousPredictiveScope = predictiveCcBrakeBypassScope;
        predictiveCcBrakeBypassScope =
            previousPredictiveScope is null &&
            predictiveCcBrakeBypass is { } intent &&
            PredictiveCcBrakeBypassRules.IsValidIntent(intent)
                ? new PredictiveCcBrakeBypassScope(this, intent)
                : null;
        internalRedirectBypassDepth++;
        try
        {
            return action();
        }
        finally
        {
            internalRedirectBypassDepth--;
            predictiveCcBrakeBypassScope = previousPredictiveScope;
        }
    }

    /// <summary>
    /// Runs exactly one authored AST Harmonic Orbis action through the ordinary
    /// redirect bypass while retaining a final hook-boundary veto for the exact
    /// local actor's active or propagating Guard. No other bypass caller opts in
    /// to this scope, and an action/target mismatch fails closed.
    /// </summary>
    internal bool RunAstrologianHarmonicOrbisWithoutRedirect(
        uint actionId,
        TargetPressureActorIdentity localPlayer,
        ulong targetGameObjectId,
        Func<bool> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (useActionHook is null ||
            !useActionHook.IsEnabled ||
            astrologianOwnGuardVetoScope is not null ||
            AstrologianHarmonicOrbisRules.ShouldVetoNativeBoundaryForOwnGuard(
                actionId,
                localPlayer,
                localPlayer,
                targetGameObjectId,
                targetGameObjectId,
                ownGuardActiveOrPropagating: false))
        {
            return false;
        }

        astrologianOwnGuardVetoScope = new AstrologianOwnGuardVetoScope(
            this,
            actionId,
            localPlayer,
            targetGameObjectId);
        try
        {
            return RunWithoutRedirect(action);
        }
        finally
        {
            astrologianOwnGuardVetoScope = null;
        }
    }

    /// <summary>
    /// Replays one already-frozen generic-buffer tuple without consuming or
    /// rewriting any assist token. Unlike helper-owned calls, this remains an
    /// authored physical action for passive accepted-action observers such as
    /// Smart Kardia. The caller must provide the exact previously validated
    /// action and post-Smart-Action target tuple.
    /// </summary>
    internal T RunExactBufferedReplay<T>(
        IntegratedBufferedReplayIntent intent,
        Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!intent.IsValid || integratedBufferedReplayScope is not null)
            return default!;

        var previousScope = integratedBufferedReplayScope;
        integratedBufferedReplayScope = new IntegratedBufferedReplayScope(this, intent);
        integratedBufferReplayDepth++;
        internalRedirectBypassDepth++;
        try
        {
            return action();
        }
        finally
        {
            internalRedirectBypassDepth--;
            integratedBufferReplayDepth--;
            integratedBufferedReplayScope = previousScope;
        }
    }

    /// <summary>
    /// Connects standard-hotbar provenance without adding another UseAction
    /// hook. The runtime is queried only at this class's sole final boundary.
    /// </summary>
    internal void AttachIntegratedInputRuntime(IntegratedInputRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (disposed) throw new ObjectDisposedException(nameof(NearAssistRedirector));
        integratedInputRuntime = runtime;
    }

    internal bool TryPeekSmartKardiaTrigger(
        long nowMilliseconds,
        uint territoryId,
        TargetPressureActorIdentity localPlayer,
        out SmartKardiaEukrasiaTrigger trigger)
    {
        lock (smartKardiaTriggerGate)
        {
            if (pendingSmartKardiaTrigger is { } pending &&
                SmartKardiaRules.IsTriggerCurrent(
                    pending,
                    nowMilliseconds,
                    territoryId,
                    localPlayer))
            {
                trigger = pending;
                return true;
            }

            pendingSmartKardiaTrigger = null;
            trigger = default;
            return false;
        }
    }

    /// <summary>
    /// Spends only the exact frozen Eukrasia opportunity. Consumption happens
    /// before terminal Kardia validation and remains terminal on any later drift.
    /// </summary>
    internal bool TryConsumeSmartKardiaTrigger(long token)
    {
        if (token <= 0) return false;
        var acceptedAtMilliseconds = -1L;
        lock (smartKardiaTriggerGate)
        {
            if (pendingSmartKardiaTrigger is not { } pending ||
                pending.Token != token)
            {
                return false;
            }

            acceptedAtMilliseconds = pending.AcceptedAtMilliseconds;
            pendingSmartKardiaTrigger = null;
        }

        pressureTracker.CancelIncomingAllyPressureCapture(
            acceptedAtMilliseconds);
        return true;
    }

    internal void ClearSmartKardiaTrigger()
    {
        var acceptedAtMilliseconds = -1L;
        lock (smartKardiaTriggerGate)
        {
            if (pendingSmartKardiaTrigger is { } pending)
                acceptedAtMilliseconds = pending.AcceptedAtMilliseconds;
            pendingSmartKardiaTrigger = null;
        }

        pressureTracker.CancelIncomingAllyPressureCapture(
            acceptedAtMilliseconds);
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


        try
        {
            useActionLocationHook?.Enable();
        }
        catch (Exception exception)
        {
            lock (guardAttemptGate)
                autoGuardProtectionLastEvent = "Location-action hook could not be enabled";
            LogFailure(
                exception,
                "Seiton Sense automatic Guard location-action hook could not be enabled; Auto-Guard remains fail-closed.");
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

    /// <summary>
    /// Arms cancellation protection only when the immediately preceding hook
    /// observation is the exact Guard call which the automatic helper has just
    /// proven client-accepted. Manual Guard and ambiguous acceptance never own it.
    /// </summary>
    internal bool TryArmAcceptedAutoGuardProtection(
        ulong localGameObjectId,
        uint localEntityId,
        long generationBeforeCall)
    {
        var now = Environment.TickCount64;
        var currentLocal = new TargetPressureActorIdentity(localGameObjectId, localEntityId);
        lock (guardAttemptGate)
        {
            if (latestLocalGuardActionAttempt is not { } attempt ||
                !AutoGuardProtectionRules.CanArmFromAcceptedAttempt(
                    attempt.Generation,
                    generationBeforeCall,
                    attempt.TerritoryId,
                    clientState.TerritoryType,
                    new TargetPressureActorIdentity(
                        attempt.LocalGameObjectId,
                        attempt.LocalEntityId),
                    currentLocal,
                    attempt.ObservedAtMilliseconds,
                    now))
            {
                autoGuardProtectionLastEvent =
                    "Accepted automatic Guard had no exact hook-owned attempt";
                return false;
            }

            var armed = AutoGuardProtectionRules.Arm(
                attempt.Generation,
                attempt.TerritoryId,
                currentLocal,
                now);
            if (!armed.IsArmed)
            {
                autoGuardProtectionLastEvent = "Automatic Guard ownership failed closed";
                return false;
            }

            autoGuardProtectionState = armed;
            autoGuardProtectionArmedCount++;
            autoGuardProtectionLastEvent =
                $"Protected accepted automatic Guard generation {attempt.Generation}";
            return true;
        }
    }

    /// <summary>
    /// Preserves the documented /panicshu contract: this explicit command may
    /// deliberately break even an automatically owned Guard. The scope stays
    /// separate from the generic redirect bypass so no other location action
    /// inherits the override.
    /// </summary>
    internal IDisposable EnterExplicitAutoGuardBreak()
    {
        explicitAutoGuardBreakBypassDepth++;
        return new ExplicitAutoGuardBreakScope();
    }

    internal NearAssistArmResult ArmSmartActionTarget()
    {
        ClearToken("Replaced or cleared by a new Smart Action arm request");

        if (disposed || !started || !configuration.Enabled || !configuration.EnableSmartActionMacro)
            return SmartTargetArmFailure(NearAssistArmOutcome.Disabled, "Smart Action arm ignored: feature disabled");
        if (useActionHook is null || !useActionHook.IsEnabled)
            return SmartTargetArmFailure(NearAssistArmOutcome.HookUnavailable, "Smart Action arm ignored: hook unavailable");
        if (!smartActionProtectionMetadataVerified)
            return SmartTargetArmFailure(
                NearAssistArmOutcome.FailedClosed,
                "Smart Action arm failed closed: protection metadata unverified");
        var context = ResolveContext();
        if (context != SupportedPvPContext.CrystallineConflict)
            return SmartTargetArmFailure(
                NearAssistArmOutcome.NotCrystallineConflict,
                "Smart Action arm ignored: not in Crystalline Conflict");

        var localPlayer = objectTable.LocalPlayer;
        if (!IsLivePlayer(localPlayer))
            return SmartTargetArmFailure(
                NearAssistArmOutcome.LocalPlayerUnavailable,
                "Smart Action arm ignored: local player unavailable");

        try
        {
            var partyEntityIds = GetPartyEntityIds();
            var canonicalEnemies = ResolveCanonicalEnemies(localPlayer!, partyEntityIds);
            CanonicalEnemy? carrier = null;
            foreach (var enemy in canonicalEnemies.Values)
            {
                if (enemy.Slot != EnemySlotRules.FirstSlot) continue;
                carrier = enemy;
                break;
            }

            if (carrier is not { } exactCarrier)
            {
                return SmartTargetArmFailure(
                    NearAssistArmOutcome.NoCanonicalEnemySlots,
                    "Smart Action failed closed: exact S1 carrier unavailable");
            }

            var now = Environment.TickCount64;
            var token = new ArmedSmartTarget(
                clientState.TerritoryType,
                localPlayer!.EntityId,
                localPlayer.GameObjectId,
                exactCarrier.Player.EntityId,
                exactCarrier.Player.GameObjectId,
                now + TokenLifetimeMilliseconds);
            lock (tokenGate)
            {
                armedSmartTarget = token;
                smartTargetArmedCount++;
                smartTargetLastEnemySlot = 0;
                smartTargetLastEvent = "Armed; waiting for exact harmful action";
                RecordTraceLocked($"smart-arm ctx={context} carrier=S1");
            }

            return new NearAssistArmResult(NearAssistArmOutcome.Armed, 0, 0f);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Seiton Sense Smart Action arm failed closed.");
            return SmartTargetArmFailure(
                NearAssistArmOutcome.FailedClosed,
                "Smart Action arm failed closed: canonical scan failed");
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
            var now = Environment.TickCount64;
            var nextState = FarHelpOneShotRules.Arm(now, TokenLifetimeMilliseconds);
            if (!nextState.IsArmed)
                return FarHelpArmFailure(FarHelpArmOutcome.FailedClosed, "Far Help arm failed closed: invalid state");

            var token = new ArmedFarHelpTarget(
                clientState.TerritoryType,
                local.EntityId,
                local.GameObjectId,
                local.EntityId,
                local.GameObjectId,
                now + TokenLifetimeMilliseconds);
            lock (tokenGate)
            {
                armedFarHelpTarget = token;
                farHelpState = nextState;
                farHelpArmedCount++;
                farHelpLastEvent = "Armed";
                RecordTraceLocked($"far-arm ctx={context} carrier=<me>");
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

    internal void Reset()
    {
        ClearToken("Reset");
        ClearSmartKardiaTrigger();
        ClearAutoGuardProtection("Reset");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started) framework.Update -= OnFrameworkUpdate;
        started = false;
        integratedInputRuntime = null;
        ClearToken("Disposed");
        ClearSmartKardiaTrigger();
        lock (guardAttemptGate)
        {
            latestLocalGuardActionAttempt = null;
            autoGuardProtectionState = AutoGuardProtectionState.Initial;
            autoGuardProtectionLastEvent = "Disposed";
        }
        useActionHook?.Dispose();
        useActionLocationHook?.Dispose();
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
        // This runs before any redirect/token work. A random action cannot both
        // cancel an automatically owned Guard and consume a macro one-shot.
        if (TryBlockOwnedAutoGuardCancellation(thisPtr, actionType, actionId))
            return false;

        var bypassRedirect = internalRedirectBypassDepth > 0;
        var inspectedSmartActionTargetId = targetId;
        var smartActionSafetyInspection = !bypassRedirect
            ? InspectSmartActionSafetyLease(
                thisPtr,
                actionType,
                actionId,
                targetId,
                mode,
                out inspectedSmartActionTargetId)
            : SmartActionSafetyInspectionOutcome.NotApplicable;
        if (smartActionSafetyInspection == SmartActionSafetyInspectionOutcome.Unsafe)
            return false;

        // Native zero/default carriers are mutable references to the selected
        // target. Once an exact Smart Action fallback is proven safe, freeze it
        // to that canonical enemy ID before any later hook can resolve another
        // actor through the carrier.
        var forwardedTargetId =
            smartActionSafetyInspection == SmartActionSafetyInspectionOutcome.Safe
                ? inspectedSmartActionTargetId
                : targetId;
        var consumedFallbackCarrier = false;
        var handlingSmartTarget = false;
        var handlingNearHelp = false;
        var handlingFarHelp = false;
        var suppressingLegacyFarHelpFallback = false;
        var suppressingSmartTargetCall = false;
        var targetSuppressedByRedirect = false;
        var helperTokenConsumed = false;
        ArmedSmartTarget? handlingSmartTargetToken = null;
        ArmedSmartTarget? potentialSmartTargetToken = null;
        if (!bypassRedirect && IsPotentialMacroAction(actionType, mode))
        {
            lock (tokenGate)
                potentialSmartTargetToken = armedSmartTarget;
        }
        var smartPaeanResult = SmartWardensPaeanInterceptResult.Vanilla(
            targetId,
            "Not evaluated");
        try
        {
            var smartTargetTokenConsumed = false;
            var smartTargetOwnershipChanged = false;
            var smartToken = default(ArmedSmartTarget);
            if (potentialSmartTargetToken is not null)
            {
                var smartTargetCallEligible = IsEligibleSmartActionRedirectAction(
                    thisPtr,
                    actionType,
                    actionId,
                    mode);
                if (!smartTargetCallEligible)
                {
                    // Exact metadata proved this is not a Smart Action call.
                    // Later unrelated helper failures must not claim its token.
                    potentialSmartTargetToken = null;
                }
                else
                {
                    smartTargetTokenConsumed = TryConsumeEligibleSmartTargetToken(
                        potentialSmartTargetToken.Value,
                        actionType,
                        mode,
                        targetId,
                        out smartToken,
                        out consumedFallbackCarrier,
                        out smartTargetOwnershipChanged);
                    potentialSmartTargetToken = smartTargetTokenConsumed
                        ? smartToken
                        : null;
                }
            }

            if (smartTargetOwnershipChanged)
            {
                // Never let an older in-flight call steal or pass through a
                // newer arm generation observed between the two token locks.
                forwardedTargetId = InvalidCarrierTargetId;
                targetSuppressedByRedirect = true;
                suppressingSmartTargetCall = true;
                helperTokenConsumed = true;
                lock (tokenGate)
                {
                    smartTargetLastEnemySlot = 0;
                    smartTargetLastEvent =
                        "Suppressed stale Smart Action call; newer token preserved";
                    RecordTraceLocked("smart-action stale generation suppressed");
                }
            }
            else if (!bypassRedirect &&
                TrySuppressLegacyFarHelpFallback(thisPtr, actionType, actionId, mode))
            {
                suppressingLegacyFarHelpFallback = true;
                forwardedTargetId = InvalidCarrierTargetId;
                targetSuppressedByRedirect = true;
                lock (tokenGate)
                {
                    farHelpFallbackCount++;
                    farHelpLastPartySlot = 0;
                    farHelpLastDistance = 0f;
                    farHelpLastEvent = "Suppressed legacy selected-target mobility fallback";
                    RecordTraceLocked(
                        $"far-legacy-suppress type={(uint)actionType} id={actionId} mode={(uint)mode}");
                }
            }
            else if (!bypassRedirect && smartTargetTokenConsumed)
            {
                helperTokenConsumed = true;
                handlingSmartTarget = true;
                handlingSmartTargetToken = smartToken;
                forwardedTargetId = TryResolveSmartTargetRedirect(
                    thisPtr,
                    actionType,
                    actionId,
                    mode,
                    targetId,
                    smartToken,
                    out var rewritten,
                    out var selectedSlot,
                    out var reason);
                if (!rewritten)
                {
                    forwardedTargetId = InvalidCarrierTargetId;
                    targetSuppressedByRedirect = true;
                    suppressingSmartTargetCall = true;
                }

                lock (tokenGate)
                {
                    if (rewritten)
                    {
                        smartTargetRedirectedCount++;
                        smartTargetLastEnemySlot = selectedSlot;
                    }
                    else
                    {
                        smartTargetFallbackCount++;
                        smartTargetLastEnemySlot = 0;
                    }

                    smartTargetLastEvent = reason;
                    RecordTraceLocked(
                        $"smart-action type={(uint)actionType} id={actionId} mode={(uint)mode} " +
                        $"result={(rewritten ? $"S{selectedSlot}" : reason)}");
                }
            }
            else if (!bypassRedirect &&
                IsEligibleRedirectAction(thisPtr, actionType, actionId, mode) &&
                TryConsumeEligibleToken(
                    actionType,
                    mode,
                    targetId,
                    out var token,
                out var previousOneShotState,
                out consumedFallbackCarrier))
            {
                helperTokenConsumed = true;
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
                {
                    forwardedTargetId = InvalidCarrierTargetId;
                    targetSuppressedByRedirect = true;
                }
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
                helperTokenConsumed = true;
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
                {
                    forwardedTargetId = InvalidCarrierTargetId;
                    targetSuppressedByRedirect = true;
                }
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
                     IsEligibleFarHelpAction(thisPtr, actionType, actionId, mode, out var resolvedFarHelpActionId) &&
                     TryConsumeEligibleFarHelpToken(
                         actionType,
                         mode,
                         targetId,
                         out var farHelpToken,
                         out var previousFarHelpState,
                         out consumedFallbackCarrier))
            {
                helperTokenConsumed = true;
                handlingFarHelp = true;
                ArmFarHelpFallbackSuppression(actionType, actionId, resolvedFarHelpActionId);
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
                // This exact reviewed movement action has already consumed the
                // Far Help intent and armed the legacy-call quarantine. Any
                // non-rewrite outcome, including the exact expiry boundary,
                // must stay inert instead of forwarding an authored carrier or
                // selected target.
                if (!rewritten)
                {
                    forwardedTargetId = InvalidCarrierTargetId;
                    targetSuppressedByRedirect = true;
                }
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

            // A normal Paean/Turbo call is reviewed only after every explicit
            // one-shot macro route had the opportunity to own it. Internal
            // plugin dispatches and consumed helper carriers remain untouched.
            if (!bypassRedirect &&
                !helperTokenConsumed &&
                !targetSuppressedByRedirect &&
                forwardedTargetId == targetId)
            {
                smartPaeanResult = smartWardensPaean.Evaluate(
                    thisPtr,
                    actionType,
                    actionId,
                    targetId,
                    mode,
                    IsLocalGuardActiveOrPropagating());
                if (smartPaeanResult.ShouldSuppress) return false;
                if (smartPaeanResult.ShouldRedirect)
                    forwardedTargetId = smartPaeanResult.ForwardTargetId;
            }
        }
        catch (Exception exception)
        {
            var failedSmartTarget = handlingSmartTarget;
            var failedSmartTargetToken = handlingSmartTargetToken;
            var failedNearHelp = handlingNearHelp;
            var failedFarHelp = handlingFarHelp || suppressingLegacyFarHelpFallback;
            ArmedSmartTarget? preservedSmartTargetToken = null;
            lock (tokenGate)
            {
                if (potentialSmartTargetToken is { } evaluatedSmartTarget)
                {
                    failedSmartTarget = true;
                    failedSmartTargetToken ??= evaluatedSmartTarget;
                }
                if (armedSmartTarget is { } currentSmartTarget &&
                    (failedSmartTargetToken is not { } failedToken ||
                     !currentSmartTarget.Equals(failedToken)))
                {
                    // A newer arm request must survive an older call's fault.
                    preservedSmartTargetToken = currentSmartTarget;
                }
                failedNearHelp |= armedHelpTarget is not null || nearHelpState.IsArmed;
                failedFarHelp |= armedFarHelpTarget is not null ||
                                 farHelpState.IsArmed ||
                                 farHelpFallbackSuppressionState.IsArmed;
            }

            if (failedFarHelp)
                EnsureFarHelpFallbackSuppressionAfterFailure(thisPtr, actionType, actionId);
            if (failedSmartTarget && preservedSmartTargetToken is null)
            {
                EnsureSmartActionSafetyLeaseAfterFailure(
                    thisPtr,
                    actionType,
                    actionId,
                    failedSmartTargetToken);
            }

            lock (tokenGate)
            {
                armedTarget = null;
                oneShotState = NearAssistOneShotState.Initial;
                armedSmartTarget = preservedSmartTargetToken;
                armedHelpTarget = null;
                nearHelpState = NearHelpOneShotState.Initial;
                armedFarHelpTarget = null;
                farHelpState = FarHelpOneShotState.Initial;
                if (failedSmartTarget)
                {
                    forwardedTargetId = InvalidCarrierTargetId;
                    targetSuppressedByRedirect = true;
                    suppressingSmartTargetCall = true;
                    smartTargetLastEnemySlot = 0;
                    smartTargetLastEvent =
                        "Redirect failed closed; Smart Action and its fallback are suppressed";
                }
                if (failedFarHelp)
                {
                    forwardedTargetId = InvalidCarrierTargetId;
                    targetSuppressedByRedirect = true;
                    farHelpFallbackCount++;
                    farHelpLastPartySlot = 0;
                    farHelpLastDistance = 0f;
                    farHelpLastEvent = "Redirect failed closed; movement target suppressed";
                }
                else if (failedNearHelp)
                {
                    forwardedTargetId = consumedFallbackCarrier ? InvalidCarrierTargetId : targetId;
                    targetSuppressedByRedirect = consumedFallbackCarrier;
                    helpFallbackCount++;
                    helpLastEvent = consumedFallbackCarrier
                        ? "Redirect failed closed; carrier invalidated for <t> fallback"
                        : "Redirect failed closed; original target preserved";
                }
                else if (!failedSmartTarget)
                {
                    forwardedTargetId = consumedFallbackCarrier ? InvalidCarrierTargetId : targetId;
                    targetSuppressedByRedirect = consumedFallbackCarrier;
                    fallbackCount++;
                    lastEvent = consumedFallbackCarrier
                        ? "Redirect failed closed; carrier invalidated for <t> fallback"
                        : "Redirect failed closed; original target preserved";
                }
            }

            LogFailure(
                exception,
                failedFarHelp
                    ? "Seiton Sense Far Help redirect failed closed without a selected-target fallback."
                    : failedNearHelp
                        ? "Seiton Sense Near Help redirect failed closed with its authored fallback policy."
                        : failedSmartTarget
                            ? "Seiton Sense Smart Action redirect failed closed and cleared its one-shot token."
                        : "Seiton Sense Near Assist redirect failed closed with its authored fallback policy.");
        }

        // The optional CC brake evaluates the final target after every redirect
        // decision. It never dispatches or stores work: a protected exact e1-e5
        // target stops only this one already incoming call before any downstream
        // hook or the game can restore a default/current target. ReAction or the
        // game may supply a later independent attempt after protection ends. Any
        // uncertainty or exception preserves the fully resolved target.
        // Plugin-owned exact-target actions bypass only the macro redirect
        // branches above. They must still pass through the same final CC brake
        // so a protection appearing between a helper's pre-check and UseAction
        // can stop the one incoming attempt.
        var predictiveCcBrakeScopeArmed =
            predictiveCcBrakeBypassScope is { } predictiveScope &&
            ReferenceEquals(predictiveScope.Owner, this);
        try
        {
            var resolvedActionId = ResolveActionId(thisPtr, actionType, actionId);
            var predictiveBypassIntent =
                TryConsumePredictiveCcBrakeBypass(
                actionType,
                actionId,
                resolvedActionId,
                targetId,
                forwardedTargetId,
                targetSuppressedByRedirect,
                mode);
            if (predictiveCcBrakeScopeArmed && predictiveBypassIntent is null)
            {
                // RunWithoutRedirect owns exactly one expected native call.
                // Adjusted-action or target drift inside that armed scope is
                // terminal; helper-only AoEs have no ordinary brake fallback.
                return false;
            }
            if (ccImmunityBrake.ShouldBlock(
                    actionType,
                    resolvedActionId,
                    targetId,
                    forwardedTargetId,
                    targetSuppressedByRedirect,
                    mode,
                    predictiveBypassIntent))
            {
                return false;
            }
        }
        catch (Exception exception)
        {
            ccImmunityBrake.RecordFailedOpen(exception);
            if (predictiveCcBrakeScopeArmed)
                return false;
        }

        // Target zero can mean the native selected-target carrier. A failed
        // Smart Action replacement therefore stops this call outright; only a
        // later independent authored <t> call may enter the exact safety lease.
        if (suppressingSmartTargetCall) return false;

        // This remains the detour's only textual Original call site. Every outer
        // pass/fail-open path executes it exactly once with every argument other
        // than an optional helper target substitution intact. A confirmed immunity
        // block returns above and deliberately executes no downstream/original call.
        var hasSmartKardiaPreflight = TryCaptureSmartKardiaEukrasiaPreflight(
            thisPtr,
            actionType,
            actionId,
            mode,
            targetId,
            forwardedTargetId,
            bypassRedirect,
            integratedBufferReplayDepth > 0,
            helperTokenConsumed,
            targetSuppressedByRedirect,
            out var smartKardiaPreflight);
        ObserveExactLocalGuardActivationAttempt(thisPtr, actionType, actionId);

        // Recheck the exact Smart Action replacement/fallback after every other
        // preflight, immediately before Original. This minimizes the client-side
        // window for Chiten, Guard, Cover, or an LB protection to appear.
        if (!bypassRedirect &&
            (handlingSmartTarget ||
             smartActionSafetyInspection == SmartActionSafetyInspectionOutcome.Safe))
        {
            smartActionSafetyInspection = InspectSmartActionSafetyLease(
                thisPtr,
                actionType,
                actionId,
                forwardedTargetId,
                mode,
                out var finalSmartActionTargetId);
            if (smartActionSafetyInspection != SmartActionSafetyInspectionOutcome.Safe)
                return false;

            // Forward only the canonical ID inspected at this final boundary.
            forwardedTargetId = finalSmartActionTargetId;
        }

        // A generic-buffer replay bypasses redirects and the short Smart Action
        // fallback lease, but never bypasses the protection policy owned by its
        // original physical call. Consume one exact requested/resolved/target
        // replay scope and, when required, rebuild the complete current
        // Chiten/Guard/Cover/invulnerability and targeted-circle snapshot here,
        // immediately before Original. No actor is reranked or substituted.
        if (integratedBufferReplayDepth > 0 &&
            !TryConsumeIntegratedBufferedReplay(
                thisPtr,
                actionType,
                actionId,
                forwardedTargetId,
                mode))
        {
            return false;
        }

        var integratedRuntime = integratedInputRuntime;
        var integratedAttempt = IntegratedActionBufferAttempt.None;
        if (mode == ActionManager.UseActionMode.None &&
            integratedRuntime?.TryGetActiveBufferRoot(
                actionType,
                actionId,
                out var integratedHotbarRoot) == true)
        {
            try
            {
                // The final post-SmartAction target is frozen here, directly
                // before this detour's sole native Original boundary.
                integratedAttempt = integratedRuntime.ActionBuffer.BeginExactStandardHotbarRoot(
                    thisPtr,
                    actionType,
                    actionId,
                    forwardedTargetId,
                    extraParam,
                    mode,
                    comboRouteId,
                    integratedHotbarRoot,
                    handlingSmartTarget ||
                    smartActionSafetyInspection == SmartActionSafetyInspectionOutcome.Safe);
            }
            catch (Exception exception)
            {
                // Buffer observation is optional. The physical action remains
                // authoritative and still crosses the original boundary.
                LogFailure(
                    exception,
                    "Seiton Sense integrated action-buffer preflight failed open for the physical action.");
                integratedAttempt = IntegratedActionBufferAttempt.None;
            }
        }

        if (ShouldVetoAstrologianOwnGuardAtFinalBoundary(
                actionType,
                actionId,
                forwardedTargetId,
                mode))
        {
            integratedRuntime?.ActionBuffer.AbandonExactStandardHotbarRoot(
                integratedAttempt,
                "AST own Guard became active or began propagating at the final native boundary");
            return false;
        }

        bool clientAccepted;
        try
        {
            clientAccepted = useActionHook!.Original(
                thisPtr,
                actionType,
                actionId,
                forwardedTargetId,
                extraParam,
                mode,
                comboRouteId,
                outOptAreaTargeted);
        }
        catch
        {
            integratedRuntime?.ActionBuffer.AbandonExactStandardHotbarRoot(
                integratedAttempt,
                "Native action boundary threw; buffer observation retired");
            throw;
        }

        if (integratedAttempt.Eligible && integratedRuntime is not null)
        {
            try
            {
                integratedRuntime.ActionBuffer.CompleteExactStandardHotbarRoot(
                    thisPtr,
                    integratedAttempt,
                    clientAccepted);
            }
            catch (Exception exception)
            {
                // Completion cannot change the already authoritative native
                // result or turn one physical action into an input error.
                integratedRuntime.ActionBuffer.AbandonExactStandardHotbarRoot(
                    integratedAttempt,
                    "Buffer completion failed closed");
                LogFailure(
                    exception,
                    "Seiton Sense integrated action-buffer completion failed closed.");
            }
        }
        if (clientAccepted &&
            (handlingSmartTarget ||
             smartActionSafetyInspection == SmartActionSafetyInspectionOutcome.Safe))
        {
            ClearSmartActionSafetyLease();
        }
        smartWardensPaean.RecordNativeResult(smartPaeanResult, clientAccepted);
        if (clientAccepted && hasSmartKardiaPreflight)
            ArmAcceptedSmartKardiaTrigger(smartKardiaPreflight);
        return clientAccepted;
    }

    private PredictiveCcBrakeBypassIntent? TryConsumePredictiveCcBrakeBypass(
        ActionType actionType,
        uint requestedActionId,
        uint resolvedActionId,
        ulong originalTargetId,
        ulong forwardedTargetId,
        bool targetSuppressedByRedirect,
        ActionManager.UseActionMode mode)
    {
        if (predictiveCcBrakeBypassScope is not { } scope ||
            !ReferenceEquals(scope.Owner, this) ||
            !PredictiveCcBrakeBypassRules.CanConsume(
                scope.Intent,
                scope.Consumed,
                requestedActionId,
                resolvedActionId,
                originalTargetId,
                forwardedTargetId,
                targetSuppressedByRedirect,
                actionType == ActionType.Action,
                mode == ActionManager.UseActionMode.None))
        {
            return null;
        }

        scope.Consumed = true;
        return scope.Intent;
    }

    private bool TryConsumeIntegratedBufferedReplay(
        ActionManager* actionManager,
        ActionType actionType,
        uint requestedActionId,
        ulong targetId,
        ActionManager.UseActionMode mode)
    {
        var scope = integratedBufferedReplayScope;
        if (integratedBufferReplayDepth != 1 ||
            scope is null ||
            !ReferenceEquals(scope.Owner, this) ||
            scope.Consumed)
        {
            SetSmartActionSafetyEvent(
                "Blocked generic buffer replay: exact replay scope was unavailable");
            return false;
        }

        scope.Consumed = true;
        try
        {
            var resolvedActionId = ResolveActionId(
                actionManager,
                actionType,
                requestedActionId);
            var intent = scope.Intent;
            if (mode != ActionManager.UseActionMode.None ||
                actionType != intent.ActionType ||
                requestedActionId != intent.RequestedActionId ||
                resolvedActionId != intent.ResolvedActionId ||
                targetId != intent.TargetId)
            {
                SetSmartActionSafetyEvent(
                    "Blocked generic buffer replay: frozen action or target drifted");
                return false;
            }

            if (!intent.RequiresSmartActionProtectionRecheck)
                return true;

            return IsExactBufferedSmartActionProtectionSafe(
                resolvedActionId,
                targetId);
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                "Seiton Sense exact generic-buffer replay inspection failed closed.");
            SetSmartActionSafetyEvent(
                "Blocked generic buffer replay: exact inspection failed");
            return false;
        }
    }

    private bool IsExactBufferedSmartActionProtectionSafe(
        uint resolvedActionId,
        ulong frozenTargetId)
    {
        var local = objectTable.LocalPlayer;
        if (!IsLivePlayer(local) ||
            !TryGetExactResolvedPvpActionMetadata(resolvedActionId, out var action) ||
            !action.CanTargetHostile ||
            action.TargetArea ||
            action.Range <= 0 ||
            !TryBuildSmartActionProtectionSnapshot(
                local!,
                GetPartyEntityIds(),
                out var canonicalEnemies,
                out var protectedActors))
        {
            SetSmartActionSafetyEvent(
                "Blocked generic buffer replay: protection snapshot was ambiguous");
            return false;
        }

        var exactMatches = canonicalEnemies
            .Where(enemy => enemy.Player.GameObjectId == frozenTargetId)
            .Take(2)
            .ToArray();
        if (exactMatches.Length != 1)
        {
            SetSmartActionSafetyEvent(
                "Blocked generic buffer replay: frozen target was not one canonical enemy");
            return false;
        }

        var target = exactMatches[0];
        var safe = SmartActionProtectionRules.IsActionProtectionSafe(
            ClassifySmartActionAttackShape(action),
            CreateSmartActionActorGeometry(target),
            action.EffectRange,
            protectedActors,
            actionIgnoresGuard:
                smartActionGuardBypassActions.Contains(resolvedActionId));
        SetSmartActionSafetyEvent(
            safe
                ? $"Safe exact generic buffer replay S{target.Slot}"
                : $"Blocked protected generic buffer replay S{target.Slot}");
        return safe;
    }

    private bool UseActionLocationDetour(
        ActionManager* thisPtr,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        Vector3* location,
        uint extraParam,
        byte a7)
    {
        if (explicitAutoGuardBreakBypassDepth > 0)
        {
            // Spending the explicit command gives up ownership before the native
            // call, even if Shukuchi is then rejected. A same-frame action must
            // not remain trapped behind Guard the user deliberately chose to exit.
            ClearAutoGuardProtection("Released: explicit /panicshu override");
        }
        else if (TryBlockOwnedAutoGuardCancellation(thisPtr, actionType, actionId))
        {
            return false;
        }

        return useActionLocationHook!.Original(
            thisPtr,
            actionType,
            actionId,
            targetId,
            location,
            extraParam,
            a7);
    }

    private bool TryCaptureSmartKardiaEukrasiaPreflight(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ActionManager.UseActionMode mode,
        ulong incomingTargetId,
        ulong forwardedTargetId,
        bool bypassRedirect,
        bool integratedBufferReplay,
        bool helperTokenConsumed,
        bool targetSuppressedByRedirect,
        out SmartKardiaEukrasiaPreflight preflight)
    {
        preflight = default;
        try
        {
            var invocationModeSupported =
                mode is ActionManager.UseActionMode.None or
                    ActionManager.UseActionMode.Macro ||
                (uint)mode == 100;
            if ((bypassRedirect && !integratedBufferReplay) ||
                helperTokenConsumed ||
                targetSuppressedByRedirect ||
                incomingTargetId != forwardedTargetId ||
                !invocationModeSupported ||
                !IsSupportedActionType(actionType) ||
                !configuration.Enabled ||
                !configuration.EnableSageKardiaAfterEukrasia ||
                ResolveContext() != SupportedPvPContext.CrystallineConflict ||
                ResolveActionId(actionManager, actionType, actionId) !=
                SmartKardiaRules.EukrasiaActionId)
            {
                return false;
            }

            var local = objectTable.LocalPlayer;
            if (!IsLivePlayer(local) ||
                !local!.ClassJob.IsValid ||
                local.ClassJob.RowId != SmartKardiaRules.SageJobId ||
                GetNativeObject(local) == null)
            {
                return false;
            }

            var localIdentity = new TargetPressureActorIdentity(
                local.GameObjectId,
                local.EntityId);
            if (!localIdentity.IsValid ||
                !TryReadExactEukrasiaEvidence(local, out var before) ||
                before.CurrentCharges == 0)
            {
                return false;
            }

            preflight = new SmartKardiaEukrasiaPreflight(
                clientState.TerritoryType,
                localIdentity,
                before);
            return true;
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                "Seiton Sense Smart Kardia ignored an unprovable Eukrasia trigger.");
            return false;
        }
    }

    private void ArmAcceptedSmartKardiaTrigger(
        SmartKardiaEukrasiaPreflight preflight)
    {
        try
        {
            var acceptedAt = Environment.TickCount64;
            SmartKardiaEukrasiaTrigger trigger;
            lock (smartKardiaTriggerGate)
            {
                if (!configuration.Enabled ||
                    !configuration.EnableSageKardiaAfterEukrasia ||
                    pendingSmartKardiaTrigger is { } pending &&
                    SmartKardiaRules.IsTriggerCurrent(
                        pending,
                        acceptedAt,
                        preflight.TerritoryId,
                        preflight.LocalPlayer))
                {
                    return;
                }

                pendingSmartKardiaTrigger = null;
                var token = NextSmartKardiaTriggerToken();
                if (!SmartKardiaRules.TryCreateAcceptedTrigger(
                        token,
                        acceptedAt,
                        preflight.TerritoryId,
                        preflight.LocalPlayer,
                        preflight.Before,
                        out trigger))
                {
                    return;
                }

                pendingSmartKardiaTrigger = trigger;
            }

            pressureTracker.RequestIncomingAllyPressureCapture(acceptedAt);
        }
        catch (Exception exception)
        {
            ClearSmartKardiaTrigger();
            LogFailure(
                exception,
                "Seiton Sense Smart Kardia failed closed while arming Eukrasia.");
        }
    }

    private long NextSmartKardiaTriggerToken()
    {
        while (true)
        {
            var current = Volatile.Read(ref smartKardiaTriggerSequence);
            if (current == long.MaxValue) return 0;
            var next = current + 1;
            if (Interlocked.CompareExchange(
                    ref smartKardiaTriggerSequence,
                    next,
                    current) == current)
            {
                return next;
            }
        }
    }

    private static bool TryReadExactEukrasiaEvidence(
        IPlayerCharacter localPlayer,
        out SmartKardiaEukrasiaEvidence evidence)
    {
        evidence = default;
        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            !localPlayer.ClassJob.IsValid ||
            localPlayer.ClassJob.RowId != SmartKardiaRules.SageJobId ||
            GetNativeObject(localPlayer) == null)
        {
            return false;
        }

        var localSourceCount = 0;
        foreach (var status in localPlayer.StatusList)
        {
            if (!SmartKardiaRules.IsEukrasiaStatus(status.StatusId)) continue;
            if (!IsNetworkEntityId(status.SourceId) ||
                status.SourceId != localPlayer.EntityId ||
                !float.IsFinite(status.RemainingTime) ||
                status.RemainingTime <= 0f ||
                ++localSourceCount > 1)
            {
                return false;
            }
        }

        var adjustedActionId = actionManager->GetAdjustedActionId(
            SmartKardiaRules.EukrasiaActionId);
        if (adjustedActionId != SmartKardiaRules.EukrasiaActionId)
            return false;
        var currentCharges = actionManager->GetCurrentCharges(adjustedActionId);
        evidence = new SmartKardiaEukrasiaEvidence(
            adjustedActionId,
            currentCharges,
            OwnStatusStateKnown: true,
            HasOwnEukrasia: localSourceCount == 1);
        return evidence.IsValid;
    }

    private void ObserveExactLocalGuardActivationAttempt(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId)
    {
        try
        {
            if (ResolveActionId(actionManager, actionType, actionId) !=
                EnemyCombatConstants.GuardActionId)
            {
                return;
            }

            var local = objectTable.LocalPlayer;
            if (!IsLivePlayer(local) || DefensiveUtilityProbe.HasActiveGuard(local))
                return;

            var attempt = new LocalGuardActionAttempt(
                clientState.TerritoryType,
                local!.GameObjectId,
                local.EntityId,
                Environment.TickCount64,
                0);
            lock (guardAttemptGate)
            {
                localGuardActionAttemptGeneration =
                    localGuardActionAttemptGeneration == long.MaxValue
                        ? 1
                        : localGuardActionAttemptGeneration + 1;
                latestLocalGuardActionAttempt = attempt with
                {
                    Generation = localGuardActionAttemptGeneration,
                };
            }
        }
        catch
        {
            // Observation must never alter or suppress the incoming Guard call.
        }
    }

    private bool TryBlockOwnedAutoGuardCancellation(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId)
    {
        lock (guardAttemptGate)
        {
            if (!autoGuardProtectionState.IsArmed) return false;
        }

        try
        {
            var supportedActionType = IsSupportedActionType(actionType);
            var resolvedActionId = supportedActionType
                ? ResolveActionId(actionManager, actionType, actionId)
                : 0;
            var explicitGuardReuse = supportedActionType &&
                                     (actionId == EnemyCombatConstants.GuardActionId ||
                                      resolvedActionId == EnemyCombatConstants.GuardActionId);
            // Unknown action resolution is deliberately fail-open. Only a
            // verified Action/PvPAction row can be suppressed.
            var actionCanCancelGuard = supportedActionType &&
                                       resolvedActionId != 0 &&
                                       TryGetActionMetadata(
                                           actionType,
                                           actionId,
                                           resolvedActionId,
                                           out var action) &&
                                       action.IsPvP &&
                                       !explicitGuardReuse;
            return ApplyAutoGuardProtectionObservation(
                actionCanCancelGuard,
                explicitGuardReuse,
                hardReset: false);
        }
        catch (Exception exception)
        {
            ClearAutoGuardProtection("Action classification failed open");
            LogFailure(
                exception,
                "Seiton Sense automatic Guard protection failed open for one action.");
            return false;
        }
    }

    private bool ApplyAutoGuardProtectionObservation(
        bool actionCanCancelGuard,
        bool explicitGuardReuse,
        bool hardReset)
    {
        var local = objectTable.LocalPlayer;
        var localLive = IsLivePlayer(local);
        var localIdentity = localLive
            ? new TargetPressureActorIdentity(local!.GameObjectId, local.EntityId)
            : default;
        var exactGuardActive = localLive && HasActiveGuardStatus(local!);
        var observation = new AutoGuardProtectionObservation(
            configuration.Enabled &&
            configuration.EnableDefensiveUtilities &&
            configuration.GuardOnStunPressure &&
            clientState.IsLoggedIn &&
            ResolveContext() == SupportedPvPContext.CrystallineConflict,
            clientState.TerritoryType,
            localIdentity,
            localLive,
            exactGuardActive,
            actionCanCancelGuard,
            explicitGuardReuse,
            Environment.TickCount64,
            hardReset);

        lock (guardAttemptGate)
        {
            var wasArmed = autoGuardProtectionState.IsArmed;
            var exactGuardWasObserved = autoGuardProtectionState.ExactGuardObserved;
            var decision = AutoGuardProtectionRules.Observe(
                autoGuardProtectionState,
                observation);
            autoGuardProtectionState = decision.NextState;
            if (decision.ShouldBlockAction)
            {
                autoGuardProtectionBlockedActionCount++;
                autoGuardProtectionLastEvent =
                    $"Blocked Guard-cancelling action ({decision.RemainingMilliseconds} ms protected)";
                return true;
            }

            if (wasArmed && !decision.NextState.IsArmed)
            {
                autoGuardProtectionReleasedCount++;
                autoGuardProtectionLastEvent = $"Released: {decision.Reason}";
            }
            else if (decision.NextState.IsArmed &&
                     decision.NextState.ExactGuardObserved &&
                     !exactGuardWasObserved)
            {
                autoGuardProtectionLastEvent = "Exact automatic Guard status confirmed";
            }

            return false;
        }
    }

    private void ClearAutoGuardProtection(string reason)
    {
        lock (guardAttemptGate)
        {
            if (autoGuardProtectionState.IsArmed) autoGuardProtectionReleasedCount++;
            autoGuardProtectionState = AutoGuardProtectionState.Initial;
            autoGuardProtectionLastEvent = reason;
        }
    }

    private bool IsLocalGuardActiveOrPropagating()
    {
        try
        {
            var local = objectTable.LocalPlayer;
            if (!IsLivePlayer(local)) return false;
            if (DefensiveUtilityProbe.HasActiveGuard(local)) return true;

            return TryGetRecentExactLocalGuardAttempt(
                clientState.TerritoryType,
                local!.GameObjectId,
                local.EntityId,
                Environment.TickCount64,
                DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
                out _);
        }
        catch
        {
            // An uncertain local Guard view must never enable a target rewrite.
            return true;
        }
    }

    private ulong TryResolveSmartTargetRedirect(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ActionManager.UseActionMode mode,
        ulong originalTargetId,
        ArmedSmartTarget token,
        out bool rewritten,
        out int selectedSlot,
        out string reason)
    {
        rewritten = false;
        selectedSlot = 0;
        reason = "Smart Action fallback: invalid context";

        var now = Environment.TickCount64;
        var localPlayer = objectTable.LocalPlayer;
        var localIdentityValid = IsLivePlayer(localPlayer) &&
                                 localPlayer!.EntityId == token.LocalEntityId &&
                                 localPlayer.GameObjectId == token.LocalGameObjectId;
        var supportedContext = configuration.Enabled &&
                               configuration.EnableSmartActionMacro &&
                               clientState.TerritoryType == token.TerritoryId &&
                               ResolveContext() == SupportedPvPContext.CrystallineConflict &&
                               localIdentityValid;
        var supportedMode = IsCertifiedMacroInvocationMode(mode) &&
                            mode != ActionManager.UseActionMode.Queue;
        var resolvedActionId = ResolveActionId(actionManager, actionType, actionId);
        if (!supportedContext ||
            !supportedMode ||
            !IsSupportedActionType(actionType) ||
            actionManager == null)
        {
            reason = "Smart Action fallback: context/action/mode changed";
            return originalTargetId;
        }

        var local = localPlayer!;
        var localActor = new TargetPressureActorIdentity(local.GameObjectId, local.EntityId);
        ArmSmartActionSafetyLease(
            token,
            localActor,
            actionType,
            actionId,
            resolvedActionId,
            now);

        if (resolvedActionId == 0)
        {
            reason = "Smart Action fallback: resolved action unavailable";
            return originalTargetId;
        }

        GameAction action = default;
        var supportedAction =
            TryGetExactResolvedPvpActionMetadata(resolvedActionId, out action) &&
            action.CanTargetHostile &&
            !action.TargetArea &&
            action.Range > 0;
        if (!supportedAction)
        {
            reason = "Smart Action fallback: exact harmful action metadata changed";
            return originalTargetId;
        }

        var partyEntityIds = GetPartyEntityIds();
        var sourceObject = GetNativeObject(local);
        if (sourceObject == null)
        {
            reason = "Smart Action fallback: native local actor unavailable";
            return originalTargetId;
        }

        if (!TryBuildSmartActionProtectionSnapshot(
                local,
                partyEntityIds,
                out var canonicalEnemies,
                out var protectedActors))
        {
            reason = "Smart Action fallback: enemy protection snapshot ambiguous";
            return originalTargetId;
        }

        var attackShape = ClassifySmartActionAttackShape(action);
        var actionIgnoresGuard =
            smartActionGuardBypassActions.Contains(resolvedActionId);

        var candidates = new List<SmartTargetRuntimeCandidate>(5);
        foreach (var canonicalEnemy in canonicalEnemies)
        {
            var slot = canonicalEnemy.Slot;
            var enemy = canonicalEnemy.Player;
            if (!TryResolveSmartTargetReachTier(local, enemy, out var reachTier)) continue;

            var targetObject = GetNativeObject(enemy);
            var hasValidActionTarget = targetObject != null;
            var rangeResult = hasValidActionTarget
                ? ActionManager.GetActionInRangeOrLoS(resolvedActionId, sourceObject, targetObject)
                : uint.MaxValue;
            var actor = new TargetPressureActorIdentity(enemy.GameObjectId, enemy.EntityId);
            var protectionSafe = SmartActionProtectionRules.IsActionProtectionSafe(
                attackShape,
                CreateSmartActionActorGeometry(canonicalEnemy),
                action.EffectRange,
                protectedActors,
                actionIgnoresGuard);
            int? freshTeamPressure = pressureTracker.TryGetFreshTeamTargetCount(
                localActor,
                actor,
                now,
                MaximumSmartTargetPressureAgeMilliseconds,
                out var teamPressure)
                ? teamPressure
                : null;

            var guardAvailability = GuardAvailability.Unknown;
            var hasTrustedMp = false;
            var currentMp = 0u;
            var maximumMp = 0u;
            var exactHudMatches = executeTracker.Enemies
                .Where(snapshot =>
                    snapshot.Slot == slot &&
                    snapshot.GameObjectId == enemy.GameObjectId &&
                    snapshot.EntityId == enemy.EntityId)
                .Take(2)
                .ToArray();
            if (exactHudMatches.Length == 1)
            {
                var hud = exactHudMatches[0];
                guardAvailability = hud.GuardUnavailable
                    ? GuardAvailability.Unavailable
                    : GuardAvailability.Unknown;
                hasTrustedMp = hud.HasTrustedMp;
                currentMp = hud.CurrentMp;
                maximumMp = hud.MaxMp;
            }

            var selection = new SmartTargetSelectionCandidate(
                slot,
                actor,
                ExactCanonicalIdentity: true,
                IsHostile: true,
                Alive: true,
                Targetable: true,
                enemy.CurrentHp,
                enemy.MaxHp,
                reachTier,
                hasValidActionTarget,
                SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
                freshTeamPressure,
                guardAvailability,
                hasTrustedMp,
                currentMp,
                maximumMp,
                CallerProvenProtectionSafe: protectionSafe);
            candidates.Add(new SmartTargetRuntimeCandidate(enemy, selection));
        }

        var selectionCandidates = candidates.Select(static candidate => candidate.Selection).ToArray();
        if (!SmartTargetSelectionRules.TryCreateIntent(
                resolvedActionId,
                selectionCandidates,
                localActor,
                out var intent))
        {
            reason = "Smart Action fallback: no exact reachable candidate";
            return originalTargetId;
        }

        var selected = candidates.SingleOrDefault(candidate =>
            candidate.Selection.EnemySlot == intent.EnemySlot &&
            candidate.Selection.Actor == intent.Target);
        if (selected.Player is null)
        {
            reason = "Smart Action fallback: selected actor became ambiguous";
            return originalTargetId;
        }

        // Revalidate the frozen tuple immediately before forwarding the sole
        // incoming native call. Never rerun ranking or select a second actor.
        var currentEnemy = EnemySlotResolver.Resolve(objectTable, intent.EnemySlot);
        var currentTargetObject = IsLivePlayer(currentEnemy) &&
                                  currentEnemy!.GameObjectId == intent.Target.GameObjectId &&
                                  currentEnemy.EntityId == intent.Target.EntityId &&
                                  !IsAlly(currentEnemy, partyEntityIds)
            ? GetNativeObject(currentEnemy)
            : null;
        var finalRange = currentTargetObject != null
            ? ActionManager.GetActionInRangeOrLoS(resolvedActionId, sourceObject, currentTargetObject)
            : uint.MaxValue;
        var finalProtectionSafe = currentEnemy is not null &&
                                  TryBuildSmartActionProtectionSnapshot(
                                      local,
                                      partyEntityIds,
                                      out _,
                                      out var finalProtectedActors) &&
                                  SmartActionProtectionRules.IsActionProtectionSafe(
                                      attackShape,
                                      CreateSmartActionActorGeometry(
                                          new CanonicalEnemy(intent.EnemySlot, currentEnemy)),
                                      action.EffectRange,
                                      finalProtectedActors,
                                      actionIgnoresGuard);
        var finalCandidate = selected.Selection with
        {
            Alive = IsLivePlayer(currentEnemy),
            Targetable = currentEnemy?.IsTargetable == true,
            CurrentHp = currentEnemy?.CurrentHp ?? 0,
            MaximumHp = currentEnemy?.MaxHp ?? 0,
            HasValidActionTarget = currentTargetObject != null,
            HasNativeRangeAndLineOfSight =
                SeitonRangeRules.HasNativeRangeAndLineOfSight(finalRange),
            CallerProvenProtectionSafe = finalProtectionSafe,
        };
        if (!SmartTargetSelectionRules.CanUseExactIntent(
                intent,
                finalCandidate,
                localActor,
                resolvedActionId))
        {
            reason = "Smart Action fallback: frozen actor/range changed";
            return originalTargetId;
        }

        rewritten = true;
        selectedSlot = intent.EnemySlot;
        reason = $"Smart Action redirected S{selectedSlot}, resolved={resolvedActionId}, range={finalRange}";
        return intent.Target.GameObjectId;
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
        var isActionSelfTargetable = supportedAction &&
                                     action.RowId == resolvedActionId &&
                                     action.CanTargetSelf;
        var preferIncomingPressure = configuration.NearHelpPreferIncomingPressure;

        var candidates = new List<NearHelpSelectionCandidate>(9);
        if (supportedContext && supportedAction && actionManager != null && localIdentityValid)
        {
            var exactLocal = localPlayer!;
            var partySlots = GetPartySlots();
            var sourceObject = GetNativeObject(exactLocal);
            if (sourceObject != null)
            {
                if (isActionSelfTargetable)
                {
                    int? incomingPressure = null;
                    if (preferIncomingPressure &&
                        pressureTracker.TryGetIncomingAllyPressure(
                            exactLocal.GameObjectId,
                            exactLocal.EntityId,
                            out var pressureCount))
                    {
                        incomingPressure = pressureCount;
                    }

                    var rangeResult = ActionManager.GetActionInRangeOrLoS(
                        resolvedActionId,
                        sourceObject,
                        sourceObject);
                    candidates.Add(new NearHelpSelectionCandidate(
                        exactLocal.GameObjectId,
                        exactLocal.EntityId,
                        partySlots.GetValueOrDefault(exactLocal.EntityId),
                        exactLocal.CurrentHp,
                        exactLocal.MaxHp,
                        DistanceSquared: 0f,
                        IsExactFriendly: true,
                        IsSelf: true,
                        HasValidActionTarget: true,
                        HasRangeAndLineOfSight:
                            SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
                        UniqueIncomingEnemyPressureCount: incomingPressure,
                        IsActionSelfTargetable: true));
                }

                foreach (var ally in objectTable.PlayerObjects.OfType<IPlayerCharacter>())
                {
                    if (!IsLivePlayer(ally) ||
                        ally.GameObjectId == exactLocal.GameObjectId ||
                        !partySlots.ContainsKey(ally.EntityId))
                    {
                        continue;
                    }

                    var targetObject = GetNativeObject(ally);
                    var distanceSquared = Vector3.DistanceSquared(exactLocal.Position, ally.Position);
                    var hasValidActionTarget = targetObject != null;
                    var rangeResult = hasValidActionTarget
                        ? ActionManager.GetActionInRangeOrLoS(resolvedActionId, sourceObject, targetObject)
                        : uint.MaxValue;
                    int? incomingPressure = null;
                    if (preferIncomingPressure &&
                        pressureTracker.TryGetIncomingAllyPressure(
                            ally.GameObjectId,
                            ally.EntityId,
                            out var pressureCount))
                    {
                        incomingPressure = pressureCount;
                    }

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
                        SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
                        UniqueIncomingEnemyPressureCount: incomingPressure,
                        IsActionSelfTargetable: false));
                }
            }
        }

        var eligibleCandidateCount = candidates.Count(NearHelpSelectionRules.IsEligible);
        var hasTrustedPressureView =
            preferIncomingPressure &&
            pressureTracker.HasActiveIncomingAllyPressureView;

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
        var decision = NearHelpOneShotRules.Observe(
            previousState,
            attempt,
            candidates,
            preferIncomingPressure,
            hasTrustedPressureView);

        lock (tokenGate) nearHelpState = decision.NextState;
        rewritten = decision.ShouldRewrite;
        if (rewritten && decision.SelectedCandidateIndex >= 0)
        {
            var selected = candidates[decision.SelectedCandidateIndex];
            var pressure = selected.UniqueIncomingEnemyPressureCount is { } pressureCount
                ? pressureCount.ToString()
                : "unknown";
            reason = $"Redirected tier={decision.SelectionReason}, " +
                     $"hp={selected.CurrentHp}/{selected.MaximumHp}, pressure={pressure}, " +
                     $"self={selected.IsSelf}, distance={MathF.Sqrt(selected.DistanceSquared):0.0}y";
        }
        else
        {
            reason = $"Fallback: {decision.Reason}, selection={decision.SelectionReason}, " +
                     $"candidates={candidates.Count}/{eligibleCandidateCount}, " +
                     $"trusted-pressure-view={hasTrustedPressureView}, resolved={resolvedActionId}";
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
        var enemySnapshot = FarHelpEnemySnapshot.Incomplete;
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
            var partyEntityIds = GetPartyEntityIds();
            enemySnapshot = ResolveFarHelpEnemySnapshot(localPlayer!, partyEntityIds);
            if (sourceObject != null)
            {
                var localPosition = localPlayer!.Position;
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
                    var allyPosition = ally.Position;
                    var distanceSquared = Vector3.DistanceSquared(localPosition, allyPosition);
                    var hasValidActionTarget = targetObject != null;
                    var rangeResult = hasValidActionTarget
                        ? ActionManager.GetActionInRangeOrLoS(resolvedActionId, sourceObject, targetObject)
                        : uint.MaxValue;
                    var insideActionSpecificLimit =
                        !float.IsFinite(maximumDistance) ||
                        (float.IsFinite(distanceSquared) &&
                         distanceSquared < maximumDistance * maximumDistance);
                    var jobId = ally.ClassJob.IsValid ? ally.ClassJob.RowId : 0;
                    var hasCompleteEnemySnapshot =
                        TryGetMinimumEnemyEdgeDistance(
                            allyPosition,
                            ally.HitboxRadius,
                            enemySnapshot,
                            out var minimumEnemyEdgeDistance);
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
                        SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
                        HasCompleteCanonicalEnemySnapshot: hasCompleteEnemySnapshot,
                        CanonicalLiveEnemyCount: enemySnapshot.LiveEnemies.Length,
                        MinimumCanonicalEnemyEdgeDistance: minimumEnemyEdgeDistance));
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
            var backlineSafe = FarHelpSelectionRules.IsBacklineSafe(selected);
            var clearance = float.IsFinite(selected.MinimumCanonicalEnemyEdgeDistance)
                ? $"{selected.MinimumCanonicalEnemyEdgeDistance:0.0}y"
                : "unknown";
            var selectionTier = GetFarHelpSelectionTier(selected, backlineSafe);
            reason = $"Redirected farthest reachable ally P{selected.PartySlot}, " +
                     $"distance={selectedDistance:0.0}y, enemy-clearance={clearance}, " +
                     $"live-enemies={selected.CanonicalLiveEnemyCount}, " +
                     $"snapshot={(selected.HasCompleteCanonicalEnemySnapshot ? "complete" : "incomplete")}, " +
                     $"tier={selectionTier}, role={selected.Role}";
        }
        else
        {
            var actionValidCandidates = candidates.Count(FarHelpSelectionRules.IsEligible);
            var safeCandidates = candidates.Count(candidate =>
                FarHelpSelectionRules.IsEligible(candidate) &&
                FarHelpSelectionRules.IsBacklineSafe(candidate));
            reason = $"Suppressed: {decision.Reason}, candidates={candidates.Count}, " +
                     $"action-valid={actionValidCandidates}, safe-backline={safeCandidates}, " +
                     $"enemy-snapshot={(enemySnapshot.IsComplete ? "complete" : "incomplete")}, " +
                     $"live-enemies={enemySnapshot.LiveEnemies.Length}, resolved={resolvedActionId}";
        }

        return decision.ForwardTargetId;
    }

    private static string GetFarHelpSelectionTier(
        FarHelpSelectionCandidate selected,
        bool backlineSafe)
    {
        if (backlineSafe) return "safe-backline(clearance>10y)";
        if (!selected.HasCompleteCanonicalEnemySnapshot)
            return "reachable-fallback(snapshot-incomplete)";
        if (selected.CanonicalLiveEnemyCount == 0)
            return "reachable-fallback(no-live-enemy-clearance)";
        if (!float.IsFinite(selected.MinimumCanonicalEnemyEdgeDistance))
            return "reachable-fallback(clearance-unknown)";

        return $"reachable-fallback(clearance<={FarHelpSelectionRules.MinimumBacklineEnemyEdgeClearance:0.#}y)";
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

        try
        {
            UpdateSmartKardiaTriggerLifecycle();
        }
        catch (Exception exception)
        {
            ClearSmartKardiaTrigger();
            LogFailure(
                exception,
                "Seiton Sense Smart Kardia trigger lifecycle failed closed.");
        }

        try
        {
            UpdateAutoGuardProtectionLifecycle();
        }
        catch (Exception exception)
        {
            ClearAutoGuardProtection("Lifecycle observation failed open");
            LogFailure(
                exception,
                "Seiton Sense automatic Guard protection lifecycle failed open.");
        }
    }

    private void UpdateAutoGuardProtectionLifecycle()
    {
        lock (guardAttemptGate)
        {
            if (!autoGuardProtectionState.IsArmed) return;
        }

        ApplyAutoGuardProtectionObservation(
            actionCanCancelGuard: false,
            explicitGuardReuse: false,
            hardReset: false);
    }

    private void UpdateSmartKardiaTriggerLifecycle()
    {
        SmartKardiaEukrasiaTrigger? pending;
        lock (smartKardiaTriggerGate) pending = pendingSmartKardiaTrigger;
        if (pending is null) return;

        var local = objectTable.LocalPlayer;
        var localIdentity = IsLivePlayer(local) && GetNativeObject(local!) != null
            ? new TargetPressureActorIdentity(local!.GameObjectId, local.EntityId)
            : default;
        var now = Environment.TickCount64;
        if (!configuration.Enabled ||
            !configuration.EnableSageKardiaAfterEukrasia ||
            ResolveContext() != SupportedPvPContext.CrystallineConflict ||
            !SmartKardiaRules.IsTriggerCurrent(
                pending.Value,
                now,
                clientState.TerritoryType,
                localIdentity))
        {
            ClearSmartKardiaTrigger();
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
                          !clientState.IsLoggedIn ||
                          ResolveContext() != SupportedPvPContext.CrystallineConflict ||
                          !IsLivePlayer(objectTable.LocalPlayer);
        lock (tokenGate)
        {
            if (armedTarget is { } token)
            {
                shouldClear |= !configuration.EnableNearAssistMacro;
                shouldClear |= token.ExpiresAtMilliseconds <= Environment.TickCount64;
            }
            if (armedSmartTarget is { } smartTargetToken)
            {
                shouldClear |= !configuration.EnableSmartActionMacro;
                shouldClear |= smartTargetToken.ExpiresAtMilliseconds <= Environment.TickCount64;
            }
            if (smartActionSafetyLeaseState.Token is { } safetyToken &&
                safetyToken.ExpiresAtMilliseconds <= Environment.TickCount64)
            {
                smartActionSafetyLeaseState = SmartActionSafetyLeaseState.Initial;
            }
            if (armedHelpTarget is { } helpToken)
            {
                shouldClear |= !configuration.EnableNearAssistMacro;
                shouldClear |= helpToken.ExpiresAtMilliseconds <= Environment.TickCount64;
            }
            if (armedFarHelpTarget is { } farHelpToken)
            {
                shouldClear |= !configuration.EnableNearAssistMacro;
                shouldClear |= farHelpToken.ExpiresAtMilliseconds <= Environment.TickCount64;
            }
            if (farHelpFallbackSuppressionState.Token is { } suppressionToken &&
                suppressionToken.ExpiresAtMilliseconds <= Environment.TickCount64)
            {
                farHelpFallbackSuppressionState = FarHelpFallbackSuppressionState.Initial;
            }
        }

        if (shouldClear)
        {
            ClearToken("Cleared: context, player, or token lifetime changed");
        }
    }

    internal bool IsExactLocalGuardActiveOrPropagating(
        TargetPressureActorIdentity expectedLocalPlayer)
    {
        try
        {
            var local = objectTable.LocalPlayer;
            if (!expectedLocalPlayer.IsValid ||
                !IsLivePlayer(local) ||
                GetNativeObject(local!) == null)
            {
                return true;
            }
            var currentLocalPlayer = new TargetPressureActorIdentity(
                local!.GameObjectId,
                local.EntityId);
            if (currentLocalPlayer != expectedLocalPlayer) return true;
            if (DefensiveUtilityProbe.HasActiveGuard(local)) return true;

            return TryGetRecentExactLocalGuardAttempt(
                clientState.TerritoryType,
                local.GameObjectId,
                local.EntityId,
                Environment.TickCount64,
                DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
                out _);
        }
        catch
        {
            // This dependency owns a native safety boundary. Uncertain current
            // local identity or Guard telemetry must veto the helper action.
            return true;
        }
    }

    private bool ShouldVetoAstrologianOwnGuardAtFinalBoundary(
        ActionType actionType,
        uint actionId,
        ulong forwardedTargetId,
        ActionManager.UseActionMode mode)
    {
        if (astrologianOwnGuardVetoScope is not { } scope) return false;
        if (scope.Owner != this || scope.Consumed) return true;
        scope.Consumed = true;

        var local = objectTable.LocalPlayer;
        var currentLocalPlayer = IsLivePlayer(local)
            ? new TargetPressureActorIdentity(
                local!.GameObjectId,
                local.EntityId)
            : default;
        var ownGuardActiveOrPropagating =
            IsExactLocalGuardActiveOrPropagating(scope.LocalPlayer);
        return actionType != ActionType.Action ||
               mode != ActionManager.UseActionMode.None ||
               actionId != scope.ActionId ||
               AstrologianHarmonicOrbisRules
                   .ShouldVetoNativeBoundaryForOwnGuard(
                       actionId,
                       scope.LocalPlayer,
                       currentLocalPlayer,
                       scope.TargetGameObjectId,
                       forwardedTargetId,
                       ownGuardActiveOrPropagating);
    }

    private void ClearSmartActionSafetyLease()
    {
        lock (tokenGate)
            smartActionSafetyLeaseState = SmartActionSafetyLeaseState.Initial;
    }

    private void ArmSmartActionSafetyLease(
        ArmedSmartTarget token,
        TargetPressureActorIdentity localPlayer,
        ActionType actionType,
        uint rawActionId,
        uint resolvedActionId,
        long nowMilliseconds)
    {
        var next = SmartActionSafetyLeaseRules.Arm(
            token.TerritoryId,
            localPlayer,
            (uint)actionType,
            rawActionId,
            resolvedActionId,
            nowMilliseconds,
            nowMilliseconds + SmartActionSafetyLeaseRules.DefaultLifetimeMilliseconds);
        lock (tokenGate) smartActionSafetyLeaseState = next;
    }

    private void EnsureSmartActionSafetyLeaseAfterFailure(
        ActionManager* actionManager,
        ActionType actionType,
        uint rawActionId,
        ArmedSmartTarget? token)
    {
        if (token is not { } exactToken) return;

        try
        {
            var now = Environment.TickCount64;
            var local = objectTable.LocalPlayer;
            if (!IsLivePlayer(local) ||
                local!.EntityId != exactToken.LocalEntityId ||
                local.GameObjectId != exactToken.LocalGameObjectId ||
                !IsSupportedActionType(actionType) ||
                actionManager == null)
            {
                return;
            }

            uint resolvedActionId;
            try
            {
                resolvedActionId = ResolveActionId(actionManager, actionType, rawActionId);
            }
            catch
            {
                // Raw identity still provides a narrow fail-closed fallback
                // blockade when adjusted-action resolution itself drifted.
                resolvedActionId = 0;
            }

            ArmSmartActionSafetyLease(
                exactToken,
                new TargetPressureActorIdentity(local.GameObjectId, local.EntityId),
                actionType,
                rawActionId,
                resolvedActionId,
                now);
        }
        catch
        {
            // The current consumed carrier is suppressed by the outer fail-closed
            // path. If exact fallback ownership cannot be reconstructed, no broad
            // or guessed action quarantine is installed.
        }
    }

    private SmartActionSafetyInspectionOutcome InspectSmartActionSafetyLease(
        ActionManager* actionManager,
        ActionType actionType,
        uint rawActionId,
        ulong incomingTargetId,
        ActionManager.UseActionMode mode,
        out ulong canonicalTargetId)
    {
        canonicalTargetId = incomingTargetId;
        SmartActionSafetyLeaseToken? token;
        lock (tokenGate) token = smartActionSafetyLeaseState.Token;
        if (token is null) return SmartActionSafetyInspectionOutcome.NotApplicable;
        var potentiallyExactAction =
            ((uint)actionType == token.Value.RawActionType &&
             rawActionId == token.Value.RawActionId) ||
            (token.Value.ResolvedActionId == 0 &&
             IsSupportedActionType(actionType));

        try
        {
            var resolvedActionId = ResolveActionId(actionManager, actionType, rawActionId);
            potentiallyExactAction |= token.Value.ResolvedActionId != 0 &&
                                      resolvedActionId == token.Value.ResolvedActionId;
            potentiallyExactAction |= resolvedActionId == 0 &&
                                      IsSupportedActionType(actionType);
            var recognizedMode = mode is ActionManager.UseActionMode.Macro or
                                 ActionManager.UseActionMode.None or
                                 ActionManager.UseActionMode.Queue ||
                                 (uint)mode == 100;
            if (!recognizedMode || !IsSupportedActionType(actionType))
            {
                if (!potentiallyExactAction)
                    return SmartActionSafetyInspectionOutcome.NotApplicable;

                SetSmartActionSafetyEvent(
                    "Blocked exact Smart Action fallback: invocation mode drifted");
                return SmartActionSafetyInspectionOutcome.Unsafe;
            }

            var now = Environment.TickCount64;
            var local = objectTable.LocalPlayer;
            var localActor = IsLivePlayer(local)
                ? new TargetPressureActorIdentity(local!.GameObjectId, local.EntityId)
                : default;
            SmartActionSafetyLeaseDecision decision;
            lock (tokenGate)
            {
                decision = SmartActionSafetyLeaseRules.Observe(
                    smartActionSafetyLeaseState,
                    clientState.TerritoryType,
                    localActor,
                    (uint)actionType,
                    rawActionId,
                    resolvedActionId,
                    now);
                smartActionSafetyLeaseState = decision.NextState;
            }

            if (!decision.ShouldInspect)
            {
                if (!decision.ShouldRejectDrift)
                    return SmartActionSafetyInspectionOutcome.NotApplicable;

                SetSmartActionSafetyEvent(
                    "Blocked exact Smart Action fallback: adjusted action drifted");
                return SmartActionSafetyInspectionOutcome.Unsafe;
            }

            if (!TryGetExactResolvedPvpActionMetadata(resolvedActionId, out var action) ||
                !action.CanTargetHostile ||
                action.TargetArea ||
                action.Range <= 0 ||
                local is null)
            {
                SetSmartActionSafetyEvent(
                    "Blocked exact Smart Action fallback: action metadata changed");
                return SmartActionSafetyInspectionOutcome.Unsafe;
            }

            var partyEntityIds = GetPartyEntityIds();
            if (!TryBuildSmartActionProtectionSnapshot(
                    local,
                    partyEntityIds,
                    out var canonicalEnemies,
                    out var protectedActors))
            {
                SetSmartActionSafetyEvent(
                    "Blocked exact Smart Action fallback: protection snapshot ambiguous");
                return SmartActionSafetyInspectionOutcome.Unsafe;
            }

            var effectiveTargetId = incomingTargetId is 0 or InvalidObjectId
                ? GetNativeHardTargetId(local)
                : incomingTargetId;
            var exactMatches = canonicalEnemies
                .Where(enemy =>
                    enemy.Player.GameObjectId == effectiveTargetId ||
                    enemy.Player.EntityId == effectiveTargetId)
                .Take(2)
                .ToArray();
            if (exactMatches.Length != 1)
            {
                SetSmartActionSafetyEvent(
                    "Blocked exact Smart Action fallback: target was not one canonical enemy");
                return SmartActionSafetyInspectionOutcome.Unsafe;
            }

            var target = exactMatches[0];
            var safe = SmartActionProtectionRules.IsActionProtectionSafe(
                ClassifySmartActionAttackShape(action),
                CreateSmartActionActorGeometry(target),
                action.EffectRange,
                protectedActors,
                actionIgnoresGuard:
                    smartActionGuardBypassActions.Contains(resolvedActionId));
            SetSmartActionSafetyEvent(
                safe
                    ? $"Safe exact Smart Action fallback S{target.Slot}"
                    : $"Blocked protected Smart Action fallback S{target.Slot}");
            if (safe)
                canonicalTargetId = target.Player.GameObjectId;
            return safe
                ? SmartActionSafetyInspectionOutcome.Safe
                : SmartActionSafetyInspectionOutcome.Unsafe;
        }
        catch (Exception exception)
        {
            potentiallyExactAction |= IsSupportedActionType(actionType);
            if (!potentiallyExactAction)
                return SmartActionSafetyInspectionOutcome.NotApplicable;

            LogFailure(
                exception,
                "Seiton Sense Smart Action fallback safety failed closed.");
            SetSmartActionSafetyEvent(
                "Blocked exact Smart Action fallback: safety inspection failed");
            return SmartActionSafetyInspectionOutcome.Unsafe;
        }
    }

    private void SetSmartActionSafetyEvent(string value)
    {
        lock (tokenGate)
        {
            smartTargetLastEvent = value;
            RecordTraceLocked(value);
        }
    }

    private bool TryBuildSmartActionProtectionSnapshot(
        IPlayerCharacter localPlayer,
        HashSet<uint> partyEntityIds,
        out CanonicalEnemy[] canonicalEnemies,
        out SmartActionProtectedActor[] protectedActors)
    {
        if (!smartActionProtectionMetadataVerified)
        {
            canonicalEnemies = [];
            protectedActors = [];
            return false;
        }

        var enemies = new List<CanonicalEnemy>(5);
        var protections = new List<SmartActionProtectedActor>(5);
        var occupiedGameObjectIds = new HashSet<ulong>();
        var occupiedEntityIds = new HashSet<uint>();

        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var enemy = EnemySlotResolver.Resolve(objectTable, slot);
            if (!IsLivePlayer(enemy) ||
                enemy!.GameObjectId == localPlayer.GameObjectId ||
                IsAlly(enemy, partyEntityIds))
            {
                continue;
            }

            if (!occupiedGameObjectIds.Add(enemy.GameObjectId) ||
                !occupiedEntityIds.Add(enemy.EntityId))
            {
                canonicalEnemies = [];
                protectedActors = [];
                return false;
            }

            var canonical = new CanonicalEnemy(slot, enemy);
            enemies.Add(canonical);

            var jobId = enemy.ClassJob.IsValid ? enemy.ClassJob.RowId : 0;
            var protectionKind = !chitenMetadataVerified &&
                                 (jobId == EnemyCombatConstants.SamuraiJobId || jobId == 0)
                ? SmartActionProtectionKind.Chiten
                : SmartActionProtectionKind.None;
            foreach (var status in enemy.StatusList)
            {
                var exactKind = SmartActionProtectionRules.ClassifyExactStatus(status.StatusId);
                if (exactKind == SmartActionProtectionKind.None) continue;
                if (exactKind == SmartActionProtectionKind.Chiten)
                {
                    if (jobId != EnemyCombatConstants.SamuraiJobId &&
                        !(!chitenMetadataVerified && jobId == 0))
                    {
                        canonicalEnemies = [];
                        protectedActors = [];
                        return false;
                    }

                    protectionKind |= exactKind;
                    continue;
                }

                protectionKind |= exactKind;
            }

            if (protectionKind != SmartActionProtectionKind.None)
            {
                protections.Add(new SmartActionProtectedActor(
                    CreateSmartActionActorGeometry(canonical),
                    protectionKind));
            }
        }

        // The fixed native S-slot view must account for every currently live,
        // targetable hostile player in the object table. Otherwise an omitted
        // protected actor could sit inside an AoE without entering its geometry.
        var observedHostileGameObjectIds = new HashSet<ulong>();
        var observedHostileEntityIds = new HashSet<uint>();
        foreach (var player in objectTable.PlayerObjects.OfType<IPlayerCharacter>())
        {
            if (!IsLivePlayer(player) ||
                player.GameObjectId == localPlayer.GameObjectId ||
                IsAlly(player, partyEntityIds))
            {
                continue;
            }

            if (!observedHostileGameObjectIds.Add(player.GameObjectId) ||
                !observedHostileEntityIds.Add(player.EntityId) ||
                !occupiedGameObjectIds.Contains(player.GameObjectId) ||
                !occupiedEntityIds.Contains(player.EntityId))
            {
                canonicalEnemies = [];
                protectedActors = [];
                return false;
            }
        }

        if (!observedHostileGameObjectIds.SetEquals(occupiedGameObjectIds) ||
            !observedHostileEntityIds.SetEquals(occupiedEntityIds))
        {
            canonicalEnemies = [];
            protectedActors = [];
            return false;
        }

        canonicalEnemies = enemies.ToArray();
        protectedActors = protections.ToArray();
        return true;
    }

    private static SmartActionAttackShape ClassifySmartActionAttackShape(GameAction action) =>
        SmartActionProtectionRules.ClassifyAttackShape(
            action.EffectRange,
            action.CastType);

    private static SmartActionActorGeometry CreateSmartActionActorGeometry(
        CanonicalEnemy enemy) =>
        new(
            enemy.Slot,
            new TargetPressureActorIdentity(
                enemy.Player.GameObjectId,
                enemy.Player.EntityId),
            ExactCanonicalIdentity: true,
            enemy.Player.Position,
            enemy.Player.HitboxRadius);

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

    private FarHelpEnemySnapshot ResolveFarHelpEnemySnapshot(
        IPlayerCharacter localPlayer,
        HashSet<uint> partyEntityIds)
    {
        var seenEntityIds = new HashSet<uint>();
        var seenGameObjectIds = new HashSet<ulong>();
        var liveEnemies = new List<FarHelpEnemyThreat>(FarHelpSelectionRules.MaximumCanonicalEnemyCount);

        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            var enemy = EnemySlotResolver.Resolve(objectTable, slot);
            if (enemy is null ||
                enemy.Address == 0 ||
                !IsNetworkEntityId(enemy.EntityId) ||
                !IsNetworkObjectId(enemy.GameObjectId) ||
                enemy.EntityId == localPlayer.EntityId ||
                enemy.GameObjectId == localPlayer.GameObjectId ||
                IsAlly(enemy, partyEntityIds) ||
                !seenEntityIds.Add(enemy.EntityId) ||
                !seenGameObjectIds.Add(enemy.GameObjectId))
            {
                return FarHelpEnemySnapshot.Incomplete;
            }

            var position = enemy.Position;
            var hitboxRadius = enemy.HitboxRadius;
            if (!float.IsFinite(position.X) ||
                !float.IsFinite(position.Z) ||
                !float.IsFinite(hitboxRadius) ||
                hitboxRadius < 0f)
            {
                return FarHelpEnemySnapshot.Incomplete;
            }

            // A confirmed dead enemy is still required for the exact e1-e5
            // identity snapshot, but cannot presently threaten either edge.
            if (enemy.IsDead) continue;

            // Untargetable living enemies still occupy the arena and therefore
            // count. Ambiguous zero-HP/non-dead observations make the whole
            // heuristic unknown instead of being silently treated as safe.
            if (enemy.CurrentHp == 0 || enemy.MaxHp < enemy.CurrentHp)
                return FarHelpEnemySnapshot.Incomplete;

            liveEnemies.Add(new FarHelpEnemyThreat(position.X, position.Z, hitboxRadius));
        }

        return seenEntityIds.Count == FarHelpSelectionRules.MaximumCanonicalEnemyCount &&
               seenGameObjectIds.Count == FarHelpSelectionRules.MaximumCanonicalEnemyCount
            ? new FarHelpEnemySnapshot(true, liveEnemies.ToArray())
            : FarHelpEnemySnapshot.Incomplete;
    }

    private static bool TryGetMinimumEnemyEdgeDistance(
        Vector3 allyPosition,
        float allyHitboxRadius,
        FarHelpEnemySnapshot snapshot,
        out float minimumEnemyEdgeDistance)
    {
        minimumEnemyEdgeDistance = float.NaN;
        if (!snapshot.IsComplete ||
            !float.IsFinite(allyPosition.X) ||
            !float.IsFinite(allyPosition.Z) ||
            !float.IsFinite(allyHitboxRadius) ||
            allyHitboxRadius < 0f)
        {
            return false;
        }

        if (snapshot.LiveEnemies.Length == 0) return true;

        var minimum = float.PositiveInfinity;
        foreach (var enemy in snapshot.LiveEnemies)
        {
            var deltaX = allyPosition.X - enemy.X;
            var deltaZ = allyPosition.Z - enemy.Z;
            var centerDistanceSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
            if (!float.IsFinite(centerDistanceSquared) || centerDistanceSquared < 0f)
                return false;

            var centerDistance = MathF.Sqrt(centerDistanceSquared);
            var edgeDistance = MathF.Max(
                0f,
                centerDistance - allyHitboxRadius - enemy.HitboxRadius);
            if (!float.IsFinite(edgeDistance)) return false;
            minimum = MathF.Min(minimum, edgeDistance);
        }

        if (!float.IsFinite(minimum)) return false;
        minimumEnemyEdgeDistance = minimum;
        return true;
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

    private bool TryGetExactResolvedPvpActionMetadata(
        uint resolvedActionId,
        out GameAction action)
    {
        var actions = dataManager.GetExcelSheet<GameAction>();
        if (resolvedActionId != 0 &&
            actions.TryGetRow(resolvedActionId, out var exact) &&
            exact.RowId == resolvedActionId &&
            exact.IsPvP)
        {
            action = exact;
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

    private bool TryConsumeEligibleSmartTargetToken(
        ArmedSmartTarget expectedToken,
        ActionType actionType,
        ActionManager.UseActionMode mode,
        ulong incomingTargetId,
        out ArmedSmartTarget token,
        out bool fallbackCarrier,
        out bool ownershipChanged)
    {
        lock (tokenGate)
        {
            if (armedSmartTarget is not { } candidate)
            {
                token = default;
                fallbackCarrier = false;
                ownershipChanged = true;
                return false;
            }
            if (!candidate.Equals(expectedToken))
            {
                token = default;
                fallbackCarrier = false;
                ownershipChanged = true;
                return false;
            }
            if (Environment.TickCount64 >= candidate.ExpiresAtMilliseconds)
            {
                // Token lifetime gates ownership. An expired arm stays on the
                // vanilla path and is never consumed into an unprotected
                // authored fallback.
                armedSmartTarget = null;
                token = default;
                fallbackCarrier = false;
                ownershipChanged = false;
                smartTargetLastEvent = "Expired before exact Smart Action claim";
                return false;
            }
            if (!IsPotentialMacroAction(actionType, mode))
            {
                token = default;
                fallbackCarrier = false;
                ownershipChanged = false;
                return false;
            }

            token = candidate;
            ownershipChanged = false;
            var currentPlayer = objectTable.LocalPlayer;
            var currentHardTargetId = IsLivePlayer(currentPlayer)
                ? GetNativeHardTargetId(currentPlayer!)
                : 0;
            fallbackCarrier = NearAssistCarrierRules.IsFallbackCarrier(
                currentHardTargetId,
                incomingTargetId,
                candidate.CarrierEnemyGameObjectId,
                candidate.CarrierEnemyEntityId);
            armedSmartTarget = null;
            smartTargetLastEvent = $"Consumed target={incomingTargetId:X}; selecting for action";
            return true;
        }
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
                           armedSmartTarget is not null ||
                           smartActionSafetyLeaseState.IsArmed ||
                           armedHelpTarget is not null || nearHelpState.IsArmed ||
                           armedFarHelpTarget is not null || farHelpState.IsArmed ||
                           farHelpFallbackSuppressionState.IsArmed;
            armedTarget = null;
            oneShotState = NearAssistOneShotState.Initial;
            armedSmartTarget = null;
            smartActionSafetyLeaseState = SmartActionSafetyLeaseState.Initial;
            armedHelpTarget = null;
            nearHelpState = NearHelpOneShotState.Initial;
            armedFarHelpTarget = null;
            farHelpState = FarHelpOneShotState.Initial;
            farHelpFallbackSuppressionState = FarHelpFallbackSuppressionState.Initial;
            lastEvent = reason;
            smartTargetLastEvent = reason;
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

    private NearAssistArmResult SmartTargetArmFailure(
        NearAssistArmOutcome outcome,
        string reason)
    {
        lock (tokenGate)
        {
            smartTargetLastEvent = reason;
            RecordTraceLocked($"smart-arm-fail {outcome}: {reason}");
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

    private bool IsEligibleSmartActionRedirectAction(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ActionManager.UseActionMode mode)
    {
        if (!IsPotentialMacroAction(actionType, mode) || actionId == 0)
            return false;

        var resolvedActionId = ResolveActionId(actionManager, actionType, actionId);
        if (resolvedActionId == 0)
            return true;
        if (!TryGetExactResolvedPvpActionMetadata(resolvedActionId, out var action))
        {
            return true;
        }

        return action.CanTargetHostile &&
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
        ActionManager.UseActionMode mode,
        out uint resolvedActionId)
    {
        resolvedActionId = 0;
        if (!IsPotentialMacroAction(actionType, mode)) return false;

        resolvedActionId = ResolveActionId(actionManager, actionType, actionId);
        // Consume only an exact known movement-action intent. Its complete current
        // metadata is deliberately revalidated after consumption so metadata drift
        // leaves the intrinsically invalid <me> carrier suppressed.
        return resolvedActionId != 0 &&
               TryGetFarHelpMovementDefinition(resolvedActionId, out _, out _);
    }

    private void ArmFarHelpFallbackSuppression(
        ActionType actionType,
        uint rawActionId,
        uint resolvedActionId)
    {
        var now = Environment.TickCount64;
        var next = FarHelpFallbackSuppressionRules.Arm(
            (uint)actionType,
            rawActionId,
            resolvedActionId,
            now,
            TokenLifetimeMilliseconds);
        lock (tokenGate) farHelpFallbackSuppressionState = next;
    }

    private void EnsureFarHelpFallbackSuppressionAfterFailure(
        ActionManager* actionManager,
        ActionType actionType,
        uint rawActionId)
    {
        lock (tokenGate)
        {
            if (farHelpFallbackSuppressionState.IsArmed) return;
        }

        try
        {
            var resolvedActionId = ResolveActionId(actionManager, actionType, rawActionId);
            if (!TryGetFarHelpMovementDefinition(resolvedActionId, out _, out _) &&
                TryGetFarHelpMovementDefinition(rawActionId, out _, out _))
            {
                resolvedActionId = rawActionId;
            }

            if (TryGetFarHelpMovementDefinition(resolvedActionId, out _, out _))
                ArmFarHelpFallbackSuppression(actionType, rawActionId, resolvedActionId);
        }
        catch
        {
            // The current action is still suppressed to target zero below. A
            // failure to establish the optional migration quarantine must never
            // prevent the detour's sole Original call.
        }
    }

    private bool TrySuppressLegacyFarHelpFallback(
        ActionManager* actionManager,
        ActionType actionType,
        uint actionId,
        ActionManager.UseActionMode mode)
    {
        // A legacy fallback can arrive as Macro, raw 100, ReAction-converted
        // None, or Queue. Only an already armed exact adjusted-action quarantine
        // can suppress it; unrelated actions always pass through unchanged.
        if (!IsSupportedActionType(actionType)) return false;
        var recognizedMode = mode is ActionManager.UseActionMode.Macro or
                             ActionManager.UseActionMode.None or
                             ActionManager.UseActionMode.Queue ||
                             (uint)mode == 100;
        if (!recognizedMode) return false;

        var resolvedActionId = ResolveActionId(actionManager, actionType, actionId);
        if (resolvedActionId == 0) return false;

        lock (tokenGate)
        {
            var decision = FarHelpFallbackSuppressionRules.Observe(
                farHelpFallbackSuppressionState,
                new FarHelpFallbackSuppressionAttempt(
                    (uint)actionType,
                    actionId,
                    resolvedActionId,
                    Environment.TickCount64));
            farHelpFallbackSuppressionState = decision.NextState;
            return decision.ShouldSuppress;
        }
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
            case 29066: // PLD Guardian. Native range/LoS owns the verified 20y sheet reachability.
                expectedJobId = 19;
                maximumDistance = float.PositiveInfinity;
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

    private static bool TryResolveSmartTargetReachTier(
        IPlayerCharacter localPlayer,
        IPlayerCharacter enemy,
        out SmartTargetReachTier tier)
    {
        tier = SmartTargetReachTier.RangedOrOther;
        var localJobId = localPlayer.ClassJob.IsValid ? localPlayer.ClassJob.RowId : 0;
        if (NearAssistSelectionRules.ClassifyPlayableJob(localJobId) !=
            NearAssistAllyRole.MeleeDamage)
        {
            return true;
        }

        return SmartTargetReachRules.TryResolveReachTier(
            localJobId,
            localPlayer.Position,
            localPlayer.HitboxRadius,
            enemy.Position,
            enemy.HitboxRadius,
            out tier);
    }

    private static bool HasActiveGuardStatus(IPlayerCharacter player)
    {
        foreach (var status in player.StatusList)
        {
            if (status.StatusId is not (EnemyCombatConstants.GuardStatusId or
                EnemyCombatConstants.GuardStatusAlternateId))
            {
                continue;
            }

            if (float.IsFinite(status.RemainingTime) && status.RemainingTime > 0f)
                return true;
        }

        return false;
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

    private sealed class ExplicitAutoGuardBreakScope : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            explicitAutoGuardBreakBypassDepth = Math.Max(
                0,
                explicitAutoGuardBreakBypassDepth - 1);
        }
    }

    private sealed class PredictiveCcBrakeBypassScope(
        NearAssistRedirector owner,
        PredictiveCcBrakeBypassIntent intent)
    {
        internal NearAssistRedirector Owner { get; } = owner;
        internal PredictiveCcBrakeBypassIntent Intent { get; } = intent;
        internal bool Consumed { get; set; }
    }

    private sealed class AstrologianOwnGuardVetoScope(
        NearAssistRedirector owner,
        uint actionId,
        TargetPressureActorIdentity localPlayer,
        ulong targetGameObjectId)
    {
        internal NearAssistRedirector Owner { get; } = owner;
        internal uint ActionId { get; } = actionId;
        internal TargetPressureActorIdentity LocalPlayer { get; } = localPlayer;
        internal ulong TargetGameObjectId { get; } = targetGameObjectId;
        internal bool Consumed { get; set; }
    }

    private sealed class IntegratedBufferedReplayScope(
        NearAssistRedirector owner,
        IntegratedBufferedReplayIntent intent)
    {
        internal NearAssistRedirector Owner { get; } = owner;
        internal IntegratedBufferedReplayIntent Intent { get; } = intent;
        internal bool Consumed { get; set; }
    }

    private static NearAssistAllyRole GetRolePreference(IPlayerCharacter player)
    {
        var jobId = player.ClassJob.IsValid ? player.ClassJob.RowId : 0;
        return NearAssistSelectionRules.ClassifyPlayableJob(jobId);
    }

    private readonly record struct CanonicalEnemy(int Slot, IPlayerCharacter Player);

    private readonly record struct SmartKardiaEukrasiaPreflight(
        uint TerritoryId,
        TargetPressureActorIdentity LocalPlayer,
        SmartKardiaEukrasiaEvidence Before);

    private readonly record struct FarHelpEnemyThreat(float X, float Z, float HitboxRadius);

    private readonly record struct FarHelpEnemySnapshot(
        bool IsComplete,
        FarHelpEnemyThreat[] LiveEnemies)
    {
        internal static FarHelpEnemySnapshot Incomplete => new(false, []);
    }

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

    private readonly record struct ArmedSmartTarget(
        uint TerritoryId,
        uint LocalEntityId,
        ulong LocalGameObjectId,
        uint CarrierEnemyEntityId,
        ulong CarrierEnemyGameObjectId,
        long ExpiresAtMilliseconds);

    private readonly record struct SmartTargetRuntimeCandidate(
        IPlayerCharacter Player,
        SmartTargetSelectionCandidate Selection);

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
