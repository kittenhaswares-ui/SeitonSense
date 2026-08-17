using SeitonSense.Core;

internal static class DarkKnightShadowbringerMacroSelfTests
{
    public static void ExactIdsAndCarrierSetArePinned()
    {
        Equal(52u, DarkKnightShadowbringerMacroRules.SouleaterComboRouteId, "combo route");
        Equal(29091u, DarkKnightShadowbringerMacroRules.ShadowbringerActionId, "base Shadowbringer");
        Equal(29738u, DarkKnightShadowbringerMacroRules.DarkArtsShadowbringerActionId, "Dark Arts Shadowbringer");
        Equal(3033u, DarkKnightShadowbringerMacroRules.DeliriumStatusId, "Delirium status");
        Equal(3034u, DarkKnightShadowbringerMacroRules.DarkArtsStatusId, "Dark Arts status");
        Equal(541u, DarkKnightShadowbringerMacroRules.WolvesDenStrikingDummyNameId, "Wolves' Den striking dummy NameId");
        Equal((byte)58, DarkKnightShadowbringerMacroRules.StandardComboSecondaryCostType, "standard combo secondary cost type");
        Equal((byte)147, DarkKnightShadowbringerMacroRules.DeliriumComboSecondaryCostType, "Delirium combo secondary cost type");

        foreach (var actionId in new uint[] { 29085, 29086, 29087, 41434, 41435, 41436 })
            True(DarkKnightShadowbringerMacroRules.IsComboCarrierAction(actionId), $"carrier {actionId}");
        False(DarkKnightShadowbringerMacroRules.IsComboCarrierAction(29091), "Shadowbringer is not a carrier");
    }

    public static void CycleRequiresAProvenExactReset()
    {
        var first = Observe(DarkKnightGcdCycleState.Initial, elapsed: 1.2f, sequence: 10);
        Equal(DarkKnightGcdObservationOutcome.Primed, first.Outcome, "mid-cycle first observation only primes");
        Equal(0UL, first.State.CurrentCycleToken, "mid-cycle load does not mint");

        var later = Observe(first.State, elapsed: 2.0f, sequence: 10);
        Equal(DarkKnightGcdObservationOutcome.Unchanged, later.Outcome, "same recast stays in one cycle");
        Equal(0UL, later.State.CurrentCycleToken, "same recast remains unowned");

        var reset = Observe(later.State, elapsed: 0.02f, sequence: 11);
        Equal(DarkKnightGcdObservationOutcome.OpenedCycle, reset.Outcome, "exact recast reset opens cycle");
        Equal(1UL, reset.State.CurrentCycleToken, "first proven token");

        var noSequence = Observe(reset.State, elapsed: 0.01f, sequence: 11);
        Equal(DarkKnightGcdObservationOutcome.Unchanged, noSequence.Outcome, "elapsed jitter without sequence cannot rearm");
        Equal(1UL, noSequence.State.CurrentCycleToken, "jitter preserves token");
    }

    public static void UnknownPreservesSpentOwnershipAndNextResetRearms()
    {
        var primed = Observe(DarkKnightGcdCycleState.Initial, elapsed: 2.0f, sequence: 20).State;
        var opened = Observe(primed, elapsed: 0.01f, sequence: 21).State;
        True(DarkKnightShadowbringerMacroRules.TrySpendCycle(opened, 1, out var spent), "first cycle can be spent");

        var unknown = DarkKnightShadowbringerMacroRules.ObserveCycle(
            spent,
            new DarkKnightGcdObservation(
                HardReset: false,
                Known: false,
                RecastGroupIndex: -1,
                IsActive: false,
                ActionId: 0,
                ElapsedSeconds: 0,
                TotalSeconds: 0,
                AdjustedRecastMilliseconds: 0,
                LastUsedActionSequence: 0));
        Equal(1UL, unknown.State.CurrentCycleToken, "unknown preserves cycle token");
        Equal(1UL, unknown.State.SpentCycleToken, "unknown preserves spent owner");
        False(DarkKnightShadowbringerMacroRules.TrySpendCycle(unknown.State, 1, out _), "unknown cannot retry spent cycle");

        var sameCycle = Observe(unknown.State, elapsed: 1.0f, sequence: 22).State;
        Equal(1UL, sameCycle.CurrentCycleToken, "oGCD sequence change without reset is same cycle");
        var next = Observe(sameCycle, elapsed: 0.01f, sequence: 23).State;
        Equal(2UL, next.CurrentCycleToken, "next proven GCD opens next token");
        True(DarkKnightShadowbringerMacroRules.TrySpendCycle(next, 2, out _), "new cycle permits one attempt");
    }

