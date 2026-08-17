using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using GameAction = Lumina.Excel.Sheets.Action;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace SeitonSense.Plugin.Services;

internal enum SmartWardensPaeanInterceptKind
{
    Vanilla = 0,
    Redirect = 1,
    Suppress = 2,
}

internal readonly record struct SmartWardensPaeanInterceptResult(
    SmartWardensPaeanInterceptKind Kind,
    ulong ForwardTargetId,
    int PartySlot,
    int IncomingEnemyCount,
    string Reason)
{
    internal bool ShouldRedirect => Kind == SmartWardensPaeanInterceptKind.Redirect;
    internal bool ShouldSuppress => Kind == SmartWardensPaeanInterceptKind.Suppress;

    internal static SmartWardensPaeanInterceptResult Vanilla(
        ulong originalTargetId,
        string reason) =>
        new(
            SmartWardensPaeanInterceptKind.Vanilla,
            originalTargetId,
            0,
            0,
            reason);
}

internal readonly record struct SmartWardensPaeanDiagnostics(
    bool Configured,
    bool MetadataVerified,
    bool ActiveInCurrentContext,
    long EvaluatedCalls,
    long VanillaCalls,
    long RedirectedCalls,
    long SuppressedCalls,
    long ClientAcceptedRedirects,
    int LastPartySlot,
    int LastIncomingEnemyCount,
    ulong LastOriginalTargetId,
    ulong LastForwardTargetId,
    uint LastInvocationMode,
    string LastEvent)
{
    internal string ToChatLine() =>
        $"configured={Configured},meta={MetadataVerified},active={ActiveInCurrentContext}," +
        $"eval={EvaluatedCalls},vanilla={VanillaCalls},redirect={RedirectedCalls}," +
        $"suppress={SuppressedCalls},accepted={ClientAcceptedRedirects}," +
        $"p={LastPartySlot},pressure={LastIncomingEnemyCount}," +
        $"target={LastOriginalTargetId:X}/{LastForwardTargetId:X},mode={LastInvocationMode}," +
        $"last={LastEvent}";
}

/// <summary>
/// Reviews only an already incoming Warden's Paean call. A successful decision
/// changes only that call's target argument; the service never invokes an
/// action, mutates a selected target, stores deferred work, or retries.
/// </summary>
internal sealed unsafe class SmartWardensPaeanService
{
    internal const uint WardensPaeanIconId = 9_628;
    internal const uint WardensPaeanWardIconId = 212_611;

    private const ushort ExpectedRecast100ms = 240;
    private const int ExpectedRange = 30;

    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;
    private readonly IDutyState dutyState;
    private readonly IDataManager dataManager;
    private readonly TargetPressureTracker pressureTracker;
    private readonly IPluginLog log;
    private readonly bool metadataVerified;
    private readonly object diagnosticsGate = new();

    private long evaluatedCalls;
    private long vanillaCalls;
    private long redirectedCalls;
    private long suppressedCalls;
    private long clientAcceptedRedirects;
    private long nextErrorLogAt;
    private int lastPartySlot;
    private int lastIncomingEnemyCount;
    private ulong lastOriginalTargetId;
    private ulong lastForwardTargetId;
    private uint lastInvocationMode;
    private string lastEvent = "Not started";

    internal SmartWardensPaeanService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IPartyList partyList,
        IDutyState dutyState,
        IDataManager dataManager,
        TargetPressureTracker pressureTracker,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
        this.dutyState = dutyState;
        this.dataManager = dataManager;
        this.pressureTracker = pressureTracker;
        this.log = log;
        metadataVerified = ValidateMetadata();

