namespace SeitonSense.Core;

public readonly record struct CrystallineConflictStartPrediction(
    double OwnTeamWinProbability,
    double OwnTeamObservedRate,
    double EnemyTeamObservedRate,
    int KnownOwnPlayers,
    int KnownEnemyPlayers)
{
    public bool IsValid =>
        double.IsFinite(OwnTeamWinProbability) &&
        double.IsFinite(OwnTeamObservedRate) &&
        double.IsFinite(EnemyTeamObservedRate) &&
        OwnTeamWinProbability is >= 0d and <= 1d &&
        OwnTeamObservedRate is >= 0d and <= 1d &&
        EnemyTeamObservedRate is >= 0d and <= 1d &&
        KnownOwnPlayers is >= 0 and <= CrystallineConflictPredictionRules.PlayersPerTeam &&
        KnownEnemyPlayers is >= 0 and <= CrystallineConflictPredictionRules.PlayersPerTeam;
}

public readonly record struct CrystallineConflictLivePredictionObservation(
    bool HasTeamProgress,
    int OwnProgressTenthsPercent,
    int EnemyProgressTenthsPercent,
    bool HasDeathCounts,
    int OwnTeamDeaths,
    int EnemyTeamDeaths);

public readonly record struct CrystallineConflictLivePrediction(
    double OwnTeamWinProbability,
    double ProgressAdjustment,
    double DeathAdjustment,
    bool UsedTeamProgress,
    bool UsedDeathCounts)
{
    public bool IsValid =>
        double.IsFinite(OwnTeamWinProbability) &&
        double.IsFinite(ProgressAdjustment) &&
        double.IsFinite(DeathAdjustment) &&
        OwnTeamWinProbability is >= 0d and <= 1d;
}

public enum CrystallineConflictObservedEffectKind : byte
{
    None = 0,
    Damage = 1,
    Healing = 2,
}

public readonly record struct CrystallineConflictObservedEffectAmount(
    CrystallineConflictObservedEffectKind Kind,
    uint Amount,
    bool AppliedToSource);

public readonly record struct CrystallineConflictObservedDeathState(
    long MatchGeneration,
    bool HasContinuousBaseline,
    bool WasDead,
    int ObservedDeaths)
{
    public static CrystallineConflictObservedDeathState Initial => new(-1, false, false, 0);
}

/// <summary>
/// Pure rules for a deliberately playful, local CC prediction. Historical
/// records are observations from this installation, not player ratings and not
/// a claim of predictive accuracy. Unknown players are exactly neutral. Small
/// samples are pulled toward 50%, and both the opening and live estimates are
/// bounded so the UI cannot present false certainty.
/// </summary>
public static class CrystallineConflictPredictionRules
{
    public const int PlayersPerTeam = 5;
    public const int PriorWins = 5;
    public const int PriorLosses = 5;
    public const int MaximumProgressTenthsPercent = 1_000;
    public const int MaximumAcceptedDeathCount = 1_000;
    public const int MaximumDeathDifferenceContribution = 6;
    public const int MaximumObservedDeathsPerPlayer = 255;

    public const double MinimumStartProbability = 0.25d;
    public const double MaximumStartProbability = 0.75d;
    public const double MinimumLiveProbability = 0.05d;
    public const double MaximumLiveProbability = 0.95d;

    public const double FullProgressDifferenceWeight = 0.25d;
    public const double PerNetDeathWeight = 0.025d;

    public const byte DamageEffectType = 3;
    public const byte HealEffectType = 4;
    public const byte BlockedDamageEffectType = 5;
    public const byte ParriedDamageEffectType = 6;
    public const byte LargeValueFlag = 0x40;
    public const byte AppliedToSourceFlag = 0x80;

    /// <summary>
    /// The complete roster is useful during the pre-match countdown, but
    /// damage, healing, deaths, and native progress become live evidence only
    /// after combat has actually started. A final scoreboard is authoritative
    /// and closes the live-capture lane again.
    /// </summary>
    public static bool CanUseLiveMatchInputs(
        bool exactRosterAvailable,
        bool combatStarted,
        bool finalResultObserved) =>
        exactRosterAvailable && combatStarted && !finalResultObserved;

    /// <summary>
    /// Combines two complete five-player teams. A 0-0 record represents an
    /// unknown player and contributes exactly 50%. A 5W/5L prior prevents a
    /// single locally observed match from pretending to be a reliable rating.
    /// </summary>
    public static bool TryCalculateStartPrediction(
        IReadOnlyList<CrystallineConflictMapWinLossSnapshot>? ownTeam,
        IReadOnlyList<CrystallineConflictMapWinLossSnapshot>? enemyTeam,
        out CrystallineConflictStartPrediction prediction)
    {
        prediction = default;
        if (!TryCalculateTeamRate(ownTeam, out var ownRate, out var ownKnown) ||
            !TryCalculateTeamRate(enemyTeam, out var enemyRate, out var enemyKnown))
        {
            return false;
        }

        var probability = Math.Clamp(
            0.5d + ownRate - enemyRate,
            MinimumStartProbability,
            MaximumStartProbability);
        prediction = new CrystallineConflictStartPrediction(
            probability,
            ownRate,
            enemyRate,
            ownKnown,
            enemyKnown);
        return prediction.IsValid;
    }

    /// <summary>
    /// Converts the local result into the result for any exact participant.
    /// Team zero and one are intentionally the only accepted values.
    /// </summary>
    public static bool TryResolveObservedPlayerWin(
        byte localResult,
        byte localTeam,
        byte participantTeam,
        out bool participantWon)
    {
        participantWon = false;
        if (localResult is not (1 or 2) || localTeam > 1 || participantTeam > 1)
            return false;

        var winningTeam = localResult == 1 ? localTeam : (byte)(1 - localTeam);
        participantWon = participantTeam == winningTeam;
        return true;
    }

