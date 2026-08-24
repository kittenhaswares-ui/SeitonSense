namespace SeitonSense.Core;

public readonly record struct EmergencyTeleportSettings(
    int HpPercent,
    uint MpThreshold,
    int MinimumDirectEnemyCount,
    float MinimumTravelYalms,
    float EnemySafetyRadiusYalms,
    int MaximumNearbyEnemyCount)
{
    public static EmergencyTeleportSettings Default { get; } = new(
        EmergencyTeleportRules.DefaultHpPercent,
        EmergencyTeleportRules.DefaultMpThreshold,
        EmergencyTeleportRules.DefaultMinimumDirectEnemyCount,
        EmergencyTeleportRules.DefaultMinimumTravelYalms,
        EmergencyTeleportRules.DefaultEnemySafetyRadiusYalms,
        EmergencyTeleportRules.DefaultMaximumNearbyEnemyCount);

    public bool IsValid => EmergencyTeleportRules.IsValidSettings(this);
}

/// <summary>
/// One exact non-self party member observed for the current job's friendly
/// movement action. Distances are horizontal hitbox-edge distances computed by
/// the runtime from one complete current enemy snapshot.
/// </summary>
public readonly record struct EmergencyTeleportCandidate(
    TargetPressureActorIdentity Actor,
    int PartySlot,
    uint CurrentHp,
    uint MaximumHp,
    float TravelDistanceYalms,
    int NearbyEnemyCount,
    float MinimumEnemyEdgeClearanceYalms,
    bool IsExactPartyMember,
    bool IsSelf,
    bool IsAlive,
    bool IsTargetable,
    bool HasValidNativeTarget,
    bool HasValidActionTarget,
    bool HasNativeRangeAndLineOfSight,
    bool HasCompleteEnemySnapshot);

/// <summary>
/// The action, local player, destination, held key, settings, and danger
/// episode are frozen together. A final failure never substitutes another
/// target inside this intent.
/// </summary>
public readonly record struct EmergencyTeleportIntent(
    uint ActionId,
    uint LocalJobId,
    TargetPressureActorIdentity LocalPlayer,
    SupportedPvPContext Context,
    TargetPressureActorIdentity Target,
    int PartySlot,
    int FrozenKeyCode,
    ulong EpisodeToken,
    EmergencyTeleportSettings Settings)
{
    public bool IsValid =>
        EmergencyTeleportRules.IsExactJobAction(LocalJobId, ActionId) &&
        LocalPlayer.IsValid &&
        Context is SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen &&
        Target.IsValid &&
        Target != LocalPlayer &&
        PartySlot is >= EmergencyTeleportRules.FirstPartySlot and
            <= EmergencyTeleportRules.LastPartySlot &&
        FrozenKeyCode > 0 &&
        EpisodeToken != 0 &&
        Settings.IsValid;
}

public enum EmergencyTeleportDangerSignal : byte
{
    Unknown = 0,
    Safe = 1,
    Danger = 2,
}

public readonly record struct EmergencyTeleportState(
    bool EpisodeOpen,
    bool EpisodeSpent,
    long SafeSinceMilliseconds,
    ulong EpisodeToken,
    EmergencyTeleportIntent? Intent,
    long LastObservedAtMilliseconds,
    ClientActionAttemptOutcome LastNativeOutcome)
{
    public static EmergencyTeleportState Initial { get; } = new(
        EpisodeOpen: false,
        EpisodeSpent: false,
        SafeSinceMilliseconds: -1,
        EpisodeToken: 0,
        Intent: null,
        LastObservedAtMilliseconds: -1,
        LastNativeOutcome: ClientActionAttemptOutcome.None);
}

public readonly record struct EmergencyTeleportObservation(
    bool ConfigurationEnabled,
    EmergencyTeleportSettings Settings,
    SupportedPvPContext Context,
    TargetPressureActorIdentity LocalPlayer,
    bool IsLocalPlayerAlive,
    bool IsLocalPlayerTargetable,
    uint LocalJobId,
    bool MetadataVerified,
    bool ActionHelpersSuppressedByGuard,
    bool HigherPriorityClaimed,
    bool InputProbeSucceeded,
    bool IsTextInputActive,
    bool HeldGameplayKeyEligible,
    int HeldGameplayKeyCode,
    bool FrozenKeyStillDown,
    uint ResolvedActionId,
    bool ActionLocallyReady,
    bool NativeBoundaryReady,
    uint CurrentHp,
    uint MaximumHp,
    uint CurrentMp,
    uint MaximumMp,
    bool DirectPressureKnown,
    int DirectEnemyCount,
    long PressurePublishedAtMilliseconds,
    IReadOnlyList<EmergencyTeleportCandidate>? Candidates,
    long NowMilliseconds,
    bool HardReset = false);

