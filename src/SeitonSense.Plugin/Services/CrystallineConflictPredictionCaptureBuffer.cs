using System.Collections.Concurrent;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal readonly record struct CrystallineConflictObservedStatCapture(
    long ObservedAtMilliseconds,
    int FeatureGeneration,
    uint CasterEntityId,
    uint TargetEntityId,
    CrystallineConflictObservedEffectKind Kind,
    uint Amount,
    bool AppliedToSource,
    uint GlobalSequence,
    ushort SourceSequence);

/// <summary>
/// Bounded sidecar for the plugin's existing shared ActionEffect hook. These
/// are deliberately labelled observed live totals: periodic HoT/DoT ticks and
/// medicine-kit actor controls use different native boundaries. The exact
/// post-match packet replaces them when it arrives.
/// </summary>
internal unsafe sealed class CrystallineConflictPredictionCaptureBuffer
{
    private const int EffectSlotsPerTarget = 8;
    private const int MaximumTargetsPerAction = 32;
    private const int MaximumQueuedEffects = 2_048;

    private readonly ConcurrentQueue<CrystallineConflictObservedStatCapture> pending = new();
    private int enabled;
    private int featureGeneration;
    private int queued;
    private long captured;
    private long dropped;

    internal bool Enabled => Volatile.Read(ref enabled) != 0;
    internal int FeatureGeneration => Volatile.Read(ref featureGeneration);
    internal int QueueDepth => Math.Max(0, Volatile.Read(ref queued));
    internal long Captured => Interlocked.Read(ref captured);
    internal long Dropped => Interlocked.Read(ref dropped);

    internal void SetEnabled(bool value)
    {
        var next = value ? 1 : 0;
        if (Interlocked.Exchange(ref enabled, next) == next) return;
        Interlocked.Increment(ref featureGeneration);
        Clear();
    }

    internal void Capture(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        if (!Enabled ||
            !IsNetworkEntityId(casterEntityId) ||
            header == null ||
            header->NumTargets == 0 ||
            header->NumTargets > MaximumTargetsPerAction ||
            effects == null ||
            targetEntityIds == null)
        {
            return;
        }

        var generation = FeatureGeneration;
        var now = Environment.TickCount64;
        for (var targetIndex = 0; targetIndex < header->NumTargets; targetIndex++)
        {
            var targetEntityId = targetEntityIds[targetIndex].ObjectId;
            if (!IsNetworkEntityId(targetEntityId)) continue;

            var targetEffects = effects[targetIndex].Effects;
            for (var slot = 0; slot < EffectSlotsPerTarget; slot++)
            {
                var native = targetEffects[slot];
                if (!CrystallineConflictPredictionRules.TryDecodeObservedEffectAmount(
                        native.Type,
                        native.Param3,
                        native.Param4,
                        native.Value,
                        out var effect))
                {
                    continue;
                }

                Enqueue(new CrystallineConflictObservedStatCapture(
                    now,
                    generation,
                    casterEntityId,
                    targetEntityId,
                    effect.Kind,
                    effect.Amount,
                    effect.AppliedToSource,
                    header->GlobalSequence,
                    header->SourceSequence));
            }
        }
    }

    internal bool TryDequeue(out CrystallineConflictObservedStatCapture capture)
    {
        while (pending.TryDequeue(out capture))
        {
            Interlocked.Decrement(ref queued);
            if (Enabled && capture.FeatureGeneration == FeatureGeneration) return true;
        }

        capture = default;
        return false;
    }

    internal void Clear()
    {
        while (pending.TryDequeue(out _)) Interlocked.Decrement(ref queued);
    }

    private void Enqueue(CrystallineConflictObservedStatCapture capture)
    {
        if (Interlocked.Increment(ref queued) > MaximumQueuedEffects)
        {
            Interlocked.Decrement(ref queued);
            Interlocked.Increment(ref dropped);
            return;
        }

        pending.Enqueue(capture);
        Interlocked.Increment(ref captured);
    }

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not (0 or 0xE0000000u);
}
