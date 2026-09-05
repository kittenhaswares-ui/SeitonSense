using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record PaladinShieldSmiteProbeSnapshot(
    bool Configured, SupportedPvPContext Context, bool MetadataVerified,
    int EnemySlot, ulong TargetGameObjectId, uint TargetEntityId,
    bool InputClaimed, bool UseActionAttempted, bool UseActionAccepted,
    long AttemptCount, long AcceptedCount, string LastEvent)
{
    internal static PaladinShieldSmiteProbeSnapshot Initial { get; } =
        new(false, SupportedPvPContext.None, false, 0, 0, 0, false, false, false, 0, 0, "Disabled");
}

/// <summary>Opt-in, keyless Shield Smite. Only full Guard targets are ranked;
/// no cast is cancelled and no visible target is changed.</summary>
internal sealed unsafe class PaladinShieldSmiteProbe
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private FrozenIntent? pending;
    private TargetPressureActorIdentity spentTarget;
    private bool spentReadyEpoch;
    private bool acceptedAwaitingCooldown;
    private long lastAttemptFrame = long.MinValue;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorAt;
    private PaladinShieldSmiteProbeSnapshot snapshot = PaladinShieldSmiteProbeSnapshot.Initial;

    internal PaladinShieldSmiteProbe(IClientState clientState, IObjectTable objectTable,
        NearAssistRedirector nearAssist, IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal PaladinShieldSmiteProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal PaladinShieldSmiteProbeSnapshot Observe(IPlayerCharacter? localPlayer,
        SupportedPvPContext context, bool enabled, bool actionMetadataVerified,
        bool wolvesDenStrikingDummyMetadataVerified, bool ownGuardActiveOrPropagating,
        bool higherPriorityClaimed, long nowMilliseconds, bool hardReset, long frameworkFrameId)
    {
        var attempted = false;
        var accepted = false;
        var reason = "Waiting for an enemy with full Guard";
        var selected = pending;
        try
        {
            var identity = Identity(localPlayer);
            var localValid = identity.IsValid && localPlayer!.ClassJob.IsValid &&
                             localPlayer.ClassJob.RowId == PaladinShieldSmiteRules.PaladinJobId;
            if (hardReset || !enabled || !localValid || !actionMetadataVerified ||
                context is not (SupportedPvPContext.CrystallineConflict or SupportedPvPContext.WolvesDen))
            {
                Reset();
                reason = !enabled ? "Disabled" : "Waiting for supported PLD context";
                return Publish();
            }

            var manager = ActionManager.Instance();
            var exactAction = manager != null && manager->GetAdjustedActionId(PaladinShieldSmiteRules.ActionId) ==
                              PaladinShieldSmiteRules.ActionId;
            var cooldownReady = exactAction && manager->IsActionOffCooldown(ActionType.Action, PaladinShieldSmiteRules.ActionId);
            if (exactAction && !cooldownReady)
            {
                spentReadyEpoch = false;
                acceptedAwaitingCooldown = false;
                spentTarget = default;
                pending = null;
            }
            if (spentReadyEpoch && !acceptedAwaitingCooldown && spentTarget.IsValid &&
                !nearAssist.IsFullGuardSmartActionTarget(objectTable.SearchByEntityId(spentTarget.EntityId) as IBattleChara))
            {
                spentReadyEpoch = false;
                spentTarget = default;
            }
            var ownGuard = ownGuardActiveOrPropagating || nearAssist.IsExactLocalGuardActiveOrPropagating(identity);
            if (ownGuard || TextInputActive())
            {
                pending = null;
                reason = ownGuard ? "Paused: your Guard is active" : "Paused while typing";
                return Publish();
            }
            var ready = cooldownReady && manager->CheckActionResources(ActionType.Action, PaladinShieldSmiteRules.ActionId) == 0;
            if (pending is { } old && (old.Context != context || old.TerritoryId != clientState.TerritoryType ||
                old.LocalPlayer != identity || nowMilliseconds < old.ObservedAt ||
                nowMilliseconds - old.ObservedAt > PaladinShieldSmiteRules.IntentLeaseMilliseconds ||
                ResolveTarget(localPlayer!, old, wolvesDenStrikingDummyMetadataVerified) is null))
                pending = null;
            if (pending is null && !spentReadyEpoch && ready && !higherPriorityClaimed &&
                TrySelect(localPlayer!, context, wolvesDenStrikingDummyMetadataVerified,
                    out var target, out var slot, out var denKind))
                pending = new FrozenIntent(clientState.TerritoryType, context, identity, target, slot,
                    denKind, nowMilliseconds, HeldActionRetryState.Initial);
            selected = pending;
            var exactTarget = selected is { } intent
                ? ResolveTarget(localPlayer!, intent, wolvesDenStrikingDummyMetadataVerified) : null;
            var nativeReady = manager != null && HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                manager->AnimationLock, localPlayer!.IsCasting, manager->CastActionId, manager->ActionQueued);
            if (!PaladinShieldSmiteRules.CanDispatch(enabled, context, localPlayer!.ClassJob.RowId,
                localValid, actionMetadataVerified, ownGuard, false, higherPriorityClaimed,
                ready, nativeReady, exactTarget is not null))
            {
                reason = !ready ? "Waiting for Shield Smite cooldown" : !nativeReady
                    ? "Waiting for your current action" : "Waiting for an enemy with full Guard";
                return Publish();
            }
            if (selected is not { } dispatch || frameworkFrameId == lastAttemptFrame ||
                !HeldActionRetryRules.CanAttemptFrozenIntent(dispatch.Retry, nowMilliseconds)) return Publish();
            lastAttemptFrame = frameworkFrameId;
            var outcome = TryUseOnce(dispatch, wolvesDenStrikingDummyMetadataVerified, out attempted);
            accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
            if (attempted) attemptCount++;
            if (accepted) acceptedCount++;
            var completion = HeldActionRetryRules.Complete(dispatch.Retry, nowMilliseconds, outcome);
            if (completion.IsTerminal)
            {
                pending = null;
                spentReadyEpoch = true;
                acceptedAwaitingCooldown = completion.Disposition is
                    HeldActionRetryDisposition.AcceptedTerminal or HeldActionRetryDisposition.AmbiguousTerminal;
                spentTarget = dispatch.Target;
            }
            else pending = dispatch with { Retry = completion.NextState };
            reason = $"Shield Smite: {outcome}";
        }
        catch (Exception exception)
        {
            pending = null;
            if (nowMilliseconds >= nextErrorAt)
            {
                nextErrorAt = nowMilliseconds + 10_000;
                log.Warning(exception, "Seiton Sense automatic Shield Smite failed closed.");
            }
            reason = "Waiting: current action data unavailable";
        }
        return Publish();

        PaladinShieldSmiteProbeSnapshot Publish()
        {
            var result = new PaladinShieldSmiteProbeSnapshot(enabled, context, actionMetadataVerified,
                selected?.EnemySlot ?? 0, selected?.Target.GameObjectId ?? 0, selected?.Target.EntityId ?? 0,
                attempted, attempted, accepted, attemptCount, acceptedCount, reason);
            Volatile.Write(ref snapshot, result);
            return result;
        }
    }

    private bool TrySelect(IPlayerCharacter local, SupportedPvPContext context, bool denMetadata,
        out TargetPressureActorIdentity target, out int slot, out DarkKnightWolvesDenTargetKind denKind)
    {
        target = default;
        slot = 0;
        denKind = DarkKnightWolvesDenTargetKind.None;
        if (context == SupportedPvPContext.CrystallineConflict)
            return nearAssist.TryResolveHeldSmartActionTarget(PaladinShieldSmiteRules.ActionId,
                out slot, out target, out _, out _, guardTargetsOnly: true);
        return DarkKnightWolvesDenCurrentTargetResolver.TryResolveExactCurrentHardTarget(
                   objectTable, denMetadata, local, out var actor, out target, out denKind, out _) &&
               TargetReady(local, actor!);
    }

    private IBattleChara? ResolveTarget(IPlayerCharacter local, FrozenIntent intent, bool denMetadata)
    {
        IBattleChara? target;
        if (intent.Context == SupportedPvPContext.CrystallineConflict)
        {
            target = EnemySlotResolver.Resolve(objectTable, intent.EnemySlot);
            if (!nearAssist.CanUseExactHeldSmartActionTarget(PaladinShieldSmiteRules.ActionId,
                intent.EnemySlot, intent.Target)) return null;
        }
        else if (!DarkKnightWolvesDenCurrentTargetResolver.TryResolveFrozenCurrentHardTarget(
                     objectTable, denMetadata, local, intent.Target, intent.DenKind, out target)) return null;
        return Identity(target) == intent.Target && TargetReady(local, target!) ? target : null;
    }

    private bool TargetReady(IPlayerCharacter local, IBattleChara target) =>
        PaladinShieldSmiteRules.CanSelectTarget(Identity(target).IsValid,
            nearAssist.IsFullGuardSmartActionTarget(target), smartActionSafe: true,
            local.Address != 0 && target.Address != 0 && SeitonRangeRules.HasNativeRangeAndLineOfSight(
                ActionManager.GetActionInRangeOrLoS(PaladinShieldSmiteRules.ActionId,
                    (GameObject*)local.Address, (GameObject*)target.Address)));

    private ClientActionAttemptOutcome TryUseOnce(FrozenIntent intent, bool denMetadata, out bool attempted)
    {
        attempted = false;
        var local = objectTable.LocalPlayer;
        var manager = ActionManager.Instance();
        if (Identity(local) != intent.LocalPlayer || local?.ClassJob.IsValid != true ||
            local.ClassJob.RowId != PaladinShieldSmiteRules.PaladinJobId ||
            clientState.TerritoryType != intent.TerritoryId ||
            nearAssist.IsExactLocalGuardActiveOrPropagating(intent.LocalPlayer) || TextInputActive() ||
            ResolveTarget(local!, intent, denMetadata) is null || manager == null ||
            manager->GetAdjustedActionId(PaladinShieldSmiteRules.ActionId) != PaladinShieldSmiteRules.ActionId ||
            !ClientActionAttemptBoundary.IsExactActionReady(manager, PaladinShieldSmiteRules.ActionId) ||
            manager->GetActionStatus(ActionType.Action, PaladinShieldSmiteRules.ActionId,
                intent.Target.GameObjectId, checkRecastActive: true, checkCastingActive: true) != 0 ||
            !HeldActionRetryRules.IsNativeBoundaryNearQueueable(manager->AnimationLock,
                local!.IsCasting, manager->CastActionId, manager->ActionQueued))
            return ClientActionAttemptOutcome.SoftUnavailable;
        var before = ClientActionAttemptBoundary.Capture(manager, PaladinShieldSmiteRules.ActionId);
        var threw = false;
        var result = nearAssist.RunExactAutomaticActionWithoutRedirect(
            new ExactAutomaticActionBoundaryIntent(ActionType.Action, PaladinShieldSmiteRules.ActionId,
                intent.Target.GameObjectId, ActionManager.UseActionMode.None, RequiresSmartActionProtectionRecheck: true),
            () =>
            {
                if (nearAssist.IsExactLocalGuardActiveOrPropagating(intent.LocalPlayer) || TextInputActive() ||
                    !nearAssist.IsFullGuardSmartActionTarget(objectTable.SearchByEntityId(intent.Target.EntityId) as IBattleChara))
                    return false;
                try { return manager->UseAction(ActionType.Action, PaladinShieldSmiteRules.ActionId,
                    intent.Target.GameObjectId, 0, ActionManager.UseActionMode.None, 0); }
                catch { threw = true; return false; }
            });
        attempted = result.NativeBoundaryInvoked;
        if (threw) return ClientActionAttemptOutcome.AcceptanceUnknown;
        if (!attempted) return ClientActionAttemptOutcome.SoftUnavailable;
        return ClientActionAttemptBoundaryRules.Classify(result.ClientReturnedAccepted, PaladinShieldSmiteRules.ActionId,
            before, ClientActionAttemptBoundary.Capture(manager, PaladinShieldSmiteRules.ActionId));
    }

    private static TargetPressureActorIdentity Identity(IBattleChara? actor) =>
        actor is not null && actor.Address != 0 && actor.IsTargetable && !actor.IsDead && actor.CurrentHp > 0 &&
        actor.MaxHp >= actor.CurrentHp ? new(actor.GameObjectId, actor.EntityId) : default;

    private static bool TextInputActive()
    {
        var module = RaptureAtkModule.Instance();
        return module == null || module->IsTextInputActive() || ImGui.GetIO().WantTextInput;
    }

    internal void Reset()
    {
        pending = null;
        spentTarget = default;
        spentReadyEpoch = false;
        acceptedAwaitingCooldown = false;
        lastAttemptFrame = long.MinValue;
        Volatile.Write(ref snapshot, PaladinShieldSmiteProbeSnapshot.Initial);
    }

    private readonly record struct FrozenIntent(uint TerritoryId, SupportedPvPContext Context,
        TargetPressureActorIdentity LocalPlayer, TargetPressureActorIdentity Target, int EnemySlot,
        DarkKnightWolvesDenTargetKind DenKind, long ObservedAt, HeldActionRetryState Retry);
}
