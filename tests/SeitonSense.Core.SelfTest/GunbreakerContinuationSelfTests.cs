using SeitonSense.Core;

internal static class GunbreakerContinuationSelfTests
{
    private const int HeldKey = 65;
    private static readonly TargetPressureActorIdentity Local = new(0x1001, 0x101);
    private static readonly TargetPressureActorIdentity Target = new(0x2001, 0x201);

    public static void ExactCatalogAndProcProofArePinned()
    {
        Equal(37u, GunbreakerContinuationRules.GunbreakerJobId, "GNB job");
        Equal(29_106u, GunbreakerContinuationRules.CarrierActionId, "Continuation carrier");
        var expected = new Dictionary<uint, (uint ProcRow, uint Status, int Range)>
        {
            [29_107] = (82, 3_041, 5),
            [29_108] = (42, 2_002, 5),
            [29_109] = (43, 2_003, 5),
            [29_110] = (44, 2_004, 5),
            [41_442] = (232, 4_293, 6),
        };
        foreach (var pair in expected)
        {
            True(GunbreakerContinuationRules.IsExactFollowUpAction(pair.Key),
                $"exact follow-up {pair.Key}");
            Equal(pair.Value.Status,
                GunbreakerContinuationRules.GetExpectedProcStatusId(pair.Key),
                $"proc status {pair.Key}");
            Equal(pair.Value.ProcRow,
                GunbreakerContinuationRules.GetExpectedProcRowId(pair.Key),
                $"proc row {pair.Key}");
            Equal(pair.Value.Range,
                GunbreakerContinuationRules.GetMaximumRangeYalms(pair.Key),
                $"range {pair.Key}");
        }

        False(GunbreakerContinuationRules.IsExactFollowUpAction(
            GunbreakerContinuationRules.CarrierActionId), "carrier is not dispatched");
        True(GunbreakerContinuationRules.IsSelfCenteredAction(
            GunbreakerContinuationRules.FatedBrandActionId), "Fated Brand is self AoE");

        var missingStatus = GunbreakerContinuationRules.ObserveCarrierExposure(
            GunbreakerContinuationExposureState.Initial,
            GunbreakerContinuationRules.HypervelocityActionId,
            observedExactProcStatusId: 0);
        False(missingStatus.HasTrackedEpisode, "carrier alone is insufficient");
        var wrongStatus = GunbreakerContinuationRules.ObserveCarrierExposure(
            GunbreakerContinuationExposureState.Initial,
            GunbreakerContinuationRules.HypervelocityActionId,
            GunbreakerContinuationRules.ReadyToRipStatusId);
        False(wrongStatus.HasTrackedEpisode, "wrong own status is insufficient");

        var exact = Exposure();
        True(exact.HasCurrentFollowUp, "carrier plus exact own status exposes");
        Equal(1L, exact.Generation, "first generation");
        Equal(GunbreakerContinuationRules.ReadyToBlastStatusId,
            exact.CurrentProcStatusId, "proc proof is retained");
    }

    public static void ExposureIsOneActionAndDebouncesCarrierFlicker()
    {
        var exposed = Exposure();
        var wrongStatusSpend = GunbreakerContinuationRules.MarkCarrierExposureSpent(
            exposed,
            exposed.Generation,
            exposed.EpisodeActionId,
            GunbreakerContinuationRules.ReadyToRipStatusId);
        Equal(exposed, wrongStatusSpend, "wrong proc cannot spend");

        var spent = GunbreakerContinuationRules.MarkCarrierExposureSpent(
            exposed,
            exposed.Generation,
            exposed.EpisodeActionId,
            exposed.EpisodeProcStatusId);
        True(spent.IsSpent, "exact exposure spent");

        var same = GunbreakerContinuationRules.ObserveCarrierExposure(
            spent,
            spent.EpisodeActionId,
            spent.EpisodeProcStatusId);
        True(same.IsSpent, "same exposure cannot duplicate");
        Equal(spent.Generation, same.Generation, "same generation retained");

        var flicker = GunbreakerContinuationRules.ObserveCarrierExposure(
            same,
            GunbreakerContinuationRules.CarrierActionId,
            0);
        Equal(1, flicker.ConsecutiveNonFollowUpObservations, "first miss is flicker");
        var returned = GunbreakerContinuationRules.ObserveCarrierExposure(
            flicker,
            spent.EpisodeActionId,
            spent.EpisodeProcStatusId);
        Equal(spent.Generation, returned.Generation, "one miss cannot rearm");
        True(returned.IsSpent, "spent latch survives flicker");

        var absentOne = GunbreakerContinuationRules.ObserveCarrierExposure(
            returned,
            GunbreakerContinuationRules.CarrierActionId,
            0);
        var absentTwo = GunbreakerContinuationRules.ObserveCarrierExposure(
            absentOne,
            GunbreakerContinuationRules.CarrierActionId,
            0);
        var rearmed = GunbreakerContinuationRules.ObserveCarrierExposure(
            absentTwo,
            spent.EpisodeActionId,
            spent.EpisodeProcStatusId);
        Equal(spent.Generation + 1, rearmed.Generation,
            "stable absence rearms same later proc");
        False(rearmed.IsSpent, "new exposure is unspent");
    }

