using SeitonSense.Core;

internal static class GuardianTeamCommunicationSelfTests
{
    private static readonly TargetPressureActorIdentity Local = new(0x100, 0x200);
    private static readonly TargetPressureActorIdentity Ally = new(0x300, 0x400);
    private static readonly TargetPressureActorIdentity Other = new(0x500, 0x600);

    internal static void AcceptedEpisodeQuickChatIsOneShot()
    {
        var episode = Episode();
        var first = GuardianTeamCommunicationRules.Observe(
            GuardianTeamCommunicationState.Initial,
            Observation(episode, bind1GameObjectId: Other.GameObjectId));

        Command(GuardianTeamCommunicationCommandKind.SendQuickChat, first, "accepted episode");
        Equal(episode.Token, first.State.LastConsumedEpisodeToken, "token consumed before command");
        Equal(episode.PartySlot, first.Command!.Value.PartySlot, "quick chat exact P-slot");
        Equal(episode.Target, first.Command.Value.Actor, "quick chat frozen target");

        var complete = Apply(first, GuardianTeamCommunicationCommandOutcome.Invoked);
        Equal(GuardianTeamCommunicationPhase.Idle, complete.Phase, "occupied marker means quickchat-only");

        var duplicate = GuardianTeamCommunicationRules.Observe(
            complete,
            Observation(episode, bind1GameObjectId: Other.GameObjectId, now: 1_001));
        False(duplicate.ShouldIssueCommand, "same episode cannot replay quick chat");
        Equal(GuardianTeamCommunicationDecisionReason.DuplicateEpisode, duplicate.Reason, "duplicate reason");
    }

    internal static void InitialFailuresConsumeWithoutCommands()
    {
        var observations = new[]
        {
            Observation(Episode(token: 1), configurationEnabled: false),
            Observation(Episode(token: 2), crystallineConflict: false),
            Observation(Episode(token: 3), hardReset: true),
            Observation(Episode(token: 4), textInputKnown: false),
            Observation(Episode(token: 5), textInputActive: true),
            Observation(Episode(token: 6), exactLocal: false),
            Observation(Episode(token: 7), exactTarget: false),
            Observation(Episode(token: 8) with { AcceptedAtMilliseconds = 1_001 }, now: 1_000),
            Observation(Episode(token: 9) with { PartySlot = 0 }),
        };

        foreach (var observation in observations)
        {
            var decision = GuardianTeamCommunicationRules.Observe(
                GuardianTeamCommunicationState.Initial,
                observation);
            False(decision.ShouldIssueCommand, $"gate {observation.AcceptedEpisode!.Value.Token}");
            Equal(
                observation.AcceptedEpisode.Value.Token,
                decision.State.LastConsumedEpisodeToken,
                "failed episode remains spent");
            Equal(GuardianTeamCommunicationPhase.Idle, decision.State.Phase, "failure is terminal");
        }
    }

    internal static void OccupiedOrUnknownMarkersStayQuickChatOnly()
    {
        var occupied = GuardianTeamCommunicationRules.Observe(
            GuardianTeamCommunicationState.Initial,
            Observation(Episode(token: 1), bind2GameObjectId: Other.GameObjectId));
        Command(GuardianTeamCommunicationCommandKind.SendQuickChat, occupied, "occupied pair still chats");
        False(Apply(occupied, GuardianTeamCommunicationCommandOutcome.Invoked).MarkerPairPlanned,
            "occupied pair is never planned");

        var unknown = GuardianTeamCommunicationRules.Observe(
            GuardianTeamCommunicationState.Initial,
            Observation(Episode(token: 2), bind2Available: false));
        Command(GuardianTeamCommunicationCommandKind.SendQuickChat, unknown, "unknown pair still chats");
        var complete = Apply(unknown, GuardianTeamCommunicationCommandOutcome.TerminalFailure);
        Equal(GuardianTeamCommunicationPhase.Idle, complete.Phase, "chat failure does not retry or mark unknown pair");
    }

