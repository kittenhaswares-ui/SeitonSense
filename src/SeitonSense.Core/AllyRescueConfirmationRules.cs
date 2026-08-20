using System.Collections.Immutable;

namespace SeitonSense.Core;

/// <summary>
/// The exact attempted friendly action that may later be confirmed by a
/// server ActionEffect packet. The local return value is retained for
/// diagnostics only; it is not proof that the action landed.
/// </summary>
public readonly record struct AllyRescuePendingAttempt(
    uint LocalCasterEntityId,
    uint ActionId,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    AllyRescueIntent Intent,
    bool UseActionAccepted,
    long AttemptedAtMilliseconds)
{
    public bool IsValid =>
        AllyRescueConfirmationRules.IsValidEntityId(LocalCasterEntityId) &&
        AllyRescueConfirmationRules.IsRescueAction(ActionId) &&
        TargetHighlightRules.IsValidGameObjectId(TargetGameObjectId) &&
        AllyRescueConfirmationRules.IsValidEntityId(TargetEntityId) &&
        Intent.IsValid &&
        Intent.GameObjectId == TargetGameObjectId &&
        Intent.EntityId == TargetEntityId &&
        AttemptedAtMilliseconds >= 0;
}

/// <summary>
/// Narrow ActionEffect evidence. Effect type 0x10 is the server's
/// RecoveredFromStatusEffect result and EffectValue is the removed status ID.
/// </summary>
public readonly record struct AllyRescueActionEffectObservation(
    uint CasterEntityId,
    uint ActionId,
    uint TargetEntityId,
    byte EffectType,
    ushort EffectValue,
    uint GlobalSequence,
    ushort SourceSequence,
    long ObservedAtMilliseconds);

public readonly record struct AllyRescueConfirmationKey(
    uint CasterEntityId,
    uint ActionId,
    uint TargetEntityId,
    uint GlobalSequence,
    ushort SourceSequence,
    uint RemovedStatusId);

public readonly record struct AllyRescueConfirmationCount(
    uint Id,
    long Count);

public sealed record AllyRescueConfirmationStatistics(
    long TotalConfirmed,
    ImmutableArray<AllyRescueConfirmationCount> ByAction,
    ImmutableArray<AllyRescueConfirmationCount> ByStatus)
{
    public static AllyRescueConfirmationStatistics Empty { get; } = new(
        0,
        ImmutableArray<AllyRescueConfirmationCount>.Empty,
        ImmutableArray<AllyRescueConfirmationCount>.Empty);

    public long CountForAction(uint actionId) => FindCount(ByAction, actionId);

    public long CountForStatus(uint statusId) => FindCount(ByStatus, statusId);

    private static long FindCount(
        ImmutableArray<AllyRescueConfirmationCount> counts,
        uint id)
    {
        if (counts.IsDefaultOrEmpty) return 0;
        foreach (var item in counts)
        {
            if (item.Id == id) return item.Count;
        }

        return 0;
    }
}

public readonly record struct AllyRescueConfirmationPopup(
    uint ActionId,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    uint RemovedStatusId,
    long StartedAtMilliseconds,
    long EndsAtMilliseconds)
{
    public bool IsVisible(long nowMilliseconds) =>
        StartedAtMilliseconds >= 0 &&
        EndsAtMilliseconds > StartedAtMilliseconds &&
        nowMilliseconds >= StartedAtMilliseconds &&
        nowMilliseconds < EndsAtMilliseconds;
}

public readonly record struct AllyRescueConfirmationState(
    AllyRescuePendingAttempt? Pending,
    ImmutableArray<AllyRescueConfirmationKey> ConfirmedKeys,
    AllyRescueConfirmationStatistics SessionStatistics,
    AllyRescueConfirmationStatistics MatchStatistics,
    AllyRescueConfirmationPopup? Popup,
    long LastObservedAtMilliseconds)
{
    public static AllyRescueConfirmationState Initial => new(
        null,
        ImmutableArray<AllyRescueConfirmationKey>.Empty,
        AllyRescueConfirmationStatistics.Empty,
        AllyRescueConfirmationStatistics.Empty,
        null,
        -1);
}

