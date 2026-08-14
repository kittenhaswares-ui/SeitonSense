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

internal readonly record struct AllyRescueCleanseEffect(
    long ObservedAtMilliseconds,
    uint CasterEntityId,
    uint TargetEntityId,
    uint ActionId,
    uint RemovedStatusId,
    uint GlobalSequence,
    ushort SourceSequence);

internal readonly record struct MiracleInterceptThreatEvent(
    long ObservedAtMilliseconds,
    uint LocalEntityId,
    uint CasterEntityId,
    uint EventTargetEntityId,
    uint ActionId,
    byte EffectType,
    ushort EffectValue,
    int FeatureGeneration,
    uint GlobalSequence,
    ushort SourceSequence);

internal readonly record struct MiracleInterceptLandedEffect(
    long ObservedAtMilliseconds,
    uint CasterEntityId,
    uint TargetEntityId,
    uint ActionId,
    byte EffectType,
    ushort EffectValue,
    uint GlobalSequence,
    ushort SourceSequence);

internal unsafe sealed class MachinistLimitBreakCapture : IDisposable
{
    private const int EffectSlotsPerTarget = 8;
    private const int MaximumQueuedWarnings = 64;
    private const int MaximumQueuedAllyRescueCleanses = 64;
    private const int MaximumQueuedMiracleInterceptThreats = 64;
    private const int MaximumQueuedMiracleInterceptConfirmations = 64;
    private const int MaximumQueuedPressureEvents = 128;
    private const int MaximumTargetsPerAction = 32;
    private const uint WardensPaeanActionId = 29400;
    private const uint AquaveilActionId = 29227;
    private const byte RemoveStatusEffectType = 0x10;

    private readonly IGameInteropProvider interop;
    private readonly IPluginLog log;
    private readonly ConcurrentQueue<MachinistLimitBreakWarning> pendingWarnings = new();
    private readonly ConcurrentQueue<AllyRescueCleanseEffect> pendingAllyRescueCleanses = new();
    private readonly ConcurrentQueue<MiracleInterceptThreatEvent> pendingMiracleInterceptThreats = new();
    private readonly ConcurrentQueue<MiracleInterceptLandedEffect> pendingMiracleInterceptConfirmations = new();
    private readonly ConcurrentQueue<TargetPressureCaptureEvent> pendingPressureEvents = new();

    private Hook<ActionEffectHandler.Delegates.Receive>? actionEffectHook;
    private int machinistLocalEntityIdBits;
    private int allyRescueLocalEntityIdBits;
    private int miracleInterceptLocalEntityIdBits;
    private int miracleCleanseFollowupLocalEntityIdBits;
    private int miracleCleanseFollowupGeneration;
    private int pressureLocalEntityIdBits;
    private int queuedWarningCount;
    private int queuedAllyRescueCleanseCount;
    private int queuedMiracleInterceptThreatCount;
    private int queuedMiracleInterceptConfirmationCount;
    private int queuedPressureEventCount;
    private int captureBlocked = 1;
    private long captureErrors;
    private long droppedWarnings;
    private long capturedAllyRescueCleanses;
    private long droppedAllyRescueCleanses;
    private long capturedMiracleInterceptThreats;
    private long droppedMiracleInterceptThreats;
    private long capturedMiracleInterceptConfirmations;
    private long droppedMiracleInterceptConfirmations;
    private long droppedPressureEvents;
    private bool disposed;

    public MachinistLimitBreakCapture(IGameInteropProvider interop, IPluginLog log)
    {
        this.interop = interop;
        this.log = log;
    }

