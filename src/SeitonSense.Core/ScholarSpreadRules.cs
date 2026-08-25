namespace SeitonSense.Core;

public enum ScholarSpreadKind : byte
{
    None = 0,
    Dot = 1,
    Shield = 2,
}

public enum ScholarSpreadPhase : byte
{
    Idle = 0,
    SetupReady = 1,
    AwaitingSetupEffect = 2,
    DeploymentReady = 3,
    AwaitingDeploymentEffect = 4,
    Completed = 5,
    Cancelled = 6,
}

public readonly record struct ScholarSpreadDotCandidate(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    bool NativeTargetValid,
    bool NativeRangeAndLineOfSight,
    bool HasOwnBiolysis,
    bool HasOwnBiolytic,
    bool ExactCoverageKnown,
    int NewlyCoveredEnemyCount);

public readonly record struct ScholarSpreadShieldCandidate(
    int PartySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    bool NativeTargetValid,
    bool NativeRangeAndLineOfSight,
    bool HasOwnGalvanize,
    bool HasOwnCatalyze,
    bool TacticalCrystalPresenceKnown,
    bool OnTacticalCrystal,
    bool ExactCoverageKnown,
    int NewlyCoveredPartyCount);

/// <summary>
/// Stable, already-observed consent for the independent Scholar lane. Enabling
/// the option while a key is down waits for a real release locally; it never
/// consumes or mutates the shared emergency-input generation.
/// </summary>
public readonly record struct ScholarSpreadHeldConsentState(
    bool WasEnabled,
    bool RequiresReleaseAfterEnable)
{
    public static ScholarSpreadHeldConsentState Initial => default;
}

public readonly record struct ScholarSpreadHeldConsentDecision(
    ScholarSpreadHeldConsentState NextState,
    bool AllowsWorkflow)
{
    public bool ClaimsSharedInputFrame => false;
    public bool ConsumesSharedInputGeneration => false;
}

public readonly record struct ScholarSpreadMatchGateState(
    uint TerritoryId,
    bool LiveContextValid,
    bool MatchStarted,
    bool MatchCompleted)
{
    public static ScholarSpreadMatchGateState Initial => default;
    public bool AllowsActions =>
        LiveContextValid && MatchStarted && !MatchCompleted;
}

public readonly record struct ScholarSpreadMatchGateObservation(
    uint TerritoryId,
    bool LiveContextValid,
    bool HardReset,
    bool DutyStartedRaw,
    bool DutyStartSignaled,
    bool DutyCompletionSignaled);

public readonly record struct ScholarSpreadPlanningObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    bool MatchStarted,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    int HeldGameplayKeyCode,
    bool BiolysisLocallyReady,
    bool AdloquiumLocallyReady,
    int DeploymentCharges,
    bool DeploymentNextChargeTimingKnown,
    long DeploymentNextChargeRemainingMilliseconds,
    bool BiolysisTimingKnown,
    long BiolysisRemainingMilliseconds,
    IReadOnlyList<ScholarSpreadDotCandidate>? DotCandidates,
    IReadOnlyList<ScholarSpreadShieldCandidate>? ShieldCandidates,
    bool HardReset = false);

public enum ScholarSpreadPlanDecisionKind : byte
{
    None = 0,
    Planned = 1,
    Cancelled = 2,
}

public enum ScholarSpreadPlanDecisionReason : byte
{
    None = 0,
    HardReset = 1,
    ConfigurationDisabled = 2,
    OutsideCrystallineConflict = 3,
    LocalPlayerIdentityInvalid = 4,
    LocalPlayerDead = 5,
    LocalJobInvalid = 6,
    MetadataUnverified = 7,
    GuardSuppressed = 8,
    InputProbeUnavailable = 9,
    TextInputActive = 10,
    NoHeldGameplayKey = 11,
    HeldGameplayKeyInvalid = 12,
    DeploymentUnavailable = 13,
    NoEligibleSequence = 14,
    MatchNotStarted = 15,
}

public readonly record struct ScholarSpreadPlan(
    ulong EpisodeToken,
    ScholarSpreadKind Kind,
    TargetPressureActorIdentity LocalPlayer,
    int TargetSlot,
    TargetPressureActorIdentity Target,
    int HeldGameplayKeyCode,
    int PredictedAffectedCount)
{
    public uint SetupActionId => Kind switch
    {
        ScholarSpreadKind.Dot => ScholarSpreadRules.BiolysisActionId,
        ScholarSpreadKind.Shield => ScholarSpreadRules.AdloquiumActionId,
        _ => 0,
    };

    public bool IsValid =>
        EpisodeToken != 0 &&
        Kind is ScholarSpreadKind.Dot or ScholarSpreadKind.Shield &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        HeldGameplayKeyCode > 0 &&
        PredictedAffectedCount >= ScholarSpreadRules.MinimumUsefulSpreadTargets &&
        ((Kind == ScholarSpreadKind.Dot &&
          EnemySlotRules.IsValidSlot(TargetSlot) &&
          Target != LocalPlayer) ||
         (Kind == ScholarSpreadKind.Shield &&
          TargetSlot is >= ScholarSpreadRules.FirstPartySlot and
              <= ScholarSpreadRules.LastPartySlot));
}

