using SeitonSense.Core;

internal static class MiracleInterceptConfirmationSelfTests
{
    private const uint Caster = 0x100;
    private const uint Target = 0x200;
    private const ulong TargetObject = 0x200UL;

    public static void AttemptNeverConfirmsLocally()
    {
        var registered = Register(MiracleInterceptThreatKind.MarksmanSpite, accepted: true, now: 1_000);
        True(registered.PendingRegistered, "exact native attempt registered");
        False(registered.Confirmed, "local return never confirms a landing");
        Equal(0L, registered.NextState.TotalConfirmed, "no local count");

        registered = Register(MiracleInterceptThreatKind.Zantetsuken, accepted: false, now: 2_000);
        False(registered.PendingRegistered, "a rejected native call cannot create a confirmation pending");
        True(registered.NextState.Pending is null, "rejected call remains unconfirmable");

        registered = Register(
            MiracleInterceptThreatKind.Zantetsuken,
            accepted: true,
            now: 3_000,
            expectedSourceSequence: 0);
        True(registered.PendingRegistered, "accepted call remains visible while its source sequence is deferred");
        True(registered.NextState.Pending is { HasBoundSourceSequence: false },
            "accepted call records that it is awaiting the exact ActionEffect sequence");

        var zeroSequence = MiracleInterceptConfirmationRules.ObserveActionEffect(
            registered.NextState,
            Effect(now: 3_050, sourceSequence: 0));
        False(zeroSequence.Confirmed, "a zero-sequence server packet cannot bind deferred ownership");
        True(zeroSequence.NextState.Pending is not null,
            "a non-binding packet cannot consume the accepted pending episode");

        var wrongTarget = MiracleInterceptConfirmationRules.ObserveActionEffect(
            zeroSequence.NextState,
            Effect(now: 3_051, sourceSequence: 21) with { TargetEntityId = 0x201 });
        False(wrongTarget.Confirmed, "a different actor cannot bind deferred ownership");
        True(wrongTarget.NextState.Pending is not null,
            "identity mismatch preserves the exact accepted pending episode");

        var bound = MiracleInterceptConfirmationRules.ObserveActionEffect(
            wrongTarget.NextState,
            Effect(now: 3_052, sourceSequence: 21));
        True(bound.Confirmed, "the first later exact non-zero ActionEffect binds and confirms");
        Equal(1L, bound.NextState.TotalConfirmed, "deferred binding confirms exactly once");
    }

    public static void ExactStatusAddConfirmsAndLabelsThreat()
    {
        var state = Register(MiracleInterceptThreatKind.FuriousBacklash, accepted: true, now: 1_000).NextState;
        var decision = MiracleInterceptConfirmationRules.ObserveActionEffect(
            state,
            Effect(now: 1_120));

        True(decision.Confirmed, "exact server status add confirms Miracle landed");
        False(decision.Duplicate, "first packet is not duplicate");
        Equal(1L, decision.NextState.TotalConfirmed, "confirmed count");
        Equal(MiracleInterceptThreatKind.FuriousBacklash, decision.TriggeredPopup!.Value.Threat, "popup keeps exact triggering threat");
        Equal(2_620L, decision.TriggeredPopup!.Value.EndsAtMilliseconds, "news flash lasts 1500 ms");
        True(decision.NextState.Pending is null, "confirmation consumes pending attempt");
    }

    public static void SilentNocturneRequiresExactSilenceStatus()
    {
        var state = Register(
            MiracleInterceptThreatKind.Contradance,
            accepted: true,
            now: 1_000,
            actionId: MiracleInterceptConfirmationRules.SilentNocturneActionId).NextState;
        var wrongStatus = MiracleInterceptConfirmationRules.ObserveActionEffect(
            state,
            Effect(
                now: 1_100,
                actionId: MiracleInterceptConfirmationRules.SilentNocturneActionId,
                effectValue: MiracleInterceptConfirmationRules.MiracleOfNatureStatusId));
        False(wrongStatus.Confirmed, "Silent Nocturne cannot confirm from Miracle status");

        var exact = MiracleInterceptConfirmationRules.ObserveActionEffect(
            state,
            Effect(
                now: 1_101,
                actionId: MiracleInterceptConfirmationRules.SilentNocturneActionId,
                effectValue: MiracleInterceptConfirmationRules.SilenceStatusId));
        True(exact.Confirmed, "exact Silent Nocturne Silence add confirms");
        Equal(
            MiracleInterceptConfirmationRules.SilentNocturneActionId,
            exact.TriggeredPopup!.Value.ActionId,
            "popup retains the actual counter-CC action");
        Equal(
            MiracleInterceptThreatKind.Contradance,
            exact.TriggeredPopup.Value.Threat,
            "DNC startup label is retained");

        state = Register(
            MiracleInterceptThreatKind.PostPurifyCrowdControl,
            accepted: true,
            now: 2_000,
            actionId: MiracleInterceptConfirmationRules.SilentNocturneActionId,
            removedStatusId: MiracleCleanseFollowupRules.DeepFreezeStatusId).NextState;
        var followup = MiracleInterceptConfirmationRules.ObserveActionEffect(
            state,
            Effect(
                now: 2_100,
                actionId: MiracleInterceptConfirmationRules.SilentNocturneActionId));
        True(followup.Confirmed, "post-Purify Silent confirmation is exact");
        Equal(
            MiracleCleanseFollowupRules.DeepFreezeStatusId,
            followup.TriggeredPopup!.Value.RemovedStatusId,
            "popup retains the Purify-removed CC label");
    }