    public bool IsRunning { get; private set; }
    public uint CurrentMachinistLocalEntityId => unchecked((uint)Volatile.Read(ref machinistLocalEntityIdBits));
    public uint CurrentAllyRescueLocalEntityId => unchecked((uint)Volatile.Read(ref allyRescueLocalEntityIdBits));
    public uint CurrentMiracleInterceptLocalEntityId => unchecked((uint)Volatile.Read(ref miracleInterceptLocalEntityIdBits));
    public uint CurrentMiracleCleanseFollowupLocalEntityId =>
        unchecked((uint)Volatile.Read(ref miracleCleanseFollowupLocalEntityIdBits));
    public int CurrentMiracleCleanseFollowupGeneration => Volatile.Read(ref miracleCleanseFollowupGeneration);
    public uint CurrentPressureLocalEntityId => unchecked((uint)Volatile.Read(ref pressureLocalEntityIdBits));
    public int QueueDepth => Math.Max(0, Volatile.Read(ref queuedWarningCount));
    public int AllyRescueCleanseQueueDepth => Math.Max(0, Volatile.Read(ref queuedAllyRescueCleanseCount));
    public int MiracleInterceptQueueDepth => Math.Max(0, Volatile.Read(ref queuedMiracleInterceptThreatCount));
    public int MiracleInterceptConfirmationQueueDepth =>
        Math.Max(0, Volatile.Read(ref queuedMiracleInterceptConfirmationCount));
    public int PressureQueueDepth => Math.Max(0, Volatile.Read(ref queuedPressureEventCount));
    public long CaptureErrors => Interlocked.Read(ref captureErrors);
    public long DroppedWarnings => Interlocked.Read(ref droppedWarnings);
    public long CapturedAllyRescueCleanses => Interlocked.Read(ref capturedAllyRescueCleanses);
    public long DroppedAllyRescueCleanses => Interlocked.Read(ref droppedAllyRescueCleanses);
    public long CapturedMiracleInterceptThreats => Interlocked.Read(ref capturedMiracleInterceptThreats);
    public long DroppedMiracleInterceptThreats => Interlocked.Read(ref droppedMiracleInterceptThreats);
    public long CapturedMiracleInterceptConfirmations => Interlocked.Read(ref capturedMiracleInterceptConfirmations);
    public long DroppedMiracleInterceptConfirmations => Interlocked.Read(ref droppedMiracleInterceptConfirmations);
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

    public void SetAllyRescueLocalEntityId(uint entityId)
    {
        var normalized = IsNetworkEntityId(entityId) ? entityId : 0u;
        var previous = unchecked((uint)Interlocked.Exchange(
            ref allyRescueLocalEntityIdBits,
            unchecked((int)normalized)));
        if (previous != normalized) ClearAllyRescueCleanses();
    }

    public void SetMiracleInterceptLocalEntityId(uint entityId)
    {
        var normalized = IsNetworkEntityId(entityId) ? entityId : 0u;
        var previous = unchecked((uint)Interlocked.Exchange(
            ref miracleInterceptLocalEntityIdBits,
            unchecked((int)normalized)));
        if (previous != normalized)
        {
            ClearMiracleInterceptThreats();
            ClearMiracleInterceptConfirmations();
        }
    }