        if (!metadataVerified)
        {
            log.Warning(
                "Seiton Sense Smart Paean metadata validation failed; incoming Paean calls remain vanilla.");
        }
    }

    internal SmartWardensPaeanDiagnostics Diagnostics
    {
        get
        {
            lock (diagnosticsGate)
            {
                return new SmartWardensPaeanDiagnostics(
                    configuration.Enabled &&
                    configuration.EnableBardWardensPaeanPressureRedirect,
                    metadataVerified,
                    ResolveContext() == SupportedPvPContext.CrystallineConflict,
                    Interlocked.Read(ref evaluatedCalls),
                    Interlocked.Read(ref vanillaCalls),
                    Interlocked.Read(ref redirectedCalls),
                    Interlocked.Read(ref suppressedCalls),
                    Interlocked.Read(ref clientAcceptedRedirects),
                    lastPartySlot,
                    lastIncomingEnemyCount,
                    lastOriginalTargetId,
                    lastForwardTargetId,
                    lastInvocationMode,
                    lastEvent);
            }
        }
    }

    internal SmartWardensPaeanInterceptResult Evaluate(
        ActionManager* actionManager,
        ActionType actionType,
        uint rawActionId,
        ulong originalTargetId,
        ActionManager.UseActionMode mode,
        bool localGuardActiveOrPropagating)
    {
        var resolvedActionId = ResolveActionId(actionManager, actionType, rawActionId);
        if (resolvedActionId != SmartWardensPaeanTargetRules.ActionId)
        {
            return SmartWardensPaeanInterceptResult.Vanilla(
                originalTargetId,
                "Not Warden's Paean");
        }

        Interlocked.Increment(ref evaluatedCalls);
        var redirectCommitted = false;
        try
        {
            if (!IsRecognizedInvocation(actionType, mode))
            {
                return RecordVanilla(
                    originalTargetId,
                    mode,
                    "Unsupported invocation mode");
            }

            if (localGuardActiveOrPropagating)
            {
                return RecordVanilla(
                    originalTargetId,
                    mode,
                    "Own Guard active or propagating");
            }

            var localPlayer = objectTable.LocalPlayer;
            var localIdentity = TryGetExactIdentity(localPlayer, out var identity)
                ? identity
                : default;
            var localAlive = IsAlive(localPlayer);
            var localJobId = localPlayer?.ClassJob.IsValid == true
                ? localPlayer.ClassJob.RowId
                : 0;
            var configurationEnabled = configuration.Enabled &&
                                       configuration.EnableBardWardensPaeanPressureRedirect;
            var isCrystallineConflict =
                ResolveContext() == SupportedPvPContext.CrystallineConflict;

            var capture = configurationEnabled &&
                          isCrystallineConflict &&
                          localIdentity.IsValid &&
                          localAlive &&
                          localJobId == SmartWardensPaeanTargetRules.BardJobId &&
                          metadataVerified
                ? CaptureExactParty(localPlayer!)
                : ExactPartyCapture.Incomplete;
            var candidates = capture.Members
                .Select(static member => member.Candidate)
                .ToArray();
            var decision = SmartWardensPaeanTargetRules.Observe(
                new SmartWardensPaeanObservation(
                    configurationEnabled,
                    isCrystallineConflict,
                    localJobId,
                    localIdentity,
                    localAlive,
                    metadataVerified,
                    resolvedActionId,
                    capture.Complete,
                    candidates));
            if (!decision.ShouldRedirect ||
                decision.Intent is not { } intent)
            {
                return RecordVanilla(
                    originalTargetId,
                    mode,
                    decision.Reason.ToString());
            }

            // From this point the exact redirect decision is committed. Any
            // last-moment uncertainty suppresses this call; it must never fall
            // back to the caller's old target or select another party member.
            redirectCommitted = true;
            if (decision.SelectedCandidateIndex < 0 ||
                decision.SelectedCandidateIndex >= capture.Members.Count)
            {
                return RecordSuppressed(
                    originalTargetId,
                    mode,
                    intent.PartySlot,
                    intent.SelectedIncomingEnemyCount,
                    "Selected candidate index drifted");
            }

            var selected = capture.Members[decision.SelectedCandidateIndex];
            var currentResolvedActionId = ResolveActionId(
                actionManager,
                actionType,
                rawActionId);
            var currentLocal = objectTable.LocalPlayer;
            if (!TryGetExactIdentity(currentLocal, out var currentLocalIdentity) ||
                currentLocalIdentity != intent.LocalPlayer ||
                currentLocal!.Address != capture.LocalAddress ||
                HasLiveGuard(currentLocal))
            {
                return RecordSuppressed(
                    originalTargetId,
                    mode,
                    intent.PartySlot,
                    intent.SelectedIncomingEnemyCount,
                    "Local identity or Guard changed after selection");
            }

            var currentTarget = PartySlotResolver.Resolve(objectTable, intent.PartySlot);
            if (!TryGetExactIdentity(currentTarget, out var currentTargetIdentity) ||
                currentTargetIdentity != intent.Target ||
                currentTarget!.Address != selected.Address)
            {
                return RecordSuppressed(
                    originalTargetId,
                    mode,
                    intent.PartySlot,
                    intent.SelectedIncomingEnemyCount,
                    "Frozen party target identity changed");
            }

            var pressureViewActive = pressureTracker.TryCaptureIncomingAllyPressure(
                out var pressureCounts);
            var currentCandidate = BuildCandidate(
                currentLocal,
                currentTarget,
                intent.PartySlot,
                pressureViewActive,
                pressureCounts).Candidate;
            var currentConfigurationEnabled = configuration.Enabled &&
                                              configuration.EnableBardWardensPaeanPressureRedirect;
            var currentContext =
                ResolveContext() == SupportedPvPContext.CrystallineConflict;
            var currentJobId = currentLocal.ClassJob.IsValid
                ? currentLocal.ClassJob.RowId
                : 0;
            if (!SmartWardensPaeanTargetRules.CanUseFrozenIntent(
                    intent,
                    currentCandidate,
                    currentConfigurationEnabled,
                    currentContext,
                    currentJobId,
                    currentLocalIdentity,
                    IsAlive(currentLocal),
                    metadataVerified,
                    currentResolvedActionId))
            {
                return RecordSuppressed(
                    originalTargetId,
                    mode,
                    intent.PartySlot,
                    intent.SelectedIncomingEnemyCount,
                    "Frozen Paean intent failed final preflight");
            }

            return RecordRedirect(
                originalTargetId,
                currentTarget.GameObjectId,
                mode,
                intent.PartySlot,
                currentCandidate.UniqueIncomingEnemyCount);
        }
        catch (Exception exception)
        {
            LogFailure(exception);
            return redirectCommitted
                ? RecordSuppressed(
                    originalTargetId,
                    mode,
                    0,
                    0,
                    "Runtime exception after redirect selection")
                : RecordVanilla(
                    originalTargetId,
                    mode,
                    "Runtime exception before redirect selection");
        }
    }

    internal void RecordNativeResult(
        SmartWardensPaeanInterceptResult result,
        bool clientAccepted)
    {
        if (!result.ShouldRedirect) return;
        if (clientAccepted) Interlocked.Increment(ref clientAcceptedRedirects);
        lock (diagnosticsGate)
        {
            lastEvent = clientAccepted
                ? $"Client accepted redirected Paean to p{result.PartySlot}"
                : $"Client rejected redirected Paean to p{result.PartySlot}";
        }
    }

    private ExactPartyCapture CaptureExactParty(IPlayerCharacter localPlayer)
    {
        var partyBefore = CaptureExactPartyEntityIds();
        if (partyBefore is null || !partyBefore.Contains(localPlayer.EntityId))
            return ExactPartyCapture.Incomplete;

        var pressureViewActive = pressureTracker.TryCaptureIncomingAllyPressure(
            out var pressureCounts);
        var members = new List<RuntimeCandidate>(
            SmartWardensPaeanTargetRules.RequiredCrystallineConflictPartySize);
        for (var slot = SmartWardensPaeanTargetRules.FirstPartySlot;
             slot <= SmartWardensPaeanTargetRules.LastPartySlot;
             slot++)
        {
            var player = PartySlotResolver.Resolve(objectTable, slot);
            if (!TryGetExactIdentity(player, out _)) continue;
            members.Add(BuildCandidate(
                localPlayer,
                player!,
                slot,
                pressureViewActive,
                pressureCounts));
        }

        var partyAfter = CaptureExactPartyEntityIds();
        var complete = partyAfter is not null &&
                       partyBefore.SetEquals(partyAfter) &&
                       members.Count ==
                       SmartWardensPaeanTargetRules.RequiredCrystallineConflictPartySize &&
                       members.Select(static member => member.Candidate.Actor.EntityId)
                           .ToHashSet()
                           .SetEquals(partyBefore) &&
                       members.Select(static member => member.Address).Distinct().Count() ==
                       members.Count &&
                       PartySlotsRemainExact(members);
        return complete
            ? new ExactPartyCapture(true, localPlayer.Address, members)
            : ExactPartyCapture.Incomplete;
    }

    private bool PartySlotsRemainExact(IReadOnlyList<RuntimeCandidate> members)
    {
        foreach (var member in members)
        {
            var stablePlayer = PartySlotResolver.Resolve(
                objectTable,
                member.Candidate.PartySlot);
            if (!TryGetExactIdentity(stablePlayer, out var stableIdentity) ||
                stableIdentity != member.Candidate.Actor ||
                stablePlayer!.Address != member.Address)
            {
                return false;
            }
        }

        return true;
    }

    private RuntimeCandidate BuildCandidate(
        IPlayerCharacter localPlayer,
        IPlayerCharacter target,
        int partySlot,
        bool pressureViewActive,
        IReadOnlyDictionary<TargetPressureActorIdentity, int> pressureCounts)
    {
        var localIdentity = new TargetPressureActorIdentity(
            localPlayer.GameObjectId,
            localPlayer.EntityId);
        var targetIdentity = new TargetPressureActorIdentity(
            target.GameObjectId,
            target.EntityId);
        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        var nativeTargetValid = sourceObject != null && targetObject != null;
        var rangeResult = nativeTargetValid
            ? ActionManager.GetActionInRangeOrLoS(
                SmartWardensPaeanTargetRules.ActionId,
                sourceObject,
                targetObject)
            : uint.MaxValue;
        var incomingEnemyCount = 0;
        var pressureKnown = pressureViewActive &&
                            pressureCounts.TryGetValue(
                                targetIdentity,
                                out incomingEnemyCount) &&
                            incomingEnemyCount >= 0;
        if (!pressureKnown) incomingEnemyCount = 0;

        var candidate = new SmartWardensPaeanCandidate(
            partySlot,
            targetIdentity,
            ExactPartyIdentity: targetIdentity.IsValid,
            IsSelf: targetIdentity == localIdentity,
            Alive: IsAlive(target),
            target.IsTargetable,
            HasWardensPaeanWard: HasLiveWardensPaeanWard(target),
            target.CurrentHp,
            target.MaxHp,
            nativeTargetValid,
            SeitonRangeRules.HasNativeRangeAndLineOfSight(rangeResult),
            pressureKnown,
            incomingEnemyCount);
        return new RuntimeCandidate(candidate, target.Address);
    }

    private HashSet<uint>? CaptureExactPartyEntityIds()
    {
        var ids = partyList
            .Select(static member => member.EntityId)
            .ToArray();
        if (ids.Length !=
            SmartWardensPaeanTargetRules.RequiredCrystallineConflictPartySize ||
            ids.Any(static entityId => !IsNetworkEntityId(entityId)))
        {
            return null;
        }

        var unique = ids.ToHashSet();
        return unique.Count == ids.Length ? unique : null;
    }

    private uint ResolveActionId(
        ActionManager* actionManager,
        ActionType actionType,
        uint rawActionId)
    {
        if (actionManager == null || rawActionId == 0) return 0;
        if (actionType == ActionType.Action)
            return actionManager->GetAdjustedActionId(rawActionId);
        if (actionType != ActionType.PvPAction) return 0;

        var pvpActions = dataManager.GetExcelSheet<PvPAction>();
        if (pvpActions.TryGetRow(rawActionId, out var pvpAction) &&
            pvpAction.Action.IsValid)
        {
            return pvpAction.Action.RowId;
        }

        var actions = dataManager.GetExcelSheet<GameAction>();
        return actions.TryGetRow(rawActionId, out var action) && action.IsPvP
            ? rawActionId
            : 0;
    }

    private bool ValidateMetadata()
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var descriptions =
                dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<GameStatus>(ClientLanguage.English);
            if (!actions.TryGetRow(SmartWardensPaeanTargetRules.ActionId, out var action) ||
                !descriptions.TryGetRow(
                    SmartWardensPaeanTargetRules.ActionId,
                    out var description) ||
                !statuses.TryGetRow(
                    SmartWardensPaeanTargetRules.WardensPaeanWardStatusId,
                    out var ward))
            {
                return false;
            }

            var actionDescription = description.Description.ToString();
            var wardDescription = ward.Description.ToString();
            return string.Equals(
                       action.Name.ToString(),
                       "The Warden's Paean",
                       StringComparison.OrdinalIgnoreCase) &&
                   action.Icon == WardensPaeanIconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == SmartWardensPaeanTargetRules.BardJobId &&
                   action.Range == ExpectedRange &&
                   action.EffectRange == 0 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == ExpectedRecast100ms &&
                   action.CanTargetSelf &&
                   action.CanTargetParty &&
                   !action.CanTargetAlly &&
                   !action.CanTargetAlliance &&
                   !action.CanTargetHostile &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight &&
                   actionDescription.Contains("Removes", StringComparison.Ordinal) &&
                   actionDescription.Contains("Purify", StringComparison.Ordinal) &&
                   actionDescription.Contains("barrier", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       ward.Name.ToString(),
                       "The Warden's Paean",
                       StringComparison.OrdinalIgnoreCase) &&
                   ward.Icon == WardensPaeanWardIconId &&
                   ward.StatusCategory == 1 &&
                   wardDescription.Contains("Purify", StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            LogFailure(exception);
            return false;
        }
    }

    private SupportedPvPContext ResolveContext()
    {
        var condition = dutyState.ContentFinderCondition;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            includeWolvesDenTesting: false,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private bool TryGetExactIdentity(
        IPlayerCharacter? player,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (player is null ||
            player.Address == nint.Zero ||
            !IsNetworkObjectId(player.GameObjectId) ||
            !IsNetworkEntityId(player.EntityId))
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        if (native == null || native->EntityId != player.EntityId) return false;
        var tablePlayer = objectTable.SearchByEntityId(player.EntityId) as IPlayerCharacter;
        if (tablePlayer is null ||
            tablePlayer.Address != player.Address ||
            tablePlayer.GameObjectId != player.GameObjectId ||
            tablePlayer.EntityId != player.EntityId)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            player.GameObjectId,
            player.EntityId);
        return identity.IsValid;
    }

    private static bool IsAlive(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp > 0 &&
        player.CurrentHp <= player.MaxHp;

    private static bool HasLiveGuard(IPlayerCharacter player)
    {
        foreach (var status in player.StatusList)
        {
            if (ScholarCriticalStrategyRules.IsExactGuardStatus(status.StatusId) &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLiveWardensPaeanWard(IPlayerCharacter player)
    {
        foreach (var status in player.StatusList)
        {
            if (SmartWardensPaeanTargetRules.IsWardensPaeanWardStatus(status.StatusId) &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject* GetNativeObject(IPlayerCharacter player)
    {
        var native = (GameObject*)player.Address;
        return native != null && native->EntityId == player.EntityId
            ? native
            : null;
    }

    private SmartWardensPaeanInterceptResult RecordVanilla(
        ulong originalTargetId,
        ActionManager.UseActionMode mode,
        string reason)
    {
        Interlocked.Increment(ref vanillaCalls);
        lock (diagnosticsGate)
        {
            lastPartySlot = 0;
            lastIncomingEnemyCount = 0;
            lastOriginalTargetId = originalTargetId;
            lastForwardTargetId = originalTargetId;
            lastInvocationMode = (uint)mode;
            lastEvent = reason;
        }

        return SmartWardensPaeanInterceptResult.Vanilla(originalTargetId, reason);
    }

    private SmartWardensPaeanInterceptResult RecordRedirect(
        ulong originalTargetId,
        ulong forwardTargetId,
        ActionManager.UseActionMode mode,
        int partySlot,
        int incomingEnemyCount)
    {
        Interlocked.Increment(ref redirectedCalls);
        lock (diagnosticsGate)
        {
            lastPartySlot = partySlot;
            lastIncomingEnemyCount = incomingEnemyCount;
            lastOriginalTargetId = originalTargetId;
            lastForwardTargetId = forwardTargetId;
            lastInvocationMode = (uint)mode;
            lastEvent = $"Redirected incoming Paean to p{partySlot}";
        }

        return new SmartWardensPaeanInterceptResult(
            SmartWardensPaeanInterceptKind.Redirect,
            forwardTargetId,
            partySlot,
            incomingEnemyCount,
            "Exact pressure redirect");
    }

    private SmartWardensPaeanInterceptResult RecordSuppressed(
        ulong originalTargetId,
        ActionManager.UseActionMode mode,
        int partySlot,
        int incomingEnemyCount,
        string reason)
    {
        Interlocked.Increment(ref suppressedCalls);
        lock (diagnosticsGate)
        {
            lastPartySlot = partySlot;
            lastIncomingEnemyCount = incomingEnemyCount;
            lastOriginalTargetId = originalTargetId;
            lastForwardTargetId = 0;
            lastInvocationMode = (uint)mode;
            lastEvent = reason;
        }

        return new SmartWardensPaeanInterceptResult(
            SmartWardensPaeanInterceptKind.Suppress,
            0,
            partySlot,
            incomingEnemyCount,
            reason);
    }

    private void LogFailure(Exception exception)
    {
        var now = Environment.TickCount64;
        lock (diagnosticsGate)
        {
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
        }

        try
        {
            log.Error(
                exception,
                "Seiton Sense Smart Paean evaluation failed with its bounded vanilla/suppress policy.");
        }
        catch
        {
            // Logging must never alter the incoming action path.
        }
    }

    private static bool IsRecognizedInvocation(
        ActionType actionType,
        ActionManager.UseActionMode mode) =>
        actionType is ActionType.Action or ActionType.PvPAction &&
        (mode is ActionManager.UseActionMode.None or
                 ActionManager.UseActionMode.Macro or
                 ActionManager.UseActionMode.Queue ||
         (uint)mode == 100);

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private readonly record struct RuntimeCandidate(
        SmartWardensPaeanCandidate Candidate,
        nint Address);

    private sealed record ExactPartyCapture(
        bool Complete,
        nint LocalAddress,
        IReadOnlyList<RuntimeCandidate> Members)
    {
        internal static ExactPartyCapture Incomplete { get; } =
            new(false, nint.Zero, []);
    }
}
