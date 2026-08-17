using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using GameAction = Lumina.Excel.Sheets.Action;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace SeitonSense.Plugin.Services;

internal sealed record PressureEscapeSprintProbeSnapshot(
    bool Active,
    bool WarningEnabled,
    bool SprintEnabled,
    bool PressureKnown,
    int DirectEnemyCount,
    int DirectHardTargetCount,
    int DirectCastTargetCount,
    long PressureAgeMilliseconds,
    bool HighPressure,
    bool WarningActive,
    ulong WarningEpisodeToken,
    bool GuardSuppressed,
    bool SprintActive,
    bool Incapacitated,
    bool SprintEpisodeSpent,
    bool SprintMetadataVerified,
    VirtualKey HeldGameplayKey,
    bool InputClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount,
    string LastEvent)
{
    internal static PressureEscapeSprintProbeSnapshot Initial { get; } = new(
        Active: false,
        WarningEnabled: false,
        SprintEnabled: false,
        PressureKnown: false,
        DirectEnemyCount: 0,
        DirectHardTargetCount: 0,
        DirectCastTargetCount: 0,
        PressureAgeMilliseconds: -1,
        HighPressure: false,
        WarningActive: false,
        WarningEpisodeToken: 0,
        GuardSuppressed: false,
        SprintActive: false,
        Incapacitated: false,
        SprintEpisodeSpent: false,
        SprintMetadataVerified: false,
        HeldGameplayKey: VirtualKey.NO_KEY,
        InputClaimed: false,
        UseActionAttempted: false,
        UseActionAccepted: false,
        AttemptCount: 0,
        AcceptedCount: 0,
        LastEvent: "Not started");
}

/// <summary>
/// Turns one unclaimed standard movement-key generation into at most one exact
/// self Sprint request while three or more current enemies directly target the
/// exact local player. Warning state is independent from the Sprint opt-in.
/// </summary>
internal sealed class PressureEscapeSprintProbe
{
    private readonly IClientState clientState;
    private readonly IDutyState dutyState;
    private readonly IObjectTable objectTable;
    private readonly TargetPressureTracker pressureTracker;
    private readonly NearAssistRedirector nearAssist;
    private readonly DefensiveUtilityProbe defensiveUtility;
    private readonly IPluginLog log;
    private readonly bool sprintMetadataVerified;
    private readonly HighPressureWarningSound warningSound;
    private PressureEscapeWarningState warningState = PressureEscapeWarningState.Initial;
    private PressureEscapeSprintProbeSnapshot snapshot = PressureEscapeSprintProbeSnapshot.Initial;
    private ulong spentSprintEpisodeToken;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;

    internal PressureEscapeSprintProbe(
        IClientState clientState,
        IDutyState dutyState,
        IObjectTable objectTable,
        IDataManager dataManager,
        TargetPressureTracker pressureTracker,
        NearAssistRedirector nearAssist,
        DefensiveUtilityProbe defensiveUtility,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.dutyState = dutyState;
        this.objectTable = objectTable;
        this.pressureTracker = pressureTracker;
        this.nearAssist = nearAssist;
        this.defensiveUtility = defensiveUtility;
        this.log = log;
        sprintMetadataVerified = ValidateMetadata(dataManager, log);
        warningSound = new HighPressureWarningSound(log);
    }

    internal PressureEscapeSprintProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal bool PlayWarningSoundPreview(int soundId) =>
        warningSound.TryPlayPreview(soundId, Environment.TickCount64);

