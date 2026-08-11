namespace SeitonSense.Core;

public enum PersonalDebuffAlertKind
{
    Warning = 0,
    CleanseUrgent = 1,
}

public readonly record struct PersonalDebuffObservation(
    uint StatusId,
    PersonalDebuffAlertKind Kind,
    long ExpiresAtMilliseconds);

public readonly record struct PersonalDebuffAlertState(
    uint StatusId,
    PersonalDebuffAlertKind Kind,
    long ExpiresAtMilliseconds,
    long MissingObservedAtMilliseconds);

public readonly record struct PersonalDebuffAlert(
    uint StatusId,
    PersonalDebuffAlertKind Kind,
    long RemainingMilliseconds,
    bool TriggerEntryPulse);

public sealed record PersonalDebuffAlertDecision(
    PersonalDebuffAlertState[] NextStates,
    PersonalDebuffAlert[] Alerts)
{
    public static PersonalDebuffAlertDecision Empty => new([], []);
}

public static class PersonalDebuffAlertRules
{
    public const long MissingGraceMilliseconds = 150;

    public static PersonalDebuffAlertDecision Observe(
        IReadOnlyList<PersonalDebuffAlertState> previousStates,
        IReadOnlyList<PersonalDebuffObservation> observations,
        long nowMilliseconds,
        bool hardReset = false,
        long missingGraceMilliseconds = MissingGraceMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(previousStates);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentOutOfRangeException.ThrowIfNegative(missingGraceMilliseconds);

        if (hardReset)
            return PersonalDebuffAlertDecision.Empty;

        var previousByStatus = NormalizePreviousStates(previousStates);
        var observedByStatus = NormalizeObservations(observations, nowMilliseconds);
        var nextStates = new List<PersonalDebuffAlertState>(observedByStatus.Count + previousByStatus.Count);
        var alerts = new List<PersonalDebuffAlert>(observedByStatus.Count + previousByStatus.Count);

        foreach (var observation in observedByStatus.Values)
        {
            var hadPrevious = previousByStatus.TryGetValue(observation.StatusId, out var previous);
            var missingGapElapsed = hadPrevious &&
                                    previous.MissingObservedAtMilliseconds >= 0 &&
                                    (nowMilliseconds < previous.MissingObservedAtMilliseconds ||
                                     nowMilliseconds - previous.MissingObservedAtMilliseconds >= missingGraceMilliseconds);
            var previousExpired = hadPrevious && previous.ExpiresAtMilliseconds <= nowMilliseconds;
            var severityEscalated = hadPrevious && observation.Kind > previous.Kind;
            var triggerEntryPulse = !hadPrevious || missingGapElapsed || previousExpired || severityEscalated;

            var state = new PersonalDebuffAlertState(
                observation.StatusId,
                observation.Kind,
                observation.ExpiresAtMilliseconds,
                -1);
            nextStates.Add(state);
            alerts.Add(ToAlert(state, nowMilliseconds, triggerEntryPulse));
            previousByStatus.Remove(observation.StatusId);
        }

        foreach (var previous in previousByStatus.Values)
        {
            if (previous.ExpiresAtMilliseconds <= nowMilliseconds)
                continue;

            var missingSince = previous.MissingObservedAtMilliseconds;
            if (missingSince < 0 || nowMilliseconds < missingSince)
                missingSince = nowMilliseconds;

            if (nowMilliseconds - missingSince >= missingGraceMilliseconds)
                continue;

            var retained = previous with { MissingObservedAtMilliseconds = missingSince };
            nextStates.Add(retained);
            alerts.Add(ToAlert(retained, nowMilliseconds, false));
        }

        nextStates.Sort(static (left, right) => left.StatusId.CompareTo(right.StatusId));
        alerts.Sort(static (left, right) =>
        {
            var kind = right.Kind.CompareTo(left.Kind);
            if (kind != 0) return kind;

            var remaining = left.RemainingMilliseconds.CompareTo(right.RemainingMilliseconds);
            return remaining != 0 ? remaining : left.StatusId.CompareTo(right.StatusId);
        });

        return new PersonalDebuffAlertDecision(nextStates.ToArray(), alerts.ToArray());
    }

    private static Dictionary<uint, PersonalDebuffAlertState> NormalizePreviousStates(
        IReadOnlyList<PersonalDebuffAlertState> previousStates)
    {
        var normalized = new Dictionary<uint, PersonalDebuffAlertState>();
        foreach (var state in previousStates)
        {
            if (state.StatusId == 0 || !IsKnownKind(state.Kind))
                continue;

            if (!normalized.TryGetValue(state.StatusId, out var existing) ||
                state.Kind > existing.Kind ||
                (state.Kind == existing.Kind && state.ExpiresAtMilliseconds > existing.ExpiresAtMilliseconds))
            {
                normalized[state.StatusId] = state;
            }
        }

        return normalized;
    }

    private static Dictionary<uint, PersonalDebuffObservation> NormalizeObservations(
        IReadOnlyList<PersonalDebuffObservation> observations,
        long nowMilliseconds)
    {
        var normalized = new Dictionary<uint, PersonalDebuffObservation>();
        foreach (var observation in observations)
        {
            if (observation.StatusId == 0 ||
                !IsKnownKind(observation.Kind) ||
                observation.ExpiresAtMilliseconds <= nowMilliseconds)
            {
                continue;
            }

            if (!normalized.TryGetValue(observation.StatusId, out var existing) ||
                observation.Kind > existing.Kind ||
                (observation.Kind == existing.Kind &&
                 observation.ExpiresAtMilliseconds > existing.ExpiresAtMilliseconds))
            {
                normalized[observation.StatusId] = observation;
            }
        }

        return normalized;
    }

    private static PersonalDebuffAlert ToAlert(
        PersonalDebuffAlertState state,
        long nowMilliseconds,
        bool triggerEntryPulse) =>
        new(
            state.StatusId,
            state.Kind,
            Math.Max(0, state.ExpiresAtMilliseconds - nowMilliseconds),
            triggerEntryPulse);

    private static bool IsKnownKind(PersonalDebuffAlertKind kind) =>
        kind is PersonalDebuffAlertKind.Warning or PersonalDebuffAlertKind.CleanseUrgent;
}
