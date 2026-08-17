using SeitonSense.Core;

internal static class SmartWardensPaeanTargetSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer =
        new(10_001, 1_001);

    internal static void EligibilityRequiresKnownPressureAndNativeReachability()
    {
        var valid = Candidate(2, pressure: 3);

        True(
            SmartWardensPaeanTargetRules.IsEligibleCandidate(valid, LocalPlayer),
            "known pressure at three");
        True(
            SmartWardensPaeanTargetRules.IsWardensPaeanWardStatus(3_143),
            "PvP Warden's Paean ward status");
        False(
            SmartWardensPaeanTargetRules.IsWardensPaeanWardStatus(2_178),
            "Grace is not the PvP ward");
        False(IsEligible(valid with { UniqueIncomingEnemyCount = 2 }), "strict pressure threshold");
        False(IsEligible(valid with { PressureKnown = false }), "unknown pressure");
        False(IsEligible(valid with { IsSelf = true }), "self flag");
        False(IsEligible(valid with { Actor = LocalPlayer }), "exact self identity");
        False(IsEligible(valid with { ExactPartyIdentity = false }), "inexact party identity");
        False(IsEligible(valid with { PartySlot = 0 }), "invalid P-slot");
        False(IsEligible(valid with { Alive = false }), "dead ally");
        False(IsEligible(valid with { Targetable = false }), "untargetable ally");
        False(
            IsEligible(valid with { HasWardensPaeanWard = true }),
            "existing live PvP ward");
        False(IsEligible(valid with { CurrentHp = 0 }), "zero HP");
        False(IsEligible(valid with { MaximumHp = 0 }), "zero maximum HP");
        False(IsEligible(valid with { CurrentHp = 101 }), "HP exceeds maximum");
        False(IsEligible(valid with { NativeTargetValid = false }), "native target invalid");
        False(
            IsEligible(valid with { NativeRangeAndLineOfSight = false }),
            "native 30y or line of sight failed");
    }

    internal static void CompletePartyViewRejectsIdentityAmbiguity()
    {
        var complete = Candidates();
        True(
            SmartWardensPaeanTargetRules.HasCompleteExactPartyView(
                complete,
                LocalPlayer),
            "exact five-member view");
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

    internal static void RankingIsPressureThenExactHpThenStableSlot()
    {
        var candidates = Candidates();
        candidates[1] = candidates[1] with
        {
            PressureKnown = true,
            UniqueIncomingEnemyCount = 3,
            CurrentHp = 10,
        };
        candidates[2] = candidates[2] with
        {
            PressureKnown = true,
            UniqueIncomingEnemyCount = 4,
            CurrentHp = 90,
        };
        Equal(
            2,
            SmartWardensPaeanTargetRules.SelectBestCandidateIndex(
                candidates,
                LocalPlayer),
            "higher unique pressure wins before HP");

        candidates[1] = candidates[1] with
        {
            UniqueIncomingEnemyCount = 4,
            CurrentHp = 1,
            MaximumHp = 3,
        };
        candidates[2] = candidates[2] with
        {
            CurrentHp = 33,
            MaximumHp = 100,
        };
        Equal(
            2,
            SmartWardensPaeanTargetRules.SelectBestCandidateIndex(
                candidates,
                LocalPlayer),
            "33/100 is exactly lower than 1/3");

        candidates[1] = candidates[1] with
        {
            CurrentHp = 1,
            MaximumHp = 4,
        };
        candidates[2] = candidates[2] with
        {
            CurrentHp = 25,
            MaximumHp = 100,
        };
        var shuffled = new[]
        {
            candidates[4],
            candidates[2],
            candidates[0],
            candidates[3],
            candidates[1],
        };
        Equal(
            4,
            SmartWardensPaeanTargetRules.SelectBestCandidateIndex(
                shuffled,
                LocalPlayer),
            "equal ratios use lower stable P-slot independent of input order");
    }

    internal static void UnknownOrMissingPressurePreservesVanillaCall()
    {
        var valid = Observation();
        Redirect(valid, "known pressure target");

        Vanilla(
            valid with { ConfigurationEnabled = false },
            SmartWardensPaeanDecisionReason.ConfigurationDisabled);
        Vanilla(
            valid with { IsCrystallineConflict = false },
            SmartWardensPaeanDecisionReason.OutsideCrystallineConflict);
        Vanilla(
            valid with { LocalPlayer = default },
            SmartWardensPaeanDecisionReason.LocalPlayerIdentityInvalid);
        Vanilla(
            valid with { IsLocalPlayerAlive = false },
            SmartWardensPaeanDecisionReason.LocalPlayerDead);
        Vanilla(
            valid with { LocalJobId = 24 },
            SmartWardensPaeanDecisionReason.LocalJobInvalid);
        Vanilla(
            valid with { MetadataVerified = false },
            SmartWardensPaeanDecisionReason.MetadataUnverified);
        Vanilla(
            valid with { ResolvedActionId = 0 },
            SmartWardensPaeanDecisionReason.ResolvedActionInvalid);
        Vanilla(
            valid with { CompleteExactPartyView = false },
            SmartWardensPaeanDecisionReason.IncompleteExactPartyView);
        Vanilla(
            valid with { Candidates = valid.Candidates!.Take(4).ToArray() },
            SmartWardensPaeanDecisionReason.IncompleteExactPartyView);

        var belowThreshold = Candidates();
        belowThreshold[1] = belowThreshold[1] with
        {
            PressureKnown = true,
            UniqueIncomingEnemyCount = 2,
        };
        Vanilla(
            valid with { Candidates = belowThreshold },
            SmartWardensPaeanDecisionReason.NoKnownPressureTarget);

        var unknown = Candidates();
        unknown[1] = unknown[1] with
        {
            PressureKnown = false,
            UniqueIncomingEnemyCount = 99,
        };
        Vanilla(
            valid with { Candidates = unknown },
            SmartWardensPaeanDecisionReason.NoKnownPressureTarget);

        unknown[2] = unknown[2] with
        {
            PressureKnown = true,
            UniqueIncomingEnemyCount = 3,
        };
        var oneKnown = SmartWardensPaeanTargetRules.Observe(
            valid with { Candidates = unknown });
        True(oneKnown.ShouldRedirect, "one exact known target remains usable");
        Equal(2, oneKnown.SelectedCandidateIndex, "unknown actors receive no synthetic pressure");
    }

    internal static void FrozenIntentCannotRerankFallbackOrRetry()
    {
        var candidates = Candidates();
        candidates[1] = candidates[1] with
        {
            PressureKnown = true,
            UniqueIncomingEnemyCount = 3,
            CurrentHp = 10,
        };
        candidates[2] = candidates[2] with
        {
            PressureKnown = true,
            UniqueIncomingEnemyCount = 5,
            CurrentHp = 90,
        };
        var decision = SmartWardensPaeanTargetRules.Observe(
            Observation() with { Candidates = candidates });
        True(decision.ShouldRedirect, "redirect freezes one intent");
        Equal(2, decision.SelectedCandidateIndex, "highest pressure actor selected");

        var intent = decision.Intent ??
            throw new InvalidOperationException("missing frozen intent");
        Equal(candidates[2].PartySlot, intent.PartySlot, "frozen P-slot");
        Equal(candidates[2].Actor, intent.Target, "frozen exact actor");
        Equal(5, intent.SelectedIncomingEnemyCount, "frozen pressure diagnostics");

        var current = candidates[2] with
        {
            UniqueIncomingEnemyCount = 3,
            CurrentHp = 15,
        };
        True(CanUse(intent, current), "same actor remains eligible at threshold");
        False(
            CanUse(intent, candidates[1] with { UniqueIncomingEnemyCount = 8 }),
            "a now-better alternate cannot replace the frozen actor");
        False(CanUse(intent, current with { PartySlot = 4 }), "P-slot drift");
        False(
            CanUse(
                intent,
                current with
                {
                    Actor = new TargetPressureActorIdentity(90_000, 9_000),
                }),
            "actor drift");
        False(CanUse(intent, current with { PressureKnown = false }), "pressure became unknown");
        False(CanUse(intent, current with { UniqueIncomingEnemyCount = 2 }), "pressure fell below threshold");
        False(CanUse(intent, current with { NativeTargetValid = false }), "native target drift");
        False(
            CanUse(intent, current with { NativeRangeAndLineOfSight = false }),
            "range or line-of-sight drift");
        False(CanUse(intent, current with { Alive = false }), "target died");
        False(CanUse(intent, current with { Targetable = false }), "target became untargetable");
        False(
            CanUse(intent, current with { HasWardensPaeanWard = true }),
            "PvP ward appeared before dispatch");

        False(CanUse(intent, current, configurationEnabled: false), "config disabled");
        False(CanUse(intent, current, isCrystallineConflict: false), "context drift");
        False(CanUse(intent, current, currentLocalJobId: 24), "job drift");
        False(
            CanUse(
                intent,
                current,
                currentLocalPlayer: new TargetPressureActorIdentity(90_001, 9_001)),
            "local identity drift");
        False(CanUse(intent, current, isLocalPlayerAlive: false), "local death");
        False(CanUse(intent, current, metadataVerified: false), "metadata drift");
        False(CanUse(intent, current, resolvedActionId: 0), "action drift");
    }

    private static SmartWardensPaeanObservation Observation()
    {
        var candidates = Candidates();
        candidates[1] = candidates[1] with
        {
            PressureKnown = true,
            UniqueIncomingEnemyCount = 3,
        };
        return new SmartWardensPaeanObservation(
            ConfigurationEnabled: true,
            IsCrystallineConflict: true,
            LocalJobId: SmartWardensPaeanTargetRules.BardJobId,
            LocalPlayer,
            IsLocalPlayerAlive: true,
            MetadataVerified: true,
            ResolvedActionId: SmartWardensPaeanTargetRules.ActionId,
            CompleteExactPartyView: true,
            Candidates: candidates);
    }

    private static SmartWardensPaeanCandidate[] Candidates() =>
    [
        Candidate(1, pressure: 0, isSelf: true, actor: LocalPlayer),
        Candidate(2, pressure: 0),
        Candidate(3, pressure: 0),
        Candidate(4, pressure: 0),
        Candidate(5, pressure: 0),
    ];

    private static SmartWardensPaeanCandidate Candidate(
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
            HasWardensPaeanWard: false,
            CurrentHp: (uint)(40 + partySlot),
            MaximumHp: 100,
            NativeTargetValid: true,
            NativeRangeAndLineOfSight: true,
            PressureKnown: true,
            pressure);

    private static bool IsEligible(SmartWardensPaeanCandidate candidate) =>
        SmartWardensPaeanTargetRules.IsEligibleCandidate(
            candidate,
            LocalPlayer);

    private static bool HasComplete(
        IReadOnlyList<SmartWardensPaeanCandidate>? candidates) =>
        SmartWardensPaeanTargetRules.HasCompleteExactPartyView(
            candidates,
            LocalPlayer);

    private static bool CanUse(
        SmartWardensPaeanIntent intent,
        SmartWardensPaeanCandidate candidate,
        bool configurationEnabled = true,
        bool isCrystallineConflict = true,
        uint currentLocalJobId = SmartWardensPaeanTargetRules.BardJobId,
        TargetPressureActorIdentity? currentLocalPlayer = null,
        bool isLocalPlayerAlive = true,
        bool metadataVerified = true,
        uint resolvedActionId = SmartWardensPaeanTargetRules.ActionId) =>
        SmartWardensPaeanTargetRules.CanUseFrozenIntent(
            intent,
            candidate,
            configurationEnabled,
            isCrystallineConflict,
            currentLocalJobId,
            currentLocalPlayer ?? LocalPlayer,
            isLocalPlayerAlive,
            metadataVerified,
            resolvedActionId);

    private static void Redirect(
        SmartWardensPaeanObservation observation,
        string label)
    {
        var decision = SmartWardensPaeanTargetRules.Observe(observation);
        True(decision.ShouldRedirect, label);
        Equal(SmartWardensPaeanDecisionKind.Redirect, decision.Kind, label);
        Equal(SmartWardensPaeanDecisionReason.None, decision.Reason, label);
    }

    private static void Vanilla(
        SmartWardensPaeanObservation observation,
        SmartWardensPaeanDecisionReason reason)
    {
        var decision = SmartWardensPaeanTargetRules.Observe(observation);
        False(decision.ShouldRedirect, reason.ToString());
        Equal(SmartWardensPaeanDecisionKind.Vanilla, decision.Kind, reason.ToString());
        Equal(reason, decision.Reason, reason.ToString());
        Equal(-1, decision.SelectedCandidateIndex, $"{reason} selected index");
        True(decision.Intent is null, $"{reason} preserves original call");
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
            throw new InvalidOperationException(
                $"{label}: expected {expected}, got {actual}");
    }
}
