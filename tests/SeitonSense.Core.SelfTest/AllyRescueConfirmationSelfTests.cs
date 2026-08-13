using SeitonSense.Core;

internal static class AllyRescueConfirmationSelfTests
{
    private const uint Caster = 0x100;
    private const uint Target = 0x200;
    private const ulong TargetObject = 0x200UL;

    public static void AttemptRegistrationNeverConfirmsLocally()
    {
        var accepted = Register(accepted: true, now: 1_000);
        True(accepted.PendingRegistered, "accepted attempt registers pending");
        False(accepted.Confirmed, "local true never confirms");
        Equal(0L, accepted.NextState.SessionStatistics.TotalConfirmed, "no local confirmation count");

        var rejected = AllyRescueConfirmationRules.RegisterAttempt(
            accepted.NextState,
            Attempt(accepted: false, now: 1_100),
            1_100);
        True(rejected.PendingRegistered, "local false still registers a real call for server correlation");
        False(rejected.Confirmed, "local false never confirms either");
        False(rejected.NextState.Pending!.Value.UseActionAccepted, "diagnostic accepted bit is preserved");
    }

    public static void ExactRecoveredEffectConfirmsOnce()
    {
        var pending = Register(accepted: false, now: 1_000).NextState;
        var confirmed = AllyRescueConfirmationRules.ObserveActionEffect(
            pending,
            Effect(statusId: AllyRescueConfirmationRules.StunStatusId, now: 1_120));

        True(confirmed.Confirmed, "exact server recovery confirms");
        False(confirmed.Duplicate, "first exact packet is not duplicate");
        True(confirmed.NextState.Pending is null, "confirmation consumes pending attempt");
        Equal(1L, confirmed.NextState.SessionStatistics.TotalConfirmed, "session total count");
        Equal(1L, confirmed.NextState.MatchStatistics.TotalConfirmed, "match total count");
        Equal(1L, confirmed.NextState.SessionStatistics.CountForAction(AllyRescueConfirmationRules.AquaveilActionId), "per-action count");
        Equal(1L, confirmed.NextState.SessionStatistics.CountForStatus(AllyRescueConfirmationRules.StunStatusId), "per-status count");
        Equal(AllyRescueConfirmationRules.StunStatusId, confirmed.TriggeredPopup!.Value.RemovedStatusId, "popup reports actual status");
        Equal(2_620L, confirmed.TriggeredPopup!.Value.EndsAtMilliseconds, "popup lasts exactly 1500 ms");
    }

    public static void ExactIdentityAndEffectShapeAreRequired()
    {
        var variants = new[]
        {
            Effect(now: 1_100) with { CasterEntityId = 0x101 },
            Effect(now: 1_100) with { ActionId = AllyRescueConfirmationRules.WardensPaeanActionId },
            Effect(now: 1_100) with { TargetEntityId = 0x201 },
            Effect(now: 1_100) with { EffectType = 0x11 },
            Effect(now: 1_100) with { EffectValue = 999 },
            Effect(now: 1_100) with { GlobalSequence = 0, SourceSequence = 0 },
            Effect(now: 999),
            Effect(now: 3_501),
        };

        foreach (var variant in variants)
        {
            var decision = AllyRescueConfirmationRules.ObserveActionEffect(
                Register(accepted: true, now: 1_000).NextState,
                variant);
            False(decision.Confirmed, $"mismatch ignored: {variant}");
            Equal(0L, decision.NextState.SessionStatistics.TotalConfirmed, "mismatch cannot count");
        }
    }

    public static void DuplicateSequenceCannotDoubleCount()
    {
        var first = AllyRescueConfirmationRules.ObserveActionEffect(
            Register(accepted: true, now: 1_000).NextState,
            Effect(now: 1_100));
        var registeredAgain = AllyRescueConfirmationRules.RegisterAttempt(
            first.NextState,
            Attempt(accepted: true, now: 1_200),
            1_200);
        var duplicate = AllyRescueConfirmationRules.ObserveActionEffect(
            registeredAgain.NextState,
            Effect(now: 1_250));

        False(duplicate.Confirmed, "duplicate is not confirmed twice");
        True(duplicate.Duplicate, "duplicate is exposed diagnostically");
        Equal(1L, duplicate.NextState.SessionStatistics.TotalConfirmed, "duplicate cannot increment total");
        True(duplicate.NextState.Pending is null, "duplicate consumes matching pending evidence");
    }

    public static void AllSixRemovedStatusesAreCountedButOnlyFourTrigger()
    {
        uint[] statuses =
        [
            AllyRescueConfirmationRules.StunStatusId,
            AllyRescueConfirmationRules.HeavyStatusId,
            AllyRescueConfirmationRules.BindStatusId,
            AllyRescueConfirmationRules.SilenceStatusId,
            AllyRescueConfirmationRules.MiracleOfNatureStatusId,
            AllyRescueConfirmationRules.DeepFreezeStatusId,
        ];
        var state = AllyRescueConfirmationState.Initial;
        for (var index = 0; index < statuses.Length; index++)
        {
            var attemptedAt = 1_000L + (index * 100L);
            state = AllyRescueConfirmationRules.RegisterAttempt(
                state,
                Attempt(accepted: true, now: attemptedAt),
                attemptedAt).NextState;
            state = AllyRescueConfirmationRules.ObserveActionEffect(
                state,
                Effect(statuses[index], attemptedAt + 10) with
                {
                    GlobalSequence = (uint)(100 + index),
                    SourceSequence = (ushort)(10 + index),
                }).NextState;
        }

        Equal(6L, state.SessionStatistics.TotalConfirmed, "all actual Purify-removable statuses count");
        Equal(6L, state.MatchStatistics.TotalConfirmed, "match counts all actual cleanses too");
        foreach (var status in statuses)
        {
            True(AllyRescueConfirmationRules.IsConfirmableRemovedStatus(status), $"confirmable {status}");
            Equal(1L, state.SessionStatistics.CountForStatus(status), $"status count {status}");
        }

        False(AllyRescueStatusRules.IsTriggerStatus(AllyRescueConfirmationRules.HeavyStatusId), "Heavy does not activate rescue");
        False(AllyRescueStatusRules.IsTriggerStatus(AllyRescueConfirmationRules.BindStatusId), "Bind does not activate rescue");
    }