public readonly record struct ScholarSpreadPlanDecision(
    ScholarSpreadPlanDecisionKind Kind,
    ScholarSpreadPlanDecisionReason Reason,
    int SelectedCandidateIndex = -1,
    ScholarSpreadPlan? Plan = null)
{
    public bool HasPlan =>
        Kind == ScholarSpreadPlanDecisionKind.Planned &&
        Plan is { IsValid: true };

    // This lane observes the raw held snapshot but never participates in the
    // main held-helper ownership chain.
    public bool ClaimsSharedInputFrame => false;
    public bool ConsumesSharedInputGeneration => false;
}

public readonly record struct ScholarSpreadIntent(
    ulong EpisodeToken,
    ScholarSpreadKind Kind,
    ScholarSpreadPhase RequiredPhase,
    uint ActionId,
    TargetPressureActorIdentity LocalPlayer,
    int TargetSlot,
    TargetPressureActorIdentity Target)
{
    public bool IsSetup =>
        RequiredPhase == ScholarSpreadPhase.SetupReady &&
        ActionId == (Kind == ScholarSpreadKind.Dot
            ? ScholarSpreadRules.BiolysisActionId
            : ScholarSpreadRules.AdloquiumActionId);

    public bool IsDeployment =>
        RequiredPhase == ScholarSpreadPhase.DeploymentReady &&
        ActionId == ScholarSpreadRules.DeploymentTacticsActionId;

    public bool IsValid =>
        EpisodeToken != 0 &&
        Kind is ScholarSpreadKind.Dot or ScholarSpreadKind.Shield &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        TargetSlot > 0 &&
        (IsSetup || IsDeployment);

    public bool ClaimsSharedInputFrame => false;
}

public readonly record struct ScholarSpreadOwnedActionToken(
    ulong EpisodeToken,
    ScholarSpreadPhase RequestedFromPhase,
    uint ActionId,
    TargetPressureActorIdentity Target,
    ushort SourceSequence)
{
    public bool IsValid =>
        EpisodeToken != 0 &&
        RequestedFromPhase is ScholarSpreadPhase.SetupReady or
            ScholarSpreadPhase.DeploymentReady &&
        ScholarSpreadRules.IsRelevantAction(ActionId) &&
        Target.IsValid;

    public bool HasBoundSourceSequence => SourceSequence != 0;
}

public readonly record struct ScholarSpreadWorkflowState(
    ScholarSpreadPlan Plan,
    ScholarSpreadPhase Phase,
    ScholarSpreadOwnedActionToken PendingOwnedAction,
    uint LastConfirmedGlobalSequence,
    ushort LastConfirmedSourceSequence,
    uint LastConfirmedActionId)
{
    public static ScholarSpreadWorkflowState Initial => default;

    public bool IsActive =>
        Plan.IsValid &&
        Phase is ScholarSpreadPhase.SetupReady or
            ScholarSpreadPhase.AwaitingSetupEffect or
            ScholarSpreadPhase.DeploymentReady or
            ScholarSpreadPhase.AwaitingDeploymentEffect;
}

public readonly record struct ScholarSpreadExactTargetSnapshot(
    ScholarSpreadKind Kind,
    int TargetSlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    bool NativeTargetValid,
    bool NativeRangeAndLineOfSight,
    bool TacticalCrystalPresenceKnown,
    bool OnTacticalCrystal,
    bool ExactCoverageKnown,
    int CurrentAffectedCount,
    bool ExpectedOwnStatusPairActive);

public readonly record struct ScholarSpreadIntentObservation(
    ScholarSpreadExactTargetSnapshot ExactTarget,
    bool HeldGameplayKeyEligible,
    bool NativeActionBoundaryClear,
    uint ResolvedActionId,
    bool ActionLocallyReady,
    int DeploymentCharges,
    bool ShieldReservationStillSafe);

public enum ScholarSpreadIntentDecisionKind : byte
{
    Ready = 0,
    SoftWaitNativeBoundary = 1,
    Cancelled = 2,
}

public enum ScholarSpreadIntentDecisionReason : byte
{
    None = 0,
    WorkflowStateInvalid = 1,
    HeldGameplayKeyReleased = 2,
    LocalPlayerIdentityDrift = 3,
    TargetIdentityDrift = 4,
    TargetUnavailable = 5,
    SpreadNoLongerUseful = 6,
    StatusOwnershipDrift = 7,
    ResolvedActionDrift = 8,
    ActionUnavailable = 9,
    DeploymentChargeUnavailable = 10,
    ShieldReservationUnavailable = 11,
    NativeActionBoundaryBusy = 12,
}

