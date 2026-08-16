namespace SeitonSense.Core;

public enum GuardianTeamCommunicationPhase : byte
{
    Idle = 0,
    AwaitingQuickChatResult = 1,
    ReadyToSetBind2 = 2,
    AwaitingBind2SetResult = 3,
    AwaitingBind2Confirmation = 4,
    ReadyToSetBind1 = 5,
    AwaitingBind1SetResult = 6,
    AwaitingBind1Confirmation = 7,
    ActivePair = 8,
    ReadyToClearBind2 = 9,
    AwaitingBind2ClearResult = 10,
    ReadyToClearBind1 = 11,
    AwaitingBind1ClearResult = 12,
}

public enum GuardianTeamCommunicationDecisionKind : byte
{
    Waiting = 0,
    IssueCommand = 1,
    Completed = 2,
    Cancelled = 3,
    Relinquished = 4,
}

public enum GuardianTeamCommunicationDecisionReason : byte
{
    None = 0,
    WaitingForAcceptedGuardian = 1,
    DuplicateEpisode = 2,
    BusyEpisodeConsumed = 3,
    InvalidState = 4,
    InvalidEpisode = 5,
    HardReset = 6,
    ConfigurationDisabled = 7,
    OutsideCrystallineConflict = 8,
    TextInputUnavailable = 9,
    TextInputActive = 10,
    LocalIdentityMismatch = 11,
    TargetIdentityMismatch = 12,
    QuickChatReady = 13,
    MarkerPairUnavailable = 14,
    AwaitingCommandResult = 15,
    CommandResultTimeout = 16,
    Bind2Ready = 17,
    Bind2Confirmed = 18,
    Bind1Ready = 19,
    Bind1Confirmed = 20,
    AwaitingMarkerConfirmation = 21,
    MarkerConfirmationTimeout = 22,
    PairActive = 23,
    CleanupDeadlineReached = 24,
    PartialPairFailure = 25,
    ClearBind2Ready = 26,
    ClearBind1Ready = 27,
    MarkerTelemetryUnavailable = 28,
    ExternalMarkerDrift = 29,
    CommandResultMismatch = 30,
    CommunicationComplete = 31,
}

public enum GuardianTeamCommunicationCommandKind : byte
{
    None = 0,
    SendQuickChat = 1,
    SetBind2 = 2,
    SetBind1 = 3,
    ClearBind2 = 4,
    ClearBind1 = 5,
}

public enum GuardianTeamCommunicationCommandOutcome : byte
{
    Invoked = 0,
    DeferredBeforeInvocation = 1,
    TerminalFailure = 2,
}

/// <summary>
/// A strong event value that callers may create only from one client-accepted
/// automatic Guardian request. The runtime integration is responsible for
/// proving that source before publishing the value.
/// </summary>
public readonly record struct GuardianTeamCommunicationEpisode(
    long Token,
    long AcceptedAtMilliseconds,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    int PartySlot)
{
    public bool IsValid =>
        Token > 0 &&
        AcceptedAtMilliseconds >= 0 &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        LocalPlayer != Target &&
        PartySlot is >= 1 and <= 8;
}

public readonly record struct GuardianTeamCommunicationResolvedActor(
    bool Exact,
    TargetPressureActorIdentity Actor);

public readonly record struct GuardianTeamCommunicationResolvedPartyMember(
    bool Exact,
    int PartySlot,
    TargetPressureActorIdentity Actor);

public readonly record struct GuardianTeamCommunicationMarkerObservation(
    int MarkerIndex,
    bool Available,
    ulong GameObjectId,
    long MarkerTime)
{
    public bool HasExactShape(int expectedIndex) =>
        Available &&
        MarkerIndex == expectedIndex &&
        MarkerTime >= 0;

    public bool IsExactlyEmpty(int expectedIndex) =>
        HasExactShape(expectedIndex) && GameObjectId == 0;
}

public readonly record struct GuardianTeamCommunicationMarkerOwnership(
    int MarkerIndex,
    TargetPressureActorIdentity Actor,
    long MarkerTime)
{
    public bool IsValid =>
        MarkerIndex is GuardianTeamCommunicationRules.Bind1MarkerIndex or
            GuardianTeamCommunicationRules.Bind2MarkerIndex &&
        Actor.IsValid &&
        MarkerTime >= 0;
}

public readonly record struct GuardianTeamCommunicationCommand(
    GuardianTeamCommunicationCommandKind Kind,
    long EpisodeToken,
    int PartySlot,
    TargetPressureActorIdentity Actor,
    int MarkerIndex,
    long ExpectedMarkerTime)
{
    public bool IsValid => Kind switch
    {
        GuardianTeamCommunicationCommandKind.SendQuickChat =>
            EpisodeToken > 0 &&
            PartySlot is >= 1 and <= 8 &&
            Actor.IsValid &&
            MarkerIndex == 0 &&
            ExpectedMarkerTime == 0,
        GuardianTeamCommunicationCommandKind.SetBind2 =>
            IsValidBind2Target && ExpectedMarkerTime == 0,
        GuardianTeamCommunicationCommandKind.SetBind1 =>
            EpisodeToken > 0 &&
            PartySlot == 0 &&
            Actor.IsValid &&
            MarkerIndex == GuardianTeamCommunicationRules.Bind1MarkerIndex &&
            ExpectedMarkerTime == 0,
        GuardianTeamCommunicationCommandKind.ClearBind2 =>
            IsValidBind2Target && ExpectedMarkerTime >= 0,
        GuardianTeamCommunicationCommandKind.ClearBind1 =>
            EpisodeToken > 0 &&
            PartySlot == 0 &&
            Actor.IsValid &&
            MarkerIndex == GuardianTeamCommunicationRules.Bind1MarkerIndex &&
            ExpectedMarkerTime >= 0,
        _ => false,
    };

    private bool IsValidBind2Target =>
        EpisodeToken > 0 &&
        PartySlot is >= 1 and <= 8 &&
        Actor.IsValid &&
        MarkerIndex == GuardianTeamCommunicationRules.Bind2MarkerIndex;
}