    public void SetMiracleCleanseFollowupLocalEntityId(uint entityId)
    {
        var normalized = IsNetworkEntityId(entityId) ? entityId : 0u;
        var previous = unchecked((uint)Interlocked.Exchange(
            ref miracleCleanseFollowupLocalEntityIdBits,
            unchecked((int)normalized)));
        if (previous != normalized) Interlocked.Increment(ref miracleCleanseFollowupGeneration);
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

    public bool TryDequeueAllyRescueCleanse(out AllyRescueCleanseEffect cleanse)
    {
        if (!pendingAllyRescueCleanses.TryDequeue(out cleanse)) return false;
        Interlocked.Decrement(ref queuedAllyRescueCleanseCount);
        return true;
    }

    public bool TryDequeueMiracleInterceptThreat(out MiracleInterceptThreatEvent threat)
    {
        if (!pendingMiracleInterceptThreats.TryDequeue(out threat)) return false;
        Interlocked.Decrement(ref queuedMiracleInterceptThreatCount);
        return true;
    }

    public bool TryDequeueMiracleInterceptConfirmation(out MiracleInterceptLandedEffect confirmation)
    {
        if (!pendingMiracleInterceptConfirmations.TryDequeue(out confirmation)) return false;
        Interlocked.Decrement(ref queuedMiracleInterceptConfirmationCount);
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

    public void ClearAllyRescueCleanses()
    {
        while (pendingAllyRescueCleanses.TryDequeue(out _))
            Interlocked.Decrement(ref queuedAllyRescueCleanseCount);
    }

    public void ClearMiracleInterceptThreats()
    {
        while (pendingMiracleInterceptThreats.TryDequeue(out _))
            Interlocked.Decrement(ref queuedMiracleInterceptThreatCount);
    }

    public void ClearMiracleInterceptConfirmations()
    {
        while (pendingMiracleInterceptConfirmations.TryDequeue(out _))
            Interlocked.Decrement(ref queuedMiracleInterceptConfirmationCount);
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
        Interlocked.Exchange(ref allyRescueLocalEntityIdBits, 0);
        Interlocked.Exchange(ref miracleInterceptLocalEntityIdBits, 0);
        Interlocked.Exchange(ref miracleCleanseFollowupLocalEntityIdBits, 0);
        Interlocked.Increment(ref miracleCleanseFollowupGeneration);
        Interlocked.Exchange(ref pressureLocalEntityIdBits, 0);
        actionEffectHook?.Dispose();
        IsRunning = false;
        ClearWarnings();
        ClearAllyRescueCleanses();
        ClearMiracleInterceptThreats();
        ClearMiracleInterceptConfirmations();
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
        AllyRescueCleanseEffect? capturedAllyRescueCleanse = null;
        MiracleInterceptThreatEvent? capturedMiracleInterceptThreat = null;
        MiracleInterceptLandedEffect? capturedMiracleInterceptConfirmation = null;
        TargetPressureCaptureEvent? capturedPressure = null;
        try
        {
            if (Volatile.Read(ref captureBlocked) == 0)
            {
                capturedWarning = TryCaptureMachinistWarning(casterEntityId, header, effects, targetEntityIds);
                capturedAllyRescueCleanse = TryCaptureAllyRescueCleanse(
                    casterEntityId,
                    header,
                    effects,
                    targetEntityIds);
                capturedMiracleInterceptThreat = TryCaptureMiracleInterceptThreat(
                    casterEntityId,
                    header,
                    effects,
                    targetEntityIds);
                capturedMiracleInterceptConfirmation = TryCaptureMiracleInterceptConfirmation(
                    casterEntityId,
                    header,
                    effects,
                    targetEntityIds);
                capturedPressure = TryCapturePressure(casterEntityId, header, effects, targetEntityIds);
            }
        }
        catch (Exception exception)
        {
            var errorCount = Interlocked.Increment(ref captureErrors);
            if (errorCount <= 3 || errorCount % 100 == 0)
                log.Error(exception, "Seiton Sense failed closed while reading a bounded action-effect signal; error #{Count}.", errorCount);
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
        if (capturedAllyRescueCleanse is { } cleanse) EnqueueAllyRescueCleanse(cleanse);
        if (capturedMiracleInterceptThreat is { } miracleThreat)
            EnqueueMiracleInterceptThreat(miracleThreat);
        if (capturedMiracleInterceptConfirmation is { } miracleConfirmation)
            EnqueueMiracleInterceptConfirmation(miracleConfirmation);
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

    private AllyRescueCleanseEffect? TryCaptureAllyRescueCleanse(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        var localEntityId = CurrentAllyRescueLocalEntityId;
        if (!IsNetworkEntityId(localEntityId) ||
            casterEntityId != localEntityId ||
            header == null ||
            effects == null ||
            targetEntityIds == null ||
            header->NumTargets is 0 or > MaximumTargetsPerAction)
        {
            return null;
        }

        var actionId = header->SpellId != 0 ? header->SpellId : header->ActionId;
        if (actionId is not (WardensPaeanActionId or AquaveilActionId)) return null;

        for (var targetIndex = 0; targetIndex < header->NumTargets; targetIndex++)
        {
            var targetEntityId = targetEntityIds[targetIndex].ObjectId;
            if (!IsNetworkEntityId(targetEntityId) || targetEntityId == localEntityId) continue;

            var targetEffects = effects[targetIndex].Effects;
            for (var slot = 0; slot < EffectSlotsPerTarget; slot++)
            {
                var effect = targetEffects[slot];
                if (effect.Type != RemoveStatusEffectType ||
                    !IsPurifyRemovableStatus(effect.Value))
                {
                    continue;
                }

                return new AllyRescueCleanseEffect(
                    Environment.TickCount64,
                    casterEntityId,
                    targetEntityId,
                    actionId,
                    effect.Value,
                    header->GlobalSequence,
                    header->SourceSequence);
            }
        }

        return null;
    }

    private MiracleInterceptThreatEvent? TryCaptureMiracleInterceptThreat(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        if (!IsNetworkEntityId(casterEntityId) ||
            header == null ||
            effects == null ||
            targetEntityIds == null ||
            header->NumTargets != 1)
        {
            return null;
        }

        var actionId = header->SpellId != 0 ? header->SpellId : header->ActionId;
        if (actionId is not (
                EnemyCombatConstants.MarksmanSpiteActionId or
                EnemyCombatConstants.ZantetsukenActionId or
                EnemyCombatConstants.FuriousBacklashActionId or
                EnemyCombatConstants.PurifyActionId))
        {
            return null;
        }

        var localEntityId = actionId == EnemyCombatConstants.PurifyActionId
            ? CurrentMiracleCleanseFollowupLocalEntityId
            : CurrentMiracleInterceptLocalEntityId;
        if (!IsNetworkEntityId(localEntityId) || casterEntityId == localEntityId)
            return null;

        var targetEntityId = targetEntityIds[0].ObjectId;
        if (!IsNetworkEntityId(targetEntityId)) return null;

        var targetEffects = effects[0].Effects;
        if (actionId == EnemyCombatConstants.PurifyActionId)
        {
            for (var slot = 0; slot < EffectSlotsPerTarget; slot++)
            {
                var effect = targetEffects[slot];
                if (!MiracleCleanseFollowupRules.IsExactStunPurifySignal(
                        casterEntityId,
                        actionId,
                        targetEntityId,
                        effect.Type,
                        effect.Value,
                        header->GlobalSequence,
                        header->SourceSequence))
                {
                    continue;
                }

                return new MiracleInterceptThreatEvent(
                    Environment.TickCount64,
                    localEntityId,
                    casterEntityId,
                    targetEntityId,
                    actionId,
                    effect.Type,
                    effect.Value,
                    CurrentMiracleCleanseFollowupGeneration,
                    header->GlobalSequence,
                    header->SourceSequence);
            }

            return null;
        }

        var kind = MiracleInterceptRules.ClassifyExactStartSignal(
            actionId,
            casterEntityId,
            targetEntityId,
            header->NumTargets,
            targetEffects[0].Type,
            IsEmpty(targetEffects[0]),
            HasOnlyEmptyAdditionalEffects(targetEffects));
        if (kind == SeitonSense.Core.MiracleInterceptThreatKind.None) return null;

        return new MiracleInterceptThreatEvent(
            Environment.TickCount64,
            localEntityId,
            casterEntityId,
            targetEntityId,
            actionId,
            0,
            0,
            0,
            header->GlobalSequence,
            header->SourceSequence);
    }

    private MiracleInterceptLandedEffect? TryCaptureMiracleInterceptConfirmation(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        var localEntityId = CurrentMiracleInterceptLocalEntityId;
        if (!IsNetworkEntityId(localEntityId) ||
            casterEntityId != localEntityId ||
            header == null ||
            effects == null ||
            targetEntityIds == null ||
            header->NumTargets != 1)
        {
            return null;
        }

        var actionId = header->SpellId != 0 ? header->SpellId : header->ActionId;
        if (actionId != MiracleInterceptConfirmationRules.MiracleOfNatureActionId) return null;

        var targetEntityId = targetEntityIds[0].ObjectId;
        if (!IsNetworkEntityId(targetEntityId) || targetEntityId == localEntityId) return null;

        var targetEffects = effects[0].Effects;
        for (var slot = 0; slot < EffectSlotsPerTarget; slot++)
        {
            var effect = targetEffects[slot];
            if (effect.Type != MiracleInterceptConfirmationRules.AddStatusEffectType ||
                effect.Value != MiracleInterceptConfirmationRules.MiracleOfNatureStatusId)
            {
                continue;
            }

            return new MiracleInterceptLandedEffect(
                Environment.TickCount64,
                casterEntityId,
                targetEntityId,
                actionId,
                effect.Type,
                effect.Value,
                header->GlobalSequence,
                header->SourceSequence);
        }

        return null;
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

    private void EnqueueAllyRescueCleanse(AllyRescueCleanseEffect cleanse)
    {
        if (disposed ||
            Volatile.Read(ref captureBlocked) != 0 ||
            cleanse.CasterEntityId != CurrentAllyRescueLocalEntityId)
        {
            return;
        }

        var depth = Interlocked.Increment(ref queuedAllyRescueCleanseCount);
        if (depth > MaximumQueuedAllyRescueCleanses)
        {
            Interlocked.Decrement(ref queuedAllyRescueCleanseCount);
            Interlocked.Increment(ref droppedAllyRescueCleanses);
            return;
        }

        pendingAllyRescueCleanses.Enqueue(cleanse);
        Interlocked.Increment(ref capturedAllyRescueCleanses);
    }

    private void EnqueueMiracleInterceptThreat(MiracleInterceptThreatEvent threat)
    {
        var isCleanseFollowup = threat.ActionId == EnemyCombatConstants.PurifyActionId;
        var currentLocalEntityId = isCleanseFollowup
            ? CurrentMiracleCleanseFollowupLocalEntityId
            : CurrentMiracleInterceptLocalEntityId;
        if (disposed ||
            Volatile.Read(ref captureBlocked) != 0 ||
            threat.LocalEntityId != currentLocalEntityId ||
            !IsNetworkEntityId(threat.CasterEntityId) ||
            !IsNetworkEntityId(currentLocalEntityId) ||
            threat.CasterEntityId == currentLocalEntityId ||
            (isCleanseFollowup &&
             threat.FeatureGeneration != CurrentMiracleCleanseFollowupGeneration))
        {
            return;
        }

        var depth = Interlocked.Increment(ref queuedMiracleInterceptThreatCount);
        if (depth > MaximumQueuedMiracleInterceptThreats)
        {
            Interlocked.Decrement(ref queuedMiracleInterceptThreatCount);
            Interlocked.Increment(ref droppedMiracleInterceptThreats);
            return;
        }

        pendingMiracleInterceptThreats.Enqueue(threat);
        Interlocked.Increment(ref capturedMiracleInterceptThreats);
    }

    private void EnqueueMiracleInterceptConfirmation(MiracleInterceptLandedEffect confirmation)
    {
        if (disposed ||
            Volatile.Read(ref captureBlocked) != 0 ||
            confirmation.CasterEntityId != CurrentMiracleInterceptLocalEntityId ||
            !IsNetworkEntityId(confirmation.TargetEntityId))
        {
            return;
        }

        var depth = Interlocked.Increment(ref queuedMiracleInterceptConfirmationCount);
        if (depth > MaximumQueuedMiracleInterceptConfirmations)
        {
            Interlocked.Decrement(ref queuedMiracleInterceptConfirmationCount);
            Interlocked.Increment(ref droppedMiracleInterceptConfirmations);
            return;
        }

        pendingMiracleInterceptConfirmations.Enqueue(confirmation);
        Interlocked.Increment(ref capturedMiracleInterceptConfirmations);
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

    private static bool IsPurifyRemovableStatus(uint statusId) =>
        statusId is 1343 or 1344 or 1345 or 1347 or 3085 or 3219;

    private static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000u;
}
