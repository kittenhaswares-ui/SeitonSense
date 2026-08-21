using System.Collections.Immutable;

namespace SeitonSense.Core;

public readonly record struct MiracleInterceptPendingAttempt(
    uint LocalCasterEntityId,
    uint ActionId,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    MiracleInterceptThreatKind Threat,
    bool UseActionAccepted,
    long AttemptedAtMilliseconds)
{
    public bool IsValid =>
        MiracleInterceptConfirmationRules.IsValidEntityId(LocalCasterEntityId) &&
        MiracleInterceptConfirmationRules.ExpectedStatusForAction(ActionId) != 0 &&
        TargetHighlightRules.IsValidGameObjectId(TargetGameObjectId) &&
        MiracleInterceptConfirmationRules.IsValidEntityId(TargetEntityId) &&
        (Threat is
            MiracleInterceptThreatKind.MarksmanSpite or
            MiracleInterceptThreatKind.Zantetsuken or
            MiracleInterceptThreatKind.FuriousBacklash or
            MiracleInterceptThreatKind.Contradance or
            MiracleInterceptThreatKind.PostPurifyCrowdControl or
            MiracleInterceptThreatKind.PostGuardCrowdControl) &&
        (Threat != MiracleInterceptThreatKind.PostPurifyCrowdControl ||
         MiracleCleanseFollowupRules.IsPurifyRemovableStatus(RemovedStatusId)) &&
        AttemptedAtMilliseconds >= 0;

    public uint RemovedStatusId { get; init; }
}

public readonly record struct MiracleInterceptLandedObservation(
    uint CasterEntityId,
    uint ActionId,
    uint TargetEntityId,
    byte EffectType,
    ushort EffectValue,
    uint GlobalSequence,
    ushort SourceSequence,
    long ObservedAtMilliseconds);

public readonly record struct MiracleInterceptConfirmationKey(
    uint CasterEntityId,
    uint ActionId,
    uint TargetEntityId,
    uint GlobalSequence,
    ushort SourceSequence);

