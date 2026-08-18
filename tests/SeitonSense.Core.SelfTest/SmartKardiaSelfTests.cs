using SeitonSense.Core;

internal static class SmartKardiaSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer =
        new(10_001, 1_001);

    internal static void ExactIdsAndCandidateEligibilityArePinned()
    {
        Equal(40u, SmartKardiaRules.SageJobId, "SGE job");
        Equal(29_264u, SmartKardiaRules.ActionId, "PvP Kardia action");
        True(SmartKardiaRules.IsKardiaStatus(2_871), "PvP Kardia status");
        True(SmartKardiaRules.IsKardionStatus(2_872), "PvP Kardion status");
        False(SmartKardiaRules.IsKardiaStatus(2_604), "PvE Kardia rejected");
        False(SmartKardiaRules.IsKardionStatus(2_605), "PvE Kardion rejected");

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
            "native 30y or line of sight failed");
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
            "one live unknown actor makes the whole pressure view incomplete");
        Equal(
            -1,
            SmartKardiaRules.SelectBestCandidateIndex(partial, LocalPlayer),
            "partial pressure cannot silently outrank the unknown actor");
        None(
            SmartKardiaRules.Observe(Observation() with { Candidates = partial }),
            SmartKardiaDecisionReason.IncompleteKnownPressureView);

        var deadUnknown = partial.ToArray();
        deadUnknown[2] = deadUnknown[2] with
        {
            Alive = false,
            Targetable = true,
        };
        True(
            SmartKardiaRules.HasCompleteKnownPressureView(deadUnknown),
            "dead unknown actor cannot affect selection");
        Dispatch(
            Observation() with { Candidates = deadUnknown },
            "dead unknown actor is ignored");

        var untargetableUnknown = partial.ToArray();
        untargetableUnknown[2] = untargetableUnknown[2] with
        {
            Alive = true,
            Targetable = false,
        };
        True(
            SmartKardiaRules.HasCompleteKnownPressureView(untargetableUnknown),
            "untargetable unknown actor cannot affect selection");
        Dispatch(
            Observation() with { Candidates = untargetableUnknown },
            "untargetable unknown actor is ignored");
    }

    internal static void RankingIsPressureThenExactHpThenStableSlot()
    {
        var candidates = Candidates();
        candidates[0] = candidates[0] with
        {
            PressureKnown = true,
            UniqueIncomingEnemyCount = 2,
            CurrentHp = 10,
        };
        candidates[1] = candidates[1] with
        {
            PressureKnown = true,
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

        candidates[0] = candidates[0] with
        {
            CurrentHp = 1,
            MaximumHp = 4,
        };
        candidates[1] = candidates[1] with
        {
            CurrentHp = 25,
            MaximumHp = 100,
        };
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
            "equal ratios use lower stable P-slot independent of input order");
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

        var unknown = SmartKardiaRules.Observe(
            Observation() with { Candidates = candidates });
        None(
            unknown,
            SmartKardiaDecisionReason.SelectedKardionStateUnknown,
            selectedIndex: 1);

        candidates[1] = candidates[1] with
        {
            OwnKardionStateKnown = true,
            HasOwnKardion = true,
        };
        var alreadyAssigned = SmartKardiaRules.Observe(
            Observation() with { Candidates = candidates });
        None(
            alreadyAssigned,
            SmartKardiaDecisionReason.SelectedAlreadyHasOwnKardion,
            selectedIndex: 1);

        False(unknown.ShouldConsumeInputGeneration, "unknown status does not consume");
        False(alreadyAssigned.ShouldConsumeInputGeneration, "already assigned does not consume");
    }

    internal static void DispatchRequiresEveryHeldKeyAndSafetyGate()
    {
        var valid = Observation();
        Dispatch(valid, "valid held generation");

        Cancel(valid with { HardReset = true }, SmartKardiaDecisionReason.HardReset);
        Cancel(valid with { ConfigurationEnabled = false }, SmartKardiaDecisionReason.ConfigurationDisabled);
        Cancel(valid with { IsCrystallineConflict = false }, SmartKardiaDecisionReason.OutsideCrystallineConflict);
        Cancel(valid with { LocalPlayer = default }, SmartKardiaDecisionReason.LocalPlayerIdentityInvalid);
        Cancel(valid with { IsLocalPlayerAlive = false }, SmartKardiaDecisionReason.LocalPlayerDead);
        Cancel(valid with { LocalJobId = 39 }, SmartKardiaDecisionReason.LocalJobInvalid);
        Cancel(valid with { MetadataVerified = false }, SmartKardiaDecisionReason.MetadataUnverified);
        Cancel(valid with { ActionHelpersSuppressedByGuard = true }, SmartKardiaDecisionReason.GuardSuppressed);
        Cancel(valid with { HigherPriorityClaimed = true }, SmartKardiaDecisionReason.HigherPriorityClaimed);
        Cancel(valid with { InputProbeSucceeded = false }, SmartKardiaDecisionReason.InputProbeUnavailable);
        Cancel(valid with { IsTextInputActive = true }, SmartKardiaDecisionReason.TextInputActive);
        Cancel(valid with { HeldGameplayKeyEligible = false }, SmartKardiaDecisionReason.NoHeldGameplayKey);
        Cancel(valid with { ResolvedActionId = 0 }, SmartKardiaDecisionReason.ResolvedActionInvalid);
        Cancel(valid with { ActionLocallyReady = false }, SmartKardiaDecisionReason.ActionNotReady);
        Cancel(valid with { CompleteExactPartyView = false }, SmartKardiaDecisionReason.IncompleteExactPartyView);
        Cancel(
            valid with { Candidates = valid.Candidates!.Take(4).ToArray() },
            SmartKardiaDecisionReason.IncompleteExactPartyView);

        var noPressure = Candidates();
        var none = SmartKardiaRules.Observe(valid with { Candidates = noPressure });
        None(none, SmartKardiaDecisionReason.NoKnownPressureTarget);
        False(none.ShouldConsumeInputGeneration, "no pressure does not steal hold");
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
        True(decision.ShouldConsumeInputGeneration, "consume before native work");
        Equal(2, decision.SelectedCandidateIndex, "highest pressure P3");

        var intent = decision.Intent ??
            throw new InvalidOperationException("missing frozen intent");
        Equal(SmartKardiaRules.ActionId, intent.ActionId, "frozen action");
        Equal(LocalPlayer, intent.LocalPlayer, "frozen local identity");
        Equal(candidates[2].Actor, intent.Target, "frozen target identity");
        Equal(4, intent.SelectedIncomingEnemyCount, "frozen pressure diagnostics");

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
        False(
            CanUse(
                intent,
                current with
                {
                    Actor = new TargetPressureActorIdentity(90_000, 9_000),
                }),
            "actor drift");
        False(CanUse(intent, current with { IsSelf = true }), "self flag drift");
        False(CanUse(intent, current with { PressureKnown = false }), "pressure unknown");
        False(CanUse(intent, current with { UniqueIncomingEnemyCount = 1 }), "pressure below threshold");
        False(CanUse(intent, current with { OwnKardionStateKnown = false }), "status ownership unknown");
        False(CanUse(intent, current with { HasOwnKardion = true }), "own Kardion appeared");
        False(CanUse(intent, current with { NativeTargetValid = false }), "native target drift");
        False(CanUse(intent, current with { NativeRangeAndLineOfSight = false }), "range or LoS drift");
        False(CanUse(intent, current with { Alive = false }), "target died");
        False(CanUse(intent, current with { Targetable = false }), "target untargetable");
        False(CanUse(intent, current, configurationEnabled: false), "config drift");
        False(CanUse(intent, current, isCrystallineConflict: false), "context drift");
        False(CanUse(intent, current, currentLocalJobId: 39), "job drift");
        False(
            CanUse(
                intent,
                current,
                currentLocalPlayer: new TargetPressureActorIdentity(90_001, 9_001)),
            "local identity drift");
        False(CanUse(intent, current, isLocalPlayerAlive: false), "local death");
        False(CanUse(intent, current, metadataVerified: false), "metadata drift");
        False(CanUse(intent, current, guardSuppressed: true), "Guard drift");
        False(CanUse(intent, current, resolvedActionId: 0), "action drift");
        False(CanUse(intent, current, actionLocallyReady: false), "readiness drift");
    }

    internal static void ConsumedPhysicalGenerationCannotRetry()
    {
        var keyState = PhysicalGameplayKeyRules.Observe(
            PhysicalGameplayKeyState.Initial,
            new PhysicalGameplayKeyObservation(
                IsDown: false,
                IsTextInputActive: false)).NextState;
        var pressed = PhysicalGameplayKeyRules.Observe(
            keyState,
            new PhysicalGameplayKeyObservation(
                IsDown: true,
                IsTextInputActive: false));
        True(pressed.IsHeldEligible, "new physical generation eligible");

        var first = SmartKardiaRules.Observe(Observation() with
        {
            HeldGameplayKeyEligible = pressed.IsHeldEligible,
        });
        True(first.ShouldDispatch, "first intent owns the generation");

        keyState = PhysicalGameplayKeyRules.Consume(pressed.NextState);
        var stillHeld = PhysicalGameplayKeyRules.Observe(
            keyState,
            new PhysicalGameplayKeyObservation(
                IsDown: true,
                IsTextInputActive: false));
        False(stillHeld.IsHeldEligible, "consumed generation remains spent");

        var retry = SmartKardiaRules.Observe(Observation() with
        {
            HeldGameplayKeyEligible = stillHeld.IsHeldEligible,
        });
        False(retry.ShouldDispatch, "same hold cannot retry");
        False(retry.ShouldConsumeInputGeneration, "no second consumption");
        Equal(
            SmartKardiaDecisionReason.NoHeldGameplayKey,
            retry.Reason,
            "spent reason");
    }

    private static SmartKardiaObservation Observation()
    {
        var candidates = Candidates();
        candidates[1] = candidates[1] with
        {
            PressureKnown = true,
            UniqueIncomingEnemyCount = 2,
        };
        return new SmartKardiaObservation(
            ConfigurationEnabled: true,
            IsCrystallineConflict: true,
            LocalJobId: SmartKardiaRules.SageJobId,
            LocalPlayer,
            IsLocalPlayerAlive: true,
            MetadataVerified: true,
            ActionHelpersSuppressedByGuard: false,
            HigherPriorityClaimed: false,
            InputProbeSucceeded: true,
            IsTextInputActive: false,
            HeldGameplayKeyEligible: true,
            ResolvedActionId: SmartKardiaRules.ActionId,
            ActionLocallyReady: true,
            CompleteExactPartyView: true,
            Candidates: candidates);
    }

    private static SmartKardiaCandidate[] Candidates() =>
    [
        Candidate(1, pressure: 0, isSelf: true, actor: LocalPlayer),
        Candidate(2, pressure: 0),
        Candidate(3, pressure: 0),
        Candidate(4, pressure: 0),
        Candidate(5, pressure: 0),
    ];

    private static SmartKardiaCandidate Candidate(
        int partySlot,
        int pressure,
        bool isSelf = false,
        TargetPressureActorIdentity? actor = null) =>
        new(
            partySlot,
            actor ?? new TargetPressureActorIdentity(
                (ulong)(20_000 + partySlot),
                (uint)(2_000 + partySlot)),
            ExactPartyIdentity: true,
            isSelf,
            Alive: true,
            Targetable: true,
            CurrentHp: (uint)(40 + partySlot),
            MaximumHp: 100,
            NativeTargetValid: true,
            NativeRangeAndLineOfSight: true,
            PressureKnown: true,
            pressure,
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
        bool isLocalPlayerAlive = true,
        bool metadataVerified = true,
        bool guardSuppressed = false,
        uint resolvedActionId = SmartKardiaRules.ActionId,
        bool actionLocallyReady = true) =>
        SmartKardiaRules.CanUseFrozenIntent(
            intent,
            candidate,
            configurationEnabled,
            isCrystallineConflict,
            currentLocalJobId,
            currentLocalPlayer ?? LocalPlayer,
            isLocalPlayerAlive,
            metadataVerified,
            guardSuppressed,
            resolvedActionId,
            actionLocallyReady);

    private static void Dispatch(
        SmartKardiaObservation observation,
        string label)
    {
        var decision = SmartKardiaRules.Observe(observation);
        True(decision.ShouldDispatch, label);
        True(decision.ShouldConsumeInputGeneration, $"{label} consumes input");
        Equal(SmartKardiaDecisionKind.Dispatch, decision.Kind, label);
        Equal(SmartKardiaDecisionReason.None, decision.Reason, label);
    }

    private static void Cancel(
        SmartKardiaObservation observation,
        SmartKardiaDecisionReason reason)
    {
        var decision = SmartKardiaRules.Observe(observation);
        False(decision.ShouldDispatch, reason.ToString());
        False(decision.ShouldConsumeInputGeneration, $"{reason} input");
        Equal(SmartKardiaDecisionKind.Cancelled, decision.Kind, reason.ToString());
        Equal(reason, decision.Reason, reason.ToString());
        Equal(-1, decision.SelectedCandidateIndex, $"{reason} selected index");
        True(decision.Intent is null, $"{reason} intent");
    }

    private static void None(
        SmartKardiaDecision decision,
        SmartKardiaDecisionReason reason,
        int selectedIndex = -1)
    {
        False(decision.ShouldDispatch, reason.ToString());
        Equal(SmartKardiaDecisionKind.None, decision.Kind, reason.ToString());
        Equal(reason, decision.Reason, reason.ToString());
        Equal(selectedIndex, decision.SelectedCandidateIndex, $"{reason} selected index");
        True(decision.Intent is null, $"{reason} intent");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
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