    internal PressureEscapeSprintProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isCrystallineConflict,
        bool showWarning,
        bool playWarningSound,
        int warningSoundId,
        bool enableSprintOnHeldMovementKey,
        bool guardSuppressed,
        bool higherPriorityClaimed,
        EmergencyActionInputFrame inputFrame,
        long nowMilliseconds,
        bool hardReset = false)
    {
        if (hardReset)
        {
            warningState = PressureEscapeWarningState.Initial;
            spentSprintEpisodeToken = 0;
            warningSound.Reset();
        }

        var localIdentityValid = TryGetExactLiveIdentity(localPlayer, out var localIdentity);
        var directPressure = default(DirectSelfPressureSnapshot);
        var pressureKnown = localIdentityValid &&
                            pressureTracker.TryGetFreshSelfDirectIncomingPressure(
                                localIdentity,
                                nowMilliseconds,
                                PressureEscapeRules.MaximumPressureAgeMilliseconds,
                                out directPressure);
        var directEnemyCount = pressureKnown ? directPressure.UniqueEnemyCount : 0;
        var directHardTargetCount = pressureKnown ? directPressure.HardTargetEnemyCount : 0;
        var directCastTargetCount = pressureKnown ? directPressure.CastTargetEnemyCount : 0;
        var pressureAge = pressureKnown
            ? nowMilliseconds - directPressure.PublishedAtMilliseconds
            : -1;
        var warningTrackingEnabled =
            showWarning || playWarningSound || enableSprintOnHeldMovementKey;
        var warningDecision = PressureEscapeRules.ObserveWarning(
            warningState,
            new PressureEscapeWarningObservation(
                warningTrackingEnabled,
                isCrystallineConflict,
                localIdentityValid,
                pressureKnown,
                directEnemyCount,
                nowMilliseconds,
                hardReset));
        warningState = warningDecision.NextState;
        if (playWarningSound && warningDecision.EnteredWarning)
        {
            warningSound.TryPlayEpisode(
                warningDecision.NextState.EpisodeToken,
                Math.Clamp(warningSoundId, 1, 16),
                nowMilliseconds);
        }

        var sprintActive = localIdentityValid &&
                           HasActiveStatus(localPlayer!, EnemyCombatConstants.PvPSprintStatusId);
        var incapacitated = localIdentityValid && HasEscapeBlockingCrowdControl(localPlayer!);
        var input = inputFrame.Snapshot;
        var heldMovementKey = enableSprintOnHeldMovementKey && inputFrame.HeldMovementKeyEligible
            ? input.HeldMovementKey
            : VirtualKey.NO_KEY;
        var sprintReady = sprintMetadataVerified && IsSprintLocallyReady();
        var warningEpisodeToken = warningDecision.NextState.EpisodeToken;
        var sprintEpisodeAvailable = warningDecision.HighPressure &&
                                     warningEpisodeToken != 0 &&
                                     warningEpisodeToken != spentSprintEpisodeToken;
        var sprintObservation = new PressureEscapeSprintObservation(
            enableSprintOnHeldMovementKey,
            isCrystallineConflict,
            localIdentityValid,
            sprintMetadataVerified,
            pressureKnown,
            directEnemyCount,
            guardSuppressed,
            sprintActive,
            incapacitated,
            higherPriorityClaimed || inputFrame.IsConsumed,
            sprintEpisodeAvailable,
            heldMovementKey != VirtualKey.NO_KEY,
            (int)heldMovementKey,
            sprintReady);

        var inputClaimed = false;
        var attempted = false;
        var accepted = false;
        var lastEvent = DescribeState(
            showWarning,
            playWarningSound,
            enableSprintOnHeldMovementKey,
            isCrystallineConflict,
            localIdentityValid,
            pressureKnown,
            directEnemyCount,
            guardSuppressed,
            sprintActive,
            incapacitated,
            higherPriorityClaimed || inputFrame.IsConsumed,
            sprintEpisodeAvailable,
            heldMovementKey,
            sprintReady);

        if (PressureEscapeRules.CanDispatchSprint(sprintObservation))
        {
            // This movement generation is terminal before any final read or
            // native boundary. Identity/pressure/status drift consumes intent;
            // it never selects another action, target, or retry.
            inputClaimed = true;
            spentSprintEpisodeToken = warningEpisodeToken;
            inputFrame.Consume();
            accepted = TryUseSprintOnce(
                localIdentity,
                clientState.TerritoryType,
                out attempted);
            lastEvent = accepted
                ? "Sprint request accepted for exact direct pressure"
                : "Movement intent consumed; Sprint rejected or final state changed";
        }

        if (attempted) Interlocked.Increment(ref attemptCount);
        if (accepted) Interlocked.Increment(ref acceptedCount);
        var result = new PressureEscapeSprintProbeSnapshot(
            (showWarning || playWarningSound || enableSprintOnHeldMovementKey) &&
            isCrystallineConflict &&
            localIdentityValid,
            showWarning,
            enableSprintOnHeldMovementKey,
            pressureKnown,
            directEnemyCount,
            directHardTargetCount,
            directCastTargetCount,
            pressureAge,
            warningDecision.HighPressure,
            showWarning && warningDecision.WarningActive,
            warningDecision.NextState.EpisodeToken,
            guardSuppressed,
            sprintActive,
            incapacitated,
            warningEpisodeToken != 0 && warningEpisodeToken == spentSprintEpisodeToken,
            sprintMetadataVerified,
            heldMovementKey,
            inputClaimed,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount),
            lastEvent);
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        warningState = PressureEscapeWarningState.Initial;
        spentSprintEpisodeToken = 0;
        warningSound.Reset();
        Volatile.Write(ref snapshot, PressureEscapeSprintProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            SprintMetadataVerified = sprintMetadataVerified,
            LastEvent = "Reset",
        });
    }

    internal PressureEscapeSprintProbeSnapshot FailClosed(long nowMilliseconds, Exception? exception = null)
    {
        // A transient runtime exception is unknown pressure, not proof that the
        // enemy-focus episode ended. Hide immediately while retaining the open
        // episode, spent Sprint token, and consumed sound token.
        warningState = new PressureEscapeWarningState(
            false,
            warningState.EpisodeOpen,
            -1,
            warningState.EpisodeToken);
        if (exception is not null) LogFailure(exception, nowMilliseconds);
        var failed = PressureEscapeSprintProbeSnapshot.Initial with
        {
            WarningEpisodeToken = warningState.EpisodeToken,
            SprintEpisodeSpent = warningState.EpisodeToken != 0 &&
                                 warningState.EpisodeToken == spentSprintEpisodeToken,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
            SprintMetadataVerified = sprintMetadataVerified,
            LastEvent = "Failed closed",
        };
        Volatile.Write(ref snapshot, failed);
        return failed;
    }

    private unsafe bool TryUseSprintOnce(
        TargetPressureActorIdentity expectedLocalIdentity,
        uint expectedTerritoryId,
        out bool attempted)
    {
        attempted = false;
        if (clientState.TerritoryType != expectedTerritoryId ||
            ResolveCurrentContext() != SupportedPvPContext.CrystallineConflict)
        {
            return false;
        }

        var localPlayer = objectTable.LocalPlayer;
        if (!TryGetExactLiveIdentity(localPlayer, out var currentIdentity) ||
            currentIdentity != expectedLocalIdentity)
        {
            return false;
        }

        var finalNow = Environment.TickCount64;
        if (!pressureTracker.TryGetFreshSelfDirectIncomingPressure(
                expectedLocalIdentity,
                finalNow,
                PressureEscapeRules.MaximumPressureAgeMilliseconds,
                out var finalPressure) ||
            !PressureEscapeRules.IsHighPressure(true, finalPressure.UniqueEnemyCount) ||
            HasActiveStatus(localPlayer!, EnemyCombatConstants.PvPSprintStatusId) ||
            HasEscapeBlockingCrowdControl(localPlayer!))
        {
            return false;
        }

        var observedGuardAttemptAt = -1L;
        nearAssist.TryGetRecentExactLocalGuardAttempt(
            clientState.TerritoryType,
            localPlayer!.GameObjectId,
            localPlayer.EntityId,
            finalNow,
            DefensiveUtilityRules.GuardPropagationLatchMilliseconds,
            out observedGuardAttemptAt);
        var guardSuppression = defensiveUtility.ObserveGuardSuppression(
            DefensiveUtilityProbe.HasActiveGuard(localPlayer),
            observedGuardAttemptAt,
            finalNow);
        if (guardSuppression.SuppressDirectActionHelpers) return false;

        var actionManager = ActionManager.Instance();
        if (!sprintMetadataVerified ||
            actionManager == null ||
            actionManager->GetAdjustedActionId(EnemyCombatConstants.PvPSprintActionId) !=
            EnemyCombatConstants.PvPSprintActionId ||
            !actionManager->IsActionOffCooldown(
                ActionType.Action,
                EnemyCombatConstants.PvPSprintActionId))
        {
            return false;
        }

        if (clientState.TerritoryType != expectedTerritoryId ||
            ResolveCurrentContext() != SupportedPvPContext.CrystallineConflict)
        {
            return false;
        }

        attempted = true;
        try
        {
            return nearAssist.RunWithoutRedirect(() =>
                actionManager->UseAction(
                    ActionType.Action,
                    EnemyCombatConstants.PvPSprintActionId,
                    expectedLocalIdentity.GameObjectId,
                    0,
                    ActionManager.UseActionMode.None,
                    0));
        }
        catch (Exception exception)
        {
            LogFailure(exception, finalNow);
            return false;
        }
    }

    private static unsafe bool TryGetExactLiveIdentity(
        IPlayerCharacter? player,
        out TargetPressureActorIdentity identity)
    {
        identity = default;
        if (player is null ||
            player.Address == 0 ||
            player.EntityId is 0 or 0xE0000000 or uint.MaxValue ||
            player.GameObjectId is 0 or 0xE0000000UL or ulong.MaxValue ||
            player.IsDead ||
            !player.IsTargetable ||
            player.CurrentHp == 0 ||
            player.MaxHp < player.CurrentHp)
        {
            return false;
        }

        var native = (GameObject*)player.Address;
        if (native == null || native->EntityId != player.EntityId) return false;
        identity = new TargetPressureActorIdentity(player.GameObjectId, player.EntityId);
        return identity.IsValid;
    }

    private static bool HasEscapeBlockingCrowdControl(IPlayerCharacter player) =>
        HasActiveStatus(player, EnemyCombatConstants.PvPStunStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.PvPBindStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.DeepFreezeStatusId) ||
        HasActiveStatus(player, EnemyCombatConstants.MiracleOfNatureStatusId);

    private SupportedPvPContext ResolveCurrentContext()
    {
        var condition = dutyState.ContentFinderCondition;
        return PvPMatchRules.ResolveSupportedContext(
            clientState.IsPvP,
            clientState.IsPvPExcludingDen,
            includeWolvesDenTesting: false,
            clientState.TerritoryType,
            condition.IsValid,
            condition.IsValid && condition.Value.PvP,
            condition.IsValid ? condition.Value.ContentUICategory.RowId : 0,
            condition.IsValid && condition.Value.CrystallineConflictCasualRoulette,
            condition.IsValid && condition.Value.CrystallineConflictRankedRoulette);
    }

    private static bool HasActiveStatus(IPlayerCharacter player, uint statusId)
    {
        foreach (var status in player.StatusList)
        {
            if (status.StatusId == statusId &&
                float.IsFinite(status.RemainingTime) &&
                status.RemainingTime > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static unsafe bool IsSprintLocallyReady()
    {
        var actionManager = ActionManager.Instance();
        return actionManager != null &&
               actionManager->GetAdjustedActionId(EnemyCombatConstants.PvPSprintActionId) ==
               EnemyCombatConstants.PvPSprintActionId &&
               actionManager->IsActionOffCooldown(
                   ActionType.Action,
                   EnemyCombatConstants.PvPSprintActionId);
    }

    private static bool ValidateMetadata(IDataManager dataManager, IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<GameStatus>(ClientLanguage.English);
            var valid = actions.TryGetRow(EnemyCombatConstants.PvPSprintActionId, out var action) &&
                        descriptions.TryGetRow(
                            EnemyCombatConstants.PvPSprintActionId,
                            out var transient) &&
                        statuses.TryGetRow(
                            EnemyCombatConstants.PvPSprintStatusId,
                            out var status) &&
                        IsExpectedSprint(action, transient, status);
            if (!valid)
                log.Warning("Seiton Sense PvP Sprint metadata failed closed.");
            return valid;
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Seiton Sense PvP Sprint metadata lookup failed closed.");
            return false;
        }
    }

    private static bool IsExpectedSprint(
        GameAction action,
        ActionTransient transient,
        GameStatus status)
    {
        var description = transient.Description.ToString();
        return action.Name.ToString() == "Sprint" &&
               action.Icon == EnemyCombatConstants.PvPSprintIconId &&
               action.IsPvP &&
               action.IsPlayerAction &&
               action.ClassJob.IsValid &&
               action.ClassJob.RowId == 0 &&
               action.ClassJobCategory.IsValid &&
               action.ClassJobCategory.RowId == 85 &&
               action.ActionCategory.IsValid &&
               action.ActionCategory.RowId == 4 &&
               action.CastType == 1 &&
               action.Range == 0 &&
               action.EffectRange == 0 &&
               action.Cast100ms == 0 &&
               action.Recast100ms == EnemyCombatConstants.PvPSprintRecast100ms &&
               action.PrimaryCostType == 0 &&
               action.PrimaryCostValue == 0 &&
               action.SecondaryCostType == 0 &&
               action.SecondaryCostValue.RowId == 0 &&
               action.CooldownGroup == 58 &&
               action.AdditionalCooldownGroup == 0 &&
               action.MaxCharges == 0 &&
               action.StatusGainSelf.IsValid &&
               action.StatusGainSelf.RowId == EnemyCombatConstants.PvPSprintStatusId &&
               action.CanTargetSelf &&
               !action.CanTargetParty &&
               !action.CanTargetAlly &&
               !action.CanTargetAlliance &&
               !action.CanTargetHostile &&
               !action.CanTargetOwnPet &&
               !action.CanTargetPartyPet &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.NeedToFaceTarget &&
               action.PreservesCombo &&
               !action.AffectsPosition &&
               description.Contains("Increases movement speed by 50%", StringComparison.Ordinal) &&
               description.Contains(
                   "Effect ends upon reuse or execution of another action",
                   StringComparison.Ordinal) &&
               status.Name.ToString() == "Sprint" &&
               status.Icon == EnemyCombatConstants.PvPSprintStatusIconId &&
               status.StatusCategory == 1 &&
               status.IsPermanent &&
               !status.CanDispel &&
               !status.LockMovement &&
               status.Description.ToString().Contains(
                   "Movement speed is increased",
                   StringComparison.Ordinal);
    }

    private static string DescribeState(
        bool showWarning,
        bool playWarningSound,
        bool sprintEnabled,
        bool isCrystallineConflict,
        bool localIdentityValid,
        bool pressureKnown,
        int directEnemyCount,
        bool guardSuppressed,
        bool sprintActive,
        bool incapacitated,
        bool higherPriorityClaimed,
        bool sprintEpisodeAvailable,
        VirtualKey heldMovementKey,
        bool sprintReady)
    {
        if (!showWarning && !playWarningSound && !sprintEnabled) return "Disabled";
        if (!isCrystallineConflict) return "Outside Crystalline Conflict";
        if (!localIdentityValid) return "Local player identity invalid";
        if (!pressureKnown) return "Direct pressure unknown or stale";
        if (directEnemyCount < PressureEscapeRules.RequiredDirectEnemyCount)
            return $"Direct pressure {directEnemyCount}/3";
        if (!sprintEnabled) return "High-pressure warning active";
        if (guardSuppressed) return "Guard active or propagating";
        if (sprintActive) return "Sprint already active";
        if (incapacitated) return "Escape-blocking crowd control active";
        if (higherPriorityClaimed) return "Higher-priority helper owns input";
        if (!sprintEpisodeAvailable) return "Sprint already spent for focus episode";
        if (heldMovementKey == VirtualKey.NO_KEY) return "Waiting for W/A/S/D or arrow hold";
        if (!sprintReady) return "Sprint not locally ready or metadata invalid";
        return "Sprint eligible";
    }

    private void LogFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds > long.MaxValue - 10_000
            ? long.MaxValue
            : nowMilliseconds + 10_000;
        log.Warning(exception, "Seiton Sense pressure-escape Sprint failed closed.");
    }
}
