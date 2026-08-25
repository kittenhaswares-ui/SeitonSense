using SeitonSense.Core;

internal static class MonkHeldComboSelfTests
{
    private const int HeldKey = 65;
    private static readonly TargetPressureActorIdentity Local =
        new(0x1001, 0x101);
    private static readonly TargetPressureActorIdentity Target =
        new(0x2001, 0x201);

    public static void ExactCatalogAndRouteArePinned()
    {
        Equal(20u, MonkHeldComboRules.MonkJobId, "Monk job");
        Equal(55u, MonkHeldComboRules.PhantomRushComboRouteId, "combo route");
        var route = new uint[]
        {
            29_475, 29_476, 29_477, 41_444, 41_445, 41_446, 29_478,
        };
        for (var index = 0; index < route.Length; index++)
        {
            True(MonkHeldComboRules.IsExactComboAction(route[index]),
                $"exact route action {route[index]}");
            Equal(index == 0 ? 0u : route[index - 1],
                MonkHeldComboRules.GetExpectedPreviousComboAction(route[index]),
                $"previous action {route[index]}");
            Equal(index == route.Length - 1 ? 0u : route[index + 1],
                MonkHeldComboRules.GetExpectedNextComboAction(route[index]),
                $"next action {route[index]}");
        }

        True(MonkHeldComboRules.IsDispatchableAction(
            MonkHeldComboRules.FireReplyActionId), "Fire's Reply");
        True(MonkHeldComboRules.IsDispatchableAction(
            MonkHeldComboRules.WindReplyActionId), "Wind's Reply");
        True(MonkHeldComboRules.IsDispatchableAction(
            MonkHeldComboRules.RisingPhoenixActionId), "Rising Phoenix");
        True(MonkHeldComboRules.IsDispatchableAction(
            MonkHeldComboRules.ThunderclapActionId), "Thunderclap");
        False(MonkHeldComboRules.IsDispatchableAction(1), "unknown action");
    }

    public static void CcSelectionPrefersMeleeThenLowestHpAndWolvesUsesCurrentTarget()
    {
        var lowRanged = Candidate(
            slot: 1,
            actor: new(0x2001, 0x201),
            currentHp: 5,
            comboRange: false,
            fireRange: true);
        var highMelee = Candidate(
            slot: 4,
            actor: new(0x2004, 0x204),
            currentHp: 80,
            comboRange: true,
            fireRange: true);
        var selected = MonkHeldComboRules.SelectBestCandidate(
            SupportedPvPContext.CrystallineConflict,
            MonkHeldComboRules.DragonKickActionId,
            fireReplyLocallyReady: true,
            windReplyLocallyReady: true,
            thunderclapLocallyReady: true,
            hasExactOwnFireResonance: false,
            [lowRanged, highMelee]);
        Equal(highMelee.Actor, selected!.Value.Actor,
            "melee tier wins before HP");

        var rangedHigh = lowRanged with
        {
            EnemySlot = 2,
            Actor = new(0x2002, 0x202),
            CurrentHp = 70,
        };
        selected = MonkHeldComboRules.SelectBestCandidate(
            SupportedPvPContext.CrystallineConflict,
            MonkHeldComboRules.DragonKickActionId,
            true, true, true, false,
            [rangedHigh, lowRanged]);
        Equal(lowRanged.Actor, selected!.Value.Actor,
            "lowest HP wins within ranged tier");

        var duplicate = lowRanged with { EnemySlot = 5 };
        False(MonkHeldComboRules.SelectBestCandidate(
                SupportedPvPContext.CrystallineConflict,
                MonkHeldComboRules.DragonKickActionId,
                true, true, true, false,
                [lowRanged, duplicate]).HasValue,
            "duplicate canonical identity fails closed");

        var wolves = Candidate(
            slot: 0,
            actor: Target,
            context: SupportedPvPContext.WolvesDen);
        selected = MonkHeldComboRules.SelectBestCandidate(
            SupportedPvPContext.WolvesDen,
            MonkHeldComboRules.DragonKickActionId,
            true, true, true, false,
            [wolves]);
        Equal(Target, selected!.Value.Actor, "one current <t> survives");
        False(MonkHeldComboRules.SelectBestCandidate(
                SupportedPvPContext.WolvesDen,
                MonkHeldComboRules.DragonKickActionId,
                true, true, true, false,
                [wolves, wolves with { Actor = new(0x2002, 0x202) }]).HasValue,
            "Wolves never chooses among targets");
    }