public readonly record struct GuardianTeamCommunicationState(
    long LastConsumedEpisodeToken,
    GuardianTeamCommunicationPhase Phase,
    GuardianTeamCommunicationEpisode? Episode,
    bool MarkerPairPlanned,
    long CleanupAtMilliseconds,
    long Bind1MarkerTimeBeforeSet,
    long Bind2MarkerTimeBeforeSet,
    GuardianTeamCommunicationMarkerOwnership? Bind1Ownership,
    GuardianTeamCommunicationMarkerOwnership? Bind2Ownership,
    GuardianTeamCommunicationCommand? PendingCommand,
    long PendingCommandExpiresAtMilliseconds)
{
    public static GuardianTeamCommunicationState Initial { get; } = new(
        0,
        GuardianTeamCommunicationPhase.Idle,
        null,
        false,
        -1,
        -1,
        -1,
        null,
        null,
        null,
        -1);

    public long ActiveToken => Episode?.Token ?? 0;
    public bool OwnsBind1 => Bind1Ownership is not null;
    public bool OwnsBind2 => Bind2Ownership is not null;
    public long Bind1OwnedMarkerTime => Bind1Ownership?.MarkerTime ?? -1;
    public long Bind2OwnedMarkerTime => Bind2Ownership?.MarkerTime ?? -1;
}

public readonly record struct GuardianTeamCommunicationObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    bool HardReset,
    bool TextInputStateKnown,
    bool TextInputActive,
    long NowMilliseconds,
    GuardianTeamCommunicationEpisode? AcceptedEpisode,
    GuardianTeamCommunicationResolvedActor LocalPlayer,
    GuardianTeamCommunicationResolvedPartyMember PartyTarget,
    GuardianTeamCommunicationMarkerObservation Bind1,
    GuardianTeamCommunicationMarkerObservation Bind2);

public readonly record struct GuardianTeamCommunicationDecision(
    GuardianTeamCommunicationState State,
    GuardianTeamCommunicationDecisionKind Kind,
    GuardianTeamCommunicationDecisionReason Reason,
    GuardianTeamCommunicationCommand? Command)
{
    public bool ShouldIssueCommand =>
        Kind == GuardianTeamCommunicationDecisionKind.IssueCommand &&
        Command is { IsValid: true };
}

public static class GuardianTeamCommunicationRules
{
    public const int Bind1MarkerIndex = 5;
    public const int Bind2MarkerIndex = 6;
    public const long ActiveLifetimeMilliseconds = 9_000;
    public const long CommandConfirmationTimeoutMilliseconds = 1_500;

