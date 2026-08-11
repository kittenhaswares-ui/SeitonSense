namespace SeitonSense.Core;

public enum SeitonCueKind
{
    Hidden = 0,
    Preparation = 1,
    Execute = 2,
}

public readonly record struct PersistentSeitonCueState(
    bool ExecuteEntryLatched,
    long ExecuteObservedAtMilliseconds,
    long RangeFalseObservedAtMilliseconds,
    SeitonCueKind LastVisibleCue)
{
    public static PersistentSeitonCueState Initial => new(false, -1, -1, SeitonCueKind.Hidden);
}

public readonly record struct PersistentSeitonCueDecision(
    PersistentSeitonCueState NextState,
    SeitonCueKind Cue,
    bool TriggerEntryPulse)
{
    public bool IsVisible => Cue != SeitonCueKind.Hidden;
}

public static class PersistentSeitonCueRules
{
    public const long StableExecuteMilliseconds = 50;
    public const long RangeFalseGraceMilliseconds = 200;
    public const uint ExecuteRearmPercent = 52;
    public const uint PreparationUpperPercent = 60;

    public static PersistentSeitonCueDecision Observe(
        PersistentSeitonCueState state,
        bool resourceReady,
        bool targetPresent,
        bool trustedHealthSample,
        uint currentHp,
        uint maximumHp,
        bool rangeAndLineOfSight,
        bool showPreparation,
        long nowMilliseconds,
        bool hardReset = false,
        long stableExecuteMilliseconds = StableExecuteMilliseconds,
        long rangeFalseGraceMilliseconds = RangeFalseGraceMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stableExecuteMilliseconds);
        ArgumentOutOfRangeException.ThrowIfNegative(rangeFalseGraceMilliseconds);

        if (hardReset || !targetPresent || !resourceReady)
            return Hidden(PersistentSeitonCueState.Initial);

        if (!trustedHealthSample || !HasValidHealth(currentHp, maximumHp))
        {
            return Hidden(new PersistentSeitonCueState(
                state.ExecuteEntryLatched,
                -1,
                -1,
                SeitonCueKind.Hidden));
        }

        var executeEntryLatched = state.ExecuteEntryLatched;
        var reachedRearmThreshold = IsAtOrAbovePercent(currentHp, maximumHp, ExecuteRearmPercent);
        if (reachedRearmThreshold)
            executeEntryLatched = false;

        var belowHalf = ExecuteThreshold.IsBelowHalf(currentHp, maximumHp);
        var preparation = showPreparation && IsPreparationBand(currentHp, maximumHp);

        if (!rangeAndLineOfSight)
        {
            var rangeFalseSince = state.RangeFalseObservedAtMilliseconds;
            if (rangeFalseSince < 0 || nowMilliseconds < rangeFalseSince)
                rangeFalseSince = nowMilliseconds;

            var lastCueStillSemanticallyValid = state.LastVisibleCue switch
            {
                SeitonCueKind.Execute => belowHalf,
                SeitonCueKind.Preparation => preparation,
                _ => false,
            };
            var retainVisibleCue = lastCueStillSemanticallyValid &&
                                   nowMilliseconds - rangeFalseSince < rangeFalseGraceMilliseconds;
            var retainedCue = retainVisibleCue ? state.LastVisibleCue : SeitonCueKind.Hidden;
            return new PersistentSeitonCueDecision(
                new PersistentSeitonCueState(
                    executeEntryLatched,
                    -1,
                    rangeFalseSince,
                    retainedCue),
                retainedCue,
                false);
        }

        if (belowHalf)
        {
            if (executeEntryLatched)
            {
                var visible = state with
                {
                    ExecuteEntryLatched = true,
                    ExecuteObservedAtMilliseconds = -1,
                    RangeFalseObservedAtMilliseconds = -1,
                    LastVisibleCue = SeitonCueKind.Execute,
                };
                return new PersistentSeitonCueDecision(visible, SeitonCueKind.Execute, false);
            }

            var executeSince = state.ExecuteObservedAtMilliseconds;
            if (executeSince < 0 || nowMilliseconds < executeSince)
                executeSince = nowMilliseconds;

            if (nowMilliseconds - executeSince >= stableExecuteMilliseconds)
            {
                var entered = new PersistentSeitonCueState(
                    true,
                    -1,
                    -1,
                    SeitonCueKind.Execute);
                return new PersistentSeitonCueDecision(entered, SeitonCueKind.Execute, true);
            }

            var pendingCue = state.LastVisibleCue == SeitonCueKind.Preparation
                ? SeitonCueKind.Preparation
                : SeitonCueKind.Hidden;
            var pending = new PersistentSeitonCueState(
                false,
                executeSince,
                -1,
                pendingCue);
            return new PersistentSeitonCueDecision(pending, pendingCue, false);
        }

        var cue = preparation ? SeitonCueKind.Preparation : SeitonCueKind.Hidden;
        var next = new PersistentSeitonCueState(
            executeEntryLatched,
            -1,
            -1,
            cue);
        return new PersistentSeitonCueDecision(next, cue, false);
    }

    public static bool IsPreparationBand(uint currentHp, uint maximumHp) =>
        HasValidHealth(currentHp, maximumHp) &&
        IsAtOrAbovePercent(currentHp, maximumHp, 50) &&
        !IsAtOrAbovePercent(currentHp, maximumHp, PreparationUpperPercent);

    private static bool HasValidHealth(uint currentHp, uint maximumHp) =>
        currentHp > 0 && maximumHp > 0 && currentHp <= maximumHp;

    private static bool IsAtOrAbovePercent(uint currentHp, uint maximumHp, uint percent) =>
        (ulong)currentHp * 100 >= (ulong)maximumHp * percent;

    private static PersistentSeitonCueDecision Hidden(PersistentSeitonCueState state) =>
        new(state, SeitonCueKind.Hidden, false);
}
