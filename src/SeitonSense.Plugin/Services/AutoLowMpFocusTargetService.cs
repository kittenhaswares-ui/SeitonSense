using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed record AutoLowMpFocusTargetDiagnostics(
    bool Configured,
    bool MetadataVerified,
    bool IsCrystallineConflict,
    bool TextInputStateKnown,
    bool TextInputActive,
    bool CompleteCanonicalEnemySet,
    AutoLowMpFocusObservedState FocusState,
    AutoLowMpFocusTargetDecisionKind Decision,
    AutoLowMpFocusTargetDecisionReason Reason,
    bool LowMpWaveActive,
    bool WaveSpent,
    bool ManualOverrideLatched,
    int CandidateCount,
    int EligibleCandidateCount,
    int SelectedEnemySlot,
    ulong SelectedGameObjectId,
    uint SelectedEntityId,
    uint SelectedCurrentMp,
    uint SelectedMaximumMp,
    uint LastNativeRangeResult,
    long SetterIntentCount,
    long SetterInvocationCount,
    long ExactReadbackCount,
    long TerminalFailureCount,
    string CandidateResolution,
    string LastEvent)
{
    internal static AutoLowMpFocusTargetDiagnostics Inactive(bool metadataVerified) => new(
        false,
        metadataVerified,
        false,
        false,
        true,
        false,
        AutoLowMpFocusObservedState.Unknown,
        AutoLowMpFocusTargetDecisionKind.None,
        AutoLowMpFocusTargetDecisionReason.None,
        false,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        uint.MaxValue,
        0,
        0,
        0,
        0,
        "Not evaluated",
        "Inactive");

    internal string ToChatLine() =>
        $"configured={Configured},meta={MetadataVerified},cc={IsCrystallineConflict}," +
        $"text={TextInputStateKnown}/{TextInputActive},complete={CompleteCanonicalEnemySet}," +
        $"focus={FocusState},decision={Decision}/{Reason},wave={LowMpWaveActive}/{WaveSpent}," +
        $"latched={ManualOverrideLatched},candidates={CandidateCount}/{EligibleCandidateCount}," +
        $"selected=S{SelectedEnemySlot}/{SelectedGameObjectId:X}/{SelectedEntityId:X}/" +
        $"{SelectedCurrentMp}/{SelectedMaximumMp},native={LastNativeRangeResult}," +
        $"set={SetterIntentCount}/{SetterInvocationCount}/{ExactReadbackCount}/{TerminalFailureCount}," +
        $"resolve={CandidateResolution},last={LastEvent}";
}

/// <summary>
/// Optionally fills an empty local Focus Target with one exact nearby CC enemy
/// after an independently trusted, debounced MP sample reaches 2,000 or lower.
/// This service never clears, replaces, restores, or retries a focus target.
/// </summary>
internal sealed class AutoLowMpFocusTargetService : IDisposable
{
    private const long UpdateIntervalMilliseconds = 100;

    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IDutyState dutyState;
    private readonly ITargetManager targetManager;
    private readonly IPluginLog log;
    private readonly PvPMetadataValidation metadata;
    private readonly ExecuteTracker executeTracker;

    private Dictionary<TargetPressureActorIdentity, LowMpState> lowMpStates = [];
    private AutoLowMpFocusTargetState state = AutoLowMpFocusTargetState.Initial;
    private AutoLowMpFocusTargetDiagnostics diagnostics;
    private uint activeTerritory;
    private ulong activeLocalGameObjectId;
    private uint activeLocalEntityId;
    private bool wasCrystallineConflict;
    private bool wasConfigured;
    private long nextUpdateAt;
    private long nextErrorLogAt;
    private long setterIntentCount;
    private long setterInvocationCount;
    private long exactReadbackCount;
    private long terminalFailureCount;
    private string lastEvent = "Inactive";
    private bool started;
    private bool disposed;