    public static void NormalRouteRequiresExactNextCarrierAndTrueRangedFallback()
    {
        var first = MonkHeldComboRules.Observe(
            MonkHeldComboState.Initial,
            Observation(MonkHeldComboRules.DragonKickActionId));
        Dispatch(first, MonkHeldComboRules.DragonKickActionId,
            MonkHeldComboActionPurpose.NormalCombo, "Dragon Kick");
        var accepted = Accept(first, 1_001);
        Equal(MonkHeldComboPhase.AwaitCarrierTransition,
            accepted.Phase, "await exact next carrier");

        var same = MonkHeldComboRules.Observe(
            accepted,
            Observation(MonkHeldComboRules.DragonKickActionId, now: 1_002));
        Equal(MonkHeldComboDecisionReason.CarrierTransitionPending,
            same.Reason, "same carrier waits");
        var next = MonkHeldComboRules.Observe(
            accepted,
            Observation(MonkHeldComboRules.TwinSnakesActionId, now: 1_002));
        Dispatch(next, MonkHeldComboRules.TwinSnakesActionId,
            MonkHeldComboActionPurpose.NormalCombo, "Twin Snakes");

        var skipped = MonkHeldComboRules.Observe(
            accepted,
            Observation(MonkHeldComboRules.DemolishActionId, now: 1_002));
        Equal(MonkHeldComboDecisionKind.Cancelled, skipped.Kind,
            "carrier skip cancels");
        Equal(MonkHeldComboDecisionReason.CarrierDrift, skipped.Reason,
            "carrier skip reason");

        var inMeleeButGcdLocked = MonkHeldComboRules.Observe(
            MonkHeldComboState.Initial,
            Observation(
                MonkHeldComboRules.DragonKickActionId,
                candidate: Candidate(comboRange: true, fireRange: true),
                comboReady: false,
                fireReady: true));
        False(inMeleeButGcdLocked.ShouldDispatch,
            "GCD lock cannot turn Fire's Reply into a melee fallback");

        var trulyRanged = MonkHeldComboRules.Observe(
            MonkHeldComboState.Initial,
            Observation(
                MonkHeldComboRules.DragonKickActionId,
                candidate: Candidate(comboRange: false, fireRange: true),
                comboReady: false,
                fireReady: true));
        Dispatch(trulyRanged, MonkHeldComboRules.FireReplyActionId,
            MonkHeldComboActionPurpose.FireReplyFallback,
            "true ranged fallback");
    }

    public static void PhantomWorkflowUsesProofRangeAndReservedPhoenix()
    {
        var outOfRange = Candidate(
            comboRange: false,
            windRange: true,
            thunderRange: true,
            phantomRange: false);
        var wind = MonkHeldComboRules.Observe(
            MonkHeldComboState.Initial,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                outOfRange,
                windReady: true,
                thunderReady: true,
                phoenixReady: true));
        Dispatch(wind, MonkHeldComboRules.WindReplyActionId,
            MonkHeldComboActionPurpose.WindReplySetup, "Wind setup");
        var awaitingPressure = Accept(wind, 1_001, sequence: 10);
        Equal(MonkHeldComboPhase.AwaitPressurePoint,
            awaitingPressure.Phase, "await Pressure Point");

