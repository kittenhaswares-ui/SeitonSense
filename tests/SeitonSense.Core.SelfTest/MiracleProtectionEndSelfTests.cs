using SeitonSense.Core;

internal static class MiracleProtectionEndSelfTests
{
    internal static void HeldConsentRequiresOneExactUnconsumedGeneration()
    {
        var inherited = MiracleProtectionEndRules.ObserveHeldConsent(
            MiracleProtectionEndHeldConsentState.Initial,
            Observation(token: 0, physicallyDown: true));
        False(inherited.IsLatched, "raw physical level cannot invent consent");

        var acquired = MiracleProtectionEndRules.ObserveHeldConsent(
            inherited,
            Observation(token: 65, physicallyDown: false));
        True(acquired.IsLatched, "one shared unconsumed eligible token acquires consent");
        Equal(65, acquired.GameplayKeyToken, "the exact key token is frozen");

        var afterSharedConsumption = MiracleProtectionEndRules.ObserveHeldConsent(
            acquired,
            Observation(token: 0, physicallyDown: true));
        Equal(acquired, afterSharedConsumption, "shared consumption does not erase a proven hold");

        var released = MiracleProtectionEndRules.ObserveHeldConsent(
            afterSharedConsumption,
            Observation(token: 0, physicallyDown: false));
        False(released.IsLatched, "physical release clears exact consent");

        foreach (var clearingObservation in new[]
                 {
                     Observation(token: 0, physicallyDown: true) with { Enabled = false },
                     Observation(token: 0, physicallyDown: true) with { IsTextInputActive = true },
                     Observation(token: 0, physicallyDown: true) with { HardReset = true },
                 })
        {
            var cleared = MiracleProtectionEndRules.ObserveHeldConsent(
                acquired,
                clearingObservation);
            False(cleared.IsLatched, $"gate clears consent: {clearingObservation}");
        }

        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.MarksmanSpite),
            "startup reactive dispatch keeps the continuous hold available");
        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.Contradance),
            "all reactive families leave the same hold available for later actions");
        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.PostPurifyCrowdControl),
            "post-Purify dispatch retains consent for a later distinct episode");
        False(
            MiracleProtectionEndRules.DispatchConsumesHeldConsent(
                MiracleInterceptThreatKind.PostGuardCrowdControl),
            "post-Guard dispatch retains consent for a later distinct episode");
    }

    internal static void RankingUsesPositivePressureBonusThenHealthMpAndIdentity()
    {
        var unknownLowHp = Candidate(slot: 1, pressureKnown: false, hp: 1_000);
        var knownZero = Candidate(slot: 2, pressureKnown: true, pressure: 0, hp: 9_000);
        Less(unknownLowHp, knownZero, "known zero and unknown pressure are neutral, so HP wins");

        var neutralUnknown = Candidate(slot: 1, pressureKnown: false, hp: 5_000);
        var neutralKnownZero = Candidate(slot: 2, pressureKnown: true, pressure: 0, hp: 5_000);
        Less(neutralUnknown, neutralKnownZero, "zero cannot outrank unknown before stable identity");

        var higherPressure = Candidate(slot: 3, pressure: 3, hp: 9_000);
        var lowerPressure = Candidate(slot: 4, pressure: 2, hp: 1_000);
        Less(higherPressure, lowerPressure, "higher positive pressure ranks before HP");

        var positivePressure = Candidate(slot: 5, pressure: 1, hp: 9_000);
        Less(positivePressure, unknownLowHp, "any positive fresh pressure earns the optional bonus");

        var lowerHp = Candidate(slot: 3, pressure: 2, hp: 2_000);
        var higherHp = Candidate(slot: 4, pressure: 2, hp: 4_000);
        Less(lowerHp, higherHp, "lower exact HP ratio ranks next");

        var knownMp = Candidate(
            slot: 3,
            pressure: 2,
            hp: 4_000,
            mpKnown: true,
            mp: 8_000);
        var unknownMp = Candidate(slot: 4, pressure: 2, hp: 4_000);
        Less(knownMp, unknownMp, "trusted MP ranks ahead of unknown MP");

        var lowerMp = Candidate(
            slot: 3,
            pressure: 2,
            hp: 4_000,
            mpKnown: true,
            mp: 2_000);
        var higherMp = Candidate(
            slot: 4,
            pressure: 2,
            hp: 4_000,
            mpKnown: true,
            mp: 8_000);
        Less(lowerMp, higherMp, "lower trusted MP ratio ranks next");

        var slotOne = Candidate(slot: 1, pressure: 0, hp: 4_000);
        var slotTwo = Candidate(slot: 2, pressure: 0, hp: 4_000);
        Less(slotOne, slotTwo, "lower trusted S-slot closes equal telemetry");

        var invalidSentinelMp = Candidate(
            slot: 5,
            mpKnown: true,
            mp: 500,
            maxMp: 1_000);
        False(invalidSentinelMp.IsValid, "non-PvP maximum MP cannot become trusted rank data");

        var selected = MiracleProtectionEndRules.SelectBestIndex(
            [unknownLowHp, lowerPressure, higherPressure]);
        Equal(2, selected, "one deterministic winner is selected");
    }

    internal static void WhiteMageBardAndNinjaShareProtectionEndSemantics()
    {
        foreach (var actionId in new[]
                 {
                     MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
                     MiracleInterceptConfirmationRules.SilentNocturneActionId,
                     MiracleInterceptConfirmationRules.ForkedRaijuActionId,
                     MiracleInterceptConfirmationRules.FleetingRaijuActionId,
                     MiracleInterceptConfirmationRules.InterveneActionId,
                     MiracleInterceptConfirmationRules.MineuchiActionId,
                     MiracleInterceptConfirmationRules.ResolutionActionId,
                     MiracleInterceptConfirmationRules.ViceOfThornsActionId,
                     MiracleInterceptConfirmationRules.FrostStarActionId,
                 })
        {
            var purify = new MiracleInterceptPendingAttempt(
                LocalCasterEntityId: 100,
                ActionId: actionId,
                TargetGameObjectId: 0x200,
                TargetEntityId: 200,
                Threat: MiracleInterceptThreatKind.PostPurifyCrowdControl,
                UseActionAccepted: true,
                AttemptedAtMilliseconds: 1_000,
                ExpectedSourceSequence: 9)
            {
                RemovedStatusId = MiracleCleanseFollowupRules.StunStatusId,
            };
            var guard = purify with
            {
                Threat = MiracleInterceptThreatKind.PostGuardCrowdControl,
                RemovedStatusId = 0,
            };
            True(purify.IsValid, $"action {actionId} accepts exact post-Purify semantics");
            True(guard.IsValid, $"action {actionId} accepts exact post-Guard semantics");
        }

        Equal(
            (ushort)MiracleCleanseFollowupRules.MiracleOfNatureStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.MiracleOfNatureActionId),
            "WHM retains its exact landing status");
        Equal(
            (ushort)MiracleCleanseFollowupRules.SilenceStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.SilentNocturneActionId),
            "BRD retains its exact landing status");
        Equal(
            (ushort)MiracleCleanseFollowupRules.StunStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.ForkedRaijuActionId),
            "NIN Forked Raiju retains exact Stun landing status");
        Equal(
            (ushort)MiracleCleanseFollowupRules.StunStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.FleetingRaijuActionId),
            "NIN Fleeting Raiju retains exact Stun landing status");
        Equal(
            (ushort)MiracleCleanseFollowupRules.StunStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.InterveneActionId),
            "PLD Intervene retains exact Stun landing status");
        Equal(
            (ushort)MiracleCleanseFollowupRules.SilenceStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.ResolutionActionId),
            "RDM Resolution retains exact Silence landing status");
        Equal(
            (ushort)MiracleCleanseFollowupRules.StunStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.ViceOfThornsActionId),
            "RDM Vice of Thorns retains exact Stun landing status");
        Equal(
            MiracleInterceptConfirmationRules.DeepFreezeStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.FrostStarActionId),
            "BLM Frost Star retains exact Deep Freeze landing status");
        Equal(
            (ushort)MiracleCleanseFollowupRules.StunStatusId,
            MiracleInterceptConfirmationRules.ExpectedStatusForAction(
                MiracleInterceptConfirmationRules.MineuchiActionId),
            "SAM Mineuchi retains exact Stun landing status for staged confirmation");

        var intervene = ReactiveCounterCcProfileRules.Get(
            MiracleInterceptConfirmationRules.InterveneActionId);
        True(intervene is { IsValid: true }, "PLD Intervene is one explicit reviewed profile");
        Equal(
            ReactiveCounterCcExecutionShape.DirectTarget,
            intervene!.Value.ExecutionShape,
            "Intervene uses one exact actor target");
        True(intervene.Value.CannotExecuteWhileBound, "Intervene retains its Bound restriction");

        var resolution = ReactiveCounterCcProfileRules.Get(
            MiracleInterceptConfirmationRules.ResolutionActionId);
        True(resolution is { IsValid: true }, "RDM Resolution is one explicit reviewed profile");
        Equal(
            ReactiveCounterCcExecutionShape.LineAoeTargeted,
            resolution!.Value.ExecutionShape,
            "Resolution cannot be flattened into the direct single-target catalog");

        var vice = ReactiveCounterCcProfileRules.Get(
            MiracleInterceptConfirmationRules.ViceOfThornsActionId);
        True(vice is { IsValid: true }, "RDM Vice of Thorns is one explicit proc profile");
        Equal(
            ReactiveCounterCcExecutionShape.TargetCenteredAoe,
            vice!.Value.ExecutionShape,
            "Vice retains its target-centered AoE shape");
        Equal(
            ReactiveCounterCcProfileRules.ForteCarrierActionId,
            ReactiveCounterCcProfileRules.CarrierActionId(vice.Value.ActionId),
            "Vice readiness is proven only by the Forte carrier adjustment");

        var frost = ReactiveCounterCcProfileRules.Get(
            MiracleInterceptConfirmationRules.FrostStarActionId);
        True(frost is { IsValid: true }, "BLM Frost Star is one explicit proc profile");
        Equal(
            ReactiveCounterCcExecutionShape.TargetCenteredAoe,
            frost!.Value.ExecutionShape,
            "Frost Star retains its target-centered AoE shape");
        Equal(
            ReactiveCounterCcProfileRules.SoulResonanceCarrierActionId,
            ReactiveCounterCcProfileRules.CarrierActionId(frost.Value.ActionId),
            "Frost readiness is proven only by Soul Resonance adjustment");
        Equal(
            MiracleInterceptConfirmationRules.ViceOfThornsActionId,
            ReactiveCounterCcProfileRules.SelectRedMageCounterAction(
                viceEnabled: true,
                viceMetadataVerified: true,
                adjustedForteActionId:
                    MiracleInterceptConfirmationRules.ViceOfThornsActionId,
                resolutionEnabled: true,
                resolutionMetadataVerified: true),
            "an exposed Vice proc wins over Resolution");
        Equal(
            MiracleInterceptConfirmationRules.ResolutionActionId,
            ReactiveCounterCcProfileRules.SelectRedMageCounterAction(
                viceEnabled: true,
                viceMetadataVerified: true,
                adjustedForteActionId:
                    ReactiveCounterCcProfileRules.ForteCarrierActionId,
                resolutionEnabled: true,
                resolutionMetadataVerified: true),
            "Resolution remains the configured fallback before Vice is exposed");
        Equal(
            MiracleInterceptConfirmationRules.ViceOfThornsActionId,
            ReactiveCounterCcProfileRules.SelectRedMageCounterAction(
                viceEnabled: true,
                viceMetadataVerified: true,
                adjustedForteActionId:
                    ReactiveCounterCcProfileRules.ForteCarrierActionId,
                resolutionEnabled: false,
                resolutionMetadataVerified: false),
            "Vice-only keeps the capture lane alive while the proc is absent");
        Equal(
            MiracleInterceptConfirmationRules.FrostStarActionId,
            ReactiveCounterCcProfileRules.SelectBlackMageCounterAction(
                frostStarEnabled: true,
                frostStarMetadataVerified: true),
            "Frost Star keeps the capture lane alive before and after proc exposure");

        Equal(
            20f,
            ReactiveCounterCcProfileRules.NormalizeInterveneMaximumRangeYalms(float.NaN),
            "invalid PLD range fails closed to the verified native maximum");
        Equal(
            1f,
            ReactiveCounterCcProfileRules.NormalizeInterveneMaximumRangeYalms(0f),
            "PLD configured range has a positive lower bound");
        Equal(
            12f,
            ReactiveCounterCcProfileRules.NormalizeInterveneMaximumRangeYalms(12f),
            "PLD configured range remains exact inside its verified bounds");
        True(
            ReactiveCounterCcProfileRules.IsSupportedContext(
                isCrystallineConflict: false,
                isWolvesDenTesting: true),
            "Wolves' Den may reuse the protection-end coordinator only when explicitly enabled");
        True(
            ReactiveCounterCcProfileRules.IsExactWolvesDenCurrentTarget(
                observedActorEntityId: 0x200,
                expectedGameObjectId: 0x200,
                expectedEntityId: 0x200,
                expectedJobId: 30,
                currentHardTargetGameObjectId: 0x200,
                currentHardTargetEntityId: 0x200,
                currentHardTargetJobId: 30),
            "Wolves' Den requires the observed actor to remain the exact current hard target");
        False(
            ReactiveCounterCcProfileRules.IsExactWolvesDenCurrentTarget(
                observedActorEntityId: 0x200,
                expectedGameObjectId: 0x200,
                expectedEntityId: 0x200,
                expectedJobId: 30,
                currentHardTargetGameObjectId: 0x201,
                currentHardTargetEntityId: 0x201,
                currentHardTargetJobId: 30),
            "Wolves' Den never substitutes a different current target");

        foreach (var protectionEnd in new[]
                 {
                     MiracleInterceptThreatKind.PostPurifyCrowdControl,
                     MiracleInterceptThreatKind.PostGuardCrowdControl,
                 })
        {
            True(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.InterveneActionId,
                    protectionEnd),
                "PLD Intervene accepts only its reviewed protection-end trigger");
            True(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.ResolutionActionId,
                    protectionEnd),
                "RDM Resolution accepts only its reviewed protection-end trigger");
            True(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.ViceOfThornsActionId,
                    protectionEnd),
                "RDM Vice accepts only its reviewed protection-end trigger");
            True(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.FrostStarActionId,
                    protectionEnd),
                "BLM Frost Star accepts only its reviewed protection-end trigger");
        }

        foreach (var urgent in new[]
                 {
                     MiracleInterceptThreatKind.MarksmanSpite,
                     MiracleInterceptThreatKind.Zantetsuken,
                     MiracleInterceptThreatKind.FuriousBacklash,
                     MiracleInterceptThreatKind.Contradance,
                 })
        {
            False(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.InterveneActionId,
                    urgent),
                "PLD Intervene never inherits an urgent LB-start trigger");
            False(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.ResolutionActionId,
                    urgent),
                "RDM Resolution never inherits an urgent LB-start trigger");
            False(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.ViceOfThornsActionId,
                    urgent),
                "RDM Vice never inherits an urgent LB-start trigger");
            False(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.FrostStarActionId,
                    urgent),
                "BLM Frost Star never inherits an urgent LB-start trigger");
            True(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
                    urgent),
                "the existing WHM urgent trigger matrix stays unchanged");
            True(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.SilentNocturneActionId,
                    urgent),
                "the existing BRD urgent trigger matrix stays unchanged");
            True(
                ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.ForkedRaijuActionId,
                    urgent),
                "the existing NIN urgent trigger matrix stays unchanged");
        }

        var samTarget = new SamuraiReactiveCounterCcTarget(
            GameObjectId: 0x300,
            EntityId: 0x300,
            JobId: 23);
        var sam = SamuraiReactiveCounterCcRules.Arm(
            samTarget,
            gameplayKeyToken: 65,
            nowMilliseconds: 1_000);
        True(sam.IsActive, "SAM freezes one exact actor and held-key episode");

        var protectedInMelee = SamuraiReactiveCounterCcRules.Observe(
            sam,
            SamObservation(distance: 4f, protectionPresent: true));
        Equal(
            SamuraiReactiveCounterCcDecisionKind.Waiting,
            protectedInMelee.Kind,
            "SAM never fires Mineuchi through live protection");

        var directMineuchi = SamuraiReactiveCounterCcRules.Observe(
            sam,
            SamObservation(distance: 4f, protectionPresent: false));
        Equal(
            SamuraiReactiveCounterCcRules.MineuchiActionId,
            directMineuchi.ActionId,
            "SAM skips Soten when the exact actor is already in Mineuchi range");

        var approach = SamuraiReactiveCounterCcRules.Observe(
            sam,
            SamObservation(
                distance: 15f,
                protectionPresent: true,
                approachWindowOpen: true));
        Equal(
            SamuraiReactiveCounterCcRules.SotenActionId,
            approach.ActionId,
            "SAM requests one Soten only after the external measured approach window opens");
        sam = SamuraiReactiveCounterCcRules.CompleteAttempt(
            sam,
            approach.ActionId,
            ClientActionAttemptOutcome.ClientAccepted);
        Equal(
            SamuraiReactiveCounterCcPhase.ApproachAccepted,
            sam.Phase,
            "accepted Soten advances to the Mineuchi-only phase");

        var noSecondSoten = SamuraiReactiveCounterCcRules.Observe(
            sam,
            SamObservation(
                distance: 12f,
                protectionPresent: false,
                approachWindowOpen: true));
        Equal(
            SamuraiReactiveCounterCcDecisionKind.Waiting,
            noSecondSoten.Kind,
            "the same protection episode cannot request a second Soten");

        var timedMineuchi = SamuraiReactiveCounterCcRules.Observe(
            sam,
            SamObservation(distance: 4f, protectionPresent: false));
        Equal(
            SamuraiReactiveCounterCcRules.MineuchiActionId,
            timedMineuchi.ActionId,
            "Mineuchi becomes eligible only after arrival and authoritative protection absence");

        var drift = SamuraiReactiveCounterCcRules.Observe(
            sam,
            SamObservation(distance: 4f, protectionPresent: false) with
            {
                ExactTargetStillCurrent = false,
            });
        Equal(
            SamuraiReactiveCounterCcDecisionKind.Cancelled,
            drift.Kind,
            "SAM target drift cancels without a fallback actor");

        Equal(
            SamuraiReactiveProtectionKind.PurifyResilience,
            SamuraiReactiveRuntimeRules.ClassifyExactProtectionSignal(
                SamuraiReactiveRuntimeRules.PurifyActionId,
                casterEntityId: 0x400,
                targetEntityId: 0x400,
                targetCount: 1,
                globalSequence: 9,
                sourceSequence: 0),
            "SAM feed recognizes only an exact self-target Purify packet");
        True(
            new SamuraiReactiveProtectionSignal(
                SamuraiReactiveProtectionKind.PurifyResilience,
                ObservedAtMilliseconds: 1_000,
                CasterEntityId: 0x400,
                TargetEntityId: 0x400,
                ActionId: SamuraiReactiveRuntimeRules.PurifyActionId,
                TargetCount: 1,
                GlobalSequence: 9,
                SourceSequence: 0).IsValid,
            "the immutable SAM feed record retains every exact packet proof");
        Equal(
            SamuraiReactiveProtectionKind.Guard,
            SamuraiReactiveRuntimeRules.ClassifyExactProtectionSignal(
                SamuraiReactiveRuntimeRules.GuardActionId,
                casterEntityId: 0x400,
                targetEntityId: 0x400,
                targetCount: 1,
                globalSequence: 0,
                sourceSequence: 7),
            "SAM feed recognizes one exact self-target Guard packet");
        Equal(
            SamuraiReactiveProtectionKind.None,
            SamuraiReactiveRuntimeRules.ClassifyExactProtectionSignal(
                SamuraiReactiveRuntimeRules.PurifyActionId,
                casterEntityId: 0x400,
                targetEntityId: 0x401,
                targetCount: 1,
                globalSequence: 9,
                sourceSequence: 7),
            "SAM feed rejects a Purify packet whose actor and target differ");
        True(
            SamuraiReactiveRuntimeRules.IsExpectedProtectionStatus(
                SamuraiReactiveProtectionKind.Guard,
                SamuraiReactiveRuntimeRules.GuardAlternateStatusId),
            "SAM guard episodes accept the verified alternate Guard row");
        False(
            SamuraiReactiveRuntimeRules.IsInsideLease(
                observedAtMilliseconds: 1_000,
                nowMilliseconds: 2_001,
                SamuraiReactiveRuntimeRules.SignalStatusObservationLeaseMilliseconds),
            "a protection signal cannot gain status authority after its acquisition lease");

        var wolvesDummy = new SamuraiReactiveCounterCcTarget(
            GameObjectId: 0x500,
            EntityId: 0x500,
            JobId: 0);
        False(
            SamuraiReactiveCounterCcRules.Arm(
                wolvesDummy,
                gameplayKeyToken: 65,
                nowMilliseconds: 1_000).IsActive,
            "jobless actors remain invalid in normal CC");
        True(
            SamuraiReactiveCounterCcRules.Arm(
                wolvesDummy,
                gameplayKeyToken: 65,
                nowMilliseconds: 1_000,
                allowJoblessWolvesDenTarget: true).IsActive,
            "the explicit Wolves' Den dummy route can preserve its exact actor identity");

        var zantetsuken = SamuraiZantetsukenRules.Arm(
            samTarget,
            gameplayKeyToken: 65,
            nowMilliseconds: 2_000);
        var shielded = SamuraiZantetsukenRules.Observe(
            zantetsuken,
            ZanObservation(shieldPercentage: 1));
        Equal(
            SamuraiZantetsukenDecisionKind.Waiting,
            shielded.Kind,
            "Zantetsuken waits while even one authoritative shield percent remains");
        var unshielded = SamuraiZantetsukenRules.Observe(
            zantetsuken,
            ZanObservation(shieldPercentage: 0));
        Equal(
            SamuraiZantetsukenRules.ActionId,
            unshielded.ActionId,
            "exact own-source Kuzushi with zero shields admits one Zantetsuken boundary");
        var foreignOrMissingKuzushi = SamuraiZantetsukenRules.Observe(
            zantetsuken,
            ZanObservation(shieldPercentage: 0) with { OwnSourceKuzushiCount = 0 });
        Equal(
            SamuraiZantetsukenDecisionKind.Cancelled,
            foreignOrMissingKuzushi.Kind,
            "missing or foreign-source Kuzushi cancels without a fallback target");

        foreach (var active in new[]
                 {
                     MiracleInterceptThreatKind.PostPurifyCrowdControl,
                     MiracleInterceptThreatKind.PostGuardCrowdControl,
                 })
        {
            foreach (var incoming in new[]
                     {
                         MiracleInterceptThreatKind.MarksmanSpite,
                         MiracleInterceptThreatKind.Zantetsuken,
                         MiracleInterceptThreatKind.FuriousBacklash,
                         MiracleInterceptThreatKind.Contradance,
                     })
            {
                True(
                    MiracleProtectionEndRules.CanPreemptUnattemptedLowerPriorityThreat(
                        active,
                        HeldActionRetryState.Initial,
                        incoming),
                    $"exact higher-priority {incoming} preempts unattempted {active}");
            }
        }

        False(
            MiracleProtectionEndRules.CanPreemptUnattemptedLowerPriorityThreat(
                MiracleInterceptThreatKind.PostPurifyCrowdControl,
                new HeldActionRetryState(1, 1_050),
                MiracleInterceptThreatKind.MarksmanSpite),
            "a native attempt freezes the protection-end lease against preemption");
        True(
            MiracleProtectionEndRules.CanPreemptUnattemptedLowerPriorityThreat(
                MiracleInterceptThreatKind.Contradance,
                HeldActionRetryState.Initial,
                MiracleInterceptThreatKind.MarksmanSpite),
            "a strictly higher urgent startup preempts an unattempted lower urgent lease");
        False(
            MiracleProtectionEndRules.CanPreemptUnattemptedLowerPriorityThreat(
                MiracleInterceptThreatKind.MarksmanSpite,
                HeldActionRetryState.Initial,
                MiracleInterceptThreatKind.Zantetsuken),
            "equal-priority urgent startups preserve the first exact lease");
        False(
            MiracleProtectionEndRules.CanPreemptUnattemptedLowerPriorityThreat(
                MiracleInterceptThreatKind.PostGuardCrowdControl,
                HeldActionRetryState.Initial,
                MiracleInterceptThreatKind.PostPurifyCrowdControl),
            "equal/lower protection-end priority cannot preempt");
    }

    internal static void HeldLeaseSurvivesPriorityAndRetriesOnlyInsideItsBound()
    {
        const long observedAt = 1_000;
        Equal(3_000L, MiracleProtectionEndRules.HeldLeaseMilliseconds,
            "every protection-end counter keeps one ordinary GCD plus its release allowance");
        Equal(3_000L, MiracleProtectionEndRules.NinjaWeaponskillHeldLeaseMilliseconds,
            "NIN keeps one verified 2.5-second weaponskill recast plus the 500 ms release allowance");
        True(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 3_999,
                leaseMilliseconds: MiracleProtectionEndRules.NinjaWeaponskillHeldLeaseMilliseconds),
            "NIN remains eligible immediately before its exact 3000 ms lease ends");
        False(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 4_000,
                leaseMilliseconds: MiracleProtectionEndRules.NinjaWeaponskillHeldLeaseMilliseconds),
            "NIN lease has an exclusive 3000 ms boundary");
        True(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 3_999),
            "a cast or higher-priority accepted action may finish before exact counter-CC dispatch");
        False(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 4_000),
            "the shared 3000 ms deadline remains exclusive");
        True(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 1_249,
                leaseMilliseconds: MiracleInterceptRules.FuriousBacklashThreatLifetimeMilliseconds),
            "startup Viper retry remains inside its original short window");
        False(
            MiracleProtectionEndRules.CanAttempt(
                HeldActionRetryState.Initial,
                observedAt,
                nowMilliseconds: 1_250,
                leaseMilliseconds: MiracleInterceptRules.FuriousBacklashThreatLifetimeMilliseconds),
            "startup threat window is not extended");
        foreach (var kind in new[]
                 {
                     MiracleInterceptThreatKind.MarksmanSpite,
                     MiracleInterceptThreatKind.Zantetsuken,
                     MiracleInterceptThreatKind.FuriousBacklash,
                     MiracleInterceptThreatKind.Contradance,
                 })
        {
            var lifetime = MiracleInterceptRules.GetThreatLifetimeMilliseconds(kind);
            True(
                MiracleProtectionEndRules.CanAttempt(
                    HeldActionRetryState.Initial,
                    observedAt,
                    observedAt + lifetime - 1,
                    lifetime),
                $"{kind} remains eligible immediately before its original deadline");
            False(
                MiracleProtectionEndRules.CanAttempt(
                    HeldActionRetryState.Initial,
                    observedAt,
                    observedAt + lifetime,
                    lifetime),
                $"{kind} keeps its original exclusive deadline");
        }

        var state = HeldActionRetryState.Initial;
        for (var attempt = 1; attempt <= MiracleProtectionEndRules.MaximumNativeAttempts; attempt++)
        {
            var now = observedAt +
                      ((attempt - 1) * MiracleProtectionEndRules.NativeRetryThrottleMilliseconds);
            True(
                MiracleProtectionEndRules.CanAttempt(state, observedAt, now),
                $"attempt {attempt} is eligible at its throttle boundary");
            var rejected = MiracleProtectionEndRules.CompleteNativeAttempt(
                state,
                observedAt,
                now,
                ClientActionAttemptOutcome.ClientRejected);
            if (attempt < MiracleProtectionEndRules.MaximumNativeAttempts)
            {
                Equal(MiracleProtectionEndAttemptOutcome.RetryScheduled, rejected.Outcome, $"false {attempt} retries");
                False(
                    MiracleProtectionEndRules.CanAttempt(
                        rejected.NextState,
                        observedAt,
                        now + MiracleProtectionEndRules.NativeRetryThrottleMilliseconds - 1),
                    "retry cannot run before the shared throttle");
                state = rejected.NextState;
            }
            else
            {
                Equal(MiracleProtectionEndAttemptOutcome.RejectedTerminal, rejected.Outcome, "final retry-budget false is terminal");
            }
        }

        var accepted = MiracleProtectionEndRules.CompleteNativeAttempt(
            HeldActionRetryState.Initial,
            observedAt,
            nowMilliseconds: 1_600,
            ClientActionAttemptOutcome.ClientAccepted);
        Equal(MiracleProtectionEndAttemptOutcome.AcceptedTerminal, accepted.Outcome, "first true is terminal");

        var ambiguous = MiracleProtectionEndRules.CompleteNativeAttempt(
            HeldActionRetryState.Initial,
            observedAt,
            nowMilliseconds: 1_600,
            ClientActionAttemptOutcome.AcceptanceUnknown);
        Equal(MiracleProtectionEndAttemptOutcome.AmbiguousTerminal, ambiguous.Outcome, "native exception is terminal");

        var notInvoked = MiracleProtectionEndRules.CompleteNativeAttempt(
            HeldActionRetryState.Initial,
            observedAt,
            nowMilliseconds: 1_600,
            ClientActionAttemptOutcome.NotInvoked);
        Equal(MiracleProtectionEndAttemptOutcome.CancelledTerminal, notInvoked.Outcome, "pre-boundary cancellation cannot retry");
    }

    private static SamuraiReactiveCounterCcObservation SamObservation(
        float distance,
        bool protectionPresent,
        bool approachWindowOpen = false) =>
        new(
            Enabled: true,
            HardReset: false,
            ExactTargetStillCurrent: true,
            TargetAliveAndTargetable: true,
            ExactGameplayKeyStillDown: true,
            ProtectionPresent: protectionPresent,
            DistanceKnown: true,
            TargetEdgeDistanceYalms: distance,
            SotenReady: true,
            MineuchiReady: true,
            BoundPresent: false,
            SotenApproachWindowOpen: approachWindowOpen,
            ConfiguredSotenMaximumRangeYalms:
            SamuraiReactiveCounterCcRules.SotenMaximumRangeYalms);

    private static SamuraiZantetsukenObservation ZanObservation(
        byte shieldPercentage) => new(
            Enabled: true,
            HardReset: false,
            ExactTargetStillCurrent: true,
            TargetAliveAndTargetable: true,
            ExactGameplayKeyStillDown: true,
            OwnSourceKuzushiCount: 1,
            shieldPercentage,
            BoundPresent: false,
            ZantetsukenReady: true,
            HasNativeRangeAndLineOfSight: true);

    private static MiracleProtectionEndHeldConsentObservation Observation(
        int token,
        bool physicallyDown) =>
        new(
            Enabled: true,
            IsTextInputActive: false,
            UnconsumedEligibleGameplayKeyToken: token,
            LatchedKeyPhysicallyDown: physicallyDown);

    private static MiracleProtectionEndRankCandidate Candidate(
        int slot,
        bool pressureKnown = true,
        int pressure = 0,
        uint hp = 5_000,
        uint maxHp = 10_000,
        bool mpKnown = false,
        uint mp = 0,
        uint maxMp = CombatFrameRules.ExpectedMaximumMp) =>
        new(
            MiracleInterceptThreatKind.PostGuardCrowdControl,
            slot,
            0x1000UL + (uint)slot,
            100u + (uint)slot,
            30,
            pressureKnown,
            pressure,
            hp,
            maxHp,
            mpKnown,
            mp,
            maxMp);

    private static void Less(
        MiracleProtectionEndRankCandidate left,
        MiracleProtectionEndRankCandidate right,
        string message)
    {
        if (MiracleProtectionEndRules.Compare(left, right) >= 0)
            throw new InvalidOperationException(message);
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected}, got {actual}: {message}");
    }
}
