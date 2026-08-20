using SeitonSense.Core;

internal static class SmartKardiaSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer =
        new(10_001, 1_001);

    internal static void ExactIdsAndCandidateEligibilityArePinned()
    {
        Equal(40u, SmartKardiaRules.SageJobId, "SGE job");
        Equal(29_264u, SmartKardiaRules.ActionId, "PvP Kardia action");
        Equal(29_258u, SmartKardiaRules.EukrasiaActionId, "PvP Eukrasia action");
        True(SmartKardiaRules.IsKardiaStatus(2_871), "PvP Kardia status");
        True(SmartKardiaRules.IsKardionStatus(2_872), "PvP Kardion status");
        True(SmartKardiaRules.IsEukrasiaStatus(3_107), "PvP Eukrasia status");
        False(SmartKardiaRules.IsKardiaStatus(2_604), "PvE Kardia rejected");
        False(SmartKardiaRules.IsKardionStatus(2_605), "PvE Kardion rejected");
        False(SmartKardiaRules.IsEukrasiaStatus(2_606), "PvE Eukrasia rejected");
        Equal(2u, SmartKardiaRules.EukrasiaMaximumCharges, "Eukrasia charges");
        Equal(2_000L, SmartKardiaRules.TriggerLifetimeMilliseconds, "bounded trigger lifetime");

        var ally = Candidate(2, pressure: 2);
        var self = Candidate(1, pressure: 2, isSelf: true, actor: LocalPlayer);
        True(IsEligible(ally), "ally at inclusive pressure threshold");
        True(IsEligible(self), "self at inclusive pressure threshold");
        False(IsEligible(ally with { UniqueIncomingEnemyCount = 1 }), "below pressure threshold");
        False(IsEligible(ally with { PressureKnown = false }), "unknown pressure");
        False(IsEligible(ally with { ExactPartyIdentity = false }), "inexact party identity");
        False(IsEligible(ally with { PartySlot = 0 }), "invalid P-slot");
        False(IsEligible(ally with { IsSelf = true }), "false self flag");
        False(IsEligible(self with { IsSelf = false }), "missing self flag");
        False(IsEligible(ally with { Alive = false }), "dead actor");
        False(IsEligible(ally with { Targetable = false }), "untargetable actor");
        False(IsEligible(ally with { CurrentHp = 0 }), "zero HP");
        False(IsEligible(ally with { MaximumHp = 0 }), "zero maximum HP");
        False(IsEligible(ally with { CurrentHp = 101 }), "HP exceeds maximum");
        False(IsEligible(ally with { NativeTargetValid = false }), "native target invalid");
        False(
            IsEligible(ally with { NativeRangeAndLineOfSight = false }),
            "native range or line of sight failed");
    }

    internal static void CompletePartyViewRejectsIdentityAmbiguity()
    {
        var complete = Candidates();
        True(HasComplete(complete), "exact five-member view");
        False(HasComplete(complete[..4]), "missing party member");
        False(HasComplete(null), "missing view");

        var duplicateSlot = complete.ToArray();
        duplicateSlot[4] = duplicateSlot[4] with { PartySlot = 2 };
        False(HasComplete(duplicateSlot), "duplicate P-slot");

        var duplicateGameObjectId = complete.ToArray();
        duplicateGameObjectId[4] = duplicateGameObjectId[4] with
        {
            Actor = new TargetPressureActorIdentity(
                duplicateGameObjectId[1].Actor.GameObjectId,
                duplicateGameObjectId[4].Actor.EntityId),
        };
        False(HasComplete(duplicateGameObjectId), "partial GOID collision");

        var duplicateEntityId = complete.ToArray();
        duplicateEntityId[4] = duplicateEntityId[4] with
        {
            Actor = new TargetPressureActorIdentity(
                duplicateEntityId[4].Actor.GameObjectId,
                duplicateEntityId[1].Actor.EntityId),
        };
        False(HasComplete(duplicateEntityId), "partial EntityId collision");

        var inexact = complete.ToArray();
        inexact[2] = inexact[2] with { ExactPartyIdentity = false };
        False(HasComplete(inexact), "inexact party actor");

        var falseSelf = complete.ToArray();
        falseSelf[0] = falseSelf[0] with { IsSelf = false };
        False(HasComplete(falseSelf), "local actor must be marked self");

        var inventedSelf = complete.ToArray();
        inventedSelf[1] = inventedSelf[1] with { IsSelf = true };
        False(HasComplete(inventedSelf), "nonlocal actor cannot be self");
    }

    internal static void PartialLivePressureViewFailsClosed()
    {
        var partial = Candidates();
        partial[1] = partial[1] with { UniqueIncomingEnemyCount = 3 };
        partial[2] = partial[2] with
        {
            PressureKnown = false,
            UniqueIncomingEnemyCount = -1,
        };
        False(
            SmartKardiaRules.HasCompleteKnownPressureView(partial),
            "one live unknown actor makes the pressure view incomplete");
        Equal(
            -1,
            SmartKardiaRules.SelectBestCandidateIndex(partial, LocalPlayer),
            "partial pressure cannot rank");
        None(
            SmartKardiaRules.Observe(Observation() with { Candidates = partial }),
            SmartKardiaDecisionReason.IncompleteKnownPressureView);

        var deadUnknown = partial.ToArray();
        deadUnknown[2] = deadUnknown[2] with { Alive = false };
        True(
            SmartKardiaRules.HasCompleteKnownPressureView(deadUnknown),
            "dead unknown actor cannot affect selection");
        Dispatch(Observation() with { Candidates = deadUnknown }, "dead unknown ignored");

        var untargetableUnknown = partial.ToArray();
        untargetableUnknown[2] = untargetableUnknown[2] with { Targetable = false };
        True(
            SmartKardiaRules.HasCompleteKnownPressureView(untargetableUnknown),
            "untargetable unknown actor cannot affect selection");
        Dispatch(
            Observation() with { Candidates = untargetableUnknown },
            "untargetable unknown ignored");
    }

    internal static void RankingIsPressureThenExactHpThenStableSlot()
    {
        var candidates = Candidates();
        candidates[0] = candidates[0] with
        {
            UniqueIncomingEnemyCount = 2,
            CurrentHp = 10,
        };
        candidates[1] = candidates[1] with
        {
            UniqueIncomingEnemyCount = 3,
            CurrentHp = 90,
        };
        Equal(
            1,
            SmartKardiaRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "higher direct pressure wins before HP");

        candidates[0] = candidates[0] with
        {
            UniqueIncomingEnemyCount = 3,
            CurrentHp = 1,
            MaximumHp = 3,
        };
        candidates[1] = candidates[1] with
        {
            CurrentHp = 33,
            MaximumHp = 100,
        };
        Equal(
            1,
            SmartKardiaRules.SelectBestCandidateIndex(candidates, LocalPlayer),
            "33/100 is exactly lower than 1/3");

        candidates[0] = candidates[0] with { CurrentHp = 1, MaximumHp = 4 };
        candidates[1] = candidates[1] with { CurrentHp = 25, MaximumHp = 100 };
        var shuffled = new[]
        {
            candidates[4],
            candidates[1],
            candidates[3],
            candidates[0],
            candidates[2],
        };
        Equal(
            3,
            SmartKardiaRules.SelectBestCandidateIndex(shuffled, LocalPlayer),
            "equal ratios use stable lower P-slot independent of input order");
    }

    internal static void BestKardionStateNeverFallsThroughToAnAlternate()
    {
        var candidates = Candidates();
        candidates[1] = candidates[1] with
        {
            UniqueIncomingEnemyCount = 4,
            CurrentHp = 80,
            OwnKardionStateKnown = false,
        };
        candidates[2] = candidates[2] with
        {
            UniqueIncomingEnemyCount = 3,
            CurrentHp = 10,
        };

        None(
            SmartKardiaRules.Observe(Observation() with { Candidates = candidates }),
            SmartKardiaDecisionReason.SelectedKardionStateUnknown,
            selectedIndex: 1);

        candidates[1] = candidates[1] with
        {
            OwnKardionStateKnown = true,
            HasOwnKardion = true,
        };
        None(
            SmartKardiaRules.Observe(Observation() with { Candidates = candidates }),
            SmartKardiaDecisionReason.SelectedAlreadyHasOwnKardion,
            selectedIndex: 1);
    }

    internal static void DefaultSelfFallbackIsExactAndTerminal()
    {
        var noPressure = Candidates();
        Equal(
            0,
            SmartKardiaRules.SelectBestCandidateIndex(noPressure, LocalPlayer),
            "no pressure defaults to exact self");
        var fallback = SmartKardiaRules.Observe(
            Observation() with { Candidates = noPressure });
        True(fallback.ShouldDispatch, "exact self fallback dispatches");
        Equal(0, fallback.SelectedCandidateIndex, "self fallback P1");
        var selfIntent = fallback.Intent ??
            throw new InvalidOperationException("missing self fallback intent");
        True(selfIntent.IsSelf, "fallback intent is self");
        Equal(LocalPlayer, selfIntent.Target, "fallback freezes exact local actor");
        True(CanUse(selfIntent, noPressure[0]), "self fallback final validation");

        var allyPressure = noPressure.ToArray();
        allyPressure[3] = allyPressure[3] with { UniqueIncomingEnemyCount = 2 };
        Equal(
            3,
            SmartKardiaRules.SelectBestCandidateIndex(allyPressure, LocalPlayer),
            "pressure-qualified ally wins before self fallback");

        var ownKardion = noPressure.ToArray();
        ownKardion[0] = ownKardion[0] with { HasOwnKardion = true };
        None(
            SmartKardiaRules.Observe(Observation() with { Candidates = ownKardion }),
            SmartKardiaDecisionReason.SelectedAlreadyHasOwnKardion,
            selectedIndex: 0);
    }

    internal static void AcceptedTriggerIsBoundedAndIdentityExact()
    {
        var before = Evidence(charges: 2, hasStatus: false);
        True(
            SmartKardiaRules.TryCreateAcceptedTrigger(
                1,
                1_000,
                77,
                LocalPlayer,
                before,
                out var trigger),
            "exact accepted trigger is created");
        Equal(3_000L, trigger.ExpiresAtMilliseconds, "two-second expiry");
        True(
            SmartKardiaRules.IsTriggerCurrent(trigger, 1_000, 77, LocalPlayer),
            "inclusive accepted boundary");
        True(
            SmartKardiaRules.IsTriggerCurrent(trigger, 2_999, 77, LocalPlayer),
            "last millisecond is current");
        False(
            SmartKardiaRules.IsTriggerCurrent(trigger, 3_000, 77, LocalPlayer),
            "expiry boundary is terminal");
        False(
            SmartKardiaRules.IsTriggerCurrent(trigger, 999, 77, LocalPlayer),
            "clock rollback fails closed");
        False(
            SmartKardiaRules.IsTriggerCurrent(trigger, 1_100, 78, LocalPlayer),
            "territory drift fails closed");
        False(
            SmartKardiaRules.IsTriggerCurrent(
                trigger,
                1_100,
                77,
                new TargetPressureActorIdentity(99, 99)),
            "local identity drift fails closed");

        False(
            SmartKardiaRules.TryCreateAcceptedTrigger(
                0,
                1_000,
                77,
                LocalPlayer,
                before,
                out _),
            "zero token rejected");
        False(
            SmartKardiaRules.TryCreateAcceptedTrigger(
                2,
                1_000,
                77,
                LocalPlayer,
                before with { CurrentCharges = 0 },
                out _),
            "a pre-call charge must exist");
        True(
            SmartKardiaRules.TryCreateAcceptedTrigger(
                3,
                long.MaxValue - 1,
                77,
                LocalPlayer,
                before,
                out var saturated),
            "near-overflow trigger saturates safely");
        Equal(long.MaxValue, saturated.ExpiresAtMilliseconds, "expiry saturation");
    }

    internal static void CausalEvidenceRequiresChargeOrOwnedStatusTransition()
    {
        True(
            SmartKardiaRules.TryCreateAcceptedTrigger(
                1,
                1_000,
                77,
                LocalPlayer,
                Evidence(charges: 2, hasStatus: false),
                out var trigger),
            "baseline trigger");
        True(
            SmartKardiaRules.HasCausalEukrasiaEvidence(
                trigger,
                Evidence(charges: 1, hasStatus: false)),
            "charge decrement proves execution");
        True(
            SmartKardiaRules.HasCausalEukrasiaEvidence(
                trigger,
                Evidence(charges: 2, hasStatus: true)),
            "absent-to-own-status transition proves execution");
        False(
            SmartKardiaRules.HasCausalEukrasiaEvidence(
                trigger,
                Evidence(charges: 2, hasStatus: false)),
            "unchanged state is not causal proof");
        False(
            SmartKardiaRules.HasCausalEukrasiaEvidence(
                trigger,
                Evidence(charges: 1, hasStatus: false) with
                {
                    OwnStatusStateKnown = false,
                }),
            "unknown own-source status fails closed");
        False(
            SmartKardiaRules.HasCausalEukrasiaEvidence(
                trigger,
                Evidence(charges: 1, hasStatus: false) with
                {
                    AdjustedActionId = 0,
                }),
            "adjusted action drift fails closed");

        True(
            SmartKardiaRules.TryCreateAcceptedTrigger(
                2,
                1_000,
                77,
                LocalPlayer,
                Evidence(charges: 1, hasStatus: true),
                out var alreadyActive),
            "active-status trigger");
        False(
            SmartKardiaRules.HasCausalEukrasiaEvidence(
                alreadyActive,
                Evidence(charges: 2, hasStatus: true)),
            "charge increase and already-active status are not proof");
    }

    internal static void DispatchRequiresEveryEventAndSafetyGate()
    {
        var valid = Observation();
        Dispatch(valid, "valid event-driven observation");

        Cancel(valid with { HardReset = true }, SmartKardiaDecisionReason.HardReset);
        Cancel(valid with { ConfigurationEnabled = false }, SmartKardiaDecisionReason.ConfigurationDisabled);
        Cancel(valid with { IsCrystallineConflict = false }, SmartKardiaDecisionReason.OutsideCrystallineConflict);
        Cancel(valid with { LocalPlayer = default }, SmartKardiaDecisionReason.LocalPlayerIdentityInvalid);
        Cancel(valid with { IsLocalPlayerAlive = false }, SmartKardiaDecisionReason.LocalPlayerDead);
        Cancel(valid with { LocalJobId = 39 }, SmartKardiaDecisionReason.LocalJobInvalid);
        Cancel(valid with { MetadataVerified = false }, SmartKardiaDecisionReason.MetadataUnverified);
        Cancel(valid with { ActionHelpersSuppressedByGuard = true }, SmartKardiaDecisionReason.GuardSuppressed);
        Cancel(valid with { HigherPriorityClaimed = true }, SmartKardiaDecisionReason.HigherPriorityClaimed);
        Cancel(valid with { TriggerAvailable = false }, SmartKardiaDecisionReason.EukrasiaTriggerUnavailable);
        Cancel(valid with { TriggerEvidenceConfirmed = false }, SmartKardiaDecisionReason.EukrasiaEvidencePending);
        Cancel(valid with { FreshPressurePublicationAvailable = false }, SmartKardiaDecisionReason.PressurePublicationPending);
        Cancel(valid with { ResolvedActionId = 0 }, SmartKardiaDecisionReason.ResolvedActionInvalid);
        Cancel(valid with { ActionLocallyReady = false }, SmartKardiaDecisionReason.ActionNotReady);
        Cancel(valid with { AnimationLockClear = false }, SmartKardiaDecisionReason.AnimationLockActive);
        Cancel(valid with { CompleteExactPartyView = false }, SmartKardiaDecisionReason.IncompleteExactPartyView);
        Cancel(
            valid with { Candidates = valid.Candidates!.Take(4).ToArray() },
            SmartKardiaDecisionReason.IncompleteExactPartyView);
    }

    internal static void FrozenIntentCannotRerankFallbackOrRetry()
    {
        var candidates = Candidates();
        candidates[0] = candidates[0] with
        {
            UniqueIncomingEnemyCount = 2,
            CurrentHp = 10,
        };
        candidates[2] = candidates[2] with
        {
            UniqueIncomingEnemyCount = 4,
            CurrentHp = 80,
        };
        var decision = SmartKardiaRules.Observe(
            Observation() with { Candidates = candidates });
        True(decision.ShouldDispatch, "dispatch freezes one intent");
        Equal(2, decision.SelectedCandidateIndex, "highest pressure P3");

        var intent = decision.Intent ??
            throw new InvalidOperationException("missing frozen intent");
        Equal(SmartKardiaRules.ActionId, intent.ActionId, "frozen action");
        Equal(LocalPlayer, intent.LocalPlayer, "frozen local identity");
        Equal(candidates[2].Actor, intent.Target, "frozen target identity");

        var current = candidates[2] with
        {
            UniqueIncomingEnemyCount = 2,
            CurrentHp = 20,
        };
        True(CanUse(intent, current), "same actor remains valid at threshold");
        False(
            CanUse(intent, candidates[0] with { UniqueIncomingEnemyCount = 8 }),
            "now-better alternate cannot replace frozen actor");
        False(CanUse(intent, current with { PartySlot = 4 }), "P-slot drift");
        False(CanUse(intent, current with { PressureKnown = false }), "pressure unknown");
        False(CanUse(intent, current with { UniqueIncomingEnemyCount = 1 }), "pressure below threshold");
        False(CanUse(intent, current with { OwnKardionStateKnown = false }), "status ownership unknown");
        False(CanUse(intent, current with { HasOwnKardion = true }), "own Kardion appeared");
        False(CanUse(intent, current with { NativeTargetValid = false }), "native target drift");
        False(CanUse(intent, current with { NativeRangeAndLineOfSight = false }), "range or LoS drift");
        False(CanUse(intent, current with { Alive = false }), "target died");
        False(CanUse(intent, current, configurationEnabled: false), "config drift");
        False(CanUse(intent, current, isCrystallineConflict: false), "context drift");
        False(CanUse(intent, current, currentLocalJobId: 39), "job drift");
        False(
            CanUse(
                intent,
                current,
                currentLocalPlayer: new TargetPressureActorIdentity(0, 0)),
            "local identity drift");
        False(CanUse(intent, current, metadataVerified: false), "metadata drift");
        False(CanUse(intent, current, guardSuppressed: true), "Guard drift");
        False(CanUse(intent, current, resolvedActionId: 0), "action identity drift");
        False(CanUse(intent, current, actionReady: false), "cooldown drift");
        False(CanUse(intent, current, animationLockClear: false), "animation lock drift");
        False(CanUse(intent, current, triggerConfirmed: false), "causal trigger drift");
    }

    private static SmartKardiaObservation Observation() =>
        new(
            ConfigurationEnabled: true,
            IsCrystallineConflict: true,
            LocalJobId: SmartKardiaRules.SageJobId,
            LocalPlayer,
            IsLocalPlayerAlive: true,
            MetadataVerified: true,
            ActionHelpersSuppressedByGuard: false,
            HigherPriorityClaimed: false,
            TriggerAvailable: true,
            TriggerEvidenceConfirmed: true,
            FreshPressurePublicationAvailable: true,
            ResolvedActionId: SmartKardiaRules.ActionId,
            ActionLocallyReady: true,
            AnimationLockClear: true,
            CompleteExactPartyView: true,
            Candidates());

    private static SmartKardiaEukrasiaEvidence Evidence(
        uint charges,
        bool hasStatus) =>
        new(
            SmartKardiaRules.EukrasiaActionId,
            charges,
            OwnStatusStateKnown: true,
            HasOwnEukrasia: hasStatus);

    private static SmartKardiaCandidate[] Candidates() =>
    [
        Candidate(1, pressure: 0, isSelf: true, actor: LocalPlayer),
        Candidate(2, pressure: 0),
        Candidate(3, pressure: 0),
        Candidate(4, pressure: 0),
        Candidate(5, pressure: 0),
    ];

    private static SmartKardiaCandidate Candidate(
        int slot,
        int pressure,
        bool isSelf = false,
        TargetPressureActorIdentity? actor = null) =>
        new(
            slot,
            actor ?? new TargetPressureActorIdentity(
                (ulong)(20_000 + slot),
                (uint)(2_000 + slot)),
            ExactPartyIdentity: true,
            IsSelf: isSelf,
            Alive: true,
            Targetable: true,
            CurrentHp: 50,
            MaximumHp: 100,
            NativeTargetValid: true,
            NativeRangeAndLineOfSight: true,
            PressureKnown: true,
            UniqueIncomingEnemyCount: pressure,
            OwnKardionStateKnown: true,
            HasOwnKardion: false);

    private static bool IsEligible(SmartKardiaCandidate candidate) =>
        SmartKardiaRules.IsEligibleCandidate(candidate, LocalPlayer);

    private static bool HasComplete(
        IReadOnlyList<SmartKardiaCandidate>? candidates) =>
        SmartKardiaRules.HasCompleteExactPartyView(candidates, LocalPlayer);

    private static bool CanUse(
        SmartKardiaIntent intent,
        SmartKardiaCandidate candidate,
        bool configurationEnabled = true,
        bool isCrystallineConflict = true,
        uint currentLocalJobId = SmartKardiaRules.SageJobId,
        TargetPressureActorIdentity? currentLocalPlayer = null,
        bool metadataVerified = true,
        bool guardSuppressed = false,
        uint resolvedActionId = SmartKardiaRules.ActionId,
        bool actionReady = true,
        bool animationLockClear = true,
        bool triggerConfirmed = true) =>
        SmartKardiaRules.CanUseFrozenIntent(
            intent,
            candidate,
            configurationEnabled,
            isCrystallineConflict,
            currentLocalJobId,
            currentLocalPlayer ?? LocalPlayer,
            isLocalPlayerAlive: true,
            metadataVerified,
            actionHelpersSuppressedByGuard: guardSuppressed,
            resolvedActionId,
            actionLocallyReady: actionReady,
            animationLockClear,
            triggerEvidenceConfirmed: triggerConfirmed);

    private static void Dispatch(
        SmartKardiaObservation observation,
        string label)
    {
        var decision = SmartKardiaRules.Observe(observation);
        Equal(SmartKardiaDecisionKind.Dispatch, decision.Kind, label);
        Equal(SmartKardiaDecisionReason.None, decision.Reason, label);
        True(decision.ShouldDispatch, label);
    }

    private static void Cancel(
        SmartKardiaObservation observation,
        SmartKardiaDecisionReason reason)
    {
        var decision = SmartKardiaRules.Observe(observation);
        Equal(SmartKardiaDecisionKind.Cancelled, decision.Kind, reason.ToString());
        Equal(reason, decision.Reason, reason.ToString());
        False(decision.ShouldDispatch, reason.ToString());
    }

    private static void None(
        SmartKardiaDecision decision,
        SmartKardiaDecisionReason reason,
        int selectedIndex = -1)
    {
        Equal(SmartKardiaDecisionKind.None, decision.Kind, reason.ToString());
        Equal(reason, decision.Reason, reason.ToString());
        Equal(selectedIndex, decision.SelectedCandidateIndex, reason.ToString());
        False(decision.ShouldDispatch, reason.ToString());
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