    internal static void MarkerPairIsSequentialAndExactlyConfirmed()
    {
        var episode = Episode();
        var observation = Observation(episode);
        var quickChat = GuardianTeamCommunicationRules.Observe(
            GuardianTeamCommunicationState.Initial,
            observation);
        var state = Apply(quickChat, GuardianTeamCommunicationCommandOutcome.Invoked);

        var bind2 = GuardianTeamCommunicationRules.Observe(state, observation with { NowMilliseconds = 1_001 });
        Command(GuardianTeamCommunicationCommandKind.SetBind2, bind2, "Bind2 first");
        Equal(GuardianTeamCommunicationRules.Bind2MarkerIndex, bind2.Command!.Value.MarkerIndex, "Bind2 index");
        Equal(episode.Target, bind2.Command.Value.Actor, "Bind2 exact ally");
        state = Apply(bind2, GuardianTeamCommunicationCommandOutcome.Invoked);

        var waiting = GuardianTeamCommunicationRules.Observe(
            state,
            observation with { NowMilliseconds = 1_002 });
        False(waiting.ShouldIssueCommand, "empty unchanged Bind2 waits");
        Equal(GuardianTeamCommunicationPhase.AwaitingBind2Confirmation, waiting.State.Phase, "Bind2 pending");

        var confirmedBind2 = GuardianTeamCommunicationRules.Observe(
            waiting.State,
            observation with
            {
                NowMilliseconds = 1_003,
                Bind2 = Marker(GuardianTeamCommunicationRules.Bind2MarkerIndex, episode.Target.GameObjectId, 21),
            });
        False(confirmedBind2.ShouldIssueCommand, "confirmation and next set stay on separate ticks");
        True(confirmedBind2.State.OwnsBind2, "Bind2 ownership confirmed");
        Equal(21L, confirmedBind2.State.Bind2OwnedMarkerTime, "Bind2 owned timestamp");

        var bind1 = GuardianTeamCommunicationRules.Observe(
            confirmedBind2.State,
            observation with
            {
                NowMilliseconds = 1_004,
                Bind2 = Marker(GuardianTeamCommunicationRules.Bind2MarkerIndex, episode.Target.GameObjectId, 21),
            });
        Command(GuardianTeamCommunicationCommandKind.SetBind1, bind1, "Bind1 follows confirmed Bind2");
        Equal(GuardianTeamCommunicationRules.Bind1MarkerIndex, bind1.Command!.Value.MarkerIndex, "Bind1 index");
        Equal(episode.LocalPlayer, bind1.Command.Value.Actor, "Bind1 exact self");
        state = Apply(bind1, GuardianTeamCommunicationCommandOutcome.Invoked);

        var active = GuardianTeamCommunicationRules.Observe(
            state,
            observation with
            {
                NowMilliseconds = 1_005,
                Bind1 = Marker(GuardianTeamCommunicationRules.Bind1MarkerIndex, episode.LocalPlayer.GameObjectId, 11),
                Bind2 = Marker(GuardianTeamCommunicationRules.Bind2MarkerIndex, episode.Target.GameObjectId, 21),
            });
        Equal(GuardianTeamCommunicationPhase.ActivePair, active.State.Phase, "pair becomes active");
        True(active.State.OwnsBind1 && active.State.OwnsBind2, "both ownerships independent");
        Equal(10_000L, active.State.CleanupAtMilliseconds, "cleanup is based on original acceptance");
    }

