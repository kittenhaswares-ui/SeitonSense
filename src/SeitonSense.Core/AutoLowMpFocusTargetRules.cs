namespace SeitonSense.Core;

public enum AutoLowMpFocusObservedState
{
    Unknown = 0,
    Empty = 1,
    Occupied = 2,
}

public enum AutoLowMpFocusTargetDecisionKind
{
    None = 0,
    Waiting = 1,
    Suppressed = 2,
    SetFocus = 3,
}

public enum AutoLowMpFocusTargetDecisionReason
{
    None = 0,
    ConfigurationDisabled = 1,
    NotCrystallineConflict = 2,
    HardReset = 3,
    LocalPlayerInvalid = 4,
    MetadataUnverified = 5,
    TextInputStateUnknown = 6,
    TextInputActive = 7,
    CanonicalEnemySetIncomplete = 8,
    FocusStateUnknown = 9,
    FocusOccupied = 10,
    FocusNotStableEmpty = 11,
    ManualOverrideLatched = 12,
    NoTrustedLowMpWave = 13,
    WaveAlreadySpent = 14,
    NoReachableCandidate = 15,
    WriteRateLimited = 16,
    ReadyToSet = 17,
}

public enum AutoLowMpFocusTargetSetOutcome
{
    TerminalFailure = 0,
    SetterInvokedWithoutExactReadback = 1,
    ExactReadbackConfirmed = 2,
}

public readonly record struct AutoLowMpFocusTargetCandidate(
    int EnemySlot,
    TargetPressureActorIdentity Actor,
    bool ExactCanonicalIdentity,
    bool Alive,
    bool Targetable,
    uint CurrentHp,
    uint MaximumHp,
    bool LowMpWaveLatched,
    bool TrustedLowMp,
    uint CurrentMp,
    uint MaximumMp,
    bool NativeTargetValid,
    bool NativeRangeAndLineOfSight);

public readonly record struct AutoLowMpFocusTargetIntent(
    int EnemySlot,
    TargetPressureActorIdentity LocalPlayer,
    TargetPressureActorIdentity Target,
    uint SelectedCurrentMp,
    uint SelectedMaximumMp)
{
    public bool IsValid =>
        EnemySlot is >= EnemySlotRules.FirstSlot and <= EnemySlotRules.LastSlot &&
        LocalPlayer.IsValid &&
        Target.IsValid &&
        Target != LocalPlayer &&
        SelectedMaximumMp > 0 &&
        SelectedCurrentMp <= AutoLowMpFocusTargetRules.MaximumEligibleMp &&
        SelectedCurrentMp <= SelectedMaximumMp;
}

public readonly record struct AutoLowMpFocusTargetState(
    bool LowMpWaveActive,
    bool AttemptSpentForWave,
    bool ManualOverrideLatched,
    long FocusEmptySinceMilliseconds,
    long LastAttemptAtMilliseconds,
    TargetPressureActorIdentity LastConfirmedFocusTarget)
{
    public static AutoLowMpFocusTargetState Initial => new(
        false,
        false,
        false,
        -1,
        -1,
        default);
}

public readonly record struct AutoLowMpFocusTargetObservation(
    bool ConfigurationEnabled,
    bool IsCrystallineConflict,
    bool LocalPlayerExactAndAlive,
    TargetPressureActorIdentity LocalPlayer,
    bool MetadataVerified,
    bool TextInputStateKnown,
    bool TextInputActive,
    bool CompleteCanonicalEnemySet,
    AutoLowMpFocusObservedState FocusState,
    TargetPressureActorIdentity FocusTarget,
    long NowMilliseconds,
    IReadOnlyList<AutoLowMpFocusTargetCandidate> Candidates,
    bool HardReset);

public readonly record struct AutoLowMpFocusTargetDecision(
    AutoLowMpFocusTargetState State,
    AutoLowMpFocusTargetDecisionKind Kind,
    AutoLowMpFocusTargetDecisionReason Reason,
    int SelectedCandidateIndex,
    AutoLowMpFocusTargetIntent? Intent)
{
    public bool ShouldSetFocus =>
        Kind == AutoLowMpFocusTargetDecisionKind.SetFocus &&
        Intent is not null;
}

