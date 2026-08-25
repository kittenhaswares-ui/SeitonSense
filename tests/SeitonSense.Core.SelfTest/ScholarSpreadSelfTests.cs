using SeitonSense.Core;

internal static class ScholarSpreadSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer = new(10_000, 1_000);

    internal static void MetadataConstantsAreExact()
    {
        Equal(28u, ScholarSpreadRules.ScholarJobId, "Scholar job");
        Equal(29_232u, ScholarSpreadRules.AdloquiumActionId, "Adloquium");
        Equal(29_233u, ScholarSpreadRules.BiolysisActionId, "Biolysis");
        Equal(29_234u, ScholarSpreadRules.DeploymentTacticsActionId, "Deployment Tactics");
        Equal(3_087u, ScholarSpreadRules.GalvanizeStatusId, "Galvanize");
        Equal(3_088u, ScholarSpreadRules.CatalyzeStatusId, "Catalyze");
        Equal(3_089u, ScholarSpreadRules.BiolysisStatusId, "Biolysis status");
        Equal(3_090u, ScholarSpreadRules.BiolyticStatusId, "Biolytic");
        Equal(5, ScholarSpreadRules.CrystallineConflictRosterSize, "exact CC roster size");
        Equal(2, ScholarSpreadRules.MinimumExactRosterSliceSize, "minimum exact roster slice");
    }

    internal static void IndependentHeldLaneNeverClaimsSharedInput()
    {
        var enabledWhileHeld = ScholarSpreadRules.ObserveIndependentHeldConsent(
            ScholarSpreadHeldConsentState.Initial,
            configurationEnabled: true,
            heldGameplayKeyEligible: true);
        False(enabledWhileHeld.AllowsWorkflow, "enable cannot inherit an already-held key");
        True(enabledWhileHeld.NextState.RequiresReleaseAfterEnable, "release latch armed");
        False(enabledWhileHeld.ClaimsSharedInputFrame, "consent never claims shared frame");
        False(enabledWhileHeld.ConsumesSharedInputGeneration, "consent never consumes shared input");

        var stillHeld = ScholarSpreadRules.ObserveIndependentHeldConsent(
            enabledWhileHeld.NextState,
            configurationEnabled: true,
            heldGameplayKeyEligible: true);
        False(stillHeld.AllowsWorkflow, "same inherited hold stays blocked");

        var released = ScholarSpreadRules.ObserveIndependentHeldConsent(
            stillHeld.NextState,
            configurationEnabled: true,
            heldGameplayKeyEligible: false);
        False(released.AllowsWorkflow, "release is not an action");
        False(released.NextState.RequiresReleaseAfterEnable, "release opens next generation");

        var pressed = ScholarSpreadRules.ObserveIndependentHeldConsent(
            released.NextState,
            configurationEnabled: true,
            heldGameplayKeyEligible: true);
        True(pressed.AllowsWorkflow, "new held level can drive Scholar lane");
        False(pressed.ClaimsSharedInputFrame, "new hold still does not claim shared frame");

        var reset = ScholarSpreadRules.ObserveIndependentHeldConsent(
            pressed.NextState,
            configurationEnabled: false,
            heldGameplayKeyEligible: true);
        Equal(ScholarSpreadHeldConsentState.Initial, reset.NextState, "disable resets local latch");

        var preparation = ScholarSpreadRules.ObserveMatchGate(
            ScholarSpreadMatchGateState.Initial,
            new ScholarSpreadMatchGateObservation(
                TerritoryId: 1032,
                LiveContextValid: true,
                HardReset: true,
                DutyStartedRaw: false,
                DutyStartSignaled: false,
                DutyCompletionSignaled: false));
        False(preparation.AllowsActions, "CC preparation is not a running match");

        var started = ScholarSpreadRules.ObserveMatchGate(
            preparation,
            new ScholarSpreadMatchGateObservation(
                TerritoryId: 1032,
                LiveContextValid: true,
                HardReset: false,
                DutyStartedRaw: false,
                DutyStartSignaled: true,
                DutyCompletionSignaled: false));
        True(started.AllowsActions, "Duty Start event opens Scholar actions");

        var transientFalse = ScholarSpreadRules.ObserveMatchGate(
            started,
            new ScholarSpreadMatchGateObservation(
                TerritoryId: 1032,
                LiveContextValid: true,
                HardReset: false,
                DutyStartedRaw: false,
                DutyStartSignaled: false,
                DutyCompletionSignaled: false));
        True(transientFalse.AllowsActions,
            "a false IsDutyStarted poll cannot stop an active match");

        var completed = ScholarSpreadRules.ObserveMatchGate(
            transientFalse,
            new ScholarSpreadMatchGateObservation(
                TerritoryId: 1032,
                LiveContextValid: true,
                HardReset: false,
                DutyStartedRaw: true,
                DutyStartSignaled: true,
                DutyCompletionSignaled: true));
        False(completed.AllowsActions, "completion wins over stale start evidence");

        var nextPreparation = ScholarSpreadRules.ObserveMatchGate(
            completed,
            new ScholarSpreadMatchGateObservation(
                TerritoryId: 1033,
                LiveContextValid: true,
                HardReset: true,
                DutyStartedRaw: false,
                DutyStartSignaled: false,
                DutyCompletionSignaled: false));
        False(nextPreparation.AllowsActions, "new territory returns to preparation");
    }

    internal static void DotSequenceWinsAndRanksMaximumExactCoverage()
    {
        var dots = new[]
        {
            Dot(1, affected: 2),
            Dot(2, affected: 5),
            Dot(3, affected: 4),
            Dot(4, affected: 2),
            Dot(5, affected: 3),
        };
        var observation = Observation() with
        {
            BiolysisLocallyReady = true,
            AdloquiumLocallyReady = true,
            DeploymentCharges = 2,
            DotCandidates = dots,
            ShieldCandidates = [Shield(1, hp: 1, onCrystal: true, affected: 5)],
        };

        var decision = ScholarSpreadRules.PlanNextSequence(observation, episodeToken: 77);
        True(decision.HasPlan, "plan exists");
        Equal(ScholarSpreadKind.Dot, decision.Plan!.Value.Kind, "DoT always precedes shield");
        Equal(1, decision.SelectedCandidateIndex, "largest exact enemy cluster");
        Equal(dots[1].Actor, decision.Plan.Value.Target, "frozen maximum-coverage seed");
        Equal(5, decision.Plan.Value.PredictedAffectedCount, "frozen coverage diagnostics");
        False(decision.ClaimsSharedInputFrame, "plan never claims main lane");
        False(decision.ConsumesSharedInputGeneration, "plan never consumes main input");

        var preparation = ScholarSpreadRules.PlanNextSequence(
            observation with { MatchStarted = false },
            episodeToken: 76);
        False(preparation.HasPlan, "preparation cannot start a Scholar workflow");
        Equal(ScholarSpreadPlanDecisionReason.MatchNotStarted, preparation.Reason,
            "match start is an explicit planning gate");

        dots[0] = dots[0] with { NewlyCoveredEnemyCount = 5 };
        Equal(
            0,
            ScholarSpreadRules.SelectBestDotSeedIndex(dots, LocalPlayer),
            "equal clusters use stable enemy slot");

        dots[0] = dots[0] with { HasOwnBiolysis = true };
        Equal(
            1,
            ScholarSpreadRules.SelectBestDotSeedIndex(dots, LocalPlayer),
            "an existing locally-owned DoT cannot be used as an automatic setup seed");

        var duplicate = dots.ToArray();
        duplicate[2] = duplicate[2] with { Actor = duplicate[1].Actor };
        Equal(-1, ScholarSpreadRules.SelectBestDotSeedIndex(duplicate, LocalPlayer),
            "ambiguous duplicate actor fails closed");
        var exactSlice = new[] { Dot(2, affected: 2), Dot(4, affected: 2) };
        Equal(0, ScholarSpreadRules.SelectBestDotSeedIndex(exactSlice, LocalPlayer),
            "two stable exact enemies are enough for a useful spread");
        Equal(-1, ScholarSpreadRules.SelectBestDotSeedIndex(exactSlice[..1], LocalPlayer),
            "one enemy cannot create a useful exact spread");

        var unknownCoverage = dots.ToArray();
        unknownCoverage[4] = unknownCoverage[4] with { ExactCoverageKnown = false };
        Equal(-1, ScholarSpreadRules.SelectBestDotSeedIndex(unknownCoverage, LocalPlayer),
            "one unknown enemy coverage invalidates maximum ranking");
    }

    internal static void ShieldReservationProtectsNextDotOpportunity()
    {
        True(
            ScholarSpreadRules.IsBiolysisPlanningReady(
                ownRecastTimingKnown: true,
                ownRecastRemainingMilliseconds: 0,
                actionResourcesAvailable: true,
                finalNativeReady: false),
            "a shared PvP GCD cannot make charged Adlo outrank ready Biolysis");
        False(
            ScholarSpreadRules.IsBiolysisPlanningReady(
                ownRecastTimingKnown: true,
                ownRecastRemainingMilliseconds: 1,
                actionResourcesAvailable: true,
                finalNativeReady: true),
            "Biolysis own recast still blocks DoT planning");
        False(
            ScholarSpreadRules.IsBiolysisPlanningReady(
                ownRecastTimingKnown: true,
                ownRecastRemainingMilliseconds: 0,
                actionResourcesAvailable: false,
                finalNativeReady: true),
            "missing Biolysis resources still fail closed");
        False(
            ScholarSpreadRules.IsBiolysisPlanningReady(
                ownRecastTimingKnown: false,
                ownRecastRemainingMilliseconds: -1,
                actionResourcesAvailable: true,
                finalNativeReady: false),
            "unknown own recast falls back to final native readiness");

        True(
            ScholarSpreadRules.CanSpendDeploymentOnShield(
                currentDeploymentCharges: 2,
                deploymentNextChargeTimingKnown: false,
                deploymentNextChargeRemainingMilliseconds: -1,
                biolysisTimingKnown: false,
                biolysisRemainingMilliseconds: -1),
            "two charges leave one reserved without timer knowledge");
        True(
            ScholarSpreadRules.CanSpendDeploymentOnShield(1, true, 8_000, true, 9_000),
            "next Deployment charge returns before Biolysis");
        True(
            ScholarSpreadRules.CanSpendDeploymentOnShield(1, true, 9_000, true, 9_000),
            "equal ready times are safe");
        False(
            ScholarSpreadRules.CanSpendDeploymentOnShield(1, true, 9_001, true, 9_000),
            "late Deployment charge is reserved");
        False(
            ScholarSpreadRules.CanSpendDeploymentOnShield(1, false, 8_000, true, 9_000),
            "unknown Deployment timer fails closed");
        False(
            ScholarSpreadRules.CanSpendDeploymentOnShield(1, true, 8_000, false, 9_000),
            "unknown Biolysis timer fails closed");
        False(
            ScholarSpreadRules.CanSpendDeploymentOnShield(1, true, 8_000, true, 0),
            "last charge cannot be spent while Biolysis is ready now");
        False(
            ScholarSpreadRules.CanSpendDeploymentOnShield(0, true, 1, true, 9_000),
            "no current charge");

        var safe = ScholarSpreadRules.PlanNextSequence(
            Observation() with
            {
                BiolysisLocallyReady = false,
                AdloquiumLocallyReady = true,
                DeploymentCharges = 1,
                DeploymentNextChargeTimingKnown = true,
                DeploymentNextChargeRemainingMilliseconds = 8_000,
                BiolysisTimingKnown = true,
                BiolysisRemainingMilliseconds = 9_000,
                ShieldCandidates =
                [
                    Shield(1, hp: 50, affected: 3),
                    Shield(2, hp: 60, affected: 2),
                    Shield(3, hp: 70, affected: 2),
                    Shield(4, hp: 80, affected: 2),
                    Shield(5, hp: 90, affected: 2),
                ],
            },
            episodeToken: 1);
        True(safe.HasPlan, "safe shield plan");
        Equal(ScholarSpreadKind.Shield, safe.Plan!.Value.Kind, "shield kind");

        var unsafePlan = ScholarSpreadRules.PlanNextSequence(
            Observation() with
            {
                BiolysisLocallyReady = false,
                AdloquiumLocallyReady = true,
                DeploymentCharges = 1,
                DeploymentNextChargeTimingKnown = true,
                DeploymentNextChargeRemainingMilliseconds = 10_000,
                BiolysisTimingKnown = true,
                BiolysisRemainingMilliseconds = 9_000,
                ShieldCandidates =
                [
                    Shield(1, hp: 50, affected: 3),
                    Shield(2, hp: 60, affected: 2),
                    Shield(3, hp: 70, affected: 2),
                    Shield(4, hp: 80, affected: 2),
                    Shield(5, hp: 90, affected: 2),
                ],
            },
            episodeToken: 2);
        False(unsafePlan.HasPlan, "unsafe shield cannot consume reserved charge");
    }

    internal static void ShieldRanksCrystalThenExactHp()
    {
        var candidates = new[]
        {
            Shield(1, hp: 10, onCrystal: false, affected: 4),
            Shield(2, hp: 80, onCrystal: true, affected: 2),
            Shield(3, hp: 40, onCrystal: false, affected: 5),
            Shield(4, hp: 60, onCrystal: false, affected: 3),
            Shield(5, hp: 70, onCrystal: false, affected: 2),
        };
        Equal(
            1,
            ScholarSpreadRules.SelectBestShieldSeedIndex(candidates, LocalPlayer),
            "verified crystal member precedes lower HP elsewhere");

        candidates[2] = candidates[2] with { TacticalCrystalPresenceKnown = false };
        Equal(
            0,
            ScholarSpreadRules.SelectBestShieldSeedIndex(candidates, LocalPlayer),
            "one unknown crystal classification falls back wholesale to exact HP");

        candidates[2] = candidates[2] with
        {
            TacticalCrystalPresenceKnown = true,
            CurrentHp = 10,
            MaximumHp = 100,
        };
        candidates[1] = candidates[1] with { OnTacticalCrystal = false };
        Equal(
            2,
            ScholarSpreadRules.SelectBestShieldSeedIndex(candidates, LocalPlayer),
            "equal HP uses larger exact spread, then stable slot");
        var exactSlice = new[]
        {
            Shield(1, hp: 10, affected: 2),
            Shield(4, hp: 60, affected: 2),
        };
        Equal(0, ScholarSpreadRules.SelectBestShieldSeedIndex(exactSlice, LocalPlayer),
            "two stable exact party members are enough for a useful spread");
        Equal(-1, ScholarSpreadRules.SelectBestShieldSeedIndex(exactSlice[..1], LocalPlayer),
            "one party member cannot create a useful exact spread");

        var unknownCoverage = candidates.ToArray();
        unknownCoverage[4] = unknownCoverage[4] with { ExactCoverageKnown = false };
        Equal(-1, ScholarSpreadRules.SelectBestShieldSeedIndex(unknownCoverage, LocalPlayer),
            "one unknown party coverage invalidates exact ranking");

        var missingSelf = candidates.ToArray();
        missingSelf[0] = missingSelf[0] with { Actor = Ally(6) };
        Equal(-1, ScholarSpreadRules.SelectBestShieldSeedIndex(missingSelf, LocalPlayer),
            "party roster without the local player fails closed");

        var fullHealthOffObjective = candidates
            .Select(candidate => candidate with
            {
                CurrentHp = candidate.MaximumHp,
                OnTacticalCrystal = false,
            })
            .ToArray();
        Equal(-1,
            ScholarSpreadRules.SelectBestShieldSeedIndex(fullHealthOffObjective, LocalPlayer),
            "full-health allies away from the objective do not create an Adlo rotation");

        fullHealthOffObjective[1] = fullHealthOffObjective[1] with
        {
            OnTacticalCrystal = true,
        };
        Equal(1,
            ScholarSpreadRules.SelectBestShieldSeedIndex(fullHealthOffObjective, LocalPlayer),
            "an objective seed still permits a proactive shield");

        True(ScholarSpreadRules.IsCleanSetupSeed(false, false),
            "a setup seed is clean only when neither owned status remains");
        False(ScholarSpreadRules.IsCleanSetupSeed(true, false),
            "a consumed-shield half pair is not a clean setup seed");
        False(ScholarSpreadRules.IsCleanSetupSeed(false, true),
            "a companion-only half pair is not a clean setup seed");
        False(ScholarSpreadRules.IsCompleteOwnedSetupStatusPair(true, false),
            "the first staggered status is not complete setup proof");
        True(ScholarSpreadRules.IsCompleteOwnedSetupStatusPair(true, true),
            "both expected statuses provide complete setup proof");
        False(ScholarSpreadRules.HasDeployableOwnedSetupStatus(false, true, false),
            "a first half-status cannot deploy before complete-pair proof");
        True(ScholarSpreadRules.HasDeployableOwnedSetupStatus(true, true, false),
            "a first status remaining after complete-pair proof stays deployable");
        True(ScholarSpreadRules.HasDeployableOwnedSetupStatus(true, false, true),
            "a companion status remaining after complete-pair proof stays deployable");
        False(ScholarSpreadRules.HasDeployableOwnedSetupStatus(true, false, false),
            "an entirely expired proven setup cannot deploy");
    }

    internal static void OwnedRequestOrExactStatusPairAdvancesWorkflow()
    {
        var state = BeginDotWorkflow();
        True(ScholarSpreadRules.TryGetNextIntent(state, out var setup), "setup intent");
        Equal(ScholarSpreadRules.BiolysisActionId, setup.ActionId, "Biolysis first");

        var ready = ScholarSpreadRules.EvaluateExactIntent(
            state,
            setup,
            IntentObservation(state, setup, ownStatusPairActive: false));
        True(ready.CanDispatch, "exact setup ready");
        False(ready.ClaimsSharedInputFrame, "ready Scholar action does not claim main lane");

        var busy = ScholarSpreadRules.EvaluateExactIntent(
            state,
            setup,
            IntentObservation(state, setup, ownStatusPairActive: false) with
            {
                NativeActionBoundaryClear = false,
            });
        True(busy.ShouldSoftWait, "real native boundary is the sole soft wait");
        False(busy.ClaimsSharedInputFrame, "soft wait cannot block main lane");

        var transientlyUnavailable = ScholarSpreadRules.EvaluateExactIntent(
            state,
            setup,
            IntentObservation(state, setup, ownStatusPairActive: false) with
            {
                ActionLocallyReady = false,
            });
        True(transientlyUnavailable.ShouldSoftWait,
            "transient action readiness waits without retiring the frozen chain");
        Equal(ScholarSpreadIntentDecisionReason.ActionUnavailable,
            transientlyUnavailable.Reason,
            "transient action readiness keeps its exact diagnostic reason");

        var unbound = ScholarSpreadRules.RecordClientAcceptedAction(
            state,
            setup,
            sourceSequence: 0);
        True(unbound.PendingOwnedAction.IsValid,
            "a synchronously accepted call may wait to bind its server source sequence");
        False(unbound.PendingOwnedAction.HasBoundSourceSequence,
            "zero means pending server binding, not manual ownership");
        var unboundConfirmed = ScholarSpreadRules.ObserveActionEffect(
            unbound,
            Effect(
                ScholarSpreadRules.BiolysisActionId,
                unbound.Plan.Target,
                sourceSequence: 39,
                globalSequence: 399),
            shieldReservationStillSafe: true);
        True(unboundConfirmed.Advanced,
            "first exact nonzero server packet binds an accepted setup");
        False(unboundConfirmed.NextState.OwnedSetupPairWasComplete,
            "ActionEffect alone cannot prove both staggered statuses arrived");
        var firstStaggeredStatus = ScholarSpreadRules.ObserveCompleteOwnedSetupStatusPair(
            unboundConfirmed.NextState,
            unboundConfirmed.NextState.Plan.Target,
            hasFirstExpectedStatus: true,
            hasSecondExpectedStatus: false);
        False(firstStaggeredStatus.OwnedSetupPairWasComplete,
            "first staggered status cannot open Deployment");
        var completePair = ScholarSpreadRules.ObserveCompleteOwnedSetupStatusPair(
            firstStaggeredStatus,
            firstStaggeredStatus.Plan.Target,
            hasFirstExpectedStatus: true,
            hasSecondExpectedStatus: true);
        True(completePair.OwnedSetupPairWasComplete,
            "full pair records deterministic setup proof");
        True(ScholarSpreadRules.HasDeployableOwnedSetupStatus(
                completePair.OwnedSetupPairWasComplete,
                hasFirstExpectedStatus: false,
                hasSecondExpectedStatus: true),
            "later half-pair remains deployable after full-pair proof");

        var zeroSequence = ScholarSpreadRules.RecordClientAcceptedAction(
            BeginDotWorkflow(),
            setup,
            sourceSequence: 38);
        var zeroSequenceConfirmed = ScholarSpreadRules.ObserveActionEffect(
            zeroSequence,
            Effect(
                ScholarSpreadRules.BiolysisActionId,
                zeroSequence.Plan.Target,
                sourceSequence: 0,
                globalSequence: 398),
            shieldReservationStillSafe: true);
        True(zeroSequenceConfirmed.Advanced,
            "an exact accepted request tolerates a missing packet source sequence");

        var unusableMetadata = ScholarSpreadRules.RecordClientAcceptedAction(
            BeginDotWorkflow(),
            setup,
            sourceSequence: 36);
        var zeroGlobal = ScholarSpreadRules.ObserveActionEffect(
            unusableMetadata,
            Effect(
                ScholarSpreadRules.BiolysisActionId,
                unusableMetadata.Plan.Target,
                sourceSequence: 36,
                globalSequence: 0),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionKind.Ignored, zeroGlobal.Kind,
            "zero global sequence waits for exact statuses instead of cancelling");
        Equal(ScholarSpreadPhase.AwaitingSetupEffect, zeroGlobal.NextState.Phase,
            "zero global sequence preserves accepted setup ownership");
        var mismatchedSource = ScholarSpreadRules.ObserveActionEffect(
            zeroGlobal.NextState,
            Effect(
                ScholarSpreadRules.BiolysisActionId,
                unusableMetadata.Plan.Target,
                sourceSequence: 999,
                globalSequence: 396),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionKind.Ignored, mismatchedSource.Kind,
            "exact setup with unusable source metadata waits for status proof");
        Equal(ScholarSpreadPhase.AwaitingSetupEffect, mismatchedSource.NextState.Phase,
            "mismatched packet metadata cannot terminally consume the hold");
        True(ScholarSpreadRules.ConfirmPendingSetupFromExactStatusPair(
                mismatchedSource.NextState,
                unusableMetadata.Plan.Target,
                expectedOwnStatusPairActive: true,
                shieldReservationStillSafe: true).Advanced,
            "exact own statuses recover from unusable packet metadata");

        var statusAwaiting = ScholarSpreadRules.RecordClientAcceptedAction(
            BeginDotWorkflow(),
            setup,
            sourceSequence: 37);
        var statusConfirmed = ScholarSpreadRules.ConfirmPendingSetupFromExactStatusPair(
            statusAwaiting,
            statusAwaiting.Plan.Target,
            expectedOwnStatusPairActive: true,
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionKind.OwnedSetupConfirmed, statusConfirmed.Kind,
            "exact own statuses confirm the already accepted setup without a packet");
        Equal(ScholarSpreadPhase.DeploymentReady, statusConfirmed.NextState.Phase,
            "status confirmation arms Deployment");
        True(statusConfirmed.NextState.OwnedSetupPairWasComplete,
            "direct status confirmation records the complete pair");
        var delayedSetupPacket = ScholarSpreadRules.ObserveActionEffect(
            statusConfirmed.NextState,
            Effect(
                ScholarSpreadRules.BiolysisActionId,
                statusAwaiting.Plan.Target,
                sourceSequence: 999,
                globalSequence: 0),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionReason.DuplicateOwnedEffect,
            delayedSetupPacket.Reason,
            "the delayed packet after status confirmation cannot cancel Deployment");
        Equal(ScholarSpreadPhase.DeploymentReady, delayedSetupPacket.NextState.Phase,
            "delayed packet preserves the armed Deployment");

        var absentPair = ScholarSpreadRules.ConfirmPendingSetupFromExactStatusPair(
            statusAwaiting,
            statusAwaiting.Plan.Target,
            expectedOwnStatusPairActive: false,
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadPhase.AwaitingSetupEffect, absentPair.NextState.Phase,
            "an absent exact status pair cannot confirm setup");

        True(ScholarSpreadRules.IsWithinOwnedConfirmationWindow(
                acceptedAtMilliseconds: 1_000,
                nowMilliseconds: 3_500,
                maximumAgeMilliseconds: 2_500),
            "exact ownership deadline remains inclusive");
        False(ScholarSpreadRules.IsWithinOwnedConfirmationWindow(
                acceptedAtMilliseconds: 1_000,
                nowMilliseconds: 3_501,
                maximumAgeMilliseconds: 2_500),
            "evidence one millisecond after ownership expiry is rejected");
        False(ScholarSpreadRules.IsWithinOwnedConfirmationWindow(
                acceptedAtMilliseconds: 1_000,
                nowMilliseconds: 999,
                maximumAgeMilliseconds: 2_500),
            "clock reversal cannot confirm an owned action");

        state = ScholarSpreadRules.RecordClientAcceptedAction(state, setup, sourceSequence: 41);
        Equal(ScholarSpreadPhase.AwaitingSetupEffect, state.Phase, "await helper-owned packet");
        Equal((ushort)41, state.PendingOwnedAction.SourceSequence, "owned source sequence");

        var unrelatedManual = ScholarSpreadRules.ObserveActionEffect(
            state,
            Effect(
                actionId: ScholarSpreadRules.BiolysisActionId,
                target: Enemy(4),
                sourceSequence: 40,
                globalSequence: 400),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionKind.Ignored, unrelatedManual.Kind,
            "manual Bio on another target cannot advance");
        Equal(ScholarSpreadPhase.AwaitingSetupEffect, unrelatedManual.NextState.Phase,
            "still waiting for exact owned packet");

        var setupConfirmed = ScholarSpreadRules.ObserveActionEffect(
            state,
            Effect(
                ScholarSpreadRules.BiolysisActionId,
                state.Plan.Target,
                sourceSequence: 41,
                globalSequence: 401),
            shieldReservationStillSafe: true);
        True(setupConfirmed.Advanced, "owned setup advances");
        Equal(ScholarSpreadPhase.DeploymentReady, setupConfirmed.NextState.Phase,
            "Deployment armed only by owned Bio");

        state = setupConfirmed.NextState;
        True(ScholarSpreadRules.TryGetNextIntent(state, out var deployment), "Deployment intent");
        Equal(ScholarSpreadRules.DeploymentTacticsActionId, deployment.ActionId,
            "Deployment second");
        var deployReady = ScholarSpreadRules.EvaluateExactIntent(
            state,
            deployment,
            IntentObservation(state, deployment, ownStatusPairActive: true));
        True(deployReady.CanDispatch, "owned statuses permit Deployment");

        state = ScholarSpreadRules.RecordClientAcceptedAction(
            state,
            deployment,
            sourceSequence: 42);
        var deployed = ScholarSpreadRules.ObserveActionEffect(
            state,
            Effect(
                ScholarSpreadRules.DeploymentTacticsActionId,
                state.Plan.Target,
                sourceSequence: 42,
                globalSequence: 402),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionKind.OwnedDeploymentConfirmed, deployed.Kind,
            "owned Deployment confirmation");
        Equal(ScholarSpreadPhase.Completed, deployed.NextState.Phase, "workflow complete");
    }

    internal static void ManualActionsCannotHijackOrDoubleDeploy()
    {
        False(ScholarSpreadRules.RequiresHeldReleaseAfterEffectCancellation(
                ScholarSpreadEffectDecisionReason.ManualDeploymentConflict),
            "manual Deployment conflict may replan on the continuing hold");
        False(ScholarSpreadRules.RequiresHeldReleaseAfterEffectCancellation(
                ScholarSpreadEffectDecisionReason.ManualSetupTargetConflict),
            "manual setup conflict may replan on the continuing hold");
        False(ScholarSpreadRules.RequiresHeldReleaseAfterEffectCancellation(
                ScholarSpreadEffectDecisionReason.ShieldReservationUnavailable),
            "a deterministic reservation loss may replan on the continuing hold");
        True(ScholarSpreadRules.RequiresHeldReleaseAfterEffectCancellation(
                ScholarSpreadEffectDecisionReason.OwnedSequenceMismatch),
            "ambiguous owned sequence evidence remains release-terminal");
        True(ScholarSpreadRules.RequiresHeldReleaseAfterEffectCancellation(
                ScholarSpreadEffectDecisionReason.OwnedEffectMalformed),
            "malformed owned effect evidence remains release-terminal");

        var initial = BeginDotWorkflow();
        var manualOther = ScholarSpreadRules.ObserveActionEffect(
            initial,
            Effect(ScholarSpreadRules.AdloquiumActionId, LocalPlayer, 10, 100),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionKind.Ignored, manualOther.Kind,
            "unrelated manual Adlo does not disturb DoT plan");

        var manualSeed = ScholarSpreadRules.ObserveActionEffect(
            initial,
            Effect(ScholarSpreadRules.BiolysisActionId, initial.Plan.Target, 11, 101),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionReason.ManualSetupTargetConflict, manualSeed.Reason,
            "manual Bio on frozen seed is never adopted");
        Equal(ScholarSpreadPhase.Cancelled, manualSeed.NextState.Phase,
            "manual seed setup cancels instead of hijacking");

        var manualDeploy = ScholarSpreadRules.ObserveActionEffect(
            initial,
            Effect(ScholarSpreadRules.DeploymentTacticsActionId, initial.Plan.Target, 12, 102),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionReason.ManualDeploymentConflict, manualDeploy.Reason,
            "manual Deployment consumes shared charge");
        Equal(ScholarSpreadPhase.Cancelled, manualDeploy.NextState.Phase,
            "automatic Deployment cannot double-spend");

        True(ScholarSpreadRules.TryGetNextIntent(initial, out var setup), "setup intent");
        var awaiting = ScholarSpreadRules.RecordClientAcceptedAction(initial, setup, 50);
        var wrongOwnedTarget = ScholarSpreadRules.ObserveActionEffect(
            awaiting,
            Effect(ScholarSpreadRules.BiolysisActionId, Enemy(5), 50, 500),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionReason.OwnedSequenceMismatch, wrongOwnedTarget.Reason,
            "same source sequence with target drift is ambiguous");
        Equal(ScholarSpreadPhase.Cancelled, wrongOwnedTarget.NextState.Phase,
            "ambiguous owned packet fails closed");

        var confirmed = ScholarSpreadRules.ObserveActionEffect(
            awaiting,
            Effect(ScholarSpreadRules.BiolysisActionId, awaiting.Plan.Target, 50, 501),
            shieldReservationStillSafe: true);
        var duplicate = ScholarSpreadRules.ObserveActionEffect(
            confirmed.NextState,
            Effect(ScholarSpreadRules.BiolysisActionId, awaiting.Plan.Target, 50, 501),
            shieldReservationStillSafe: true);
        Equal(ScholarSpreadEffectDecisionReason.DuplicateOwnedEffect, duplicate.Reason,
            "duplicate packet is ignored, not mistaken for manual input");
        Equal(ScholarSpreadPhase.DeploymentReady, duplicate.NextState.Phase,
            "duplicate preserves armed Deployment");
    }

    internal static void ExactTargetRevalidationNeverFallsBack()
    {
        var state = BeginDotWorkflow();
        True(ScholarSpreadRules.TryGetNextIntent(state, out var setup), "setup intent");
        var valid = IntentObservation(state, setup, ownStatusPairActive: false);

        CancelIntent(
            state,
            setup,
            valid with
            {
                ExactTarget = valid.ExactTarget with
                {
                    TargetSlot = 2,
                    Target = Enemy(2),
                },
            },
            ScholarSpreadIntentDecisionReason.TargetIdentityDrift);
        CancelIntent(
            state,
            setup,
            valid with
            {
                ExactTarget = valid.ExactTarget with { CurrentAffectedCount = 1 },
            },
            ScholarSpreadIntentDecisionReason.SpreadNoLongerUseful);
        CancelIntent(
            state,
            setup,
            valid with
            {
                ExactTarget = valid.ExactTarget with { ExpectedOwnStatusPairActive = true },
            },
            ScholarSpreadIntentDecisionReason.StatusOwnershipDrift);
        CancelIntent(
            state,
            setup,
            valid with { HeldGameplayKeyEligible = false },
            ScholarSpreadIntentDecisionReason.HeldGameplayKeyReleased);

        var shieldSetupState = ScholarSpreadRules.BeginWorkflow(
            Plan(ScholarSpreadKind.Shield, LocalPlayer, targetSlot: 1, affected: 3));
        True(ScholarSpreadRules.TryGetNextIntent(
            shieldSetupState,
            out var shieldSetup), "shield setup intent");
        var shieldSetupObservation = IntentObservation(
            shieldSetupState,
            shieldSetup,
            ownStatusPairActive: false);
        var damagedOffCrystal = shieldSetupObservation with
        {
            ExactTarget = shieldSetupObservation.ExactTarget with
            {
                CurrentHp = 99,
                MaximumHp = 100,
                TacticalCrystalPresenceKnown = true,
                OnTacticalCrystal = false,
            },
        };
        True(
            ScholarSpreadRules.EvaluateExactIntent(
                shieldSetupState,
                shieldSetup,
                damagedOffCrystal).CanDispatch,
            "damaged off-crystal seed remains useful");
        CancelIntent(
            shieldSetupState,
            shieldSetup,
            damagedOffCrystal with
            {
                ExactTarget = damagedOffCrystal.ExactTarget with { CurrentHp = 100 },
            },
            ScholarSpreadIntentDecisionReason.SpreadNoLongerUseful);

        var fullHealthOnCrystal = damagedOffCrystal with
        {
            ExactTarget = damagedOffCrystal.ExactTarget with
            {
                CurrentHp = 100,
                OnTacticalCrystal = true,
            },
        };
        True(
            ScholarSpreadRules.EvaluateExactIntent(
                shieldSetupState,
                shieldSetup,
                fullHealthOnCrystal).CanDispatch,
            "full-health tactical-crystal seed remains proactively useful");

        var shieldState = ScholarSpreadRules.BeginWorkflow(
            Plan(ScholarSpreadKind.Shield, LocalPlayer, targetSlot: 1, affected: 3));
        True(ScholarSpreadRules.TryGetNextIntent(shieldState, out var adlo), "Adlo intent");
        shieldState = ScholarSpreadRules.RecordClientAcceptedAction(shieldState, adlo, 61);
        var confirmedShield = ScholarSpreadRules.ObserveActionEffect(
            shieldState,
            Effect(ScholarSpreadRules.AdloquiumActionId, shieldState.Plan.Target, 61, 601),
            shieldReservationStillSafe: true).NextState;
        True(ScholarSpreadRules.TryGetNextIntent(confirmedShield, out var shieldDeploy),
            "shield Deployment intent");
        var reserveLost = ScholarSpreadRules.EvaluateExactIntent(
            confirmedShield,
            shieldDeploy,
            IntentObservation(
                confirmedShield,
                shieldDeploy,
                ownStatusPairActive: true) with
            {
                ShieldReservationStillSafe = false,
            });
        Equal(ScholarSpreadIntentDecisionReason.ShieldReservationUnavailable,
            reserveLost.Reason, "reserve is rechecked immediately before Deployment");
        False(reserveLost.CanDispatch, "unsafe shield spread cancels");
    }

    private static ScholarSpreadPlanningObservation Observation() =>
        new(
            ConfigurationEnabled: true,
            IsCrystallineConflict: true,
            MatchStarted: true,
            LocalJobId: ScholarSpreadRules.ScholarJobId,
            LocalPlayer,
            IsLocalPlayerAlive: true,
            MetadataVerified: true,
            ActionHelpersSuppressedByGuard: false,
            InputProbeSucceeded: true,
            IsTextInputActive: false,
            HeldGameplayKeyEligible: true,
            HeldGameplayKeyCode: 0x57,
            BiolysisLocallyReady: false,
            AdloquiumLocallyReady: false,
            DeploymentCharges: 2,
            DeploymentNextChargeTimingKnown: true,
            DeploymentNextChargeRemainingMilliseconds: 1,
            BiolysisTimingKnown: true,
            BiolysisRemainingMilliseconds: 1,
            DotCandidates: null,
            ShieldCandidates: null);

    private static ScholarSpreadDotCandidate Dot(int slot, int affected) =>
        new(
            slot,
            Enemy(slot),
            ExactCanonicalIdentity: true,
            Alive: true,
            Targetable: true,
            CurrentHp: 100,
            MaximumHp: 100,
            NativeTargetValid: true,
            NativeRangeAndLineOfSight: true,
            HasOwnBiolysis: false,
            HasOwnBiolytic: false,
            ExactCoverageKnown: true,
            NewlyCoveredEnemyCount: affected);

    private static ScholarSpreadShieldCandidate Shield(
        int slot,
        uint hp,
        bool onCrystal = false,
        int affected = 2) =>
        new(
            slot,
            slot == 1 ? LocalPlayer : Ally(slot),
            ExactCanonicalIdentity: true,
            Alive: true,
            Targetable: true,
            CurrentHp: hp,
            MaximumHp: 100,
            NativeTargetValid: true,
            NativeRangeAndLineOfSight: true,
            HasOwnGalvanize: false,
            HasOwnCatalyze: false,
            TacticalCrystalPresenceKnown: true,
            OnTacticalCrystal: onCrystal,
            ExactCoverageKnown: true,
            NewlyCoveredPartyCount: affected);

    private static ScholarSpreadWorkflowState BeginDotWorkflow() =>
        ScholarSpreadRules.BeginWorkflow(
            Plan(ScholarSpreadKind.Dot, Enemy(1), targetSlot: 1, affected: 4));

    private static ScholarSpreadPlan Plan(
        ScholarSpreadKind kind,
        TargetPressureActorIdentity target,
        int targetSlot,
        int affected) =>
        new(
            EpisodeToken: 7,
            kind,
            LocalPlayer,
            targetSlot,
            target,
            HeldGameplayKeyCode: 0x57,
            PredictedAffectedCount: affected);

    private static ScholarSpreadIntentObservation IntentObservation(
        ScholarSpreadWorkflowState state,
        ScholarSpreadIntent intent,
        bool ownStatusPairActive) =>
        new(
            new ScholarSpreadExactTargetSnapshot(
                state.Plan.Kind,
                state.Plan.TargetSlot,
                state.Plan.LocalPlayer,
                state.Plan.Target,
                ExactCanonicalIdentity: true,
                Alive: true,
                Targetable: true,
                CurrentHp: 100,
                MaximumHp: 100,
                NativeTargetValid: true,
                NativeRangeAndLineOfSight: true,
                TacticalCrystalPresenceKnown: true,
                OnTacticalCrystal: true,
                ExactCoverageKnown: true,
                CurrentAffectedCount: state.Plan.PredictedAffectedCount,
                ExpectedOwnStatusPairActive: ownStatusPairActive),
            HeldGameplayKeyEligible: true,
            NativeActionBoundaryClear: true,
            ResolvedActionId: intent.ActionId,
            ActionLocallyReady: true,
            DeploymentCharges: 1,
            ShieldReservationStillSafe: true);

    private static ScholarSpreadActionEffectObservation Effect(
        uint actionId,
        TargetPressureActorIdentity target,
        ushort sourceSequence,
        uint globalSequence) =>
        new(
            LocalPlayer,
            target,
            actionId,
            globalSequence,
            sourceSequence);

    private static TargetPressureActorIdentity Enemy(int slot) =>
        new((ulong)(20_000 + slot), (uint)(2_000 + slot));

    private static TargetPressureActorIdentity Ally(int slot) =>
        new((ulong)(30_000 + slot), (uint)(3_000 + slot));

    private static void CancelIntent(
        ScholarSpreadWorkflowState state,
        ScholarSpreadIntent intent,
        ScholarSpreadIntentObservation observation,
        ScholarSpreadIntentDecisionReason expectedReason)
    {
        var decision = ScholarSpreadRules.EvaluateExactIntent(state, intent, observation);
        False(decision.CanDispatch, expectedReason.ToString());
        False(decision.ShouldSoftWait, $"{expectedReason} is terminal");
        False(decision.ClaimsSharedInputFrame, $"{expectedReason} cannot claim main lane");
        Equal(expectedReason, decision.Reason, expectedReason.ToString());
    }

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label)
    {
        if (condition) throw new InvalidOperationException($"Expected false: {label}");
    }
}