    internal static void SetConfirmationRequiresActorAndChangedTime()
    {
        var episode = Episode();
        var pending = ReachAwaitingBind2Confirmation(episode);

        var unchangedTime = GuardianTeamCommunicationRules.Observe(
            pending,
            Observation(
                episode,
                now: 1_002,
                bind2GameObjectId: episode.Target.GameObjectId,
                bind2Time: 20));
        False(unchangedTime.ShouldIssueCommand, "unchanged timestamp never confirms");
        Equal(GuardianTeamCommunicationPhase.Idle, unchangedTime.State.Phase, "ambiguous marker is terminal");

        pending = ReachAwaitingBind2Confirmation(episode with { Token = 2 });
        var wrongActor = GuardianTeamCommunicationRules.Observe(
            pending,
            Observation(
                episode with { Token = 2 },
                now: 1_002,
                bind2GameObjectId: Other.GameObjectId,
                bind2Time: 21));
        Equal(GuardianTeamCommunicationPhase.Idle, wrongActor.State.Phase, "wrong actor is never owned");
        False(wrongActor.State.OwnsBind2, "wrong actor cannot be cleared");

        pending = ReachAwaitingBind2Confirmation(episode with { Token = 3 });
        var timeout = GuardianTeamCommunicationRules.Observe(
            pending,
            Observation(
                episode with { Token = 3 },
                now: pending.PendingCommandExpiresAtMilliseconds));
        Equal(GuardianTeamCommunicationPhase.Idle, timeout.State.Phase, "confirmation timeout never retries");
    }

    internal static void PartialBind1FailureCleansOnlyOwnedBind2()
    {
        var episode = Episode();
        var (pendingBind1, observation) = ReachAwaitingBind1Confirmation(episode);
        var failed = GuardianTeamCommunicationRules.ApplyCommandResult(
            pendingBind1 with
            {
                Phase = GuardianTeamCommunicationPhase.AwaitingBind1SetResult,
                PendingCommand = SetBind1Command(episode),
                PendingCommandExpiresAtMilliseconds = 2_500,
            },
            SetBind1Command(episode),
            GuardianTeamCommunicationCommandOutcome.TerminalFailure);
        Equal(GuardianTeamCommunicationPhase.ReadyToClearBind2, failed.Phase, "partial pair enters cleanup");
        True(failed.OwnsBind2, "confirmed Bind2 remains separately owned");
        False(failed.OwnsBind1, "failed Bind1 is not invented as owned");

        var clear = GuardianTeamCommunicationRules.Observe(failed, observation with { NowMilliseconds = 1_006 });
        Command(GuardianTeamCommunicationCommandKind.ClearBind2, clear, "partial cleanup only Bind2");
        Equal(21L, clear.Command!.Value.ExpectedMarkerTime, "cleanup freezes exact owned time");
        var complete = Apply(clear, GuardianTeamCommunicationCommandOutcome.TerminalFailure);
        Equal(GuardianTeamCommunicationPhase.Idle, complete.Phase, "failed clear is terminal without retry");
    }

    internal static void DeadlineCleanupIsBind2ThenBind1()
    {
        var episode = Episode();
        var (active, observation) = ReachActivePair(episode);
        var clearBind2 = GuardianTeamCommunicationRules.Observe(
            active,
            observation with { NowMilliseconds = 10_000 });
        Command(GuardianTeamCommunicationCommandKind.ClearBind2, clearBind2, "deadline clears Bind2 first");
        Equal(21L, clearBind2.Command!.Value.ExpectedMarkerTime, "Bind2 unchanged time required");

        var afterBind2 = Apply(clearBind2, GuardianTeamCommunicationCommandOutcome.Invoked);
        Equal(GuardianTeamCommunicationPhase.ReadyToClearBind1, afterBind2.Phase, "Bind1 cleanup follows");
        var clearBind1 = GuardianTeamCommunicationRules.Observe(
            afterBind2,
            observation with { NowMilliseconds = 10_001 });
        Command(GuardianTeamCommunicationCommandKind.ClearBind1, clearBind1, "deadline clears Bind1 second");
        Equal(11L, clearBind1.Command!.Value.ExpectedMarkerTime, "Bind1 unchanged time required");

        var complete = Apply(clearBind1, GuardianTeamCommunicationCommandOutcome.Invoked);
        Equal(GuardianTeamCommunicationPhase.Idle, complete.Phase, "cleanup completes");
        Equal(episode.Token, complete.LastConsumedEpisodeToken, "episode stays consumed");
    }

