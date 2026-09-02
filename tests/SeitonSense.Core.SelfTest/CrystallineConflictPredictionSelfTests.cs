using SeitonSense.Core;

internal static class CrystallineConflictPredictionSelfTests
{
    public static void UnknownPlayersAndBalancedRecordsAreNeutral()
    {
        var unknown = Team();
        True(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(
                unknown,
                unknown,
                out var allUnknown),
            "two complete unknown teams");
        Near(0.5d, allUnknown.OwnTeamWinProbability, "all unknown prediction");
        Near(0.5d, allUnknown.OwnTeamObservedRate, "all unknown own rate");
        Near(0.5d, allUnknown.EnemyTeamObservedRate, "all unknown enemy rate");
        Equal(0, allUnknown.KnownOwnPlayers, "unknown own count");
        Equal(0, allUnknown.KnownEnemyPlayers, "unknown enemy count");

        var balancedKnown = Team(new CrystallineConflictMapWinLossSnapshot(2, 2));
        True(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(
                balancedKnown,
                unknown,
                out var balanced),
            "balanced known record");
        Near(0.5d, balanced.OwnTeamWinProbability, "2W 2L remains neutral");
        Equal(1, balanced.KnownOwnPlayers, "one known own player");

        var mixed = Team(
            new CrystallineConflictMapWinLossSnapshot(45, 55),
            new CrystallineConflictMapWinLossSnapshot(45, 55),
            new CrystallineConflictMapWinLossSnapshot(60, 40));
        True(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(
                mixed,
                unknown,
                out var mixedPrediction),
            "two 45 percent plus one 60 percent");
        Near(0.5d, mixedPrediction.OwnTeamObservedRate, "mixed example averages back to neutral");
        Near(0.5d, mixedPrediction.OwnTeamWinProbability, "mixed example prediction");
        Equal(3, mixedPrediction.KnownOwnPlayers, "three known own players");
    }

    public static void StartPredictionIsSmoothedSymmetricAndBounded()
    {
        var unknown = Team();
        var oneWinner = Team(new CrystallineConflictMapWinLossSnapshot(1, 0));
        True(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(
                oneWinner,
                unknown,
                out var smallSample),
            "one observed win");
        Near(6d / 11d, smallSample.OwnTeamObservedRate * 5d - 2d, "one record uses 5W 5L prior");
        True(
            smallSample.OwnTeamWinProbability > 0.5d &&
            smallSample.OwnTeamWinProbability < 0.51d,
            "one lucky match has only a small influence");

        var strong = Team(
            new CrystallineConflictMapWinLossSnapshot(100, 0),
            new CrystallineConflictMapWinLossSnapshot(100, 0),
            new CrystallineConflictMapWinLossSnapshot(100, 0),
            new CrystallineConflictMapWinLossSnapshot(100, 0),
            new CrystallineConflictMapWinLossSnapshot(100, 0));
        var weak = Team(
            new CrystallineConflictMapWinLossSnapshot(0, 100),
            new CrystallineConflictMapWinLossSnapshot(0, 100),
            new CrystallineConflictMapWinLossSnapshot(0, 100),
            new CrystallineConflictMapWinLossSnapshot(0, 100),
            new CrystallineConflictMapWinLossSnapshot(0, 100));
        True(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(strong, weak, out var high),
            "strong against weak");
        True(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(weak, strong, out var low),
            "weak against strong");
        Near(CrystallineConflictPredictionRules.MaximumStartProbability, high.OwnTeamWinProbability, "upper opening clamp");
        Near(CrystallineConflictPredictionRules.MinimumStartProbability, low.OwnTeamWinProbability, "lower opening clamp");
        Near(1d, high.OwnTeamWinProbability + low.OwnTeamWinProbability, "team swap symmetry");

        False(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(unknown[..4], unknown, out _),
            "incomplete own team");
        False(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(null, unknown, out _),
            "missing own team");
        var invalid = unknown.ToArray();
        invalid[2] = new CrystallineConflictMapWinLossSnapshot(long.MaxValue, 1);
        False(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(invalid, unknown, out _),
            "overflowing player record");
        var maximumValid = unknown.ToArray();
        maximumValid[2] = new CrystallineConflictMapWinLossSnapshot(long.MaxValue, 0);
        True(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(maximumValid, unknown, out var finiteMaximum) &&
            finiteMaximum.IsValid,
            "maximum valid counter remains finite without arithmetic overflow");
    }

