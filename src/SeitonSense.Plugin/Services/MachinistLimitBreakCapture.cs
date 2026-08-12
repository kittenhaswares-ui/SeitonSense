using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal readonly record struct MachinistLimitBreakWarning(
    long ObservedAtMilliseconds,
    uint CasterEntityId,
    uint TargetEntityId,
    uint GlobalSequence,
    ushort SourceSequence);

internal readonly record struct TargetPressureCaptureEvent(
    long ObservedAtMilliseconds,
    uint CasterEntityId,
    uint TargetEntityId,
    TargetPressureEvidence Evidence,
    uint GlobalSequence,
    ushort SourceSequence);

internal unsafe sealed class MachinistLimitBreakCapture : IDisposable
{
    private const int EffectSlotsPerTarget = 8;
    private const int MaximumQueuedWarnings = 64;
    private const int MaximumQueuedPressureEvents = 128;
    private const int MaximumTargetsPerAction = 32;

    private readonly IGameInteropProvider interop;
    private readonly IPluginLog log;
    private readonly ConcurrentQueue<MachinistLimitBreakWarning> pendingWarnings = new();
    private readonly ConcurrentQueue<TargetPressureCaptureEvent> pendingPressureEvents = new();

    private Hook<ActionEffectHandler.Delegates.Receive>? actionEffectHook;
    private int machinistLocalEntityIdBits;
    private int pressureLocalEntityIdBits;
    private int queuedWarningCount;
    private int queuedPressureEventCount;
    private int captureBlocked = 1;
    private long captureErrors;
    private long droppedWarnings;
    private long droppedPressureEvents;
    private bool disposed;

    public MachinistLimitBreakCapture(IGameInteropProvider interop, IPluginLog log)
    {
        this.interop = interop;
        this.log = log;
    }

