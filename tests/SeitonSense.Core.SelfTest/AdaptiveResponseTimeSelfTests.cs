using SeitonSense.Core;

internal static class AdaptiveResponseTimeSelfTests
{
    internal static void RawTickConversionsAreExactAndSaturating()
    {
        Equal(
            10_000L,
            AdaptiveResponseTimeRules.DurationMillisecondsToTimestampTicksCeiling(
                1,
                10_000_000),
            "one millisecond at ten MHz");
        Equal(
            1L,
            AdaptiveResponseTimeRules.DurationMillisecondsToTimestampTicksCeiling(1, 3),
            "fractional raw ticks round up");
        Equal(
            0L,
            AdaptiveResponseTimeRules.DurationMillisecondsToTimestampTicksCeiling(0, 3),
            "zero duration");
        Equal(
            -1L,
            AdaptiveResponseTimeRules.DurationMillisecondsToTimestampTicksCeiling(-1, 3),
            "negative duration fails closed");
        Equal(
            -1L,
            AdaptiveResponseTimeRules.DurationMillisecondsToTimestampTicksCeiling(1, 0),
            "invalid frequency fails closed");
        Equal(
            long.MaxValue,
            AdaptiveResponseTimeRules.DeadlineAfterMilliseconds(
                long.MaxValue - 2,
                10,
                1_000),
            "deadline addition saturates");
    }

    internal static void DeadlineAndRemainingBoundariesAreExact()
    {
        var deadline = AdaptiveResponseTimeRules.DeadlineAfterMilliseconds(
            100,
            1_000,
            1_000);
        Equal(1_100L, deadline, "one-second raw deadline");
        Equal(
            1_000L,
            AdaptiveResponseTimeRules.RemainingMillisecondsCeiling(100, deadline, 1_000),
            "full remaining window");
        Equal(
            1L,
            AdaptiveResponseTimeRules.RemainingMillisecondsCeiling(1_099, deadline, 1_000),
            "last raw tick remains visible");
        Equal(
            0L,
            AdaptiveResponseTimeRules.RemainingMillisecondsCeiling(1_100, deadline, 1_000),
            "exact deadline expires");
        False(
            AdaptiveResponseTimeRules.HasReachedDeadline(1_099, deadline),
            "before deadline");
        True(
            AdaptiveResponseTimeRules.HasReachedDeadline(1_100, deadline),
            "at deadline");

        var subMillisecondDeadline = AdaptiveResponseTimeRules.DeadlineAfterMilliseconds(
            0,
            1,
            10_000);
        Equal(10L, subMillisecondDeadline, "one millisecond keeps raw precision");
        Equal(
            1L,
            AdaptiveResponseTimeRules.RemainingMillisecondsCeiling(
                9,
                subMillisecondDeadline,
                10_000),
            "a positive sub-millisecond remainder rounds up");
    }

    internal static void AnchoredLegacyProjectionIsIncrementalAndMonotonic()
    {
        Equal(
            20_500L,
            AdaptiveResponseTimeRules.ProjectLegacyMilliseconds(
                10_000,
                20_000,
                10_500,
                1_000),
            "legacy projection shares the existing epoch");
        Equal(
            20_000L,
            AdaptiveResponseTimeRules.ProjectLegacyMilliseconds(
                10_000,
                20_000,
                10_009,
                10_000),
            "sub-millisecond raw progress does not invent a millisecond");
        Equal(
            -1L,
            AdaptiveResponseTimeRules.ProjectLegacyMilliseconds(
                10_000,
                20_000,
                9_999,
                1_000),
            "regressed raw time fails closed");
        Equal(
            long.MaxValue,
            AdaptiveResponseTimeRules.ProjectLegacyMilliseconds(
                0,
                long.MaxValue - 1,
                10,
                1_000),
            "legacy projection saturates");
    }

    internal static void EqualRawTimestampsRemainTotallyOrdered()
    {
        var first = new AdaptiveResponseTimeStamp(500, 10, 20, 1_000);
        var second = new AdaptiveResponseTimeStamp(500, 10, 21, 1_000);
        var nextFrame = new AdaptiveResponseTimeStamp(500, 11, 22, 1_000);

        True(first.IsValid, "first stamp is valid");
        True(second.IsValid, "second stamp is valid");
        True(
            AdaptiveResponseTimeRules.IsSameFrameworkFrame(first, second),
            "equal frame epochs share ownership");
        False(
            AdaptiveResponseTimeRules.IsSameFrameworkFrame(second, nextFrame),
            "later frame is distinct even at the same raw timestamp");
        True(
            AdaptiveResponseTimeRules.CompareCaptureOrder(first, second) < 0,
            "capture ordinal orders equal raw timestamps");
        True(
            AdaptiveResponseTimeRules.CompareCaptureOrder(second, nextFrame) < 0,
            "frame epoch orders equal raw timestamps before ordinal");

        var frame = new AdaptiveResponseFrameStamp(11, 500, 1_000);
        True(frame.IsValid, "immutable frame stamp is valid");
        False(AdaptiveResponseTimeStamp.Invalid.IsValid, "invalid stamp fails closed");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
