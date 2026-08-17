namespace SeitonSense.Core;

public enum DarkKnightGcdObservationOutcome
{
    Unknown,
    Reset,
    Primed,
    Unchanged,
    OpenedCycle,
}

public readonly record struct DarkKnightGcdObservation(
    bool HardReset,
    bool Known,
    int RecastGroupIndex,
    bool IsActive,
    uint ActionId,
    float ElapsedSeconds,
    float TotalSeconds,
    int AdjustedRecastMilliseconds,
    ushort LastUsedActionSequence);

public readonly record struct DarkKnightGcdCycleState(
    bool HasPreviousKnownObservation,
    bool PreviousActive,
    uint PreviousActionId,
    float PreviousElapsedSeconds,
    float PreviousTotalSeconds,
    ushort PreviousLastUsedActionSequence,
    ulong CurrentCycleToken,
    ulong SpentCycleToken)
{
    public static DarkKnightGcdCycleState Initial => new(
        HasPreviousKnownObservation: false,
        PreviousActive: false,
        PreviousActionId: 0,
        PreviousElapsedSeconds: 0f,
        PreviousTotalSeconds: 0f,
        PreviousLastUsedActionSequence: 0,
        CurrentCycleToken: 0,
        SpentCycleToken: 0);

    public bool HasProvenCycle => CurrentCycleToken != 0;

    public bool CurrentCycleSpent =>
        HasProvenCycle && SpentCycleToken == CurrentCycleToken;
}

public readonly record struct DarkKnightGcdObservationResult(
    DarkKnightGcdCycleState State,
    DarkKnightGcdObservationOutcome Outcome);

public readonly record struct DarkKnightShadowbringerMacroArm(
    int MacroLine,
    string MacroName,
    long ExpiresAtMilliseconds,
    uint TerritoryId,
    ulong LocalGameObjectId,
    uint LocalEntityId,
    nint LocalAddress,
    ulong CycleToken);

public readonly record struct DarkKnightShadowbringerPairObservation(
    long NowMilliseconds,
    bool MacroLocked,
    int MacroLine,
    string MacroName,
    uint TerritoryId,
    ulong LocalGameObjectId,
    uint LocalEntityId,
    nint LocalAddress,
    ulong CycleToken,
    uint ActionType,
    uint RawActionId,
    uint AdjustedActionId,
    uint UseActionMode,
    uint ComboRouteId,
    uint ExtraParam);

public enum DarkKnightShadowbringerPairDecision
{
    Paired,
    Expired,
    MacroInactive,
    MacroIdentityChanged,
    NotImmediatelyFollowingLine,
    ContextOrPlayerChanged,
    CycleChanged,
    UnsupportedActionType,
    UnsupportedInvocationMode,
    WrongComboRoute,
    UnsupportedCarrier,
    UnexpectedExtraParam,
}

public readonly record struct DarkKnightShadowbringerPairResult(
    bool IsPaired,
    DarkKnightShadowbringerPairDecision Decision);

public readonly record struct DarkKnightShadowbringerAttemptObservation(
    bool PluginEnabled,
    bool FeatureEnabled,
    bool MetadataVerified,
    bool ExactSupportedContext,
    bool LocalIdentityStable,
    bool LocalAliveAndTargetable,
    bool LocalIsDarkKnight,
    bool SafeCarrierPath,
    bool ExactCycleSnapshot,
    bool CycleActive,
    ulong ExpectedCycleToken,
    ulong CurrentCycleToken,
    ulong SpentCycleToken,
    bool CycleOwnedByThisAttempt,
    float RemainingGcdSeconds,
    bool NativeQueueClearAndStable,
    bool ActionSequenceStable,
    bool AnimationLockClear,
    bool NotCasting,
    bool OwnGuardClear,
    bool TargetIdentityStable,
    bool TargetAliveAndTargetable,
    bool TargetGuardClear,
    bool ComboHasNativeRangeAndLineOfSight,
    bool ShadowbringerHasNativeRangeAndLineOfSight,
    bool ComboStructurallyReady,
    uint ShadowbringerAdjustedActionId,
    bool HasDarkArts,
    uint CurrentHp,
    bool ShadowbringerCooldownReady,
    bool ShadowbringerActionReady,
    bool ShadowbringerResourcesReady);

public enum DarkKnightShadowbringerAttemptDecision
{
    Ready,
    Disabled,
    MetadataMismatch,
    InvalidContext,
    InvalidLocalPlayer,
    UnsafeCarrierPath,
    CycleUnknownOrChanged,
    CycleAlreadySpent,
    OutsideNoClipWindow,
    NativeQueueOwned,
    ActionSequenceChanged,
    AnimationLocked,
    Casting,
    OwnGuardActiveOrPropagating,
    InvalidTarget,
    TargetGuardActive,
    ComboOutOfRangeOrLineOfSight,
    ShadowbringerOutOfRangeOrLineOfSight,
    ComboStructurallyUnavailable,
    InvalidShadowbringerResourceState,
    ShadowbringerUnavailable,
}

