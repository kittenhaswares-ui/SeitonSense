namespace SeitonSense.Core;

public enum CombatFrameAvailability : byte
{
    Unknown = 0,
    Alive = 1,
    Dead = 2,
}

[Flags]
public enum CombatFrameIncomingEvidence : byte
{
    None = 0,
    HardTarget = 1 << 0,
    CastTarget = 1 << 1,
    RecentHarmfulAction = 1 << 2,
    LimitBreakMarker = 1 << 3,
}

/// <summary>
/// One immutable observation for a screen-space combat frame. Slot zero is the
/// exact local player; enemy observations use the canonical CC slots one to five.
/// </summary>
public readonly record struct CombatFrameObservation(
    int Slot,
    TargetPressureActorIdentity Actor,
    uint JobId,
    uint CurrentHp,
    uint MaximumHp,
    uint CurrentMp,
    uint MaximumMp,
    bool MpTrusted,
    bool IsDead,
    bool IsTargetable,
    bool PressureTrusted,
    bool IsCurrentTarget,
    bool IsFocusTarget,
    int DirectPressureCount,
    int TeamTargetCount,
    CombatFrameIncomingEvidence IncomingEvidence);

/// <summary>
/// Sanitized presentation data. Unknown telemetry stays unknown instead of
/// becoming a fabricated zero, while Slot remains stable for layout purposes.
/// </summary>
public readonly record struct CombatFramePlanRow(
    int Slot,
    CombatFrameAvailability Availability,
    TargetPressureActorIdentity Actor,
    uint JobId,
    uint CurrentHp,
    uint MaximumHp,
    uint CurrentMp,
    uint MaximumMp,
    bool MpTrusted,
    bool PressureTrusted,
    bool IsCurrentTarget,
    bool IsFocusTarget,
    int DirectPressureCount,
    int TeamTargetCount,
    CombatFrameIncomingEvidence IncomingEvidence)
{
    public bool HasTrustedHp =>
        Availability == CombatFrameAvailability.Alive &&
        MaximumHp > 0 &&
        CurrentHp > 0 &&
        CurrentHp <= MaximumHp;

    public bool HasTrustedMp =>
        Availability == CombatFrameAvailability.Alive &&
        MpTrusted &&
        MaximumMp == CombatFrameRules.ExpectedMaximumMp &&
        CurrentMp <= MaximumMp;

    public float HpFraction => CombatFrameRules.ResourceFraction(CurrentHp, MaximumHp, HasTrustedHp);
    public float MpFraction => CombatFrameRules.ResourceFraction(CurrentMp, MaximumMp, HasTrustedMp);
    public int AffordableRecuperates => CombatFrameRules.AffordableRecuperates(CurrentMp, MaximumMp, HasTrustedMp);
}

public static class CombatFrameRules
{
    public const int SelfSlot = 0;
    public const int FirstEnemySlot = 1;
    public const int LastEnemySlot = 5;
    public const int EnemySlotCount = LastEnemySlot - FirstEnemySlot + 1;
    public const uint ExpectedMaximumMp = 10_000;
    public const long DefaultMaximumSnapshotAgeMilliseconds = 500;

    private const CombatFrameIncomingEvidence AllowedIncomingEvidence =
        CombatFrameIncomingEvidence.HardTarget |
        CombatFrameIncomingEvidence.CastTarget |
        CombatFrameIncomingEvidence.RecentHarmfulAction |
        CombatFrameIncomingEvidence.LimitBreakMarker;

    public static CombatFramePlanRow BuildSelfRow(CombatFrameObservation? observation)
    {
        if (observation is not { Slot: SelfSlot } exact || !exact.Actor.IsValid)
            return Unknown(SelfSlot);

        return Sanitize(exact, requireTargetable: false);
    }

    /// <summary>
    /// Always returns exactly S1-S5. Duplicate slots, duplicate identities, or
    /// partial identity collisions blank the affected rows instead of borrowing
    /// another actor's data or reordering the layout.
    /// </summary>
    public static CombatFramePlanRow[] BuildEnemyRows(IEnumerable<CombatFrameObservation>? observations)
    {
        var result = CreateUnknownEnemyRows();
        if (observations is null) return result;

        var candidates = observations
            .Where(static observation =>
                observation.Slot is >= FirstEnemySlot and <= LastEnemySlot &&
                observation.Actor.IsValid)
            .ToArray();
        if (candidates.Length == 0) return result;

        var ambiguousActors = FindAmbiguousActors(candidates);
        foreach (var slotGroup in candidates.GroupBy(static observation => observation.Slot))
        {
            if (slotGroup.Count() != 1) continue;

            var observation = slotGroup.Single();
            if (ambiguousActors.Contains(observation.Actor)) continue;
            result[observation.Slot - FirstEnemySlot] = Sanitize(observation, requireTargetable: true);
        }

        return result;
    }

    public static CombatFramePlanRow[] CreateUnknownEnemyRows() =>
    [
        Unknown(1),
        Unknown(2),
        Unknown(3),
        Unknown(4),
        Unknown(5),
    ];