    public static void CcSelectionIsLowestHpReachableAndAmbiguityFailsClosed()
    {
        var high = Candidate(1, new(0x2001, 0x201), 80, 100);
        var low = Candidate(4, new(0x2004, 0x204), 15, 100);
        var lowerRatio = Candidate(3, new(0x2003, 0x203), 20, 200);
        var outOfRange = Candidate(2, new(0x2002, 0x202), 1, 100) with
        {
            HasNativeRangeAndLineOfSight = false,
        };
        var selected = GunbreakerContinuationRules.SelectBestCandidate(
            SupportedPvPContext.CrystallineConflict,
            [high, low, lowerRatio, outOfRange]);
        True(selected.HasValue, "CC candidate selected");
        Equal(lowerRatio.Actor, selected!.Value.Actor, "lowest HP ratio wins");

        var tieEarlierSlot = Candidate(2, new(0x2012, 0x212), 10, 100);
        var tieLaterSlot = Candidate(5, new(0x2015, 0x215), 20, 200);
        selected = GunbreakerContinuationRules.SelectBestCandidate(
            SupportedPvPContext.CrystallineConflict,
            [tieLaterSlot, tieEarlierSlot]);
        Equal(2, selected!.Value.EnemySlot, "slot is stable tie break");

        var duplicate = high with { EnemySlot = 5 };
        False(GunbreakerContinuationRules.SelectBestCandidate(
                SupportedPvPContext.CrystallineConflict,
                [high, duplicate]).HasValue,
            "duplicate canonical identity is ambiguous");
    }

    public static void WolvesDenRequiresOneCurrentTargetAndFatedBrandAnchor()
    {
        var wolves = Candidate(
            0,
            Target,
            50,
            100,
            SupportedPvPContext.WolvesDen);
        var selected = GunbreakerContinuationRules.SelectBestCandidate(
            SupportedPvPContext.WolvesDen,
            [wolves]);
        Equal(Target, selected!.Value.Actor, "single current <t> survives");
        False(GunbreakerContinuationRules.SelectBestCandidate(
                SupportedPvPContext.WolvesDen,
                [wolves, wolves with { Actor = new(0x2002, 0x202) }]).HasValue,
            "Wolves never chooses among multiple targets");

        var fatedAnchorUnavailable = wolves with
        {
            HasNativeRangeAndLineOfSight = false,
        };
        False(GunbreakerContinuationRules.SelectBestCandidate(
                SupportedPvPContext.WolvesDen,
                [fatedAnchorUnavailable]).HasValue,
            "Fated Brand needs an exact enemy anchor in six yalms");
    }

    public static void FrozenIntentRetryAndAmbiguousBoundaryAreFailClosed()
    {
        var exposure = Exposure();
        var dispatch = GunbreakerContinuationRules.Observe(
            GunbreakerContinuationState.Initial,
            Observation(exposure));
        True(dispatch.ShouldDispatch, "exact hold dispatches");
        var intent = dispatch.Intent!.Value;
        Equal(exposure.EpisodeProcStatusId, intent.ProcStatusId, "proc status frozen");
        True(CanUse(intent, exposure, Candidate()), "exact frozen intent");
        False(CanUse(intent, exposure, Candidate() with
        {
            Actor = new(0x2099, 0x299),
        }), "target drift rejected");

        var stable = Fingerprint(sequence: 7);
        Equal(ClientActionAttemptOutcome.ClientRejected,
            GunbreakerContinuationRules.ClassifyFollowUpBoundary(
                false,
                intent.ActionId,
                intent.ProcStatusId,
                intent.ProcStatusId,
                intent.ProcStatusId,
                0,
                0,
                intent.ActionId,
                intent.ActionId,
                stable,
                stable),
            "only explicit stable false is retryable");
        Equal(ClientActionAttemptOutcome.AcceptanceUnknown,
            GunbreakerContinuationRules.ClassifyFollowUpBoundary(
                false,
                intent.ActionId,
                intent.ProcStatusId,
                intent.ProcStatusId,
                0,
                0,
                0,
                intent.ActionId,
                intent.ActionId,
                stable,
                stable),
            "lost proc proof is terminal ambiguous");

        var rejected = GunbreakerContinuationRules.ApplyNativeAttemptOutcome(
            dispatch.NextState,
            ClientActionAttemptOutcome.ClientRejected,
            1_000);
        True(rejected.RetryScheduled, "clean false schedules exact retry");
        False(rejected.SpendExposure, "scheduled retry keeps exposure");
        var ambiguous = GunbreakerContinuationRules.ApplyNativeAttemptOutcome(
            dispatch.NextState,
            ClientActionAttemptOutcome.AcceptanceUnknown,
            1_000);
        Equal(HeldActionRetryDisposition.AmbiguousTerminal,
            ambiguous.Disposition, "ambiguous result terminal");
        True(ambiguous.SpendExposure, "ambiguous result spends exposure");
    }

