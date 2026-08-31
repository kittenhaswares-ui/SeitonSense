using System.Diagnostics;
using Dalamud.Plugin.Services;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// One process-local high-resolution monotonic time source shared by every
/// response-sensitive subsystem. It also publishes an atomic framework-frame
/// epoch so equal native timestamps never erase frame ownership.
/// </summary>
internal sealed class SeitonResponseClock : IDisposable
{
    private readonly IFramework framework;
    private readonly Func<long> timestampProvider;
    private readonly long anchorTimestamp;
    private readonly long anchorLegacyMilliseconds;
    private AdaptiveResponseFrameStamp currentFrame;
    private long lastTimestamp;
    private long frameEpoch;
    private long captureOrdinal;
    private int started;
    private int disposed;

    internal SeitonResponseClock(IFramework framework)
        : this(
            framework,
            Stopwatch.GetTimestamp,
            Stopwatch.Frequency,
            Math.Max(0, Environment.TickCount64))
    {
    }

    internal SeitonResponseClock(
        IFramework framework,
        Func<long> timestampProvider,
        long timestampFrequency,
        long anchorLegacyMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentNullException.ThrowIfNull(timestampProvider);
        if (timestampFrequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(timestampFrequency));
        if (anchorLegacyMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(anchorLegacyMilliseconds));

        this.framework = framework;
        this.timestampProvider = timestampProvider;
        TimestampFrequency = timestampFrequency;
        this.anchorLegacyMilliseconds = anchorLegacyMilliseconds;
        anchorTimestamp = Math.Max(0, timestampProvider());
        lastTimestamp = anchorTimestamp;
        currentFrame = new AdaptiveResponseFrameStamp(
            0,
            anchorTimestamp,
            anchorLegacyMilliseconds);
    }

    internal long TimestampFrequency { get; }

    internal AdaptiveResponseFrameStamp CurrentFrame =>
        Volatile.Read(ref currentFrame);

    internal bool IsStarted => Volatile.Read(ref started) != 0;

    internal void Start()
    {
        if (Volatile.Read(ref disposed) != 0 ||
            Interlocked.CompareExchange(ref started, 1, 0) != 0)
        {
            return;
        }

        framework.Update += OnFrameworkUpdate;
    }

    internal AdaptiveResponseTimeStamp Capture()
    {
        while (true)
        {
            var frameBefore = Volatile.Read(ref currentFrame);
            var timestamp = CaptureMonotonicTimestamp();
            var frameAfter = Volatile.Read(ref currentFrame);
            if (!ReferenceEquals(frameBefore, frameAfter)) continue;

            var ordinal = IncrementSaturating(ref captureOrdinal);
            var legacyMilliseconds = AdaptiveResponseTimeRules.ProjectLegacyMilliseconds(
                anchorTimestamp,
                anchorLegacyMilliseconds,
                timestamp,
                TimestampFrequency);
            return new AdaptiveResponseTimeStamp(
                timestamp,
                frameAfter.Epoch,
                ordinal,
                legacyMilliseconds);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        if (Interlocked.Exchange(ref started, 0) != 0)
            framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (Volatile.Read(ref disposed) != 0) return;

        var timestamp = CaptureMonotonicTimestamp();
        var epoch = IncrementSaturating(ref frameEpoch);
        var legacyMilliseconds = AdaptiveResponseTimeRules.ProjectLegacyMilliseconds(
            anchorTimestamp,
            anchorLegacyMilliseconds,
            timestamp,
            TimestampFrequency);
        Volatile.Write(
            ref currentFrame,
            new AdaptiveResponseFrameStamp(epoch, timestamp, legacyMilliseconds));
    }

    private long CaptureMonotonicTimestamp()
    {
        var observed = Math.Max(0, timestampProvider());
        while (true)
        {
            var previous = Volatile.Read(ref lastTimestamp);
            var candidate = Math.Max(previous, observed);
            if (Interlocked.CompareExchange(ref lastTimestamp, candidate, previous) == previous)
                return candidate;
        }
    }

    private static long IncrementSaturating(ref long value)
    {
        while (true)
        {
            var previous = Volatile.Read(ref value);
            if (previous == long.MaxValue) return long.MaxValue;
            var next = previous + 1;
            if (Interlocked.CompareExchange(ref value, next, previous) == previous)
                return next;
        }
    }
}