    public static GuardianTeamCommunicationDecision Observe(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation)
    {
        if (!IsValidState(state))
        {
            return Result(
                ToIdle(Math.Max(0, state.LastConsumedEpisodeToken)),
                GuardianTeamCommunicationDecisionKind.Cancelled,
                GuardianTeamCommunicationDecisionReason.InvalidState);
        }

        var newerEpisodeConsumed = false;
        if (observation.AcceptedEpisode is { Token: > 0 } observedEpisode &&
            observedEpisode.Token > state.LastConsumedEpisodeToken)
        {
            if (state.Phase == GuardianTeamCommunicationPhase.Idle)
                return ObserveNewEpisode(state, observation, observedEpisode);

            state = state with { LastConsumedEpisodeToken = observedEpisode.Token };
            newerEpisodeConsumed = true;
        }

        if (state.Phase == GuardianTeamCommunicationPhase.Idle)
        {
            var duplicate = observation.AcceptedEpisode is { Token: > 0 };
            return Result(
                state,
                GuardianTeamCommunicationDecisionKind.Waiting,
                duplicate
                    ? GuardianTeamCommunicationDecisionReason.DuplicateEpisode
                    : GuardianTeamCommunicationDecisionReason.WaitingForAcceptedGuardian);
        }

        if (observation.NowMilliseconds < 0)
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Cancelled,
                GuardianTeamCommunicationDecisionReason.InvalidState);
        }

        if (observation.HardReset)
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Relinquished,
                GuardianTeamCommunicationDecisionReason.HardReset);
        }

        if (!observation.IsCrystallineConflict)
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Relinquished,
                GuardianTeamCommunicationDecisionReason.OutsideCrystallineConflict);
        }

        // A set command has already been invoked in these phases. Configuration
        // loss or text entry must not make its asynchronously appearing marker
        // unowned. Continue read-only confirmation until the original deadline;
        // once ownership is proven, cleanup waits until commands are safe again.
        if (state.Phase == GuardianTeamCommunicationPhase.AwaitingBind2Confirmation)
            return ObserveBind2Confirmation(state, observation);
        if (state.Phase == GuardianTeamCommunicationPhase.AwaitingBind1Confirmation)
            return ObserveBind1Confirmation(state, observation);

        if (!observation.ConfigurationEnabled)
            return CancelOrCleanOwned(state, observation, GuardianTeamCommunicationDecisionReason.ConfigurationDisabled);

        if (!observation.TextInputStateKnown)
            return CancelOrCleanOwned(state, observation, GuardianTeamCommunicationDecisionReason.TextInputUnavailable);
        if (observation.TextInputActive)
            return CancelOrCleanOwned(state, observation, GuardianTeamCommunicationDecisionReason.TextInputActive);

        if (newerEpisodeConsumed &&
            state.Phase is GuardianTeamCommunicationPhase.AwaitingQuickChatResult or
                GuardianTeamCommunicationPhase.AwaitingBind2SetResult or
                GuardianTeamCommunicationPhase.AwaitingBind2Confirmation or
                GuardianTeamCommunicationPhase.AwaitingBind1SetResult or
                GuardianTeamCommunicationPhase.AwaitingBind1Confirmation)
        {
            // The active frozen intent always wins. The newer token is consumed
            // above so it can never replay after this episode completes.
        }

        return ObserveActive(state, observation, newerEpisodeConsumed);
    }

    public static GuardianTeamCommunicationState ApplyCommandResult(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationCommand command,
        GuardianTeamCommunicationCommandOutcome outcome)
    {
        if (!IsValidState(state))
            return ToIdle(Math.Max(0, state.LastConsumedEpisodeToken));

        if (state.PendingCommand is not { } expected ||
            expected != command ||
            !command.IsValid ||
            !Enum.IsDefined(outcome))
        {
            return StartCleanupOrIdle(state);
        }

        if (outcome == GuardianTeamCommunicationCommandOutcome.DeferredBeforeInvocation &&
            command.Kind != GuardianTeamCommunicationCommandKind.SendQuickChat)
        {
            return state with
            {
                Phase = ReadyPhaseFor(command.Kind),
                PendingCommand = null,
                PendingCommandExpiresAtMilliseconds = -1,
            };
        }

        return command.Kind switch
        {
            GuardianTeamCommunicationCommandKind.SendQuickChat =>
                AdvanceAfterQuickChat(state),
            GuardianTeamCommunicationCommandKind.SetBind2 =>
                outcome == GuardianTeamCommunicationCommandOutcome.Invoked
                    ? state with
                    {
                        Phase = GuardianTeamCommunicationPhase.AwaitingBind2Confirmation,
                        PendingCommand = null,
                    }
                    : ToIdle(state.LastConsumedEpisodeToken),
            GuardianTeamCommunicationCommandKind.SetBind1 =>
                outcome == GuardianTeamCommunicationCommandOutcome.Invoked
                    ? state with
                    {
                        Phase = GuardianTeamCommunicationPhase.AwaitingBind1Confirmation,
                        PendingCommand = null,
                    }
                    : StartCleanupOrIdle(state with { PendingCommand = null }),
            GuardianTeamCommunicationCommandKind.ClearBind2 =>
                StartBind1CleanupOrIdle(state with
                {
                    Bind2Ownership = null,
                    PendingCommand = null,
                    PendingCommandExpiresAtMilliseconds = -1,
                }),
            GuardianTeamCommunicationCommandKind.ClearBind1 =>
                ToIdle(state.LastConsumedEpisodeToken),
            _ => StartCleanupOrIdle(state),
        };
    }

    private static GuardianTeamCommunicationDecision ObserveNewEpisode(
        GuardianTeamCommunicationState previous,
        GuardianTeamCommunicationObservation observation,
        GuardianTeamCommunicationEpisode episode)
    {
        var consumed = ToIdle(episode.Token);
        if (!episode.IsValid ||
            observation.NowMilliseconds < episode.AcceptedAtMilliseconds ||
            observation.NowMilliseconds >= SaturatingAdd(
                episode.AcceptedAtMilliseconds,
                ActiveLifetimeMilliseconds))
        {
            return Result(
                consumed,
                GuardianTeamCommunicationDecisionKind.Cancelled,
                GuardianTeamCommunicationDecisionReason.InvalidEpisode);
        }

        var gateFailure = GetInitialGateFailure(observation);
        if (gateFailure != GuardianTeamCommunicationDecisionReason.None)
        {
            return Result(consumed, GuardianTeamCommunicationDecisionKind.Cancelled, gateFailure);
        }

        if (!MatchesLocal(episode, observation.LocalPlayer))
        {
            return Result(
                consumed,
                GuardianTeamCommunicationDecisionKind.Cancelled,
                GuardianTeamCommunicationDecisionReason.LocalIdentityMismatch);
        }

        if (!MatchesTarget(episode, observation.PartyTarget))
        {
            return Result(
                consumed,
                GuardianTeamCommunicationDecisionKind.Cancelled,
                GuardianTeamCommunicationDecisionReason.TargetIdentityMismatch);
        }

        var pairPlanned = BothMarkersExactlyEmpty(observation);
        var active = new GuardianTeamCommunicationState(
            episode.Token,
            GuardianTeamCommunicationPhase.Idle,
            episode,
            pairPlanned,
            SaturatingAdd(episode.AcceptedAtMilliseconds, ActiveLifetimeMilliseconds),
            pairPlanned ? observation.Bind1.MarkerTime : -1,
            pairPlanned ? observation.Bind2.MarkerTime : -1,
            null,
            null,
            null,
            -1);
        var command = QuickChatCommand(episode);
        return Issue(
            active,
            GuardianTeamCommunicationPhase.AwaitingQuickChatResult,
            command,
            observation.NowMilliseconds,
            GuardianTeamCommunicationDecisionReason.QuickChatReady);
    }

    private static GuardianTeamCommunicationDecision ObserveActive(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation,
        bool newerEpisodeConsumed)
    {
        if (state.Phase is GuardianTeamCommunicationPhase.AwaitingQuickChatResult or
            GuardianTeamCommunicationPhase.AwaitingBind2SetResult or
            GuardianTeamCommunicationPhase.AwaitingBind1SetResult or
            GuardianTeamCommunicationPhase.AwaitingBind2ClearResult or
            GuardianTeamCommunicationPhase.AwaitingBind1ClearResult)
        {
            if (observation.NowMilliseconds < state.PendingCommandExpiresAtMilliseconds)
            {
                return Result(
                    state,
                    GuardianTeamCommunicationDecisionKind.Waiting,
                    newerEpisodeConsumed
                        ? GuardianTeamCommunicationDecisionReason.BusyEpisodeConsumed
                        : GuardianTeamCommunicationDecisionReason.AwaitingCommandResult);
            }

            state = ApplyCommandResult(
                state,
                state.PendingCommand!.Value,
                GuardianTeamCommunicationCommandOutcome.TerminalFailure);
            return Result(
                state,
                state.Phase == GuardianTeamCommunicationPhase.Idle
                    ? GuardianTeamCommunicationDecisionKind.Completed
                    : GuardianTeamCommunicationDecisionKind.Waiting,
                GuardianTeamCommunicationDecisionReason.CommandResultTimeout);
        }

        return state.Phase switch
        {
            GuardianTeamCommunicationPhase.ReadyToSetBind2 => ObserveReadyBind2(state, observation),
            GuardianTeamCommunicationPhase.ReadyToSetBind1 => ObserveReadyBind1(state, observation),
            GuardianTeamCommunicationPhase.ActivePair => ObserveActivePair(state, observation),
            GuardianTeamCommunicationPhase.ReadyToClearBind2 or
                GuardianTeamCommunicationPhase.ReadyToClearBind1 => ObserveCleanup(state, observation),
            _ => Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Cancelled,
                GuardianTeamCommunicationDecisionReason.InvalidState),
        };
    }

    private static GuardianTeamCommunicationDecision ObserveReadyBind2(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation)
    {
        var episode = state.Episode!.Value;
        if (observation.NowMilliseconds >= state.CleanupAtMilliseconds)
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Completed,
                GuardianTeamCommunicationDecisionReason.CleanupDeadlineReached);
        }

        if (!MatchesLocal(episode, observation.LocalPlayer))
            return CancelWithoutOwnership(state, GuardianTeamCommunicationDecisionReason.LocalIdentityMismatch);
        if (!MatchesTarget(episode, observation.PartyTarget))
            return CancelWithoutOwnership(state, GuardianTeamCommunicationDecisionReason.TargetIdentityMismatch);
        if (!MarkersStillExactlyEmpty(state, observation))
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Completed,
                GuardianTeamCommunicationDecisionReason.MarkerPairUnavailable);
        }

        return Issue(
            state,
            GuardianTeamCommunicationPhase.AwaitingBind2SetResult,
            SetBind2Command(episode),
            observation.NowMilliseconds,
            GuardianTeamCommunicationDecisionReason.Bind2Ready);
    }

    private static GuardianTeamCommunicationDecision ObserveBind2Confirmation(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation)
    {
        var episode = state.Episode!.Value;
        if (!MatchesLocal(episode, observation.LocalPlayer))
            return CancelWithoutOwnership(state, GuardianTeamCommunicationDecisionReason.LocalIdentityMismatch);
        if (!MatchesTarget(episode, observation.PartyTarget))
            return CancelWithoutOwnership(state, GuardianTeamCommunicationDecisionReason.TargetIdentityMismatch);
        if (!observation.Bind2.HasExactShape(Bind2MarkerIndex))
            return CancelWithoutOwnership(state, GuardianTeamCommunicationDecisionReason.MarkerTelemetryUnavailable);

        var bind1StillFree =
            observation.Bind1.IsExactlyEmpty(Bind1MarkerIndex) &&
            observation.Bind1.MarkerTime == state.Bind1MarkerTimeBeforeSet;
        if (!bind1StillFree) state = state with { MarkerPairPlanned = false };

        if (CanConfirmSet(
                observation.Bind2,
                Bind2MarkerIndex,
                episode.Target,
                state.Bind2MarkerTimeBeforeSet))
        {
            var owned = new GuardianTeamCommunicationMarkerOwnership(
                Bind2MarkerIndex,
                episode.Target,
                observation.Bind2.MarkerTime);
            state = state with
            {
                Bind2Ownership = owned,
                PendingCommandExpiresAtMilliseconds = -1,
            };

            if (!CanStartNewCommands(observation) ||
                !state.MarkerPairPlanned ||
                observation.NowMilliseconds >= state.CleanupAtMilliseconds)
            {
                return HoldOrObserveCleanup(
                    StartCleanupOrIdle(state),
                    observation,
                    !CanStartNewCommands(observation)
                        ? GetCommandGateFailure(observation)
                        : GuardianTeamCommunicationDecisionReason.PartialPairFailure);
            }

            return Result(
                state with { Phase = GuardianTeamCommunicationPhase.ReadyToSetBind1 },
                GuardianTeamCommunicationDecisionKind.Waiting,
                GuardianTeamCommunicationDecisionReason.Bind2Confirmed);
        }

        if (observation.Bind2.GameObjectId != 0)
            return CancelWithoutOwnership(state, GuardianTeamCommunicationDecisionReason.ExternalMarkerDrift);
        if (observation.NowMilliseconds >= state.PendingCommandExpiresAtMilliseconds)
            return CancelWithoutOwnership(state, GuardianTeamCommunicationDecisionReason.MarkerConfirmationTimeout);

        return Result(
            state,
            GuardianTeamCommunicationDecisionKind.Waiting,
            CanStartNewCommands(observation)
                ? GuardianTeamCommunicationDecisionReason.AwaitingMarkerConfirmation
                : GetCommandGateFailure(observation));
    }

    private static GuardianTeamCommunicationDecision ObserveReadyBind1(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation)
    {
        var episode = state.Episode!.Value;
        state = RefreshConfirmedOwnership(state, observation);
        if (!state.OwnsBind2)
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Relinquished,
                GuardianTeamCommunicationDecisionReason.ExternalMarkerDrift);
        }

        if (observation.NowMilliseconds >= state.CleanupAtMilliseconds)
        {
            return ObserveCleanup(
                StartCleanupOrIdle(state),
                observation,
                GuardianTeamCommunicationDecisionReason.CleanupDeadlineReached);
        }

        if (!MatchesLocal(episode, observation.LocalPlayer))
        {
            return ObserveCleanup(
                StartCleanupOrIdle(state),
                observation,
                GuardianTeamCommunicationDecisionReason.LocalIdentityMismatch);
        }

        if (!observation.Bind1.IsExactlyEmpty(Bind1MarkerIndex) ||
            observation.Bind1.MarkerTime != state.Bind1MarkerTimeBeforeSet)
        {
            return ObserveCleanup(
                StartCleanupOrIdle(state),
                observation,
                GuardianTeamCommunicationDecisionReason.PartialPairFailure);
        }

        return Issue(
            state,
            GuardianTeamCommunicationPhase.AwaitingBind1SetResult,
            SetBind1Command(episode),
            observation.NowMilliseconds,
            GuardianTeamCommunicationDecisionReason.Bind1Ready);
    }

    private static GuardianTeamCommunicationDecision ObserveBind1Confirmation(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation)
    {
        var episode = state.Episode!.Value;
        state = RefreshConfirmedOwnership(state, observation);
        if (!observation.Bind1.HasExactShape(Bind1MarkerIndex))
        {
            return HoldOrObserveCleanup(
                StartCleanupOrIdle(state),
                observation,
                GuardianTeamCommunicationDecisionReason.MarkerTelemetryUnavailable);
        }

        if (MatchesLocal(episode, observation.LocalPlayer) &&
            CanConfirmSet(
                observation.Bind1,
                Bind1MarkerIndex,
                episode.LocalPlayer,
                state.Bind1MarkerTimeBeforeSet))
        {
            state = state with
            {
                Bind1Ownership = new GuardianTeamCommunicationMarkerOwnership(
                    Bind1MarkerIndex,
                    episode.LocalPlayer,
                    observation.Bind1.MarkerTime),
                PendingCommandExpiresAtMilliseconds = -1,
            };

            if (CanStartNewCommands(observation) &&
                state.OwnsBind2 &&
                observation.NowMilliseconds < state.CleanupAtMilliseconds)
            {
                return Result(
                    state with { Phase = GuardianTeamCommunicationPhase.ActivePair },
                    GuardianTeamCommunicationDecisionKind.Waiting,
                    GuardianTeamCommunicationDecisionReason.Bind1Confirmed);
            }

            return HoldOrObserveCleanup(
                StartCleanupOrIdle(state),
                observation,
                !CanStartNewCommands(observation)
                    ? GetCommandGateFailure(observation)
                    : GuardianTeamCommunicationDecisionReason.PartialPairFailure);
        }

        if (observation.Bind1.GameObjectId != 0 ||
            observation.NowMilliseconds >= state.PendingCommandExpiresAtMilliseconds)
        {
            return HoldOrObserveCleanup(
                StartCleanupOrIdle(state),
                observation,
                observation.Bind1.GameObjectId != 0
                    ? GuardianTeamCommunicationDecisionReason.ExternalMarkerDrift
                    : GuardianTeamCommunicationDecisionReason.MarkerConfirmationTimeout);
        }

        return Result(
            state,
            GuardianTeamCommunicationDecisionKind.Waiting,
            CanStartNewCommands(observation)
                ? GuardianTeamCommunicationDecisionReason.AwaitingMarkerConfirmation
                : GetCommandGateFailure(observation));
    }

    private static GuardianTeamCommunicationDecision ObserveActivePair(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation)
    {
        state = RefreshConfirmedOwnership(state, observation);
        if (!state.OwnsBind1 || !state.OwnsBind2)
        {
            return ObserveCleanup(
                StartCleanupOrIdle(state),
                observation,
                GuardianTeamCommunicationDecisionReason.ExternalMarkerDrift);
        }

        if (observation.NowMilliseconds >= state.CleanupAtMilliseconds)
        {
            return ObserveCleanup(
                StartCleanupOrIdle(state),
                observation,
                GuardianTeamCommunicationDecisionReason.CleanupDeadlineReached);
        }

        return Result(
            state,
            GuardianTeamCommunicationDecisionKind.Waiting,
            GuardianTeamCommunicationDecisionReason.PairActive);
    }

    private static GuardianTeamCommunicationDecision ObserveCleanup(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation,
        GuardianTeamCommunicationDecisionReason reason = GuardianTeamCommunicationDecisionReason.None)
    {
        state = RefreshConfirmedOwnership(state, observation);
        if (!state.OwnsBind1 && !state.OwnsBind2)
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                reason == GuardianTeamCommunicationDecisionReason.ExternalMarkerDrift
                    ? GuardianTeamCommunicationDecisionKind.Relinquished
                    : GuardianTeamCommunicationDecisionKind.Completed,
                reason == GuardianTeamCommunicationDecisionReason.None
                    ? GuardianTeamCommunicationDecisionReason.CommunicationComplete
                    : reason);
        }

        if (!observation.IsCrystallineConflict)
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Relinquished,
                GuardianTeamCommunicationDecisionReason.OutsideCrystallineConflict);
        }

        if (!observation.TextInputStateKnown || observation.TextInputActive)
        {
            return Result(
                StartCleanupOrIdle(state),
                GuardianTeamCommunicationDecisionKind.Waiting,
                !observation.TextInputStateKnown
                    ? GuardianTeamCommunicationDecisionReason.TextInputUnavailable
                    : GuardianTeamCommunicationDecisionReason.TextInputActive);
        }

        if (state.OwnsBind2)
        {
            var owned = state.Bind2Ownership!.Value;
            var command = ClearBind2Command(state.Episode!.Value, owned.MarkerTime);
            return Issue(
                state,
                GuardianTeamCommunicationPhase.AwaitingBind2ClearResult,
                command,
                observation.NowMilliseconds,
                GuardianTeamCommunicationDecisionReason.ClearBind2Ready);
        }

        var bind1Owned = state.Bind1Ownership!.Value;
        return Issue(
            state,
            GuardianTeamCommunicationPhase.AwaitingBind1ClearResult,
            ClearBind1Command(state.Episode!.Value, bind1Owned.MarkerTime),
            observation.NowMilliseconds,
            GuardianTeamCommunicationDecisionReason.ClearBind1Ready);
    }

    private static GuardianTeamCommunicationDecision CancelOrCleanOwned(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation,
        GuardianTeamCommunicationDecisionReason reason)
    {
        state = RefreshConfirmedOwnership(state, observation);
        if (!state.OwnsBind1 && !state.OwnsBind2)
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Cancelled,
                reason);
        }

        if (!observation.IsCrystallineConflict)
        {
            return Result(
                ToIdle(state.LastConsumedEpisodeToken),
                GuardianTeamCommunicationDecisionKind.Relinquished,
                reason);
        }

        if (!observation.TextInputStateKnown || observation.TextInputActive)
        {
            return Result(
                StartCleanupOrIdle(state),
                GuardianTeamCommunicationDecisionKind.Waiting,
                reason);
        }

        return ObserveCleanup(StartCleanupOrIdle(state), observation, reason);
    }

    private static GuardianTeamCommunicationState RefreshConfirmedOwnership(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation)
    {
        if (state.Bind2Ownership is { } bind2 &&
            (!observation.Bind2.HasExactShape(Bind2MarkerIndex) ||
             !MatchesTarget(state.Episode!.Value, observation.PartyTarget) ||
             observation.Bind2.GameObjectId != bind2.Actor.GameObjectId ||
             observation.Bind2.MarkerTime != bind2.MarkerTime))
        {
            state = state with { Bind2Ownership = null };
        }

        if (state.Bind1Ownership is { } bind1 &&
            (!observation.Bind1.HasExactShape(Bind1MarkerIndex) ||
             !MatchesLocal(state.Episode!.Value, observation.LocalPlayer) ||
             observation.Bind1.GameObjectId != bind1.Actor.GameObjectId ||
             observation.Bind1.MarkerTime != bind1.MarkerTime))
        {
            state = state with { Bind1Ownership = null };
        }

        return state;
    }

    private static GuardianTeamCommunicationDecision Issue(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationPhase pendingPhase,
        GuardianTeamCommunicationCommand command,
        long nowMilliseconds,
        GuardianTeamCommunicationDecisionReason reason)
    {
        if (!command.IsValid)
        {
            return Result(
                StartCleanupOrIdle(state),
                GuardianTeamCommunicationDecisionKind.Cancelled,
                GuardianTeamCommunicationDecisionReason.InvalidState);
        }

        var pending = state with
        {
            Phase = pendingPhase,
            PendingCommand = command,
            PendingCommandExpiresAtMilliseconds = SaturatingAdd(
                nowMilliseconds,
                CommandConfirmationTimeoutMilliseconds),
        };
        return new GuardianTeamCommunicationDecision(
            pending,
            GuardianTeamCommunicationDecisionKind.IssueCommand,
            reason,
            command);
    }

    private static GuardianTeamCommunicationDecision CancelWithoutOwnership(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationDecisionReason reason) =>
        Result(
            ToIdle(state.LastConsumedEpisodeToken),
            GuardianTeamCommunicationDecisionKind.Cancelled,
            reason);

    private static GuardianTeamCommunicationDecision Result(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationDecisionKind kind,
        GuardianTeamCommunicationDecisionReason reason) =>
        new(state, kind, reason, null);

    private static GuardianTeamCommunicationState AdvanceAfterQuickChat(
        GuardianTeamCommunicationState state) =>
        state.MarkerPairPlanned
            ? state with
            {
                Phase = GuardianTeamCommunicationPhase.ReadyToSetBind2,
                PendingCommand = null,
                PendingCommandExpiresAtMilliseconds = -1,
            }
            : ToIdle(state.LastConsumedEpisodeToken);

    private static GuardianTeamCommunicationState StartCleanupOrIdle(
        GuardianTeamCommunicationState state) =>
        state.Bind2Ownership is not null
            ? state with
            {
                Phase = GuardianTeamCommunicationPhase.ReadyToClearBind2,
                MarkerPairPlanned = false,
                PendingCommand = null,
                PendingCommandExpiresAtMilliseconds = -1,
            }
            : StartBind1CleanupOrIdle(state);

    private static GuardianTeamCommunicationState StartBind1CleanupOrIdle(
        GuardianTeamCommunicationState state) =>
        state.Bind1Ownership is not null
            ? state with
            {
                Phase = GuardianTeamCommunicationPhase.ReadyToClearBind1,
                MarkerPairPlanned = false,
                PendingCommand = null,
                PendingCommandExpiresAtMilliseconds = -1,
            }
            : ToIdle(state.LastConsumedEpisodeToken);

    private static GuardianTeamCommunicationState ToIdle(long lastConsumedEpisodeToken) =>
        GuardianTeamCommunicationState.Initial with
        {
            LastConsumedEpisodeToken = Math.Max(0, lastConsumedEpisodeToken),
        };

    private static GuardianTeamCommunicationPhase ReadyPhaseFor(
        GuardianTeamCommunicationCommandKind commandKind) => commandKind switch
        {
            GuardianTeamCommunicationCommandKind.SetBind2 =>
                GuardianTeamCommunicationPhase.ReadyToSetBind2,
            GuardianTeamCommunicationCommandKind.SetBind1 =>
                GuardianTeamCommunicationPhase.ReadyToSetBind1,
            GuardianTeamCommunicationCommandKind.ClearBind2 =>
                GuardianTeamCommunicationPhase.ReadyToClearBind2,
            GuardianTeamCommunicationCommandKind.ClearBind1 =>
                GuardianTeamCommunicationPhase.ReadyToClearBind1,
            _ => GuardianTeamCommunicationPhase.Idle,
        };

    private static GuardianTeamCommunicationDecisionReason GetInitialGateFailure(
        GuardianTeamCommunicationObservation observation)
    {
        if (observation.HardReset)
            return GuardianTeamCommunicationDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return GuardianTeamCommunicationDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return GuardianTeamCommunicationDecisionReason.OutsideCrystallineConflict;
        if (!observation.TextInputStateKnown)
            return GuardianTeamCommunicationDecisionReason.TextInputUnavailable;
        if (observation.TextInputActive)
            return GuardianTeamCommunicationDecisionReason.TextInputActive;
        return GuardianTeamCommunicationDecisionReason.None;
    }

    private static bool BothMarkersExactlyEmpty(
        GuardianTeamCommunicationObservation observation) =>
        observation.Bind1.IsExactlyEmpty(Bind1MarkerIndex) &&
        observation.Bind2.IsExactlyEmpty(Bind2MarkerIndex);

    private static bool MarkersStillExactlyEmpty(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation) =>
        BothMarkersExactlyEmpty(observation) &&
        observation.Bind1.MarkerTime == state.Bind1MarkerTimeBeforeSet &&
        observation.Bind2.MarkerTime == state.Bind2MarkerTimeBeforeSet;

    private static bool HasExactMarkerTelemetry(
        GuardianTeamCommunicationObservation observation) =>
        observation.Bind1.HasExactShape(Bind1MarkerIndex) &&
        observation.Bind2.HasExactShape(Bind2MarkerIndex);

    private static bool CanStartNewCommands(
        GuardianTeamCommunicationObservation observation) =>
        observation.ConfigurationEnabled &&
        observation.TextInputStateKnown &&
        !observation.TextInputActive;

    private static GuardianTeamCommunicationDecisionReason GetCommandGateFailure(
        GuardianTeamCommunicationObservation observation) =>
        !observation.ConfigurationEnabled
            ? GuardianTeamCommunicationDecisionReason.ConfigurationDisabled
            : !observation.TextInputStateKnown
                ? GuardianTeamCommunicationDecisionReason.TextInputUnavailable
                : observation.TextInputActive
                    ? GuardianTeamCommunicationDecisionReason.TextInputActive
                    : GuardianTeamCommunicationDecisionReason.None;

    private static GuardianTeamCommunicationDecision HoldOrObserveCleanup(
        GuardianTeamCommunicationState state,
        GuardianTeamCommunicationObservation observation,
        GuardianTeamCommunicationDecisionReason reason)
    {
        if (state.Phase == GuardianTeamCommunicationPhase.Idle)
        {
            return Result(
                state,
                GuardianTeamCommunicationDecisionKind.Completed,
                reason);
        }

        return !observation.TextInputStateKnown || observation.TextInputActive
            ? Result(state, GuardianTeamCommunicationDecisionKind.Waiting, reason)
            : ObserveCleanup(state, observation, reason);
    }

    private static bool CanConfirmSet(
        GuardianTeamCommunicationMarkerObservation marker,
        int expectedIndex,
        TargetPressureActorIdentity expectedActor,
        long markerTimeBeforeCommand) =>
        expectedActor.IsValid &&
        marker.HasExactShape(expectedIndex) &&
        marker.GameObjectId == expectedActor.GameObjectId &&
        marker.MarkerTime != markerTimeBeforeCommand;

    private static bool MatchesLocal(
        GuardianTeamCommunicationEpisode episode,
        GuardianTeamCommunicationResolvedActor localPlayer) =>
        localPlayer.Exact &&
        localPlayer.Actor.IsValid &&
        localPlayer.Actor == episode.LocalPlayer;

    private static bool MatchesTarget(
        GuardianTeamCommunicationEpisode episode,
        GuardianTeamCommunicationResolvedPartyMember partyTarget) =>
        partyTarget.Exact &&
        partyTarget.PartySlot == episode.PartySlot &&
        partyTarget.Actor.IsValid &&
        partyTarget.Actor == episode.Target;

    private static bool IsValidState(GuardianTeamCommunicationState state)
    {
        if (state.LastConsumedEpisodeToken < 0 || !Enum.IsDefined(state.Phase)) return false;
        if (state.Phase == GuardianTeamCommunicationPhase.Idle)
        {
            return state.Episode is null &&
                   state.Bind1Ownership is null &&
                   state.Bind2Ownership is null &&
                   state.PendingCommand is null;
        }

        if (state.Episode is not { IsValid: true } episode ||
            state.LastConsumedEpisodeToken < episode.Token ||
            state.CleanupAtMilliseconds != SaturatingAdd(
                episode.AcceptedAtMilliseconds,
                ActiveLifetimeMilliseconds) ||
            state.Bind1Ownership is { IsValid: false } ||
            state.Bind2Ownership is { IsValid: false })
        {
            return false;
        }

        if (state.Bind1Ownership is { } bind1 &&
            (bind1.MarkerIndex != Bind1MarkerIndex || bind1.Actor != episode.LocalPlayer))
        {
            return false;
        }

        if (state.Bind2Ownership is { } bind2 &&
            (bind2.MarkerIndex != Bind2MarkerIndex || bind2.Actor != episode.Target))
        {
            return false;
        }

        var awaitingResult = state.Phase is
            GuardianTeamCommunicationPhase.AwaitingQuickChatResult or
            GuardianTeamCommunicationPhase.AwaitingBind2SetResult or
            GuardianTeamCommunicationPhase.AwaitingBind1SetResult or
            GuardianTeamCommunicationPhase.AwaitingBind2ClearResult or
            GuardianTeamCommunicationPhase.AwaitingBind1ClearResult;
        if (awaitingResult)
        {
            if (state.PendingCommand is not { IsValid: true } pending ||
                pending.EpisodeToken != episode.Token ||
                state.PendingCommandExpiresAtMilliseconds < 0 ||
                !PendingKindMatchesPhase(state.Phase, pending.Kind))
            {
                return false;
            }
        }
        else if (state.PendingCommand is not null)
        {
            return false;
        }

        var confirmationDeadlineValid = state.PendingCommandExpiresAtMilliseconds >= 0;
        return state.Phase switch
        {
            GuardianTeamCommunicationPhase.AwaitingQuickChatResult =>
                !state.OwnsBind1 && !state.OwnsBind2,
            GuardianTeamCommunicationPhase.ReadyToSetBind2 or
                GuardianTeamCommunicationPhase.AwaitingBind2SetResult =>
                state.MarkerPairPlanned &&
                state.Bind1MarkerTimeBeforeSet >= 0 &&
                state.Bind2MarkerTimeBeforeSet >= 0 &&
                !state.OwnsBind1 &&
                !state.OwnsBind2,
            GuardianTeamCommunicationPhase.AwaitingBind2Confirmation =>
                confirmationDeadlineValid && !state.OwnsBind1 && !state.OwnsBind2,
            GuardianTeamCommunicationPhase.ReadyToSetBind1 or
                GuardianTeamCommunicationPhase.AwaitingBind1SetResult =>
                state.MarkerPairPlanned && state.OwnsBind2 && !state.OwnsBind1,
            GuardianTeamCommunicationPhase.AwaitingBind1Confirmation =>
                confirmationDeadlineValid && state.OwnsBind2 && !state.OwnsBind1,
            GuardianTeamCommunicationPhase.ActivePair =>
                state.OwnsBind1 && state.OwnsBind2,
            GuardianTeamCommunicationPhase.ReadyToClearBind2 or
                GuardianTeamCommunicationPhase.AwaitingBind2ClearResult =>
                state.OwnsBind2,
            GuardianTeamCommunicationPhase.ReadyToClearBind1 or
                GuardianTeamCommunicationPhase.AwaitingBind1ClearResult =>
                !state.OwnsBind2 && state.OwnsBind1,
            _ => false,
        };
    }

    private static bool PendingKindMatchesPhase(
        GuardianTeamCommunicationPhase phase,
        GuardianTeamCommunicationCommandKind command) => phase switch
        {
            GuardianTeamCommunicationPhase.AwaitingQuickChatResult =>
                command == GuardianTeamCommunicationCommandKind.SendQuickChat,
            GuardianTeamCommunicationPhase.AwaitingBind2SetResult =>
                command == GuardianTeamCommunicationCommandKind.SetBind2,
            GuardianTeamCommunicationPhase.AwaitingBind1SetResult =>
                command == GuardianTeamCommunicationCommandKind.SetBind1,
            GuardianTeamCommunicationPhase.AwaitingBind2ClearResult =>
                command == GuardianTeamCommunicationCommandKind.ClearBind2,
            GuardianTeamCommunicationPhase.AwaitingBind1ClearResult =>
                command == GuardianTeamCommunicationCommandKind.ClearBind1,
            _ => false,
        };

    private static GuardianTeamCommunicationCommand QuickChatCommand(
        GuardianTeamCommunicationEpisode episode) =>
        new(
            GuardianTeamCommunicationCommandKind.SendQuickChat,
            episode.Token,
            episode.PartySlot,
            episode.Target,
            0,
            0);

    private static GuardianTeamCommunicationCommand SetBind2Command(
        GuardianTeamCommunicationEpisode episode) =>
        new(
            GuardianTeamCommunicationCommandKind.SetBind2,
            episode.Token,
            episode.PartySlot,
            episode.Target,
            Bind2MarkerIndex,
            0);

    private static GuardianTeamCommunicationCommand SetBind1Command(
        GuardianTeamCommunicationEpisode episode) =>
        new(
            GuardianTeamCommunicationCommandKind.SetBind1,
            episode.Token,
            0,
            episode.LocalPlayer,
            Bind1MarkerIndex,
            0);

    private static GuardianTeamCommunicationCommand ClearBind2Command(
        GuardianTeamCommunicationEpisode episode,
        long expectedMarkerTime) =>
        new(
            GuardianTeamCommunicationCommandKind.ClearBind2,
            episode.Token,
            episode.PartySlot,
            episode.Target,
            Bind2MarkerIndex,
            expectedMarkerTime);

    private static GuardianTeamCommunicationCommand ClearBind1Command(
        GuardianTeamCommunicationEpisode episode,
        long expectedMarkerTime) =>
        new(
            GuardianTeamCommunicationCommandKind.ClearBind1,
            episode.Token,
            0,
            episode.LocalPlayer,
            Bind1MarkerIndex,
            expectedMarkerTime);

    private static long SaturatingAdd(long value, long delta) =>
        value > long.MaxValue - delta ? long.MaxValue : value + delta;
}
