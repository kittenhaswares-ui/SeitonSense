using SeitonSense.Core;

internal static class EmergencyTeleportSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer = new(0x1001, 0x2001);
    private static readonly TargetPressureActorIdentity FirstAlly = new(0x1002, 0x2002);
    private static readonly TargetPressureActorIdentity SecondAlly = new(0x1003, 0x2003);

    public static void ExactJobActionMappingAndDefaultsArePinned()
    {
        Equal(50, EmergencyTeleportRules.DefaultHpPercent, "default HP threshold");
        Equal(4_000U, EmergencyTeleportRules.DefaultMpThreshold, "default MP threshold");
        Equal(1, EmergencyTeleportRules.DefaultMinimumDirectEnemyCount, "default pressure threshold");
        Equal(10f, EmergencyTeleportRules.DefaultMinimumTravelYalms, "default travel threshold");
        Equal(10f, EmergencyTeleportRules.DefaultEnemySafetyRadiusYalms, "default safety radius");
        Equal(0, EmergencyTeleportRules.DefaultMaximumNearbyEnemyCount, "default nearby count");
        Equal(250L, EmergencyTeleportRules.MaximumPressureAgeMilliseconds, "pressure age");
        Equal(300L, EmergencyTeleportRules.DangerClearGraceMilliseconds, "clear grace");
        True(EmergencyTeleportSettings.Default.IsValid, "default settings are valid");

        Mapping(EmergencyTeleportRules.MonkJobId, EmergencyTeleportRules.ThunderclapActionId);
        Mapping(EmergencyTeleportRules.SageJobId, EmergencyTeleportRules.IcarusActionId);
        Mapping(EmergencyTeleportRules.BlackMageJobId, EmergencyTeleportRules.AetherialManipulationActionId);
        Mapping(EmergencyTeleportRules.ViperJobId, EmergencyTeleportRules.SlitherActionId);

        False(EmergencyTeleportRules.TryGetActionForJob(19, out var unsupported), "PLD excluded");
        Equal(0U, unsupported, "unsupported action sentinel");
        False(
            EmergencyTeleportRules.IsExactJobAction(
                EmergencyTeleportRules.ViperJobId,
                EmergencyTeleportRules.IcarusActionId),
            "cross-job action rejected");
    }

    public static void TriggerThresholdsAreStrictAndPressureMustBeFresh()
    {
        var exactHp = Observation(now: 1_000) with { CurrentHp = 50_000 };
        Equal(
            EmergencyTeleportDangerSignal.Safe,
            EmergencyTeleportRules.ClassifyDanger(exactHp),
            "exactly 50 percent is safe");

        var exactMp = Observation(now: 1_000) with { CurrentMp = 4_000 };
        Equal(
            EmergencyTeleportDangerSignal.Safe,
            EmergencyTeleportRules.ClassifyDanger(exactMp),
            "exactly 4000 MP is safe");

        Equal(
            EmergencyTeleportDangerSignal.Danger,
            EmergencyTeleportRules.ClassifyDanger(Observation(now: 1_000)),
            "strictly below both thresholds with one focus is danger");
        Equal(
            EmergencyTeleportDangerSignal.Safe,
            EmergencyTeleportRules.ClassifyDanger(
                Observation(now: 1_000) with { DirectEnemyCount = 0 }),
            "zero focusing enemies is safe");
        Equal(
            EmergencyTeleportDangerSignal.Unknown,
            EmergencyTeleportRules.ClassifyDanger(
                Observation(now: 1_251) with { PressurePublishedAtMilliseconds = 1_000 }),
            "251 ms pressure is stale");
        Equal(
            EmergencyTeleportDangerSignal.Danger,
            EmergencyTeleportRules.ClassifyDanger(
                Observation(now: 1_250) with { PressurePublishedAtMilliseconds = 1_000 }),
            "250 ms pressure boundary is fresh");
        Equal(
            EmergencyTeleportDangerSignal.Unknown,
            EmergencyTeleportRules.ClassifyDanger(
                Observation(now: 999) with { PressurePublishedAtMilliseconds = 1_000 }),
            "future publication is unknown");
    }

    public static void InvalidSettingsAndTelemetryFailClosed()
    {
        var defaults = EmergencyTeleportSettings.Default;
        var invalid = new[]
        {
            defaults with { HpPercent = 9 },
            defaults with { HpPercent = 91 },
            defaults with { MpThreshold = 10_001 },
            defaults with { MinimumDirectEnemyCount = 0 },
            defaults with { MinimumDirectEnemyCount = 6 },
            defaults with { MinimumTravelYalms = float.NaN },
            defaults with { MinimumTravelYalms = 2.99f },
            defaults with { EnemySafetyRadiusYalms = float.PositiveInfinity },
            defaults with { EnemySafetyRadiusYalms = 20.01f },
            defaults with { MaximumNearbyEnemyCount = -1 },
            defaults with { MaximumNearbyEnemyCount = 6 },
        };
        foreach (var settings in invalid)
            False(settings.IsValid, $"invalid settings {settings}");

        Equal(
            EmergencyTeleportDangerSignal.Unknown,
            EmergencyTeleportRules.ClassifyDanger(
                Observation(now: 1_000) with { CurrentHp = 0 }),
            "zero HP telemetry is unknown");
        Equal(
            EmergencyTeleportDangerSignal.Unknown,
            EmergencyTeleportRules.ClassifyDanger(
                Observation(now: 1_000) with { CurrentMp = 10_001 }),
            "MP above maximum is unknown");

        var decision = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 1_000) with { Settings = invalid[0] });
        Equal(EmergencyTeleportDecisionKind.Cancelled, decision.Kind, "invalid config cancels");
        Equal(EmergencyTeleportDecisionReason.SettingsInvalid, decision.Reason, "invalid config reason");
    }

    public static void SelectionPrefersSafetyThenDistanceThenStableIdentity()
    {
        var permissive = EmergencyTeleportSettings.Default with
        {
            MaximumNearbyEnemyCount = 2,
        };
        var candidates = new[]
        {
            Candidate(FirstAlly, slot: 2, travel: 24f, nearby: 1, clearance: 4f),
            Candidate(SecondAlly, slot: 3, travel: 12f, nearby: 0, clearance: 12f),
        };
        Equal(
            1,
            EmergencyTeleportRules.SelectBestCandidateIndex(candidates, permissive),
            "zero-enemy destination beats farther threatened destination");

        candidates =
        [
            Candidate(FirstAlly, slot: 2, travel: 18f, nearby: 0, clearance: 12f),
            Candidate(SecondAlly, slot: 3, travel: 22f, nearby: 0, clearance: 11f),
        ];
        Equal(
            1,
            EmergencyTeleportRules.SelectBestCandidateIndex(candidates, permissive),
            "farthest wins within equal safety tier");

        candidates =
        [
            Candidate(FirstAlly, slot: 3, travel: 20f, nearby: 0, clearance: 12f),
            Candidate(SecondAlly, slot: 2, travel: 20f, nearby: 0, clearance: 14f),
        ];
        Equal(
            1,
            EmergencyTeleportRules.SelectBestCandidateIndex(candidates, permissive),
            "greater clearance breaks equal distance before slot");

        candidates =
        [
            Candidate(FirstAlly, slot: 3, travel: 20f, nearby: 0, clearance: 14f),
            Candidate(SecondAlly, slot: 2, travel: 20f, nearby: 0, clearance: 14f),
        ];
        Equal(
            1,
            EmergencyTeleportRules.SelectBestCandidateIndex(candidates, permissive),
            "lower party slot is stable final tier");
        Array.Reverse(candidates);
        var reversed = EmergencyTeleportRules.SelectBestCandidateIndex(candidates, permissive);
        Equal(SecondAlly, candidates[reversed].Actor, "enumeration order cannot change winner");
    }

    public static void UnsafeIncompleteOrAmbiguousCandidatesNeverFallback()
    {
        var valid = Candidate(FirstAlly, slot: 2, travel: 10f, nearby: 0, clearance: 10.001f);
        True(
            EmergencyTeleportRules.IsEligibleCandidate(valid, EmergencyTeleportSettings.Default),
            "exact minimum travel and strictly safe clearance are eligible");
        False(
            EmergencyTeleportRules.IsEligibleCandidate(
                valid with { MinimumEnemyEdgeClearanceYalms = 10f },
                EmergencyTeleportSettings.Default),
            "exact safety-radius boundary is rejected");
        False(
            EmergencyTeleportRules.IsEligibleCandidate(
                valid with { NearbyEnemyCount = 1 },
                EmergencyTeleportSettings.Default),
            "default allows no nearby enemy");
        False(
            EmergencyTeleportRules.IsEligibleCandidate(
                valid with { HasCompleteEnemySnapshot = false },
                EmergencyTeleportSettings.Default),
            "incomplete enemy snapshot is rejected");
        False(
            EmergencyTeleportRules.IsEligibleCandidate(
                valid with { HasValidActionTarget = false },
                EmergencyTeleportSettings.Default),
            "target-specific native action rejection is fail-closed");
        False(
            EmergencyTeleportRules.IsEligibleCandidate(
                valid with { TravelDistanceYalms = 9.999f },
                EmergencyTeleportSettings.Default),
            "short hop is rejected");

        var noSafe = new[]
        {
            valid with { NearbyEnemyCount = 1 },
            Candidate(SecondAlly, slot: 3, travel: 25f, nearby: 2, clearance: 0f),
        };
        Equal(
            -1,
            EmergencyTeleportRules.SelectBestCandidateIndex(noSafe, EmergencyTeleportSettings.Default),
            "no unsafe fallback exists");

        var duplicateSlot = new[]
        {
            valid,
            Candidate(SecondAlly, slot: 2, travel: 20f, nearby: 0, clearance: 15f),
        };
        Equal(
            -1,
            EmergencyTeleportRules.SelectBestCandidateIndex(
                duplicateSlot,
                EmergencyTeleportSettings.Default),
            "duplicate party slot fails closed");

        var duplicateActor = new[]
        {
            valid,
            Candidate(FirstAlly, slot: 3, travel: 20f, nearby: 0, clearance: 15f),
        };
        Equal(
            -1,
            EmergencyTeleportRules.SelectBestCandidateIndex(
                duplicateActor,
                EmergencyTeleportSettings.Default),
            "duplicate actor fails closed");
    }

    public static void ValidHoldFreezesOneExactIntentAndBoundaryClaims()
    {
        var noPhysicalDiscovery = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 999) with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKeyCode = 0,
                FrozenKeyStillDown = true,
            });
        Equal(EmergencyTeleportDecisionKind.None, noPhysicalDiscovery.Kind, "release grace cannot discover an intent");
        Equal(EmergencyTeleportDecisionReason.NoHeldGameplayKey, noPhysicalDiscovery.Reason, "discovery remains physical");
        True(noPhysicalDiscovery.Intent is null, "release grace alone freezes no target");

        var waiting = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 1_000) with { NativeBoundaryReady = false });
        Equal(EmergencyTeleportDecisionKind.Armed, waiting.Kind, "ready intent arms at busy boundary");
        Equal(
            EmergencyTeleportDecisionReason.NativeBoundaryUnavailable,
            waiting.Reason,
            "busy boundary reason");
        True(waiting.InputClaimed, "otherwise-ready boundary wait owns current frame");
        True(waiting.Intent is { IsValid: true }, "exact intent frozen");
        Equal(FirstAlly, waiting.Intent!.Value.Target, "frozen target");
        Equal(1UL, waiting.Intent.Value.EpisodeToken, "frozen episode");

        var ready = EmergencyTeleportRules.Observe(
            waiting.NextState,
            Observation(now: 1_001) with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKeyCode = 0,
                FrozenKeyStillDown = true,
            });
        Equal(EmergencyTeleportDecisionKind.Dispatch, ready.Kind, "release-reserved frozen intent dispatches later");
        Equal(FirstAlly, ready.Intent!.Value.Target, "target remains exact");
        True(ready.InputClaimed, "dispatch owns current frame");

        var expiredRelease = EmergencyTeleportRules.Observe(
            waiting.NextState,
            Observation(now: 1_002) with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKeyCode = 0,
                FrozenKeyStillDown = false,
            });
        Equal(EmergencyTeleportDecisionKind.None, expiredRelease.Kind, "expired release reservation cancels the intent");
        Equal(EmergencyTeleportDecisionReason.ExactKeyReleased, expiredRelease.Reason, "expired release reason");
        True(expiredRelease.Intent is null, "expired release clears exact target ownership");

        var higherPriority = EmergencyTeleportRules.Observe(
            waiting.NextState,
            Observation(now: 1_001) with
            {
                HigherPriorityClaimed = true,
                NativeBoundaryReady = true,
            });
        Equal(EmergencyTeleportDecisionKind.Armed, higherPriority.Kind, "higher priority preserves intent");
        False(higherPriority.InputClaimed, "higher priority yields the frame");
        Equal(FirstAlly, higherPriority.Intent!.Value.Target, "priority wait cannot rerank");
    }

    public static void FrozenTargetDriftSpendsWithoutAlternate()
    {
        var armed = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 1_000) with { NativeBoundaryReady = false });
        var replacement = Candidate(SecondAlly, slot: 3, travel: 25f, nearby: 0, clearance: 20f);
        var drifted = EmergencyTeleportRules.Observe(
            armed.NextState,
            Observation(now: 1_001) with { Candidates = [replacement] });

        Equal(EmergencyTeleportDecisionKind.Cancelled, drifted.Kind, "frozen target drift cancels");
        Equal(EmergencyTeleportDecisionReason.FrozenIntentInvalid, drifted.Reason, "drift reason");
        True(drifted.NextState.EpisodeSpent, "drift terminally spends the danger episode");
        True(drifted.Intent is null, "no alternate intent is exposed");
        False(drifted.ShouldDispatch, "replacement is never dispatched");

        var stillDanger = EmergencyTeleportRules.Observe(
            drifted.NextState,
            Observation(now: 1_002) with { Candidates = [replacement] });
        Equal(EmergencyTeleportDecisionKind.Spent, stillDanger.Kind, "same episode remains spent");
        False(stillDanger.ShouldDispatch, "same episode cannot choose replacement later");

        var identityArmed = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 2_000) with { NativeBoundaryReady = false });
        var halfIdentityCollision = new TargetPressureActorIdentity(
            FirstAlly.GameObjectId,
            SecondAlly.EntityId);
        var ambiguous = EmergencyTeleportRules.Observe(
            identityArmed.NextState,
            Observation(now: 2_001) with
            {
                Candidates =
                [
                    Candidate(FirstAlly, slot: 2, travel: 20f, nearby: 0, clearance: 15f),
                    Candidate(halfIdentityCollision, slot: 3, travel: 25f, nearby: 0, clearance: 20f),
                ],
            });
        Equal(EmergencyTeleportDecisionKind.Cancelled, ambiguous.Kind, "half-ID collision cancels");
        True(ambiguous.NextState.EpisodeSpent, "ambiguous exact identity cannot be retried");
    }

    public static void NativeCommitIsAtMostOnceAndOutcomesNeverRetry()
    {
        var decision = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 1_000));
        Equal(EmergencyTeleportDecisionKind.Dispatch, decision.Kind, "valid observation dispatches");

        var committed = EmergencyTeleportRules.CommitNativeAttempt(
            decision.NextState,
            Observation(now: 1_001) with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKeyCode = 0,
                FrozenKeyStillDown = true,
            });
        True(committed.ShouldInvokeNative, "release-reserved exact commit permits one call");
        True(committed.NextState.EpisodeSpent, "commit spends before native call");
        Equal(FirstAlly, committed.Intent!.Value.Target, "commit returns only frozen target");

        var duplicate = EmergencyTeleportRules.CommitNativeAttempt(
            committed.NextState,
            Observation(now: 1_002));
        False(duplicate.ShouldInvokeNative, "second commit in same episode is blocked");
        Equal(EmergencyTeleportDecisionReason.EpisodeSpent, duplicate.Reason, "duplicate reason");

        foreach (var outcome in new[]
                 {
                     ClientActionAttemptOutcome.ClientAccepted,
                     ClientActionAttemptOutcome.ClientRejected,
                     ClientActionAttemptOutcome.AcceptanceUnknown,
                 })
        {
            var recorded = EmergencyTeleportRules.RecordNativeOutcome(
                committed.NextState,
                outcome,
                1_003);
            True(recorded.EpisodeSpent, $"{outcome} remains spent");
            True(recorded.Intent is null, $"{outcome} exposes no retry intent");
            Equal(outcome, recorded.LastNativeOutcome, $"{outcome} diagnostics retained");
            var afterOutcome = EmergencyTeleportRules.Observe(
                recorded,
                Observation(now: 1_004));
            Equal(EmergencyTeleportDecisionKind.Spent, afterOutcome.Kind, $"{outcome} cannot retry");
        }

        var freshDispatch = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 2_000));
        False(
            EmergencyTeleportRules.CommitNativeAttempt(
                freshDispatch.NextState,
                Observation(now: 1_999)).ShouldInvokeNative,
            "clock regression cannot cross the native boundary");
        False(
            EmergencyTeleportRules.CommitNativeAttempt(
                freshDispatch.NextState,
                Observation(now: 2_001) with { HardReset = true }).ShouldInvokeNative,
            "hard reset cannot cross the native boundary");
        False(
            EmergencyTeleportRules.CommitNativeAttempt(
                freshDispatch.NextState,
                Observation(now: 2_001) with
                {
                    HeldGameplayKeyEligible = false,
                    HeldGameplayKeyCode = 0,
                    FrozenKeyStillDown = false,
                }).ShouldInvokeNative,
            "invalid frozen-key consent cannot cross the native boundary");

        var rejectedFinalTarget = EmergencyTeleportRules.CommitNativeAttempt(
            freshDispatch.NextState,
            Observation(now: 2_001) with
            {
                Candidates =
                [
                    Candidate(FirstAlly, slot: 2, travel: 20f, nearby: 0, clearance: 15f) with
                    {
                        HasValidActionTarget = false,
                    },
                ],
            });
        False(rejectedFinalTarget.ShouldInvokeNative, "final target-status failure cannot invoke");
        True(rejectedFinalTarget.NextState.EpisodeSpent, "final preflight failure retires episode");
        True(rejectedFinalTarget.NextState.Intent is null, "final preflight failure clears target");
        Equal(
            ClientActionAttemptOutcome.NotInvoked,
            rejectedFinalTarget.NextState.LastNativeOutcome,
            "final preflight failure is diagnostic");

        var monotonicOutcome = EmergencyTeleportRules.RecordNativeOutcome(
            committed.NextState,
            ClientActionAttemptOutcome.AcceptanceUnknown,
            nowMilliseconds: 999);
        Equal(
            committed.NextState.LastObservedAtMilliseconds,
            monotonicOutcome.LastObservedAtMilliseconds,
            "diagnostic recording cannot move the state clock backwards");
    }

    public static void EpisodeRearmsOnlyAfterKnownClearGrace()
    {
        var dispatch = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 1_000));
        var committed = EmergencyTeleportRules.CommitNativeAttempt(
            dispatch.NextState,
            Observation(now: 1_001));
        var spent = committed.NextState;

        var safe = EmergencyTeleportRules.Observe(
            spent,
            Observation(now: 2_000) with { CurrentHp = 50_000 });
        True(safe.NextState.EpisodeOpen, "known safe begins grace");
        True(safe.NextState.EpisodeSpent, "spent latch remains during grace");
        Equal(EmergencyTeleportDecisionReason.DangerClearGrace, safe.Reason, "clear grace reason");

        var boundary = EmergencyTeleportRules.Observe(
            safe.NextState,
            Observation(now: 2_300) with { CurrentHp = 50_000 });
        False(boundary.NextState.EpisodeOpen, "300 ms known safe closes episode");
        False(boundary.NextState.EpisodeSpent, "closed episode clears spent latch");

        var rearmed = EmergencyTeleportRules.Observe(
            boundary.NextState,
            Observation(now: 2_301));
        Equal(EmergencyTeleportDecisionKind.Dispatch, rearmed.Kind, "later danger can dispatch again");
        Equal(2UL, rearmed.NextState.EpisodeToken, "later danger gets a new token");
    }

    public static void UnknownPressureCannotClearOrRearmSpentEpisode()
    {
        var dispatch = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 1_000));
        var committed = EmergencyTeleportRules.CommitNativeAttempt(
            dispatch.NextState,
            Observation(now: 1_001));

        var unknown = EmergencyTeleportRules.Observe(
            committed.NextState,
            Observation(now: 2_000) with
            {
                DirectPressureKnown = false,
                PressurePublishedAtMilliseconds = -1,
            });
        Equal(EmergencyTeleportDangerSignal.Unknown, unknown.DangerSignal, "pressure unknown");
        True(unknown.NextState.EpisodeOpen, "unknown cannot close episode");
        True(unknown.NextState.EpisodeSpent, "unknown cannot clear spent latch");

        var resumed = EmergencyTeleportRules.Observe(
            unknown.NextState,
            Observation(now: 2_001));
        Equal(EmergencyTeleportDecisionKind.Spent, resumed.Kind, "fresh danger resumes same spent episode");
        Equal(1UL, resumed.NextState.EpisodeToken, "unknown gap cannot mint token");

        var safe = EmergencyTeleportRules.Observe(
            unknown.NextState,
            Observation(now: 2_100) with { CurrentHp = 50_000 });
        var interrupted = EmergencyTeleportRules.Observe(
            safe.NextState,
            Observation(now: 2_399) with
            {
                DirectPressureKnown = false,
                PressurePublishedAtMilliseconds = -1,
            });
        var safeAgain = EmergencyTeleportRules.Observe(
            interrupted.NextState,
            Observation(now: 2_400) with { CurrentHp = 50_000 });
        True(safeAgain.NextState.EpisodeOpen, "unknown interruption restarts clear grace");
        var finallyClear = EmergencyTeleportRules.Observe(
            safeAgain.NextState,
            Observation(now: 2_700) with { CurrentHp = 50_000 });
        False(finallyClear.NextState.EpisodeOpen, "new full known-safe grace closes");
    }

    public static void EveryStaticAndHeldGateFailsClosed()
    {
        Cancel(Observation(now: 1_000) with { ConfigurationEnabled = false }, EmergencyTeleportDecisionReason.ConfigurationDisabled);
        Cancel(Observation(now: 1_000) with { Context = SupportedPvPContext.None }, EmergencyTeleportDecisionReason.OutsideSupportedPvPContext);
        Cancel(Observation(now: 1_000) with { LocalPlayer = default }, EmergencyTeleportDecisionReason.LocalPlayerIdentityInvalid);
        Cancel(Observation(now: 1_000) with { IsLocalPlayerAlive = false }, EmergencyTeleportDecisionReason.LocalPlayerDead);
        Cancel(Observation(now: 1_000) with { IsLocalPlayerTargetable = false }, EmergencyTeleportDecisionReason.LocalPlayerUntargetable);
        Cancel(Observation(now: 1_000) with { LocalJobId = 19 }, EmergencyTeleportDecisionReason.LocalJobUnsupported);
        Cancel(Observation(now: 1_000) with { MetadataVerified = false }, EmergencyTeleportDecisionReason.MetadataUnverified);

        Block(Observation(now: 1_000) with { InputProbeSucceeded = false }, EmergencyTeleportDecisionReason.InputProbeUnavailable);
        Block(Observation(now: 1_000) with { IsTextInputActive = true }, EmergencyTeleportDecisionReason.TextInputActive);
        Block(Observation(now: 1_000) with { ActionHelpersSuppressedByGuard = true }, EmergencyTeleportDecisionReason.GuardSuppressed);
        Block(Observation(now: 1_000) with { HigherPriorityClaimed = true }, EmergencyTeleportDecisionReason.HigherPriorityClaimed);
        Block(Observation(now: 1_000) with { HeldGameplayKeyEligible = false }, EmergencyTeleportDecisionReason.NoHeldGameplayKey);
        Block(Observation(now: 1_000) with { ResolvedActionId = EmergencyTeleportRules.IcarusActionId }, EmergencyTeleportDecisionReason.ResolvedActionInvalid);
        Block(Observation(now: 1_000) with { ActionLocallyReady = false }, EmergencyTeleportDecisionReason.ActionNotReady);
        Block(Observation(now: 1_000) with { Candidates = [] }, EmergencyTeleportDecisionReason.NoExactSafeDestination);

        var wolves = EmergencyTeleportRules.Observe(
            EmergencyTeleportState.Initial,
            Observation(now: 1_000) with { Context = SupportedPvPContext.WolvesDen });
        Equal(EmergencyTeleportDecisionKind.Dispatch, wolves.Kind, "Wolves Den uses identical exact gates");
    }

    private static EmergencyTeleportObservation Observation(long now) => new(
        ConfigurationEnabled: true,
        Settings: EmergencyTeleportSettings.Default,
        Context: SupportedPvPContext.CrystallineConflict,
        LocalPlayer,
        IsLocalPlayerAlive: true,
        IsLocalPlayerTargetable: true,
        LocalJobId: EmergencyTeleportRules.ViperJobId,
        MetadataVerified: true,
        ActionHelpersSuppressedByGuard: false,
        HigherPriorityClaimed: false,
        InputProbeSucceeded: true,
        IsTextInputActive: false,
        HeldGameplayKeyEligible: true,
        HeldGameplayKeyCode: 0x57,
        FrozenKeyStillDown: true,
        ResolvedActionId: EmergencyTeleportRules.SlitherActionId,
        ActionLocallyReady: true,
        NativeBoundaryReady: true,
        CurrentHp: 49_999,
        MaximumHp: 100_000,
        CurrentMp: 3_999,
        MaximumMp: 10_000,
        DirectPressureKnown: true,
        DirectEnemyCount: 1,
        PressurePublishedAtMilliseconds: now,
        Candidates: [Candidate(FirstAlly, slot: 2, travel: 20f, nearby: 0, clearance: 15f)],
        NowMilliseconds: now);

    private static EmergencyTeleportCandidate Candidate(
        TargetPressureActorIdentity actor,
        int slot,
        float travel,
        int nearby,
        float clearance) => new(
        actor,
        slot,
        CurrentHp: 50_000,
        MaximumHp: 50_000,
        travel,
        nearby,
        clearance,
        IsExactPartyMember: true,
        IsSelf: false,
        IsAlive: true,
        IsTargetable: true,
        HasValidNativeTarget: true,
        HasValidActionTarget: true,
        HasNativeRangeAndLineOfSight: true,
        HasCompleteEnemySnapshot: true);

    private static void Mapping(uint jobId, uint actionId)
    {
        True(EmergencyTeleportRules.TryGetActionForJob(jobId, out var actual), $"job {jobId} mapped");
        Equal(actionId, actual, $"job {jobId} action");
        True(EmergencyTeleportRules.IsExactJobAction(jobId, actionId), $"job {jobId} exact pair");
    }

    private static void Cancel(
        EmergencyTeleportObservation observation,
        EmergencyTeleportDecisionReason expected)
    {
        var decision = EmergencyTeleportRules.Observe(EmergencyTeleportState.Initial, observation);
        Equal(EmergencyTeleportDecisionKind.Cancelled, decision.Kind, $"cancel {expected}");
        Equal(expected, decision.Reason, $"cancel reason {expected}");
        False(decision.ShouldDispatch, $"cancelled {expected} cannot dispatch");
    }

    private static void Block(
        EmergencyTeleportObservation observation,
        EmergencyTeleportDecisionReason expected)
    {
        var decision = EmergencyTeleportRules.Observe(EmergencyTeleportState.Initial, observation);
        Equal(EmergencyTeleportDecisionKind.None, decision.Kind, $"block {expected}");
        Equal(expected, decision.Reason, $"block reason {expected}");
        False(decision.ShouldDispatch, $"blocked {expected} cannot dispatch");
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
