using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct ActionBarActivitySnapshot(
    bool Known,
    ulong Token);

internal sealed record SmartSprintProbeSnapshot(
    bool Configured,
    SupportedPvPContext Context,
    bool MetadataVerified,
    bool ActionBarActivityKnown,
    ulong ActionBarActivityToken,
    int InactivityMilliseconds,
    long IdleForMilliseconds,
    VirtualKey HeldGameplayKey,
    bool GuardActive,
    bool Incapacitated,
    bool SprintActive,
    bool SprintReady,
    bool IdleEpisodeSpent,
    bool InputClaimed,
    bool Attempted,
    bool Accepted,
    string LastEvent)
{
    internal static SmartSprintProbeSnapshot Initial { get; } = new(
        Configured: false,
        SupportedPvPContext.None,
        MetadataVerified: false,
        ActionBarActivityKnown: false,
        ActionBarActivityToken: 0,
        SmartSprintRules.DefaultInactivityMilliseconds,
        IdleForMilliseconds: 0,
        HeldGameplayKey: VirtualKey.NO_KEY,
        GuardActive: false,
        Incapacitated: false,
        SprintActive: false,
        SprintReady: false,
        IdleEpisodeSpent: false,
        InputClaimed: false,
        Attempted: false,
        Accepted: false,
        LastEvent: "Disabled");
}

/// <summary>
/// Optional low-priority Sprint request after a quiet action-bar period. The
/// action-bar token comes from the native hotbar input owner; movement, camera,
/// and target input therefore cannot reset this clock.
/// </summary>
internal sealed class SmartSprintProbe
{
    private readonly IClientState clientState;
    private readonly IDutyState dutyState;
    private readonly IObjectTable objectTable;
    private readonly PluginConfiguration configuration;
    private readonly NearAssistRedirector nearAssist;
    private readonly DefensiveUtilityProbe defensiveUtility;
    private readonly IPluginLog log;
    private SmartSprintIdleState idleState = SmartSprintIdleState.Initial;
    private SmartSprintProbeSnapshot snapshot = SmartSprintProbeSnapshot.Initial;
    private long nextErrorLogAt;

    internal SmartSprintProbe(
        IClientState clientState,
        IDutyState dutyState,
        IObjectTable objectTable,
        PluginConfiguration configuration,
        NearAssistRedirector nearAssist,
        DefensiveUtilityProbe defensiveUtility,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.dutyState = dutyState;
        this.objectTable = objectTable;
        this.configuration = configuration;
        this.nearAssist = nearAssist;
        this.defensiveUtility = defensiveUtility;
        this.log = log;
    }