public static class AutoLowMpFocusTargetRules
{
    public const uint ProbeActionId = 29_515;
    public const int ProbeRange = 20;
    public const int MaximumEligibleMp = LowMpRules.RecuperateCost;
    public const int ObservationEnterThreshold = MaximumEligibleMp + 1;
    public const long FocusEmptyStabilityMilliseconds = 100;
    public const long MinimumWriteIntervalMilliseconds = 1_000;

    public static AutoLowMpFocusTargetDecision Observe(
        AutoLowMpFocusTargetState state,
        AutoLowMpFocusTargetObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation.Candidates);

        if (observation.HardReset)
            return None(AutoLowMpFocusTargetState.Initial, AutoLowMpFocusTargetDecisionReason.HardReset);

        if (!observation.ConfigurationEnabled)
            return None(AutoLowMpFocusTargetState.Initial, AutoLowMpFocusTargetDecisionReason.ConfigurationDisabled);

        if (!observation.IsCrystallineConflict)
            return None(AutoLowMpFocusTargetState.Initial, AutoLowMpFocusTargetDecisionReason.NotCrystallineConflict);

        var next = ObserveConfirmedFocusDrift(state, observation);
        if (next.ManualOverrideLatched)
        {
            return Suppressed(
                next with { FocusEmptySinceMilliseconds = -1 },
                AutoLowMpFocusTargetDecisionReason.ManualOverrideLatched);
        }

        if (!observation.LocalPlayerExactAndAlive || !observation.LocalPlayer.IsValid)
            return Suppressed(next, AutoLowMpFocusTargetDecisionReason.LocalPlayerInvalid);

        if (!observation.MetadataVerified)
            return Suppressed(next, AutoLowMpFocusTargetDecisionReason.MetadataUnverified);

        if (!observation.TextInputStateKnown)
        {
            return Suppressed(
                next with { FocusEmptySinceMilliseconds = -1 },
                AutoLowMpFocusTargetDecisionReason.TextInputStateUnknown);
        }

        if (observation.TextInputActive)
        {
            return Suppressed(
                next with { FocusEmptySinceMilliseconds = -1 },
                AutoLowMpFocusTargetDecisionReason.TextInputActive);
        }

        if (observation.FocusState == AutoLowMpFocusObservedState.Unknown)
        {
            return Suppressed(
                next with { FocusEmptySinceMilliseconds = -1 },
                AutoLowMpFocusTargetDecisionReason.FocusStateUnknown);
        }

        if (!observation.CompleteCanonicalEnemySet ||
            !HasCompleteExactCanonicalSet(observation.Candidates))
        {
            return Suppressed(next, AutoLowMpFocusTargetDecisionReason.CanonicalEnemySetIncomplete);
        }

        next = ObserveFocusEmpty(next, observation.FocusState, observation.NowMilliseconds);
        var lowMpWaveActive = observation.Candidates.Any(IsLowMpWaveMember);
        if (!lowMpWaveActive)
        {
            return Waiting(
                next with
                {
                    LowMpWaveActive = false,
                    AttemptSpentForWave = false,
                },
                AutoLowMpFocusTargetDecisionReason.NoTrustedLowMpWave);
        }

        if (!next.LowMpWaveActive)
        {
            next = next with
            {
                LowMpWaveActive = true,
                AttemptSpentForWave = observation.FocusState != AutoLowMpFocusObservedState.Empty,
            };
        }
        else if (observation.FocusState != AutoLowMpFocusObservedState.Empty &&
                 !next.AttemptSpentForWave)
        {
            // An occupied focus during this wave wins permanently. Clearing it later
            // cannot turn an old low-MP sample into a delayed target mutation.
            next = next with { AttemptSpentForWave = true };
        }

        if (observation.FocusState == AutoLowMpFocusObservedState.Occupied)
            return Suppressed(next, AutoLowMpFocusTargetDecisionReason.FocusOccupied);

        if (next.AttemptSpentForWave)
            return Suppressed(next, AutoLowMpFocusTargetDecisionReason.WaveAlreadySpent);

        if (!HasStableEmptyFocus(next, observation.NowMilliseconds))
            return Waiting(next, AutoLowMpFocusTargetDecisionReason.FocusNotStableEmpty);

        if (!CanIssueWrite(next.LastAttemptAtMilliseconds, observation.NowMilliseconds))
            return Waiting(next, AutoLowMpFocusTargetDecisionReason.WriteRateLimited);

