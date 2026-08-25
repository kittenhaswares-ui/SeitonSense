using SeitonSense.Core;

internal static class HeldCastCancellationSelfTests
{
    private const uint HelperActionId = 29_400;
    private const uint CastActionId = 29_391;
    private const int FrozenKeyCode = 65;

    private static readonly TargetPressureActorIdentity LocalPlayer =
        new(10_001, 1_001);

    private static readonly TargetPressureActorIdentity Target =
        new(10_002, 1_002);

    internal static void CanonicalHelperPriorityOrderIsPinned()
    {
        var expected = new[]
        {
            HeldCastCancellationHelperKind.Purify,
            HeldCastCancellationHelperKind.ReactiveCounterCc,
            HeldCastCancellationHelperKind.AllyRescue,
            HeldCastCancellationHelperKind.Guardian,
            HeldCastCancellationHelperKind.NinjaGuardShukuchi,
            HeldCastCancellationHelperKind.NinjaSeiton,
            HeldCastCancellationHelperKind.ScholarCriticalStrategy,
            HeldCastCancellationHelperKind.DarkKnightPlunge,
            HeldCastCancellationHelperKind.SmartRecuperate,
            HeldCastCancellationHelperKind.EmergencyTeleport,
            HeldCastCancellationHelperKind.Guard,
            HeldCastCancellationHelperKind.PressureEscapeSprint,
            HeldCastCancellationHelperKind.DarkKnightShadowbringer,
        };
        var actual = Enum.GetValues<HeldCastCancellationHelperKind>()
            .Where(static helper => helper != HeldCastCancellationHelperKind.None)
            .OrderBy(static helper => (byte)helper)
            .ToArray();

        Equal(expected.Length, actual.Length, "helper count");
        for (var index = 0; index < expected.Length; index++)
            Equal(expected[index], actual[index], $"priority {index + 1}");
    }

    internal static void ExactRequestIsOncePerObservedCastEpoch()
    {
        var first = HeldCastCancellationRules.Observe(
            HeldCastCancellationState.Initial,
            ReadyObservation());
        Equal(HeldCastCancellationDecisionKind.CancelRequested, first.Kind, "first request");
        True(first.ShouldInvokeNative, "first request invokes native boundary");
        Equal(1UL, first.NextState.LastCastEpochToken, "first cast epoch");

        var duplicate = HeldCastCancellationRules.Observe(
            first.NextState,
            ReadyObservation());
        Equal(
            HeldCastCancellationDecisionKind.WaitingForCastEnd,
            duplicate.Kind,
            "duplicate waits");
        Equal(
            HeldCastCancellationDecisionReason.AlreadyRequested,
            duplicate.Reason,
            "duplicate reason");
        False(duplicate.ShouldInvokeNative, "duplicate never invokes native boundary");

        var ended = HeldCastCancellationRules.Observe(
            duplicate.NextState,
            ReadyObservation() with
            {
                LocalPlayerIsCasting = false,
                CastActionId = 0,
            });
        Equal(HeldCastCancellationDecisionKind.CastEnded, ended.Kind, "clear frame");

        var second = HeldCastCancellationRules.Observe(
            ended.NextState,
            ReadyObservation());
        Equal(HeldCastCancellationDecisionKind.CancelRequested, second.Kind, "second request");
        Equal(2UL, second.NextState.LastCastEpochToken, "second cast epoch");
    }

    internal static void IntentMayBecomeEligibleInsideTheSameCast()
    {
        var observing = HeldCastCancellationRules.Observe(
            HeldCastCancellationState.Initial,
            ReadyObservation() with { PrioritizedInputClaimed = false });
        Equal(HeldCastCancellationDecisionKind.ObservingCast, observing.Kind, "not claimed");
        Equal(
            HeldCastCancellationDecisionReason.NoPrioritizedIntent,
            observing.Reason,
            "not claimed reason");

        var requested = HeldCastCancellationRules.Observe(
            observing.NextState,
            ReadyObservation());
        Equal(HeldCastCancellationDecisionKind.CancelRequested, requested.Kind, "later eligible");
        Equal(
            observing.NextState.LastCastEpochToken,
            requested.NextState.LastCastEpochToken,
            "same cast epoch");
    }