    public static void PlayerResultsAreOrientedToEachPlayersTeam()
    {
        True(
            CrystallineConflictPredictionRules.TryResolveObservedPlayerWin(1, 0, 0, out var allyWon) && allyWon,
            "local victory credits ally victory");
        True(
            CrystallineConflictPredictionRules.TryResolveObservedPlayerWin(1, 0, 1, out var enemyLost) && !enemyLost,
            "local victory credits enemy loss");
        True(
            CrystallineConflictPredictionRules.TryResolveObservedPlayerWin(2, 0, 1, out var enemyWon) && enemyWon,
            "local defeat credits enemy victory");
        True(
            CrystallineConflictPredictionRules.TryResolveObservedPlayerWin(2, 1, 1, out var allyLost) && !allyLost,
            "local defeat credits ally loss");
        False(CrystallineConflictPredictionRules.TryResolveObservedPlayerWin(0, 0, 0, out _), "unknown result");
        False(CrystallineConflictPredictionRules.TryResolveObservedPlayerWin(1, 2, 0, out _), "invalid local team");
        False(CrystallineConflictPredictionRules.TryResolveObservedPlayerWin(1, 0, 2, out _), "invalid participant team");
    }

    public static void LivePredictionUsesOnlyKnownBoundedSignals()
    {
        True(
            CrystallineConflictPredictionRules.TryCalculateStartPrediction(Team(), Team(), out var opening),
            "neutral opening");
        True(
            CrystallineConflictPredictionRules.TryApplyLiveAdjustment(
                opening,
                new CrystallineConflictLivePredictionObservation(true, 750, 250, true, 2, 4),
                out var live),
            "valid live signals");
        Near(0.125d, live.ProgressAdjustment, "50 point progress lead");
        Near(0.05d, live.DeathAdjustment, "two net enemy deaths");
        Near(0.675d, live.OwnTeamWinProbability, "combined live estimate");
        True(live.UsedTeamProgress, "progress source marked used");
        True(live.UsedDeathCounts, "death source marked used");

        True(
            CrystallineConflictPredictionRules.TryApplyLiveAdjustment(
                opening,
                new CrystallineConflictLivePredictionObservation(false, -99, 5_000, false, -2, 5_000),
                out var missing),
            "unavailable sources are omitted rather than treated as zero");
        Near(0.5d, missing.OwnTeamWinProbability, "missing live telemetry keeps opening estimate");
        Near(0d, missing.ProgressAdjustment, "missing progress contributes nothing");
        Near(0d, missing.DeathAdjustment, "missing deaths contribute nothing");

        True(
            CrystallineConflictPredictionRules.TryApplyLiveAdjustment(
                opening,
                new CrystallineConflictLivePredictionObservation(false, 0, 0, true, 0, 100),
                out var cappedDeaths),
            "large but valid death count");
        Near(0.15d, cappedDeaths.DeathAdjustment, "death difference contribution is capped");
        False(
            CrystallineConflictPredictionRules.TryApplyLiveAdjustment(
                opening,
                new CrystallineConflictLivePredictionObservation(true, 1_001, 0, false, 0, 0),
                out _),
            "out of range progress fails closed");
        False(
            CrystallineConflictPredictionRules.TryApplyLiveAdjustment(
                opening,
                new CrystallineConflictLivePredictionObservation(false, 0, 0, true, -1, 0),
                out _),
            "negative death count fails closed");

        var certainOpening = opening with { OwnTeamWinProbability = 0.99d };
        True(
            CrystallineConflictPredictionRules.TryApplyLiveAdjustment(
                certainOpening,
                new CrystallineConflictLivePredictionObservation(true, 1_000, 0, true, 0, 100),
                out var clamped),
            "bounded high live estimate");
        Near(CrystallineConflictPredictionRules.MaximumLiveProbability, clamped.OwnTeamWinProbability, "live upper clamp");
    }