    internal SmartSprintProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal SmartSprintProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        SupportedPvPContext context,
        bool enabled,
        int inactivityMilliseconds,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset)
    {
        if (hardReset) idleState = SmartSprintIdleState.Initial;

        var localIdentityValid = TryGetExactLiveIdentity(localPlayer, out var localIdentity);
        var activity = nearAssist.ActionBarActivitySnapshot;
        var metadataVerified = nearAssist.PvPSprintMetadataVerified;
        var sprintActive = localIdentityValid &&
                           HasActiveStatus(localPlayer!, EnemyCombatConstants.PvPSprintStatusId);
        var incapacitated = localIdentityValid && HasEscapeBlockingCrowdControl(localPlayer!);
        var guardSuppressed = localIdentityValid &&
                              IsGuardActiveOrPropagating(localPlayer!, nowMilliseconds);
        var heldKey = enabled && inputFrame.HeldGameplayKeyEligible
            ? inputFrame.Snapshot.HeldGameplayKey
            : VirtualKey.NO_KEY;
        var sprintReady = metadataVerified &&
                          IsSprintActionSpecificallyReady(localPlayer);
        var normalizedInactivity =
            SmartSprintRules.NormalizeInactivityMilliseconds(inactivityMilliseconds);
        var observation = new SmartSprintIdleObservation(
            enabled,
            context is SupportedPvPContext.CrystallineConflict or
                SupportedPvPContext.WolvesDen,
            localIdentityValid,
            activity.Known,
            activity.Token,
            heldKey != VirtualKey.NO_KEY,
            GuardStateKnown: localIdentityValid,
            GuardActive: guardSuppressed,
            IncapacitationStateKnown: localIdentityValid,
            Incapacitated: incapacitated,
            HigherPriorityClaimed: higherPriorityClaimed || inputFrame.IsConsumed,
            SprintMetadataVerified: metadataVerified,
            SprintStatusKnown: localIdentityValid,
            SprintActive: sprintActive,
            SprintLocallyReady: sprintReady,
            normalizedInactivity,
            nowMilliseconds,
            hardReset);
        var decision = SmartSprintRules.ObserveIdle(idleState, observation);
        idleState = decision.NextState;

        var inputClaimed = false;
        var attempted = false;
        var accepted = false;
        var lastEvent = DescribeState(
            enabled,
            context,
            activity,
            heldKey,
            guardSuppressed,
            incapacitated,
            sprintActive,
            sprintReady,
            decision);
        if (decision.ShouldDispatch)
        {
            inputClaimed = true;
            inputFrame.Consume();
            var outcome = TryUseSprintOnce(
                localIdentity,
                clientState.TerritoryType,
                context,
                activity.Token,
                heldKey,
                inputFrame,
                out attempted);
            if (!attempted && outcome is
                (ClientActionAttemptOutcome.NotInvoked or
                 ClientActionAttemptOutcome.SoftUnavailable))
            {
                // A gate changed after the Core decision. Keep the same idle
                // episode open so Guard, a cast, or a one-frame priority claim
                // cannot consume the only chance without reaching FFXIV.
                idleState = idleState with { IdleEpisodeSpent = false };
            }
            accepted = outcome == ClientActionAttemptOutcome.ClientAccepted;
            lastEvent = $"Idle Sprint: {outcome}";
        }

        var idleFor = idleState.HasActionBarActivityBaseline
            ? Math.Max(0, nowMilliseconds - idleState.LastActionBarActivityAtMilliseconds)
            : 0;
        var result = new SmartSprintProbeSnapshot(
            enabled,
            context,
            metadataVerified,
            activity.Known,
            activity.Token,
            normalizedInactivity,
            idleFor,
            heldKey,
            guardSuppressed,
            incapacitated,
            sprintActive,
            sprintReady,
            idleState.IdleEpisodeSpent,
            inputClaimed,
            attempted,
            accepted,
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        idleState = SmartSprintIdleState.Initial;
        Volatile.Write(ref snapshot, SmartSprintProbeSnapshot.Initial with
        {
            MetadataVerified = nearAssist.PvPSprintMetadataVerified,
        });
    }

    internal SmartSprintProbeSnapshot FailClosed(long nowMilliseconds, Exception? exception = null)
    {
        idleState = SmartSprintIdleState.Initial;
        if (exception is not null) LogFailure(exception, nowMilliseconds);
        var failed = SmartSprintProbeSnapshot.Initial with
        {
            Configured = configuration.Enabled && configuration.EnableIdleSmartSprintOnHeldKey,
            MetadataVerified = nearAssist.PvPSprintMetadataVerified,
            LastEvent = "Failed closed",
        };
        Volatile.Write(ref snapshot, failed);
        return failed;
    }

    private unsafe ClientActionAttemptOutcome TryUseSprintOnce(
        TargetPressureActorIdentity expectedLocalIdentity,
        uint expectedTerritoryId,
        SupportedPvPContext expectedContext,
        ulong expectedActionBarActivityToken,
        VirtualKey expectedHeldKey,
        EmergencyActionInputFrame inputFrame,
        out bool attempted)
    {
        attempted = false;
        if (!expectedLocalIdentity.IsValid ||
            expectedHeldKey == VirtualKey.NO_KEY ||
            clientState.TerritoryType != expectedTerritoryId ||
            ResolveCurrentContext() != expectedContext ||
            !HasSameActionBarActivity(expectedActionBarActivityToken) ||
            !inputFrame.IsGameplayKeyPhysicallyDown(expectedHeldKey) ||
            !inputFrame.IsGameplayKeyGenerationEligible(expectedHeldKey))
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var localPlayer = objectTable.LocalPlayer;
        if (!TryGetExactLiveIdentity(localPlayer, out var currentIdentity) ||
            currentIdentity != expectedLocalIdentity ||
            HasActiveStatus(localPlayer!, EnemyCombatConstants.PvPSprintStatusId) ||
            HasEscapeBlockingCrowdControl(localPlayer!))
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var finalNow = Environment.TickCount64;
        if (IsGuardActiveOrPropagating(localPlayer!, finalNow))
            return ClientActionAttemptOutcome.NotInvoked;

        var actionManager = ActionManager.Instance();
        if (!nearAssist.PvPSprintMetadataVerified ||
            actionManager == null ||
            actionManager->GetAdjustedActionId(EnemyCombatConstants.PvPSprintActionId) !=
            EnemyCombatConstants.PvPSprintActionId ||
            !ClientActionAttemptBoundary.IsExactActionReady(
                actionManager,
                EnemyCombatConstants.PvPSprintActionId) ||
            !HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                actionManager->AnimationLock,
                localPlayer!.IsCasting,
                actionManager->CastActionId,
                actionManager->ActionQueued))
        {
            return ClientActionAttemptOutcome.SoftUnavailable;
        }

        if (clientState.TerritoryType != expectedTerritoryId ||
            ResolveCurrentContext() != expectedContext ||
            !HasSameActionBarActivity(expectedActionBarActivityToken) ||
            !inputFrame.IsGameplayKeyPhysicallyDown(expectedHeldKey) ||
            !inputFrame.IsGameplayKeyGenerationEligible(expectedHeldKey))
        {
            return ClientActionAttemptOutcome.NotInvoked;
        }

        var before = ClientActionAttemptBoundary.Capture(
            actionManager,
            EnemyCombatConstants.PvPSprintActionId);
        try
        {
            Exception? nativeException = null;
            var invocation = nearAssist.RunExactAutomaticActionWithoutRedirect(
                new ExactAutomaticActionBoundaryIntent(
                    ActionType.Action,
                    EnemyCombatConstants.PvPSprintActionId,
                    expectedLocalIdentity.GameObjectId,
                    ActionManager.UseActionMode.None),
                () =>
                {
                    try
                    {
                        return actionManager->UseAction(
                            ActionType.Action,
                            EnemyCombatConstants.PvPSprintActionId,
                            expectedLocalIdentity.GameObjectId,
                            0,
                            ActionManager.UseActionMode.None,
                            0);
                    }
                    catch (Exception exception)
                    {
                        nativeException = exception;
                        return false;
                    }
                });
            attempted = invocation.NativeBoundaryInvoked;
            if (nativeException is not null)
            {
                LogFailure(nativeException, finalNow);
                return attempted
                    ? ClientActionAttemptOutcome.AcceptanceUnknown
                    : ClientActionAttemptOutcome.NotInvoked;
            }
            if (!attempted)
                return ClientActionAttemptOutcome.NotInvoked;

            return ClientActionAttemptBoundaryRules.Classify(
                invocation.ClientReturnedAccepted,
                EnemyCombatConstants.PvPSprintActionId,
                before,
                ClientActionAttemptBoundary.Capture(
                    actionManager,
                    EnemyCombatConstants.PvPSprintActionId));
        }
        catch (Exception exception)
        {
            LogFailure(exception, finalNow);
            return attempted
                ? ClientActionAttemptOutcome.AcceptanceUnknown
                : ClientActionAttemptOutcome.NotInvoked;
        }
    }

    private SupportedPvPContext ResolveCurrentContext()
    {
        var condition = dutyState.ContentFinderCondition;
        var conditionValid = condition.IsValid;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            configuration.EnableWolvesDenTesting,
            clientState.TerritoryType,
            conditionValid,
            conditionValid && condition.Value.PvP,
            conditionValid ? condition.Value.ContentUICategory.RowId : 0,
            conditionValid && condition.Value.CrystallineConflictCasualRoulette,
            conditionValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private bool HasSameActionBarActivity(ulong expectedToken)
    {
        var current = nearAssist.ActionBarActivitySnapshot;
        return current.Known && current.Token == expectedToken;
    }

    private bool IsGuardActiveOrPropagating(
        IPlayerCharacter localPlayer,
        long nowMilliseconds)
    {
        // RemainingTime can briefly be zero while the exact Guard row is still
        // present at a client update boundary. For an optional Sprint helper,
        // membership itself is enough to wait; a false negative here could
        // cancel the player's Guard, while a one-frame false positive is only
        // a harmless delay.
        if (HasGuardStatusMembership(localPlayer)) return true;

        nearAssist.TryGetRecentExactLocalGuardAttempt(
            clientState.TerritoryType,
            localPlayer.GameObjectId,
            localPlayer.EntityId,
            nowMilliseconds,
            DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
            out var guardAttemptAt);
        return defensiveUtility.ObserveGuardSuppression(
                DefensiveUtilityProbe.HasActiveGuard(localPlayer),
                guardAttemptAt,
                nowMilliseconds)
            .SuppressDirectActionHelpers;
    }

    private static bool HasGuardStatusMembership(IPlayerCharacter player)
    {
        foreach (var status in player.StatusList)
        {
            if (status.StatusId is EnemyCombatConstants.GuardStatusId or
                EnemyCombatConstants.GuardStatusAlternateId)
            {
                return true;
            }
        }

        return false;
    }

    private static unsafe bool IsSprintActionSpecificallyReady(
        IPlayerCharacter? localPlayer)
    {
        var actionManager = ActionManager.Instance();
        return localPlayer is not null &&
               actionManager != null &&
               actionManager->GetAdjustedActionId(EnemyCombatConstants.PvPSprintActionId) ==
               EnemyCombatConstants.PvPSprintActionId &&
               actionManager->IsActionOffCooldown(
                   ActionType.Action,
                   EnemyCombatConstants.PvPSprintActionId) &&
               actionManager->CheckActionResources(
                   ActionType.Action,
                   EnemyCombatConstants.PvPSprintActionId) == 0 &&
               HeldActionRetryRules.IsNativeBoundaryNearQueueable(
                   actionManager->AnimationLock,
                   localPlayer.IsCasting,
                   actionManager->CastActionId,
                   actionManager->ActionQueued);
    }

    private static unsafe bool TryGetExactLiveIdentity(
        IPlayerCharacter? player,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (player is null ||
            player.GameObjectId is 0 or ulong.MaxValue ||
            player.EntityId == 0 ||
            player.CurrentHp == 0 ||
            player.Address == nint.Zero)
        {
            return false;
        }

        identity = new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);
        return identity.IsValid;
    }

    private static bool HasEscapeBlockingCrowdControl(IPlayerCharacter player) =>
        HasActiveStatus(player, EnemyCombatConstants.PvPStunStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.PvPBindStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.DeepFreezeStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.MiracleOfNatureStatusId);

    private static bool HasActiveStatus(IPlayerCharacter player, uint statusId)
    {
        foreach (var status in player.StatusList)
        {
            if (status.StatusId == statusId) return true;
        }

        return false;
    }

    private static string DescribeState(
        bool enabled,
        SupportedPvPContext context,
        ActionBarActivitySnapshot activity,
        VirtualKey heldKey,
        bool guardActive,
        bool incapacitated,
        bool sprintActive,
        bool sprintReady,
        SmartSprintIdleDecision decision)
    {
        if (!enabled) return "Idle Sprint disabled";
        if (context == SupportedPvPContext.None) return "Outside supported PvP";
        if (!activity.Known) return "Waiting for action-bar input tracking";
        if (!decision.IdleThresholdReached) return "Waiting for action-bar inactivity";
        if (decision.NextState.IdleEpisodeSpent) return "Idle Sprint episode spent";
        if (heldKey == VirtualKey.NO_KEY) return "Hold a gameplay key";
        if (guardActive) return "Waiting for Guard to end";
        if (incapacitated) return "Waiting for crowd control to end";
        if (sprintActive) return "Sprint already active";
        if (!sprintReady) return "Sprint is not ready";
        return "Idle Sprint ready";
    }

    private void LogFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Warning(exception, "Seiton Sense Smart Sprint failed closed.");
    }
}
