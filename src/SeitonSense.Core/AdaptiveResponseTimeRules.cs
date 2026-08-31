namespace SeitonSense.Core;

/// <summary>
/// One immutable observation from the shared adaptive-response clock. Raw
/// timestamps provide high-resolution duration ordering, while frame epoch and
/// capture ordinal keep observations distinct when the native counter returns
/// the same value more than once.
/// </summary>
public readonly record struct AdaptiveResponseTimeStamp(
    long Timestamp,
    long FrameEpoch,
    long CaptureOrdinal,
    long LegacyMilliseconds)
{
    public static AdaptiveResponseTimeStamp Invalid => new(-1, -1, -1, -1);

    public bool IsValid =>
        Timestamp >= 0 &&
        FrameEpoch >= 0 &&
        CaptureOrdinal > 0 &&
        LegacyMilliseconds >= 0;
}

/// <summary>
/// Atomic, immutable identity for one Dalamud framework frame.
/// </summary>
public sealed record AdaptiveResponseFrameStamp(
    long Epoch,
    long StartedAtTimestamp,
    long StartedAtLegacyMilliseconds)
{
    public bool IsValid =>
        Epoch >= 0 &&
        StartedAtTimestamp >= 0 &&
        StartedAtLegacyMilliseconds >= 0;
}

/// <summary>
/// Pure conversion and ordering rules for a monotonic high-resolution clock.
/// Durations stay user-facing milliseconds; deadlines remain raw clock ticks
/// so sub-millisecond observations are not collapsed prematurely.
/// </summary>
public static class AdaptiveResponseTimeRules
{
    public const long MillisecondsPerSecond = 1_000;

    public static long DurationMillisecondsToTimestampTicksCeiling(
        long durationMilliseconds,
        long timestampFrequency)
    {
        if (durationMilliseconds < 0 || timestampFrequency <= 0) return -1;
        if (durationMilliseconds == 0) return 0;

        var ticks = decimal.Ceiling(
            (decimal)durationMilliseconds * timestampFrequency /
            MillisecondsPerSecond);
        return ticks >= long.MaxValue ? long.MaxValue : (long)ticks;
    }

    public static long DeadlineAfterMilliseconds(
        long timestamp,
        long durationMilliseconds,
        long timestampFrequency)
    {
        if (timestamp < 0) return -1;
        var durationTicks = DurationMillisecondsToTimestampTicksCeiling(
            durationMilliseconds,
            timestampFrequency);
        if (durationTicks < 0) return -1;
        return SaturatingAdd(timestamp, durationTicks);
    }

    public static bool HasReachedDeadline(
        long timestamp,
        long deadlineTimestamp) =>
        timestamp >= 0 &&
        deadlineTimestamp >= 0 &&
        timestamp >= deadlineTimestamp;

    public static long RemainingMillisecondsCeiling(
        long timestamp,
        long deadlineTimestamp,
        long timestampFrequency)
    {
        if (timestamp < 0 ||
            deadlineTimestamp < 0 ||
            timestampFrequency <= 0 ||
            timestamp >= deadlineTimestamp)
        {
            return 0;
        }

        var remainingTicks = deadlineTimestamp - timestamp;
        var milliseconds = decimal.Ceiling(
            (decimal)remainingTicks * MillisecondsPerSecond /
            timestampFrequency);
        return milliseconds >= long.MaxValue ? long.MaxValue : (long)milliseconds;
    }

    public static long ElapsedMillisecondsFloor(
        long startedAtTimestamp,
        long observedAtTimestamp,
        long timestampFrequency)
    {
        if (startedAtTimestamp < 0 ||
            observedAtTimestamp < startedAtTimestamp ||
            timestampFrequency <= 0)
        {
            return -1;
        }

        var elapsedTicks = observedAtTimestamp - startedAtTimestamp;
        var milliseconds = decimal.Floor(
            (decimal)elapsedTicks * MillisecondsPerSecond /
            timestampFrequency);
        return milliseconds >= long.MaxValue ? long.MaxValue : (long)milliseconds;
    }

    /// <summary>
    /// Projects a raw timestamp onto the existing TickCount64-style millisecond
    /// epoch. Anchoring once permits an incremental migration without comparing
    /// unrelated absolute time domains.
    /// </summary>
    public static long ProjectLegacyMilliseconds(
        long anchorTimestamp,
        long anchorLegacyMilliseconds,
        long timestamp,
        long timestampFrequency)
    {
        if (anchorLegacyMilliseconds < 0) return -1;
        var elapsed = ElapsedMillisecondsFloor(
            anchorTimestamp,
            timestamp,
            timestampFrequency);
        return elapsed < 0 ? -1 : SaturatingAdd(anchorLegacyMilliseconds, elapsed);
    }

    public static bool IsSameFrameworkFrame(
        AdaptiveResponseTimeStamp left,
        AdaptiveResponseTimeStamp right) =>
        left.IsValid &&
        right.IsValid &&
        left.FrameEpoch == right.FrameEpoch;

    public static int CompareCaptureOrder(
        AdaptiveResponseTimeStamp left,
        AdaptiveResponseTimeStamp right)
    {
        if (!left.IsValid || !right.IsValid)
            throw new ArgumentException("Both adaptive-response timestamps must be valid.");

        var timestampOrder = left.Timestamp.CompareTo(right.Timestamp);
        if (timestampOrder != 0) return timestampOrder;
        var frameOrder = left.FrameEpoch.CompareTo(right.FrameEpoch);
        return frameOrder != 0
            ? frameOrder
            : left.CaptureOrdinal.CompareTo(right.CaptureOrdinal);
    }

    private static long SaturatingAdd(long left, long right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;
}