public readonly record struct AllyRescueConfirmationDecision(
    AllyRescueConfirmationState NextState,
    bool PendingRegistered,
    bool Confirmed,
    bool Duplicate,
    AllyRescueConfirmationPopup? TriggeredPopup);

/// <summary>
/// Pure correlation rules for ally-rescue confirmation. Registration never
/// confirms a cleanse. Only an exact local-caster/action/target 0x10 effect
/// with one of the six removable PvP status IDs confirms and increments once.
/// </summary>
public static class AllyRescueConfirmationRules
{
    public const uint WardensPaeanActionId = 29400;
    public const uint AquaveilActionId = 29227;
    public const byte RecoveredFromStatusEffectType = 0x10;
    public const long DefaultCorrelationMilliseconds = 2_500;
    public const long PopupDurationMilliseconds = 1_500;
    public const int MaximumConfirmedKeys = 128;

    public const uint StunStatusId = 1343;
    public const uint HeavyStatusId = 1344;
    public const uint BindStatusId = 1345;
    public const uint SilenceStatusId = 1347;
    public const uint MiracleOfNatureStatusId = 3085;
    public const uint DeepFreezeStatusId = 3219;

    public static AllyRescueConfirmationDecision RegisterAttempt(
        AllyRescueConfirmationState previous,
        AllyRescuePendingAttempt attempt,
        long nowMilliseconds,
        bool hardReset = false)
    {
        previous = Normalize(previous);
        if (hardReset)
            return NoDecision(ResetMatch(previous, nowMilliseconds));
        if (!IsMonotonic(previous, nowMilliseconds))
        {
            return NoDecision(ClearPending(previous, nowMilliseconds));
        }

        // A local false return is a rejected request, not correlation evidence.
        // In particular it must never overwrite an earlier accepted request
        // which may still produce the exact server ActionEffect packet.
        if (!attempt.UseActionAccepted)
        {
            return NoDecision(previous with
            {
                Pending = PendingInsideWindow(
                    previous.Pending,
                    nowMilliseconds,
                    DefaultCorrelationMilliseconds),
                Popup = ActivePopup(previous.Popup, nowMilliseconds),
                LastObservedAtMilliseconds = nowMilliseconds,
            });
        }

        if (!attempt.IsValid ||
            attempt.AttemptedAtMilliseconds != nowMilliseconds)
        {
            return NoDecision(ClearPending(previous, nowMilliseconds));
        }

        return new AllyRescueConfirmationDecision(
            previous with
            {
                Pending = attempt,
                Popup = ActivePopup(previous.Popup, nowMilliseconds),
                LastObservedAtMilliseconds = nowMilliseconds,
            },
            PendingRegistered: true,
            Confirmed: false,
            Duplicate: false,
            TriggeredPopup: null);
    }

