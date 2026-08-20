using System.Collections.Concurrent;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal readonly record struct CombatLimitBreakActivationCapture(
    long ObservedAtMilliseconds,
    int FeatureGeneration,
    uint CasterEntityId,
    uint JobId,
    uint ActionId,
    uint IconId,
    uint GlobalSequence,
    ushort SourceSequence);

internal readonly record struct CombatLimitBreakDamageCapture(
    long ObservedAtMilliseconds,
    int FeatureGeneration,
    uint CasterEntityId,
    uint TargetEntityId,
    uint JobId,
    uint ActionId,
    uint IconId,
    uint Damage,
    uint GlobalSequence,
    ushort SourceSequence);

/// <summary>
/// Bounded, value-only sidecar for the plugin's existing single
/// ActionEffectHandler.Receive hook. It must be called from that detour; it does
/// not install a competing native hook and retains no pointers or actor names.
/// </summary>
internal unsafe sealed class CombatLimitBreakCaptureBuffer
{
    private const int EffectSlotsPerTarget = 8;
    private const int MaximumTargetsPerAction = 32;
    private const int MaximumQueuedActivations = 64;
    private const int MaximumQueuedDamageEvents = 256;

    private readonly ConcurrentQueue<CombatLimitBreakActivationCapture> pendingActivations = new();
    private readonly ConcurrentQueue<CombatLimitBreakDamageCapture> pendingDamageEvents = new();
    // 0 = disabled, 1 = activation capture only, 2 = activation + direct
    // ally-damage capture. One packed mode keeps the hook-side gate atomic.
    private int captureMode;
    private int featureGeneration;
    private int queuedActivations;
    private int queuedDamageEvents;
    private long capturedActivations;
    private long capturedDamageEvents;
    private long droppedActivations;
    private long droppedDamageEvents;

    internal bool Enabled => Volatile.Read(ref captureMode) != 0;
    internal bool DamageEnabled => Volatile.Read(ref captureMode) == 2;
    internal int FeatureGeneration => Volatile.Read(ref featureGeneration);
    internal int ActivationQueueDepth => Math.Max(0, Volatile.Read(ref queuedActivations));
    internal int DamageQueueDepth => Math.Max(0, Volatile.Read(ref queuedDamageEvents));
    internal long CapturedActivations => Interlocked.Read(ref capturedActivations);
    internal long CapturedDamageEvents => Interlocked.Read(ref capturedDamageEvents);
    internal long DroppedActivations => Interlocked.Read(ref droppedActivations);
    internal long DroppedDamageEvents => Interlocked.Read(ref droppedDamageEvents);

    internal void SetEnabled(bool value, bool includeDamage = false)
    {
        var next = value ? includeDamage ? 2 : 1 : 0;
        if (Interlocked.Exchange(ref captureMode, next) == next) return;
        Interlocked.Increment(ref featureGeneration);
        // A mode change starts a new capture generation. This also drops any
        // damage queued immediately before the display leaf was disabled.
        Clear();
    }

    internal void Capture(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        if (!Enabled ||
            !CombatLimitBreakEventRules.IsNetworkEntityId(casterEntityId) ||
            header == null ||
            header->NumTargets > MaximumTargetsPerAction)
        {
            return;
        }

        var actionId = header->SpellId != 0 ? header->SpellId : header->ActionId;
        if (!CombatLimitBreakCatalog.TryFindByAction(actionId, out var definition, out var action))
            return;

        var now = Environment.TickCount64;
        var generation = FeatureGeneration;
        var iconId = CombatLimitBreakCatalog.ResolveIconId(definition, action);
        if (CombatLimitBreakCatalog.IsActivation(action))
        {
            Enqueue(new CombatLimitBreakActivationCapture(
                now,
                generation,
                casterEntityId,
                definition.JobId,
                actionId,
                iconId,
                header->GlobalSequence,
                header->SourceSequence));
        }

        if (!DamageEnabled ||
            !CombatLimitBreakCatalog.IsDirectlyAttributableDamage(action) ||
            header->NumTargets == 0 ||
            effects == null ||
            targetEntityIds == null)
        {
            return;
        }

        for (var targetIndex = 0; targetIndex < header->NumTargets; targetIndex++)
        {
            var targetEntityId = targetEntityIds[targetIndex].ObjectId;
            if (!CombatLimitBreakEventRules.IsNetworkEntityId(targetEntityId)) continue;

            ulong targetDamage = 0;
            var targetEffects = effects[targetIndex].Effects;
            for (var slot = 0; slot < EffectSlotsPerTarget; slot++)
            {
                var effect = targetEffects[slot];
                if (!CombatLimitBreakEventRules.TryDecodeDirectDamage(
                        effect.Type,
                        effect.Param3,
                        effect.Param4,
                        effect.Value,
                        out var damage))
                {
                    continue;
                }

                targetDamage = Math.Min(uint.MaxValue, targetDamage + damage);
            }

            if (targetDamage == 0) continue;
            Enqueue(new CombatLimitBreakDamageCapture(
                now,
                generation,
                casterEntityId,
                targetEntityId,
                definition.JobId,
                actionId,
                iconId,
                (uint)targetDamage,
                header->GlobalSequence,
                header->SourceSequence));
        }
    }

    internal bool TryDequeueActivation(out CombatLimitBreakActivationCapture activation)
    {
        while (pendingActivations.TryDequeue(out activation))
        {
            Interlocked.Decrement(ref queuedActivations);
            if (Enabled && activation.FeatureGeneration == FeatureGeneration) return true;
        }

        activation = default;
        return false;
    }

    internal bool TryDequeueDamage(out CombatLimitBreakDamageCapture damage)
    {
        while (pendingDamageEvents.TryDequeue(out damage))
        {
            Interlocked.Decrement(ref queuedDamageEvents);
            if (Enabled && damage.FeatureGeneration == FeatureGeneration) return true;
        }

        damage = default;
        return false;
    }

    internal void Clear()
    {
        while (pendingActivations.TryDequeue(out _))
            Interlocked.Decrement(ref queuedActivations);
        while (pendingDamageEvents.TryDequeue(out _))
            Interlocked.Decrement(ref queuedDamageEvents);
    }

    private void Enqueue(CombatLimitBreakActivationCapture activation)
    {
        if (Interlocked.Increment(ref queuedActivations) > MaximumQueuedActivations)
        {
            Interlocked.Decrement(ref queuedActivations);
            Interlocked.Increment(ref droppedActivations);
            return;
        }

        pendingActivations.Enqueue(activation);
        Interlocked.Increment(ref capturedActivations);
    }

    private void Enqueue(CombatLimitBreakDamageCapture damage)
    {
        if (Interlocked.Increment(ref queuedDamageEvents) > MaximumQueuedDamageEvents)
        {
            Interlocked.Decrement(ref queuedDamageEvents);
            Interlocked.Increment(ref droppedDamageEvents);
            return;
        }

        pendingDamageEvents.Enqueue(damage);
        Interlocked.Increment(ref capturedDamageEvents);
    }
}