public readonly record struct DarkKnightShadowbringerAttemptResult(
    bool ShouldAttempt,
    DarkKnightShadowbringerAttemptDecision Decision);

public static class DarkKnightShadowbringerMacroRules
{
    public const uint DarkKnightJobId = 32;
    public const uint DarkKnightClassJobCategoryId = 98;

    public const uint HardSlashActionId = 29085;
    public const uint SyphonStrikeActionId = 29086;
    public const uint SouleaterActionId = 29087;
    public const uint ScarletDeliriumActionId = 41434;
    public const uint ComeuppanceActionId = 41435;
    public const uint TorcleaverActionId = 41436;
    public const uint SouleaterComboRouteId = 52;

    public const uint ShadowbringerActionId = 29091;
    public const uint DarkArtsShadowbringerActionId = 29738;
    public const uint DeliriumStatusId = 3033;
    public const uint DarkArtsStatusId = 3034;
    public const uint ShadowbringerIconId = 9594;
    public const uint DarkArtsStatusIconId = 213107;
    public const uint ShadowbringerHpCost = 12000;
    public const uint WolvesDenStrikingDummyNameId = 541;
    public const byte StandardComboSecondaryCostType = 58;
    public const byte DeliriumComboSecondaryCostType = 147;

    public const int ComboRecastGroupIndex = 57;
    public const int ShadowbringerRecastGroupIndex = 0;
    public const int ComboAdjustedRecastMilliseconds = 2400;
    public const int ShadowbringerAdjustedRecastMilliseconds = 1000;
    public const float ComboTotalToleranceSeconds = 0.01f;
    public const float CycleResetEpsilonSeconds = 0.025f;
    public const float MinimumNoClipRemainingSeconds = 0.6f;
    public const float MaximumNoClipRemainingSeconds = 0.8f;
    public const int MacroTokenLifetimeMilliseconds = 750;

    public static DarkKnightGcdObservationResult ObserveCycle(
        DarkKnightGcdCycleState state,
        DarkKnightGcdObservation observation)
    {
        if (observation.HardReset)
        {
            return new DarkKnightGcdObservationResult(
                DarkKnightGcdCycleState.Initial,
                DarkKnightGcdObservationOutcome.Reset);
        }

        if (!IsExactKnownCycleObservation(observation))
        {
            return new DarkKnightGcdObservationResult(
                state,
                DarkKnightGcdObservationOutcome.Unknown);
        }

        var updated = state with
        {
            HasPreviousKnownObservation = true,
            PreviousActive = observation.IsActive,
            PreviousActionId = observation.ActionId,
            PreviousElapsedSeconds = observation.ElapsedSeconds,
            PreviousTotalSeconds = observation.TotalSeconds,
            PreviousLastUsedActionSequence = observation.LastUsedActionSequence,
        };

        if (!state.HasPreviousKnownObservation)
        {
            return new DarkKnightGcdObservationResult(
                updated,
                DarkKnightGcdObservationOutcome.Primed);
        }

        var recastRestarted = observation.IsActive &&
                              (!state.PreviousActive ||
                               observation.ElapsedSeconds + CycleResetEpsilonSeconds <
                               state.PreviousElapsedSeconds);
        var exactNewActionSequence =
            observation.LastUsedActionSequence != state.PreviousLastUsedActionSequence;
        if (!recastRestarted || !exactNewActionSequence)
        {
            return new DarkKnightGcdObservationResult(
                updated,
                DarkKnightGcdObservationOutcome.Unchanged);
        }

        updated = updated with
        {
            CurrentCycleToken = NextToken(state.CurrentCycleToken),
        };
        return new DarkKnightGcdObservationResult(
            updated,
            DarkKnightGcdObservationOutcome.OpenedCycle);
    }

    public static bool TrySpendCycle(
        DarkKnightGcdCycleState state,
        ulong expectedCycleToken,
        out DarkKnightGcdCycleState spentState)
    {
        spentState = state;
        if (expectedCycleToken == 0 ||
            state.CurrentCycleToken != expectedCycleToken ||
            state.SpentCycleToken == expectedCycleToken)
        {
            return false;
        }

        spentState = state with { SpentCycleToken = expectedCycleToken };
        return true;
    }

