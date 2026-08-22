using SeitonSense.Core;

internal static class NinjaSeitonDispatchSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer = new(10_000, 1_000);

    public static void ExecuteBlockingProtectionStatusSetIsExact()
    {
        var blocked = new uint[]
        {
            NinjaSeitonProtectionStatusCatalog.CoveredLegacyStatusId,
            NinjaSeitonProtectionStatusCatalog.CoveredStatusId,
            NinjaSeitonProtectionStatusCatalog.CoveredPvpStatusId,
            NinjaSeitonProtectionStatusCatalog.CoveredPvpAlternateStatusId,
            NinjaSeitonProtectionStatusCatalog.HallowedGroundStatusId,
            NinjaSeitonProtectionStatusCatalog.UndeadRedemptionStatusId,
        };
        foreach (var statusId in blocked)
        {
            True(
                NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(statusId),
                $"status {statusId} blocks Seiton");
        }

        foreach (var statusId in new uint[]
                 {
                     80,    // Cover: covering Paladin, not the protected target.
                     1_300, // Cover duplicate.
                     2_412, // Current Cover row.
                     3_210, // Phalanx: 33% mitigation, not invulnerability.
                     3_250, // Blade of Faith Ready.
                     3_033, // Blackblood.
                     3_837, // Scorn legacy row.
                     4_290, // Scorn current row.
                     3_054, // Guard, handled independently by the action itself.
                 })
        {
            False(
                NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(statusId),
                $"status {statusId} does not block Seiton");
        }
    }

    public static void CandidateEligibilityIsExactAndStrict()
    {
        var valid = Candidate(slot: 1, gameObjectId: 20_001, entityId: 2_001, hp: 49, maxHp: 100);

        True(NinjaSeitonDispatchRules.IsEligibleCandidate(valid, LocalPlayer), "49 percent exact enemy");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { CurrentHp = 50 }, LocalPlayer), "exactly half");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { CurrentHp = 0 }, LocalPlayer), "dead health sample");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { MaximumHp = 0 }, LocalPlayer), "invalid maximum");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { ExactCanonicalIdentity = false }, LocalPlayer), "noncanonical actor");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { Alive = false }, LocalPlayer), "dead actor flag");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { Targetable = false }, LocalPlayer), "untargetable actor");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with
        {
            ExecuteBlockingStatusId = NinjaSeitonProtectionStatusCatalog.CoveredPvpAlternateStatusId,
        }, LocalPlayer), "Covered or invulnerable actor");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { HasValidActionTarget = false }, LocalPlayer), "native target rejection");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { HasNativeRangeAndLineOfSight = false }, LocalPlayer), "native range or line of sight rejection");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { EnemySlot = 0 }, LocalPlayer), "invalid S-slot");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { Actor = default }, LocalPlayer), "invalid actor identity");
        False(NinjaSeitonDispatchRules.IsEligibleCandidate(valid with { Actor = LocalPlayer }, LocalPlayer), "self cannot be an enemy candidate");
    }

    public static void LowestExactHealthWinsThenStableSlot()
    {
        var candidates = new[]
        {
            Candidate(4, 20_004, 2_004, uint.MaxValue / 3, uint.MaxValue),
            Candidate(3, 20_003, 2_003, 33, 100),
            Candidate(2, 20_002, 2_002, 33, 100),
        };

        // 33/100 is below floor(MaxValue/3)/MaxValue, and the exact tie uses S2.
        Equal(
            2,
            NinjaSeitonDispatchRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "exact ratio then slot");

        var equalRatio = new[]
        {
            Candidate(5, 20_005, 2_005, 1, 4),
            Candidate(1, 20_001, 2_001, 25, 100),
        };
        Equal(
            1,
            NinjaSeitonDispatchRules.SelectBestCandidateIndex(equalRatio, LocalPlayer),
            "equivalent fractions use stable S-slot");
    }

    public static void ProtectedTargetsAreSkippedAndFrozenProtectionDriftCancels()
    {
        var protectedLowest = Candidate(
            1,
            20_001,
            2_001,
            5,
            100,
            executeBlockingStatusId: NinjaSeitonProtectionStatusCatalog.CoveredPvpStatusId);
        var eligible = Candidate(2, 20_002, 2_002, 20, 100);
        var candidates = new[] { protectedLowest, eligible };

        Equal(
            1,
            NinjaSeitonDispatchRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "protected lowest target is skipped without invalidating the canonical set");

        foreach (var actionId in new[]
                 {
                     NinjaSeitonDispatchRules.BaseActionId,
                     NinjaSeitonDispatchRules.FollowUpActionId,
                 })
        {
            var intent = new NinjaSeitonDispatchIntent(actionId, eligible.EnemySlot, eligible.Actor);
            True(
                NinjaSeitonDispatchRules.CanUseExactIntent(
                    intent,
                    eligible,
                    LocalPlayer,
                    actionId,
                    actionLocallyReady: true),
                $"action {actionId} accepts unchanged unprotected frozen target");
            False(
                NinjaSeitonDispatchRules.CanUseExactIntent(
                    intent,
                    eligible with
                    {
                        ExecuteBlockingStatusId =
                            NinjaSeitonProtectionStatusCatalog.UndeadRedemptionStatusId,
                    },
                    LocalPlayer,
                    actionId,
                    actionLocallyReady: true),
                $"action {actionId} cancels when frozen target gains protection");
        }
    }

    public static void AmbiguousCanonicalCandidatesFailClosed()
    {
        var duplicateSlot = new[]
        {
            Candidate(1, 20_001, 2_001, 20, 100),
            Candidate(1, 20_002, 2_002, 10, 100),
        };
        Equal(-1, NinjaSeitonDispatchRules.SelectBestCandidateIndex(duplicateSlot, LocalPlayer), "duplicate slot");

        var actor = new TargetPressureActorIdentity(20_001, 2_001);
        var duplicateActor = new[]
        {
            Candidate(1, actor.GameObjectId, actor.EntityId, 20, 100),
            Candidate(2, actor.GameObjectId, actor.EntityId, 10, 100),
        };
        Equal(-1, NinjaSeitonDispatchRules.SelectBestCandidateIndex(duplicateActor, LocalPlayer), "duplicate actor");

        Equal(-1, NinjaSeitonDispatchRules.SelectBestCandidateIndex(null, LocalPlayer), "null candidates");
        Equal(-1, NinjaSeitonDispatchRules.SelectBestCandidateIndex([], LocalPlayer), "empty candidates");
    }

    public static void DispatchRequiresEveryGateAndHeldInput()
    {
        var valid = Observation();
        Dispatch(valid, NinjaSeitonDispatchRules.BaseActionId, "base Seiton");
        Dispatch(valid with { ResolvedActionId = NinjaSeitonDispatchRules.FollowUpActionId }, NinjaSeitonDispatchRules.FollowUpActionId, "Unsealed Seiton");

        Cancel(valid with { ConfigurationEnabled = false }, NinjaSeitonDispatchDecisionReason.ConfigurationDisabled);
        Cancel(valid with { IsCrystallineConflict = false }, NinjaSeitonDispatchDecisionReason.OutsideCrystallineConflict);
        Cancel(valid with { LocalPlayer = default }, NinjaSeitonDispatchDecisionReason.LocalPlayerIdentityInvalid);
        Cancel(valid with { IsLocalPlayerAlive = false }, NinjaSeitonDispatchDecisionReason.LocalPlayerDead);
        Cancel(valid with { LocalJobId = 29 }, NinjaSeitonDispatchDecisionReason.LocalJobInvalid);
        Cancel(valid with { MetadataVerified = false }, NinjaSeitonDispatchDecisionReason.MetadataUnverified);
        Cancel(valid with { ActionHelpersSuppressedByGuard = true }, NinjaSeitonDispatchDecisionReason.GuardSuppressed);
        Cancel(valid with { HigherPriorityClaimed = true }, NinjaSeitonDispatchDecisionReason.HigherPriorityClaimed);
        Cancel(valid with { InputProbeSucceeded = false }, NinjaSeitonDispatchDecisionReason.InputProbeUnavailable);
        Cancel(valid with { IsTextInputActive = true }, NinjaSeitonDispatchDecisionReason.TextInputActive);
        Cancel(valid with { HeldGameplayKeyEligible = false }, NinjaSeitonDispatchDecisionReason.NoHeldGameplayKey);
        Cancel(valid with { ResolvedActionId = 0 }, NinjaSeitonDispatchDecisionReason.ResolvedActionInvalid);
        Cancel(valid with { ActionLocallyReady = false }, NinjaSeitonDispatchDecisionReason.ActionNotReady);

        var noCandidate = NinjaSeitonDispatchRules.Observe(valid with
        {
            Candidates = [Candidate(1, 20_001, 2_001, 50, 100)],
        });
        False(noCandidate.ShouldDispatch, "no execute target");
        False(noCandidate.ShouldConsumeInputGeneration, "no target does not claim input");
        Equal(NinjaSeitonDispatchDecisionReason.NoExactEligibleTarget, noCandidate.Reason, "no target reason");

        Cancel(valid with { HardReset = true }, NinjaSeitonDispatchDecisionReason.HardReset);
    }

    public static void DispatchFreezesOneExactIntent()
    {
        var selected = Candidate(2, 20_002, 2_002, 20, 100);
        var alternate = Candidate(3, 20_003, 2_003, 10, 100);
        var decision = NinjaSeitonDispatchRules.Observe(Observation() with
        {
            Candidates = [selected, alternate],
        });

        True(decision.ShouldDispatch, "dispatch decision");
        True(decision.ShouldConsumeInputGeneration, "consume before any native work");
        Equal(1, decision.SelectedCandidateIndex, "lowest HP candidate index");
        var intent = decision.Intent ?? throw new InvalidOperationException("missing exact intent");
        Equal(NinjaSeitonDispatchRules.BaseActionId, intent.ActionId, "frozen action");
        Equal(3, intent.EnemySlot, "frozen slot");
        Equal(alternate.Actor, intent.Target, "frozen actor");

        var castWaitRequest = new HeldCastCancellationRequest(
            HeldCastCancellationHelperKind.NinjaSeiton,
            intent.ActionId,
            LocalPlayer,
            intent.Target,
            FrozenKeyCode: 0x57,
            IntentEpochToken: 1);
        True(castWaitRequest.IsValid, "cast wait retains the exact frozen Seiton intent");
        True(
            HeldActionRetryRules.RetainsSchedulerFrame(
                HeldActionRetryState.Initial,
                nowMilliseconds: 0,
                exactIntentValid: true,
                actionSpecificReady: true,
                targetSpecificReady: true),
            "active cast soft-wait keeps initial Seiton priority without an attempt");
        Equal(
            HeldActionRetryState.Initial,
            HeldActionRetryRules.Complete(
                HeldActionRetryState.Initial,
                nowMilliseconds: 0,
                ClientActionAttemptOutcome.SoftUnavailable).NextState,
            "active cast soft-wait spends no Seiton attempt budget");

        True(
            NinjaSeitonDispatchRules.CanUseExactIntent(
                intent,
                alternate,
                LocalPlayer,
                NinjaSeitonDispatchRules.BaseActionId,
                actionLocallyReady: true),
            "unchanged intent");
        False(
            NinjaSeitonDispatchRules.CanUseExactIntent(
                intent,
                alternate with { CurrentHp = 50, MaximumHp = 100 },
                LocalPlayer,
                NinjaSeitonDispatchRules.BaseActionId,
                actionLocallyReady: true),
            "healing to exactly half cancels the frozen intent");
        False(
            NinjaSeitonDispatchRules.CanUseExactIntent(
                intent,
                alternate with { CurrentHp = 51, MaximumHp = 100 },
                LocalPlayer,
                NinjaSeitonDispatchRules.BaseActionId,
                actionLocallyReady: true),
            "healing above half cancels the frozen intent");
        False(
            NinjaSeitonDispatchRules.CanUseExactIntent(
                intent,
                selected,
                LocalPlayer,
                NinjaSeitonDispatchRules.BaseActionId,
                actionLocallyReady: true),
            "an alternate candidate cannot replace the frozen actor");
        False(
            NinjaSeitonDispatchRules.CanUseExactIntent(
                intent,
                alternate with { Actor = new TargetPressureActorIdentity(20_004, 2_004) },
                LocalPlayer,
                NinjaSeitonDispatchRules.BaseActionId,
                actionLocallyReady: true),
            "actor drift");
        False(
            NinjaSeitonDispatchRules.CanUseExactIntent(
                intent,
                alternate,
                LocalPlayer,
                NinjaSeitonDispatchRules.FollowUpActionId,
                actionLocallyReady: true),
            "action drift");
        False(
            NinjaSeitonDispatchRules.CanUseExactIntent(
                intent,
                alternate with { HasNativeRangeAndLineOfSight = false },
                LocalPlayer,
                NinjaSeitonDispatchRules.BaseActionId,
                actionLocallyReady: true),
            "range drift");
        False(
            NinjaSeitonDispatchRules.CanUseExactIntent(
                intent,
                alternate,
                LocalPlayer,
                NinjaSeitonDispatchRules.BaseActionId,
                actionLocallyReady: false),
            "readiness drift");
    }

    public static void HeldLevelUsesOneAcceptedAdjustedActionEpochAtATime()
    {
        True(NinjaSeitonDispatchRules.Observe(Observation()).ShouldDispatch, "held level dispatches");

        var acceptedBase = NinjaSeitonDispatchRules.BeginAcceptedHold(
            0x57,
            NinjaSeitonDispatchRules.BaseActionId);
        True(acceptedBase.OwnsHold, "accepted base owns exact key");
        False(
            NinjaSeitonDispatchRules.CanOpenAdjustedActionEpoch(
                acceptedBase,
                NinjaSeitonDispatchRules.BaseActionId),
            "same accepted base epoch cannot repeat");
        True(
            NinjaSeitonDispatchRules.CanOpenAdjustedActionEpoch(
                acceptedBase,
                NinjaSeitonDispatchRules.FollowUpActionId),
            "adjusted follow-up is one distinct epoch");

        var retiredFollowUp = NinjaSeitonDispatchRules.RetireAdjustedActionEpoch(
            acceptedBase,
            NinjaSeitonDispatchRules.FollowUpActionId);
        False(
            NinjaSeitonDispatchRules.CanOpenAdjustedActionEpoch(
                retiredFollowUp,
                NinjaSeitonDispatchRules.FollowUpActionId),
            "spent follow-up epoch cannot reopen after terminal drift");

        var acceptedFollowUp = NinjaSeitonDispatchRules.BeginAcceptedHold(
            0x57,
            NinjaSeitonDispatchRules.FollowUpActionId);
        False(
            NinjaSeitonDispatchRules.CanOpenAdjustedActionEpoch(
                acceptedFollowUp,
                NinjaSeitonDispatchRules.FollowUpActionId),
            "accepted follow-up cannot repeat");
        Equal(
            NinjaSeitonAcceptedHoldState.Initial,
            NinjaSeitonDispatchRules.ObserveAcceptedHold(
                acceptedBase,
                hardReset: false,
                ownershipContextValid: true,
                exactHeldKeyStillDown: false),
            "key release ends accepted ownership");
    }

    private static NinjaSeitonDispatchObservation Observation() => new(
        ConfigurationEnabled: true,
        IsCrystallineConflict: true,
        LocalJobId: ExecuteThreshold.NinjaJobId,
        LocalPlayer,
        IsLocalPlayerAlive: true,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        ResolvedActionId: NinjaSeitonDispatchRules.BaseActionId,
        ActionLocallyReady: true,
        Candidates: [Candidate(1, 20_001, 2_001, 49, 100)]);

    private static NinjaSeitonDispatchCandidate Candidate(
        int slot,
        ulong gameObjectId,
        uint entityId,
        uint hp,
        uint maxHp,
        uint executeBlockingStatusId = 0) => new(
        slot,
        new TargetPressureActorIdentity(gameObjectId, entityId),
        ExactCanonicalIdentity: true,
        Alive: true,
        Targetable: true,
        hp,
        maxHp,
        ExecuteBlockingStatusId: executeBlockingStatusId,
        HasValidActionTarget: true,
        HasNativeRangeAndLineOfSight: true);

    private static void Dispatch(
        NinjaSeitonDispatchObservation observation,
        uint expectedActionId,
        string label)
    {
        var decision = NinjaSeitonDispatchRules.Observe(observation);
        True(decision.ShouldDispatch, label);
        True(decision.ShouldConsumeInputGeneration, $"{label} consumes input");
        Equal(expectedActionId, decision.Intent!.Value.ActionId, $"{label} action");
    }

    private static void Cancel(
        NinjaSeitonDispatchObservation observation,
        NinjaSeitonDispatchDecisionReason expectedReason)
    {
        var decision = NinjaSeitonDispatchRules.Observe(observation);
        False(decision.ShouldDispatch, expectedReason.ToString());
        False(decision.ShouldConsumeInputGeneration, $"{expectedReason} input");
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