    public bool IsRunning { get; private set; }
    public uint CurrentMachinistLocalEntityId => unchecked((uint)Volatile.Read(ref machinistLocalEntityIdBits));
    public uint CurrentPressureLocalEntityId => unchecked((uint)Volatile.Read(ref pressureLocalEntityIdBits));
    public int QueueDepth => Math.Max(0, Volatile.Read(ref queuedWarningCount));
    public int PressureQueueDepth => Math.Max(0, Volatile.Read(ref queuedPressureEventCount));
    public long CaptureErrors => Interlocked.Read(ref captureErrors);
    public long DroppedWarnings => Interlocked.Read(ref droppedWarnings);
    public long DroppedPressureEvents => Interlocked.Read(ref droppedPressureEvents);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsRunning) return;

        try
        {
            actionEffectHook = interop.HookFromAddress(
                (nint)ActionEffectHandler.MemberFunctionPointers.Receive,
                new ActionEffectHandler.Delegates.Receive(ActionEffectDetour));
            actionEffectHook.Enable();
            IsRunning = true;
            Volatile.Write(ref captureBlocked, 0);
        }
        catch
        {
            Volatile.Write(ref captureBlocked, 1);
            actionEffectHook?.Dispose();
            IsRunning = false;
            throw;
        }
    }

    public void SetMachinistLocalEntityId(uint entityId)
    {
        var normalized = IsNetworkEntityId(entityId) ? entityId : 0u;
        var previous = unchecked((uint)Interlocked.Exchange(
            ref machinistLocalEntityIdBits,
            unchecked((int)normalized)));
        if (previous != normalized) ClearWarnings();
    }

    public void SetPressureLocalEntityId(uint entityId)
    {
        var normalized = IsNetworkEntityId(entityId) ? entityId : 0u;
        var previous = unchecked((uint)Interlocked.Exchange(
            ref pressureLocalEntityIdBits,
            unchecked((int)normalized)));
        if (previous != normalized) ClearPressureEvents();
    }

    public bool TryDequeue(out MachinistLimitBreakWarning warning)
    {
        if (!pendingWarnings.TryDequeue(out warning)) return false;
        Interlocked.Decrement(ref queuedWarningCount);
        return true;
    }

    public bool TryDequeuePressure(out TargetPressureCaptureEvent pressureEvent)
    {
        if (!pendingPressureEvents.TryDequeue(out pressureEvent)) return false;
        Interlocked.Decrement(ref queuedPressureEventCount);
        return true;
    }

    public void ClearWarnings()
    {
        while (pendingWarnings.TryDequeue(out _))
            Interlocked.Decrement(ref queuedWarningCount);
    }

    public void ClearPressureEvents()
    {
        while (pendingPressureEvents.TryDequeue(out _))
            Interlocked.Decrement(ref queuedPressureEventCount);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Volatile.Write(ref captureBlocked, 1);
        Interlocked.Exchange(ref machinistLocalEntityIdBits, 0);
        Interlocked.Exchange(ref pressureLocalEntityIdBits, 0);
        actionEffectHook?.Dispose();
        IsRunning = false;
        ClearWarnings();
        ClearPressureEvents();
    }

    private void ActionEffectDetour(
        uint casterEntityId,
        Character* casterPointer,
        Vector3* targetPosition,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        MachinistLimitBreakWarning? capturedWarning = null;
        TargetPressureCaptureEvent? capturedPressure = null;
        try
        {
            if (Volatile.Read(ref captureBlocked) == 0)
            {
                capturedWarning = TryCaptureMachinistWarning(casterEntityId, header, effects, targetEntityIds);
                capturedPressure = TryCapturePressure(casterEntityId, header, effects, targetEntityIds);
            }
        }
        catch (Exception exception)
        {
            var errorCount = Interlocked.Increment(ref captureErrors);
            if (errorCount <= 3 || errorCount % 100 == 0)
                log.Error(exception, "Seiton Sense failed closed while reading a Machinist limit-break marker; error #{Count}.", errorCount);
        }
        finally
        {
            actionEffectHook!.OriginalDisposeSafe(
                casterEntityId,
                casterPointer,
                targetPosition,
                header,
                effects,
                targetEntityIds);
        }

        if (capturedWarning is { } warning) Enqueue(warning);
        if (capturedPressure is { } pressure) EnqueuePressure(pressure);
    }

    private MachinistLimitBreakWarning? TryCaptureMachinistWarning(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        var localEntityId = CurrentMachinistLocalEntityId;
        if (!IsNetworkEntityId(localEntityId) ||
            !IsNetworkEntityId(casterEntityId) ||
            casterEntityId == localEntityId ||
            header == null ||
            effects == null ||
            targetEntityIds == null ||
            header->NumTargets != 1)
        {
            return null;
        }

        var actionId = header->SpellId != 0 ? header->SpellId : header->ActionId;
        var targetEffects = effects[0].Effects;
        var hasAdditionalEffects = false;
        for (var slot = 1; slot < EffectSlotsPerTarget; slot++)
        {
            if (!IsEmpty(targetEffects[slot]))
            {
                hasAdditionalEffects = true;
                break;
            }
        }

        if (!MachinistLimitBreakMarkerRules.IsExactEarlyTargetMarker(
                actionId,
                header->NumTargets,
                targetEntityIds[0].ObjectId == localEntityId,
                targetEffects[0].Type,
                hasAdditionalEffects))
        {
            return null;
        }

        return new MachinistLimitBreakWarning(
            Environment.TickCount64,
            casterEntityId,
            localEntityId,
            header->GlobalSequence,
            header->SourceSequence);
    }

    private TargetPressureCaptureEvent? TryCapturePressure(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        var localEntityId = CurrentPressureLocalEntityId;
        if (!IsNetworkEntityId(localEntityId) ||
            !IsNetworkEntityId(casterEntityId) ||
            casterEntityId == localEntityId ||
            header == null ||
            effects == null ||
            targetEntityIds == null ||
            header->NumTargets is 0 or > MaximumTargetsPerAction)
        {
            return null;
        }

        var actionId = header->SpellId != 0 ? header->SpellId : header->ActionId;
        for (var index = 0; index < header->NumTargets; index++)
        {
            if (targetEntityIds[index].ObjectId != localEntityId) continue;

            var evidence = HasHarmfulPressureEffect(&effects[index])
                ? TargetPressureEvidence.RecentHarmfulAction
                : TargetPressureEvidence.None;
            if (actionId == EnemyCombatConstants.MarksmanSpiteActionId &&
                header->NumTargets == 1 &&
                effects[index].Effects[0].Type == 0x1B &&
                HasOnlyEmptyAdditionalEffects(effects[index].Effects))
            {
                evidence |= TargetPressureEvidence.MachinistLimitBreakMarker;
            }

            if (evidence == TargetPressureEvidence.None) return null;
            return new TargetPressureCaptureEvent(
                Environment.TickCount64,
                casterEntityId,
                localEntityId,
                evidence,
                header->GlobalSequence,
                header->SourceSequence);
        }

        return null;
    }

    private void Enqueue(MachinistLimitBreakWarning warning)
    {
        if (disposed ||
            Volatile.Read(ref captureBlocked) != 0 ||
            warning.TargetEntityId != CurrentMachinistLocalEntityId)
        {
            return;
        }

        var depth = Interlocked.Increment(ref queuedWarningCount);
        if (depth > MaximumQueuedWarnings)
        {
            Interlocked.Decrement(ref queuedWarningCount);
            Interlocked.Increment(ref droppedWarnings);
            return;
        }

        pendingWarnings.Enqueue(warning);
    }

    private void EnqueuePressure(TargetPressureCaptureEvent pressureEvent)
    {
        if (disposed ||
            Volatile.Read(ref captureBlocked) != 0 ||
            pressureEvent.TargetEntityId != CurrentPressureLocalEntityId)
        {
            return;
        }

        var depth = Interlocked.Increment(ref queuedPressureEventCount);
        if (depth > MaximumQueuedPressureEvents)
        {
            Interlocked.Decrement(ref queuedPressureEventCount);
            Interlocked.Increment(ref droppedPressureEvents);
            return;
        }

        pendingPressureEvents.Enqueue(pressureEvent);
    }

    private static bool HasHarmfulPressureEffect(ActionEffectHandler.TargetEffects* targetEffects)
    {
        var slots = targetEffects->Effects;
        for (var index = 0; index < slots.Length; index++)
        {
            if (slots[index].Type is 1 or 2 or 3 or 5 or 6 or 7 or 74) return true;
        }

        return false;
    }

    private static bool HasOnlyEmptyAdditionalEffects(Span<ActionEffectHandler.Effect> effects)
    {
        for (var index = 1; index < effects.Length; index++)
        {
            if (!IsEmpty(effects[index])) return false;
        }

        return true;
    }

    private static bool IsEmpty(ActionEffectHandler.Effect effect) =>
        effect.Type == 0 &&
        effect.Param0 == 0 &&
        effect.Param1 == 0 &&
        effect.Param2 == 0 &&
        effect.Param3 == 0 &&
        effect.Param4 == 0 &&
        effect.Value == 0;

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000u;
}
