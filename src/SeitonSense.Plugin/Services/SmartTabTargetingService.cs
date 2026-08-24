using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal enum SmartTabTargetingOutcome
{
    NotStarted,
    Selected,
    Disabled,
    NotCrystallineConflict,
    LocalPlayerUnavailable,
    UnsupportedJob,
    CanonicalIdentityRejected,
    AmbiguousCanonicalIdentity,
    NoEligibleTarget,
    FrozenTargetChanged,
    ContextChanged,
    SetterRejected,
    SetterReadbackMismatch,
    FailedClosed,
}

internal sealed record SmartTabTargetingDiagnostics(
    bool HookAvailable,
    bool Started,
    bool Configured,
    bool IsCrystallineConflict,
    uint LocalJobId,
    int ResolvedSlotCount,
    int CandidateCount,
    int SelectedEnemySlot,
    ulong SelectedGameObjectId,
    uint SelectedEntityId,
    SmartTargetReachTier SelectedReachTier,
    SmartTabTargetingOutcome Outcome,
    long RequestCount,
    long SetterInvocationCount,
    long ExactReadbackCount,
    long RejectedCount,
    long TerminalFailureCount,
    long NativeForwardRequestCount,
    long ConsumedRequestCount,
    long ConsumedWithoutSelectionCount,
    long VanillaPassThroughCount,
    string LastEvent)
{
    internal static SmartTabTargetingDiagnostics Initial { get; } = new(
        false,
        false,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        SmartTargetReachTier.RangedOrOther,
        SmartTabTargetingOutcome.NotStarted,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        "Not started");

    internal string ToChatLine() =>
        $"hook={HookAvailable},started={Started},configured={Configured}," +
        $"cc={IsCrystallineConflict},job={LocalJobId}," +
        $"slots={ResolvedSlotCount},candidates={CandidateCount}," +
        $"selected=S{SelectedEnemySlot}/{SelectedGameObjectId:X}/{SelectedEntityId:X}/" +
        $"{SelectedReachTier},outcome={Outcome}," +
        $"counts={RequestCount}/{SetterInvocationCount}/{ExactReadbackCount}/" +
        $"{RejectedCount}/{TerminalFailureCount},native={NativeForwardRequestCount}/" +
        $"{ConsumedRequestCount}/{ConsumedWithoutSelectionCount}/{VanillaPassThroughCount}," +
        $"last={LastEvent}";
}

/// <summary>
/// Replaces only FFXIV's native world-target forward cycle in exact CC on
/// reviewed melee jobs. FFXIV reaches this function after its own logical
/// TARGET_NEXT binding and UI/input gates, so remaps remain native while reverse
/// targeting and unrelated inputs never become owned requests. Toggle-off,
/// unsupported jobs, and unsupported contexts call the original cycle unchanged.
/// An owned request sets at most one exact hard target and never dispatches an
/// action, calls the native cycle, retries, reranks, restores, or substitutes.
/// </summary>
internal sealed unsafe class SmartTabTargetingService : IDisposable
{
    [ThreadStatic]
    private static int nativeTargetingHandlerDepth;

    // SigScanner resolves a leading E8 to the called function. On the reviewed
    // client this is the world target-cycle routine used by TARGET_NEXT/PREV;
    // its final byte is false for forward and true for reverse.
    private const string NativeWorldTargetCycleSignature =
        "E8 ?? ?? ?? ?? BA ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? 84 C0 0F 84";
    private const long MaximumPressureAgeMilliseconds = 250;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte NativeWorldTargetCycleDelegate(
        TargetSystem* targetSystem,
        nint targetingContext,
        GameObjectArray* candidateArray,
        byte reverse);

    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDutyState dutyState;
    private readonly TargetPressureTracker pressureTracker;
    private readonly ExecuteTracker executeTracker;
    private readonly IPluginLog log;
    private readonly Hook<TargetSystem.Delegates.HandleTargetingKeybinds>? targetingKeybindsHook;
    private readonly Hook<NativeWorldTargetCycleDelegate>? targetCycleHook;