public enum EmergencyTeleportDecisionKind : byte
{
    None = 0,
    Armed = 1,
    Dispatch = 2,
    Cancelled = 3,
    Spent = 4,
}

public enum EmergencyTeleportDecisionReason : byte
{
    None = 0,
    HardReset = 1,
    ClockMovedBackwards = 2,
    ConfigurationDisabled = 3,
    SettingsInvalid = 4,
    OutsideSupportedPvPContext = 5,
    LocalPlayerIdentityInvalid = 6,
    LocalPlayerDead = 7,
    LocalPlayerUntargetable = 8,
    LocalJobUnsupported = 9,
    MetadataUnverified = 10,
    DangerUnknown = 11,
    DangerInactive = 12,
    DangerClearGrace = 13,
    EpisodeSpent = 14,
    InputProbeUnavailable = 15,
    TextInputActive = 16,
    GuardSuppressed = 17,
    HigherPriorityClaimed = 18,
    NoHeldGameplayKey = 19,
    ExactKeyReleased = 20,
    ResolvedActionInvalid = 21,
    ActionNotReady = 22,
    NoExactSafeDestination = 23,
    NativeBoundaryUnavailable = 24,
    FrozenIntentInvalid = 25,
    AttemptCommitted = 26,
}

public readonly record struct EmergencyTeleportDecision(
    EmergencyTeleportState NextState,
    EmergencyTeleportDecisionKind Kind,
    EmergencyTeleportDecisionReason Reason,
    EmergencyTeleportDangerSignal DangerSignal,
    EmergencyTeleportIntent? Intent = null,
    int SelectedCandidateIndex = -1,
    bool InputClaimed = false)
{
    public bool ShouldDispatch =>
        Kind == EmergencyTeleportDecisionKind.Dispatch &&
        Intent is { IsValid: true };
}

/// <summary>
/// The runtime must store NextState before invoking the one native action.
/// Calling CommitNativeAttempt again for the same danger episode cannot expose
/// another action request.
/// </summary>
public readonly record struct EmergencyTeleportAttemptCommit(
    EmergencyTeleportState NextState,
    EmergencyTeleportIntent? Intent,
    bool ShouldInvokeNative,
    EmergencyTeleportDecisionReason Reason);

/// <summary>
/// Pure policy for the held Emergency Teleport helper. The policy opens one
/// danger episode only from strict current HP, MP, and fresh direct hard/cast
/// pressure. It selects one exact party destination deterministically, never
/// supplies a selected-target fallback, and permits at most one native call in
/// that episode. Accepted, rejected, or ambiguous outcomes never retry.
/// </summary>
public static class EmergencyTeleportRules
{
    public const uint MonkJobId = 20;
    public const uint BlackMageJobId = 25;
    public const uint SageJobId = 40;
    public const uint ViperJobId = 41;

    public const uint IcarusActionId = 29_261;
    public const uint ThunderclapActionId = 29_484;
    public const uint AetherialManipulationActionId = 29_660;
    public const uint SlitherActionId = 39_184;

    public const int FirstPartySlot = 1;
    public const int LastPartySlot = 8;
    public const int MaximumCanonicalEnemyCount = 5;

    public const int DefaultHpPercent = 50;
    public const uint DefaultMpThreshold = 4_000;
    public const int DefaultMinimumDirectEnemyCount = 1;
    public const float DefaultMinimumTravelYalms = 10f;
    public const float DefaultEnemySafetyRadiusYalms = 10f;
    public const int DefaultMaximumNearbyEnemyCount = 0;
    public const long MaximumPressureAgeMilliseconds = 250;
    public const long DangerClearGraceMilliseconds = 300;