    internal static void ExternalDriftCleansOnlyRemainingOwnership()
    {
        var episode = Episode();
        var (active, observation) = ReachActivePair(episode);

        var bind2Drift = GuardianTeamCommunicationRules.Observe(
            active,
            observation with
            {
                NowMilliseconds = 2_000,
                Bind2 = Marker(GuardianTeamCommunicationRules.Bind2MarkerIndex, Other.GameObjectId, 22),
            });
        Command(GuardianTeamCommunicationCommandKind.ClearBind1, bind2Drift, "drifted Bind2 is relinquished");
        False(bind2Drift.State.OwnsBind2, "drifted Bind2 ownership removed");
        Equal(11L, bind2Drift.Command!.Value.ExpectedMarkerTime, "only unchanged Bind1 is cleared");

        (active, observation) = ReachActivePair(episode with { Token = 2 });
        var bind1Drift = GuardianTeamCommunicationRules.Observe(
            active,
            observation with
            {
                NowMilliseconds = 2_000,
                Bind1 = Marker(GuardianTeamCommunicationRules.Bind1MarkerIndex, Other.GameObjectId, 12),
            });
        Command(GuardianTeamCommunicationCommandKind.ClearBind2, bind1Drift, "drifted Bind1 is relinquished");
        False(bind1Drift.State.OwnsBind1, "drifted Bind1 ownership removed");

        (active, observation) = ReachActivePair(episode with { Token = 3 });
        var bothDrift = GuardianTeamCommunicationRules.Observe(
            active,
            observation with
            {
                NowMilliseconds = 2_000,
                Bind1 = Marker(GuardianTeamCommunicationRules.Bind1MarkerIndex, Other.GameObjectId, 12),
                Bind2 = Marker(GuardianTeamCommunicationRules.Bind2MarkerIndex, Other.GameObjectId, 22),
            });
        False(bothDrift.ShouldIssueCommand, "no ownership means no clear");
        Equal(GuardianTeamCommunicationPhase.Idle, bothDrift.State.Phase, "both drifted markers relinquished");

        (active, observation) = ReachActivePair(episode with { Token = 4 });
        var bind2Unknown = GuardianTeamCommunicationRules.Observe(
            active,
            observation with
            {
                NowMilliseconds = 2_000,
                Bind2 = new GuardianTeamCommunicationMarkerObservation(
                    GuardianTeamCommunicationRules.Bind2MarkerIndex,
                    false,
                    0,
                    0),
            });
        Command(GuardianTeamCommunicationCommandKind.ClearBind1, bind2Unknown,
            "unknown Bind2 cannot hide independently proven Bind1 cleanup");

        (active, observation) = ReachActivePair(episode with { Token = 5 });
        var bind1Unknown = GuardianTeamCommunicationRules.Observe(
            active,
            observation with
            {
                NowMilliseconds = 2_000,
                Bind1 = new GuardianTeamCommunicationMarkerObservation(
                    GuardianTeamCommunicationRules.Bind1MarkerIndex,
                    false,
                    0,
                    0),
            });
        Command(GuardianTeamCommunicationCommandKind.ClearBind2, bind1Unknown,
            "unknown Bind1 cannot hide independently proven Bind2 cleanup");
    }