    public static void CycleTokenWrapsToOne()
    {
        var state = new DarkKnightGcdCycleState(
            HasPreviousKnownObservation: true,
            PreviousActive: true,
            PreviousActionId: DarkKnightShadowbringerMacroRules.HardSlashActionId,
            PreviousElapsedSeconds: 2f,
            PreviousTotalSeconds: 2.4f,
            PreviousLastUsedActionSequence: 50,
            CurrentCycleToken: ulong.MaxValue,
            SpentCycleToken: ulong.MaxValue);
        var reset = Observe(state, elapsed: 0.01f, sequence: 51);
        Equal(1UL, reset.State.CurrentCycleToken, "overflow wraps to nonzero token");
        True(DarkKnightShadowbringerMacroRules.TrySpendCycle(reset.State, 1, out _), "wrapped token is usable once");
    }

    public static void PairingRequiresImmediateQueueableSouleaterLine()
    {
        var arm = new DarkKnightShadowbringerMacroArm(
            MacroLine: 1,
            MacroName: "DRK",
            ExpiresAtMilliseconds: 1_750,
            TerritoryId: 1032,
            LocalGameObjectId: 100,
            LocalEntityId: 200,
            LocalAddress: (nint)300,
            CycleToken: 7);
        var valid = new DarkKnightShadowbringerPairObservation(
            NowMilliseconds: 1_001,
            MacroLocked: true,
            MacroLine: 2,
            MacroName: "DRK",
            TerritoryId: 1032,
            LocalGameObjectId: 100,
            LocalEntityId: 200,
            LocalAddress: (nint)300,
            CycleToken: 7,
            ActionType: 1,
            RawActionId: DarkKnightShadowbringerMacroRules.HardSlashActionId,
            AdjustedActionId: DarkKnightShadowbringerMacroRules.ScarletDeliriumActionId,
            UseActionMode: 100,
            ComboRouteId: DarkKnightShadowbringerMacroRules.SouleaterComboRouteId,
            ExtraParam: 0);

        True(DarkKnightShadowbringerMacroRules.EvaluatePair(arm, valid).IsPaired, "raw ReAction macro mode pairs");
        True(DarkKnightShadowbringerMacroRules.EvaluatePair(arm, valid with { UseActionMode = 0 }).IsPaired, "ReAction-converted None pairs");
        True(DarkKnightShadowbringerMacroRules.EvaluatePair(
            arm with { MacroLine = 0 },
            valid with { MacroLine = 1 }).IsPaired, "zero-based native first line pairs");
        False(DarkKnightShadowbringerMacroRules.EvaluatePair(arm, valid with { UseActionMode = 2 }).IsPaired, "native nonqueueable Macro mode rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluatePair(arm, valid with { UseActionMode = 1 }).IsPaired, "queued drain rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluatePair(arm, valid with { MacroLine = 3 }).IsPaired, "later macro line rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluatePair(arm, valid with { ComboRouteId = 0 }).IsPaired, "individual action rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluatePair(arm, valid with { AdjustedActionId = 29091 }).IsPaired, "unrelated adjusted action rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluatePair(arm, valid with { NowMilliseconds = 1_750 }).IsPaired, "expiry boundary rejects");
    }

    public static void NoClipWindowIsInclusiveAndNeverLate()
    {
        False(DarkKnightShadowbringerMacroRules.IsWithinNoClipWeaveWindow(0.5f), "500 ms is deliberately too late");
        False(DarkKnightShadowbringerMacroRules.IsWithinNoClipWeaveWindow(0.599f), "below lower boundary rejects");
        True(DarkKnightShadowbringerMacroRules.IsWithinNoClipWeaveWindow(0.6f), "600 ms boundary accepts");
        True(DarkKnightShadowbringerMacroRules.IsWithinNoClipWeaveWindow(0.8f), "800 ms boundary accepts");
        False(DarkKnightShadowbringerMacroRules.IsWithinNoClipWeaveWindow(0.801f), "above upper boundary rejects");
        False(DarkKnightShadowbringerMacroRules.IsWithinNoClipWeaveWindow(float.NaN), "invalid timer rejects");
    }

    public static void HpAndDarkArtsGateIsExact()
    {
        False(DarkKnightShadowbringerMacroRules.IsShadowbringerResourceStateValid(29091, false, 12000), "exact HP cost rejects");
        True(DarkKnightShadowbringerMacroRules.IsShadowbringerResourceStateValid(29091, false, 12001), "strictly above HP cost accepts");
        False(DarkKnightShadowbringerMacroRules.IsShadowbringerResourceStateValid(29091, true, 50000), "Dark Arts/base mismatch rejects");
        True(DarkKnightShadowbringerMacroRules.IsShadowbringerResourceStateValid(29738, true, 1), "Dark Arts adjusted row accepts positive HP");
        False(DarkKnightShadowbringerMacroRules.IsShadowbringerResourceStateValid(29738, false, 50000), "adjusted row without status rejects");
    }

    public static void AttemptRequiresEverySafetyGate()
    {
        True(
            DarkKnightShadowbringerMacroRules.CanExecuteInContext(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: false),
            "exact CC remains supported independently of Den testing");
        False(
            DarkKnightShadowbringerMacroRules.CanExecuteInContext(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: false),
            "Den requires the existing global test opt-in");
        True(
            DarkKnightShadowbringerMacroRules.CanExecuteInContext(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true),
            "Den is supported only under the existing global test opt-in");
        False(
            DarkKnightShadowbringerMacroRules.CanExecuteInContext(
                SupportedPvPContext.None,
                wolvesDenTestingEnabled: true),
            "unrelated PvP contexts remain rejected");
        True(
            DarkKnightShadowbringerMacroRules.IsExactWolvesDenStrikingDummy(
                metadataVerified: true,
                battleNpcCombatant: true,
                nameId: 541,
                nativeIdentityValid: true,
                isSelf: false,
                aliveWithPositiveHp: true,
                targetable: true),
            "exact Den striking dummy is eligible");
        False(
            DarkKnightShadowbringerMacroRules.IsExactWolvesDenStrikingDummy(
                metadataVerified: true,
                battleNpcCombatant: true,
                nameId: 13078,
                nativeIdentityValid: true,
                isSelf: false,
                aliveWithPositiveHp: true,
                targetable: true),
            "timeworn dummy is not the exact Den test target");
        False(
            DarkKnightShadowbringerMacroRules.IsExactWolvesDenStrikingDummy(
                metadataVerified: true,
                battleNpcCombatant: false,
                nameId: 541,
                nativeIdentityValid: true,
                isSelf: false,
                aliveWithPositiveHp: true,
                targetable: true),
            "players and arbitrary targetable objects remain rejected");
        False(
            DarkKnightShadowbringerMacroRules.IsExactWolvesDenStrikingDummy(
                metadataVerified: false,
                battleNpcCombatant: true,
                nameId: 541,
                nativeIdentityValid: true,
                isSelf: false,
                aliveWithPositiveHp: true,
                targetable: true),
            "unverified Den dummy metadata fails closed without changing CC eligibility");
        False(
            DarkKnightShadowbringerMacroRules.IsExactWolvesDenStrikingDummy(
                metadataVerified: true,
                battleNpcCombatant: true,
                nameId: 541,
                nativeIdentityValid: true,
                isSelf: true,
                aliveWithPositiveHp: true,
                targetable: true),
            "self can never become the Den dummy target");

        var valid = ValidAttempt();
        True(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid).ShouldAttempt, "all exact gates permit ownership");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { ExactSupportedContext = false }).ShouldAttempt, "unsupported context rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { RemainingGcdSeconds = 0.5f }).ShouldAttempt, "late pulse skips");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { CycleActive = false }).ShouldAttempt, "inactive GCD skips");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { SpentCycleToken = 9 }).ShouldAttempt, "spent cycle rejects");
        True(DarkKnightShadowbringerMacroRules.EvaluateAttempt(
            valid with { SpentCycleToken = 9, CycleOwnedByThisAttempt = true }).ShouldAttempt,
            "the already consumed final-boundary owner remains eligible");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { NativeQueueClearAndStable = false }).ShouldAttempt, "owned queue rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { AnimationLockClear = false }).ShouldAttempt, "animation lock rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { OwnGuardClear = false }).ShouldAttempt, "own Guard rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { TargetGuardClear = false }).ShouldAttempt, "target Guard rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { ComboHasNativeRangeAndLineOfSight = false }).ShouldAttempt, "combo range rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { ShadowbringerHasNativeRangeAndLineOfSight = false }).ShouldAttempt, "Shadowbringer range rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { CurrentHp = 12000 }).ShouldAttempt, "HP equality rejects");
        False(DarkKnightShadowbringerMacroRules.EvaluateAttempt(valid with { ShadowbringerActionReady = false }).ShouldAttempt, "native readiness rejects");
    }

    private static DarkKnightGcdObservationResult Observe(
        DarkKnightGcdCycleState state,
        float elapsed,
        ushort sequence) =>
        DarkKnightShadowbringerMacroRules.ObserveCycle(
            state,
            new DarkKnightGcdObservation(
                HardReset: false,
                Known: true,
                RecastGroupIndex: DarkKnightShadowbringerMacroRules.ComboRecastGroupIndex,
                IsActive: true,
                ActionId: DarkKnightShadowbringerMacroRules.HardSlashActionId,
                ElapsedSeconds: elapsed,
                TotalSeconds: 2.4f,
                AdjustedRecastMilliseconds: 2400,
                LastUsedActionSequence: sequence));

    private static DarkKnightShadowbringerAttemptObservation ValidAttempt() => new(
        PluginEnabled: true,
        FeatureEnabled: true,
        MetadataVerified: true,
        ExactSupportedContext: true,
        LocalIdentityStable: true,
        LocalAliveAndTargetable: true,
        LocalIsDarkKnight: true,
        SafeCarrierPath: true,
        ExactCycleSnapshot: true,
        CycleActive: true,
        ExpectedCycleToken: 9,
        CurrentCycleToken: 9,
        SpentCycleToken: 8,
        CycleOwnedByThisAttempt: false,
        RemainingGcdSeconds: 0.7f,
        NativeQueueClearAndStable: true,
        ActionSequenceStable: true,
        AnimationLockClear: true,
        NotCasting: true,
        OwnGuardClear: true,
        TargetIdentityStable: true,
        TargetAliveAndTargetable: true,
        TargetGuardClear: true,
        ComboHasNativeRangeAndLineOfSight: true,
        ShadowbringerHasNativeRangeAndLineOfSight: true,
        ComboStructurallyReady: true,
        ShadowbringerAdjustedActionId: DarkKnightShadowbringerMacroRules.ShadowbringerActionId,
        HasDarkArts: false,
        CurrentHp: 12001,
        ShadowbringerCooldownReady: true,
        ShadowbringerActionReady: true,
        ShadowbringerResourcesReady: true);

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