    public static void NinjaRaijuVariantsRequireExactStunStatus()
    {
        foreach (var actionId in new[]
                 {
                     MiracleInterceptConfirmationRules.ForkedRaijuActionId,
                     MiracleInterceptConfirmationRules.FleetingRaijuActionId,
                 })
        {
            var state = Register(
                MiracleInterceptThreatKind.PostGuardCrowdControl,
                accepted: true,
                now: 1_000,
                actionId: actionId).NextState;
            var wrong = MiracleInterceptConfirmationRules.ObserveActionEffect(
                state,
                Effect(
                    now: 1_050,
                    actionId: actionId,
                    effectValue: MiracleInterceptConfirmationRules.SilenceStatusId));
            False(wrong.Confirmed, $"Raiju {actionId} cannot confirm from Silence");

            var manual = MiracleInterceptConfirmationRules.ObserveActionEffect(
                wrong.NextState,
                Effect(
                    now: 1_051,
                    actionId: actionId,
                    effectValue: MiracleInterceptConfirmationRules.StunStatusId,
                    sourceSequence: 10));
            False(manual.Confirmed, $"Raiju {actionId} cannot confirm from a manual source sequence");
            True(manual.NextState.Pending is not null, "wrong Raiju sequence preserves the exact pending");

            var exact = MiracleInterceptConfirmationRules.ObserveActionEffect(
                manual.NextState,
                Effect(
                    now: 1_052,
                    actionId: actionId,
                    effectValue: MiracleInterceptConfirmationRules.StunStatusId));
            True(exact.Confirmed, $"Raiju {actionId} confirms only exact Stun");
            Equal(actionId, exact.TriggeredPopup!.Value.ActionId, "popup keeps exact Raiju variant");
        }
    }

    public static void CorrelationRequiresExactIdentityShapeAndWindow()
    {
        var variants = new[]
        {
            Effect(now: 1_100) with { CasterEntityId = 0x101 },
            Effect(now: 1_100) with { ActionId = 29_229 },
            Effect(now: 1_100) with { TargetEntityId = 0x201 },
            Effect(now: 1_100) with { EffectType = 0x0F },
            Effect(now: 1_100) with { EffectValue = 3_086 },
            Effect(now: 1_100) with { GlobalSequence = 0, SourceSequence = 0 },
            Effect(now: 1_100) with { GlobalSequence = 77, SourceSequence = 0 },
            Effect(now: 1_100) with { SourceSequence = 10 },
            Effect(now: 999),
            Effect(now: 2_501),
        };

        foreach (var observation in variants)
        {
            var decision = MiracleInterceptConfirmationRules.ObserveActionEffect(
                Register(MiracleInterceptThreatKind.MarksmanSpite, accepted: true, now: 1_000).NextState,
                observation);
            False(decision.Confirmed, $"mismatch rejected: {observation}");
        }

        var boundary = MiracleInterceptConfirmationRules.ObserveActionEffect(
            Register(MiracleInterceptThreatKind.Zantetsuken, accepted: true, now: 1_000).NextState,
            Effect(now: 2_500));
        True(boundary.Confirmed, "exact 1500 ms boundary remains eligible");

        var pending = Register(
            MiracleInterceptThreatKind.PostGuardCrowdControl,
            accepted: true,
            now: 3_000).NextState;
        var manual = MiracleInterceptConfirmationRules.ObserveActionEffect(
            pending,
            Effect(now: 3_100, sourceSequence: 10));
        False(manual.Confirmed, "manual same-action same-target packet cannot confirm helper ownership");
        True(manual.NextState.Pending is not null, "manual packet leaves helper pending alive");
        var stillPending = manual.NextState.Pending!.Value;
        Equal((ushort)9, stillPending.ExpectedSourceSequence, "pending retains exact helper source sequence");

        var exact = MiracleInterceptConfirmationRules.ObserveActionEffect(
            manual.NextState,
            Effect(now: 3_101));
        True(exact.Confirmed, "later exact helper source sequence still confirms");
    }

