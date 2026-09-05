namespace SeitonSense.Core;

public enum OpponentLimitBreakGaugeDisplayMode : byte
{
    Hidden,
    Preview,
    Live,
}

public readonly record struct OpponentLimitBreakGaugeValue(
    TargetPressureActorIdentity Actor,
    int EnemySlot,
    uint JobId,
    int MinimumValue,
    int CurrentValue,
    int MaximumValue)
{
    public int Range => MaximumValue - MinimumValue;
    public int NormalizedCurrent => CurrentValue - MinimumValue;
    public float Fraction => Range > 0 ? NormalizedCurrent / (float)Range : 0f;
    public bool IsReady => Range > 0 && CurrentValue == MaximumValue;
}

/// <summary>
/// Pure validation for direct read-only values exposed by AtkComponentGaugeBar.
/// It deliberately contains no recharge model or rendered-geometry calibration.
/// </summary>
public static class OpponentLimitBreakGaugeRules
{
    public const int FirstEnemySlot = 1;
    public const int LastEnemySlot = 5;
    public const int EnemyCount = 5;
    public const int CalibratedMaximumValue = 10_000;
    public const long MaximumSnapshotAgeMilliseconds = 250;

    public static OpponentLimitBreakGaugeDisplayMode ResolveDisplayMode(
        bool enabled,
        bool showLiveBars,
        bool counterPreview,
        bool limitBreakPreview,
        bool snapshotActive,
        long publishedAtMilliseconds,
        long nowMilliseconds,
        IReadOnlyList<OpponentLimitBreakGaugeValue>? values)
    {
        if (limitBreakPreview || (counterPreview && showLiveBars))
            return OpponentLimitBreakGaugeDisplayMode.Preview;
        if (counterPreview || !enabled || !showLiveBars || !snapshotActive ||
            !IsFresh(publishedAtMilliseconds, nowMilliseconds) || !IsCompleteExactEnemySet(values))
            return OpponentLimitBreakGaugeDisplayMode.Hidden;
        return OpponentLimitBreakGaugeDisplayMode.Live;
    }

    public static bool TryCreateValue(
        TargetPressureActorIdentity actor,
        int enemySlot,
        uint jobId,
        int minimumValue,
        int currentValue,
        int maximumValue,
        out OpponentLimitBreakGaugeValue value)
    {
        value = default;
        if (!actor.IsValid ||
            enemySlot is < FirstEnemySlot or > LastEnemySlot ||
            jobId == 0 ||
            maximumValue <= minimumValue ||
            currentValue < minimumValue ||
            currentValue > maximumValue)
        {
            return false;
        }

        value = new OpponentLimitBreakGaugeValue(
            actor,
            enemySlot,
            jobId,
            minimumValue,
            currentValue,
            maximumValue);
        return true;
    }

    public static bool TryCreateCalibratedValue(
        TargetPressureActorIdentity actor,
        int enemySlot,
        uint jobId,
        float fraction,
        out OpponentLimitBreakGaugeValue value)
    {
        value = default;
        if (!float.IsFinite(fraction) || fraction is < 0f or > 1f) return false;

        var currentValue = fraction == 1f
            ? CalibratedMaximumValue
            : Math.Min(
                CalibratedMaximumValue - 1,
                (int)MathF.Round(fraction * CalibratedMaximumValue));
        return TryCreateValue(
            actor,
            enemySlot,
            jobId,
            0,
            currentValue,
            CalibratedMaximumValue,
            out value);
    }

    public static bool MatchesLocalController(
        int gaugeMinimum,
        int gaugeCurrent,
        int gaugeMaximum,
        uint controllerCurrent,
        uint controllerMaximum)
    {
        if (gaugeMaximum <= gaugeMinimum ||
            gaugeCurrent < gaugeMinimum ||
            gaugeCurrent > gaugeMaximum ||
            controllerMaximum == 0 ||
            controllerCurrent > controllerMaximum)
        {
            return false;
        }

        var gaugeRange = (long)gaugeMaximum - gaugeMinimum;
        var gaugeNormalized = (long)gaugeCurrent - gaugeMinimum;
        var left = gaugeNormalized * controllerMaximum;
        var right = (long)controllerCurrent * gaugeRange;
        var delta = Math.Abs(left - right);

        // Permit only the deterministic quantization of one unit in either
        // native scale. This is not a timing or charge-rate estimate.
        var quantizationTolerance = controllerMaximum + gaugeRange;
        return delta <= quantizationTolerance;
    }

    public static bool MatchesNativeScale(
        int referenceMinimum,
        int referenceMaximum,
        int candidateMinimum,
        int candidateMaximum) =>
        referenceMaximum > referenceMinimum &&
        candidateMinimum == referenceMinimum &&
        candidateMaximum == referenceMaximum;

    public static bool IsCompleteExactEnemySet(
        IReadOnlyList<OpponentLimitBreakGaugeValue>? values)
    {
        if (values is null || values.Count != EnemyCount) return false;
        var actors = new HashSet<TargetPressureActorIdentity>();
        for (var index = 0; index < EnemyCount; index++)
        {
            var value = values[index];
            if (value.EnemySlot != index + FirstEnemySlot ||
                !value.Actor.IsValid ||
                !actors.Add(value.Actor) ||
                value.JobId == 0 ||
                value.MaximumValue <= value.MinimumValue ||
                value.CurrentValue < value.MinimumValue ||
                value.CurrentValue > value.MaximumValue)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsFresh(long publishedAtMilliseconds, long nowMilliseconds) =>
        publishedAtMilliseconds >= 0 &&
        nowMilliseconds >= publishedAtMilliseconds &&
        nowMilliseconds - publishedAtMilliseconds <= MaximumSnapshotAgeMilliseconds;

    public static float ReadyPulseAlpha(bool ready, long nowMilliseconds, bool reducedMotion)
    {
        if (!ready || reducedMotion || nowMilliseconds < 0) return 1f;
        var phase = nowMilliseconds % 1_000L / 1_000f;
        var wave = 0.5f + (0.5f * MathF.Sin(phase * MathF.PI * 2f));
        return 0.78f + (0.22f * wave);
    }
}