public readonly record struct ScholarSpreadIntentDecision(
    ScholarSpreadIntentDecisionKind Kind,
    ScholarSpreadIntentDecisionReason Reason)
{
    public bool CanDispatch => Kind == ScholarSpreadIntentDecisionKind.Ready;
    public bool ShouldSoftWait =>
        Kind == ScholarSpreadIntentDecisionKind.SoftWaitNativeBoundary;
    public bool ClaimsSharedInputFrame => false;
}

public readonly record struct ScholarSpreadActionEffectObservation(
    TargetPressureActorIdentity Caster,
    TargetPressureActorIdentity PrimaryTarget,
    uint ActionId,
    uint GlobalSequence,
    ushort SourceSequence);

public enum ScholarSpreadEffectDecisionKind : byte
{
    Ignored = 0,
    OwnedSetupConfirmed = 1,
    OwnedDeploymentConfirmed = 2,
    Cancelled = 3,
}

public enum ScholarSpreadEffectDecisionReason : byte
{
    None = 0,
    InactiveWorkflow = 1,
    IrrelevantAction = 2,
    OtherCaster = 3,
    DuplicateOwnedEffect = 4,
    ManualUnrelatedAction = 5,
    ManualDeploymentConflict = 6,
    ManualSetupTargetConflict = 7,
    OwnedSequenceMismatch = 8,
    OwnedEffectMalformed = 9,
    ShieldReservationUnavailable = 10,
}

public readonly record struct ScholarSpreadEffectDecision(
    ScholarSpreadWorkflowState NextState,
    ScholarSpreadEffectDecisionKind Kind,
    ScholarSpreadEffectDecisionReason Reason)
{
    public bool Advanced => Kind is
        ScholarSpreadEffectDecisionKind.OwnedSetupConfirmed or
        ScholarSpreadEffectDecisionKind.OwnedDeploymentConfirmed;
}

/// <summary>
/// Pure planning and exact-attribution rules for the independent Scholar PvP
/// Biolysis/Adloquium -> Deployment Tactics held workflow. The runtime is
/// responsible for native actor/range snapshots and for calculating exact AoE
/// coverage; these rules rank and freeze one target, never substitute it, and
/// only advance on the source sequence created by the helper's own UseAction.
/// </summary>
public static class ScholarSpreadRules
{
    public const uint ScholarJobId = 28;
    public const uint AdloquiumActionId = 29_232;
    public const uint BiolysisActionId = 29_233;
    public const uint DeploymentTacticsActionId = 29_234;

    public const uint GalvanizeStatusId = 3_087;
    public const uint CatalyzeStatusId = 3_088;
    public const uint BiolysisStatusId = 3_089;
    public const uint BiolyticStatusId = 3_090;

    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;
    public const int CrystallineConflictRosterSize = 5;
    public const int MinimumUsefulSpreadTargets = 2;
    public const int MaximumEnemyTargets = 5;
    public const int MaximumPartyTargets = 8;

    public static ScholarSpreadMatchGateState ObserveMatchGate(
        ScholarSpreadMatchGateState previous,
        ScholarSpreadMatchGateObservation observation)
    {
        if (!observation.LiveContextValid)
        {
            return new ScholarSpreadMatchGateState(
                observation.TerritoryId,
                LiveContextValid: false,
                MatchStarted: false,
                MatchCompleted: false);
        }

        var reset = observation.HardReset ||
                    !previous.LiveContextValid ||
                    previous.TerritoryId != observation.TerritoryId;
        var started = !reset && previous.MatchStarted;
        var completed = !reset && previous.MatchCompleted;
        if (!completed &&
            (observation.DutyStartedRaw || observation.DutyStartSignaled))
        {
            started = true;
        }

        if (observation.DutyCompletionSignaled)
        {
            started = false;
            completed = true;
        }

        return new ScholarSpreadMatchGateState(
            observation.TerritoryId,
            LiveContextValid: true,
            MatchStarted: started,
            MatchCompleted: completed);
    }

    public static ScholarSpreadHeldConsentDecision ObserveIndependentHeldConsent(
        ScholarSpreadHeldConsentState state,
        bool configurationEnabled,
        bool heldGameplayKeyEligible)
    {
        if (!configurationEnabled)
        {
            return new ScholarSpreadHeldConsentDecision(
                ScholarSpreadHeldConsentState.Initial,
                false);
        }

        if (!state.WasEnabled)
        {
            return new ScholarSpreadHeldConsentDecision(
                new ScholarSpreadHeldConsentState(
                    WasEnabled: true,
                    RequiresReleaseAfterEnable: heldGameplayKeyEligible),
                AllowsWorkflow: false);
        }

        if (state.RequiresReleaseAfterEnable)
        {
            if (heldGameplayKeyEligible)
                return new ScholarSpreadHeldConsentDecision(state, false);

            return new ScholarSpreadHeldConsentDecision(
                state with { RequiresReleaseAfterEnable = false },
                AllowsWorkflow: false);
        }

        return new ScholarSpreadHeldConsentDecision(
            state,
            heldGameplayKeyEligible);
    }