    public static bool IsValidSettings(EmergencyTeleportSettings settings) =>
        settings.HpPercent is >= 10 and <= 90 &&
        settings.MpThreshold <= 10_000 &&
        settings.MinimumDirectEnemyCount is >= 1 and <= MaximumCanonicalEnemyCount &&
        float.IsFinite(settings.MinimumTravelYalms) &&
        settings.MinimumTravelYalms is >= 3f and <= 25f &&
        float.IsFinite(settings.EnemySafetyRadiusYalms) &&
        settings.EnemySafetyRadiusYalms is >= 3f and <= 20f &&
        settings.MaximumNearbyEnemyCount is >= 0 and <= MaximumCanonicalEnemyCount;

    public static bool TryGetActionForJob(uint jobId, out uint actionId)
    {
        actionId = jobId switch
        {
            MonkJobId => ThunderclapActionId,
            SageJobId => IcarusActionId,
            BlackMageJobId => AetherialManipulationActionId,
            ViperJobId => SlitherActionId,
            _ => 0,
        };
        return actionId != 0;
    }

    public static bool IsExactJobAction(uint jobId, uint actionId) =>
        TryGetActionForJob(jobId, out var expected) && actionId == expected;

    public static bool HasValidHealth(uint currentHp, uint maximumHp) =>
        currentHp > 0 && maximumHp > 0 && currentHp <= maximumHp;

    public static bool HasValidMp(uint currentMp, uint maximumMp) =>
        maximumMp > 0 && currentMp <= maximumMp;

    public static bool IsBelowHpThreshold(
        uint currentHp,
        uint maximumHp,
        int hpPercent) =>
        HasValidHealth(currentHp, maximumHp) &&
        hpPercent is >= 1 and <= 99 &&
        (ulong)currentHp * 100UL < (ulong)maximumHp * (uint)hpPercent;

    public static bool IsBelowMpThreshold(
        uint currentMp,
        uint maximumMp,
        uint mpThreshold) =>
        HasValidMp(currentMp, maximumMp) && currentMp < mpThreshold;

    public static bool IsFreshDirectPressure(
        bool pressureKnown,
        int directEnemyCount,
        long publishedAtMilliseconds,
        long nowMilliseconds) =>
        pressureKnown &&
        directEnemyCount is >= 0 and <= MaximumCanonicalEnemyCount &&
        publishedAtMilliseconds >= 0 &&
        nowMilliseconds >= publishedAtMilliseconds &&
        nowMilliseconds - publishedAtMilliseconds <= MaximumPressureAgeMilliseconds;

    public static EmergencyTeleportDangerSignal ClassifyDanger(
        EmergencyTeleportObservation observation)
    {
        if (!observation.Settings.IsValid ||
            !HasValidHealth(observation.CurrentHp, observation.MaximumHp) ||
            !HasValidMp(observation.CurrentMp, observation.MaximumMp) ||
            !IsFreshDirectPressure(
                observation.DirectPressureKnown,
                observation.DirectEnemyCount,
                observation.PressurePublishedAtMilliseconds,
                observation.NowMilliseconds))
        {
            return EmergencyTeleportDangerSignal.Unknown;
        }

        return IsBelowHpThreshold(
                   observation.CurrentHp,
                   observation.MaximumHp,
                   observation.Settings.HpPercent) &&
               IsBelowMpThreshold(
                   observation.CurrentMp,
                   observation.MaximumMp,
                   observation.Settings.MpThreshold) &&
               observation.DirectEnemyCount >=
               observation.Settings.MinimumDirectEnemyCount
            ? EmergencyTeleportDangerSignal.Danger
            : EmergencyTeleportDangerSignal.Safe;
    }