    public static DarkKnightShadowbringerPairResult EvaluatePair(
        DarkKnightShadowbringerMacroArm arm,
        DarkKnightShadowbringerPairObservation observation)
    {
        if (observation.NowMilliseconds >= arm.ExpiresAtMilliseconds)
            return Pair(DarkKnightShadowbringerPairDecision.Expired);
        if (!observation.MacroLocked)
            return Pair(DarkKnightShadowbringerPairDecision.MacroInactive);
        if (!string.Equals(observation.MacroName, arm.MacroName, StringComparison.Ordinal))
            return Pair(DarkKnightShadowbringerPairDecision.MacroIdentityChanged);
        // The native field is used as a line cursor by different macro paths;
        // accepting 0..14 keeps both the zero-based and one-based first-line
        // representation exact while still requiring the immediately next line.
        if (arm.MacroLine is < 0 or >= 15 || observation.MacroLine != arm.MacroLine + 1)
            return Pair(DarkKnightShadowbringerPairDecision.NotImmediatelyFollowingLine);
        if (observation.TerritoryId != arm.TerritoryId ||
            observation.LocalGameObjectId != arm.LocalGameObjectId ||
            observation.LocalEntityId != arm.LocalEntityId ||
            observation.LocalAddress != arm.LocalAddress)
        {
            return Pair(DarkKnightShadowbringerPairDecision.ContextOrPlayerChanged);
        }

        if (observation.CycleToken == 0 || observation.CycleToken != arm.CycleToken)
            return Pair(DarkKnightShadowbringerPairDecision.CycleChanged);
        if (observation.ActionType != 1)
            return Pair(DarkKnightShadowbringerPairDecision.UnsupportedActionType);
        if (observation.UseActionMode is not (0 or 100))
            return Pair(DarkKnightShadowbringerPairDecision.UnsupportedInvocationMode);
        if (observation.ComboRouteId != SouleaterComboRouteId)
            return Pair(DarkKnightShadowbringerPairDecision.WrongComboRoute);
        if (!IsComboCarrierAction(observation.RawActionId) ||
            !IsComboCarrierAction(observation.AdjustedActionId))
        {
            return Pair(DarkKnightShadowbringerPairDecision.UnsupportedCarrier);
        }

        if (observation.ExtraParam != 0)
            return Pair(DarkKnightShadowbringerPairDecision.UnexpectedExtraParam);

        return new DarkKnightShadowbringerPairResult(
            IsPaired: true,
            DarkKnightShadowbringerPairDecision.Paired);
    }

    public static DarkKnightShadowbringerAttemptResult EvaluateAttempt(
        DarkKnightShadowbringerAttemptObservation observation)
    {
        if (!observation.PluginEnabled || !observation.FeatureEnabled)
            return Attempt(DarkKnightShadowbringerAttemptDecision.Disabled);
        if (!observation.MetadataVerified)
            return Attempt(DarkKnightShadowbringerAttemptDecision.MetadataMismatch);
        if (!observation.ExactSupportedContext)
            return Attempt(DarkKnightShadowbringerAttemptDecision.InvalidContext);
        if (!observation.LocalIdentityStable ||
            !observation.LocalAliveAndTargetable ||
            !observation.LocalIsDarkKnight)
        {
            return Attempt(DarkKnightShadowbringerAttemptDecision.InvalidLocalPlayer);
        }

        if (!observation.SafeCarrierPath)
            return Attempt(DarkKnightShadowbringerAttemptDecision.UnsafeCarrierPath);
        if (!observation.ExactCycleSnapshot ||
            !observation.CycleActive ||
            observation.ExpectedCycleToken == 0 ||
            observation.CurrentCycleToken != observation.ExpectedCycleToken)
        {
            return Attempt(DarkKnightShadowbringerAttemptDecision.CycleUnknownOrChanged);
        }

        if (observation.SpentCycleToken == observation.ExpectedCycleToken &&
            !observation.CycleOwnedByThisAttempt)
            return Attempt(DarkKnightShadowbringerAttemptDecision.CycleAlreadySpent);
        if (!IsWithinNoClipWeaveWindow(observation.RemainingGcdSeconds))
            return Attempt(DarkKnightShadowbringerAttemptDecision.OutsideNoClipWindow);
        if (!observation.NativeQueueClearAndStable)
            return Attempt(DarkKnightShadowbringerAttemptDecision.NativeQueueOwned);
        if (!observation.ActionSequenceStable)
            return Attempt(DarkKnightShadowbringerAttemptDecision.ActionSequenceChanged);
        if (!observation.AnimationLockClear)
            return Attempt(DarkKnightShadowbringerAttemptDecision.AnimationLocked);
        if (!observation.NotCasting)
            return Attempt(DarkKnightShadowbringerAttemptDecision.Casting);
        if (!observation.OwnGuardClear)
            return Attempt(DarkKnightShadowbringerAttemptDecision.OwnGuardActiveOrPropagating);
        if (!observation.TargetIdentityStable || !observation.TargetAliveAndTargetable)
            return Attempt(DarkKnightShadowbringerAttemptDecision.InvalidTarget);
        if (!observation.TargetGuardClear)
            return Attempt(DarkKnightShadowbringerAttemptDecision.TargetGuardActive);
        if (!observation.ComboHasNativeRangeAndLineOfSight)
            return Attempt(DarkKnightShadowbringerAttemptDecision.ComboOutOfRangeOrLineOfSight);
        if (!observation.ShadowbringerHasNativeRangeAndLineOfSight)
            return Attempt(DarkKnightShadowbringerAttemptDecision.ShadowbringerOutOfRangeOrLineOfSight);
        if (!observation.ComboStructurallyReady)
            return Attempt(DarkKnightShadowbringerAttemptDecision.ComboStructurallyUnavailable);
        if (!IsShadowbringerResourceStateValid(
                observation.ShadowbringerAdjustedActionId,
                observation.HasDarkArts,
                observation.CurrentHp))
        {
            return Attempt(DarkKnightShadowbringerAttemptDecision.InvalidShadowbringerResourceState);
        }

        if (!observation.ShadowbringerCooldownReady ||
            !observation.ShadowbringerActionReady ||
            !observation.ShadowbringerResourcesReady)
        {
            return Attempt(DarkKnightShadowbringerAttemptDecision.ShadowbringerUnavailable);
        }

        return new DarkKnightShadowbringerAttemptResult(
            ShouldAttempt: true,
            DarkKnightShadowbringerAttemptDecision.Ready);
    }