    private SmartTabTargetingDiagnostics diagnostics = SmartTabTargetingDiagnostics.Initial;
    private long requestCount;
    private long setterInvocationCount;
    private long exactReadbackCount;
    private long rejectedCount;
    private long terminalFailureCount;
    private long nativeForwardRequestCount;
    private long consumedRequestCount;
    private long consumedWithoutSelectionCount;
    private long vanillaPassThroughCount;
    private long nextErrorLogAtMilliseconds;
    private bool started;
    private bool disposed;

    internal SmartTabTargetingService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IDutyState dutyState,
        IGameInteropProvider interop,
        ISigScanner sigScanner,
        TargetPressureTracker pressureTracker,
        ExecuteTracker executeTracker,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.dutyState = dutyState;
        this.pressureTracker = pressureTracker;
        this.executeTracker = executeTracker;
        this.log = log;

        Hook<TargetSystem.Delegates.HandleTargetingKeybinds>? createdTargetingHook = null;
        Hook<NativeWorldTargetCycleDelegate>? createdCycleHook = null;
        try
        {
            var cycleAddress = sigScanner.ScanText(NativeWorldTargetCycleSignature);
            if (cycleAddress == nint.Zero)
                throw new InvalidOperationException("Native world target-cycle signature resolved to zero");

            createdCycleHook = interop.HookFromAddress<NativeWorldTargetCycleDelegate>(
                cycleAddress,
                NativeWorldTargetCycleDetour);
            createdTargetingHook =
                interop.HookFromAddress<TargetSystem.Delegates.HandleTargetingKeybinds>(
                    TargetSystem.MemberFunctionPointers.HandleTargetingKeybinds,
                    HandleTargetingKeybindsDetour);
            diagnostics = diagnostics with { LastEvent = "Native world forward-target hooks created" };
        }
        catch (Exception exception)
        {
            try
            {
                createdTargetingHook?.Dispose();
            }
            catch (Exception disposeException)
            {
                log.Error(disposeException, "Seiton Sense Smart Tab targeting-handler hook creation rollback failed.");
            }

            try
            {
                createdCycleHook?.Dispose();
            }
            catch (Exception disposeException)
            {
                log.Error(disposeException, "Seiton Sense Smart Tab target-cycle hook creation rollback failed.");
            }

            createdTargetingHook = null;
            createdCycleHook = null;
            diagnostics = diagnostics with { LastEvent = "Native world forward-target hooks unavailable" };
            LogFailure(exception, "Smart Tab native target-cycle hooks are unavailable; FFXIV targeting remains vanilla");
        }

