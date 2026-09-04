namespace SeitonSense.Core;

/// <summary>
/// Tracks native readiness only within one frozen Mannstopper intent. A real
/// busy-to-ready edge can wake its clean-false retry before the normal timer;
/// repeated ready samples, unknown samples, and another intent cannot do so.
/// </summary>
public sealed class BardRepellingShotRetryGate
{
    private ulong intentEpoch;
    private long observedFrame = -1;
    private bool readinessKnown;
    private bool nativeReady;
    private long lastNativeAttemptFrame = -1;

    public bool Observe(
        ulong currentIntentEpoch,
        HeldActionRetryState retry,
        bool currentReadinessKnown,
        bool currentNativeReady,
        bool edgeDrivenRetriesEnabled,
        long frameId,
        long nowMilliseconds)
    {
        if (currentIntentEpoch == 0)
        {
            ClearEpisode();
            return false;
        }

        var becameReady = currentIntentEpoch == intentEpoch &&
                          readinessKnown && currentReadinessKnown &&
                          !nativeReady && currentNativeReady &&
                          observedFrame >= 0 && frameId > observedFrame;
        intentEpoch = currentIntentEpoch;
        observedFrame = frameId;
        readinessKnown = currentReadinessKnown;
        nativeReady = currentNativeReady;

        if (!currentReadinessKnown || !currentNativeReady ||
            frameId < 0 || frameId == lastNativeAttemptFrame)
        {
            return false;
        }

        return edgeDrivenRetriesEnabled
            ? HeldActionRetryRules.CanAttemptFrozenIntentOnBoundaryEdgeOrThrottle(
                retry,
                nowMilliseconds,
                frameId,
                lastNativeAttemptFrame,
                becameReady)
            : HeldActionRetryRules.CanAttemptFrozenIntent(retry, nowMilliseconds);
    }

    // Reserve before the final native preflight so a synchronous callback
    // cannot invoke the same helper twice in one framework frame. A blocked
    // preflight still spends no native retry budget and resumes next frame.
    public void ReserveAttemptFrame(long frameId)
    {
        if (frameId >= 0) lastNativeAttemptFrame = frameId;
    }

    public void ClearEpisode()
    {
        intentEpoch = 0;
        observedFrame = -1;
        readinessKnown = false;
        nativeReady = false;
    }

    public void Reset()
    {
        ClearEpisode();
        lastNativeAttemptFrame = -1;
    }
}