        var pressure = outOfRange with { HasExactOwnPressurePoint = true };
        var thunder = MonkHeldComboRules.Observe(
            awaitingPressure,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                pressure,
                windReady: false,
                thunderReady: true,
                phoenixReady: true,
                now: 1_002));
        Dispatch(thunder, MonkHeldComboRules.ThunderclapActionId,
            MonkHeldComboActionPurpose.ThunderclapReturn,
            "Thunderclap only while out of Phantom range");
        var afterThunder = Accept(thunder, 1_003);

        var arrived = pressure with { PhantomRushTargetReady = true };
        var phoenix = MonkHeldComboRules.Observe(
            afterThunder,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                arrived,
                windReady: false,
                thunderReady: true,
                phoenixReady: true,
                now: 1_004));
        Dispatch(phoenix, MonkHeldComboRules.RisingPhoenixActionId,
            MonkHeldComboActionPurpose.RisingPhoenixBuff,
            "reserved Rising Phoenix");
        var awaitingFire = Accept(phoenix, 1_005, sequence: 20);
        Equal(MonkHeldComboPhase.AwaitFireResonance,
            awaitingFire.Phase, "await Fire Resonance");

        var fire = MonkHeldComboRules.Observe(
            awaitingFire,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                arrived,
                windReady: false,
                thunderReady: false,
                phoenixReady: false,
                fireResonance: true,
                now: 1_006));
        Dispatch(fire, MonkHeldComboRules.PhantomRushActionId,
            MonkHeldComboActionPurpose.PhantomRushFinish,
            "buffed Phantom Rush");
        var complete = MonkHeldComboRules.ApplyNativeAttemptOutcome(
            fire.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_007,
            confirmationSequenceBaseline: 0);
        True(complete.RouteComplete, "Phantom completes route");
        Equal(MonkHeldComboPhase.Waiting,
            complete.NextState.Phase, "route is spent");
    }

    public static void MissingOrExpiredProofFailsClosed()
    {
        var phantomRange = Candidate(phantomRange: true, windRange: true);
        var wind = MonkHeldComboRules.Observe(
            MonkHeldComboState.Initial,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                phantomRange,
                windReady: true,
                phoenixReady: true));
        var awaitingPressure = Accept(wind, 1_001, sequence: 10);
        var missing = MonkHeldComboRules.Observe(
            awaitingPressure,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                phantomRange,
                windReady: false,
                phoenixReady: true,
                confirmationBoundary: true,
                now: 1_002));
        Equal(MonkHeldComboDecisionKind.Cancelled, missing.Kind,
            "accepted Wind without proof cancels");
        Equal(MonkHeldComboDecisionReason.PressurePointMissing,
            missing.Reason, "missing Pressure Point reason");

        var proven = phantomRange with { HasExactOwnPressurePoint = true };
        var windAgain = MonkHeldComboRules.Observe(
            MonkHeldComboState.Initial,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                phantomRange,
                windReady: true,
                phoenixReady: true,
                now: 2_000));
        var pressureWait = Accept(windAgain, 2_001, sequence: 11);
        var phoenix = MonkHeldComboRules.Observe(
            pressureWait,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                proven,
                windReady: false,
                phoenixReady: true,
                now: 2_002));
        var awaitingFire = Accept(phoenix, 2_003, sequence: 20);
        var expiredPressure = MonkHeldComboRules.Observe(
            awaitingFire,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                phantomRange,
                windReady: false,
                phoenixReady: false,
                fireResonance: true,
                now: 2_004));
        Equal(MonkHeldComboDecisionReason.PressurePointMissing,
            expiredPressure.Reason, "Pressure proof rechecked after Phoenix");

        var noWind = MonkHeldComboRules.Observe(
            MonkHeldComboState.Initial,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                phantomRange,
                windReady: false,
                phoenixReady: true,
                now: 3_000));
        Dispatch(noWind, MonkHeldComboRules.RisingPhoenixActionId,
            MonkHeldComboActionPurpose.RisingPhoenixBuff,
            "no-Wind Phoenix path");
        var fireWait = Accept(noWind, 3_001, sequence: 30);
        var missingFire = MonkHeldComboRules.Observe(
            fireWait,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                phantomRange,
                windReady: false,
                phoenixReady: false,
                confirmationBoundary: true,
                now: 3_002));
        Equal(MonkHeldComboDecisionReason.FireResonanceMissing,
            missingFire.Reason, "accepted Phoenix requires Fire proof");

        var noPhoenix = MonkHeldComboRules.Observe(
            MonkHeldComboState.Initial,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                phantomRange,
                windReady: false,
                phoenixReady: false,
                now: 4_000));
        Dispatch(noPhoenix, MonkHeldComboRules.PhantomRushActionId,
            MonkHeldComboActionPurpose.PhantomRushFinish,
            "unavailable Phoenix never stalls safe Phantom");
    }

    public static void StableFalseAloneRetriesAndStatusDriftIsAmbiguous()
    {
        var fingerprint = Fingerprint(
            MonkHeldComboRules.WindReplyActionId,
            sequence: 7);
        Equal(ClientActionAttemptOutcome.ClientRejected,
            MonkHeldComboRules.ClassifyActionBoundary(
                false,
                MonkHeldComboRules.WindReplyActionId,
                MonkHeldComboRules.PhantomRushActionId,
                0, 0,
                MonkHeldComboRules.PhantomRushActionId,
                MonkHeldComboRules.PhantomRushActionId,
                false, false,
                false, false,
                fingerprint,
                fingerprint),
            "stable explicit false is retryable");
        Equal(ClientActionAttemptOutcome.AcceptanceUnknown,
            MonkHeldComboRules.ClassifyActionBoundary(
                false,
                MonkHeldComboRules.WindReplyActionId,
                MonkHeldComboRules.PhantomRushActionId,
                0, 0,
                MonkHeldComboRules.PhantomRushActionId,
                MonkHeldComboRules.PhantomRushActionId,
                false, true,
                false, false,
                fingerprint,
                fingerprint),
            "status change makes false ambiguous");

        var dispatch = MonkHeldComboRules.Observe(
            MonkHeldComboState.Initial,
            Observation(
                MonkHeldComboRules.PhantomRushActionId,
                Candidate(phantomRange: true),
                windReady: false,
                phoenixReady: false));
        var exactCandidate = Candidate(phantomRange: true);
        var exactObservation = Observation(
            MonkHeldComboRules.PhantomRushActionId,
            exactCandidate,
            windReady: false,
            phoenixReady: false);
        True(MonkHeldComboRules.CanUseFrozenIntent(
                dispatch.NextState,
                exactObservation,
                exactCandidate),
            "exact actor key route and action remain usable");
        var driftedCandidate = exactCandidate with
        {
            Actor = new(0x2099, 0x299),
        };
        False(MonkHeldComboRules.CanUseFrozenIntent(
                dispatch.NextState,
                exactObservation with { Candidate = driftedCandidate },
                driftedCandidate),
            "actor drift cannot substitute");
        False(MonkHeldComboRules.CanUseFrozenIntent(
                dispatch.NextState,
                exactObservation with { HeldGameplayKeyCode = 66 },
                exactCandidate),
            "key drift cannot substitute");
        False(MonkHeldComboRules.CanUseFrozenIntent(
                dispatch.NextState,
                exactObservation with { HigherPriorityClaimed = true },
                exactCandidate),
            "higher priority blocks the final boundary");
        var retry = MonkHeldComboRules.ApplyNativeAttemptOutcome(
            dispatch.NextState,
            ClientActionAttemptOutcome.ClientRejected,
            1_001,
            0);
        True(retry.RetryScheduled, "clean false retains exact intent");
        var ambiguous = MonkHeldComboRules.ApplyNativeAttemptOutcome(
            dispatch.NextState,
            ClientActionAttemptOutcome.AcceptanceUnknown,
            1_001,
            0);
        Equal(HeldActionRetryDisposition.AmbiguousTerminal,
            ambiguous.Disposition, "ambiguous result terminal");
        True(ambiguous.Terminal, "ambiguous state terminal");
    }

    private static MonkHeldComboCandidate Candidate(
        int slot = 3,
        TargetPressureActorIdentity? actor = null,
        uint currentHp = 50,
        uint maxHp = 100,
        SupportedPvPContext context = SupportedPvPContext.CrystallineConflict,
        bool comboRange = true,
        bool fireRange = true,
        bool windRange = true,
        bool thunderRange = true,
        bool phantomRange = true) => new(
        context,
        slot,
        actor ?? Target,
        ExactCanonicalIdentity: true,
        Alive: true,
        Targetable: true,
        currentHp,
        maxHp,
        ComboTargetReady: comboRange,
        FireReplyTargetReady: fireRange,
        WindReplyTargetReady: windRange,
        ThunderclapTargetReady: thunderRange,
        PhantomRushTargetReady: phantomRange,
        HasExactOwnPressurePoint: false);

    private static MonkHeldComboObservation Observation(
        uint carrier,
        MonkHeldComboCandidate? candidate = null,
        bool comboReady = true,
        bool fireReady = true,
        bool windReady = true,
        bool thunderReady = true,
        bool phoenixReady = true,
        bool fireResonance = false,
        bool confirmationBoundary = false,
        bool nativeBoundary = true,
        long now = 1_000) => new(
        ConfigurationEnabled: true,
        Context: candidate?.Context ??
            SupportedPvPContext.CrystallineConflict,
        LocalPlayer: Local,
        IsLocalPlayerAlive: true,
        LocalJobId: MonkHeldComboRules.MonkJobId,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        HeldGameplayKeyCode: HeldKey,
        FrozenKeyStillDown: true,
        ResolvedComboActionId: carrier,
        ComboActionLocallyReady: comboReady,
        FireReplyLocallyReady: fireReady,
        WindReplyLocallyReady: windReady,
        ThunderclapLocallyReady: thunderReady,
        RisingPhoenixLocallyReady: phoenixReady,
        HasExactOwnFireResonance: fireResonance,
        ConfirmationBoundaryReopened: confirmationBoundary,
        NativeBoundaryReady: nativeBoundary,
        Candidate: candidate ?? Candidate(),
        HardReset: false,
        NowMilliseconds: now);

    private static MonkHeldComboState Accept(
        MonkHeldComboDecision decision,
        long now,
        ushort sequence = 0)
    {
        True(decision.ShouldDispatch, "accepted decision dispatches");
        var accepted = MonkHeldComboRules.ApplyNativeAttemptOutcome(
            decision.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            now,
            sequence);
        True(accepted.ClientAccepted, "client accepted");
        return accepted.NextState;
    }

    private static void Dispatch(
        MonkHeldComboDecision decision,
        uint actionId,
        MonkHeldComboActionPurpose purpose,
        string label)
    {
        True(decision.ShouldDispatch, $"{label} dispatches");
        Equal(actionId, decision.ActionId, $"{label} action");
        Equal(purpose, decision.Purpose, $"{label} purpose");
        True(decision.InputClaimed, $"{label} claims input");
    }

    private static ClientActionAttemptFingerprint Fingerprint(
        uint actionId,
        ushort sequence) => new(
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
        AdjustedActionId: actionId,
        IsActionOffCooldown: true,
        ResourceStatus: 0);

    private static void True(bool condition, string label)
    {
        if (!condition)
            throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) =>
        True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{label}: expected {expected}, got {actual}");
        }
    }
}