    public static bool IsEligibleCandidate(
        EmergencyTeleportCandidate candidate,
        EmergencyTeleportSettings settings) =>
        settings.IsValid &&
        candidate.Actor.IsValid &&
        candidate.PartySlot is >= FirstPartySlot and <= LastPartySlot &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp >= candidate.CurrentHp &&
        float.IsFinite(candidate.TravelDistanceYalms) &&
        candidate.TravelDistanceYalms >= settings.MinimumTravelYalms &&
        candidate.NearbyEnemyCount is >= 0 and <= MaximumCanonicalEnemyCount &&
        candidate.NearbyEnemyCount <= settings.MaximumNearbyEnemyCount &&
        float.IsFinite(candidate.MinimumEnemyEdgeClearanceYalms) &&
        candidate.MinimumEnemyEdgeClearanceYalms >= 0f &&
        (candidate.NearbyEnemyCount != 0 ||
         candidate.MinimumEnemyEdgeClearanceYalms > settings.EnemySafetyRadiusYalms) &&
        candidate.IsExactPartyMember &&
        !candidate.IsSelf &&
        candidate.IsAlive &&
        candidate.IsTargetable &&
        candidate.HasValidNativeTarget &&
        candidate.HasValidActionTarget &&
        candidate.HasNativeRangeAndLineOfSight &&
        candidate.HasCompleteEnemySnapshot;

    /// <summary>
    /// Safety dominates travel. The fewest nearby enemies wins first; among an
    /// equal safety tier the farthest jump wins, then greater clearance, then a
    /// stable party/actor identity order. Ambiguous party slots or actor IDs
    /// fail closed instead of selecting from enumeration order.
    /// </summary>
    public static int SelectBestCandidateIndex(
        IReadOnlyList<EmergencyTeleportCandidate>? candidates,
        EmergencyTeleportSettings settings)
    {
        if (!settings.IsValid || candidates is null || candidates.Count == 0)
            return -1;

        var seenSlots = new HashSet<int>();
        var seenGameObjectIds = new HashSet<ulong>();
        var seenEntityIds = new HashSet<uint>();
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!candidate.IsExactPartyMember ||
                !candidate.Actor.IsValid ||
                candidate.PartySlot is < FirstPartySlot or > LastPartySlot)
            {
                continue;
            }

