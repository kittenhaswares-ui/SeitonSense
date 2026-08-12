namespace SeitonSense.Core;

[Flags]
public enum TargetPressureSources : byte
{
    None = 0,
    HardTarget = 1 << 0,
    CastTarget = 1 << 1,
    RecentHarmfulAction = 1 << 2,
    MachinistLimitBreakEarlyMarker = 1 << 3,
}

/// <summary>
/// Identifies one currently resolved actor with both of the independent IDs
/// available to the game client. Equality is intentionally exact: a match on
/// only one half is treated as stale or ambiguous input.
/// </summary>
public readonly record struct TargetPressureActorIdentity(
    ulong GameObjectId,
    uint EntityId)
{
    public bool IsValid => TargetPressureSnapshot.IsValidIdentity(this);
}

public readonly record struct TargetPressureEnemyObservation(
    TargetPressureActorIdentity Actor,
    TargetPressureActorIdentity? HardTarget,
    TargetPressureActorIdentity? CastTarget,
    uint JobId,
    int CcEnemySlot,
    bool IsHostile,
    bool IsDead,
    bool IsTargetable);

/// <summary>
/// A short-lived, already validated event source. Only event-derived source
/// flags are accepted here; hard- and cast-target state must come from the
/// corresponding enemy observation.
/// </summary>
public readonly record struct TargetPressureSignal(
    TargetPressureActorIdentity Actor,
    TargetPressureSources Sources);

public readonly record struct TargetPressureAllyObservation(
    TargetPressureActorIdentity Actor,
    TargetPressureActorIdentity? HardTarget,
    bool IsAlly,
    bool IsDead,
    bool IsTargetable);

public readonly record struct TargetPressureOpponent(
    TargetPressureActorIdentity Actor,
    uint JobId,
    int? CcEnemySlot,
    TargetPressureSources Sources,
    int AllyTargetCount)
{
    public bool HasSource(TargetPressureSources source) =>
        source != TargetPressureSources.None && (Sources & source) == source;
}

public readonly record struct TargetPressureAllyTargetCount(
    TargetPressureActorIdentity Enemy,
    int AllyTargetCount);

/// <summary>
/// Produces a read-only, deterministic union of enemies currently pressuring
/// the local player. It does not acquire targets or retain time-based state;
/// callers own the lifetime of recent action signals.
/// </summary>
public sealed class TargetPressureSnapshot
{
    private const ulong InvalidGameObjectId = 0xE0000000UL;
    private const uint InvalidEntityId = 0xE0000000u;
    private const uint MachinistJobId = 31;
    private const TargetPressureSources AllowedSignalSources =
        TargetPressureSources.RecentHarmfulAction |
        TargetPressureSources.MachinistLimitBreakEarlyMarker;

    private readonly IReadOnlyDictionary<TargetPressureActorIdentity, int> allyTargetCountByEnemy;

    private TargetPressureSnapshot(
        TargetPressureOpponent[] opponents,
        TargetPressureAllyTargetCount[] allyTargetCounts,
        IReadOnlyDictionary<TargetPressureActorIdentity, int> allyTargetCountByEnemy)
    {
        Opponents = opponents;
        AllyTargetCounts = allyTargetCounts;
        this.allyTargetCountByEnemy = allyTargetCountByEnemy;
    }

    public static TargetPressureSnapshot Empty { get; } = new(
        [],
        [],
        new Dictionary<TargetPressureActorIdentity, int>());

    public IReadOnlyList<TargetPressureOpponent> Opponents { get; }

    /// <summary>
    /// Exact enemy identities currently hard-targeted by one or more validated
    /// allies. This may contain an enemy that is not itself pressuring the local
    /// player, allowing Near Assist to reuse the same team-pressure view.
    /// </summary>
    public IReadOnlyList<TargetPressureAllyTargetCount> AllyTargetCounts { get; }

    public int Count => Opponents.Count;

    public int HardTargetCount => CountWithSource(TargetPressureSources.HardTarget);

    public int CastTargetCount => CountWithSource(TargetPressureSources.CastTarget);

    public int RecentHarmfulActionCount => CountWithSource(TargetPressureSources.RecentHarmfulAction);

    public int MachinistLimitBreakCount => CountWithSource(TargetPressureSources.MachinistLimitBreakEarlyMarker);

    public int GetAllyTargetCount(TargetPressureActorIdentity enemy) =>
        enemy.IsValid && allyTargetCountByEnemy.TryGetValue(enemy, out var count)
            ? count
            : 0;

    public bool TryGetOpponent(
        TargetPressureActorIdentity actor,
        out TargetPressureOpponent opponent)
    {
        if (actor.IsValid)
        {
            foreach (var candidate in Opponents)
            {
                if (candidate.Actor != actor) continue;
                opponent = candidate;
                return true;
            }
        }

        opponent = default;
        return false;
    }