        var selectedIndex = SelectBestCandidateIndex(observation.Candidates, observation.LocalPlayer);
        if (selectedIndex < 0)
            return Waiting(next, AutoLowMpFocusTargetDecisionReason.NoReachableCandidate);

        var selected = observation.Candidates[selectedIndex];
        var intent = new AutoLowMpFocusTargetIntent(
            selected.EnemySlot,
            observation.LocalPlayer,
            selected.Actor,
            selected.CurrentMp,
            selected.MaximumMp);
        var spent = next with
        {
            AttemptSpentForWave = true,
            LastAttemptAtMilliseconds = observation.NowMilliseconds,
        };
        return new AutoLowMpFocusTargetDecision(
            spent,
            AutoLowMpFocusTargetDecisionKind.SetFocus,
            AutoLowMpFocusTargetDecisionReason.ReadyToSet,
            selectedIndex,
            intent);
    }

    public static AutoLowMpFocusTargetState ApplySetOutcome(
        AutoLowMpFocusTargetState state,
        AutoLowMpFocusTargetIntent intent,
        AutoLowMpFocusTargetSetOutcome outcome) =>
        outcome == AutoLowMpFocusTargetSetOutcome.ExactReadbackConfirmed && intent.IsValid
            ? state with { LastConfirmedFocusTarget = intent.Target }
            : state;

    public static bool HasCompleteExactCanonicalSet(
        IReadOnlyList<AutoLowMpFocusTargetCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count != EnemySlotRules.LastSlot) return false;

        var slots = new HashSet<int>();
        var gameObjectIds = new HashSet<ulong>();
        var entityIds = new HashSet<uint>();
        foreach (var candidate in candidates)
        {
            if (!candidate.ExactCanonicalIdentity ||
                !candidate.Actor.IsValid ||
                !EnemySlotRules.IsValidSlot(candidate.EnemySlot) ||
                !slots.Add(candidate.EnemySlot) ||
                !gameObjectIds.Add(candidate.Actor.GameObjectId) ||
                !entityIds.Add(candidate.Actor.EntityId))
            {
                return false;
            }
        }

        return slots.Count == EnemySlotRules.LastSlot &&
               Enumerable.Range(EnemySlotRules.FirstSlot, EnemySlotRules.LastSlot)
                   .All(slots.Contains);
    }

    public static bool IsEligibleCandidate(
        AutoLowMpFocusTargetCandidate candidate,
        TargetPressureActorIdentity localPlayer) =>
        HasTrustedCurrentLowMp(candidate) &&
        localPlayer.IsValid &&
        candidate.Actor != localPlayer &&
        candidate.NativeTargetValid &&
        candidate.NativeRangeAndLineOfSight;

    public static int SelectBestCandidateIndex(
        IReadOnlyList<AutoLowMpFocusTargetCandidate> candidates,
        TargetPressureActorIdentity localPlayer)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var bestIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsEligibleCandidate(candidate, localPlayer)) continue;
            if (bestIndex < 0 || Compare(candidate, candidates[bestIndex]) < 0)
                bestIndex = index;
        }

        return bestIndex;
    }

    public static bool CanSetFrozenIntent(
        AutoLowMpFocusTargetIntent intent,
        AutoLowMpFocusTargetCandidate candidate,
        bool configurationEnabled,
        bool isCrystallineConflict,
        bool localPlayerExactAndAlive,
        TargetPressureActorIdentity currentLocalPlayer,
        bool metadataVerified,
        AutoLowMpFocusObservedState focusState) =>
        configurationEnabled &&
        isCrystallineConflict &&
        localPlayerExactAndAlive &&
        metadataVerified &&
        focusState == AutoLowMpFocusObservedState.Empty &&
        intent.IsValid &&
        intent.LocalPlayer == currentLocalPlayer &&
        intent.EnemySlot == candidate.EnemySlot &&
        intent.Target.Equals(candidate.Actor) &&
        IsEligibleCandidate(candidate, currentLocalPlayer);

    private static AutoLowMpFocusTargetState ObserveConfirmedFocusDrift(
        AutoLowMpFocusTargetState state,
        AutoLowMpFocusTargetObservation observation)
    {
        if (!state.LastConfirmedFocusTarget.IsValid ||
            observation.FocusState == AutoLowMpFocusObservedState.Unknown)
        {
            return state;
        }

        if (observation.FocusState == AutoLowMpFocusObservedState.Occupied &&
            observation.FocusTarget.Equals(state.LastConfirmedFocusTarget))
        {
            return state;
        }

        return state with
        {
            ManualOverrideLatched = true,
            LastConfirmedFocusTarget = default,
        };
    }

    private static AutoLowMpFocusTargetState ObserveFocusEmpty(
        AutoLowMpFocusTargetState state,
        AutoLowMpFocusObservedState focusState,
        long nowMilliseconds)
    {
        if (focusState != AutoLowMpFocusObservedState.Empty || nowMilliseconds < 0)
            return state with { FocusEmptySinceMilliseconds = -1 };

        if (state.FocusEmptySinceMilliseconds >= 0 &&
            nowMilliseconds >= state.FocusEmptySinceMilliseconds)
        {
            return state;
        }

        return state with { FocusEmptySinceMilliseconds = nowMilliseconds };
    }

    private static bool HasStableEmptyFocus(
        AutoLowMpFocusTargetState state,
        long nowMilliseconds) =>
        state.FocusEmptySinceMilliseconds >= 0 &&
        nowMilliseconds >= state.FocusEmptySinceMilliseconds &&
        nowMilliseconds - state.FocusEmptySinceMilliseconds >= FocusEmptyStabilityMilliseconds;

    private static bool CanIssueWrite(long lastAttemptAtMilliseconds, long nowMilliseconds) =>
        nowMilliseconds >= 0 &&
        (lastAttemptAtMilliseconds < 0 ||
         (nowMilliseconds >= lastAttemptAtMilliseconds &&
          nowMilliseconds - lastAttemptAtMilliseconds >= MinimumWriteIntervalMilliseconds));

    private static bool IsLowMpWaveMember(AutoLowMpFocusTargetCandidate candidate) =>
        candidate.ExactCanonicalIdentity &&
        candidate.Actor.IsValid &&
        EnemySlotRules.IsValidSlot(candidate.EnemySlot) &&
        candidate.LowMpWaveLatched;

    private static bool HasTrustedCurrentLowMp(AutoLowMpFocusTargetCandidate candidate) =>
        IsLowMpWaveMember(candidate) &&
        candidate.Alive &&
        candidate.Targetable &&
        candidate.CurrentHp > 0 &&
        candidate.MaximumHp > 0 &&
        candidate.CurrentHp <= candidate.MaximumHp &&
        candidate.TrustedLowMp &&
        candidate.MaximumMp > 0 &&
        candidate.CurrentMp <= MaximumEligibleMp &&
        candidate.CurrentMp <= candidate.MaximumMp;

    private static int Compare(
        AutoLowMpFocusTargetCandidate left,
        AutoLowMpFocusTargetCandidate right)
    {
        var mp = CompareRatio(left.CurrentMp, left.MaximumMp, right.CurrentMp, right.MaximumMp);
        if (mp != 0) return mp;

        var hp = CompareRatio(left.CurrentHp, left.MaximumHp, right.CurrentHp, right.MaximumHp);
        if (hp != 0) return hp;

        var slot = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slot != 0) return slot;

        var entity = left.Actor.EntityId.CompareTo(right.Actor.EntityId);
        return entity != 0
            ? entity
            : left.Actor.GameObjectId.CompareTo(right.Actor.GameObjectId);
    }

    private static int CompareRatio(
        uint leftCurrent,
        uint leftMaximum,
        uint rightCurrent,
        uint rightMaximum)
    {
        var leftScaled = (ulong)leftCurrent * rightMaximum;
        var rightScaled = (ulong)rightCurrent * leftMaximum;
        return leftScaled.CompareTo(rightScaled);
    }

    private static AutoLowMpFocusTargetDecision None(
        AutoLowMpFocusTargetState state,
        AutoLowMpFocusTargetDecisionReason reason) =>
        new(state, AutoLowMpFocusTargetDecisionKind.None, reason, -1, null);

    private static AutoLowMpFocusTargetDecision Waiting(
        AutoLowMpFocusTargetState state,
        AutoLowMpFocusTargetDecisionReason reason) =>
        new(state, AutoLowMpFocusTargetDecisionKind.Waiting, reason, -1, null);

    private static AutoLowMpFocusTargetDecision Suppressed(
        AutoLowMpFocusTargetState state,
        AutoLowMpFocusTargetDecisionReason reason) =>
        new(state, AutoLowMpFocusTargetDecisionKind.Suppressed, reason, -1, null);
}
