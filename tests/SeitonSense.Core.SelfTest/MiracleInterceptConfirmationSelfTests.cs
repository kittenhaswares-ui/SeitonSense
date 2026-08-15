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
        True(registered.PendingRegistered, "a real native call is correlatable even after a false local return");
        False(registered.NextState.Pending!.Value.UseActionAccepted, "diagnostic local result preserved");
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
        uint removedStatusId = 0) =>
        MiracleInterceptConfirmationRules.RegisterAttempt(
            MiracleInterceptConfirmationState.Initial,
            Attempt(threat, accepted, now, actionId, removedStatusId),
            now);

    private static MiracleInterceptPendingAttempt Attempt(
        MiracleInterceptThreatKind threat,
        bool accepted,
        long now,
        uint actionId = MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
        uint removedStatusId = 0) =>
        new(
            Caster,
            actionId,
            TargetObject,
            Target,
            threat,
            accepted,
            now)
        {
            RemovedStatusId = removedStatusId,
        };

    private static MiracleInterceptLandedObservation Effect(
        long now,
        uint actionId = MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
        ushort? effectValue = null) =>
        new(
            Caster,
            actionId,
            Target,
            MiracleInterceptConfirmationRules.AddStatusEffectType,
            effectValue ?? MiracleInterceptConfirmationRules.ExpectedStatusForAction(actionId),
            77,
            9,
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