    public static TargetPressureSnapshot Build(
        TargetPressureActorIdentity localPlayer,
        IEnumerable<TargetPressureEnemyObservation> enemies,
        IEnumerable<TargetPressureSignal>? recentSignals = null,
        IEnumerable<TargetPressureAllyObservation>? allies = null)
    {
        ArgumentNullException.ThrowIfNull(enemies);
        if (!localPlayer.IsValid) return Empty;

        var eligibleEnemies = enemies
            .Where(enemy => IsEligibleEnemy(localPlayer, enemy))
            .ToArray();
        if (eligibleEnemies.Length == 0) return Empty;

        var ambiguousEnemyIdentities = FindAmbiguousIdentities(
            eligibleEnemies.Select(enemy => enemy.Actor));
        var aggregates = new Dictionary<TargetPressureActorIdentity, EnemyAggregate>();
        foreach (var observation in eligibleEnemies)
        {
            if (ambiguousEnemyIdentities.Contains(observation.Actor)) continue;

            if (!aggregates.TryGetValue(observation.Actor, out var aggregate))
            {
                aggregate = new EnemyAggregate(observation.Actor);
                aggregates.Add(observation.Actor, aggregate);
            }

            aggregate.MergeMetadata(observation.JobId, observation.CcEnemySlot);
            if (observation.HardTarget == localPlayer)
                aggregate.Sources |= TargetPressureSources.HardTarget;
            if (observation.CastTarget == localPlayer)
                aggregate.Sources |= TargetPressureSources.CastTarget;
        }

        if (aggregates.Count == 0) return Empty;

        MergeRecentSignals(aggregates, recentSignals);
        ClearAmbiguousCcSlots(aggregates.Values);

        var allyTargetCounts = CountAllyHardTargets(
            localPlayer,
            aggregates,
            allies);
        var orderedEnemies = OrderDeterministically(aggregates.Values).ToArray();

        var opponents = orderedEnemies
            .Where(enemy => enemy.Sources != TargetPressureSources.None)
            .Select(enemy => new TargetPressureOpponent(
                enemy.Actor,
                enemy.JobId,
                enemy.CcEnemySlot,
                enemy.Sources,
                allyTargetCounts.GetValueOrDefault(enemy.Actor)))
            .ToArray();
        var orderedAllyTargetCounts = orderedEnemies
            .Where(enemy => allyTargetCounts.GetValueOrDefault(enemy.Actor) > 0)
            .Select(enemy => new TargetPressureAllyTargetCount(
                enemy.Actor,
                allyTargetCounts[enemy.Actor]))
            .ToArray();

        return opponents.Length == 0 && orderedAllyTargetCounts.Length == 0
            ? Empty
            : new TargetPressureSnapshot(
                opponents,
                orderedAllyTargetCounts,
                new Dictionary<TargetPressureActorIdentity, int>(allyTargetCounts));
    }

    public static bool IsValidIdentity(TargetPressureActorIdentity identity) =>
        identity.GameObjectId is not 0 and not InvalidGameObjectId and not ulong.MaxValue &&
        identity.EntityId is not 0 and not InvalidEntityId and not uint.MaxValue;

    private int CountWithSource(TargetPressureSources source)
    {
        var count = 0;
        foreach (var opponent in Opponents)
        {
            if (opponent.HasSource(source)) count++;
        }

        return count;
    }

    private static bool IsEligibleEnemy(
        TargetPressureActorIdentity localPlayer,
        TargetPressureEnemyObservation enemy) =>
        enemy.Actor.IsValid &&
        !SharesEitherId(enemy.Actor, localPlayer) &&
        enemy.IsHostile &&
        !enemy.IsDead &&
        enemy.IsTargetable;

    private static bool IsEligibleAlly(
        TargetPressureActorIdentity localPlayer,
        TargetPressureAllyObservation ally) =>
        ally.Actor.IsValid &&
        !SharesEitherId(ally.Actor, localPlayer) &&
        ally.IsAlly &&
        !ally.IsDead &&
        ally.IsTargetable &&
        ally.HardTarget is { } hardTarget &&
        hardTarget.IsValid;

    private static bool SharesEitherId(
        TargetPressureActorIdentity left,
        TargetPressureActorIdentity right) =>
        left.GameObjectId == right.GameObjectId || left.EntityId == right.EntityId;

    private static HashSet<TargetPressureActorIdentity> FindAmbiguousIdentities(
        IEnumerable<TargetPressureActorIdentity> identities)
    {
        var materialized = identities.Distinct().ToArray();
        var ambiguous = new HashSet<TargetPressureActorIdentity>();

        foreach (var group in materialized.GroupBy(identity => identity.GameObjectId))
        {
            if (group.Select(identity => identity.EntityId).Distinct().Skip(1).Any())
                ambiguous.UnionWith(group);
        }

        foreach (var group in materialized.GroupBy(identity => identity.EntityId))
        {
            if (group.Select(identity => identity.GameObjectId).Distinct().Skip(1).Any())
                ambiguous.UnionWith(group);
        }

        return ambiguous;
    }

