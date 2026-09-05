using System.Numerics;

namespace SeitonSense.Core;

public readonly record struct AggressorArrowObservation(
    TargetPressureActorIdentity Actor,
    TargetPressureSources Sources,
    long LastHarmfulActionAtMilliseconds,
    bool IsEligible);

public readonly record struct AggressorArrowPulse(
    TargetPressureActorIdentity Actor,
    long StartedAtMilliseconds);

/// <summary>Pure, display-only bounds shared by the pressure arrow tracker and renderer.</summary>
public static class AggressorArrowRules
{
    public const float LegacyDefaultDurationSeconds = .75f;
    public const float DefaultDurationSeconds = 2f;
    public const float MinimumDurationSeconds = .35f;
    public const float MaximumDurationSeconds = 4f;
    public const long MaximumSnapshotAgeMilliseconds = 500;
    public const long MaximumPulseRetentionMilliseconds = 4_500;
    public const float MinimumProjectedLengthPixels = 8;
    public const float DefaultOverallScale = 1.35f;
    public const float MinimumOverallScale = 0.75f;
    public const float MaximumOverallScale = 3f;

    public static float MigrateLegacyDuration(float duration) =>
        !float.IsFinite(duration) || duration == LegacyDefaultDurationSeconds
            ? DefaultDurationSeconds : duration;

    public static float ResolveVisualScale(float uiScale, float overallScale) =>
        Math.Clamp(float.IsFinite(uiScale) ? uiScale : 1f, 0.75f, 2f) *
        Math.Clamp(float.IsFinite(overallScale) ? overallScale : DefaultOverallScale,
            MinimumOverallScale, MaximumOverallScale);

    public static float PulseAlpha(long start, long now, float duration, bool reducedMotion)
    {
        if (start < 0 || now < start || !float.IsFinite(duration) || duration <= 0)
            return 0;

        var lifetime = Math.Clamp(duration, MinimumDurationSeconds, MaximumDurationSeconds) * 1_000d;
        var age = now - start;
        if (age >= lifetime) return 0;

        // Reduced motion uses a steady emphasis rather than a changing pulse.
        return reducedMotion ? .8f : (float)(1d - age / lifetime);
    }

    public static bool IsValidProjectedSegment(
        Vector2 from,
        Vector2 to,
        Vector2 viewportMin,
        Vector2 viewportMax)
    {
        if (!IsFinite(from) || !IsFinite(to) || !IsFinite(viewportMin) || !IsFinite(viewportMax) ||
            viewportMax.X <= viewportMin.X || viewportMax.Y <= viewportMin.Y ||
            !IsInside(from, viewportMin, viewportMax) || !IsInside(to, viewportMin, viewportMax))
        {
            return false;
        }

        var lengthSquared = Vector2.DistanceSquared(from, to);
        return float.IsFinite(lengthSquared) &&
               lengthSquared >= MinimumProjectedLengthPixels * MinimumProjectedLengthPixels;
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool IsInside(Vector2 point, Vector2 min, Vector2 max) =>
        point.X >= min.X && point.Y >= min.Y && point.X <= max.X && point.Y <= max.Y;
}

/// <summary>
/// Tracks direct hard/cast acquisition separately from recent harmful evidence in
/// complete, fresh enemy publications. The first publication is a silent baseline;
/// new identities may signal later, while returning or reused IDs baseline silently.
/// No targeting, native calls, wall clock, or gameplay state changes are performed.
/// </summary>
public sealed class AggressorArrowTracker
{
    private const TargetPressureSources DirectSources =
        TargetPressureSources.HardTarget | TargetPressureSources.CastTarget;
    private const TargetPressureSources KnownSources = DirectSources |
        TargetPressureSources.RecentHarmfulAction | TargetPressureSources.MachinistLimitBreakEarlyMarker;

    private readonly Dictionary<TargetPressureActorIdentity, ActorState> actors = new();
    private readonly Dictionary<TargetPressureActorIdentity, AggressorArrowPulse> pulses = new();
    private readonly HashSet<ulong> knownGameObjectIds = new();
    private readonly HashSet<uint> knownEntityIds = new();
    private TargetPressureActorIdentity localPlayer;
    private long lastObservedAt = -1;