public readonly record struct MiracleInterceptConfirmationPopup(
    uint ActionId,
    ulong TargetGameObjectId,
    uint TargetEntityId,
    MiracleInterceptThreatKind Threat,
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

public readonly record struct MiracleInterceptConfirmationState(
    MiracleInterceptPendingAttempt? Pending,
    ImmutableArray<MiracleInterceptConfirmationKey> ConfirmedKeys,
    MiracleInterceptConfirmationPopup? Popup,
    long TotalConfirmed,
    long LastObservedAtMilliseconds)
{
    public static MiracleInterceptConfirmationState Initial => new(
        null,
        ImmutableArray<MiracleInterceptConfirmationKey>.Empty,
        null,
        0,
        -1);
}

public readonly record struct MiracleInterceptConfirmationDecision(
    MiracleInterceptConfirmationState NextState,
    bool PendingRegistered,
    bool Confirmed,
    bool Duplicate,
    MiracleInterceptConfirmationPopup? TriggeredPopup);

/// <summary>
/// Correlates the sole native WHM Miracle, BRD Silent Nocturne, or NIN Raiju
/// attempt made by the reactive helper with the exact server status-add on the intended target.
/// This proves only that the counter-CC landed; it never claims the hostile
/// action was interrupted.
/// </summary>
public static class MiracleInterceptConfirmationRules
{
    public const uint MiracleOfNatureActionId = 29_228;
    public const ushort MiracleOfNatureStatusId = 3_085;
    public const uint SilentNocturneActionId = 29_395;
    public const ushort SilenceStatusId = 1_347;
    public const uint ForkedRaijuActionId = 29_510;
    public const uint FleetingRaijuActionId = 29_707;
    public const ushort StunStatusId = 1_343;
    public const byte AddStatusEffectType = 0x0E;
    public const long CorrelationMilliseconds = 1_500;
    public const long PopupDurationMilliseconds = 1_500;
    public const int MaximumConfirmedKeys = 128;

    public static MiracleInterceptConfirmationDecision RegisterAttempt(
        MiracleInterceptConfirmationState previous,
        MiracleInterceptPendingAttempt attempt,
        long nowMilliseconds,
        bool hardReset = false)
    {
        previous = Normalize(previous);
        if (hardReset)
            return None(Reset(previous, nowMilliseconds));
        if (!IsMonotonic(previous, nowMilliseconds))
        {
            return None(ClearPending(previous, nowMilliseconds));
        }

        // Preserve the first still-correlatable native attempt. A second helper
        // attempt can occur before its ActionEffect reaches the client; replacing
        // it here could lose or mislabel the exact landing confirmation.
        if (PendingInsideWindow(previous.Pending, nowMilliseconds) is { } activePending)
        {
            return None(previous with
            {
                Pending = activePending,
                Popup = ActivePopup(previous.Popup, nowMilliseconds),
                LastObservedAtMilliseconds = nowMilliseconds,
            });
        }

        if (!attempt.IsValid || attempt.AttemptedAtMilliseconds != nowMilliseconds)
            return None(ClearPending(previous, nowMilliseconds));

        return new MiracleInterceptConfirmationDecision(
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

    public static MiracleInterceptConfirmationDecision ObserveActionEffect(
        MiracleInterceptConfirmationState previous,
        MiracleInterceptLandedObservation observation,
        bool hardReset = false)
    {
        previous = Normalize(previous);
        if (hardReset)
            return None(Reset(previous, observation.ObservedAtMilliseconds));
        if (!IsMonotonic(previous, observation.ObservedAtMilliseconds))
            return None(ClearPending(previous, observation.ObservedAtMilliseconds));

        var now = observation.ObservedAtMilliseconds;
        var popup = ActivePopup(previous.Popup, now);
        if (previous.Pending is not { } pending || !Matches(pending, observation))
        {
            return None(previous with
            {
                Pending = PendingInsideWindow(previous.Pending, now),
                Popup = popup,
                LastObservedAtMilliseconds = now,
            });
        }

        var key = new MiracleInterceptConfirmationKey(
            observation.CasterEntityId,
            observation.ActionId,
            observation.TargetEntityId,
            observation.GlobalSequence,
            observation.SourceSequence);
        if (previous.ConfirmedKeys.Contains(key))
        {
            return new MiracleInterceptConfirmationDecision(
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

        var triggeredPopup = new MiracleInterceptConfirmationPopup(
            pending.ActionId,
            pending.TargetGameObjectId,
            pending.TargetEntityId,
            pending.Threat,
            pending.RemovedStatusId,
            now,
            SaturatingAdd(now, PopupDurationMilliseconds));
        var next = previous with
        {
            Pending = null,
            ConfirmedKeys = AppendBounded(previous.ConfirmedKeys, key),
            Popup = triggeredPopup,
            TotalConfirmed = SaturatingIncrement(previous.TotalConfirmed),
            LastObservedAtMilliseconds = now,
        };
        return new MiracleInterceptConfirmationDecision(
            next,
            PendingRegistered: false,
            Confirmed: true,
            Duplicate: false,
            TriggeredPopup: triggeredPopup);
    }

    public static MiracleInterceptConfirmationState ObserveTime(
        MiracleInterceptConfirmationState previous,
        long nowMilliseconds,
        bool hardReset = false)
    {
        previous = Normalize(previous);
        if (hardReset || !IsMonotonic(previous, nowMilliseconds))
            return Reset(previous, nowMilliseconds);

        return previous with
        {
            Pending = PendingInsideWindow(previous.Pending, nowMilliseconds),
            Popup = ActivePopup(previous.Popup, nowMilliseconds),
            LastObservedAtMilliseconds = nowMilliseconds,
        };
    }

    public static bool IsValidEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000 and not uint.MaxValue;

    public static ushort ExpectedStatusForAction(uint actionId) =>
        actionId switch
        {
            MiracleOfNatureActionId => MiracleOfNatureStatusId,
            SilentNocturneActionId => SilenceStatusId,
            ForkedRaijuActionId or FleetingRaijuActionId => StunStatusId,
            _ => 0,
        };

    private static bool Matches(
        MiracleInterceptPendingAttempt pending,
        MiracleInterceptLandedObservation observation)
    {
        if (observation.ObservedAtMilliseconds < pending.AttemptedAtMilliseconds ||
            observation.ObservedAtMilliseconds - pending.AttemptedAtMilliseconds > CorrelationMilliseconds)
        {
            return false;
        }

        return observation.CasterEntityId == pending.LocalCasterEntityId &&
               observation.ActionId == pending.ActionId &&
               observation.TargetEntityId == pending.TargetEntityId &&
               observation.EffectType == AddStatusEffectType &&
               observation.EffectValue == ExpectedStatusForAction(pending.ActionId) &&
               (observation.GlobalSequence != 0 || observation.SourceSequence != 0);
    }

    private static MiracleInterceptPendingAttempt? PendingInsideWindow(
        MiracleInterceptPendingAttempt? pending,
        long nowMilliseconds) =>
        pending is { } value &&
        nowMilliseconds >= value.AttemptedAtMilliseconds &&
        nowMilliseconds - value.AttemptedAtMilliseconds <= CorrelationMilliseconds
            ? value
            : null;

    private static MiracleInterceptConfirmationPopup? ActivePopup(
        MiracleInterceptConfirmationPopup? popup,
        long nowMilliseconds) =>
        popup is { } value && value.IsVisible(nowMilliseconds) ? value : null;

    private static ImmutableArray<MiracleInterceptConfirmationKey> AppendBounded(
        ImmutableArray<MiracleInterceptConfirmationKey> previous,
        MiracleInterceptConfirmationKey key)
    {
        if (previous.IsDefault) previous = ImmutableArray<MiracleInterceptConfirmationKey>.Empty;
        var skip = Math.Max(0, previous.Length - MaximumConfirmedKeys + 1);
        return previous.Skip(skip).Append(key).ToImmutableArray();
    }

    private static MiracleInterceptConfirmationState Normalize(
        MiracleInterceptConfirmationState state) =>
        state with
        {
            ConfirmedKeys = state.ConfirmedKeys.IsDefault
                ? ImmutableArray<MiracleInterceptConfirmationKey>.Empty
                : state.ConfirmedKeys,
        };

    private static MiracleInterceptConfirmationState Reset(
        MiracleInterceptConfirmationState previous,
        long nowMilliseconds) =>
        MiracleInterceptConfirmationState.Initial with
        {
            TotalConfirmed = previous.TotalConfirmed,
            LastObservedAtMilliseconds = Math.Max(-1, nowMilliseconds),
        };

    private static bool IsMonotonic(
        MiracleInterceptConfirmationState state,
        long nowMilliseconds) =>
        nowMilliseconds >= 0 &&
        (state.LastObservedAtMilliseconds < 0 || nowMilliseconds >= state.LastObservedAtMilliseconds);

    private static MiracleInterceptConfirmationState ClearPending(
        MiracleInterceptConfirmationState previous,
        long nowMilliseconds) =>
        previous with
        {
            Pending = null,
            Popup = null,
            LastObservedAtMilliseconds = Math.Max(-1, nowMilliseconds),
        };

    private static MiracleInterceptConfirmationDecision None(
        MiracleInterceptConfirmationState state) =>
        new(state, false, false, false, null);

    private static long SaturatingIncrement(long value) =>
        value >= long.MaxValue ? long.MaxValue : value + 1;

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