    internal static void OnlyConsistentClearRearmsAndSignalDriftFailsClosed()
    {
        var observing = HeldCastCancellationRules.Observe(
            HeldCastCancellationState.Initial,
            ReadyObservation() with { PrioritizedInputClaimed = false });

        var changed = HeldCastCancellationRules.Observe(
            observing.NextState,
            ReadyObservation() with { CastActionId = CastActionId + 1 });
        Equal(
            HeldCastCancellationDecisionKind.WaitingForCastEnd,
            changed.Kind,
            "changed cast signal waits");
        Equal(
            HeldCastCancellationDecisionReason.CastSignalChangedWithoutClear,
            changed.Reason,
            "changed cast signal reason");

        var partialClear = HeldCastCancellationRules.Observe(
            changed.NextState,
            ReadyObservation() with
            {
                LocalPlayerIsCasting = false,
                CastActionId = CastActionId + 1,
            });
        Equal(
            HeldCastCancellationDecisionReason.CastSignalChangedWithoutClear,
            partialClear.Reason,
            "one clear signal cannot rearm");
        False(partialClear.ShouldInvokeNative, "partial clear stays terminal");

        var clear = HeldCastCancellationRules.Observe(
            partialClear.NextState,
            ReadyObservation() with
            {
                LocalPlayerIsCasting = false,
                CastActionId = 0,
            });
        Equal(HeldCastCancellationDecisionKind.CastEnded, clear.Kind, "consistent clear");

        var rearmed = HeldCastCancellationRules.Observe(
            clear.NextState,
            ReadyObservation());
        Equal(HeldCastCancellationDecisionKind.CancelRequested, rearmed.Kind, "rearmed");

        var identityObserved = HeldCastCancellationRules.Observe(
            HeldCastCancellationState.Initial,
            ReadyObservation() with { PrioritizedInputClaimed = false });
        var alternateLocal = new TargetPressureActorIdentity(20_001, 2_001);
        var identityChanged = HeldCastCancellationRules.Observe(
            identityObserved.NextState,
            ReadyObservation() with
            {
                CurrentLocalPlayer = alternateLocal,
                Request = Request() with { LocalPlayer = alternateLocal },
            });
        Equal(
            HeldCastCancellationDecisionReason.LocalPlayerChanged,
            identityChanged.Reason,
            "local identity drift");

        var identityReturned = HeldCastCancellationRules.Observe(
            identityChanged.NextState,
            ReadyObservation());
        Equal(
            HeldCastCancellationDecisionKind.WaitingForCastEnd,
            identityReturned.Kind,
            "identity drift stays terminal until clear");
        False(identityReturned.ShouldInvokeNative, "identity return cannot rearm");
    }

    internal static void EveryCentralSafetyGateFailsClosed()
    {
        var alternateLocal = new TargetPressureActorIdentity(20_001, 2_001);
        var cases = new (string Label, HeldCastCancellationObservation Observation,
            HeldCastCancellationDecisionReason Reason)[]
        {
            ("hard reset", ReadyObservation() with { HardReset = true },
                HeldCastCancellationDecisionReason.HardReset),
            ("feature", ReadyObservation() with { FeatureEnabled = false },
                HeldCastCancellationDecisionReason.FeatureDisabled),
            ("context", ReadyObservation() with { SupportedContext = false },
                HeldCastCancellationDecisionReason.UnsupportedContext),
            ("text input", ReadyObservation() with { TextInputActive = true },
                HeldCastCancellationDecisionReason.TextInputActive),
            ("guard", ReadyObservation() with { GuardActive = true },
                HeldCastCancellationDecisionReason.GuardActive),
            ("priority", ReadyObservation() with { PrioritizedInputClaimed = false },
                HeldCastCancellationDecisionReason.NoPrioritizedIntent),
            ("request", ReadyObservation() with { Request = null },
                HeldCastCancellationDecisionReason.InvalidRequest),
            ("otherwise ready", ReadyObservation() with { IntentOtherwiseReady = false },
                HeldCastCancellationDecisionReason.IntentNotOtherwiseReady),
            ("key", ReadyObservation() with { FrozenKeyStillDown = false },
                HeldCastCancellationDecisionReason.FrozenKeyReleased),
            ("local validity", ReadyObservation() with { LocalPlayerIdentityValid = false },
                HeldCastCancellationDecisionReason.LocalPlayerInvalid),
            ("current local", ReadyObservation() with { CurrentLocalPlayer = default },
                HeldCastCancellationDecisionReason.LocalPlayerInvalid),
            ("local identity", ReadyObservation() with { CurrentLocalPlayer = alternateLocal },
                HeldCastCancellationDecisionReason.LocalPlayerChanged),
            ("alive", ReadyObservation() with { LocalPlayerAlive = false },
                HeldCastCancellationDecisionReason.LocalPlayerDead),
            ("targetable", ReadyObservation() with { LocalPlayerTargetable = false },
                HeldCastCancellationDecisionReason.LocalPlayerUntargetable),
            ("action identity", ReadyObservation() with { ResolvedHelperActionId = HelperActionId + 1 },
                HeldCastCancellationDecisionReason.ActionIdentityChanged),
            ("cooldown", ReadyObservation() with { HelperActionOffCooldown = false },
                HeldCastCancellationDecisionReason.ActionOnCooldown),
            ("resources", ReadyObservation() with { HelperActionResourcesReady = false },
                HeldCastCancellationDecisionReason.ActionResourcesUnavailable),
            ("managed cast", ReadyObservation() with { LocalPlayerIsCasting = false },
                HeldCastCancellationDecisionReason.CastSignalIncomplete),
            ("native cast", ReadyObservation() with { CastActionId = 0 },
                HeldCastCancellationDecisionReason.CastSignalIncomplete),
            ("queue", ReadyObservation() with { ActionQueued = true },
                HeldCastCancellationDecisionReason.NativeQueueOccupied),
            ("NaN lock", ReadyObservation() with { AnimationLockSeconds = float.NaN },
                HeldCastCancellationDecisionReason.InvalidAnimationLock),
            ("negative lock", ReadyObservation() with { AnimationLockSeconds = -0.001f },
                HeldCastCancellationDecisionReason.InvalidAnimationLock),
            ("busy lock", ReadyObservation() with { AnimationLockSeconds = 0.051f },
                HeldCastCancellationDecisionReason.AnimationLockBusy),
        };

        foreach (var test in cases)
        {
            var decision = HeldCastCancellationRules.Observe(
                HeldCastCancellationState.Initial,
                test.Observation);
            False(decision.ShouldInvokeNative, test.Label);
            Equal(test.Reason, decision.Reason, test.Label);
        }
    }