    public static ScholarSpreadPlanDecision PlanNextSequence(
        ScholarSpreadPlanningObservation observation,
        ulong episodeToken)
    {
        var gateFailure = GetPlanningGateFailure(observation);
        if (gateFailure != ScholarSpreadPlanDecisionReason.None)
        {
            return new ScholarSpreadPlanDecision(
                ScholarSpreadPlanDecisionKind.Cancelled,
                gateFailure);
        }

        if (observation.DeploymentCharges is < 1 or > 2)
        {
            return new ScholarSpreadPlanDecision(
                ScholarSpreadPlanDecisionKind.None,
                ScholarSpreadPlanDecisionReason.DeploymentUnavailable);
        }

        if (observation.BiolysisLocallyReady)
        {
            var dotIndex = SelectBestDotSeedIndex(
                observation.DotCandidates,
                observation.LocalPlayer);
            if (dotIndex >= 0)
            {
                var candidate = observation.DotCandidates![dotIndex];
                var plan = new ScholarSpreadPlan(
                    episodeToken,
                    ScholarSpreadKind.Dot,
                    observation.LocalPlayer,
                    candidate.EnemySlot,
                    candidate.Actor,
                    observation.HeldGameplayKeyCode,
                    candidate.NewlyCoveredEnemyCount);
                if (plan.IsValid)
                {
                    return new ScholarSpreadPlanDecision(
                        ScholarSpreadPlanDecisionKind.Planned,
                        ScholarSpreadPlanDecisionReason.None,
                        dotIndex,
                        plan);
                }
            }
        }

        if (observation.AdloquiumLocallyReady &&
            CanSpendDeploymentOnShield(
                observation.DeploymentCharges,
                observation.DeploymentNextChargeTimingKnown,
                observation.DeploymentNextChargeRemainingMilliseconds,
                observation.BiolysisTimingKnown,
                observation.BiolysisRemainingMilliseconds))
        {
            var shieldIndex = SelectBestShieldSeedIndex(
                observation.ShieldCandidates,
                observation.LocalPlayer);
            if (shieldIndex >= 0)
            {
                var candidate = observation.ShieldCandidates![shieldIndex];
                var plan = new ScholarSpreadPlan(
                    episodeToken,
                    ScholarSpreadKind.Shield,
                    observation.LocalPlayer,
                    candidate.PartySlot,
                    candidate.Actor,
                    observation.HeldGameplayKeyCode,
                    candidate.NewlyCoveredPartyCount);
                if (plan.IsValid)
                {
                    return new ScholarSpreadPlanDecision(
                        ScholarSpreadPlanDecisionKind.Planned,
                        ScholarSpreadPlanDecisionReason.None,
                        shieldIndex,
                        plan);
                }
            }
        }

        return new ScholarSpreadPlanDecision(
            ScholarSpreadPlanDecisionKind.None,
            ScholarSpreadPlanDecisionReason.NoEligibleSequence);
    }

    public static bool CanSpendDeploymentOnShield(
        int currentDeploymentCharges,
        bool deploymentNextChargeTimingKnown,
        long deploymentNextChargeRemainingMilliseconds,
        bool biolysisTimingKnown,
        long biolysisRemainingMilliseconds)
    {
        if (currentDeploymentCharges == 2) return true;
        if (currentDeploymentCharges != 1 ||
            !deploymentNextChargeTimingKnown ||
            !biolysisTimingKnown ||
            deploymentNextChargeRemainingMilliseconds <= 0 ||
            biolysisRemainingMilliseconds <= 0)
        {
            return false;
        }

        return deploymentNextChargeRemainingMilliseconds <=
               biolysisRemainingMilliseconds;
    }