    public static void DistinctContinuationProcsWorkUnderOneContinuousHold()
    {
        var firstExposure = Exposure();
        var first = GunbreakerContinuationRules.Observe(
            GunbreakerContinuationState.Initial,
            Observation(firstExposure));
        var firstIntent = first.Intent!.Value;
        var spent = GunbreakerContinuationRules.MarkCarrierExposureSpent(
            firstExposure,
            firstIntent.ExposureGeneration,
            firstIntent.ActionId,
            firstIntent.ProcStatusId);
        var nextExposure = GunbreakerContinuationRules.ObserveCarrierExposure(
            spent,
            GunbreakerContinuationRules.JugularRipActionId,
            GunbreakerContinuationRules.ReadyToRipStatusId);
        Equal(firstExposure.Generation + 1, nextExposure.Generation,
            "different exact follow-up advances immediately");
        var second = GunbreakerContinuationRules.Observe(
            GunbreakerContinuationState.Initial,
            Observation(nextExposure) with { NowMilliseconds = 1_001 });
        True(second.ShouldDispatch, "same held key handles distinct proc");
        Equal(GunbreakerContinuationRules.JugularRipActionId,
            second.Intent!.Value.ActionId, "new exact action frozen");
        Equal(GunbreakerContinuationRules.ReadyToRipStatusId,
            second.Intent.Value.ProcStatusId, "new exact status frozen");
    }

    private static GunbreakerContinuationExposureState Exposure(
        uint actionId = GunbreakerContinuationRules.HypervelocityActionId) =>
        GunbreakerContinuationRules.ObserveCarrierExposure(
            GunbreakerContinuationExposureState.Initial,
            actionId,
            GunbreakerContinuationRules.GetExpectedProcStatusId(actionId));

    private static GunbreakerContinuationCandidate Candidate(
        int slot = 3,
        TargetPressureActorIdentity? actor = null,
        uint currentHp = 50,
        uint maxHp = 100,
        SupportedPvPContext context = SupportedPvPContext.CrystallineConflict) => new(
        context,
        slot,
        actor ?? Target,
        ExactCanonicalIdentity: true,
        Alive: true,
        Targetable: true,
        currentHp,
        maxHp,
        HasValidActionTarget: true,
        HasNativeRangeAndLineOfSight: true);

    private static GunbreakerContinuationObservation Observation(
        GunbreakerContinuationExposureState exposure) => new(
        ConfigurationEnabled: true,
        Context: SupportedPvPContext.CrystallineConflict,
        LocalPlayer: Local,
        IsLocalPlayerAlive: true,
        LocalJobId: GunbreakerContinuationRules.GunbreakerJobId,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        HeldGameplayKeyCode: HeldKey,
        FrozenKeyStillDown: true,
        Exposure: exposure,
        ActionLocallyReady: true,
        NativeBoundaryReady: true,
        Candidate: Candidate(),
        HardReset: false,
        NowMilliseconds: 1_000);

    private static bool CanUse(
        GunbreakerContinuationIntent intent,
        GunbreakerContinuationExposureState exposure,
        GunbreakerContinuationCandidate candidate) =>
        GunbreakerContinuationRules.CanUseFrozenIntent(
            intent,
            configurationEnabled: true,
            SupportedPvPContext.CrystallineConflict,
            Local,
            localAlive: true,
            GunbreakerContinuationRules.GunbreakerJobId,
            metadataVerified: true,
            guardSuppressed: false,
            higherPriorityClaimed: false,
            exposure,
            actionLocallyReady: true,
            currentHeldKeyCode: HeldKey,
            frozenKeyStillDown: true,
            candidate);

    private static ClientActionAttemptFingerprint Fingerprint(ushort sequence) => new(
        Captured: true,
        ActionQueued: false,
        QueuedActionType: 0,
        QueuedActionId: 0,
        QueuedTargetId: 0,
        QueuedExtraParam: 0,
        QueueMode: 0,
        QueuedComboRouteId: 0,
        LastUsedActionSequence: sequence,
        AnimationLockSeconds: 0,
        CastActionId: 0,
        AdjustedActionId: GunbreakerContinuationRules.HypervelocityActionId,
        IsActionOffCooldown: true,
        ResourceStatus: 0);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(
                $"{label}: expected {expected}, got {actual}");
    }
}
