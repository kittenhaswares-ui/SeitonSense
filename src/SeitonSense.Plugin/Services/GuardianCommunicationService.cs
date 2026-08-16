using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal sealed record GuardianCommunicationDiagnostics(
    bool Configured,
    bool MetadataVerified,
    bool ActiveInCurrentContext,
    bool TextInputStateKnown,
    bool TextInputActive,
    GuardianTeamCommunicationPhase Phase,
    GuardianTeamCommunicationDecisionKind Decision,
    GuardianTeamCommunicationDecisionReason Reason,
    long LastConsumedEpisodeToken,
    long ActiveEpisodeToken,
    int PartySlot,
    ulong LocalGameObjectId,
    uint LocalEntityId,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    ulong Bind1GameObjectId,
    long Bind1MarkerTime,
    ulong Bind2GameObjectId,
    long Bind2MarkerTime,
    bool OwnsBind1,
    bool OwnsBind2,
    long CleanupRemainingMilliseconds,
    long ObservedEpisodeCount,
    long CommandDecisionCount,
    long QuickChatInvocationCount,
    long MarkerSetInvocationCount,
    long MarkerClearInvocationCount,
    long DeferredMarkerCount,
    long TerminalFailureCount,
    string LastEvent)
{
    internal static GuardianCommunicationDiagnostics Initial { get; } = new(
        false,
        false,
        false,
        false,
        true,
        GuardianTeamCommunicationPhase.Idle,
        GuardianTeamCommunicationDecisionKind.Waiting,
        GuardianTeamCommunicationDecisionReason.WaitingForAcceptedGuardian,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        "Not observed");

    internal string ToChatLine() =>
        $"configured={Configured},meta={MetadataVerified},active={ActiveInCurrentContext}," +
        $"text={TextInputStateKnown}/{TextInputActive},phase={Phase},decision={Decision}/{Reason}," +
        $"token={LastConsumedEpisodeToken}/{ActiveEpisodeToken},P={PartySlot}," +
        $"local={LocalGameObjectId:X}/{LocalEntityId:X},target={TargetGameObjectId:X}/{TargetEntityId:X}," +
        $"bind1={Bind1GameObjectId:X}@{Bind1MarkerTime}/{OwnsBind1}," +
        $"bind2={Bind2GameObjectId:X}@{Bind2MarkerTime}/{OwnsBind2}," +
        $"cleanup={CleanupRemainingMilliseconds},episodes={ObservedEpisodeCount}," +
        $"decisions={CommandDecisionCount},invoked={QuickChatInvocationCount}/" +
        $"{MarkerSetInvocationCount}/{MarkerClearInvocationCount}," +
        $"deferred={DeferredMarkerCount},terminal={TerminalFailureCount},last={LastEvent}";
}

/// <summary>
/// Turns a proven, client-accepted automatic Guardian episode into one bounded
/// Quick Chat / Bind-pair sequence. The pure Core state machine owns ordering,
/// dedupe, confirmation and cleanup; this class only supplies exact live state
/// and dispatches one closed command decision per framework tick.
/// </summary>
internal sealed class GuardianCommunicationService
{
    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IDutyState dutyState;
    private readonly IPluginLog log;
    private readonly ReviewedPvpCommandDispatcher commands;
    private readonly GuardianCommunicationMetadataValidation metadata;

    private GuardianTeamCommunicationState state = GuardianTeamCommunicationState.Initial;
    private GuardianCommunicationDiagnostics diagnostics = GuardianCommunicationDiagnostics.Initial;
    private AcceptedAutoGuardianEpisode? lastObservedEpisode;
    private long lastObservedEpisodeToken;
    private long observedEpisodeCount;
    private long commandDecisionCount;
    private long quickChatInvocationCount;
    private long markerSetInvocationCount;
    private long markerClearInvocationCount;
    private long deferredMarkerCount;
    private long terminalFailureCount;
    private long nextErrorLogAt;
    private bool forceHardReset;