    public static int SelectBestDotSeedIndex(
        IReadOnlyList<ScholarSpreadDotCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (!HasUniqueDotCandidateSet(candidates, localPlayer)) return -1;

        var bestIndex = -1;
        for (var index = 0; index < candidates!.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleDotSeed(candidate, localPlayer)) continue;
            if (bestIndex < 0 ||
                CompareDotSeed(candidate, candidates[bestIndex]) < 0)
            {
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    public static int SelectBestShieldSeedIndex(
        IReadOnlyList<ScholarSpreadShieldCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (!HasUniqueShieldCandidateSet(candidates, localPlayer)) return -1;

        var eligible = new List<int>(candidates!.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            if (IsEligibleShieldSeed(candidates[index], localPlayer))
                eligible.Add(index);
        }

        if (eligible.Count == 0) return -1;
        var useTacticalCrystalPreference =
            eligible.All(index => candidates[index].TacticalCrystalPresenceKnown) &&
            eligible.Any(index => candidates[index].OnTacticalCrystal);
        var bestIndex = eligible[0];
        for (var index = 1; index < eligible.Count; index++)
        {
            var candidateIndex = eligible[index];
            if (CompareShieldSeed(
                    candidates[candidateIndex],
                    candidates[bestIndex],
                    useTacticalCrystalPreference) < 0)
            {
                bestIndex = candidateIndex;
            }
        }

        return bestIndex;
    }

    public static bool IsEligibleDotSeed(
        ScholarSpreadDotCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.Actor.IsValid &&
        candidate.Actor != localPlayer &&
        EnemySlotRules.IsValidSlot(candidate.EnemySlot) &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        HasValidHp(candidate.CurrentHp, candidate.MaximumHp) &&
        candidate.NativeTargetValid &&
        candidate.NativeRangeAndLineOfSight &&
        !candidate.HasOwnBiolysis &&
        !candidate.HasOwnBiolytic &&
        candidate.ExactCoverageKnown &&
        candidate.NewlyCoveredEnemyCount is >= MinimumUsefulSpreadTargets and
            <= MaximumEnemyTargets;

    public static bool IsEligibleShieldSeed(
        ScholarSpreadShieldCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        localPlayer.IsValid &&
        candidate.Actor.IsValid &&
        candidate.PartySlot is >= FirstPartySlot and <= LastPartySlot &&
        candidate.ExactCanonicalIdentity &&
        candidate.Alive &&
        candidate.Targetable &&
        HasValidHp(candidate.CurrentHp, candidate.MaximumHp) &&
        candidate.NativeTargetValid &&
        candidate.NativeRangeAndLineOfSight &&
        (candidate.CurrentHp < candidate.MaximumHp ||
         (candidate.TacticalCrystalPresenceKnown && candidate.OnTacticalCrystal)) &&
        !candidate.HasOwnGalvanize &&
        !candidate.HasOwnCatalyze &&
        candidate.ExactCoverageKnown &&
        candidate.NewlyCoveredPartyCount is >= MinimumUsefulSpreadTargets and
            <= MaximumPartyTargets;

    public static ScholarSpreadWorkflowState BeginWorkflow(ScholarSpreadPlan plan) =>
        plan.IsValid
            ? new ScholarSpreadWorkflowState(
                plan,
                ScholarSpreadPhase.SetupReady,
                default,
                0,
                0,
                0)
            : ScholarSpreadWorkflowState.Initial;

    public static bool TryGetNextIntent(
        ScholarSpreadWorkflowState state,
        out ScholarSpreadIntent intent)
    {
        intent = default;
        if (!state.Plan.IsValid) return false;

        var actionId = state.Phase switch
        {
            ScholarSpreadPhase.SetupReady => state.Plan.SetupActionId,
            ScholarSpreadPhase.DeploymentReady => DeploymentTacticsActionId,
            _ => 0u,
        };
        if (actionId == 0) return false;

        intent = new ScholarSpreadIntent(
            state.Plan.EpisodeToken,
            state.Plan.Kind,
            state.Phase,
            actionId,
            state.Plan.LocalPlayer,
            state.Plan.TargetSlot,
            state.Plan.Target);
        return intent.IsValid;
    }

    public static ScholarSpreadWorkflowState RecordClientAcceptedAction(
        ScholarSpreadWorkflowState state,
        ScholarSpreadIntent intent,
        ushort sourceSequence)
    {
        if (!IntentBelongsToState(state, intent))
            return Cancel(state);

        var nextPhase = intent.RequiredPhase switch
        {
            ScholarSpreadPhase.SetupReady => ScholarSpreadPhase.AwaitingSetupEffect,
            ScholarSpreadPhase.DeploymentReady => ScholarSpreadPhase.AwaitingDeploymentEffect,
            _ => ScholarSpreadPhase.Cancelled,
        };
        if (nextPhase == ScholarSpreadPhase.Cancelled) return Cancel(state);

        var ownership = new ScholarSpreadOwnedActionToken(
            state.Plan.EpisodeToken,
            intent.RequiredPhase,
            intent.ActionId,
            intent.Target,
            sourceSequence);
        return ownership.IsValid
            ? state with
            {
                Phase = nextPhase,
                PendingOwnedAction = ownership,
            }
            : Cancel(state);
    }

    public static ScholarSpreadIntentDecision EvaluateExactIntent(
        ScholarSpreadWorkflowState state,
        ScholarSpreadIntent intent,
        ScholarSpreadIntentObservation observation)
    {
        if (!IntentBelongsToState(state, intent))
            return CancelIntent(ScholarSpreadIntentDecisionReason.WorkflowStateInvalid);
        if (!observation.HeldGameplayKeyEligible)
            return CancelIntent(ScholarSpreadIntentDecisionReason.HeldGameplayKeyReleased);

        var target = observation.ExactTarget;
        if (target.LocalPlayer != state.Plan.LocalPlayer)
            return CancelIntent(ScholarSpreadIntentDecisionReason.LocalPlayerIdentityDrift);
        if (target.Kind != state.Plan.Kind ||
            target.TargetSlot != state.Plan.TargetSlot ||
            target.Target != state.Plan.Target)
        {
            return CancelIntent(ScholarSpreadIntentDecisionReason.TargetIdentityDrift);
        }

        if (!target.ExactCanonicalIdentity ||
            !target.Alive ||
            !target.Targetable ||
            !HasValidHp(target.CurrentHp, target.MaximumHp) ||
            !target.NativeTargetValid ||
            !target.NativeRangeAndLineOfSight)
        {
            return CancelIntent(ScholarSpreadIntentDecisionReason.TargetUnavailable);
        }

        if (!target.ExactCoverageKnown ||
            target.CurrentAffectedCount < MinimumUsefulSpreadTargets)
        {
            return CancelIntent(ScholarSpreadIntentDecisionReason.SpreadNoLongerUseful);
        }

        if (intent.Kind == ScholarSpreadKind.Shield &&
            intent.IsSetup &&
            target.CurrentHp >= target.MaximumHp &&
            (!target.TacticalCrystalPresenceKnown || !target.OnTacticalCrystal))
        {
            return CancelIntent(ScholarSpreadIntentDecisionReason.SpreadNoLongerUseful);
        }

        var statusPairShouldBeActive = intent.IsDeployment;
        if (target.ExpectedOwnStatusPairActive != statusPairShouldBeActive)
            return CancelIntent(ScholarSpreadIntentDecisionReason.StatusOwnershipDrift);
        if (observation.ResolvedActionId != intent.ActionId)
            return CancelIntent(ScholarSpreadIntentDecisionReason.ResolvedActionDrift);
        if (!observation.ActionLocallyReady)
            return CancelIntent(ScholarSpreadIntentDecisionReason.ActionUnavailable);

        if (intent.IsDeployment)
        {
            if (observation.DeploymentCharges is < 1 or > 2)
            {
                return CancelIntent(
                    ScholarSpreadIntentDecisionReason.DeploymentChargeUnavailable);
            }

            if (state.Plan.Kind == ScholarSpreadKind.Shield &&
                !observation.ShieldReservationStillSafe)
            {
                return CancelIntent(
                    ScholarSpreadIntentDecisionReason.ShieldReservationUnavailable);
            }
        }

        if (!observation.NativeActionBoundaryClear)
        {
            return new ScholarSpreadIntentDecision(
                ScholarSpreadIntentDecisionKind.SoftWaitNativeBoundary,
                ScholarSpreadIntentDecisionReason.NativeActionBoundaryBusy);
        }

        return new ScholarSpreadIntentDecision(
            ScholarSpreadIntentDecisionKind.Ready,
            ScholarSpreadIntentDecisionReason.None);
    }

    public static ScholarSpreadEffectDecision ObserveActionEffect(
        ScholarSpreadWorkflowState state,
        ScholarSpreadActionEffectObservation effect,
        bool shieldReservationStillSafe)
    {
        if (!state.IsActive)
            return Effect(state, ScholarSpreadEffectDecisionKind.Ignored,
                ScholarSpreadEffectDecisionReason.InactiveWorkflow);
        if (!IsRelevantAction(effect.ActionId))
            return Effect(state, ScholarSpreadEffectDecisionKind.Ignored,
                ScholarSpreadEffectDecisionReason.IrrelevantAction);
        if (effect.Caster != state.Plan.LocalPlayer)
            return Effect(state, ScholarSpreadEffectDecisionKind.Ignored,
                ScholarSpreadEffectDecisionReason.OtherCaster);

        if (effect.GlobalSequence != 0 &&
            effect.GlobalSequence == state.LastConfirmedGlobalSequence &&
            effect.SourceSequence == state.LastConfirmedSourceSequence &&
            effect.ActionId == state.LastConfirmedActionId)
        {
            return Effect(state, ScholarSpreadEffectDecisionKind.Ignored,
                ScholarSpreadEffectDecisionReason.DuplicateOwnedEffect);
        }

        var pending = state.PendingOwnedAction;
        if (pending.IsValid &&
            effect.SourceSequence != 0 &&
            (!pending.HasBoundSourceSequence ||
             effect.SourceSequence == pending.SourceSequence))
        {
            if (effect.GlobalSequence == 0 ||
                effect.ActionId != pending.ActionId ||
                effect.PrimaryTarget != pending.Target)
            {
                return Effect(Cancel(state), ScholarSpreadEffectDecisionKind.Cancelled,
                    ScholarSpreadEffectDecisionReason.OwnedSequenceMismatch);
            }

            if (pending.RequestedFromPhase == ScholarSpreadPhase.SetupReady)
            {
                if (state.Plan.Kind == ScholarSpreadKind.Shield &&
                    !shieldReservationStillSafe)
                {
                    return Effect(Cancel(state), ScholarSpreadEffectDecisionKind.Cancelled,
                        ScholarSpreadEffectDecisionReason.ShieldReservationUnavailable);
                }

                return Effect(
                    state with
                    {
                        Phase = ScholarSpreadPhase.DeploymentReady,
                        PendingOwnedAction = default,
                        LastConfirmedGlobalSequence = effect.GlobalSequence,
                        LastConfirmedSourceSequence = effect.SourceSequence,
                        LastConfirmedActionId = effect.ActionId,
                    },
                    ScholarSpreadEffectDecisionKind.OwnedSetupConfirmed,
                    ScholarSpreadEffectDecisionReason.None);
            }

            if (pending.RequestedFromPhase == ScholarSpreadPhase.DeploymentReady)
            {
                return Effect(
                    state with
                    {
                        Phase = ScholarSpreadPhase.Completed,
                        PendingOwnedAction = default,
                        LastConfirmedGlobalSequence = effect.GlobalSequence,
                        LastConfirmedSourceSequence = effect.SourceSequence,
                        LastConfirmedActionId = effect.ActionId,
                    },
                    ScholarSpreadEffectDecisionKind.OwnedDeploymentConfirmed,
                    ScholarSpreadEffectDecisionReason.None);
            }

            return Effect(Cancel(state), ScholarSpreadEffectDecisionKind.Cancelled,
                ScholarSpreadEffectDecisionReason.OwnedEffectMalformed);
        }

        // A source-sequence collision with the helper-owned request is always
        // ambiguous. Never reinterpret it as a manual action or try another target.
        if (pending.IsValid &&
            pending.HasBoundSourceSequence &&
            effect.SourceSequence == pending.SourceSequence)
        {
            return Effect(Cancel(state), ScholarSpreadEffectDecisionKind.Cancelled,
                ScholarSpreadEffectDecisionReason.OwnedSequenceMismatch);
        }

        // Any separately-issued Deployment spends the shared two-charge resource;
        // keeping an armed automatic Deployment after it could double-spend.
        if (effect.ActionId == DeploymentTacticsActionId)
        {
            return Effect(Cancel(state), ScholarSpreadEffectDecisionKind.Cancelled,
                ScholarSpreadEffectDecisionReason.ManualDeploymentConflict);
        }

        // A manual setup on the frozen seed can refresh/create the same statuses,
        // but it never owns this workflow. Cancel instead of spreading it.
        if (effect.ActionId == state.Plan.SetupActionId &&
            effect.PrimaryTarget == state.Plan.Target)
        {
            return Effect(Cancel(state), ScholarSpreadEffectDecisionKind.Cancelled,
                ScholarSpreadEffectDecisionReason.ManualSetupTargetConflict);
        }

        return Effect(state, ScholarSpreadEffectDecisionKind.Ignored,
            ScholarSpreadEffectDecisionReason.ManualUnrelatedAction);
    }

    public static ScholarSpreadWorkflowState Cancel(
        ScholarSpreadWorkflowState state) =>
        state.Plan.IsValid
            ? state with
            {
                Phase = ScholarSpreadPhase.Cancelled,
                PendingOwnedAction = default,
            }
            : ScholarSpreadWorkflowState.Initial;

    public static bool IsRelevantAction(uint actionId) =>
        actionId is AdloquiumActionId or BiolysisActionId or
            DeploymentTacticsActionId;

    private static ScholarSpreadPlanDecisionReason GetPlanningGateFailure(
        ScholarSpreadPlanningObservation observation)
    {
        if (observation.HardReset)
            return ScholarSpreadPlanDecisionReason.HardReset;
        if (!observation.ConfigurationEnabled)
            return ScholarSpreadPlanDecisionReason.ConfigurationDisabled;
        if (!observation.IsCrystallineConflict)
            return ScholarSpreadPlanDecisionReason.OutsideCrystallineConflict;
        if (!observation.MatchStarted)
            return ScholarSpreadPlanDecisionReason.MatchNotStarted;
        if (!observation.LocalPlayer.IsValid)
            return ScholarSpreadPlanDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return ScholarSpreadPlanDecisionReason.LocalPlayerDead;
        if (observation.LocalJobId != ScholarJobId)
            return ScholarSpreadPlanDecisionReason.LocalJobInvalid;
        if (!observation.MetadataVerified)
            return ScholarSpreadPlanDecisionReason.MetadataUnverified;
        if (observation.ActionHelpersSuppressedByGuard)
            return ScholarSpreadPlanDecisionReason.GuardSuppressed;
        if (!observation.InputProbeSucceeded)
            return ScholarSpreadPlanDecisionReason.InputProbeUnavailable;
        if (observation.IsTextInputActive)
            return ScholarSpreadPlanDecisionReason.TextInputActive;
        if (!observation.HeldGameplayKeyEligible)
            return ScholarSpreadPlanDecisionReason.NoHeldGameplayKey;
        if (observation.HeldGameplayKeyCode <= 0)
            return ScholarSpreadPlanDecisionReason.HeldGameplayKeyInvalid;
        return ScholarSpreadPlanDecisionReason.None;
    }

    private static bool HasUniqueDotCandidateSet(
        IReadOnlyList<ScholarSpreadDotCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null ||
            candidates.Count != CrystallineConflictRosterSize ||
            !localPlayer.IsValid)
        {
            return false;
        }

        var slots = new HashSet<int>();
        var actors = new HashSet<TargetPressureActorIdentity>();
        foreach (var candidate in candidates)
        {
            if (!EnemySlotRules.IsValidSlot(candidate.EnemySlot) ||
                !candidate.ExactCanonicalIdentity ||
                !candidate.ExactCoverageKnown ||
                !candidate.Actor.IsValid ||
                candidate.Actor == localPlayer ||
                !slots.Add(candidate.EnemySlot) ||
                !actors.Add(candidate.Actor))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUniqueShieldCandidateSet(
        IReadOnlyList<ScholarSpreadShieldCandidate>? candidates,
        TargetPressureActorIdentity localPlayer)
    {
        if (candidates is null ||
            candidates.Count != CrystallineConflictRosterSize ||
            !localPlayer.IsValid)
        {
            return false;
        }

        var slots = new HashSet<int>();
        var actors = new HashSet<TargetPressureActorIdentity>();
        foreach (var candidate in candidates)
        {
            if (candidate.PartySlot is < FirstPartySlot or > LastPartySlot ||
                !candidate.ExactCanonicalIdentity ||
                !candidate.ExactCoverageKnown ||
                !candidate.Actor.IsValid ||
                !slots.Add(candidate.PartySlot) ||
                !actors.Add(candidate.Actor))
            {
                return false;
            }
        }

        return actors.Contains(localPlayer);
    }

    private static int CompareDotSeed(
        ScholarSpreadDotCandidate left,
        ScholarSpreadDotCandidate right)
    {
        var coverage = right.NewlyCoveredEnemyCount.CompareTo(
            left.NewlyCoveredEnemyCount);
        if (coverage != 0) return coverage;

        var slot = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slot != 0) return slot;

        return CompareIdentity(left.Actor, right.Actor);
    }

    private static int CompareShieldSeed(
        ScholarSpreadShieldCandidate left,
        ScholarSpreadShieldCandidate right,
        bool useTacticalCrystalPreference)
    {
        if (useTacticalCrystalPreference)
        {
            var crystal = right.OnTacticalCrystal.CompareTo(left.OnTacticalCrystal);
            if (crystal != 0) return crystal;
        }

        var health = CompareRatio(
            left.CurrentHp,
            left.MaximumHp,
            right.CurrentHp,
            right.MaximumHp);
        if (health != 0) return health;

        var coverage = right.NewlyCoveredPartyCount.CompareTo(
            left.NewlyCoveredPartyCount);
        if (coverage != 0) return coverage;

        var slot = left.PartySlot.CompareTo(right.PartySlot);
        if (slot != 0) return slot;

        return CompareIdentity(left.Actor, right.Actor);
    }

    private static bool IntentBelongsToState(
        ScholarSpreadWorkflowState state,
        ScholarSpreadIntent intent) =>
        state.Plan.IsValid &&
        intent.IsValid &&
        state.Phase == intent.RequiredPhase &&
        intent.EpisodeToken == state.Plan.EpisodeToken &&
        intent.Kind == state.Plan.Kind &&
        intent.LocalPlayer == state.Plan.LocalPlayer &&
        intent.TargetSlot == state.Plan.TargetSlot &&
        intent.Target == state.Plan.Target &&
        intent.ActionId == (state.Phase == ScholarSpreadPhase.SetupReady
            ? state.Plan.SetupActionId
            : DeploymentTacticsActionId);

    private static ScholarSpreadIntentDecision CancelIntent(
        ScholarSpreadIntentDecisionReason reason) =>
        new(ScholarSpreadIntentDecisionKind.Cancelled, reason);

    private static ScholarSpreadEffectDecision Effect(
        ScholarSpreadWorkflowState state,
        ScholarSpreadEffectDecisionKind kind,
        ScholarSpreadEffectDecisionReason reason) =>
        new(state, kind, reason);

    private static bool HasValidHp(uint currentHp, uint maximumHp) =>
        currentHp > 0 && maximumHp > 0 && currentHp <= maximumHp;

    private static int CompareRatio(
        uint leftCurrent,
        uint leftMaximum,
        uint rightCurrent,
        uint rightMaximum) =>
        ((ulong)leftCurrent * rightMaximum).CompareTo(
            (ulong)rightCurrent * leftMaximum);

    private static int CompareIdentity(
        TargetPressureActorIdentity left,
        TargetPressureActorIdentity right)
    {
        var entity = left.EntityId.CompareTo(right.EntityId);
        return entity != 0
            ? entity
            : left.GameObjectId.CompareTo(right.GameObjectId);
    }
}