    internal static void ResetAndContextLossOnlyUseSafeCleanup()
    {
        var episode = Episode();
        var (active, observation) = ReachActivePair(episode);
        var disabled = GuardianTeamCommunicationRules.Observe(
            active,
            observation with { ConfigurationEnabled = false, NowMilliseconds = 2_000 });
        Command(GuardianTeamCommunicationCommandKind.ClearBind2, disabled, "disable permits exact owned cleanup");

        (active, observation) = ReachActivePair(episode with { Token = 2 });
        var resetWithDrift = GuardianTeamCommunicationRules.Observe(
            active,
            observation with
            {
                HardReset = true,
                NowMilliseconds = 2_000,
                Bind2 = Marker(GuardianTeamCommunicationRules.Bind2MarkerIndex, Other.GameObjectId, 22),
            });
        False(resetWithDrift.ShouldIssueCommand, "hard reset never issues a command");
        Equal(GuardianTeamCommunicationPhase.Idle, resetWithDrift.State.Phase, "hard reset relinquishes runtime state");

        (active, observation) = ReachActivePair(episode with { Token = 3 });
        var outside = GuardianTeamCommunicationRules.Observe(
            active,
            observation with { IsCrystallineConflict = false, NowMilliseconds = 2_000 });
        False(outside.ShouldIssueCommand, "context loss cannot issue cleanup");
        Equal(GuardianTeamCommunicationPhase.Idle, outside.State.Phase, "context loss relinquishes state");
    }

    internal static void PendingConfirmationSurvivesConfigAndTextUntilCleanupIsSafe()
    {
        var episode = Episode();
        var pendingBind2 = ReachAwaitingBind2Confirmation(episode);
        var disabledBeforePropagation = GuardianTeamCommunicationRules.Observe(
            pendingBind2,
            Observation(episode, now: 1_002, configurationEnabled: false));
        Equal(GuardianTeamCommunicationPhase.AwaitingBind2Confirmation, disabledBeforePropagation.State.Phase,
            "config loss keeps already-invoked Bind2 confirmation bounded");
        False(disabledBeforePropagation.ShouldIssueCommand, "config loss never issues another set");

        var confirmedWhileTyping = GuardianTeamCommunicationRules.Observe(
            disabledBeforePropagation.State,
            Observation(
                episode,
                now: 1_003,
                configurationEnabled: false,
                textInputActive: true,
                bind2GameObjectId: episode.Target.GameObjectId,
                bind2Time: 21));
        Equal(GuardianTeamCommunicationPhase.ReadyToClearBind2, confirmedWhileTyping.State.Phase,
            "late Bind2 propagation becomes owned cleanup state");
        True(confirmedWhileTyping.State.OwnsBind2, "late exact propagation is not stranded");
        False(confirmedWhileTyping.ShouldIssueCommand, "typing blocks owned cleanup command");

        var safeAgain = GuardianTeamCommunicationRules.Observe(
            confirmedWhileTyping.State,
            Observation(
                episode,
                now: 1_004,
                configurationEnabled: false,
                bind2GameObjectId: episode.Target.GameObjectId,
                bind2Time: 21));
        Command(GuardianTeamCommunicationCommandKind.ClearBind2, safeAgain,
            "owned Bind2 cleanup resumes only when command input is safe");

        var (pendingBind1, bind1Observation) = ReachAwaitingBind1Confirmation(episode with { Token = 2 });
        var bind1ConfirmedWhileTyping = GuardianTeamCommunicationRules.Observe(
            pendingBind1,
            bind1Observation with
            {
                ConfigurationEnabled = false,
                TextInputActive = true,
                NowMilliseconds = 1_005,
                Bind1 = Marker(
                    GuardianTeamCommunicationRules.Bind1MarkerIndex,
                    episode.LocalPlayer.GameObjectId,
                    11),
            });
        Equal(GuardianTeamCommunicationPhase.ReadyToClearBind2, bind1ConfirmedWhileTyping.State.Phase,
            "confirmed pair enters ordered cleanup while typing");
        True(bind1ConfirmedWhileTyping.State.OwnsBind1 && bind1ConfirmedWhileTyping.State.OwnsBind2,
            "both exact ownership records survive until cleanup is safe");
        False(bind1ConfirmedWhileTyping.ShouldIssueCommand, "typing never emits clear");
    }

