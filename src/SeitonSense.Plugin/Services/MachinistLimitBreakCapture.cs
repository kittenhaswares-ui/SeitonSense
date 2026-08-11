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

internal unsafe sealed class MachinistLimitBreakCapture : IDisposable
{
    private const int EffectSlotsPerTarget = 8;
    private const int MaximumQueuedWarnings = 64;

    private readonly IGameInteropProvider interop;
    private readonly IPluginLog log;
    private readonly ConcurrentQueue<MachinistLimitBreakWarning> pendingWarnings = new();

    private Hook<ActionEffectHandler.Delegates.Receive>? actionEffectHook;
    private int localEntityIdBits;
    private int queuedWarningCount;
    private int captureBlocked = 1;
    private long captureErrors;
    private long droppedWarnings;
    private bool disposed;

    public MachinistLimitBreakCapture(IGameInteropProvider interop, IPluginLog log)
    {
        this.interop = interop;
        this.log = log;
    }

    public bool IsRunning { get; private set; }
    public uint CurrentLocalEntityId => unchecked((uint)Volatile.Read(ref localEntityIdBits));
    public int QueueDepth => Math.Max(0, Volatile.Read(ref queuedWarningCount));
    public long CaptureErrors => Interlocked.Read(ref captureErrors);
    public long DroppedWarnings => Interlocked.Read(ref droppedWarnings);

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

    public void SetLocalEntityId(uint entityId)
    {
        var normalized = IsNetworkEntityId(entityId) ? entityId : 0u;
        var previous = unchecked((uint)Interlocked.Exchange(
            ref localEntityIdBits,
            unchecked((int)normalized)));
        if (previous != normalized) ClearWarnings();
    }

    public bool TryDequeue(out MachinistLimitBreakWarning warning)
    {
        if (!pendingWarnings.TryDequeue(out warning)) return false;
        Interlocked.Decrement(ref queuedWarningCount);
        return true;
    }

    public void ClearWarnings()
    {
        while (pendingWarnings.TryDequeue(out _))
            Interlocked.Decrement(ref queuedWarningCount);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Volatile.Write(ref captureBlocked, 1);
        Interlocked.Exchange(ref localEntityIdBits, 0);
        actionEffectHook?.Dispose();
        IsRunning = false;
        ClearWarnings();
    }

    private void ActionEffectDetour(
        uint casterEntityId,
        Character* casterPointer,
        Vector3* targetPosition,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        MachinistLimitBreakWarning? captured = null;
        try
        {
            if (Volatile.Read(ref captureBlocked) == 0)
                captured = TryCapture(casterEntityId, header, effects, targetEntityIds);
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

        if (captured is { } warning) Enqueue(warning);
    }

    private MachinistLimitBreakWarning? TryCapture(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        var localEntityId = CurrentLocalEntityId;
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

    private void Enqueue(MachinistLimitBreakWarning warning)
    {
        if (disposed ||
            Volatile.Read(ref captureBlocked) != 0 ||
            warning.TargetEntityId != CurrentLocalEntityId)
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