    public static AllyRescueConfirmationDecision ObserveActionEffect(
        AllyRescueConfirmationState previous,
        AllyRescueActionEffectObservation observation,
        long correlationMilliseconds = DefaultCorrelationMilliseconds,
        bool hardReset = false)
    {
        previous = Normalize(previous);
        correlationMilliseconds = Math.Max(0, correlationMilliseconds);
        if (hardReset)
            return NoDecision(ResetMatch(previous, observation.ObservedAtMilliseconds));
        if (!IsMonotonic(previous, observation.ObservedAtMilliseconds))
            return NoDecision(ClearPending(previous, observation.ObservedAtMilliseconds));

        var now = observation.ObservedAtMilliseconds;
        var popup = ActivePopup(previous.Popup, now);
        if (previous.Pending is not { } pending ||
            !Matches(pending, observation, correlationMilliseconds))
        {
            return NoDecision(previous with
            {
                Pending = PendingInsideWindow(previous.Pending, now, correlationMilliseconds),
                Popup = popup,
                LastObservedAtMilliseconds = now,
            });
        }

        var key = new AllyRescueConfirmationKey(
            observation.CasterEntityId,
            observation.ActionId,
            observation.TargetEntityId,
            observation.GlobalSequence,
            observation.SourceSequence,
            observation.EffectValue);
        if (previous.ConfirmedKeys.Contains(key))
        {
            return new AllyRescueConfirmationDecision(
                previous with
                {
                    Pending = null,
                    Popup = popup,
                    LastObservedAtMilliseconds = now,
                },
                PendingRegistered: false,
                Confirmed: false,
                Duplicate: true,
                TriggeredPopup: null);
        }

        var confirmedKeys = AppendBounded(previous.ConfirmedKeys, key);
        var sessionStatistics = IncrementStatistics(
            previous.SessionStatistics,
            observation.ActionId,
            observation.EffectValue);
        var matchStatistics = IncrementStatistics(
            previous.MatchStatistics,
            observation.ActionId,
            observation.EffectValue);
        var triggeredPopup = new AllyRescueConfirmationPopup(
            observation.ActionId,
            pending.TargetGameObjectId,
            observation.TargetEntityId,
            observation.EffectValue,
            now,
            SaturatingAdd(now, PopupDurationMilliseconds));
        var next = previous with
        {
            Pending = null,
            ConfirmedKeys = confirmedKeys,
            SessionStatistics = sessionStatistics,
            MatchStatistics = matchStatistics,
            Popup = triggeredPopup,
            LastObservedAtMilliseconds = now,
        };
        return new AllyRescueConfirmationDecision(
            next,
            PendingRegistered: false,
            Confirmed: true,
            Duplicate: false,
            TriggeredPopup: triggeredPopup);
    }

    public static AllyRescueConfirmationState ObserveTime(
        AllyRescueConfirmationState previous,
        long nowMilliseconds,
        long correlationMilliseconds = DefaultCorrelationMilliseconds,
        bool hardReset = false)
    {
        previous = Normalize(previous);
        if (hardReset)
            return ResetMatch(previous, nowMilliseconds);
        if (!IsMonotonic(previous, nowMilliseconds))
            return ResetMatch(previous, nowMilliseconds);

        correlationMilliseconds = Math.Max(0, correlationMilliseconds);
        return previous with
        {
            Pending = PendingInsideWindow(previous.Pending, nowMilliseconds, correlationMilliseconds),
            Popup = ActivePopup(previous.Popup, nowMilliseconds),
            LastObservedAtMilliseconds = nowMilliseconds,
        };
    }

    public static bool IsRescueAction(uint actionId) =>
        actionId is WardensPaeanActionId or AquaveilActionId;

    /// <summary>
    /// Confirmation accepts all six statuses these actions may actually remove.
    /// Heavy and Bind remain excluded from AllyRescueStatusRules activation.
    /// </summary>
    public static bool IsConfirmableRemovedStatus(uint statusId) =>
        statusId is
            StunStatusId or
            HeavyStatusId or
            BindStatusId or
            SilenceStatusId or
            MiracleOfNatureStatusId or
            DeepFreezeStatusId;

    public static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    /// <summary>
    /// Explicit user-facing statistics reset. It deliberately leaves pending
    /// correlation and match dedupe intact so a reset cannot turn one server
    /// packet into a second confirmed cleanse. Any old visible popup is closed
    /// because its displayed match count has just been reset.
    /// </summary>
    public static AllyRescueConfirmationState ResetStatistics(
        AllyRescueConfirmationState previous)
    {
        previous = Normalize(previous);
        return previous with
        {
            SessionStatistics = AllyRescueConfirmationStatistics.Empty,
            MatchStatistics = AllyRescueConfirmationStatistics.Empty,
            Popup = null,
        };
    }

    private static bool Matches(
        AllyRescuePendingAttempt pending,
        AllyRescueActionEffectObservation observation,
        long correlationMilliseconds)
    {
        if (observation.ObservedAtMilliseconds < pending.AttemptedAtMilliseconds ||
            observation.ObservedAtMilliseconds - pending.AttemptedAtMilliseconds > correlationMilliseconds)
        {
            return false;
        }

        return observation.CasterEntityId == pending.LocalCasterEntityId &&
               observation.ActionId == pending.ActionId &&
               observation.TargetEntityId == pending.TargetEntityId &&
               observation.EffectType == RecoveredFromStatusEffectType &&
               IsConfirmableRemovedStatus(observation.EffectValue) &&
               (observation.GlobalSequence != 0 || observation.SourceSequence != 0);
    }