    internal static void DeferredBeforeInvocationIsTheOnlyRepeatableDecision()
    {
        var episode = Episode();
        var observation = Observation(episode);
        var quickChat = GuardianTeamCommunicationRules.Observe(
            GuardianTeamCommunicationState.Initial,
            observation);

        var afterDeferredChat = Apply(
            quickChat,
            GuardianTeamCommunicationCommandOutcome.DeferredBeforeInvocation);
        Equal(GuardianTeamCommunicationPhase.ReadyToSetBind2, afterDeferredChat.Phase,
            "QuickChat is spent even on an unexpected deferral");

        var firstBind2 = GuardianTeamCommunicationRules.Observe(
            afterDeferredChat,
            observation with { NowMilliseconds = 1_001 });
        Command(GuardianTeamCommunicationCommandKind.SetBind2, firstBind2, "first marker reservation");
        var deferred = Apply(firstBind2, GuardianTeamCommunicationCommandOutcome.DeferredBeforeInvocation);
        Equal(GuardianTeamCommunicationPhase.ReadyToSetBind2, deferred.Phase,
            "pre-invocation marker reservation may wait");

        var secondBind2 = GuardianTeamCommunicationRules.Observe(
            deferred,
            observation with { NowMilliseconds = 1_101 });
        Command(GuardianTeamCommunicationCommandKind.SetBind2, secondBind2, "deferred command can be offered again");
        var terminal = Apply(secondBind2, GuardianTeamCommunicationCommandOutcome.TerminalFailure);
        Equal(GuardianTeamCommunicationPhase.Idle, terminal.Phase, "terminal marker result never retries");

        var duplicate = GuardianTeamCommunicationRules.Observe(
            terminal,
            observation with { NowMilliseconds = 1_102 });
        False(duplicate.ShouldIssueCommand, "spent episode cannot start again");
    }

    internal static void NewEpisodeWhileBusyIsConsumedWithoutReplacement()
    {
        var firstEpisode = Episode(token: 1);
        var observation = Observation(firstEpisode);
        var state = Apply(
            GuardianTeamCommunicationRules.Observe(
                GuardianTeamCommunicationState.Initial,
                observation),
            GuardianTeamCommunicationCommandOutcome.Invoked);

        var secondEpisode = Episode(token: 2, acceptedAt: 1_001);
        var busy = GuardianTeamCommunicationRules.Observe(
            state,
            Observation(secondEpisode, now: 1_001));
        Command(GuardianTeamCommunicationCommandKind.SetBind2, busy, "original frozen episode continues");
        Equal(2L, busy.State.LastConsumedEpisodeToken, "newer token consumed while busy");
        Equal(firstEpisode.Token, busy.Command!.Value.EpisodeToken, "new episode cannot replace frozen intent");

        var terminal = Apply(busy, GuardianTeamCommunicationCommandOutcome.TerminalFailure);
        var noReplay = GuardianTeamCommunicationRules.Observe(
            terminal,
            Observation(secondEpisode, now: 1_002));
        False(noReplay.ShouldIssueCommand, "busy-consumed episode cannot replay later");
    }

    private static GuardianTeamCommunicationState ReachAwaitingBind2Confirmation(
        GuardianTeamCommunicationEpisode episode)
    {
        var observation = Observation(episode);
        var quickChat = GuardianTeamCommunicationRules.Observe(
            GuardianTeamCommunicationState.Initial,
            observation);
        var state = Apply(quickChat, GuardianTeamCommunicationCommandOutcome.Invoked);
        var bind2 = GuardianTeamCommunicationRules.Observe(
            state,
            observation with { NowMilliseconds = episode.AcceptedAtMilliseconds + 1 });
        return Apply(bind2, GuardianTeamCommunicationCommandOutcome.Invoked);
    }

    private static (
        GuardianTeamCommunicationState State,
        GuardianTeamCommunicationObservation Observation) ReachAwaitingBind1Confirmation(
        GuardianTeamCommunicationEpisode episode)
    {
        var state = ReachAwaitingBind2Confirmation(episode);
        var observation = Observation(
            episode,
            now: episode.AcceptedAtMilliseconds + 2,
            bind2GameObjectId: episode.Target.GameObjectId,
            bind2Time: 21);
        var confirmed = GuardianTeamCommunicationRules.Observe(state, observation);
        var bind1 = GuardianTeamCommunicationRules.Observe(
            confirmed.State,
            observation with { NowMilliseconds = episode.AcceptedAtMilliseconds + 3 });
        return (Apply(bind1, GuardianTeamCommunicationCommandOutcome.Invoked), observation);
    }

