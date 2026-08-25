using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;
using Lumina.Excel.Sheets;
using GameAction = Lumina.Excel.Sheets.Action;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace SeitonSense.Plugin.Services;

internal enum MonkWolvesDenTargetKind : byte
{
    None = 0,
    DuelOpponent = 1,
    StrikingDummy = 2,
}

internal sealed record MonkHeldComboProbeSnapshot(
    MonkHeldComboDecisionKind Decision,
    MonkHeldComboDecisionReason Reason,
    MonkHeldComboPhase Phase,
    uint ResolvedComboActionId,
    uint PendingActionId,
    MonkHeldComboActionPurpose PendingPurpose,
    int EnemySlot,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    bool PressurePointConfirmed,
    bool FireResonanceConfirmed,
    bool NativeBoundaryReady,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    ClientActionAttemptOutcome LastNativeOutcome,
    int NativeAttemptCount,
    long AttemptCount,
    long AcceptedCount,
    long RejectedCount,
    long UnknownCount,
    long SoftWaitCount,
    string LastEvent)
{
    internal static MonkHeldComboProbeSnapshot Initial { get; } = new(
        MonkHeldComboDecisionKind.None,
        MonkHeldComboDecisionReason.None,
        MonkHeldComboPhase.Waiting,
        0,
        0,
        MonkHeldComboActionPurpose.None,
        0,
        0,
        0,
        false,
        false,
        false,
        VirtualKey.NO_KEY,
        false,
        false,
        false,
        ClientActionAttemptOutcome.None,
        0,
        0,
        0,
        0,
        0,
        0,
        "Waiting for Monk combo");
}