    /// <summary>
    /// Applies only live inputs that were positively observed. Missing progress
    /// or death telemetry contributes zero rather than being invented as zero.
    /// Raw CC progress is expressed in tenths of one percent (0 through 1000).
    /// </summary>
    public static bool TryApplyLiveAdjustment(
        CrystallineConflictStartPrediction opening,
        CrystallineConflictLivePredictionObservation observation,
        out CrystallineConflictLivePrediction prediction)
    {
        prediction = default;
        if (!opening.IsValid ||
            observation.HasTeamProgress &&
            (observation.OwnProgressTenthsPercent is < 0 or > MaximumProgressTenthsPercent ||
             observation.EnemyProgressTenthsPercent is < 0 or > MaximumProgressTenthsPercent) ||
            observation.HasDeathCounts &&
            (observation.OwnTeamDeaths is < 0 or > MaximumAcceptedDeathCount ||
             observation.EnemyTeamDeaths is < 0 or > MaximumAcceptedDeathCount))
        {
            return false;
        }

        var progressAdjustment = observation.HasTeamProgress
            ? FullProgressDifferenceWeight *
              (observation.OwnProgressTenthsPercent - observation.EnemyProgressTenthsPercent) /
              MaximumProgressTenthsPercent
            : 0d;
        var netEnemyDeaths = observation.HasDeathCounts
            ? Math.Clamp(
                observation.EnemyTeamDeaths - observation.OwnTeamDeaths,
                -MaximumDeathDifferenceContribution,
                MaximumDeathDifferenceContribution)
            : 0;
        var deathAdjustment = observation.HasDeathCounts
            ? PerNetDeathWeight * netEnemyDeaths
            : 0d;
        var probability = Math.Clamp(
            opening.OwnTeamWinProbability + progressAdjustment + deathAdjustment,
            MinimumLiveProbability,
            MaximumLiveProbability);

        prediction = new CrystallineConflictLivePrediction(
            probability,
            progressAdjustment,
            deathAdjustment,
            observation.HasTeamProgress,
            observation.HasDeathCounts);
        return prediction.IsValid;
    }

    /// <summary>
    /// Decodes the native 24-bit amount for direct damage and healing. The
    /// applied-to-source bit is preserved for the runtime attribution layer;
    /// it must not silently credit a reflected effect to the packet target.
    /// HoT and DoT ticks use a different native boundary and are not represented
    /// by this decoder.
    /// </summary>
    public static bool TryDecodeObservedEffectAmount(
        byte effectType,
        byte param3,
        byte param4,
        ushort value,
        out CrystallineConflictObservedEffectAmount effect)
    {
        effect = default;
        var kind = effectType switch
        {
            DamageEffectType or BlockedDamageEffectType or ParriedDamageEffectType =>
                CrystallineConflictObservedEffectKind.Damage,
            HealEffectType => CrystallineConflictObservedEffectKind.Healing,
            _ => CrystallineConflictObservedEffectKind.None,
        };
        if (kind == CrystallineConflictObservedEffectKind.None ||
            (param4 & LargeValueFlag) == 0 && param3 != 0)
        {
            return false;
        }

        var amount = (uint)value;
        if ((param4 & LargeValueFlag) != 0) amount += (uint)param3 << 16;
        if (amount == 0) return false;

        effect = new CrystallineConflictObservedEffectAmount(
            kind,
            amount,
            (param4 & AppliedToSourceFlag) != 0);
        return true;
    }

    /// <summary>
    /// Optional in-memory fallback for an observed death column. It counts only
    /// a continuous alive-to-dead edge. A new match generation clears totals;
    /// missing identity/context breaks continuity so a later dead sample cannot
    /// create a phantom death.
    /// </summary>
    public static CrystallineConflictObservedDeathState ObserveDeathEdge(
        CrystallineConflictObservedDeathState state,
        long matchGeneration,
        bool exactContext,
        bool exactIdentity,
        bool isDead)
    {
        if (matchGeneration < 0) return CrystallineConflictObservedDeathState.Initial;
        if (state.MatchGeneration != matchGeneration)
            state = new CrystallineConflictObservedDeathState(matchGeneration, false, false, 0);

        if (!exactContext || !exactIdentity)
            return state with { HasContinuousBaseline = false, WasDead = false };

        if (!state.HasContinuousBaseline)
            return state with { HasContinuousBaseline = true, WasDead = isDead };

        var deaths = state.ObservedDeaths;
        if (!state.WasDead && isDead && deaths < MaximumObservedDeathsPerPlayer)
            deaths++;

        return state with { WasDead = isDead, ObservedDeaths = deaths };
    }

    private static bool TryCalculateTeamRate(
        IReadOnlyList<CrystallineConflictMapWinLossSnapshot>? team,
        out double rate,
        out int knownPlayers)
    {
        rate = 0d;
        knownPlayers = 0;
        if (team is null || team.Count != PlayersPerTeam) return false;

        var sum = 0d;
        foreach (var record in team)
        {
            if (!record.IsValid) return false;
            if (record.HasData) knownPlayers++;
            sum += (record.Wins + (double)PriorWins) /
                   ((double)record.Matches + PriorWins + PriorLosses);
        }

        rate = sum / PlayersPerTeam;
        return double.IsFinite(rate) && rate is >= 0d and <= 1d;
    }
}