    private static void MergeRecentSignals(
        IReadOnlyDictionary<TargetPressureActorIdentity, EnemyAggregate> aggregates,
        IEnumerable<TargetPressureSignal>? recentSignals)
    {
        if (recentSignals is null) return;

        foreach (var signal in recentSignals)
        {
            if (!signal.Actor.IsValid ||
                signal.Sources == TargetPressureSources.None ||
                (signal.Sources & ~AllowedSignalSources) != TargetPressureSources.None ||
                !aggregates.TryGetValue(signal.Actor, out var aggregate))
            {
                continue;
            }

            var sources = signal.Sources;
            if (aggregate.JobId != MachinistJobId)
                sources &= ~TargetPressureSources.MachinistLimitBreakEarlyMarker;

            aggregate.Sources |= sources;
        }
    }

    private static Dictionary<TargetPressureActorIdentity, int> CountAllyHardTargets(
        TargetPressureActorIdentity localPlayer,
        IReadOnlyDictionary<TargetPressureActorIdentity, EnemyAggregate> enemies,
        IEnumerable<TargetPressureAllyObservation>? allies)
    {
        var counts = new Dictionary<TargetPressureActorIdentity, int>();
        if (allies is null) return counts;

        var eligibleAllies = allies
            .Where(ally => IsEligibleAlly(localPlayer, ally))
            .Where(ally => !enemies.Keys.Any(enemy => SharesEitherId(ally.Actor, enemy)))
            .Where(ally => enemies.ContainsKey(ally.HardTarget!.Value))
            .ToArray();
        var ambiguousAllyIdentities = FindAmbiguousIdentities(
            eligibleAllies.Select(ally => ally.Actor));
        var exactTargetByAlly = new Dictionary<
            TargetPressureActorIdentity,
            TargetPressureActorIdentity>();
        var conflictingAllies = new HashSet<TargetPressureActorIdentity>();

        foreach (var ally in eligibleAllies)
        {
            if (ambiguousAllyIdentities.Contains(ally.Actor)) continue;
            var hardTarget = ally.HardTarget!.Value;
            if (exactTargetByAlly.TryGetValue(ally.Actor, out var existingTarget) &&
                existingTarget != hardTarget)
            {
                conflictingAllies.Add(ally.Actor);
                continue;
            }

            exactTargetByAlly[ally.Actor] = hardTarget;
        }

        foreach (var pair in exactTargetByAlly)
        {
            if (conflictingAllies.Contains(pair.Key)) continue;
            counts[pair.Value] = counts.GetValueOrDefault(pair.Value) + 1;
        }

        return counts;
    }

    private static void ClearAmbiguousCcSlots(IEnumerable<EnemyAggregate> enemies)
    {
        foreach (var group in enemies
                     .Where(enemy => enemy.CcEnemySlot.HasValue)
                     .GroupBy(enemy => enemy.CcEnemySlot!.Value))
        {
            if (!group.Skip(1).Any()) continue;
            foreach (var enemy in group) enemy.ClearCcEnemySlot();
        }
    }

    private static IOrderedEnumerable<EnemyAggregate> OrderDeterministically(
        IEnumerable<EnemyAggregate> enemies) =>
        enemies
            .OrderBy(enemy => enemy.CcEnemySlot.HasValue ? 0 : 1)
            .ThenBy(enemy => enemy.CcEnemySlot ?? int.MaxValue)
            .ThenBy(enemy => enemy.Actor.EntityId)
            .ThenBy(enemy => enemy.Actor.GameObjectId);

    private sealed class EnemyAggregate
    {
        private bool jobConflicted;
        private bool ccEnemySlotConflicted;

        internal EnemyAggregate(TargetPressureActorIdentity actor)
        {
            Actor = actor;
        }

        internal TargetPressureActorIdentity Actor { get; }
        internal uint JobId { get; private set; }
        internal int? CcEnemySlot { get; private set; }
        internal TargetPressureSources Sources { get; set; }

        internal void MergeMetadata(uint jobId, int ccEnemySlot)
        {
            if (!jobConflicted && jobId != 0)
            {
                if (JobId == 0) JobId = jobId;
                else if (JobId != jobId)
                {
                    JobId = 0;
                    jobConflicted = true;
                }
            }

            if (ccEnemySlotConflicted || !EnemySlotRules.IsValidSlot(ccEnemySlot)) return;
            if (!CcEnemySlot.HasValue) CcEnemySlot = ccEnemySlot;
            else if (CcEnemySlot.Value != ccEnemySlot) ClearCcEnemySlot();
        }

        internal void ClearCcEnemySlot()
        {
            CcEnemySlot = null;
            ccEnemySlotConflicted = true;
        }
    }
}