    internal GuardianCommunicationService(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IDutyState dutyState,
        IDataManager dataManager,
        IPluginLog log,
        ReviewedPvpCommandDispatcher commands)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.dutyState = dutyState;
        this.log = log;
        this.commands = commands;
        metadata = GuardianCommunicationMetadataGuard.Validate(
            dataManager,
            clientState.ClientLanguage,
            log);
    }

    internal GuardianCommunicationDiagnostics Diagnostics => Volatile.Read(ref diagnostics);

    internal GuardianCommunicationDiagnostics Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        AcceptedAutoGuardianEpisode? acceptedEpisode,
        long nowMilliseconds,
        bool hardReset = false)
    {
        nowMilliseconds = Math.Max(0, nowMilliseconds);
        var effectiveHardReset = hardReset || forceHardReset;
        forceHardReset = false;
        if (effectiveHardReset) lastObservedEpisode = null;

        GuardianTeamCommunicationEpisode? coreEpisode = null;
        if (acceptedEpisode is { IsValid: true } accepted)
        {
            coreEpisode = new GuardianTeamCommunicationEpisode(
                accepted.Token,
                accepted.AcceptedAtMilliseconds,
                accepted.LocalPlayer,
                accepted.Target,
                accepted.PartySlot);
            if (accepted.Token > lastObservedEpisodeToken)
            {
                lastObservedEpisodeToken = accepted.Token;
                Interlocked.Increment(ref observedEpisodeCount);
                lastObservedEpisode = accepted;
            }
            else if (lastObservedEpisode is null ||
                     lastObservedEpisode.Value.Token == accepted.Token)
            {
                lastObservedEpisode = accepted;
            }
        }

        var episodeForResolution = state.Episode ?? coreEpisode;
        var local = ResolveExactLocal(localPlayer);
        var partyTarget = ResolveExactPartyTarget(episodeForResolution?.PartySlot ?? 0);
        ReadMarkerObservations(out var bind1, out var bind2);
        var textInputStateKnown = TryGetTextInputState(out var textInputActive);
        var isCrystallineConflict =
            context == SupportedPvPContext.CrystallineConflict &&
            ResolveSupportedPvPContext() == SupportedPvPContext.CrystallineConflict;
        var configured = IsCommunicationConfigured() &&
                         localPlayer is not null &&
                         localPlayer.ClassJob.IsValid &&
                         localPlayer.ClassJob.RowId == EnemyCombatConstants.PaladinJobId;
        var observation = new GuardianTeamCommunicationObservation(
            configured,
            isCrystallineConflict,
            effectiveHardReset,
            textInputStateKnown,
            textInputActive,
            nowMilliseconds,
            coreEpisode,
            local,
            partyTarget,
            bind1,
            bind2);

        var decision = GuardianTeamCommunicationRules.Observe(state, observation);
        state = decision.State;
        var lastEvent = decision.Reason.ToString();
        if (decision.ShouldIssueCommand && decision.Command is { } command)
        {
            commandDecisionCount++;
            var outcome = DispatchOnce(
                command,
                localPlayer,
                context,
                nowMilliseconds,
                out var dispatchEvent);
            state = GuardianTeamCommunicationRules.ApplyCommandResult(state, command, outcome);
            CountOutcome(command.Kind, outcome);
            lastEvent = $"{decision.Reason}: {dispatchEvent}";
        }

        var result = CreateDiagnostics(
            configured,
            context,
            textInputStateKnown,
            textInputActive,
            decision,
            bind1,
            bind2,
            nowMilliseconds,
            lastEvent);
        Volatile.Write(ref diagnostics, result);
        return result;
    }

    internal void Reset()
    {
        state = GuardianTeamCommunicationState.Initial with
        {
            LastConsumedEpisodeToken = Math.Max(
                state.LastConsumedEpisodeToken,
                lastObservedEpisodeToken),
        };
        lastObservedEpisode = null;
        forceHardReset = false;
        Volatile.Write(ref diagnostics, GuardianCommunicationDiagnostics.Initial with
        {
            Configured = configuration.PaladinGuardianAnnounceAndMark,
            MetadataVerified = metadata.Verified,
            LastConsumedEpisodeToken = state.LastConsumedEpisodeToken,
            ObservedEpisodeCount = Interlocked.Read(ref observedEpisodeCount),
            CommandDecisionCount = Interlocked.Read(ref commandDecisionCount),
            QuickChatInvocationCount = Interlocked.Read(ref quickChatInvocationCount),
            MarkerSetInvocationCount = Interlocked.Read(ref markerSetInvocationCount),
            MarkerClearInvocationCount = Interlocked.Read(ref markerClearInvocationCount),
            DeferredMarkerCount = Interlocked.Read(ref deferredMarkerCount),
            TerminalFailureCount = Interlocked.Read(ref terminalFailureCount),
            LastEvent = "Reset",
        });
    }

    internal void FailClosed(long nowMilliseconds, Exception? exception = null)
    {
        forceHardReset = true;
        if (exception is not null && nowMilliseconds >= nextErrorLogAt)
        {
            nextErrorLogAt = nowMilliseconds + 10_000;
            log.Error(exception, "Seiton Sense Guardian communication failed closed.");
        }

        Volatile.Write(ref diagnostics, Diagnostics with
        {
            ActiveInCurrentContext = false,
            LastEvent = "Failed closed; hard reset scheduled",
        });
    }

    internal void TryClearOneExactOwnershipOnDispose(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        long nowMilliseconds)
    {
        try
        {
            if (state.Episode is not { } episode ||
                context != SupportedPvPContext.CrystallineConflict)
            {
                return;
            }

            ReadMarkerObservations(out var bind1, out var bind2);
            GuardianTeamCommunicationCommand? command = null;
            if (state.Bind2Ownership is { } bind2Ownership &&
                bind2.GameObjectId == bind2Ownership.Actor.GameObjectId &&
                bind2.MarkerTime == bind2Ownership.MarkerTime)
            {
                var exactTarget = ResolveExactPartyTarget(episode.PartySlot);
                if (exactTarget.Exact && exactTarget.Actor == episode.Target)
                {
                    command = new GuardianTeamCommunicationCommand(
                        GuardianTeamCommunicationCommandKind.ClearBind2,
                        episode.Token,
                        episode.PartySlot,
                        episode.Target,
                        GuardianTeamCommunicationRules.Bind2MarkerIndex,
                        bind2Ownership.MarkerTime);
                }
            }

            if (command is null &&
                state.Bind1Ownership is { } bind1Ownership &&
                bind1.GameObjectId == bind1Ownership.Actor.GameObjectId &&
                bind1.MarkerTime == bind1Ownership.MarkerTime)
            {
                command = new GuardianTeamCommunicationCommand(
                    GuardianTeamCommunicationCommandKind.ClearBind1,
                    episode.Token,
                    0,
                    episode.LocalPlayer,
                    GuardianTeamCommunicationRules.Bind1MarkerIndex,
                    bind1Ownership.MarkerTime);
            }

            if (command is not { } exactCommand) return;

            var outcome = DispatchOnce(
                exactCommand,
                localPlayer,
                context,
                Math.Max(0, nowMilliseconds),
                out _);
            CountOutcome(exactCommand.Kind, outcome);
        }
        catch
        {
            // Unload cannot wait or retry. Any uncertainty leaves the marker
            // untouched rather than risking another player's sign.
        }
    }

    private GuardianCommunicationDiagnostics CreateDiagnostics(
        bool configured,
        SupportedPvPContext context,
        bool textInputStateKnown,
        bool textInputActive,
        GuardianTeamCommunicationDecision decision,
        GuardianTeamCommunicationMarkerObservation bind1,
        GuardianTeamCommunicationMarkerObservation bind2,
        long nowMilliseconds,
        string lastEvent)
    {
        var episode = state.Episode;
        var remembered = lastObservedEpisode;
        var partySlot = episode?.PartySlot ?? remembered?.PartySlot ?? 0;
        var local = episode?.LocalPlayer ?? remembered?.LocalPlayer ?? default;
        var target = episode?.Target ?? remembered?.Target ?? default;
        var cleanupRemaining = state.CleanupAtMilliseconds > nowMilliseconds
            ? state.CleanupAtMilliseconds - nowMilliseconds
            : 0;
        return new GuardianCommunicationDiagnostics(
            configuration.PaladinGuardianAnnounceAndMark,
            metadata.Verified && clientState.ClientLanguage == metadata.Language,
            configured &&
            context == SupportedPvPContext.CrystallineConflict &&
            ResolveSupportedPvPContext() == SupportedPvPContext.CrystallineConflict,
            textInputStateKnown,
            textInputActive,
            state.Phase,
            decision.Kind,
            decision.Reason,
            state.LastConsumedEpisodeToken,
            state.ActiveToken,
            partySlot,
            local.GameObjectId,
            local.EntityId,
            target.GameObjectId,
            target.EntityId,
            bind1.GameObjectId,
            bind1.MarkerTime,
            bind2.GameObjectId,
            bind2.MarkerTime,
            state.OwnsBind1,
            state.OwnsBind2,
            cleanupRemaining,
            Interlocked.Read(ref observedEpisodeCount),
            Interlocked.Read(ref commandDecisionCount),
            Interlocked.Read(ref quickChatInvocationCount),
            Interlocked.Read(ref markerSetInvocationCount),
            Interlocked.Read(ref markerClearInvocationCount),
            Interlocked.Read(ref deferredMarkerCount),
            Interlocked.Read(ref terminalFailureCount),
            lastEvent);
    }

    private GuardianTeamCommunicationCommandOutcome DispatchOnce(
        GuardianTeamCommunicationCommand command,
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        long nowMilliseconds,
        out string dispatchEvent)
    {
        dispatchEvent = "Terminal preflight failure";
        if (!command.IsValid ||
            state.Episode is not { } episode ||
            episode.Token != command.EpisodeToken ||
            context != SupportedPvPContext.CrystallineConflict ||
            ResolveSupportedPvPContext() != SupportedPvPContext.CrystallineConflict ||
            !TryGetTextInputState(out var textInputActive) ||
            textInputActive)
        {
            return GuardianTeamCommunicationCommandOutcome.TerminalFailure;
        }

        var exactLocal = ResolveExactLocal(localPlayer);
        if (!exactLocal.Exact || exactLocal.Actor != episode.LocalPlayer)
        {
            dispatchEvent = "Terminal local identity drift";
            return GuardianTeamCommunicationCommandOutcome.TerminalFailure;
        }

        var isClear = command.Kind is
            GuardianTeamCommunicationCommandKind.ClearBind2 or
            GuardianTeamCommunicationCommandKind.ClearBind1;
        if (!isClear &&
            (!IsCommunicationConfigured() ||
             localPlayer is null ||
             !localPlayer.ClassJob.IsValid ||
             localPlayer.ClassJob.RowId != EnemyCombatConstants.PaladinJobId))
        {
            dispatchEvent = "Terminal configuration or metadata drift";
            return GuardianTeamCommunicationCommandOutcome.TerminalFailure;
        }

        ReviewedPvpCommandDispatchResult result;
        switch (command.Kind)
        {
            case GuardianTeamCommunicationCommandKind.SendQuickChat:
                {
                    if (!MatchesExactPartyTarget(episode, command))
                    {
                        dispatchEvent = "Terminal Quick Chat target drift";
                        return GuardianTeamCommunicationCommandOutcome.TerminalFailure;
                    }

                    result = commands.TryQuickChatCoveringTarget(
                        clientState.ClientLanguage,
                        command.PartySlot);
                    break;
                }
            case GuardianTeamCommunicationCommandKind.SetBind2:
                {
                    ReadMarkerObservations(out var bind1, out var bind2);
                    if (command.MarkerIndex != GuardianTeamCommunicationRules.Bind2MarkerIndex ||
                        !MatchesExactPartyTarget(episode, command) ||
                        !bind1.IsExactlyEmpty(GuardianTeamCommunicationRules.Bind1MarkerIndex) ||
                        !bind2.IsExactlyEmpty(GuardianTeamCommunicationRules.Bind2MarkerIndex) ||
                        bind1.MarkerTime != state.Bind1MarkerTimeBeforeSet ||
                        bind2.MarkerTime != state.Bind2MarkerTimeBeforeSet)
                    {
                        dispatchEvent = "Terminal Bind2 pair precondition drift";
                        return GuardianTeamCommunicationCommandOutcome.TerminalFailure;
                    }

                    result = commands.TryMarkGuardianAlly(command.PartySlot, nowMilliseconds);
                    break;
                }
            case GuardianTeamCommunicationCommandKind.SetBind1:
                {
                    ReadMarkerObservations(out var bind1, out var bind2);
                    var exactTarget = ResolveExactPartyTarget(episode.PartySlot);
                    if (command.MarkerIndex != GuardianTeamCommunicationRules.Bind1MarkerIndex ||
                        command.Actor != episode.LocalPlayer ||
                        !exactTarget.Exact ||
                        exactTarget.Actor != episode.Target ||
                        !bind1.IsExactlyEmpty(GuardianTeamCommunicationRules.Bind1MarkerIndex) ||
                        bind1.MarkerTime != state.Bind1MarkerTimeBeforeSet ||
                        !state.OwnsBind2 ||
                        bind2.GameObjectId != episode.Target.GameObjectId ||
                        bind2.MarkerTime != state.Bind2OwnedMarkerTime)
                    {
                        dispatchEvent = "Terminal Bind1 pair precondition drift";
                        return GuardianTeamCommunicationCommandOutcome.TerminalFailure;
                    }

                    result = commands.TryMarkGuardianSelf(nowMilliseconds);
                    break;
                }
            case GuardianTeamCommunicationCommandKind.ClearBind2:
                {
                    ReadMarkerObservations(out _, out var bind2);
                    if (command.MarkerIndex != GuardianTeamCommunicationRules.Bind2MarkerIndex ||
                        command.Actor != episode.Target ||
                        !MatchesExactPartyTarget(episode, command) ||
                        !MarkerMatchesOwnedCommand(bind2, command))
                    {
                        dispatchEvent = "Terminal Bind2 ownership drift";
                        return GuardianTeamCommunicationCommandOutcome.TerminalFailure;
                    }

                    result = commands.TryClearGuardianAlly(nowMilliseconds);
                    break;
                }
            case GuardianTeamCommunicationCommandKind.ClearBind1:
                {
                    ReadMarkerObservations(out var bind1, out _);
                    if (command.MarkerIndex != GuardianTeamCommunicationRules.Bind1MarkerIndex ||
                        command.Actor != episode.LocalPlayer ||
                        !MarkerMatchesOwnedCommand(bind1, command))
                    {
                        dispatchEvent = "Terminal Bind1 ownership drift";
                        return GuardianTeamCommunicationCommandOutcome.TerminalFailure;
                    }

                    result = commands.TryClearGuardianSelf(nowMilliseconds);
                    break;
                }
            default:
                dispatchEvent = "Terminal invalid command kind";
                return GuardianTeamCommunicationCommandOutcome.TerminalFailure;
        }

        dispatchEvent = $"{command.Kind} {result}";
        return result switch
        {
            ReviewedPvpCommandDispatchResult.Invoked =>
                GuardianTeamCommunicationCommandOutcome.Invoked,
            ReviewedPvpCommandDispatchResult.MarkerRateLimited
                when command.Kind != GuardianTeamCommunicationCommandKind.SendQuickChat =>
                GuardianTeamCommunicationCommandOutcome.DeferredBeforeInvocation,
            _ => GuardianTeamCommunicationCommandOutcome.TerminalFailure,
        };
    }

    private void CountOutcome(
        GuardianTeamCommunicationCommandKind command,
        GuardianTeamCommunicationCommandOutcome outcome)
    {
        if (outcome == GuardianTeamCommunicationCommandOutcome.DeferredBeforeInvocation)
        {
            Interlocked.Increment(ref deferredMarkerCount);
            return;
        }

        if (outcome == GuardianTeamCommunicationCommandOutcome.TerminalFailure)
        {
            Interlocked.Increment(ref terminalFailureCount);
            return;
        }

        switch (command)
        {
            case GuardianTeamCommunicationCommandKind.SendQuickChat:
                Interlocked.Increment(ref quickChatInvocationCount);
                break;
            case GuardianTeamCommunicationCommandKind.SetBind2:
            case GuardianTeamCommunicationCommandKind.SetBind1:
                Interlocked.Increment(ref markerSetInvocationCount);
                break;
            case GuardianTeamCommunicationCommandKind.ClearBind2:
            case GuardianTeamCommunicationCommandKind.ClearBind1:
                Interlocked.Increment(ref markerClearInvocationCount);
                break;
        }
    }

    private bool MatchesExactPartyTarget(
        GuardianTeamCommunicationEpisode episode,
        GuardianTeamCommunicationCommand command)
    {
        var target = ResolveExactPartyTarget(command.PartySlot);
        return command.PartySlot == episode.PartySlot &&
               command.Actor == episode.Target &&
               target.Exact &&
               target.PartySlot == episode.PartySlot &&
               target.Actor == episode.Target;
    }

    private bool IsCommunicationConfigured() =>
        configuration.Enabled &&
        configuration.EnableDefensiveUtilities &&
        configuration.PaladinGuardianLowAlly &&
        configuration.PaladinGuardianAnnounceAndMark &&
        metadata.Verified &&
        clientState.ClientLanguage == metadata.Language;

    private SupportedPvPContext ResolveSupportedPvPContext()
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

    private static bool MarkerMatchesOwnedCommand(
        GuardianTeamCommunicationMarkerObservation marker,
        GuardianTeamCommunicationCommand command) =>
        marker.HasExactShape(command.MarkerIndex) &&
        marker.GameObjectId == command.Actor.GameObjectId &&
        marker.MarkerTime == command.ExpectedMarkerTime;

    private static GuardianTeamCommunicationResolvedActor ResolveExactLocal(
        IPlayerCharacter? player)
    {
        if (!TryGetExactIdentity(player, out var actor))
            return new GuardianTeamCommunicationResolvedActor(false, default);

        return new GuardianTeamCommunicationResolvedActor(true, actor);
    }

    private GuardianTeamCommunicationResolvedPartyMember ResolveExactPartyTarget(int partySlot)
    {
        var player = PartySlotResolver.Resolve(objectTable, partySlot);
        if (!TryGetExactIdentity(player, out var actor))
            return new GuardianTeamCommunicationResolvedPartyMember(false, partySlot, default);

        return new GuardianTeamCommunicationResolvedPartyMember(true, partySlot, actor);
    }

    private static unsafe bool TryGetExactIdentity(
        IPlayerCharacter? player,
        out TargetPressureActorIdentity actor)
    {
        actor = default;
        if (player is null ||
            player.Address == nint.Zero ||
            player.GameObjectId is 0 or 0xE0000000UL ||
            player.EntityId is 0 or 0xE0000000u)
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        if (native == null || native->EntityId != player.EntityId) return false;

        actor = new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);
        return actor.IsValid;
    }

    private static unsafe void ReadMarkerObservations(
        out GuardianTeamCommunicationMarkerObservation bind1,
        out GuardianTeamCommunicationMarkerObservation bind2)
    {
        bind1 = new GuardianTeamCommunicationMarkerObservation(
            GuardianTeamCommunicationRules.Bind1MarkerIndex,
            false,
            0,
            -1);
        bind2 = new GuardianTeamCommunicationMarkerObservation(
            GuardianTeamCommunicationRules.Bind2MarkerIndex,
            false,
            0,
            -1);

        try
        {
            var marking = MarkingController.Instance();
            if (marking == null ||
                marking->Markers.Length <= GuardianTeamCommunicationRules.Bind2MarkerIndex ||
                marking->MarkerTimes.Length <= GuardianTeamCommunicationRules.Bind2MarkerIndex)
            {
                return;
            }

            bind1 = new GuardianTeamCommunicationMarkerObservation(
                GuardianTeamCommunicationRules.Bind1MarkerIndex,
                true,
                (ulong)marking->Markers[GuardianTeamCommunicationRules.Bind1MarkerIndex],
                marking->MarkerTimes[GuardianTeamCommunicationRules.Bind1MarkerIndex]);
            bind2 = new GuardianTeamCommunicationMarkerObservation(
                GuardianTeamCommunicationRules.Bind2MarkerIndex,
                true,
                (ulong)marking->Markers[GuardianTeamCommunicationRules.Bind2MarkerIndex],
                marking->MarkerTimes[GuardianTeamCommunicationRules.Bind2MarkerIndex]);
        }
        catch
        {
            // The unavailable observations above force the pure rules to close.
        }
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
}