            if (!seenSlots.Add(candidate.PartySlot) ||
                !seenGameObjectIds.Add(candidate.Actor.GameObjectId) ||
                !seenEntityIds.Add(candidate.Actor.EntityId))
            {
                return -1;
            }
        }

        var bestIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleCandidate(candidate, settings)) continue;
            if (bestIndex < 0 || IsBetter(candidate, candidates[bestIndex]))
                bestIndex = index;
        }

        return bestIndex;
    }

    public static EmergencyTeleportDecision Observe(
        EmergencyTeleportState previous,
        EmergencyTeleportObservation observation)
    {
        if (observation.HardReset)
        {
            return Cancelled(
                EmergencyTeleportState.Initial,
                EmergencyTeleportDecisionReason.HardReset,
                EmergencyTeleportDangerSignal.Unknown);
        }

        if (observation.NowMilliseconds < 0 ||
            (previous.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.LastObservedAtMilliseconds) ||
            (previous.SafeSinceMilliseconds >= 0 &&
             observation.NowMilliseconds < previous.SafeSinceMilliseconds))
        {
            return Cancelled(
                EmergencyTeleportState.Initial,
                EmergencyTeleportDecisionReason.ClockMovedBackwards,
                EmergencyTeleportDangerSignal.Unknown);
        }

        var permanentFailure = GetPermanentGateFailure(observation);
        if (permanentFailure != EmergencyTeleportDecisionReason.None)
        {
            return Cancelled(
                EmergencyTeleportState.Initial,
                permanentFailure,
                EmergencyTeleportDangerSignal.Unknown);
        }

        var signal = ClassifyDanger(observation);
        var state = AdvanceEpisode(previous, signal, observation.NowMilliseconds);
        if (signal == EmergencyTeleportDangerSignal.Unknown)
            return None(state, EmergencyTeleportDecisionReason.DangerUnknown, signal);
        if (signal == EmergencyTeleportDangerSignal.Safe)
        {
            return None(
                state,
                state.EpisodeOpen
                    ? EmergencyTeleportDecisionReason.DangerClearGrace
                    : EmergencyTeleportDecisionReason.DangerInactive,
                signal);
        }

        if (state.EpisodeSpent)
            return Spent(state, EmergencyTeleportDecisionReason.EpisodeSpent, signal);

        if (!observation.InputProbeSucceeded)
            return None(state, EmergencyTeleportDecisionReason.InputProbeUnavailable, signal);
        if (observation.IsTextInputActive)
            return None(state, EmergencyTeleportDecisionReason.TextInputActive, signal);

        if (state.Intent is { IsValid: true } frozen)
            return ObserveFrozen(state, frozen, observation, signal);

        if (observation.ActionHelpersSuppressedByGuard)
            return None(state, EmergencyTeleportDecisionReason.GuardSuppressed, signal);
        if (observation.HigherPriorityClaimed)
            return None(state, EmergencyTeleportDecisionReason.HigherPriorityClaimed, signal);
        if (!observation.HeldGameplayKeyEligible || observation.HeldGameplayKeyCode <= 0)
            return None(state, EmergencyTeleportDecisionReason.NoHeldGameplayKey, signal);
        if (!IsExactJobAction(observation.LocalJobId, observation.ResolvedActionId))
            return None(state, EmergencyTeleportDecisionReason.ResolvedActionInvalid, signal);
        if (!observation.ActionLocallyReady)
            return None(state, EmergencyTeleportDecisionReason.ActionNotReady, signal);

        var selectedIndex = SelectBestCandidateIndex(
            observation.Candidates,
            observation.Settings);
        if (selectedIndex < 0)
        {
            return None(
                state,
                EmergencyTeleportDecisionReason.NoExactSafeDestination,
                signal);
        }

        var selected = observation.Candidates![selectedIndex];
        var intent = new EmergencyTeleportIntent(
            observation.ResolvedActionId,
            observation.LocalJobId,
            observation.LocalPlayer,
            observation.Context,
            selected.Actor,
            selected.PartySlot,
            observation.HeldGameplayKeyCode,
            state.EpisodeToken,
            observation.Settings);
        if (!intent.IsValid)
        {
            return Cancelled(
                SpendWithoutAttempt(state, observation.NowMilliseconds),
                EmergencyTeleportDecisionReason.FrozenIntentInvalid,
                signal);
        }

        state = Stamp(state with { Intent = intent }, observation.NowMilliseconds);
        return observation.NativeBoundaryReady
            ? Dispatch(state, intent, selectedIndex, signal)
            : Armed(
                state,
                intent,
                selectedIndex,
                EmergencyTeleportDecisionReason.NativeBoundaryUnavailable,
                signal,
                inputClaimed: true);
    }

    /// <summary>
    /// Full final exact-intent validation. It finds only the frozen party actor;
    /// it never calls SelectBestCandidateIndex and therefore cannot rerank or
    /// substitute a destination.
    /// </summary>
    public static bool CanUseFrozenIntent(
        EmergencyTeleportState state,
        EmergencyTeleportObservation observation)
    {
        if (!state.EpisodeOpen ||
            state.EpisodeSpent ||
            state.Intent is not { IsValid: true } intent ||
            intent.EpisodeToken != state.EpisodeToken ||
            observation.HardReset ||
            observation.NowMilliseconds < 0 ||
            (state.LastObservedAtMilliseconds >= 0 &&
             observation.NowMilliseconds < state.LastObservedAtMilliseconds) ||
            !observation.ConfigurationEnabled ||
            observation.Settings != intent.Settings ||
            observation.Context != intent.Context ||
            observation.LocalPlayer != intent.LocalPlayer ||
            !observation.IsLocalPlayerAlive ||
            !observation.IsLocalPlayerTargetable ||
            observation.LocalJobId != intent.LocalJobId ||
            !observation.MetadataVerified ||
            observation.ActionHelpersSuppressedByGuard ||
            observation.HigherPriorityClaimed ||
            !observation.InputProbeSucceeded ||
            observation.IsTextInputActive ||
            !observation.HeldGameplayKeyEligible ||
            observation.HeldGameplayKeyCode != intent.FrozenKeyCode ||
            !observation.FrozenKeyStillDown ||
            !IsExactJobAction(observation.LocalJobId, observation.ResolvedActionId) ||
            observation.ResolvedActionId != intent.ActionId ||
            !observation.ActionLocallyReady ||
            !observation.NativeBoundaryReady ||
            ClassifyDanger(observation) != EmergencyTeleportDangerSignal.Danger)
        {
            return false;
        }

        return TryFindFrozenCandidateIndex(
                   observation.Candidates,
                   intent,
                   observation.Settings,
                   out _);
    }

    /// <summary>
    /// Atomically spends the open episode before the runtime's sole native call.
    /// A second commit in the same episode always returns ShouldInvokeNative=false.
    /// </summary>
    public static EmergencyTeleportAttemptCommit CommitNativeAttempt(
        EmergencyTeleportState current,
        EmergencyTeleportObservation finalObservation)
    {
        if (!CanUseFrozenIntent(current, finalObservation) ||
            current.Intent is not { IsValid: true } intent)
        {
            // Crossing the runtime's final-preflight boundary is terminal for
            // this danger episode. Never leave an armed target available for a
            // later frame after exact identity/status/action drift.
            var retired = current.EpisodeOpen &&
                          !current.EpisodeSpent &&
                          current.Intent is { IsValid: true }
                ? Stamp(current with
                {
                    EpisodeSpent = true,
                    Intent = null,
                    LastNativeOutcome = ClientActionAttemptOutcome.NotInvoked,
                }, Math.Max(
                    current.LastObservedAtMilliseconds,
                    Math.Max(0, finalObservation.NowMilliseconds)))
                : current;
            return new EmergencyTeleportAttemptCommit(
                retired,
                null,
                false,
                current.EpisodeSpent
                    ? EmergencyTeleportDecisionReason.EpisodeSpent
                    : EmergencyTeleportDecisionReason.FrozenIntentInvalid);
        }

        var spent = Stamp(current with
        {
            EpisodeSpent = true,
            Intent = null,
            LastNativeOutcome = ClientActionAttemptOutcome.None,
        }, finalObservation.NowMilliseconds);
        return new EmergencyTeleportAttemptCommit(
            spent,
            intent,
            true,
            EmergencyTeleportDecisionReason.AttemptCommitted);
    }

    /// <summary>
    /// Records diagnostics only. No native outcome can clear the spent latch or
    /// expose a retry.
    /// </summary>
    public static EmergencyTeleportState RecordNativeOutcome(
        EmergencyTeleportState committed,
        ClientActionAttemptOutcome outcome,
        long nowMilliseconds) =>
        Stamp(committed with
        {
            EpisodeSpent = committed.EpisodeOpen || committed.EpisodeSpent,
            Intent = null,
            LastNativeOutcome = outcome,
        }, Math.Max(
            committed.LastObservedAtMilliseconds,
            Math.Max(0, nowMilliseconds)));

    private static EmergencyTeleportDecision ObserveFrozen(
        EmergencyTeleportState state,
        EmergencyTeleportIntent intent,
        EmergencyTeleportObservation observation,
        EmergencyTeleportDangerSignal signal)
    {
        if (!observation.FrozenKeyStillDown)
        {
            return None(
                Stamp(state with { Intent = null }, observation.NowMilliseconds),
                EmergencyTeleportDecisionReason.ExactKeyReleased,
                signal);
        }

        var selectedIndex = -1;
        var exactIdentity = observation.Context == intent.Context &&
                            observation.LocalPlayer == intent.LocalPlayer &&
                            observation.LocalJobId == intent.LocalJobId &&
                            observation.Settings == intent.Settings &&
                            IsExactJobAction(observation.LocalJobId, observation.ResolvedActionId) &&
                            observation.ResolvedActionId == intent.ActionId &&
                            TryFindFrozenCandidateIndex(
                                observation.Candidates,
                                intent,
                                observation.Settings,
                                out selectedIndex);
        if (!exactIdentity)
        {
            return Cancelled(
                SpendWithoutAttempt(state, observation.NowMilliseconds),
                EmergencyTeleportDecisionReason.FrozenIntentInvalid,
                signal);
        }

        if (observation.ActionHelpersSuppressedByGuard)
        {
            return Armed(
                state,
                intent,
                selectedIndex,
                EmergencyTeleportDecisionReason.GuardSuppressed,
                signal,
                inputClaimed: false);
        }

        if (observation.HigherPriorityClaimed)
        {
            return Armed(
                state,
                intent,
                selectedIndex,
                EmergencyTeleportDecisionReason.HigherPriorityClaimed,
                signal,
                inputClaimed: false);
        }

        if (!observation.ActionLocallyReady)
        {
            return Armed(
                state,
                intent,
                selectedIndex,
                EmergencyTeleportDecisionReason.ActionNotReady,
                signal,
                inputClaimed: false);
        }

        if (!observation.NativeBoundaryReady)
        {
            return Armed(
                state,
                intent,
                selectedIndex,
                EmergencyTeleportDecisionReason.NativeBoundaryUnavailable,
                signal,
                inputClaimed: true);
        }

        return Dispatch(state, intent, selectedIndex, signal);
    }

    private static bool TryFindFrozenCandidateIndex(
        IReadOnlyList<EmergencyTeleportCandidate>? candidates,
        EmergencyTeleportIntent intent,
        EmergencyTeleportSettings settings,
        out int selectedIndex)
    {
        selectedIndex = -1;
        if (candidates is null || !intent.IsValid || !settings.IsValid)
            return false;

        var targetMatches = 0;
        var slotMatches = 0;
        var gameObjectMatches = 0;
        var entityMatches = 0;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (candidate.Actor == intent.Target) targetMatches++;
            if (candidate.PartySlot == intent.PartySlot) slotMatches++;
            if (candidate.Actor.GameObjectId == intent.Target.GameObjectId)
                gameObjectMatches++;
            if (candidate.Actor.EntityId == intent.Target.EntityId)
                entityMatches++;
            if (candidate.Actor != intent.Target ||
                candidate.PartySlot != intent.PartySlot)
            {
                continue;
            }

            if (selectedIndex >= 0 || !IsEligibleCandidate(candidate, settings))
                return false;
            selectedIndex = index;
        }

        return selectedIndex >= 0 &&
               targetMatches == 1 &&
               slotMatches == 1 &&
               gameObjectMatches == 1 &&
               entityMatches == 1;
    }

    private static EmergencyTeleportDecisionReason GetPermanentGateFailure(
        EmergencyTeleportObservation observation)
    {
        if (!observation.ConfigurationEnabled)
            return EmergencyTeleportDecisionReason.ConfigurationDisabled;
        if (!observation.Settings.IsValid)
            return EmergencyTeleportDecisionReason.SettingsInvalid;
        if (observation.Context is not (SupportedPvPContext.CrystallineConflict or
            SupportedPvPContext.WolvesDen))
        {
            return EmergencyTeleportDecisionReason.OutsideSupportedPvPContext;
        }
        if (!observation.LocalPlayer.IsValid)
            return EmergencyTeleportDecisionReason.LocalPlayerIdentityInvalid;
        if (!observation.IsLocalPlayerAlive)
            return EmergencyTeleportDecisionReason.LocalPlayerDead;
        if (!observation.IsLocalPlayerTargetable)
            return EmergencyTeleportDecisionReason.LocalPlayerUntargetable;
        if (!TryGetActionForJob(observation.LocalJobId, out _))
            return EmergencyTeleportDecisionReason.LocalJobUnsupported;
        if (!observation.MetadataVerified)
            return EmergencyTeleportDecisionReason.MetadataUnverified;
        return EmergencyTeleportDecisionReason.None;
    }

    private static EmergencyTeleportState AdvanceEpisode(
        EmergencyTeleportState previous,
        EmergencyTeleportDangerSignal signal,
        long nowMilliseconds)
    {
        if (signal == EmergencyTeleportDangerSignal.Unknown)
        {
            return Stamp(previous with { SafeSinceMilliseconds = -1 }, nowMilliseconds);
        }

        if (signal == EmergencyTeleportDangerSignal.Danger)
        {
            if (previous.EpisodeOpen)
            {
                return Stamp(
                    previous with { SafeSinceMilliseconds = -1 },
                    nowMilliseconds);
            }

            return new EmergencyTeleportState(
                EpisodeOpen: true,
                EpisodeSpent: false,
                SafeSinceMilliseconds: -1,
                EpisodeToken: NextToken(previous.EpisodeToken),
                Intent: null,
                LastObservedAtMilliseconds: nowMilliseconds,
                LastNativeOutcome: ClientActionAttemptOutcome.None);
        }

        if (!previous.EpisodeOpen)
        {
            return Stamp(previous with
            {
                EpisodeSpent = false,
                SafeSinceMilliseconds = -1,
                Intent = null,
            }, nowMilliseconds);
        }

        var safeSince = previous.SafeSinceMilliseconds >= 0
            ? previous.SafeSinceMilliseconds
            : nowMilliseconds;
        if (nowMilliseconds - safeSince < DangerClearGraceMilliseconds)
        {
            return Stamp(previous with
            {
                SafeSinceMilliseconds = safeSince,
                Intent = null,
            }, nowMilliseconds);
        }

        return new EmergencyTeleportState(
            EpisodeOpen: false,
            EpisodeSpent: false,
            SafeSinceMilliseconds: -1,
            EpisodeToken: previous.EpisodeToken,
            Intent: null,
            LastObservedAtMilliseconds: nowMilliseconds,
            LastNativeOutcome: previous.LastNativeOutcome);
    }

    private static bool IsBetter(
        EmergencyTeleportCandidate candidate,
        EmergencyTeleportCandidate current)
    {
        var nearby = candidate.NearbyEnemyCount.CompareTo(current.NearbyEnemyCount);
        if (nearby != 0) return nearby < 0;

        var travel = candidate.TravelDistanceYalms.CompareTo(current.TravelDistanceYalms);
        if (travel != 0) return travel > 0;

        var clearance = candidate.MinimumEnemyEdgeClearanceYalms.CompareTo(
            current.MinimumEnemyEdgeClearanceYalms);
        if (clearance != 0) return clearance > 0;

        if (candidate.PartySlot != current.PartySlot)
            return candidate.PartySlot < current.PartySlot;
        if (candidate.Actor.EntityId != current.Actor.EntityId)
            return candidate.Actor.EntityId < current.Actor.EntityId;
        return candidate.Actor.GameObjectId < current.Actor.GameObjectId;
    }

    private static EmergencyTeleportState SpendWithoutAttempt(
        EmergencyTeleportState state,
        long nowMilliseconds) =>
        Stamp(state with
        {
            EpisodeSpent = state.EpisodeOpen,
            Intent = null,
            LastNativeOutcome = ClientActionAttemptOutcome.NotInvoked,
        }, nowMilliseconds);

    private static EmergencyTeleportState Stamp(
        EmergencyTeleportState state,
        long nowMilliseconds) =>
        state with { LastObservedAtMilliseconds = nowMilliseconds };

    private static EmergencyTeleportDecision Dispatch(
        EmergencyTeleportState state,
        EmergencyTeleportIntent intent,
        int selectedCandidateIndex,
        EmergencyTeleportDangerSignal signal) =>
        new(
            state,
            EmergencyTeleportDecisionKind.Dispatch,
            EmergencyTeleportDecisionReason.None,
            signal,
            intent,
            selectedCandidateIndex,
            InputClaimed: true);

    private static EmergencyTeleportDecision Armed(
        EmergencyTeleportState state,
        EmergencyTeleportIntent intent,
        int selectedCandidateIndex,
        EmergencyTeleportDecisionReason reason,
        EmergencyTeleportDangerSignal signal,
        bool inputClaimed) =>
        new(
            Stamp(state, Math.Max(0, state.LastObservedAtMilliseconds)),
            EmergencyTeleportDecisionKind.Armed,
            reason,
            signal,
            intent,
            selectedCandidateIndex,
            inputClaimed);

    private static EmergencyTeleportDecision None(
        EmergencyTeleportState state,
        EmergencyTeleportDecisionReason reason,
        EmergencyTeleportDangerSignal signal) =>
        new(
            state,
            EmergencyTeleportDecisionKind.None,
            reason,
            signal);

    private static EmergencyTeleportDecision Spent(
        EmergencyTeleportState state,
        EmergencyTeleportDecisionReason reason,
        EmergencyTeleportDangerSignal signal) =>
        new(
            state,
            EmergencyTeleportDecisionKind.Spent,
            reason,
            signal);

    private static EmergencyTeleportDecision Cancelled(
        EmergencyTeleportState state,
        EmergencyTeleportDecisionReason reason,
        EmergencyTeleportDangerSignal signal) =>
        new(
            state,
            EmergencyTeleportDecisionKind.Cancelled,
            reason,
            signal);

    private static ulong NextToken(ulong current) =>
        current == ulong.MaxValue ? 1UL : current + 1UL;
}