    public static void DirectDamageAndHealingAmountsDecodeExactly()
    {
        True(
            CrystallineConflictPredictionRules.TryDecodeObservedEffectAmount(3, 0, 0, 123, out var damage),
            "normal damage");
        Equal(CrystallineConflictObservedEffectKind.Damage, damage.Kind, "damage kind");
        Equal(123u, damage.Amount, "normal damage amount");
        False(damage.AppliedToSource, "normal damage target direction");

        True(
            CrystallineConflictPredictionRules.TryDecodeObservedEffectAmount(4, 2, 0x40, 5, out var heal),
            "large healing");
        Equal(CrystallineConflictObservedEffectKind.Healing, heal.Kind, "healing kind");
        Equal(131_077u, heal.Amount, "24-bit healing amount");

        True(
            CrystallineConflictPredictionRules.TryDecodeObservedEffectAmount(5, 1, 0xC0, 7, out var reflected),
            "blocked reflected damage");
        Equal(65_543u, reflected.Amount, "24-bit reflected amount");
        True(reflected.AppliedToSource, "source-applied bit is preserved for attribution");

        False(CrystallineConflictPredictionRules.TryDecodeObservedEffectAmount(1, 0, 0, 100, out _), "unsupported effect");
        False(CrystallineConflictPredictionRules.TryDecodeObservedEffectAmount(3, 1, 0, 100, out _), "high byte without flag");
        False(CrystallineConflictPredictionRules.TryDecodeObservedEffectAmount(4, 0, 0, 0, out _), "zero amount");
    }

    public static void DeathEdgesRequireContinuousAliveEvidence()
    {
        var state = CrystallineConflictObservedDeathState.Initial;
        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 7, true, true, false);
        True(state.HasContinuousBaseline, "first living sample establishes baseline");
        Equal(0, state.ObservedDeaths, "baseline is not a death");

        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 7, true, true, true);
        Equal(1, state.ObservedDeaths, "alive to dead counts once");
        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 7, true, true, true);
        Equal(1, state.ObservedDeaths, "continuous dead frames do not repeat");
        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 7, true, true, false);
        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 7, true, true, true);
        Equal(2, state.ObservedDeaths, "a later continuous death counts");

        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 7, true, false, false);
        False(state.HasContinuousBaseline, "identity loss breaks continuity");
        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 7, true, true, true);
        Equal(2, state.ObservedDeaths, "dead sample after a gap cannot create a phantom death");
        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 7, true, true, false);
        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 7, true, true, true);
        Equal(3, state.ObservedDeaths, "fresh continuous alive edge rearms");

        state = CrystallineConflictPredictionRules.ObserveDeathEdge(state, 8, true, true, true);
        Equal(0, state.ObservedDeaths, "new match generation clears totals");
        True(state.WasDead, "first dead sample is only a baseline");
        Equal(
            CrystallineConflictObservedDeathState.Initial,
            CrystallineConflictPredictionRules.ObserveDeathEdge(state, -1, true, true, false),
            "invalid generation resets");
    }

    private static CrystallineConflictMapWinLossSnapshot[] Team(
        params CrystallineConflictMapWinLossSnapshot[] known)
    {
        var team = new CrystallineConflictMapWinLossSnapshot[
            CrystallineConflictPredictionRules.PlayersPerTeam];
        Array.Copy(known, team, Math.Min(known.Length, team.Length));
        return team;
    }

    private static void Near(double expected, double actual, string message, double tolerance = 0.000_000_1d)
    {
        if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
