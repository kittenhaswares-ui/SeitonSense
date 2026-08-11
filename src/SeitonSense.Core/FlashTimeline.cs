namespace SeitonSense.Core;

public static class FlashTimeline
{
    public static float Remaining01(
        long nowMilliseconds,
        long startedAtMilliseconds,
        long endsAtMilliseconds)
    {
        var duration = endsAtMilliseconds - startedAtMilliseconds;
        if (duration <= 0 || nowMilliseconds < startedAtMilliseconds || nowMilliseconds >= endsAtMilliseconds)
            return 0f;

        return Math.Clamp(
            (endsAtMilliseconds - nowMilliseconds) / (float)duration,
            0f,
            1f);
    }
}