/// <summary>
/// Holds one exact Monk combo route against one frozen actor. CC selects a
/// canonical S1-S5 actor without changing target state; Wolves' Den accepts
/// only the current native duel opponent or verified striking dummy. Every
/// native attempt freezes action, actor, key, route, and addresses.
/// </summary>
internal sealed unsafe class MonkHeldComboProbe
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private MonkHeldComboState state = MonkHeldComboState.Initial;
    private MonkHeldComboProbeSnapshot snapshot = MonkHeldComboProbeSnapshot.Initial;
    private VirtualKey terminalHeldKey = VirtualKey.NO_KEY;
    private uint frozenTerritoryId;
    private nint frozenLocalAddress;
    private nint frozenTargetAddress;
    private MonkWolvesDenTargetKind frozenWolvesDenTargetKind;
    private long attemptCount;
    private long acceptedCount;
    private long rejectedCount;
    private long unknownCount;
    private long softWaitCount;
    private long nextErrorLogAt;
    private string lastEvent = "Waiting for Monk combo";

    internal MonkHeldComboProbe(
        IClientState clientState,
        IObjectTable objectTable,
        NearAssistRedirector nearAssist,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal MonkHeldComboProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    /// <summary>
    /// One-time English-sheet validation for the route, auxiliary actions,
    /// status proof rows, and Wolves' Den dummy identity.
    /// </summary>
    internal static bool ValidateMetadata(
        IDataManager dataManager,
        IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(
                ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(
                ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<GameStatus>(
                ClientLanguage.English);
            var routes = dataManager.GetExcelSheet<ActionComboRoute>(
                ClientLanguage.English);
            var npcNames = dataManager.GetExcelSheet<BNpcName>(
                ClientLanguage.English);

            var comboDefinitions = new[]
            {
                new ComboMetadata(29_475, "Dragon Kick", 9_156, 5, 0, 1, 0, 0),
                new ComboMetadata(29_476, "Twin Snakes", 9_157, 5, 0, 1, 29_475, 58),
                new ComboMetadata(29_477, "Demolish", 9_158, 5, 0, 1, 29_476, 58),
                new ComboMetadata(41_444, "Leaping Opo", 9_776, 5, 0, 1, 29_477, 58),
                new ComboMetadata(41_445, "Rising Raptor", 9_777, 5, 0, 1, 41_444, 58),
                new ComboMetadata(41_446, "Pouncing Coeurl", 9_778, 5, 0, 1, 41_445, 58),
                new ComboMetadata(29_478, "Phantom Rush", 9_642, 6, 5, 2, 41_446, 58),
            };
            foreach (var definition in comboDefinitions)
            {
                if (!actions.TryGetRow(definition.ActionId, out var action) ||
                    !descriptions.TryGetRow(
                        definition.ActionId,
                        out var description) ||
                    !IsExpectedComboAction(action, description, definition))
                {
                    log.Warning(
                        "Seiton Sense Monk combo metadata failed closed at {ActionId}.",
                        definition.ActionId);
                    return false;
                }
            }

            var auxiliariesValid =
                actions.TryGetRow(
                    MonkHeldComboRules.FireReplyActionId,
                    out var fireReply) &&
                descriptions.TryGetRow(
                    MonkHeldComboRules.FireReplyActionId,
                    out var fireReplyDescription) &&
                IsExpectedAuxiliary(
                    fireReply,
                    fireReplyDescription,
                    "Fire's Reply",
                    9_781,
                    actionCategory: 3,
                    range: 20,
                    effectRange: 5,
                    castType: 2,
                    recast100ms: 160,
                    cooldownGroup: 1,
                    additionalCooldownGroup: 58,
                    maximumCharges: 2,
                    canTargetSelf: false,
                    canTargetHostile: true,
                    canTargetParty: false,
                    canTargetAlly: false,
                    affectsPosition: false) &&
                actions.TryGetRow(
                    MonkHeldComboRules.WindReplyActionId,
                    out var windReply) &&
                descriptions.TryGetRow(
                    MonkHeldComboRules.WindReplyActionId,
                    out var windReplyDescription) &&
                IsExpectedAuxiliary(
                    windReply,
                    windReplyDescription,
                    "Wind's Reply",
                    9_779,
                    actionCategory: 3,
                    range: 10,
                    effectRange: 10,
                    castType: 4,
                    recast100ms: 160,
                    cooldownGroup: 2,
                    additionalCooldownGroup: 58,
                    maximumCharges: 0,
                    canTargetSelf: false,
                    canTargetHostile: true,
                    canTargetParty: false,
                    canTargetAlly: false,
                    affectsPosition: false) &&
                windReplyDescription.Description.ToString().Contains(
                    "afflicts first target with Pressure Point if successfully knocked back",
                    StringComparison.Ordinal) &&
                actions.TryGetRow(
                    MonkHeldComboRules.RisingPhoenixActionId,
                    out var risingPhoenix) &&
                descriptions.TryGetRow(
                    MonkHeldComboRules.RisingPhoenixActionId,
                    out var risingPhoenixDescription) &&
                IsExpectedAuxiliary(
                    risingPhoenix,
                    risingPhoenixDescription,
                    "Rising Phoenix",
                    9_643,
                    actionCategory: 4,
                    range: 0,
                    effectRange: 6,
                    castType: 2,
                    recast100ms: 120,
                    cooldownGroup: 3,
                    additionalCooldownGroup: 72,
                    maximumCharges: 2,
                    canTargetSelf: true,
                    canTargetHostile: false,
                    canTargetParty: false,
                    canTargetAlly: false,
                    affectsPosition: false) &&
                risingPhoenixDescription.Description.ToString().Contains(
                    "Grants Fire Resonance",
                    StringComparison.Ordinal) &&
                actions.TryGetRow(
                    MonkHeldComboRules.ThunderclapActionId,
                    out var thunderclap) &&
                descriptions.TryGetRow(
                    MonkHeldComboRules.ThunderclapActionId,
                    out var thunderclapDescription) &&
                IsExpectedAuxiliary(
                    thunderclap,
                    thunderclapDescription,
                    "Thunderclap",
                    9_645,
                    actionCategory: 4,
                    range: 20,
                    effectRange: 0,
                    castType: 1,
                    recast100ms: 80,
                    cooldownGroup: 6,
                    additionalCooldownGroup: 71,
                    maximumCharges: 2,
                    canTargetSelf: false,
                    canTargetHostile: true,
                    canTargetParty: true,
                    canTargetAlly: true,
                    affectsPosition: true);

            var routeValid =
                routes.TryGetRow(
                    MonkHeldComboRules.PhantomRushComboRouteId,
                    out var route) &&
                route.Action.Count >= comboDefinitions.Length &&
                route.Name.ToString() == "Phantom Rush Combo" &&
                route.Unknown4;
            if (routeValid)
            {
                for (var index = 0; index < comboDefinitions.Length; index++)
                {
                    if (route.Action[index].RowId !=
                        comboDefinitions[index].ActionId)
                    {
                        routeValid = false;
                        break;
                    }
                }
            }

            var statusesValid =
                statuses.TryGetRow(
                    MonkHeldComboRules.PressurePointStatusId,
                    out var pressurePoint) &&
                IsExpectedStatus(
                    pressurePoint,
                    "Pressure Point",
                    214_931,
                    category: 2) &&
                statuses.TryGetRow(
                    MonkHeldComboRules.FireResonanceStatusId,
                    out var fireResonance) &&
                IsExpectedStatus(
                    fireResonance,
                    "Fire Resonance",
                    212_528,
                    category: 1) &&
                statuses.TryGetRow(
                    MonkHeldComboRules.WindResonanceStatusId,
                    out var windResonance) &&
                IsExpectedStatus(
                    windResonance,
                    "Wind Resonance",
                    212_537,
                    category: 1);
            var dummyValid =
                npcNames.TryGetRow(
                    MonkHeldComboRules.WolvesDenStrikingDummyNameId,
                    out var dummy) &&
                dummy.Singular.ToString() == "striking dummy" &&
                dummy.Plural.ToString() == "striking dummies";

            var valid = auxiliariesValid && routeValid &&
                        statusesValid && dummyValid;
            if (!valid)
            {
                log.Warning(
                    "Seiton Sense Monk held-combo metadata failed closed: auxiliaries={Auxiliaries}, route={Route}, statuses={Statuses}, dummy={Dummy}.",
                    auxiliariesValid,
                    routeValid,
                    statusesValid,
                    dummyValid);
            }

            return valid;
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense Monk held-combo metadata lookup failed closed.");
            return false;
        }
    }

    internal MonkHeldComboProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        try
        {
            return ObserveCore(
                localPlayer,
                context,
                configurationEnabled,
                actionMetadataVerified,
                actionHelpersSuppressedByGuard,
                higherPriorityClaimed,
                inputFrame,
                nowMilliseconds,
                hardReset);
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                nowMilliseconds,
                "Seiton Sense Monk held combo probe failed closed.");
            return FailClosed();
        }
    }

    internal void Reset()
    {
        state = MonkHeldComboState.Initial;
        terminalHeldKey = VirtualKey.NO_KEY;
        ClearFrozenRuntime();
        lastEvent = "Reset";
        PublishTerminalSnapshot(
            MonkHeldComboDecisionKind.None,
            MonkHeldComboDecisionReason.HardReset,
            lastEvent);
    }

    internal MonkHeldComboProbeSnapshot FailClosed()
    {
        var failedKey = state.Intent is { IsValid: true } intent
            ? (VirtualKey)intent.FrozenKeyCode
            : terminalHeldKey;
        state = MonkHeldComboState.Initial;
        terminalHeldKey = failedKey;
        ClearFrozenRuntime();

        lastEvent = "Failed closed";
        return PublishTerminalSnapshot(
            MonkHeldComboDecisionKind.Cancelled,
            MonkHeldComboDecisionReason.HardReset,
            lastEvent);
    }

    private MonkHeldComboProbeSnapshot ObserveCore(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool actionMetadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset)
    {
        var effectiveHardReset = hardReset || nowMilliseconds < 0;
        if (effectiveHardReset)
        {
            state = MonkHeldComboState.Initial;
            terminalHeldKey = VirtualKey.NO_KEY;
            ClearFrozenRuntime();
        }

        if (terminalHeldKey != VirtualKey.NO_KEY &&
            inputFrame.Snapshot.ProbeSucceeded &&
            !inputFrame.IsGameplayKeyPhysicallyDown(terminalHeldKey))
        {
            terminalHeldKey = VirtualKey.NO_KEY;
        }

        var exactLocal = ResolveExactLocalPlayer(localPlayer);
        var localIdentity = exactLocal is null
            ? default
            : new TargetPressureActorIdentity(
                exactLocal.GameObjectId,
                exactLocal.EntityId);
        var localAlive = IsLivePlayer(exactLocal);
        var localJobId = exactLocal?.ClassJob.IsValid == true
            ? exactLocal.ClassJob.RowId
            : 0;
        var territoryId = clientState.TerritoryType;
        var runtimeDrift = state.Intent is { IsValid: true } frozenIntent &&
                           (!FrozenRuntimeMatches(
                                frozenIntent,
                                territoryId,
                                exactLocal?.Address ?? nint.Zero) ||
                            frozenIntent.Context != context ||
                            frozenIntent.LocalPlayer != localIdentity ||
                            !configurationEnabled ||
                            !localAlive ||
                            localJobId != MonkHeldComboRules.MonkJobId ||
                            !actionMetadataVerified);
        if (runtimeDrift)
        {
            effectiveHardReset = true;
            state = MonkHeldComboState.Initial;
            terminalHeldKey = VirtualKey.NO_KEY;
            ClearFrozenRuntime();
        }

        var featureGateReady = !effectiveHardReset &&
                               configurationEnabled &&
                               actionMetadataVerified &&
                               context is SupportedPvPContext.CrystallineConflict or
                                   SupportedPvPContext.WolvesDen &&
                               localAlive &&
                               localJobId == MonkHeldComboRules.MonkJobId;
        var guardSuppressed = featureGateReady &&
                              (actionHelpersSuppressedByGuard ||
                               IsCurrentlySuppressedByGuard(
                                   exactLocal,
                                   nowMilliseconds));
        var effectiveHigherPriorityClaimed =
            higherPriorityClaimed || inputFrame.IsConsumed;

        var localActions = default(LocalActionState);
        if (featureGateReady && exactLocal is not null)
            localActions = ObserveLocalActions(exactLocal);

        var hasFireResonance = exactLocal is not null &&
                               HasExactOwnStatus(
                                   exactLocal,
                                   MonkHeldComboRules.FireResonanceStatusId,
                                   exactLocal.EntityId);
        var input = inputFrame.Snapshot;
        var frozenKeyStillDown =
            state.Intent is { IsValid: true } heldIntent &&
            inputFrame.IsGameplayKeyPhysicallyDown(
                (VirtualKey)heldIntent.FrozenKeyCode);

        RuntimeCandidate? runtimeCandidate = null;
        if (exactLocal is not null &&
            MonkHeldComboRules.IsExactComboAction(
                localActions.ResolvedComboActionId))
        {
            runtimeCandidate = state.Intent is { IsValid: true } intent
                ? ResolveExactCandidate(
                    exactLocal,
                    context,
                    intent.EnemySlot,
                    intent.Target,
                    frozenWolvesDenTargetKind,
                    localActions.ResolvedComboActionId)
                : ResolveCurrentBestCandidate(
                    exactLocal,
                    context,
                    localActions.ResolvedComboActionId,
                    localActions.FireReplyLocallyReady,
                    localActions.WindReplyLocallyReady,
                    localActions.ThunderclapLocallyReady,
                    hasFireResonance,
                    actionMetadataVerified);
        }

        if (state.Intent is { IsValid: true } &&
            runtimeCandidate is { } tracked &&
            tracked.Target.Address != frozenTargetAddress)
        {
            runtimeCandidate = null;
        }

        var exactComboReady =
            localActions.ComboActionLocallyReady &&
            runtimeCandidate?.ComboUseReady == true;
        var exactFireReady =
            localActions.FireReplyLocallyReady &&
            runtimeCandidate?.FireReplyUseReady == true;
        var exactWindReady =
            localActions.WindReplyLocallyReady &&
            runtimeCandidate?.WindReplyUseReady == true;
        var exactThunderReady =
            localActions.ThunderclapLocallyReady &&
            runtimeCandidate?.ThunderclapUseReady == true;
        var exactPhoenixReady =
            localActions.RisingPhoenixLocallyReady &&
            localActions.RisingPhoenixSelfUseReady;
        var confirmationBoundaryReopened =
            HasConfirmationBoundaryReopened(state, localActions);

        var observation = new MonkHeldComboObservation(
            ConfigurationEnabled: configurationEnabled,
            Context: context,
            LocalPlayer: localIdentity,
            IsLocalPlayerAlive: localAlive,
            LocalJobId: localJobId,
            MetadataVerified: actionMetadataVerified,
            ActionHelpersSuppressedByGuard: guardSuppressed,
            HigherPriorityClaimed: effectiveHigherPriorityClaimed,
            InputProbeSucceeded: input.ProbeSucceeded,
            IsTextInputActive: input.IsTextInputActive,
            HeldGameplayKeyEligible:
                terminalHeldKey == VirtualKey.NO_KEY &&
                inputFrame.HeldGameplayKeyEligible,
            HeldGameplayKeyCode: (int)input.HeldGameplayKey,
            FrozenKeyStillDown: frozenKeyStillDown,
            ResolvedComboActionId: localActions.ResolvedComboActionId,
            ComboActionLocallyReady: exactComboReady,
            FireReplyLocallyReady: exactFireReady,
            WindReplyLocallyReady: exactWindReady,
            ThunderclapLocallyReady: exactThunderReady,
            RisingPhoenixLocallyReady: exactPhoenixReady,
            HasExactOwnFireResonance: hasFireResonance,
            ConfirmationBoundaryReopened: confirmationBoundaryReopened,
            NativeBoundaryReady: localActions.NativeBoundaryReady,
            Candidate: runtimeCandidate?.Core,
            HardReset: effectiveHardReset,
            NowMilliseconds: nowMilliseconds);

        var previousIntent = state.Intent;
        var decision = MonkHeldComboRules.Observe(state, observation);
        if (decision.NextState.Intent is { IsValid: true } nextIntent &&
            (!previousIntent.HasValue || previousIntent.Value != nextIntent))
        {
            if (runtimeCandidate is not { } frozenCandidate ||
                exactLocal is null ||
                frozenCandidate.Core.Context != nextIntent.Context ||
                frozenCandidate.Core.EnemySlot != nextIntent.EnemySlot ||
                frozenCandidate.Core.Actor != nextIntent.Target)
            {
                decision = new MonkHeldComboDecision(
                    MonkHeldComboState.Initial,
                    MonkHeldComboDecisionKind.Cancelled,
                    MonkHeldComboDecisionReason.CandidateUnavailable);
                ClearFrozenRuntime();
            }
            else
            {
                FreezeRuntime(
                    nextIntent,
                    territoryId,
                    exactLocal.Address,
                    frozenCandidate.Target.Address,
                    frozenCandidate.WolvesDenTargetKind);
            }
        }

        if (previousIntent is { IsValid: true } previousFrozenIntent &&
            decision.Kind == MonkHeldComboDecisionKind.Cancelled &&
            decision.Reason is
                MonkHeldComboDecisionReason.CandidateUnavailable or
                MonkHeldComboDecisionReason.CandidateInvalid or
                MonkHeldComboDecisionReason.CarrierDrift or
                MonkHeldComboDecisionReason.PressurePointMissing or
                MonkHeldComboDecisionReason.FireResonanceMissing or
                MonkHeldComboDecisionReason.NativeAcceptanceUnknown)
        {
            terminalHeldKey = (VirtualKey)previousFrozenIntent.FrozenKeyCode;
        }

        state = decision.NextState;
        if (decision.InputClaimed) inputFrame.Consume();

        var attempted = false;
        var accepted = false;
        var nativeOutcome = ClientActionAttemptOutcome.None;
        if (decision.ShouldDispatch && decision.Intent is { } dispatchIntent)
        {
            nativeOutcome = TryUseOnce(
                decision,
                observation,
                context,
                configurationEnabled,
                actionMetadataVerified,
                actionHelpersSuppressedByGuard,
                effectiveHigherPriorityClaimed,
                inputFrame,
                out attempted,
                out var confirmationSequenceBaseline);
            if (attempted) Interlocked.Increment(ref attemptCount);
            if (nativeOutcome == ClientActionAttemptOutcome.ClientAccepted)
                Interlocked.Increment(ref acceptedCount);
            if (nativeOutcome == ClientActionAttemptOutcome.ClientRejected)
                Interlocked.Increment(ref rejectedCount);
            if (nativeOutcome == ClientActionAttemptOutcome.AcceptanceUnknown)
                Interlocked.Increment(ref unknownCount);

            var completion = MonkHeldComboRules.ApplyNativeAttemptOutcome(
                state,
                nativeOutcome,
                nowMilliseconds,
                confirmationSequenceBaseline);
            state = completion.NextState;
            accepted = completion.ClientAccepted;
            if (completion.SoftWait) Interlocked.Increment(ref softWaitCount);
            if (completion.RouteComplete ||
                completion.Terminal &&
                HeldActionRetryRules.ShouldLatchHeldKeyUntilRelease(
                    completion.Disposition))
            {
                terminalHeldKey =
                    (VirtualKey)dispatchIntent.FrozenKeyCode;
            }

            lastEvent = DescribeNativeResult(
                decision.ActionId,
                decision.Purpose,
                nativeOutcome,
                completion);
        }
        else
        {
            lastEvent = decision.Reason.ToString();
        }

        if (state.Intent is null)
            ClearFrozenRuntime();

        var activeIntent = state.Intent ?? decision.Intent;
        var selectedCandidate = runtimeCandidate?.Core;
        var result = new MonkHeldComboProbeSnapshot(
            decision.Kind,
            decision.Reason,
            state.Phase,
            localActions.ResolvedComboActionId,
            state.PendingActionId != 0
                ? state.PendingActionId
                : decision.ActionId,
            state.PendingPurpose != MonkHeldComboActionPurpose.None
                ? state.PendingPurpose
                : decision.Purpose,
            activeIntent?.EnemySlot ?? selectedCandidate?.EnemySlot ?? 0,
            activeIntent?.Target.GameObjectId ??
                selectedCandidate?.Actor.GameObjectId ?? 0,
            activeIntent?.Target.EntityId ??
                selectedCandidate?.Actor.EntityId ?? 0,
            state.PressurePointConfirmed ||
                selectedCandidate?.HasExactOwnPressurePoint == true,
            hasFireResonance,
            localActions.NativeBoundaryReady,
            activeIntent is { IsValid: true }
                ? (VirtualKey)activeIntent.Value.FrozenKeyCode
                : input.HeldGameplayKey,
            decision.InputClaimed,
            attempted,
            accepted,
            nativeOutcome != ClientActionAttemptOutcome.None
                ? nativeOutcome
                : state.LastNativeOutcome,
            state.Retry.NativeAttemptCount,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            Interlocked.Read(ref rejectedCount),
            Interlocked.Read(ref unknownCount),
            Interlocked.Read(ref softWaitCount),
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private ClientActionAttemptOutcome TryUseOnce(
        MonkHeldComboDecision decision,
        MonkHeldComboObservation priorObservation,
        SupportedPvPContext context,
        bool configurationEnabled,
        bool metadataVerified,
        bool actionHelpersSuppressedByGuard,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        out bool attempted,
        out ushort confirmationSequenceBaseline)
    {
        attempted = false;
        confirmationSequenceBaseline = 0;
        if (!decision.ShouldDispatch ||
            decision.Intent is not { IsValid: true } intent ||
            !state.HasBufferedAction ||
            state.Intent != intent ||
            state.PendingActionId != decision.ActionId ||
            state.PendingPurpose != decision.Purpose ||
            frozenTerritoryId == 0 ||
            frozenLocalAddress == nint.Zero ||
            frozenTargetAddress == nint.Zero)
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var attemptedAtBoundary = false;
        var softUnavailableAtBoundary = false;
        var before = default(ClientActionAttemptFingerprint);
        var after = default(ClientActionAttemptFingerprint);
        var carrierBefore = 0u;
        var carrierAfter = 0u;
        var targetStatusBefore = uint.MaxValue;
        var targetStatusAfter = uint.MaxValue;
        var pressurePointBefore = false;
        var pressurePointAfter = false;
        var fireResonanceBefore = false;
        var fireResonanceAfter = false;
        ushort capturedConfirmationSequenceBaseline = 0;
        try
        {
            var clientAccepted = nearAssist.RunWithoutRedirect(() =>
            {
                var currentLocal = ResolveExactLocalPlayer(objectTable.LocalPlayer);
                if (currentLocal is null ||
                    currentLocal.Address != frozenLocalAddress ||
                    clientState.TerritoryType != frozenTerritoryId ||
                    context != intent.Context)
                {
                    return false;
                }

                var currentActions = ObserveLocalActions(currentLocal);
                var candidate = ResolveExactCandidate(
                    currentLocal,
                    context,
                    intent.EnemySlot,
                    intent.Target,
                    frozenWolvesDenTargetKind,
                    currentActions.ResolvedComboActionId);
                if (candidate is null ||
                    candidate.Value.Target.Address != frozenTargetAddress)
                {
                    return false;
                }

                var currentIdentity = new TargetPressureActorIdentity(
                    currentLocal.GameObjectId,
                    currentLocal.EntityId);
                var boundaryNow = Environment.TickCount64;
                var guardSuppressed = actionHelpersSuppressedByGuard ||
                                      IsCurrentlySuppressedByGuard(
                                          currentLocal,
                                          boundaryNow);
                var hasFireResonance = HasExactOwnStatus(
                    currentLocal,
                    MonkHeldComboRules.FireResonanceStatusId,
                    currentLocal.EntityId);
                pressurePointBefore = HasExactOwnStatus(
                    candidate.Value.Target,
                    MonkHeldComboRules.PressurePointStatusId,
                    currentLocal.EntityId);
                fireResonanceBefore = hasFireResonance;
                var currentObservation = priorObservation with
                {
                    LocalPlayer = currentIdentity,
                    IsLocalPlayerAlive = IsLivePlayer(currentLocal),
                    LocalJobId = currentLocal.ClassJob.IsValid
                        ? currentLocal.ClassJob.RowId
                        : 0,
                    MetadataVerified = metadataVerified,
                    ActionHelpersSuppressedByGuard = guardSuppressed,
                    HigherPriorityClaimed = higherPriorityClaimed,
                    HeldGameplayKeyCode =
                        (int)inputFrame.Snapshot.HeldGameplayKey,
                    FrozenKeyStillDown =
                        inputFrame.IsGameplayKeyGenerationEligible(
                            (VirtualKey)intent.FrozenKeyCode),
                    ResolvedComboActionId =
                        currentActions.ResolvedComboActionId,
                    ComboActionLocallyReady =
                        currentActions.ComboActionLocallyReady &&
                        candidate.Value.ComboUseReady,
                    FireReplyLocallyReady =
                        currentActions.FireReplyLocallyReady &&
                        candidate.Value.FireReplyUseReady,
                    WindReplyLocallyReady =
                        currentActions.WindReplyLocallyReady &&
                        candidate.Value.WindReplyUseReady,
                    ThunderclapLocallyReady =
                        currentActions.ThunderclapLocallyReady &&
                        candidate.Value.ThunderclapUseReady,
                    RisingPhoenixLocallyReady =
                        currentActions.RisingPhoenixLocallyReady &&
                        currentActions.RisingPhoenixSelfUseReady,
                    HasExactOwnFireResonance = hasFireResonance,
                    NativeBoundaryReady = currentActions.NativeBoundaryReady,
                    Candidate = candidate.Value.Core,
                    HardReset = false,
                    NowMilliseconds = boundaryNow,
                };
                if (!MonkHeldComboRules.CanUseFrozenIntent(
                        state,
                        currentObservation,
                        candidate.Value.Core))
                {
                    return false;
                }

                var actionManager = ActionManager.Instance();
                if (actionManager == null) return false;
                var useTargetId =
                    decision.ActionId == MonkHeldComboRules.RisingPhoenixActionId
                        ? currentLocal.GameObjectId
                        : intent.Target.GameObjectId;
                targetStatusBefore = actionManager->GetActionStatus(
                    ActionType.Action,
                    decision.ActionId,
                    useTargetId,
                    checkRecastActive: true,
                    checkCastingActive: true);
                if (!currentActions.NativeBoundaryReady)
                {
                    softUnavailableAtBoundary = true;
                    return false;
                }

                carrierBefore = actionManager->GetAdjustedActionId(
                    MonkHeldComboRules.ComboCarrierActionId);
                before = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    decision.ActionId);
                if (carrierBefore != state.CarrierActionId ||
                    !before.IsExactActionReady(decision.ActionId) ||
                    targetStatusBefore != 0)
                {
                    softUnavailableAtBoundary =
                        carrierBefore == state.CarrierActionId;
                    return false;
                }

                attemptedAtBoundary = true;
                var comboRouteId = MonkHeldComboRules.GetNativeComboRouteId(
                    decision.ActionId,
                    decision.Purpose);
                var accepted = actionManager->UseAction(
                    ActionType.Action,
                    decision.ActionId,
                    useTargetId,
                    0,
                    comboRouteId != 0
                        ? ActionManager.UseActionMode.Combo
                        : ActionManager.UseActionMode.None,
                    comboRouteId);
                after = ClientActionAttemptBoundary.Capture(
                    actionManager,
                    decision.ActionId);
                capturedConfirmationSequenceBaseline = after.Captured
                    ? after.LastUsedActionSequence
                    : before.LastUsedActionSequence;
                carrierAfter = actionManager->GetAdjustedActionId(
                    MonkHeldComboRules.ComboCarrierActionId);
                targetStatusAfter = actionManager->GetActionStatus(
                    ActionType.Action,
                    decision.ActionId,
                    useTargetId,
                    checkRecastActive: true,
                    checkCastingActive: true);
                pressurePointAfter = HasExactOwnStatus(
                    candidate.Value.Target,
                    MonkHeldComboRules.PressurePointStatusId,
                    currentLocal.EntityId);
                fireResonanceAfter = HasExactOwnStatus(
                    currentLocal,
                    MonkHeldComboRules.FireResonanceStatusId,
                    currentLocal.EntityId);
                return accepted;
            });

            if (!attemptedAtBoundary)
            {
                return softUnavailableAtBoundary
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
            }

            confirmationSequenceBaseline =
                capturedConfirmationSequenceBaseline;

            return MonkHeldComboRules.ClassifyActionBoundary(
                clientAccepted,
                decision.ActionId,
                state.CarrierActionId,
                targetStatusBefore,
                targetStatusAfter,
                carrierBefore,
                carrierAfter,
                pressurePointBefore,
                pressurePointAfter,
                fireResonanceBefore,
                fireResonanceAfter,
                before,
                after);
        }
        catch (Exception exception)
        {
            LogFailure(
                exception,
                Environment.TickCount64,
                "Seiton Sense Monk held-combo native boundary failed.");
            return attemptedAtBoundary
                ? ClientActionAttemptOutcome.AcceptanceUnknown
                : softUnavailableAtBoundary
                    ? ClientActionAttemptOutcome.SoftUnavailable
                    : ClientActionAttemptOutcome.NotInvoked;
        }
        finally
        {
            attempted = attemptedAtBoundary;
        }
    }

    private RuntimeCandidate? ResolveCurrentBestCandidate(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        uint resolvedComboActionId,
        bool fireReplyLocallyReady,
        bool windReplyLocallyReady,
        bool thunderclapLocallyReady,
        bool hasExactOwnFireResonance,
        bool strikingDummyMetadataVerified)
    {
        if (!MonkHeldComboRules.IsExactComboAction(resolvedComboActionId))
            return null;

        if (context == SupportedPvPContext.WolvesDen)
        {
            if (!TryResolveCurrentWolvesDenTarget(
                    localPlayer,
                    strikingDummyMetadataVerified,
                    out _,
                    out var identity,
                    out var kind))
            {
                return null;
            }

            var candidate = ResolveExactCandidate(
                localPlayer,
                context,
                0,
                identity,
                kind,
                resolvedComboActionId);
            if (!candidate.HasValue) return null;
            var selected = MonkHeldComboRules.SelectBestCandidate(
                context,
                resolvedComboActionId,
                fireReplyLocallyReady,
                windReplyLocallyReady,
                thunderclapLocallyReady,
                hasExactOwnFireResonance,
                [candidate.Value.Core]);
            return selected.HasValue ? candidate : null;
        }

        if (context != SupportedPvPContext.CrystallineConflict) return null;
        var runtimeCandidates = new List<RuntimeCandidate>(
            EnemySlotRules.LastSlot - EnemySlotRules.FirstSlot + 1);
        for (var slot = EnemySlotRules.FirstSlot;
             slot <= EnemySlotRules.LastSlot;
             slot++)
        {
            var enemy = EnemySlotResolver.Resolve(objectTable, slot);
            if (!HasValidNativeIdentity(enemy)) continue;
            var identity = new TargetPressureActorIdentity(
                enemy!.GameObjectId,
                enemy.EntityId);
            var candidate = ResolveExactCandidate(
                localPlayer,
                context,
                slot,
                identity,
                MonkWolvesDenTargetKind.None,
                resolvedComboActionId);
            if (candidate.HasValue) runtimeCandidates.Add(candidate.Value);
        }

        if (runtimeCandidates.Count == 0) return null;
        var core = new MonkHeldComboCandidate[runtimeCandidates.Count];
        for (var index = 0; index < runtimeCandidates.Count; index++)
            core[index] = runtimeCandidates[index].Core;
        var best = MonkHeldComboRules.SelectBestCandidate(
            context,
            resolvedComboActionId,
            fireReplyLocallyReady,
            windReplyLocallyReady,
            thunderclapLocallyReady,
            hasExactOwnFireResonance,
            core);
        if (!best.HasValue) return null;
        foreach (var candidate in runtimeCandidates)
        {
            if (candidate.Core == best.Value) return candidate;
        }

        return null;
    }

    private RuntimeCandidate? ResolveExactCandidate(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        int enemySlot,
        TargetPressureActorIdentity expectedTarget,
        MonkWolvesDenTargetKind expectedWolvesKind,
        uint resolvedComboActionId)
    {
        if (!expectedTarget.IsValid ||
            expectedTarget == new TargetPressureActorIdentity(
                localPlayer.GameObjectId,
                localPlayer.EntityId) ||
            !MonkHeldComboRules.IsExactComboAction(resolvedComboActionId))
        {
            return null;
        }

        IBattleChara? target;
        var exactCanonicalIdentity = false;
        var wolvesKind = MonkWolvesDenTargetKind.None;
        switch (context)
        {
            case SupportedPvPContext.CrystallineConflict:
            {
                if (!EnemySlotRules.IsValidSlot(enemySlot)) return null;
                var player = EnemySlotResolver.Resolve(objectTable, enemySlot);
                if (!HasValidNativeIdentity(player) ||
                    player!.GameObjectId != expectedTarget.GameObjectId ||
                    player.EntityId != expectedTarget.EntityId)
                {
                    return null;
                }

                var byObjectId = objectTable.SearchById(player.GameObjectId)
                    as IPlayerCharacter;
                var byEntityId = objectTable.SearchByEntityId(player.EntityId)
                    as IPlayerCharacter;
                exactCanonicalIdentity =
                    HasSameNativeIdentity(player, byObjectId) &&
                    HasSameNativeIdentity(player, byEntityId) &&
                    HasSameNativeIdentity(
                        player,
                        EnemySlotResolver.Resolve(objectTable, enemySlot));
                target = player;
                break;
            }
            case SupportedPvPContext.WolvesDen:
            {
                if (enemySlot != 0 ||
                    expectedWolvesKind is not
                        (MonkWolvesDenTargetKind.DuelOpponent or
                         MonkWolvesDenTargetKind.StrikingDummy) ||
                    !TryResolveCurrentWolvesDenTarget(
                        localPlayer,
                        strikingDummyMetadataVerified: true,
                        out target,
                        out var currentIdentity,
                        out wolvesKind) ||
                    currentIdentity != expectedTarget ||
                    wolvesKind != expectedWolvesKind)
                {
                    return null;
                }

                exactCanonicalIdentity = true;
                break;
            }
            default:
                return null;
        }

        if (!exactCanonicalIdentity ||
            !HasValidNativeIdentity(target) ||
            target!.GameObjectId != expectedTarget.GameObjectId ||
            target.EntityId != expectedTarget.EntityId)
        {
            return null;
        }

        return BuildRuntimeCandidate(
            localPlayer,
            context,
            enemySlot,
            target,
            expectedTarget,
            wolvesKind,
            resolvedComboActionId);
    }

    private RuntimeCandidate? BuildRuntimeCandidate(
        IPlayerCharacter localPlayer,
        SupportedPvPContext context,
        int enemySlot,
        IBattleChara target,
        TargetPressureActorIdentity identity,
        MonkWolvesDenTargetKind wolvesKind,
        uint resolvedComboActionId)
    {
        if (!HasValidNativeIdentity(localPlayer) ||
            !HasValidNativeIdentity(target) ||
            !identity.IsValid ||
            !MonkHeldComboRules.IsExactComboAction(resolvedComboActionId))
        {
            return null;
        }

        var sourceObject = GetNativeObject(localPlayer);
        var targetObject = GetNativeObject(target);
        if (sourceObject == null || targetObject == null) return null;
        var actionManager = ActionManager.Instance();
        if (actionManager == null) return null;

        var comboRange = HasNativeRangeAndLineOfSight(
            resolvedComboActionId,
            sourceObject,
            targetObject);
        var fireRange = HasNativeRangeAndLineOfSight(
            MonkHeldComboRules.FireReplyActionId,
            sourceObject,
            targetObject);
        var windRange = HasNativeRangeAndLineOfSight(
            MonkHeldComboRules.WindReplyActionId,
            sourceObject,
            targetObject);
        var thunderRange = HasNativeRangeAndLineOfSight(
            MonkHeldComboRules.ThunderclapActionId,
            sourceObject,
            targetObject);
        var phantomRange = HasNativeRangeAndLineOfSight(
            MonkHeldComboRules.PhantomRushActionId,
            sourceObject,
            targetObject);
        var pressurePoint = HasExactOwnStatus(
            target,
            MonkHeldComboRules.PressurePointStatusId,
            localPlayer.EntityId);

        var core = new MonkHeldComboCandidate(
            context,
            enemySlot,
            identity,
            ExactCanonicalIdentity: true,
            Alive: IsLiveBattleCharacter(target),
            Targetable: target.IsTargetable,
            target.CurrentHp,
            target.MaxHp,
            ComboTargetReady: comboRange,
            FireReplyTargetReady: fireRange,
            WindReplyTargetReady: windRange,
            ThunderclapTargetReady: thunderRange,
            PhantomRushTargetReady: phantomRange,
            HasExactOwnPressurePoint: pressurePoint);
        return new RuntimeCandidate(
            core,
            target,
            wolvesKind,
            ComboUseReady: comboRange &&
                IsTargetActionReady(
                    actionManager,
                    resolvedComboActionId,
                    target.GameObjectId),
            FireReplyUseReady: fireRange &&
                IsTargetActionReady(
                    actionManager,
                    MonkHeldComboRules.FireReplyActionId,
                    target.GameObjectId),
            WindReplyUseReady: windRange &&
                IsTargetActionReady(
                    actionManager,
                    MonkHeldComboRules.WindReplyActionId,
                    target.GameObjectId),
            ThunderclapUseReady: thunderRange &&
                IsTargetActionReady(
                    actionManager,
                    MonkHeldComboRules.ThunderclapActionId,
                    target.GameObjectId),
            PhantomRushUseReady: phantomRange &&
                IsTargetActionReady(
                    actionManager,
                    MonkHeldComboRules.PhantomRushActionId,
                    target.GameObjectId));
    }

    private bool TryResolveCurrentWolvesDenTarget(
        IPlayerCharacter localPlayer,
        bool strikingDummyMetadataVerified,
        out IBattleChara? target,
        out TargetPressureActorIdentity identity,
        out MonkWolvesDenTargetKind kind)
    {
        target = null;
        identity = default;
        kind = MonkWolvesDenTargetKind.None;
        if (StrictWolvesDenStrikingDummyResolver.TryResolveExactCurrentHardTarget(
                objectTable,
                strikingDummyMetadataVerified,
                localPlayer,
                out var dummy,
                out var dummyIdentity,
                out _))
        {
            target = dummy;
            identity = dummyIdentity;
            kind = MonkWolvesDenTargetKind.StrikingDummy;
            return true;
        }

        var nativeHardTargetId = GetNativeHardTargetId(localPlayer);
        if (!IsNetworkObjectId(nativeHardTargetId)) return false;
        var duelOpponent = WolvesDenOpponentResolver.Resolve(
            objectTable,
            localPlayer,
            out var nativeDuelEnemyEntityId,
            out var nativePlayerResolved,
            out var hostileFlag);
        if (!nativePlayerResolved ||
            !hostileFlag ||
            !HasValidNativeIdentity(duelOpponent) ||
            nativeDuelEnemyEntityId != duelOpponent!.EntityId ||
            !ActorIdMatches(nativeHardTargetId, duelOpponent) ||
            !IsLiveTargetableHostilePlayer(localPlayer, duelOpponent))
        {
            return false;
        }

        var byObjectId = objectTable.SearchById(duelOpponent.GameObjectId)
            as IPlayerCharacter;
        var byEntityId = objectTable.SearchByEntityId(duelOpponent.EntityId)
            as IPlayerCharacter;
        if (!HasSameNativeIdentity(duelOpponent, byObjectId) ||
            !HasSameNativeIdentity(duelOpponent, byEntityId))
        {
            return false;
        }

        var stable = WolvesDenOpponentResolver.Resolve(
            objectTable,
            localPlayer,
            out var stableEnemyEntityId,
            out var stableNativePlayerResolved,
            out var stableHostileFlag);
        if (!stableNativePlayerResolved ||
            !stableHostileFlag ||
            stableEnemyEntityId != nativeDuelEnemyEntityId ||
            !HasSameNativeIdentity(duelOpponent, stable) ||
            GetNativeHardTargetId(localPlayer) != nativeHardTargetId)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(
            duelOpponent.GameObjectId,
            duelOpponent.EntityId);
        target = duelOpponent;
        kind = MonkWolvesDenTargetKind.DuelOpponent;
        return identity.IsValid;
    }

    private static LocalActionState ObserveLocalActions(
        IPlayerCharacter localPlayer)
    {
        if (!HasValidNativeIdentity(localPlayer) ||
            localPlayer.ClassJob.IsValid != true ||
            localPlayer.ClassJob.RowId != MonkHeldComboRules.MonkJobId)
        {
            return default;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager == null) return default;
        var resolved = actionManager->GetAdjustedActionId(
            MonkHeldComboRules.ComboCarrierActionId);
        if (!MonkHeldComboRules.IsExactComboAction(resolved))
            return default;

        var comboFingerprint = ClientActionAttemptBoundary.Capture(
            actionManager,
            resolved);
        var phoenixLocallyReady = IsLocallyReady(
            actionManager,
            MonkHeldComboRules.RisingPhoenixActionId);
        return new LocalActionState(
            resolved,
            IsLocallyReady(actionManager, resolved),
            IsLocallyReady(
                actionManager,
                MonkHeldComboRules.FireReplyActionId),
            IsLocallyReady(
                actionManager,
                MonkHeldComboRules.WindReplyActionId),
            IsLocallyReady(
                actionManager,
                MonkHeldComboRules.ThunderclapActionId),
            phoenixLocallyReady,
            phoenixLocallyReady &&
                IsTargetActionReady(
                    actionManager,
                    MonkHeldComboRules.RisingPhoenixActionId,
                    localPlayer.GameObjectId),
            HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                actionManager->AnimationLock,
                localPlayer.IsCasting,
                actionManager->CastActionId,
                actionManager->ActionQueued),
            comboFingerprint.LastUsedActionSequence);
    }

    private static bool IsLocallyReady(
        ActionManager* actionManager,
        uint actionId)
    {
        var fingerprint = ClientActionAttemptBoundary.Capture(
            actionManager,
            actionId);
        return fingerprint.Captured &&
               fingerprint.AdjustedActionId == actionId &&
               fingerprint.IsActionOffCooldown &&
               fingerprint.ResourceStatus == 0;
    }

    private static bool IsTargetActionReady(
        ActionManager* actionManager,
        uint actionId,
        ulong targetId) =>
        actionManager->GetActionStatus(
            ActionType.Action,
            actionId,
            targetId,
            checkRecastActive: true,
            checkCastingActive: true) == 0;

    private static bool HasNativeRangeAndLineOfSight(
        uint actionId,
        GameObject* source,
        GameObject* target) =>
        SeitonRangeRules.HasNativeRangeAndLineOfSight(
            ActionManager.GetActionInRangeOrLoS(
                actionId,
                source,
                target));

    private static bool HasConfirmationBoundaryReopened(
        MonkHeldComboState current,
        LocalActionState actions) =>
        (current.Phase is MonkHeldComboPhase.AwaitPressurePoint or
            MonkHeldComboPhase.AwaitFireResonance) &&
        actions.NativeBoundaryReady &&
        actions.LastUsedActionSequence !=
            current.ConfirmationSequenceBaseline;

    private static bool HasExactOwnStatus(
        IBattleChara actor,
        uint statusId,
        uint localEntityId)
    {
        if (!IsNetworkEntityId(localEntityId)) return false;
        var ownMatches = 0;
        foreach (var status in actor.StatusList)
        {
            if (status.StatusId != statusId) continue;
            if (status.SourceId != localEntityId) continue;
            if (!float.IsFinite(status.RemainingTime) ||
                status.RemainingTime <= 0f ||
                ++ownMatches > 1)
            {
                return false;
            }
        }

        return ownMatches == 1;
    }

    private static bool IsLiveTargetableHostilePlayer(
        IPlayerCharacter localPlayer,
        IPlayerCharacter candidate) =>
        candidate.GameObjectId != localPlayer.GameObjectId &&
        candidate.EntityId != localPlayer.EntityId &&
        (candidate.StatusFlags & StatusFlags.Hostile) != 0 &&
        IsLivePlayer(candidate) &&
        candidate.IsTargetable;

    private IPlayerCharacter? ResolveExactLocalPlayer(IPlayerCharacter? expected)
    {
        if (!HasValidNativeIdentity(expected)) return null;
        var current = objectTable.LocalPlayer;
        if (!HasSameNativeIdentity(expected, current)) return null;
        var byObjectId = objectTable.SearchById(expected!.GameObjectId)
            as IPlayerCharacter;
        var byEntityId = objectTable.SearchByEntityId(expected.EntityId)
            as IPlayerCharacter;
        return HasSameNativeIdentity(expected, byObjectId) &&
               HasSameNativeIdentity(expected, byEntityId)
            ? expected
            : null;
    }

    private bool IsCurrentlySuppressedByGuard(
        IPlayerCharacter? localPlayer,
        long nowMilliseconds)
    {
        if (localPlayer is null) return false;
        if (DefensiveUtilityProbe.HasActiveGuard(localPlayer)) return true;
        return nearAssist.TryGetRecentExactLocalGuardAttempt(
            clientState.TerritoryType,
            localPlayer.GameObjectId,
            localPlayer.EntityId,
            nowMilliseconds,
            DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
            out _);
    }

    private void FreezeRuntime(
        MonkHeldComboIntent intent,
        uint territoryId,
        nint localAddress,
        nint targetAddress,
        MonkWolvesDenTargetKind wolvesDenTargetKind)
    {
        frozenTerritoryId = territoryId;
        frozenLocalAddress = localAddress;
        frozenTargetAddress = targetAddress;
        frozenWolvesDenTargetKind = wolvesDenTargetKind;
    }

    private bool FrozenRuntimeMatches(
        MonkHeldComboIntent intent,
        uint territoryId,
        nint localAddress) =>
        intent.IsValid &&
        frozenTerritoryId == territoryId &&
        frozenLocalAddress != nint.Zero &&
        frozenLocalAddress == localAddress &&
        frozenTargetAddress != nint.Zero;

    private void ClearFrozenRuntime()
    {
        frozenTerritoryId = 0;
        frozenLocalAddress = nint.Zero;
        frozenTargetAddress = nint.Zero;
        frozenWolvesDenTargetKind = MonkWolvesDenTargetKind.None;
    }

    private MonkHeldComboProbeSnapshot PublishTerminalSnapshot(
        MonkHeldComboDecisionKind decision,
        MonkHeldComboDecisionReason reason,
        string message)
    {
        var result = MonkHeldComboProbeSnapshot.Initial with
        {
            Decision = decision,
            Reason = reason,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            RejectedCount = Interlocked.Read(ref rejectedCount),
            UnknownCount = Interlocked.Read(ref unknownCount),
            SoftWaitCount = Interlocked.Read(ref softWaitCount),
            LastEvent = message,
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private static string DescribeNativeResult(
        uint actionId,
        MonkHeldComboActionPurpose purpose,
        ClientActionAttemptOutcome outcome,
        MonkHeldComboNativeAttemptDecision completion) =>
        outcome switch
        {
            ClientActionAttemptOutcome.ClientAccepted =>
                $"Monk {purpose} {actionId} client-accepted",
            ClientActionAttemptOutcome.ClientRejected when completion.RetryScheduled =>
                $"Monk {purpose} {actionId} client-rejected; exact intent retained",
            ClientActionAttemptOutcome.ClientRejected =>
                $"Monk {purpose} {actionId} retry limit reached",
            ClientActionAttemptOutcome.SoftUnavailable =>
                $"Monk {purpose} {actionId} waiting for native boundary",
            ClientActionAttemptOutcome.AcceptanceUnknown =>
                $"Monk {purpose} {actionId} acceptance ambiguous; intent terminal",
            _ => completion.Reason.ToString(),
        };

    private void LogFailure(
        Exception exception,
        long nowMilliseconds,
        string message)
    {
        if (nowMilliseconds >= 0 && nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds < 0
            ? 0
            : nowMilliseconds > long.MaxValue - 10_000
                ? long.MaxValue
                : nowMilliseconds + 10_000;
        log.Error(exception, message);
    }

    private static bool IsLivePlayer(IPlayerCharacter? player) =>
        HasValidNativeIdentity(player) &&
        !player!.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp > 0 &&
        player.CurrentHp <= player.MaxHp;

    private static bool IsLiveBattleCharacter(IBattleChara? actor) =>
        HasValidNativeIdentity(actor) &&
        !actor!.IsDead &&
        actor.CurrentHp > 0 &&
        actor.MaxHp > 0 &&
        actor.CurrentHp <= actor.MaxHp;

    private static bool HasValidNativeIdentity(IGameObject? actor)
    {
        if (actor is null ||
            actor.Address == nint.Zero ||
            !IsNetworkObjectId(actor.GameObjectId) ||
            !IsNetworkEntityId(actor.EntityId))
        {
            return false;
        }

        var native = (GameObject*)actor.Address;
        return native != null && native->EntityId == actor.EntityId;
    }

    private static bool HasSameNativeIdentity(
        IGameObject? left,
        IGameObject? right) =>
        HasValidNativeIdentity(left) &&
        HasValidNativeIdentity(right) &&
        left!.Address == right!.Address &&
        left.GameObjectId == right.GameObjectId &&
        left.EntityId == right.EntityId;

    private static GameObject* GetNativeObject(IGameObject actor)
    {
        var native = (GameObject*)actor.Address;
        return native != null && native->EntityId == actor.EntityId
            ? native
            : null;
    }

    private static ulong GetNativeHardTargetId(IPlayerCharacter localPlayer)
    {
        if (!HasValidNativeIdentity(localPlayer)) return 0;
        var character = (Character*)localPlayer.Address;
        return character == null ? 0 : character->GetTargetId().Id;
    }

    private static bool ActorIdMatches(
        ulong actorId,
        TargetPressureActorIdentity actor) =>
        actor.IsValid &&
        (actorId == actor.GameObjectId ||
         actorId <= uint.MaxValue && (uint)actorId == actor.EntityId);

    private static bool ActorIdMatches(ulong actorId, IGameObject actor) =>
        ActorIdMatches(
            actorId,
            new TargetPressureActorIdentity(
                actor.GameObjectId,
                actor.EntityId));

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    private static bool IsNetworkObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;

    private static bool IsExpectedComboAction(
        GameAction action,
        ActionTransient transient,
        ComboMetadata definition) =>
        MonkHeldComboRules.IsExactComboAction(definition.ActionId) &&
        MonkHeldComboRules.GetExpectedPreviousComboAction(
            definition.ActionId) == definition.PreviousActionId &&
        action.Name.ToString() == definition.Name &&
        action.Icon == definition.Icon &&
        action.IsPvP &&
        !action.IsPlayerAction &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == MonkHeldComboRules.MonkJobId &&
        action.ClassJobCategory.IsValid &&
        action.ClassJobCategory.RowId ==
            MonkHeldComboRules.MonkClassJobCategoryId &&
        action.ActionCategory.IsValid &&
        action.ActionCategory.RowId == 3 &&
        action.Range == definition.Range &&
        action.EffectRange == definition.EffectRange &&
        action.CastType == definition.CastType &&
        action.Cast100ms == 0 &&
        action.Recast100ms == 25 &&
        action.CooldownGroup == 58 &&
        action.AdditionalCooldownGroup == 0 &&
        action.MaxCharges == 0 &&
        action.ActionCombo.RowId == definition.PreviousActionId &&
        action.ActionProcStatus.RowId == 0 &&
        action.PrimaryCostType == 0 &&
        action.PrimaryCostValue == 0 &&
        action.SecondaryCostType == definition.SecondaryCostType &&
        action.CanTargetHostile &&
        !action.CanTargetSelf &&
        !action.CanTargetParty &&
        !action.CanTargetAlliance &&
        !action.CanTargetAlly &&
        !action.CanTargetOwnPet &&
        !action.CanTargetPartyPet &&
        !action.TargetArea &&
        action.RequiresLineOfSight &&
        action.NeedToFaceTarget &&
        !action.AffectsPosition &&
        transient.Description.ToString().Contains(
            "This action cannot be assigned to a hotbar.",
            StringComparison.Ordinal);

    private static bool IsExpectedAuxiliary(
        GameAction action,
        ActionTransient transient,
        string name,
        uint icon,
        byte actionCategory,
        sbyte range,
        byte effectRange,
        byte castType,
        ushort recast100ms,
        byte cooldownGroup,
        byte additionalCooldownGroup,
        byte maximumCharges,
        bool canTargetSelf,
        bool canTargetHostile,
        bool canTargetParty,
        bool canTargetAlly,
        bool affectsPosition)
    {
        var actionId = action.RowId;
        if (actionId is not
            (MonkHeldComboRules.FireReplyActionId or
             MonkHeldComboRules.WindReplyActionId or
             MonkHeldComboRules.RisingPhoenixActionId or
             MonkHeldComboRules.ThunderclapActionId))
        {
            return false;
        }

        var canTargetAlliance =
            actionId == MonkHeldComboRules.ThunderclapActionId;
        var needToFaceTarget =
            actionId != MonkHeldComboRules.ThunderclapActionId;
        return action.Name.ToString() == name &&
               action.Icon == icon &&
               action.IsPvP &&
               action.IsPlayerAction &&
               action.ClassJob.IsValid &&
               action.ClassJob.RowId == MonkHeldComboRules.MonkJobId &&
               action.ClassJobCategory.IsValid &&
               action.ClassJobCategory.RowId ==
                   MonkHeldComboRules.MonkClassJobCategoryId &&
               action.ActionCategory.IsValid &&
               action.ActionCategory.RowId == actionCategory &&
               action.Range == range &&
               action.EffectRange == effectRange &&
               action.CastType == castType &&
               action.Cast100ms == 0 &&
               action.Recast100ms == recast100ms &&
               action.CooldownGroup == cooldownGroup &&
               action.AdditionalCooldownGroup == additionalCooldownGroup &&
               action.MaxCharges == maximumCharges &&
               action.ActionProcStatus.RowId == 0 &&
               action.PrimaryCostType == 0 &&
               action.PrimaryCostValue == 0 &&
               action.CanTargetSelf == canTargetSelf &&
               action.CanTargetHostile == canTargetHostile &&
               action.CanTargetParty == canTargetParty &&
               action.CanTargetAlliance == canTargetAlliance &&
               action.CanTargetAlly == canTargetAlly &&
               !action.CanTargetOwnPet &&
               !action.CanTargetPartyPet &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.NeedToFaceTarget == needToFaceTarget &&
               action.PreservesCombo &&
               action.AffectsPosition == affectsPosition &&
               transient.Description.ToString().Length > 0;
    }

    private static bool IsExpectedStatus(
        GameStatus status,
        string name,
        uint icon,
        byte category) =>
        status.Name.ToString() == name &&
        status.Icon == icon &&
        status.StatusCategory == category &&
        !status.CanDispel &&
        !status.IsPermanent;

    private readonly record struct ComboMetadata(
        uint ActionId,
        string Name,
        uint Icon,
        sbyte Range,
        byte EffectRange,
        byte CastType,
        uint PreviousActionId,
        byte SecondaryCostType);

    private readonly record struct LocalActionState(
        uint ResolvedComboActionId,
        bool ComboActionLocallyReady,
        bool FireReplyLocallyReady,
        bool WindReplyLocallyReady,
        bool ThunderclapLocallyReady,
        bool RisingPhoenixLocallyReady,
        bool RisingPhoenixSelfUseReady,
        bool NativeBoundaryReady,
        ushort LastUsedActionSequence);

    private readonly record struct RuntimeCandidate(
        MonkHeldComboCandidate Core,
        IBattleChara Target,
        MonkWolvesDenTargetKind WolvesDenTargetKind,
        bool ComboUseReady,
        bool FireReplyUseReady,
        bool WindReplyUseReady,
        bool ThunderclapUseReady,
        bool PhantomRushUseReady);
}