    public static void PopupAndPendingExpireWithoutChangingSessionCounts()
    {
        var confirmed = AllyRescueConfirmationRules.ObserveActionEffect(
            Register(accepted: true, now: 1_000).NextState,
            Effect(now: 1_100)).NextState;
        True(confirmed.Popup!.Value.IsVisible(2_599), "popup visible before exclusive boundary");

        var expired = AllyRescueConfirmationRules.ObserveTime(confirmed, 2_600);
        True(expired.Popup is null, "popup expires at 1500 ms boundary");
        Equal(1L, expired.SessionStatistics.TotalConfirmed, "popup expiry preserves session count");
        Equal(1L, expired.MatchStatistics.TotalConfirmed, "popup expiry preserves match count");

        var pending = AllyRescueConfirmationRules.RegisterAttempt(
            expired,
            Attempt(accepted: true, now: 3_000),
            3_000).NextState;
        pending = AllyRescueConfirmationRules.ObserveTime(pending, 5_501);
        True(pending.Pending is null, "correlation deadline is exclusive after 2500 ms");
        Equal(1L, pending.SessionStatistics.TotalConfirmed, "pending timeout preserves session count");
    }

    public static void HardResetAndInvalidStateFailClosed()
    {
        var confirmed = AllyRescueConfirmationRules.ObserveActionEffect(
            Register(accepted: true, now: 1_000).NextState,
            Effect(now: 1_100)).NextState;
        var reset = AllyRescueConfirmationRules.ObserveTime(confirmed, 1_200, hardReset: true);
        Equal(1L, reset.SessionStatistics.TotalConfirmed, "hard reset preserves session statistics");
        Equal(0L, reset.MatchStatistics.TotalConfirmed, "hard reset clears match statistics");
        True(reset.Pending is null, "hard reset clears pending");
        True(reset.Popup is null, "hard reset clears popup");
        Equal(0, reset.ConfirmedKeys.Length, "hard reset clears match dedupe");

        var cleared = AllyRescueConfirmationRules.ResetStatistics(reset);
        Equal(0L, cleared.SessionStatistics.TotalConfirmed, "explicit reset clears session statistics");
        Equal(0L, cleared.MatchStatistics.TotalConfirmed, "explicit reset clears match statistics");

        var visible = AllyRescueConfirmationRules.ObserveActionEffect(
            AllyRescueConfirmationRules.RegisterAttempt(
                AllyRescueConfirmationState.Initial,
                Attempt(accepted: true, now: 2_000),
                2_000).NextState,
            Effect(now: 2_100)).NextState;
        True(visible.Popup is not null, "confirmed cleanse has a popup before reset");
        var resetVisible = AllyRescueConfirmationRules.ResetStatistics(visible);
        True(resetVisible.Popup is null, "statistics reset closes the old popup");
        Equal(1, resetVisible.ConfirmedKeys.Length, "statistics reset keeps dedupe evidence");

        var invalid = AllyRescueConfirmationRules.RegisterAttempt(
            AllyRescueConfirmationState.Initial,
            Attempt(accepted: true, now: 1_000) with { LocalCasterEntityId = 0 },
            1_000);
        False(invalid.PendingRegistered, "invalid actor cannot register");
        True(invalid.NextState.Pending is null, "invalid attempt fails closed");

        invalid = AllyRescueConfirmationRules.ObserveActionEffect(
            confirmed,
            Effect(now: 1_099));
        False(invalid.Confirmed, "backwards clock cannot confirm");
        True(invalid.NextState.Pending is null, "clock regression clears pending");
    }

    private static AllyRescueConfirmationDecision Register(bool accepted, long now) =>
        AllyRescueConfirmationRules.RegisterAttempt(
            AllyRescueConfirmationState.Initial,
            Attempt(accepted, now),
            now);

    private static AllyRescuePendingAttempt Attempt(bool accepted, long now) =>
        new(
            Caster,
            AllyRescueConfirmationRules.AquaveilActionId,
            TargetObject,
            Target,
            new AllyRescueIntent(
                TargetObject,
                Target,
                new AllyRescueStatusInstance(AllyRescueStatusRules.StunStatusId, 1)),
            accepted,
            now);

    private static AllyRescueActionEffectObservation Effect(
        uint statusId = AllyRescueConfirmationRules.StunStatusId,
        long now = 1_100) =>
        new(
            Caster,
            AllyRescueConfirmationRules.AquaveilActionId,
            Target,
            AllyRescueConfirmationRules.RecoveredFromStatusEffectType,
            (ushort)statusId,
            100,
            10,
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