    public static void DuplicateCannotIncrementTwice()
    {
        var first = MiracleInterceptConfirmationRules.ObserveActionEffect(
            Register(MiracleInterceptThreatKind.Zantetsuken, accepted: true, now: 1_000).NextState,
            Effect(now: 1_100));
        var secondPending = MiracleInterceptConfirmationRules.RegisterAttempt(
            first.NextState,
            Attempt(MiracleInterceptThreatKind.Zantetsuken, accepted: true, now: 1_200),
            1_200).NextState;
        var duplicate = MiracleInterceptConfirmationRules.ObserveActionEffect(
            secondPending,
            Effect(now: 1_250));

        False(duplicate.Confirmed, "same server sequence is not confirmed twice");
        True(duplicate.Duplicate, "duplicate is diagnosed");
        Equal(1L, duplicate.NextState.TotalConfirmed, "count remains one");
    }

    public static void NewAttemptCannotOverwriteActivePending()
    {
        var first = Register(
            MiracleInterceptThreatKind.MarksmanSpite,
            accepted: true,
            now: 1_000).NextState;
        var second = MiracleInterceptConfirmationRules.RegisterAttempt(
            first,
            Attempt(MiracleInterceptThreatKind.Zantetsuken, accepted: true, now: 1_200),
            1_200);

        False(second.PendingRegistered, "second attempt does not replace a live pending correlation");
        Equal(
            MiracleInterceptThreatKind.MarksmanSpite,
            second.NextState.Pending!.Value.Threat,
            "first pending threat remains exact");

        var confirmed = MiracleInterceptConfirmationRules.ObserveActionEffect(
            second.NextState,
            Effect(now: 1_250));
        True(confirmed.Confirmed, "first attempt can still receive its exact confirmation");
        Equal(
            MiracleInterceptThreatKind.MarksmanSpite,
            confirmed.TriggeredPopup!.Value.Threat,
            "popup cannot be mislabeled as the second threat");
    }

    public static void PopupAndPendingExpireWithoutReplay()
    {
        var confirmed = MiracleInterceptConfirmationRules.ObserveActionEffect(
            Register(MiracleInterceptThreatKind.MarksmanSpite, accepted: true, now: 1_000).NextState,
            Effect(now: 1_100)).NextState;
        var visible = MiracleInterceptConfirmationRules.ObserveTime(confirmed, 2_599);
        True(visible.Popup is not null, "popup visible inside duration");
        var expired = MiracleInterceptConfirmationRules.ObserveTime(visible, 2_600);
        True(expired.Popup is null, "popup expires at exact duration boundary");

        var pending = Register(MiracleInterceptThreatKind.MarksmanSpite, accepted: true, now: 3_000).NextState;
        pending = MiracleInterceptConfirmationRules.ObserveTime(pending, 4_501);
        True(pending.Pending is null, "unconfirmed attempt expires and is never replayed");
    }

    private static MiracleInterceptConfirmationDecision Register(
        MiracleInterceptThreatKind threat,
        bool accepted,
        long now,
        uint actionId = MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
        uint removedStatusId = 0,
        ushort expectedSourceSequence = 9) =>
        MiracleInterceptConfirmationRules.RegisterAttempt(
            MiracleInterceptConfirmationState.Initial,
            Attempt(threat, accepted, now, actionId, removedStatusId, expectedSourceSequence),
            now);

    private static MiracleInterceptPendingAttempt Attempt(
        MiracleInterceptThreatKind threat,
        bool accepted,
        long now,
        uint actionId = MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
        uint removedStatusId = 0,
        ushort expectedSourceSequence = 9) =>
        new(
            Caster,
            actionId,
            TargetObject,
            Target,
            threat,
            accepted,
            now,
            expectedSourceSequence)
        {
            RemovedStatusId = removedStatusId,
        };

    private static MiracleInterceptLandedObservation Effect(
        long now,
        uint actionId = MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
        ushort? effectValue = null,
        ushort sourceSequence = 9) =>
        new(
            Caster,
            actionId,
            Target,
            MiracleInterceptConfirmationRules.AddStatusEffectType,
            effectValue ?? MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId),
            77,
            sourceSequence,
            now);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