        targetingKeybindsHook = createdTargetingHook;
        targetCycleHook = createdCycleHook;
    }

    internal SmartTabTargetingDiagnostics Diagnostics => Volatile.Read(ref diagnostics) with
    {
        HookAvailable = HooksAvailable,
        Started = started && !disposed,
        Configured = configuration.Enabled && configuration.EnableSmartTabTargeting,
        NativeForwardRequestCount = Volatile.Read(ref nativeForwardRequestCount),
        ConsumedRequestCount = Volatile.Read(ref consumedRequestCount),
        ConsumedWithoutSelectionCount = Volatile.Read(ref consumedWithoutSelectionCount),
        VanillaPassThroughCount = Volatile.Read(ref vanillaPassThroughCount),
    };

    internal void Start()
    {
        if (started || disposed) return;
        started = true;

        try
        {
            targetCycleHook?.Enable();
            targetingKeybindsHook?.Enable();
            diagnostics = diagnostics with
            {
                HookAvailable = HooksAvailable,
                Started = true,
                LastEvent = HooksAvailable
                    ? "Native world forward-target override ready"
                    : "Native target-cycle hook unavailable; vanilla targeting preserved",
            };
        }
        catch (Exception exception)
        {
            try
            {
                targetingKeybindsHook?.Disable();
            }
            catch (Exception disableException)
            {
                LogFailure(disableException, "Smart Tab targeting-handler hook rollback failed");
            }

            try
            {
                targetCycleHook?.Disable();
            }
            catch (Exception disableException)
            {
                LogFailure(disableException, "Smart Tab target-cycle hook rollback failed");
            }

            started = false;
            diagnostics = diagnostics with
            {
                HookAvailable = false,
                Started = false,
                LastEvent = "Native target hooks could not start; vanilla targeting preserved",
            };
            LogFailure(exception, "Smart Tab native target hook could not start; FFXIV targeting remains vanilla");
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        started = false;
        try
        {
            targetingKeybindsHook?.Dispose();
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Smart Tab targeting-handler hook disposal failed");
        }

        try
        {
            targetCycleHook?.Dispose();
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Smart Tab target-cycle hook disposal failed");
        }
    }

    private void HandleTargetingKeybindsDetour(TargetSystem* targetSystem)
    {
        nativeTargetingHandlerDepth++;
        try
        {
            targetingKeybindsHook!.Original(targetSystem);
        }
        finally
        {
            nativeTargetingHandlerDepth--;
        }
    }

    private byte NativeWorldTargetCycleDetour(
        TargetSystem* targetSystem,
        nint targetingContext,
        GameObjectArray* candidateArray,
        byte reverse)
    {
        if (nativeTargetingHandlerDepth <= 0 || reverse != 0)
        {
            Interlocked.Increment(ref vanillaPassThroughCount);
            return targetCycleHook!.Original(
                targetSystem,
                targetingContext,
                candidateArray,
                reverse);
        }

        Interlocked.Increment(ref nativeForwardRequestCount);
        var owned = false;
        try
        {
            var localPlayer = objectTable.LocalPlayer;
            var localPlayerAvailable = IsLiveTarget(localPlayer);
            var localJobId = localPlayerAvailable && localPlayer!.ClassJob.IsValid
                ? localPlayer.ClassJob.RowId
                : 0;
            var observation = new SmartTabInterceptionObservation(
                configuration.Enabled,
                configuration.EnableSmartTabTargeting,
                HookAvailable: HooksAvailable,
                InsideNativeTargetingHandler: nativeTargetingHandlerDepth > 0,
                ExactCrystallineConflict:
                    IsExactCrystallineConflictContext(executeTracker.Diagnostics),
                ReviewedMeleeJob: SmartTargetReachRules.IsReviewedMeleeJob(localJobId),
                LocalPlayerAvailable: localPlayerAvailable,
                NativeWorldForwardCycle: true);
            if (!SmartTabInterceptionRules.ShouldConsumeNativeForwardTarget(observation))
            {
                Interlocked.Increment(ref vanillaPassThroughCount);
                return targetCycleHook!.Original(
                    targetSystem,
                    targetingContext,
                    candidateArray,
                    reverse);
            }

            owned = true;
            Interlocked.Increment(ref consumedRequestCount);
            if (SelectBestTargetOnce(targetSystem))
                return 1;

            Interlocked.Increment(ref consumedWithoutSelectionCount);
            return 0;
        }
        catch (Exception exception)
        {
            if (owned)
            {
                LogFailure(exception, "Smart Tab owned forward-target request failed closed; no fallback was attempted");
                Interlocked.Increment(ref consumedWithoutSelectionCount);
                return 0;
            }

            LogFailure(exception, "Smart Tab native forward-target gate failed; this request remains vanilla");
            Interlocked.Increment(ref vanillaPassThroughCount);
            return targetCycleHook!.Original(
                targetSystem,
                targetingContext,
                candidateArray,
                reverse);
        }
    }

    private bool HooksAvailable =>
        started &&
        !disposed &&
        targetingKeybindsHook?.IsEnabled == true &&
        targetCycleHook?.IsEnabled == true;

    private bool SelectBestTargetOnce(TargetSystem* targetSystem)
    {
        Interlocked.Increment(ref requestCount);
        var configured = configuration.Enabled && configuration.EnableSmartTabTargeting;
        if (!configured)
        {
            return Reject(
                SmartTabTargetingOutcome.Disabled,
                configured,
                false,
                "Smart Tab ignored: feature disabled");
        }

        try
        {
            var diagnosticsBefore = executeTracker.Diagnostics;
            if (!IsExactCrystallineConflictContext(diagnosticsBefore))
            {
                return Reject(
                    SmartTabTargetingOutcome.NotCrystallineConflict,
                    configured,
                    false,
                    "Smart Tab ignored: exact Crystalline Conflict context unavailable");
            }

            var localPlayer = objectTable.LocalPlayer;
            if (!IsLiveTarget(localPlayer))
            {
                return Reject(
                    SmartTabTargetingOutcome.LocalPlayerUnavailable,
                    configured,
                    true,
                    "Smart Tab ignored: local player unavailable");
            }

            var local = localPlayer!;
            var localJobId = local.ClassJob.IsValid ? local.ClassJob.RowId : 0;
            if (!SmartTargetReachRules.IsReviewedMeleeJob(localJobId))
            {
                return Reject(
                    SmartTabTargetingOutcome.UnsupportedJob,
                    configured,
                    true,
                    "Smart Tab ignored: current job is not a reviewed melee DPS",
                    localJobId);
            }

            var localActor = new TargetPressureActorIdentity(local.GameObjectId, local.EntityId);
            var partyEntityIds = GetPartyEntityIds();
            var seenGameObjectIds = new HashSet<ulong>();
            var seenEntityIds = new HashSet<uint>();
            var candidates = new List<SmartTabRuntimeCandidate>(EnemySlotRules.LastSlot);
            var resolvedSlotCount = 0;
            var nowMilliseconds = Environment.TickCount64;

            for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
            {
                var enemy = EnemySlotResolver.Resolve(objectTable, slot);
                if (!HasValidIdentity(enemy)) continue;
                resolvedSlotCount++;

                if (SharesEitherId(enemy!, local) || IsAlly(enemy!, partyEntityIds))
                {
                    return Reject(
                        SmartTabTargetingOutcome.CanonicalIdentityRejected,
                        configured,
                        true,
                        $"Smart Tab failed closed: S{slot} was not a hostile canonical enemy",
                        localJobId,
                        resolvedSlotCount,
                        candidates.Count);
                }

                if (!seenGameObjectIds.Add(enemy!.GameObjectId) ||
                    !seenEntityIds.Add(enemy.EntityId))
                {
                    return Reject(
                        SmartTabTargetingOutcome.AmbiguousCanonicalIdentity,
                        configured,
                        true,
                        "Smart Tab failed closed: canonical enemy identities were ambiguous",
                        localJobId,
                        resolvedSlotCount,
                        candidates.Count);
                }

                if (!TryCreateCandidate(
                        slot,
                        local,
                        localActor,
                        localJobId,
                        enemy,
                        nowMilliseconds,
                        out var candidate))
                {
                    continue;
                }

                candidates.Add(new SmartTabRuntimeCandidate(
                    enemy,
                    enemy.Address,
                    candidate));
            }

            var selectionCandidates = candidates
                .Select(static candidate => candidate.Selection)
                .ToArray();
            if (!SmartTabSelectionRules.TryCreateIntent(
                    selectionCandidates,
                    localActor,
                    out var intent))
            {
                return Reject(
                    SmartTabTargetingOutcome.NoEligibleTarget,
                    configured,
                    true,
                    "Smart Tab left the current target unchanged: no exact reachable enemy",
                    localJobId,
                    resolvedSlotCount,
                    candidates.Count);
            }

            SmartTabRuntimeCandidate? frozen = null;
            foreach (var candidate in candidates)
            {
                if (candidate.Selection.EnemySlot != intent.EnemySlot ||
                    candidate.Selection.Actor != intent.Target)
                {
                    continue;
                }

                if (frozen is not null)
                {
                    return Reject(
                        SmartTabTargetingOutcome.AmbiguousCanonicalIdentity,
                        configured,
                        true,
                        "Smart Tab failed closed: selected actor was not unique",
                        localJobId,
                        resolvedSlotCount,
                        candidates.Count);
                }

                frozen = candidate;
            }

            if (frozen is not { } exactIntent)
            {
                return Reject(
                    SmartTabTargetingOutcome.FrozenTargetChanged,
                    configured,
                    true,
                    "Smart Tab failed closed: selected actor disappeared before revalidation",
                    localJobId,
                    resolvedSlotCount,
                    candidates.Count,
                    intent.EnemySlot,
                    intent.Target);
            }

            var exactTarget = EnemySlotResolver.Resolve(objectTable, intent.EnemySlot);
            var tableTarget = HasValidIdentity(exactTarget)
                ? objectTable.SearchByEntityId(exactTarget!.EntityId) as IPlayerCharacter
                : null;
            if (!HasValidIdentity(exactTarget) ||
                !HasValidIdentity(tableTarget) ||
                exactTarget!.Address != exactIntent.Address ||
                exactTarget.GameObjectId != intent.Target.GameObjectId ||
                exactTarget.EntityId != intent.Target.EntityId ||
                tableTarget!.Address != exactTarget.Address ||
                tableTarget.GameObjectId != exactTarget.GameObjectId ||
                tableTarget.EntityId != exactTarget.EntityId ||
                SharesEitherId(exactTarget, local) ||
                IsAlly(exactTarget, partyEntityIds) ||
                !TryCreateCandidate(
                    intent.EnemySlot,
                    local,
                    localActor,
                    localJobId,
                    exactTarget,
                    Environment.TickCount64,
                    out var revalidated) ||
                revalidated.ReachTier != exactIntent.Selection.ReachTier ||
                !SmartTabSelectionRules.CanSetExactIntent(intent, revalidated, localActor))
            {
                return Reject(
                    SmartTabTargetingOutcome.FrozenTargetChanged,
                    configured,
                    true,
                    "Smart Tab left the current target unchanged: frozen actor or reach changed",
                    localJobId,
                    resolvedSlotCount,
                    candidates.Count,
                    intent.EnemySlot,
                    intent.Target,
                    exactIntent.Selection.ReachTier);
            }

            var diagnosticsAfter = executeTracker.Diagnostics;
            if (!ReferenceEquals(diagnosticsBefore, diagnosticsAfter) ||
                !IsExactCrystallineConflictContext(diagnosticsAfter))
            {
                return Reject(
                    SmartTabTargetingOutcome.ContextChanged,
                    configured,
                    false,
                    "Smart Tab left the current target unchanged: context snapshot changed",
                    localJobId,
                    resolvedSlotCount,
                    candidates.Count,
                    intent.EnemySlot,
                    intent.Target,
                    exactIntent.Selection.ReachTier);
            }

            if (targetSystem == null)
            {
                return Fail(
                    SmartTabTargetingOutcome.ContextChanged,
                    configured,
                    true,
                    "Smart Tab left the current target unchanged: native target system unavailable",
                    localJobId,
                    resolvedSlotCount,
                    candidates.Count,
                    intent.EnemySlot,
                    intent.Target,
                    exactIntent.Selection.ReachTier);
            }

            Interlocked.Increment(ref setterInvocationCount);
            var setterAccepted = targetSystem->SetHardTarget((GameObject*)exactTarget.Address);
            if (!setterAccepted)
            {
                return Fail(
                    SmartTabTargetingOutcome.SetterRejected,
                    configured,
                    true,
                    "Smart Tab native hard-target setter rejected the frozen actor; no retry was attempted",
                    localJobId,
                    resolvedSlotCount,
                    candidates.Count,
                    intent.EnemySlot,
                    intent.Target,
                    exactIntent.Selection.ReachTier);
            }

            if ((nint)targetSystem->GetHardTarget() != exactTarget.Address)
            {
                return Fail(
                    SmartTabTargetingOutcome.SetterReadbackMismatch,
                    configured,
                    true,
                    "Smart Tab target setter did not confirm the frozen actor; no retry was attempted",
                    localJobId,
                    resolvedSlotCount,
                    candidates.Count,
                    intent.EnemySlot,
                    intent.Target,
                    exactIntent.Selection.ReachTier);
            }

            Interlocked.Increment(ref exactReadbackCount);
            Publish(
                configured,
                true,
                localJobId,
                resolvedSlotCount,
                candidates.Count,
                intent.EnemySlot,
                intent.Target,
                exactIntent.Selection.ReachTier,
                SmartTabTargetingOutcome.Selected,
                $"Selected S{intent.EnemySlot} through the native hard-target setter");
            return true;
        }
        catch (Exception exception)
        {
            LogFailure(exception, "Smart Tab selection failed closed");
            return Fail(
                SmartTabTargetingOutcome.FailedClosed,
                configured,
                false,
                "Smart Tab failed closed after an unexpected error; no retry was attempted");
        }
    }

    private bool TryCreateCandidate(
        int enemySlot,
        IPlayerCharacter localPlayer,
        TargetPressureActorIdentity localActor,
        uint localJobId,
        IPlayerCharacter enemy,
        long nowMilliseconds,
        out SmartTabSelectionCandidate candidate)
    {
        candidate = default;
        if (!HasValidIdentity(enemy) ||
            !SmartTargetReachRules.TryResolveReachTier(
                localJobId,
                localPlayer.Position,
                localPlayer.HitboxRadius,
                enemy.Position,
                enemy.HitboxRadius,
                out var reachTier))
        {
            return false;
        }

        var actor = new TargetPressureActorIdentity(enemy.GameObjectId, enemy.EntityId);
        int? freshTeamPressure = pressureTracker.TryGetFreshTeamTargetCount(
            localActor,
            actor,
            nowMilliseconds,
            MaximumPressureAgeMilliseconds,
            out var teamPressure)
            ? teamPressure
            : null;

        var guardAvailability = GuardAvailability.Unknown;
        var hasTrustedMp = false;
        var currentMp = 0u;
        var maximumMp = 0u;
        var exactHudMatches = executeTracker.Enemies
            .Where(snapshot =>
                snapshot.Slot == enemySlot &&
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
            hasTrustedMp = hud.HasTrustedMp &&
                           hud.MaxMp > 0 &&
                           hud.CurrentMp <= hud.MaxMp;
            if (hasTrustedMp)
            {
                currentMp = hud.CurrentMp;
                maximumMp = hud.MaxMp;
            }
        }

        candidate = new SmartTabSelectionCandidate(
            enemySlot,
            actor,
            ExactCanonicalIdentity: true,
            IsHostile: true,
            Alive: IsLiveTarget(enemy),
            Targetable: enemy.IsTargetable,
            HasActiveGuard: HasActiveGuardStatus(enemy),
            enemy.CurrentHp,
            enemy.MaxHp,
            reachTier,
            freshTeamPressure,
            guardAvailability,
            hasTrustedMp,
            currentMp,
            maximumMp);
        return true;
    }

    private HashSet<uint> GetPartyEntityIds() => partyList
        .Select(member => member.EntityId)
        .Where(IsNetworkEntityId)
        .ToHashSet();

    private bool IsExactCrystallineConflictContext(TrackerDiagnostics tracker) =>
        tracker.Active &&
        tracker.IsPvP &&
        tracker.IsCrystallineConflict &&
        !tracker.IsWolvesDen &&
        tracker.TerritoryId != 0 &&
        tracker.TerritoryId == clientState.TerritoryType &&
        tracker.SlotCapacity == EnemySlotRules.LastSlot &&
        ResolveContext() == SupportedPvPContext.CrystallineConflict;

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

    private static bool IsLiveTarget(IPlayerCharacter? player) =>
        HasValidIdentity(player) &&
        player!.IsTargetable &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool HasValidIdentity(IGameObject? gameObject) =>
        gameObject is not null &&
        gameObject.Address != nint.Zero &&
        gameObject.IsValid() &&
        IsNetworkObjectId(gameObject.GameObjectId) &&
        IsNetworkEntityId(gameObject.EntityId);

    private static bool SharesEitherId(IGameObject left, IGameObject right) =>
        left.GameObjectId == right.GameObjectId || left.EntityId == right.EntityId;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000UL and not ulong.MaxValue;

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000u and not uint.MaxValue;

    private bool Reject(
        SmartTabTargetingOutcome outcome,
        bool configured,
        bool isCrystallineConflict,
        string lastEvent,
        uint localJobId = 0,
        int resolvedSlotCount = 0,
        int candidateCount = 0,
        int selectedEnemySlot = 0,
        TargetPressureActorIdentity selectedActor = default,
        SmartTargetReachTier selectedReachTier = SmartTargetReachTier.RangedOrOther)
    {
        Interlocked.Increment(ref rejectedCount);
        Publish(
            configured,
            isCrystallineConflict,
            localJobId,
            resolvedSlotCount,
            candidateCount,
            selectedEnemySlot,
            selectedActor,
            selectedReachTier,
            outcome,
            lastEvent);
        return false;
    }

    private bool Fail(
        SmartTabTargetingOutcome outcome,
        bool configured,
        bool isCrystallineConflict,
        string lastEvent,
        uint localJobId = 0,
        int resolvedSlotCount = 0,
        int candidateCount = 0,
        int selectedEnemySlot = 0,
        TargetPressureActorIdentity selectedActor = default,
        SmartTargetReachTier selectedReachTier = SmartTargetReachTier.RangedOrOther)
    {
        Interlocked.Increment(ref terminalFailureCount);
        Publish(
            configured,
            isCrystallineConflict,
            localJobId,
            resolvedSlotCount,
            candidateCount,
            selectedEnemySlot,
            selectedActor,
            selectedReachTier,
            outcome,
            lastEvent);
        return false;
    }

    private void Publish(
        bool configured,
        bool isCrystallineConflict,
        uint localJobId,
        int resolvedSlotCount,
        int candidateCount,
        int selectedEnemySlot,
        TargetPressureActorIdentity selectedActor,
        SmartTargetReachTier selectedReachTier,
        SmartTabTargetingOutcome outcome,
        string lastEvent)
    {
        Volatile.Write(ref diagnostics, new SmartTabTargetingDiagnostics(
            HooksAvailable,
            started && !disposed,
            configured,
            isCrystallineConflict,
            localJobId,
            resolvedSlotCount,
            candidateCount,
            selectedEnemySlot,
            selectedActor.GameObjectId,
            selectedActor.EntityId,
            selectedReachTier,
            outcome,
            Volatile.Read(ref requestCount),
            Volatile.Read(ref setterInvocationCount),
            Volatile.Read(ref exactReadbackCount),
            Volatile.Read(ref rejectedCount),
            Volatile.Read(ref terminalFailureCount),
            Volatile.Read(ref nativeForwardRequestCount),
            Volatile.Read(ref consumedRequestCount),
            Volatile.Read(ref consumedWithoutSelectionCount),
            Volatile.Read(ref vanillaPassThroughCount),
            lastEvent));
    }

    private void LogFailure(Exception exception, string message)
    {
        var now = Environment.TickCount64;
        if (now < nextErrorLogAtMilliseconds) return;
        nextErrorLogAtMilliseconds = now + 10_000;
        log.Error(exception, $"Seiton Sense {message}.");
    }

    private readonly record struct SmartTabRuntimeCandidate(
        IPlayerCharacter Player,
        nint Address,
        SmartTabSelectionCandidate Selection);
}