    public IReadOnlyList<AggressorArrowPulse> Observe(
        bool enabled,
        TargetPressureActorIdentity local,
        long now,
        IReadOnlyList<AggressorArrowObservation> observations)
    {
        if (!enabled || !local.IsValid || now < 0 || observations is null)
        {
            Reset();
            return Array.Empty<AggressorArrowPulse>();
        }

        if (lastObservedAt < 0 || local != localPlayer || now < lastObservedAt ||
            now - lastObservedAt > AggressorArrowRules.MaximumSnapshotAgeMilliseconds)
        {
            Reset();
        }

        var isInitialPublication = lastObservedAt < 0;
        localPlayer = local;
        lastObservedAt = now;

        // Any reused half-ID or duplicate entry makes that actor ambiguous. Do
        // not merge contradictory eligibility or carry ownership through it.
        var gameIds = new Dictionary<ulong, int>();
        var entityIds = new Dictionary<uint, int>();
        foreach (var observation in observations)
        {
            gameIds[observation.Actor.GameObjectId] =
                gameIds.GetValueOrDefault(observation.Actor.GameObjectId) + 1;
            entityIds[observation.Actor.EntityId] =
                entityIds.GetValueOrDefault(observation.Actor.EntityId) + 1;
        }

        var seen = new HashSet<TargetPressureActorIdentity>();
        foreach (var observation in observations)
        {
            var actor = observation.Actor;
            if (!observation.IsEligible || !actor.IsValid ||
                actor.GameObjectId == local.GameObjectId || actor.EntityId == local.EntityId ||
                gameIds[actor.GameObjectId] != 1 || entityIds[actor.EntityId] != 1 ||
                (observation.Sources & ~KnownSources) != 0)
            {
                continue;
            }

            seen.Add(actor);
            var direct = (observation.Sources & DirectSources) != 0;
            var harmfulAt = observation.LastHarmfulActionAtMilliseconds;
            var validHarmfulTime = harmfulAt >= 0 && harmfulAt <= now;
            var recent = (observation.Sources & TargetPressureSources.RecentHarmfulAction) != 0 &&
                         validHarmfulTime;
            var active = direct || recent;
            var freshHarmful = recent &&
                now - harmfulAt <= AggressorArrowRules.MaximumSnapshotAgeMilliseconds;

            if (actors.TryGetValue(actor, out var previous))
            {
                // An actual retarget matters even while the previous attack's
                // recent-harmful flag is still retained by the pressure window.
                // Hard-to-cast handovers remain one continuous direct episode.
                var directOnset = direct && !previous.DirectActive;
                var harmfulOnset = !previous.Active && freshHarmful &&
                                   harmfulAt > previous.LastHarmfulAt;
                if (directOnset || harmfulOnset)
                    pulses[actor] = new AggressorArrowPulse(actor, now);

                actors[actor] = new ActorState(direct, active,
                    validHarmfulTime ? Math.Max(previous.LastHarmfulAt, harmfulAt) : previous.LastHarmfulAt);
            }
            else
            {
                // Neither half-ID may have appeared earlier in this context.
                // This distinguishes a genuinely new arrival from respawn,
                // temporary absence, an ambiguous publication, or ID reuse.
                var brandNew = !knownGameObjectIds.Contains(actor.GameObjectId) &&
                               !knownEntityIds.Contains(actor.EntityId);
                if (!isInitialPublication && brandNew && (direct || freshHarmful))
                    pulses[actor] = new AggressorArrowPulse(actor, now);

                actors[actor] = new ActorState(direct, active, validHarmfulTime ? harmfulAt : -1);
            }
        }

        // Retain only identity tombstones, not absent actors or their pulses.
        // Even invalid/conflicting observations reserve their valid half-IDs so
        // a later clean publication cannot turn ambiguity into a new-arrival cue.
        foreach (var observation in observations)
        {
            if (observation.Actor.GameObjectId is not (0 or 0xE0000000UL))
                knownGameObjectIds.Add(observation.Actor.GameObjectId);
            if (observation.Actor.EntityId is not (0 or 0xE0000000u))
                knownEntityIds.Add(observation.Actor.EntityId);
        }

        foreach (var actor in actors.Keys.Where(actor => !seen.Contains(actor)).ToArray())
            actors.Remove(actor);
        foreach (var actor in pulses.Keys.Where(actor => !seen.Contains(actor) ||
                     now - pulses[actor].StartedAtMilliseconds >=
                     AggressorArrowRules.MaximumPulseRetentionMilliseconds).ToArray())
            pulses.Remove(actor);

        return pulses.Values.OrderBy(pulse => pulse.Actor.GameObjectId)
            .ThenBy(pulse => pulse.Actor.EntityId).ToArray();
    }

    public void Reset()
    {
        actors.Clear();
        pulses.Clear();
        knownGameObjectIds.Clear();
        knownEntityIds.Clear();
        localPlayer = default;
        lastObservedAt = -1;
    }

    private readonly record struct ActorState(bool DirectActive, bool Active, long LastHarmfulAt);
}