    public static bool IsComboCarrierAction(uint actionId) =>
        actionId is HardSlashActionId or
            SyphonStrikeActionId or
            SouleaterActionId or
            ScarletDeliriumActionId or
            ComeuppanceActionId or
            TorcleaverActionId;

    public static bool CanExecuteInContext(
        SupportedPvPContext context,
        bool wolvesDenTestingEnabled) =>
        context == SupportedPvPContext.CrystallineConflict ||
        (wolvesDenTestingEnabled && context == SupportedPvPContext.WolvesDen);

    public static bool IsExactWolvesDenStrikingDummy(
        bool metadataVerified,
        bool battleNpcCombatant,
        uint nameId,
        bool nativeIdentityValid,
        bool isSelf,
        bool aliveWithPositiveHp,
        bool targetable) =>
        metadataVerified &&
        battleNpcCombatant &&
        nameId == WolvesDenStrikingDummyNameId &&
        nativeIdentityValid &&
        !isSelf &&
        aliveWithPositiveHp &&
        targetable;

    public static bool IsWithinNoClipWeaveWindow(float remainingSeconds) =>
        float.IsFinite(remainingSeconds) &&
        remainingSeconds >= MinimumNoClipRemainingSeconds &&
        remainingSeconds <= MaximumNoClipRemainingSeconds;

    public static bool IsShadowbringerResourceStateValid(
        uint adjustedActionId,
        bool hasDarkArts,
        uint currentHp) =>
        adjustedActionId switch
        {
            DarkArtsShadowbringerActionId => hasDarkArts && currentHp > 0,
            ShadowbringerActionId => !hasDarkArts && currentHp > ShadowbringerHpCost,
            _ => false,
        };

    public static bool IsExactComboTiming(
        int recastGroupIndex,
        float totalSeconds,
        int adjustedRecastMilliseconds) =>
        recastGroupIndex == ComboRecastGroupIndex &&
        float.IsFinite(totalSeconds) &&
        Math.Abs(totalSeconds - ComboAdjustedRecastMilliseconds / 1000f) <=
        ComboTotalToleranceSeconds &&
        adjustedRecastMilliseconds == ComboAdjustedRecastMilliseconds;

    private static bool IsExactKnownCycleObservation(DarkKnightGcdObservation observation) =>
        observation.Known &&
        IsComboCarrierAction(observation.ActionId) &&
        float.IsFinite(observation.ElapsedSeconds) &&
        observation.ElapsedSeconds >= 0f &&
        IsExactComboTiming(
            observation.RecastGroupIndex,
            observation.TotalSeconds,
            observation.AdjustedRecastMilliseconds);

    private static ulong NextToken(ulong current) =>
        current == ulong.MaxValue ? 1UL : current + 1UL;

    private static DarkKnightShadowbringerPairResult Pair(
        DarkKnightShadowbringerPairDecision decision) => new(false, decision);

    private static DarkKnightShadowbringerAttemptResult Attempt(
        DarkKnightShadowbringerAttemptDecision decision) => new(false, decision);
}