    internal AutoLowMpFocusTargetService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IDutyState dutyState,
        ITargetManager targetManager,
        IPluginLog log,
        PvPMetadataValidation metadata,
        ExecuteTracker executeTracker)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.dutyState = dutyState;
        this.targetManager = targetManager;
        this.log = log;
        this.metadata = metadata;
        this.executeTracker = executeTracker;
        diagnostics = AutoLowMpFocusTargetDiagnostics.Inactive(IsMetadataVerified);
    }

    internal AutoLowMpFocusTargetDiagnostics Diagnostics => Volatile.Read(ref diagnostics);

    private bool IsMetadataVerified =>
        metadata.RecuperateVerified && metadata.AutoLowMpFocusProbeVerified;

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

        // Focus ownership cannot be proven because FFXIV exposes no focus timestamp.
        // Unload therefore resets only local state and deliberately performs no clear.
        ResetInternal("Disposed without changing Focus Target");
        Volatile.Write(
            ref diagnostics,
            AutoLowMpFocusTargetDiagnostics.Inactive(IsMetadataVerified) with
            {
                LastEvent = lastEvent,
            });
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
            ResetInternal("Exception failed closed without changing Focus Target");
            terminalFailureCount++;
            Publish(
                configured: configuration.Enabled && configuration.EnableAutoLowMpFocusTarget,
                isCrystallineConflict: false,
                textInputStateKnown: false,
                textInputActive: true,
                completeCanonicalEnemySet: false,
                focusState: AutoLowMpFocusObservedState.Unknown,
                decision: default,
                candidates: [],
                candidateResolution: "Exception");
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
            log.Error(exception, "Seiton Sense Auto Low-MP Focus failed closed without changing Focus Target.");
        }
    }

    private void Update(long now)
    {
        var local = objectTable.LocalPlayer;
        var context = ResolveContext();
        var isCrystallineConflict = context == SupportedPvPContext.CrystallineConflict;
        var configured = configuration.Enabled && configuration.EnableAutoLowMpFocusTarget;
        var hasExactLocal = TryGetExactLocal(local, out var localIdentity);
        var localAlive = hasExactLocal && IsLivePlayer(local);
        var localIdentityChanged = localIdentity.IsValid &&
                                   activeLocalEntityId != 0 &&
                                   (localIdentity.GameObjectId != activeLocalGameObjectId ||
                                    localIdentity.EntityId != activeLocalEntityId);
        var hardReset = clientState.TerritoryType != activeTerritory ||
                        localIdentityChanged ||
                        (wasCrystallineConflict && !isCrystallineConflict) ||
                        (!wasCrystallineConflict && isCrystallineConflict) ||
                        (wasConfigured && !configured);
        if (hardReset)
            ResetInternal("Context, local identity, or explicit configuration changed");

        activeTerritory = clientState.TerritoryType;
        if (localIdentity.IsValid)
        {
            activeLocalGameObjectId = localIdentity.GameObjectId;
            activeLocalEntityId = localIdentity.EntityId;
        }

        wasCrystallineConflict = isCrystallineConflict;
        wasConfigured = configured;

        var focusStateKnown = TryReadFocus(out var focusState, out var focusTarget);
        if (!focusStateKnown)
            focusState = AutoLowMpFocusObservedState.Unknown;
        var textInputStateKnown = TryGetTextInputState(out var textInputActive);
        var metadataVerified = IsMetadataVerified;

        IReadOnlyList<AutoLowMpFocusTargetCandidate> candidates = [];
        var completeCanonicalEnemySet = false;
        var candidateResolution = "Not evaluated";
        var lastNativeRangeResult = uint.MaxValue;
        if (configured &&
            isCrystallineConflict &&
            localAlive &&
            metadataVerified)
        {
            candidates = ResolveExactCandidates(
                local!,
                now,
                out completeCanonicalEnemySet,
                out candidateResolution,
                out lastNativeRangeResult);
            if (!completeCanonicalEnemySet)
                ResetLowMpSampling();
        }
        else
        {
            ResetLowMpSampling();
            candidateResolution = !configured
                ? "Feature disabled"
                : !isCrystallineConflict
                    ? "Not exact Crystalline Conflict"
                    : !localAlive
                        ? "Local player invalid or dead"
                        : "Metadata unverified";
        }

        var decision = AutoLowMpFocusTargetRules.Observe(
            state,
            new AutoLowMpFocusTargetObservation(
                configured,
                isCrystallineConflict,
                localAlive,
                localIdentity,
                metadataVerified,
                textInputStateKnown,
                textInputActive,
                completeCanonicalEnemySet,
                focusState,
                focusTarget,
                now,
                candidates,
                hardReset));
        state = decision.State;

        if (decision.ShouldSetFocus && decision.Intent is { } intent)
        {
            setterIntentCount++;
            var outcome = TrySetFrozenIntentOnce(
                intent,
                out var setterInvoked,
                out var exactReadback,
                out var finalNativeRangeResult,
                out var attemptEvent);
            state = AutoLowMpFocusTargetRules.ApplySetOutcome(state, intent, outcome);
            if (setterInvoked) setterInvocationCount++;
            if (exactReadback) exactReadbackCount++;
            if (outcome != AutoLowMpFocusTargetSetOutcome.ExactReadbackConfirmed)
                terminalFailureCount++;
            if (finalNativeRangeResult != uint.MaxValue)
                lastNativeRangeResult = finalNativeRangeResult;
            lastEvent = attemptEvent;
        }
        else
        {
            lastEvent = decision.Reason.ToString();
        }

        Publish(
            configured,
            isCrystallineConflict,
            textInputStateKnown,
            textInputActive,
            completeCanonicalEnemySet,
            focusState,
            decision,
            candidates,
            candidateResolution,
            lastNativeRangeResult);
    }

    private IReadOnlyList<AutoLowMpFocusTargetCandidate> ResolveExactCandidates(
        IPlayerCharacter localPlayer,
        long now,
        out bool complete,
        out string resolution,
        out uint lastNativeRangeResult)
    {
        complete = false;
        lastNativeRangeResult = uint.MaxValue;
        var diagnosticsBefore = executeTracker.Diagnostics;
        var trackerEnemies = executeTracker.Enemies;
        if (!diagnosticsBefore.Active ||
            !diagnosticsBefore.IsCrystallineConflict ||
            diagnosticsBefore.IsWolvesDen ||
            diagnosticsBefore.TerritoryId != clientState.TerritoryType ||
            !diagnosticsBefore.RecuperateMetadataVerified ||
            diagnosticsBefore.SlotCapacity != EnemySlotRules.LastSlot ||
            diagnosticsBefore.ResolvedSlots != EnemySlotRules.LastSlot)
        {
            resolution =
                $"Tracker context incomplete: {diagnosticsBefore.ResolvedSlots}/{diagnosticsBefore.SlotCapacity}";
            return [];
        }

        var snapshots = trackerEnemies.ToArray();
        if (!ReferenceEquals(diagnosticsBefore, executeTracker.Diagnostics) ||
            !ReferenceEquals(trackerEnemies, executeTracker.Enemies) ||
            snapshots.Length > EnemySlotRules.LastSlot ||
            snapshots.Length != diagnosticsBefore.ValidEnemySlots)
        {
            resolution = "Tracker snapshot changed during capture";
            return [];
        }

        var snapshotSlots = new HashSet<int>();
        var snapshotGameObjectIds = new HashSet<ulong>();
        var snapshotEntityIds = new HashSet<uint>();
        var snapshotsBySlot = new Dictionary<int, EnemyHudSnapshot>(snapshots.Length);
        foreach (var snapshot in snapshots)
        {
            if (!EnemySlotRules.IsValidSlot(snapshot.Slot) ||
                !IsValidGameObjectId(snapshot.GameObjectId) ||
                !IsValidEntityId(snapshot.EntityId) ||
                !snapshotSlots.Add(snapshot.Slot) ||
                !snapshotGameObjectIds.Add(snapshot.GameObjectId) ||
                !snapshotEntityIds.Add(snapshot.EntityId))
            {
                resolution = "Tracker snapshot identity ambiguous";
                return [];
            }

            snapshotsBySlot.Add(snapshot.Slot, snapshot);
        }

        var currentSlots = new List<(int Slot, IPlayerCharacter Player)>(EnemySlotRules.LastSlot);
        var nativeGameObjectIds = new HashSet<ulong>();
        var nativeEntityIds = new HashSet<uint>();
        var nativeAddresses = new HashSet<nint>();
        for (var slot = EnemySlotRules.FirstSlot; slot <= EnemySlotRules.LastSlot; slot++)
        {
            if (!TryResolveExactCanonicalSlot(slot, out var player))
            {
                resolution = $"Native S{slot} unresolved";
                return [];
            }

            if (!nativeGameObjectIds.Add(player.GameObjectId) ||
                !nativeEntityIds.Add(player.EntityId) ||
                !nativeAddresses.Add(player.Address))
            {
                resolution = "Native S1-S5 identities duplicate";
                return [];
            }

            currentSlots.Add((slot, player));
        }

        var trackerEligibleSlots = currentSlots
            .Where(static entry =>
                IsLivePlayer(entry.Player) &&
                entry.Player.IsTargetable &&
                ExecuteThreshold.HasValidHp(entry.Player.CurrentHp, entry.Player.MaxHp))
            .ToArray();
        if (trackerEligibleSlots.Length != diagnosticsBefore.ValidEnemySlots ||
            trackerEligibleSlots.Length != snapshots.Length)
        {
            resolution =
                $"Tracker/native eligible count drift: {snapshots.Length}/{trackerEligibleSlots.Length}";
            return [];
        }

        foreach (var (slot, player) in trackerEligibleSlots)
        {
            if (!snapshotsBySlot.TryGetValue(slot, out var snapshot) ||
                snapshot.GameObjectId != player.GameObjectId ||
                snapshot.EntityId != player.EntityId)
            {
                resolution = $"Tracker/native S{slot} identity mismatch";
                return [];
            }
        }

        var nextLowMpStates = new Dictionary<TargetPressureActorIdentity, LowMpState>();
        var candidates = new List<AutoLowMpFocusTargetCandidate>(EnemySlotRules.LastSlot);
        foreach (var (slot, player) in currentSlots)
        {
            var actor = new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);
            var alive = IsLivePlayer(player);
            var targetable = player.IsTargetable;
            var validHp = ExecuteThreshold.HasValidHp(player.CurrentHp, player.MaxHp);
            lowMpStates.TryGetValue(actor, out var previousLowMp);
            var plausibleMp = player.MaxMp > 0 && player.CurrentMp <= player.MaxMp;
            var trustedMp = plausibleMp &&
                            (player.CurrentMp > 0 || previousLowMp.HasTrustedSample);
            var nextLowMp = LowMpRules.Observe(
                previousLowMp,
                (int)Math.Min(player.CurrentMp, int.MaxValue),
                trustedMp,
                now,
                enterThreshold: AutoLowMpFocusTargetRules.ObservationEnterThreshold,
                exitThreshold: LowMpRules.ExitThreshold);
            nextLowMpStates[actor] = nextLowMp;
            var lowMpWaveLatched = nextLowMp.HasTrustedSample && nextLowMp.IsUnavailable;
            var trustedLowMp = alive &&
                               targetable &&
                               validHp &&
                               trustedMp &&
                               lowMpWaveLatched;

            var nativeTargetValid = TryGetExactLocal(localPlayer, out _);
            var rangeAndLineOfSight = false;
            if (nativeTargetValid &&
                trustedLowMp &&
                player.CurrentMp <= AutoLowMpFocusTargetRules.MaximumEligibleMp)
            {
                rangeAndLineOfSight = SeitonReadinessProbe.HasRangeAndLineOfSight(
                    localPlayer,
                    player,
                    AutoLowMpFocusTargetRules.ProbeActionId,
                    out lastNativeRangeResult);
            }

            candidates.Add(new AutoLowMpFocusTargetCandidate(
                slot,
                actor,
                ExactCanonicalIdentity: true,
                alive,
                targetable,
                player.CurrentHp,
                player.MaxHp,
                lowMpWaveLatched,
                trustedLowMp,
                player.CurrentMp,
                player.MaxMp,
                nativeTargetValid,
                rangeAndLineOfSight));
        }

        foreach (var (slot, player) in currentSlots)
        {
            var stablePlayer = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasValidIdentity(stablePlayer) ||
                stablePlayer!.Address != player.Address ||
                stablePlayer.GameObjectId != player.GameObjectId ||
                stablePlayer.EntityId != player.EntityId)
            {
                resolution = $"Native S{slot} changed during capture";
                return [];
            }
        }

        if (!ReferenceEquals(diagnosticsBefore, executeTracker.Diagnostics) ||
            !ReferenceEquals(trackerEnemies, executeTracker.Enemies) ||
            !AutoLowMpFocusTargetRules.HasCompleteExactCanonicalSet(candidates))
        {
            resolution = "Canonical or tracker snapshot changed after capture";
            return [];
        }

        lowMpStates = nextLowMpStates;
        complete = true;
        resolution = $"Exact coherent S1-S5 set; eligible={candidates.Count(candidate =>
            AutoLowMpFocusTargetRules.IsEligibleCandidate(
                candidate,
                new TargetPressureActorIdentity(localPlayer.GameObjectId, localPlayer.EntityId)))}";
        return candidates;
    }

    private AutoLowMpFocusTargetSetOutcome TrySetFrozenIntentOnce(
        AutoLowMpFocusTargetIntent intent,
        out bool setterInvoked,
        out bool exactReadback,
        out uint finalNativeRangeResult,
        out string attemptEvent)
    {
        setterInvoked = false;
        exactReadback = false;
        finalNativeRangeResult = uint.MaxValue;
        attemptEvent = "Frozen intent failed final preflight";

        try
        {
            var currentLocal = objectTable.LocalPlayer;
            var configured = configuration.Enabled && configuration.EnableAutoLowMpFocusTarget;
            var isCrystallineConflict = ResolveContext() == SupportedPvPContext.CrystallineConflict;
            var localExact = TryGetExactLocal(currentLocal, out var currentLocalIdentity);
            var localAlive = localExact && IsLivePlayer(currentLocal);
            if (!TryGetTextInputState(out var textInputActive) || textInputActive ||
                !TryReadFocus(out var firstFocusState, out _) ||
                firstFocusState != AutoLowMpFocusObservedState.Empty ||
                !localAlive ||
                !TryResolveFrozenCandidate(
                    currentLocal!,
                    intent,
                    out var exactTarget,
                    out var currentCandidate,
                    out finalNativeRangeResult) ||
                !AutoLowMpFocusTargetRules.CanSetFrozenIntent(
                    intent,
                    currentCandidate,
                    configured,
                    isCrystallineConflict,
                    localAlive,
                    currentLocalIdentity,
                    IsMetadataVerified,
                    firstFocusState))
            {
                return AutoLowMpFocusTargetSetOutcome.TerminalFailure;
            }

            // The public API has no compare-and-set operation. This final same-thread
            // empty read is therefore immediately adjacent to the sole reviewed write.
            if (!TryReadFocus(out var finalFocusState, out _) ||
                finalFocusState != AutoLowMpFocusObservedState.Empty)
            {
                attemptEvent = "Focus became occupied before the sole setter";
                return AutoLowMpFocusTargetSetOutcome.TerminalFailure;
            }

            setterInvoked = true;
            targetManager.FocusTarget = exactTarget;

            if (TryReadFocus(out var readbackState, out var readbackTarget) &&
                readbackState == AutoLowMpFocusObservedState.Occupied &&
                readbackTarget == intent.Target)
            {
                exactReadback = true;
                attemptEvent = $"S{intent.EnemySlot} empty-to-exact Focus Target confirmed";
                return AutoLowMpFocusTargetSetOutcome.ExactReadbackConfirmed;
            }

            attemptEvent = $"S{intent.EnemySlot} setter invoked without exact readback; no retry";
            return AutoLowMpFocusTargetSetOutcome.SetterInvokedWithoutExactReadback;
        }
        catch (Exception exception)
        {
            attemptEvent = setterInvoked
                ? "Focus setter threw; no retry or clear"
                : "Frozen focus preflight threw; no retry";
            var now = Environment.TickCount64;
            if (now >= nextErrorLogAt)
            {
                nextErrorLogAt = now + 10_000;
                log.Error(exception, "Seiton Sense Auto Low-MP Focus attempt failed and will not be retried.");
            }

            return AutoLowMpFocusTargetSetOutcome.TerminalFailure;
        }
    }

    private bool TryResolveFrozenCandidate(
        IPlayerCharacter localPlayer,
        AutoLowMpFocusTargetIntent intent,
        out IPlayerCharacter exactTarget,
        out AutoLowMpFocusTargetCandidate candidate,
        out uint nativeRangeResult)
    {
        exactTarget = null!;
        candidate = default;
        nativeRangeResult = uint.MaxValue;
        if (!intent.IsValid ||
            !TryResolveExactCanonicalSlot(intent.EnemySlot, out var target) ||
            target.GameObjectId != intent.Target.GameObjectId ||
            target.EntityId != intent.Target.EntityId ||
            !lowMpStates.TryGetValue(intent.Target, out var lowMpState))
        {
            return false;
        }

        var plausibleMp = target.MaxMp > 0 && target.CurrentMp <= target.MaxMp;
        var trustedMp = plausibleMp &&
                        (target.CurrentMp > 0 || lowMpState.HasTrustedSample);
        var lowMpWaveLatched = lowMpState.HasTrustedSample && lowMpState.IsUnavailable;
        var trustedLowMp = trustedMp &&
                           lowMpState.HasTrustedSample &&
                           lowMpState.IsUnavailable;
        var nativeTargetValid = TryGetExactLocal(localPlayer, out _);
        var rangeAndLineOfSight = nativeTargetValid &&
                                  SeitonReadinessProbe.HasRangeAndLineOfSight(
                                      localPlayer,
                                      target,
                                      AutoLowMpFocusTargetRules.ProbeActionId,
                                      out nativeRangeResult);
        candidate = new AutoLowMpFocusTargetCandidate(
            intent.EnemySlot,
            intent.Target,
            ExactCanonicalIdentity: true,
            IsLivePlayer(target),
            target.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            lowMpWaveLatched,
            trustedLowMp,
            target.CurrentMp,
            target.MaxMp,
            nativeTargetValid,
            rangeAndLineOfSight);
        exactTarget = target;
        return true;
    }

    private bool TryResolveExactCanonicalSlot(int slot, out IPlayerCharacter player)
    {
        player = null!;
        var resolved = EnemySlotResolver.Resolve(objectTable, slot);
        if (!HasValidIdentity(resolved)) return false;

        var tablePlayer = objectTable.SearchByEntityId(resolved!.EntityId) as IPlayerCharacter;
        if (tablePlayer is null ||
            tablePlayer.Address != resolved.Address ||
            tablePlayer.GameObjectId != resolved.GameObjectId ||
            tablePlayer.EntityId != resolved.EntityId)
        {
            return false;
        }

        player = resolved;
        return true;
    }

    private bool TryGetExactLocal(
        IPlayerCharacter? localPlayer,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (!HasValidIdentity(localPlayer)) return false;

        var tablePlayer = objectTable.SearchByEntityId(localPlayer!.EntityId) as IPlayerCharacter;
        if (tablePlayer is null ||
            tablePlayer.Address != localPlayer.Address ||
            tablePlayer.GameObjectId != localPlayer.GameObjectId ||
            tablePlayer.EntityId != localPlayer.EntityId)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(localPlayer.GameObjectId, localPlayer.EntityId);
        return identity.IsValid;
    }

    private bool TryReadFocus(
        out AutoLowMpFocusObservedState focusState,
        out TargetPressureActorIdentity focusTarget)
    {
        focusState = AutoLowMpFocusObservedState.Unknown;
        focusTarget = default;
        try
        {
            var focus = targetManager.FocusTarget;
            if (focus is null)
            {
                focusState = AutoLowMpFocusObservedState.Empty;
                return true;
            }

            focusState = AutoLowMpFocusObservedState.Occupied;
            if (!HasValidIdentity(focus)) return true;

            var tableFocus = objectTable.SearchByEntityId(focus.EntityId);
            if (tableFocus is not null &&
                tableFocus.Address == focus.Address &&
                tableFocus.GameObjectId == focus.GameObjectId &&
                tableFocus.EntityId == focus.EntityId)
            {
                focusTarget = new TargetPressureActorIdentity(focus.GameObjectId, focus.EntityId);
            }

            return true;
        }
        catch
        {
            focusState = AutoLowMpFocusObservedState.Unknown;
            focusTarget = default;
            return false;
        }
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

    private static bool HasValidIdentity(IGameObject? gameObject) =>
        gameObject is not null &&
        gameObject.Address != nint.Zero &&
        IsValidGameObjectId(gameObject.GameObjectId) &&
        IsValidEntityId(gameObject.EntityId);

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        player is not null &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;

    private static bool IsValidGameObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000UL;

    private static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000u;

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

    private void ResetLowMpSampling() => lowMpStates.Clear();

    private void ResetInternal(string reason)
    {
        state = AutoLowMpFocusTargetState.Initial;
        ResetLowMpSampling();
        lastEvent = reason;
    }

    private void Publish(
        bool configured,
        bool isCrystallineConflict,
        bool textInputStateKnown,
        bool textInputActive,
        bool completeCanonicalEnemySet,
        AutoLowMpFocusObservedState focusState,
        AutoLowMpFocusTargetDecision decision,
        IReadOnlyList<AutoLowMpFocusTargetCandidate> candidates,
        string candidateResolution,
        uint lastNativeRangeResult = uint.MaxValue)
    {
        var selected = decision.SelectedCandidateIndex >= 0 &&
                       decision.SelectedCandidateIndex < candidates.Count
            ? candidates[decision.SelectedCandidateIndex]
            : (AutoLowMpFocusTargetCandidate?)null;
        var local = objectTable.LocalPlayer;
        var localIdentity = local is null
            ? default
            : new TargetPressureActorIdentity(local.GameObjectId, local.EntityId);
        var eligibleCount = localIdentity.IsValid
            ? candidates.Count(candidate =>
                AutoLowMpFocusTargetRules.IsEligibleCandidate(candidate, localIdentity))
            : 0;
        Volatile.Write(
            ref diagnostics,
            new AutoLowMpFocusTargetDiagnostics(
                configured,
                IsMetadataVerified,
                isCrystallineConflict,
                textInputStateKnown,
                textInputActive,
                completeCanonicalEnemySet,
                focusState,
                decision.Kind,
                decision.Reason,
                state.LowMpWaveActive,
                state.AttemptSpentForWave,
                state.ManualOverrideLatched,
                candidates.Count,
                eligibleCount,
                selected?.EnemySlot ?? decision.Intent?.EnemySlot ?? 0,
                selected?.Actor.GameObjectId ?? decision.Intent?.Target.GameObjectId ?? 0,
                selected?.Actor.EntityId ?? decision.Intent?.Target.EntityId ?? 0,
                selected?.CurrentMp ?? decision.Intent?.SelectedCurrentMp ?? 0,
                selected?.MaximumMp ?? decision.Intent?.SelectedMaximumMp ?? 0,
                lastNativeRangeResult,
                setterIntentCount,
                setterInvocationCount,
                exactReadbackCount,
                terminalFailureCount,
                candidateResolution,
                lastEvent));
    }
}
