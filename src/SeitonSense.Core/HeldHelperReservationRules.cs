namespace SeitonSense.Core;

/// <summary>
/// Release-side consent for one already-frozen held-helper intent. The helper
/// still owns and revalidates the exact action, target, actor, context, and
/// episode; this policy only decides whether the original physical key consent
/// may survive key-up for the configured bounded response window.
/// </summary>
public static class HeldHelperReservationRules
{
    public static bool CanObserveExactFrozenOwnership(
        PhysicalGameplayKeyState generation,
        bool exactFrozenOwnershipAlreadyObserved,
        bool ownershipRetiredUntilRelease,
        bool textInputActive) =>
        generation.IsPrimed &&
        generation.IsDown &&
        !ownershipRetiredUntilRelease &&
        !textInputActive &&
        (exactFrozenOwnershipAlreadyObserved ||
         (generation.IsEligible && !generation.IsConsumed));

    public static bool CanBeginReleaseReservation(
        bool exactFrozenOwnershipObserved,
        bool textInputActive) =>
        !textInputActive &&
        exactFrozenOwnershipObserved;

    public static int NormalizeWindowMilliseconds(int requestedMilliseconds) =>
        Math.Clamp(
            requestedMilliseconds,
            HeldActionRetryRules.MinimumLatencyResponseWindowMilliseconds,
            HeldActionRetryRules.MaximumLatencyResponseWindowMilliseconds);

    public static long ReleaseDeadlineMilliseconds(
        long releasedAtMilliseconds,
        int requestedWindowMilliseconds)
    {
        if (releasedAtMilliseconds < 0) return -1;
        var window = NormalizeWindowMilliseconds(requestedWindowMilliseconds);
        return releasedAtMilliseconds > long.MaxValue - window
            ? long.MaxValue
            : releasedAtMilliseconds + window;
    }

    /// <summary>
    /// Physical hold remains authoritative. After key-up, only the same
    /// eligible input generation may retain an already-frozen intent, and only
    /// before its absolute deadline. Text input, exact-intent drift, a newer
    /// physical key generation, disabled policy, or invalid telemetry fail
    /// closed. The deadline is exclusive.
    /// </summary>
    public static bool IsFrozenConsentValid(
        bool physicallyDown,
        bool releaseWasEligible,
        long releasedAtMilliseconds,
        long releaseInputGeneration,
        long currentInputGeneration,
        bool reservationEnabled,
        int reservationWindowMilliseconds,
        long nowMilliseconds,
        bool textInputActive,
        bool exactIntentValid = true)
    {
        if (!exactIntentValid || textInputActive || nowMilliseconds < 0)
            return false;
        if (physicallyDown) return true;
        if (!reservationEnabled ||
            !releaseWasEligible ||
            releasedAtMilliseconds < 0 ||
            releaseInputGeneration < 0 ||
            currentInputGeneration != releaseInputGeneration ||
            nowMilliseconds < releasedAtMilliseconds)
        {
            return false;
        }

        var deadline = ReleaseDeadlineMilliseconds(
            releasedAtMilliseconds,
            reservationWindowMilliseconds);
        return deadline >= 0 && nowMilliseconds < deadline;
    }
}
