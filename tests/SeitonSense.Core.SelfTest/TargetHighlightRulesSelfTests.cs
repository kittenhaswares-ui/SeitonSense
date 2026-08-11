using SeitonSense.Core;

internal static class TargetHighlightRulesSelfTests
{
    public static void SameObjectIsCombined()
    {
        var target = Candidate(gameObjectId: 100, jobId: 30, currentHp: 49, maximumHp: 100, enemySlot: 3);
        var plan = TargetHighlightRules.BuildPlan(Observation(target, target));

        Equal(1, plan.Length, "same actor is rendered once");
        Equal(TargetHighlightRelation.CurrentAndFocus, plan[0].Relation, "combined relation");
        Equal(100UL, plan[0].GameObjectId, "combined identity");
        Equal(30u, plan[0].JobId, "job stays available");
        Equal("49%", plan[0].HpLabel, "HP stays available");
        Equal("S3", plan[0].EnemySlotLabel, "slot stays available");
    }

    public static void DifferentObjectsRemainOrdered()
    {
        var current = Candidate(gameObjectId: 100);
        var focus = Candidate(gameObjectId: 200);
        var plan = TargetHighlightRules.BuildPlan(Observation(current, focus));

        Equal(2, plan.Length, "different actors remain separate");
        Equal(TargetHighlightRelation.Current, plan[0].Relation, "current is first");
        Equal(100UL, plan[0].GameObjectId, "current identity");
        Equal(TargetHighlightRelation.Focus, plan[1].Relation, "focus is second");
        Equal(200UL, plan[1].GameObjectId, "focus identity");
    }

    public static void PvpGateIsPerSource()
    {
        var target = Candidate(gameObjectId: 100);
        var outsidePvp = Observation(target, target) with
        {
            IsPvP = false,
            CurrentTargetPvPOnly = true,
            FocusTargetPvPOnly = false,
        };
        var plan = TargetHighlightRules.BuildPlan(outsidePvp);

        Equal(1, plan.Length, "all-context focus survives outside PvP");
        Equal(TargetHighlightRelation.Focus, plan[0].Relation, "gated current is not falsely combined");

        plan = TargetHighlightRules.BuildPlan(outsidePvp with { IsPvP = true });
        Equal(1, plan.Length, "same actor deduplicates inside PvP");
        Equal(TargetHighlightRelation.CurrentAndFocus, plan[0].Relation, "both sources become active");

        plan = TargetHighlightRules.BuildPlan(outsidePvp with { IsLoggedIn = false, IsPvP = true });
        Equal(0, plan.Length, "logout clears every source");
    }

    public static void InvalidIdentitiesFailClosed()
    {
        var invalid = new[]
        {
            Candidate(gameObjectId: 0),
            Candidate(gameObjectId: 0xE0000000UL),
            Candidate(gameObjectId: ulong.MaxValue),
            Candidate(gameObjectId: 100) with { IsValid = false },
        };

        foreach (var candidate in invalid)
        {
            var plan = TargetHighlightRules.BuildPlan(Observation(candidate, null));
            Equal(0, plan.Length, $"invalid identity is omitted: {candidate.GameObjectId}");
        }
    }

    public static void HpFormattingIsSafe()
    {
        Equal("0%", TargetHighlightRules.FormatHpPercent(0, 100), "zero HP is valid display data");
        Equal("50%", TargetHighlightRules.FormatHpPercent(50, 100), "exact half");
        Equal("33%", TargetHighlightRules.FormatHpPercent(1, 3), "round down");
        Equal("67%", TargetHighlightRules.FormatHpPercent(2, 3), "round up");
        Equal("100%", TargetHighlightRules.FormatHpPercent(uint.MaxValue, uint.MaxValue), "wide arithmetic");
        Equal(string.Empty, TargetHighlightRules.FormatHpPercent(1, 0), "zero maximum fails closed");
        Equal(string.Empty, TargetHighlightRules.FormatHpPercent(101, 100), "impossible HP fails closed");
    }

    public static void DistanceFormattingIsSafe()
    {
        Equal("~7.0y", TargetHighlightRules.FormatDistance(10f, 1f, 2f), "hitbox-edge distance");
        Equal("~0.0y", TargetHighlightRules.FormatDistance(2f, 2f, 2f), "overlap clamps to zero");
        Equal("~128y", TargetHighlightRules.FormatDistance(130f, 1f, 1f), "large distance uses whole yalms");
        Equal(string.Empty, TargetHighlightRules.FormatDistance(float.NaN, 1f, 1f), "NaN fails closed");
        Equal(string.Empty, TargetHighlightRules.FormatDistance(float.PositiveInfinity, 1f, 1f), "infinity fails closed");
        Equal(string.Empty, TargetHighlightRules.FormatDistance(-1f, 1f, 1f), "negative center distance fails closed");
        Equal(string.Empty, TargetHighlightRules.FormatDistance(10f, -1f, 1f), "negative radius fails closed");
    }

    public static void EnemySlotFormattingIsExact()
    {
        for (var slot = 1; slot <= 5; slot++)
            Equal($"S{slot}", TargetHighlightRules.FormatEnemySlot(slot), $"slot {slot}");

        Equal(string.Empty, TargetHighlightRules.FormatEnemySlot(0), "slot zero");
        Equal(string.Empty, TargetHighlightRules.FormatEnemySlot(6), "slot six");
    }

    public static void CombinedPlanUsesOnlySafeFallbacks()
    {
        var current = Candidate(gameObjectId: 100) with
        {
            JobId = 0,
            CurrentHp = 101,
            MaximumHp = 100,
            CenterDistanceYalms = float.NaN,
            EnemySlot = 0,
        };
        var focus = Candidate(
            gameObjectId: 100,
            jobId: 30,
            currentHp: 40,
            maximumHp: 100,
            centerDistanceYalms: 10f,
            localHitboxRadius: 1f,
            targetHitboxRadius: 1f,
            enemySlot: 4);
        var plan = TargetHighlightRules.BuildPlan(Observation(current, focus));

        Equal(1, plan.Length, "same actor remains combined");
        Equal(30u, plan[0].JobId, "missing job uses same-identity fallback");
        Equal("40%", plan[0].HpLabel, "invalid HP uses safe same-identity fallback");
        Equal("~8.0y", plan[0].DistanceLabel, "invalid distance uses safe same-identity fallback");
        Equal("S4", plan[0].EnemySlotLabel, "invalid slot uses safe same-identity fallback");
    }

    private static TargetHighlightObservation Observation(
        TargetHighlightCandidate? current,
        TargetHighlightCandidate? focus) =>
        new(
            IsLoggedIn: true,
            IsPvP: true,
            IncludeCurrentTarget: true,
            CurrentTargetPvPOnly: true,
            CurrentTarget: current,
            IncludeFocusTarget: true,
            FocusTargetPvPOnly: false,
            FocusTarget: focus);

    private static TargetHighlightCandidate Candidate(
        ulong gameObjectId,
        uint jobId = 0,
        uint currentHp = 100,
        uint maximumHp = 100,
        float centerDistanceYalms = 10f,
        float localHitboxRadius = 1f,
        float targetHitboxRadius = 1f,
        int enemySlot = 0) =>
        new(
            gameObjectId,
            IsValid: true,
            jobId,
            currentHp,
            maximumHp,
            centerDistanceYalms,
            localHitboxRadius,
            targetHitboxRadius,
            enemySlot);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