    public static bool IsSnapshotFresh(
        long publishedAtMilliseconds,
        long nowMilliseconds,
        long maximumAgeMilliseconds = DefaultMaximumSnapshotAgeMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumAgeMilliseconds);
        return publishedAtMilliseconds >= 0 &&
               nowMilliseconds >= publishedAtMilliseconds &&
               nowMilliseconds - publishedAtMilliseconds <= maximumAgeMilliseconds;
    }

    public static float ResourceFraction(uint current, uint maximum, bool trusted) =>
        trusted && maximum > 0 && current <= maximum
            ? Math.Clamp(current / (float)maximum, 0f, 1f)
            : 0f;

    /// <summary>
    /// Returns -1 when MP is unknown. Otherwise returns the exact number of
    /// currently affordable 2,000-MP Recuperates, bounded by the actor's maximum.
    /// </summary>
    public static int AffordableRecuperates(uint currentMp, uint maximumMp, bool trusted)
    {
        if (!trusted || maximumMp != ExpectedMaximumMp || currentMp > maximumMp) return -1;

        var maximumPips = ExpectedMaximumMp / (uint)LowMpRules.RecuperateCost;
        return (int)Math.Min(currentMp / (uint)LowMpRules.RecuperateCost, maximumPips);
    }

    /// <summary>
    /// Advances the zero-MP trust latch only from exact PvP resource telemetry.
    /// An invalid maximum or clock sample clears prior trust so a later zero
    /// cannot inherit credibility from corrupt/sentinel data.
    /// </summary>
    public static bool AdvanceMpTrust(
        LowMpState state,
        uint currentMp,
        uint maximumMp,
        long nowMilliseconds,
        out LowMpState nextState)
    {
        if (nowMilliseconds < 0 ||
            maximumMp != ExpectedMaximumMp ||
            currentMp > maximumMp)
        {
            nextState = LowMpState.Initial;
            return false;
        }

        var trustedSample = currentMp > 0 || state.HasTrustedSample;
        nextState = LowMpRules.Observe(
            state,
            (int)currentMp,
            trustedSample,
            nowMilliseconds,
            debounceMilliseconds: 0);
        return nextState.HasTrustedSample;
    }

    private static CombatFramePlanRow Sanitize(
        CombatFrameObservation observation,
        bool requireTargetable)
    {
        var dead = observation.IsDead ||
                   observation.CurrentHp == 0 && observation.MaximumHp > 0;
        if (dead)
        {
            return new CombatFramePlanRow(
                observation.Slot,
                CombatFrameAvailability.Dead,
                observation.Actor,
                observation.JobId,
                0,
                observation.MaximumHp,
                0,
                0,
                false,
                false,
                false,
                false,
                0,
                0,
                CombatFrameIncomingEvidence.None);
        }

        var validHp = observation.MaximumHp > 0 &&
                      observation.CurrentHp > 0 &&
                      observation.CurrentHp <= observation.MaximumHp;
        if (!validHp || requireTargetable && !observation.IsTargetable)
            return Unknown(observation.Slot);

        var trustedMp = observation.MpTrusted &&
                        observation.MaximumMp == ExpectedMaximumMp &&
                        observation.CurrentMp <= observation.MaximumMp;
        return new CombatFramePlanRow(
            observation.Slot,
            CombatFrameAvailability.Alive,
            observation.Actor,
            observation.JobId,
            observation.CurrentHp,
            observation.MaximumHp,
            trustedMp ? observation.CurrentMp : 0,
            trustedMp ? observation.MaximumMp : 0,
            trustedMp,
            observation.PressureTrusted,
            observation.IsCurrentTarget,
            observation.IsFocusTarget,
            observation.PressureTrusted
                ? Math.Clamp(observation.DirectPressureCount, 0, EnemySlotCount)
                : 0,
            observation.PressureTrusted
                ? Math.Clamp(observation.TeamTargetCount, 0, EnemySlotCount)
                : 0,
            observation.PressureTrusted
                ? observation.IncomingEvidence & AllowedIncomingEvidence
                : CombatFrameIncomingEvidence.None);
    }

    private static HashSet<TargetPressureActorIdentity> FindAmbiguousActors(
        IReadOnlyList<CombatFrameObservation> observations)
    {
        var ambiguous = new HashSet<TargetPressureActorIdentity>();
        for (var leftIndex = 0; leftIndex < observations.Count; leftIndex++)
        {
            var left = observations[leftIndex].Actor;
            for (var rightIndex = leftIndex + 1; rightIndex < observations.Count; rightIndex++)
            {
                var right = observations[rightIndex].Actor;
                if (left.GameObjectId != right.GameObjectId && left.EntityId != right.EntityId) continue;
                ambiguous.Add(left);
                ambiguous.Add(right);
            }
        }

        return ambiguous;
    }

    private static CombatFramePlanRow Unknown(int slot) => new(
        slot,
        CombatFrameAvailability.Unknown,
        default,
        0,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        false,
        0,
        0,
        CombatFrameIncomingEvidence.None);
}