    internal static void RequestIdentityAndLockBoundaryAreExact()
    {
        True(Request().IsValid, "complete exact request");
        False((Request() with { HelperKind = HeldCastCancellationHelperKind.None }).IsValid,
            "helper kind");
        False((Request() with { HelperActionId = 0 }).IsValid, "helper action");
        False((Request() with { LocalPlayer = default }).IsValid, "local identity");
        False((Request() with { Target = default }).IsValid, "target identity");
        False((Request() with { FrozenKeyCode = 0 }).IsValid, "key");
        False((Request() with { IntentEpochToken = 0 }).IsValid, "intent token");

        var exactMaximum = HeldCastCancellationRules.Observe(
            HeldCastCancellationState.Initial,
            ReadyObservation() with
            {
                AnimationLockSeconds =
                    HeldCastCancellationRules.MaximumCancellationAnimationLockSeconds,
            });
        True(exactMaximum.ShouldInvokeNative, "maximum lock is inclusive");
    }

    internal static void TerminalRequestSurvivesLaterGateChanges()
    {
        var requested = HeldCastCancellationRules.Observe(
            HeldCastCancellationState.Initial,
            ReadyObservation());

        var disabled = HeldCastCancellationRules.Observe(
            requested.NextState,
            ReadyObservation() with
            {
                HardReset = true,
                FeatureEnabled = false,
                Request = null,
            });
        Equal(
            HeldCastCancellationDecisionReason.AlreadyRequested,
            disabled.Reason,
            "request remains terminal");
        False(disabled.ShouldInvokeNative, "no retry after later gate change");
    }

    private static HeldCastCancellationRequest Request() =>
        new(
            HeldCastCancellationHelperKind.SmartRecuperate,
            HelperActionId,
            LocalPlayer,
            Target,
            FrozenKeyCode,
            IntentEpochToken: 7);

    private static HeldCastCancellationObservation ReadyObservation() =>
        new(
            HardReset: false,
            FeatureEnabled: true,
            SupportedContext: true,
            TextInputActive: false,
            GuardActive: false,
            PrioritizedInputClaimed: true,
            IntentOtherwiseReady: true,
            Request: Request(),
            FrozenKeyStillDown: true,
            LocalPlayerIdentityValid: true,
            CurrentLocalPlayer: LocalPlayer,
            LocalPlayerAlive: true,
            LocalPlayerTargetable: true,
            ResolvedHelperActionId: HelperActionId,
            HelperActionOffCooldown: true,
            HelperActionResourcesReady: true,
            LocalPlayerIsCasting: true,
            CastActionId,
            ActionQueued: false,
            AnimationLockSeconds: 0f);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label)
    {
        if (condition) throw new InvalidOperationException($"Expected false: {label}");
    }
}
