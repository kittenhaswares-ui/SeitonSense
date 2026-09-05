namespace SeitonSense.Core;

public readonly record struct AllyTargetArrowObservation(
    TargetPressureActorIdentity Ally,
    TargetPressureActorIdentity? HostileTarget,
    bool IsEligible);

public readonly record struct AllyTargetArrowPulse(
    TargetPressureActorIdentity Ally,
    TargetPressureActorIdentity Target,
    long StartedAtMilliseconds);

/// <summary>
/// One blue cue when an ally acquires a different, caller-proven hostile target.
/// Uses existing team-target observations only: no action inference or dispatch.
/// Initial, stale, returning, dead, and ambiguous actors baseline silently.
/// </summary>
public sealed class AllyTargetArrowTracker
{
    private readonly Dictionary<TargetPressureActorIdentity, TargetPressureActorIdentity?> targets = new();
    private readonly Dictionary<TargetPressureActorIdentity, AllyTargetArrowPulse> pulses = new();
    private readonly HashSet<ulong> knownGameIds = new();
    private readonly HashSet<uint> knownEntityIds = new();
    private TargetPressureActorIdentity localPlayer;
    private long lastObservedAt = -1;

    public IReadOnlyList<AllyTargetArrowPulse> Observe(bool enabled,
        TargetPressureActorIdentity local, long now,
        IReadOnlyList<AllyTargetArrowObservation> observations)
    {
        if (!enabled || !local.IsValid || now < 0 || observations is null)
        {
            Reset();
            return [];
        }
        if (lastObservedAt < 0 || localPlayer != local || now < lastObservedAt ||
            now - lastObservedAt > AggressorArrowRules.MaximumSnapshotAgeMilliseconds)
            Reset();
        var baseline = lastObservedAt < 0;
        localPlayer = local;
        lastObservedAt = now;
        var gameCounts = observations.GroupBy(o => o.Ally.GameObjectId).ToDictionary(g => g.Key, g => g.Count());
        var entityCounts = observations.GroupBy(o => o.Ally.EntityId).ToDictionary(g => g.Key, g => g.Count());
        var seen = new HashSet<TargetPressureActorIdentity>();
        foreach (var observation in observations)
        {
            var ally = observation.Ally;
            if (!observation.IsEligible || !ally.IsValid || SharesId(ally, local) ||
                gameCounts[ally.GameObjectId] != 1 || entityCounts[ally.EntityId] != 1)
                continue;
            var target = observation.HostileTarget;
            if (target is { } value &&
                (!value.IsValid || SharesId(value, ally) || SharesId(value, local)))
                target = null;
            seen.Add(ally);
            var hadPrevious = targets.TryGetValue(ally, out var previous);
            var brandNew = !knownGameIds.Contains(ally.GameObjectId) &&
                           !knownEntityIds.Contains(ally.EntityId);
            var reusedTargetId = previous is { } oldTarget && target is { } nextTarget &&
                                 oldTarget != nextTarget && SharesId(oldTarget, nextTarget);
            if (!baseline && target is { } hostile && !reusedTargetId &&
                ((hadPrevious && previous != target) || (!hadPrevious && brandNew)))
                pulses[ally] = new AllyTargetArrowPulse(ally, hostile, now);
            targets[ally] = target;
            if (pulses.TryGetValue(ally, out var pulse) &&
                (target != pulse.Target || reusedTargetId ||
                 now - pulse.StartedAtMilliseconds >= AggressorArrowRules.MaximumPulseRetentionMilliseconds))
                pulses.Remove(ally);
        }
        foreach (var observation in observations)
        {
            if (observation.Ally.GameObjectId is not (0 or 0xE0000000UL or ulong.MaxValue))
                knownGameIds.Add(observation.Ally.GameObjectId);
            if (observation.Ally.EntityId is not (0 or 0xE0000000u or uint.MaxValue))
                knownEntityIds.Add(observation.Ally.EntityId);
        }
        foreach (var ally in targets.Keys.Where(ally => !seen.Contains(ally)).ToArray())
        {
            targets.Remove(ally);
            pulses.Remove(ally);
        }
        return pulses.Values.OrderBy(p => p.Ally.GameObjectId).ThenBy(p => p.Ally.EntityId).ToArray();
    }

    public void Reset()
    {
        targets.Clear();
        pulses.Clear();
        knownGameIds.Clear();
        knownEntityIds.Clear();
        localPlayer = default;
        lastObservedAt = -1;
    }

    private static bool SharesId(TargetPressureActorIdentity a, TargetPressureActorIdentity b) =>
        a.GameObjectId == b.GameObjectId || a.EntityId == b.EntityId;
}