    private static (
        GuardianTeamCommunicationState State,
        GuardianTeamCommunicationObservation Observation) ReachActivePair(
        GuardianTeamCommunicationEpisode episode)
    {
        var (state, observation) = ReachAwaitingBind1Confirmation(episode);
        observation = observation with
        {
            NowMilliseconds = episode.AcceptedAtMilliseconds + 4,
            Bind1 = Marker(
                GuardianTeamCommunicationRules.Bind1MarkerIndex,
                episode.LocalPlayer.GameObjectId,
                11),
        };
        var active = GuardianTeamCommunicationRules.Observe(state, observation);
        Equal(GuardianTeamCommunicationPhase.ActivePair, active.State.Phase, "test setup active pair");
        return (active.State, observation);
    }

    private static GuardianTeamCommunicationEpisode Episode(
        long token = 1,
        long acceptedAt = 1_000,
        int partySlot = 3) =>
        new(token, acceptedAt, Local, Ally, partySlot);

    private static GuardianTeamCommunicationObservation Observation(
        GuardianTeamCommunicationEpisode? episode,
        long now = 1_000,
        bool configurationEnabled = true,
        bool crystallineConflict = true,
        bool hardReset = false,
        bool textInputKnown = true,
        bool textInputActive = false,
        bool exactLocal = true,
        bool exactTarget = true,
        bool bind1Available = true,
        bool bind2Available = true,
        ulong bind1GameObjectId = 0,
        ulong bind2GameObjectId = 0,
        long bind1Time = 10,
        long bind2Time = 20)
    {
        var value = episode ?? Episode();
        return new GuardianTeamCommunicationObservation(
            configurationEnabled,
            crystallineConflict,
            hardReset,
            textInputKnown,
            textInputActive,
            now,
            episode,
            new GuardianTeamCommunicationResolvedActor(exactLocal, value.LocalPlayer),
            new GuardianTeamCommunicationResolvedPartyMember(
                exactTarget,
                value.PartySlot,
                value.Target),
            new GuardianTeamCommunicationMarkerObservation(
                GuardianTeamCommunicationRules.Bind1MarkerIndex,
                bind1Available,
                bind1GameObjectId,
                bind1Time),
            new GuardianTeamCommunicationMarkerObservation(
                GuardianTeamCommunicationRules.Bind2MarkerIndex,
                bind2Available,
                bind2GameObjectId,
                bind2Time));
    }

    private static GuardianTeamCommunicationMarkerObservation Marker(
        int index,
        ulong gameObjectId,
        long markerTime) =>
        new(index, true, gameObjectId, markerTime);

    private static GuardianTeamCommunicationState Apply(
        GuardianTeamCommunicationDecision decision,
        GuardianTeamCommunicationCommandOutcome outcome)
    {
        True(decision.ShouldIssueCommand, "decision should issue command");
        return GuardianTeamCommunicationRules.ApplyCommandResult(
            decision.State,
            decision.Command!.Value,
            outcome);
    }

    private static GuardianTeamCommunicationCommand SetBind1Command(
        GuardianTeamCommunicationEpisode episode) =>
        new(
            GuardianTeamCommunicationCommandKind.SetBind1,
            episode.Token,
            0,
            episode.LocalPlayer,
            GuardianTeamCommunicationRules.Bind1MarkerIndex,
            0);

    private static void Command(
        GuardianTeamCommunicationCommandKind expected,
        GuardianTeamCommunicationDecision decision,
        string message)
    {
        True(decision.ShouldIssueCommand, message);
        Equal(expected, decision.Command!.Value.Kind, message);
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected={expected}, actual={actual}");
    }
}