    private static AllyRescuePendingAttempt? PendingInsideWindow(
        AllyRescuePendingAttempt? pending,
        long nowMilliseconds,
        long correlationMilliseconds) =>
        pending is { } value &&
        nowMilliseconds >= value.AttemptedAtMilliseconds &&
        nowMilliseconds - value.AttemptedAtMilliseconds <= correlationMilliseconds
            ? value
            : null;

    private static AllyRescueConfirmationPopup? ActivePopup(
        AllyRescueConfirmationPopup? popup,
        long nowMilliseconds) =>
        popup is { } value && value.IsVisible(nowMilliseconds) ? value : null;

    private static AllyRescueConfirmationStatistics IncrementStatistics(
        AllyRescueConfirmationStatistics previous,
        uint actionId,
        uint statusId) =>
        new(
            SaturatingIncrement(previous.TotalConfirmed),
            IncrementCount(previous.ByAction, actionId),
            IncrementCount(previous.ByStatus, statusId));

    private static ImmutableArray<AllyRescueConfirmationCount> IncrementCount(
        ImmutableArray<AllyRescueConfirmationCount> previous,
        uint id)
    {
        if (previous.IsDefault) previous = ImmutableArray<AllyRescueConfirmationCount>.Empty;
        var builder = previous.ToBuilder();
        for (var index = 0; index < builder.Count; index++)
        {
            if (builder[index].Id != id) continue;
            builder[index] = builder[index] with { Count = SaturatingIncrement(builder[index].Count) };
            return builder.ToImmutable();
        }

        builder.Add(new AllyRescueConfirmationCount(id, 1));
        builder.Sort(static (left, right) => left.Id.CompareTo(right.Id));
        return builder.ToImmutable();
    }

    private static ImmutableArray<AllyRescueConfirmationKey> AppendBounded(
        ImmutableArray<AllyRescueConfirmationKey> previous,
        AllyRescueConfirmationKey key)
    {
        if (previous.IsDefault) previous = ImmutableArray<AllyRescueConfirmationKey>.Empty;
        var skip = Math.Max(0, previous.Length - MaximumConfirmedKeys + 1);
        return previous.Skip(skip).Append(key).ToImmutableArray();
    }

    private static AllyRescueConfirmationState Normalize(AllyRescueConfirmationState state) =>
        state with
        {
            ConfirmedKeys = state.ConfirmedKeys.IsDefault
                ? ImmutableArray<AllyRescueConfirmationKey>.Empty
                : state.ConfirmedKeys,
            SessionStatistics = state.SessionStatistics ?? AllyRescueConfirmationStatistics.Empty,
            MatchStatistics = state.MatchStatistics ?? AllyRescueConfirmationStatistics.Empty,
        };

    private static AllyRescueConfirmationState ResetMatch(
        AllyRescueConfirmationState previous,
        long nowMilliseconds) =>
        AllyRescueConfirmationState.Initial with
        {
            SessionStatistics = previous.SessionStatistics,
            LastObservedAtMilliseconds = Math.Max(-1, nowMilliseconds),
        };

    private static bool IsMonotonic(
        AllyRescueConfirmationState state,
        long nowMilliseconds) =>
        nowMilliseconds >= 0 &&
        (state.LastObservedAtMilliseconds < 0 || nowMilliseconds >= state.LastObservedAtMilliseconds);

    private static AllyRescueConfirmationState ClearPending(
        AllyRescueConfirmationState previous,
        long nowMilliseconds) =>
        previous with
        {
            Pending = null,
            Popup = null,
            LastObservedAtMilliseconds = Math.Max(-1, nowMilliseconds),
        };

    private static AllyRescueConfirmationDecision NoDecision(
        AllyRescueConfirmationState state) =>
        new(state, false, false, false, null);

    private static long SaturatingIncrement(long value) =>
        value >= long.MaxValue ? long.MaxValue : value + 1;

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
