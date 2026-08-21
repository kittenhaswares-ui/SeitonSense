using SeitonSense.Core;

internal static class PanicShukuchiSelfTests
{
    private static readonly TargetPressureActorIdentity LocalA = new(0x100, 0x200);
    private static readonly TargetPressureActorIdentity LocalB = new(0x101, 0x201);

    public static void ConstantsAndForwardAxesAreExact()
    {
        Equal(30u, PanicShukuchiRules.NinjaJobId, "NIN job ID");
        Equal(29_513u, PanicShukuchiRules.ActionId, "PvP Shukuchi action ID");
        Equal(20f, PanicShukuchiRules.NativeMaximumRangeYalms, "native action range");
        Equal(19.5f, PanicShukuchiRules.SafeForwardDistanceYalms, "safe destination range");
        Equal(500L, PanicShukuchiRules.MaximumPendingMilliseconds, "maximum pending lease");

        var origin = new PanicShukuchiPoint(10f, 3f, -4f);
        True(
            PanicShukuchiRules.TryCreateForwardProbe(origin, 0f, out var north),
            "zero rotation has a finite forward point");
        Near(10f, north.X, 0.0001f, "zero rotation keeps X");
        Near(3f, north.Y, 0.0001f, "probe keeps origin Y");
        Near(15.5f, north.Z, 0.0001f, "zero rotation advances positive Z");

        True(
            PanicShukuchiRules.TryCreateForwardProbe(origin, MathF.PI / 2f, out var east),
            "quarter turn has a finite forward point");
        Near(29.5f, east.X, 0.0001f, "quarter turn advances positive X");
        Near(-4f, east.Z, 0.0001f, "quarter turn keeps Z");

        True(
            PanicShukuchiRules.TryCreateForwardProbe(origin, -MathF.PI / 2f, out var west),
            "negative quarter turn has a finite forward point");
        Near(-9.5f, west.X, 0.0001f, "negative quarter turn advances negative X");

        False(
            PanicShukuchiRules.TryCreateForwardProbe(
                origin with { X = float.NaN },
                0f,
                out _),
            "non-finite origin fails closed");
        False(
            PanicShukuchiRules.TryCreateForwardProbe(origin, float.PositiveInfinity, out _),
            "non-finite rotation fails closed");
        False(
            PanicShukuchiRules.TryCreateForwardProbe(
                new PanicShukuchiPoint(float.MaxValue, 0f, float.MaxValue),
                0f,
                out _),
            "finite coordinates that erase the displacement fail closed");
    }

    public static void SupportedContextsAreExact()
    {
        True(
            PanicShukuchiRules.IsSupportedContext(
                SupportedPvPContext.CrystallineConflict,
                wolvesDenTestingEnabled: false),
            "CC never needs the test toggle");
        False(
            PanicShukuchiRules.IsSupportedContext(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: false),
            "Wolves' Den fails closed without testing consent");
        True(
            PanicShukuchiRules.IsSupportedContext(
                SupportedPvPContext.WolvesDen,
                wolvesDenTestingEnabled: true),
            "Wolves' Den is supported only with testing consent");
        False(
            PanicShukuchiRules.IsSupportedContext(
                SupportedPvPContext.None,
                wolvesDenTestingEnabled: true),
            "ordinary territory is never supported");
        False(
            PanicShukuchiRules.IsSupportedContext(
                (SupportedPvPContext)99,
                wolvesDenTestingEnabled: true),
            "unknown context fails closed");
    }

    public static void GroundHitMustBeExactForwardFiniteAndInRange()
    {
        var valid = Candidate(new PanicShukuchiPoint(0f, 0f, 19.5f));
        True(PanicShukuchiRules.IsValidGroundHit(valid), "exact forward ground hit is valid");
        True(
            PanicShukuchiRules.IsValidGroundHit(
                Candidate(new PanicShukuchiPoint(0f, 4f, 19.5f))),
            "ground Y may follow a slope while remaining inside native range");

        False(
            PanicShukuchiRules.IsValidGroundHit(
                valid with { GroundHit = valid.GroundHit with { ExactGroundHit = false } }),
            "missing collision proof fails closed");
        False(
            PanicShukuchiRules.IsValidGroundHit(
                Candidate(new PanicShukuchiPoint(0.051f, 0f, 19.5f))),
            "lateral ground drift cannot redirect the command");
        False(
            PanicShukuchiRules.IsValidGroundHit(
                Candidate(new PanicShukuchiPoint(0f, 0f, 19.44f))),
            "a shorter hit is not an inward fallback");
        False(
            PanicShukuchiRules.IsValidGroundHit(
                Candidate(new PanicShukuchiPoint(0f, 5f, 19.5f))),
            "three-dimensional point outside native range fails closed");
        False(
            PanicShukuchiRules.IsValidGroundHit(
                Candidate(new PanicShukuchiPoint(0f, float.NaN, 19.5f))),
            "non-finite ground height fails closed");
        False(
            PanicShukuchiRules.IsValidGroundHit(
                valid with { RotationRadians = MathF.PI }),
            "a ground hit behind the snapshotted facing is rejected");
    }

    public static void ArmFreezesExactIdentityContextAndDestination()
    {
        var arm = PanicShukuchiRules.Arm(
            PanicShukuchiPendingState.Initial,
            ValidArm());

        True(arm.DidArm, "valid command arms one pending lease");
        Equal(PanicShukuchiDecisionReason.Armed, arm.Reason, "arm reason");
        True(arm.NextState.IsPending, "armed state is valid");
        var pending = arm.NextState.Pending!.Value;
        Equal(LocalA, pending.LocalPlayer, "local identity is frozen");
        Equal(1_032u, pending.TerritoryId, "territory is frozen");
        Equal(SupportedPvPContext.CrystallineConflict, pending.Context, "context is frozen");
        Equal(new PanicShukuchiPoint(0f, 0f, 19.5f), pending.Intent.Destination, "destination is frozen");
        Equal(1_000L, pending.ArmedAtMilliseconds, "arm time is exact");
        Equal(1_500L, pending.ExpiresAtMilliseconds, "default lease is exactly 500 ms");

        RejectedArm(ValidArm() with { PluginEnabled = false }, PanicShukuchiDecisionReason.PluginDisabled);
        RejectedArm(ValidArm() with { MetadataVerified = false }, PanicShukuchiDecisionReason.MetadataUnverified);
        RejectedArm(ValidArm() with { Context = SupportedPvPContext.None }, PanicShukuchiDecisionReason.UnsupportedContext);
        RejectedArm(ValidArm() with { TerritoryId = 0 }, PanicShukuchiDecisionReason.InvalidLocalPlayer);
        RejectedArm(ValidArm() with { LocalPlayer = default }, PanicShukuchiDecisionReason.InvalidLocalPlayer);
        RejectedArm(ValidArm() with { LocalPlayerAliveAndTargetable = false }, PanicShukuchiDecisionReason.InvalidLocalPlayer);
        RejectedArm(ValidArm() with { LocalJobId = 31 }, PanicShukuchiDecisionReason.WrongJob);
        RejectedArm(ValidArm() with { OwnGuardClear = false }, PanicShukuchiDecisionReason.OwnGuardActiveOrPropagating);
        RejectedArm(ValidArm() with { Incapacitated = true }, PanicShukuchiDecisionReason.Incapacitated);
        RejectedArm(ValidArm() with { ResolvedActionId = 1 }, PanicShukuchiDecisionReason.ResolvedActionInvalid);
        RejectedArm(
            ValidArm() with
            {
                Candidate = Candidate(new PanicShukuchiPoint(0f, 0f, 18f)),
            },
            PanicShukuchiDecisionReason.InvalidForwardGroundHit);

        var wolvesBlocked = PanicShukuchiRules.Arm(
            PanicShukuchiPendingState.Initial,
            ValidArm() with
            {
                Context = SupportedPvPContext.WolvesDen,
                WolvesDenTestingEnabled = false,
            });
        False(wolvesBlocked.DidArm, "Wolves' Den needs explicit test consent");
        var wolvesAllowed = PanicShukuchiRules.Arm(
            PanicShukuchiPendingState.Initial,
            ValidArm() with
            {
                Context = SupportedPvPContext.WolvesDen,
                WolvesDenTestingEnabled = true,
                TerritoryId = PvPMatchRules.WolvesDenPierTerritoryId,
            });
        True(wolvesAllowed.DidArm, "Wolves' Den can arm with test consent");

        foreach (var lifetime in new[] { -1L, 0L, 501L })
        {
            var invalidLifetime = PanicShukuchiRules.Arm(
                PanicShukuchiPendingState.Initial,
                ValidArm(),
                lifetime);
            False(invalidLifetime.DidArm, $"invalid lifetime {lifetime} fails closed");
            Equal(PanicShukuchiDecisionReason.InvalidClock, invalidLifetime.Reason, "lifetime reason");
        }
    }

    public static void ExistingPendingCannotBeReplacedAndExpiryIsExact()
    {
        var state = Arm();
        var replacement = PanicShukuchiRules.Arm(
            state,
            ValidArm(now: 1_100) with
            {
                LocalPlayer = LocalB,
                Candidate = Candidate(new PanicShukuchiPoint(0f, 0.1f, 19.5f)),
            });

        Equal(
            PanicShukuchiArmDecisionKind.ExistingPendingPreserved,
            replacement.Kind,
            "second command is rejected as already pending");
        Equal(PanicShukuchiDecisionReason.AlreadyPending, replacement.Reason, "replacement reason");
        Equal(state, replacement.NextState, "second command preserves every frozen bit");

        var lastInside = PanicShukuchiRules.ObservePending(
            state,
            ValidPending(state, now: 1_499));
        True(lastInside.ShouldAttempt, "lease is usable one millisecond before expiry");

        var boundary = PanicShukuchiRules.ObservePending(
            state,
            ValidPending(state, now: 1_500));
        Equal(PanicShukuchiPendingDecisionKind.Cleared, boundary.Kind, "deadline is expired");
        Equal(PanicShukuchiDecisionReason.Expired, boundary.Reason, "expiry reason");
        False(boundary.NextState.IsPending, "expired lease is cleared");

        var newAtBoundary = PanicShukuchiRules.Arm(state, ValidArm(now: 1_500));
        True(newAtBoundary.DidArm, "a new explicit command may arm after proven expiry");
        Equal(2_000L, newAtBoundary.NextState.Pending!.Value.ExpiresAtMilliseconds, "new lease has its own deadline");

        var rollback = PanicShukuchiRules.Arm(state, ValidArm(now: 999));
        Equal(PanicShukuchiDecisionReason.ClockMovedBackwards, rollback.Reason, "clock rollback reason");
        False(rollback.NextState.IsPending, "clock rollback clears instead of replacing");
    }

    public static void CastQueueAndAnimationLockWaitWithoutDrift()
    {
        var state = Arm();
        var higherPriority = PanicShukuchiRules.ObservePending(
            state,
            ValidPending(state, 1_001) with
            {
                HigherPriorityClaimed = true,
                CooldownStateKnown = false,
                ActionStructurallyReady = false,
            });
        Waiting(
            higherPriority,
            state,
            PanicShukuchiDecisionReason.WaitingForHigherPriority,
            "Purify priority");

        var casting = PanicShukuchiRules.ObservePending(
            higherPriority.NextState,
            ValidPending(state, 1_001) with
            {
                NotCasting = false,
                CooldownStateKnown = false,
                ActionStructurallyReady = false,
            });
        Waiting(casting, state, PanicShukuchiDecisionReason.WaitingForCast, "cast");

        var queued = PanicShukuchiRules.ObservePending(
            casting.NextState,
            ValidPending(state, 1_100) with
            {
                NativeQueueClear = false,
                CooldownReady = false,
            });
        Waiting(queued, state, PanicShukuchiDecisionReason.WaitingForNativeQueue, "native queue");

        var locked = PanicShukuchiRules.ObservePending(
            queued.NextState,
            ValidPending(state, 1_200) with
            {
                AnimationLockClear = false,
                ActionStructurallyReady = false,
            });
        Waiting(locked, state, PanicShukuchiDecisionReason.WaitingForAnimationLock, "animation lock");

        Equal(
            state.Pending!.Value.ExpiresAtMilliseconds,
            locked.NextState.Pending!.Value.ExpiresAtMilliseconds,
            "soft waits never extend the deadline");
        Equal(
            state.Pending.Value.Intent,
            locked.NextState.Pending.Value.Intent,
            "soft waits never recompute the destination");
    }

    public static void ReadyConsumesBeforeTheSoleNativeAttempt()
    {
        var state = Arm();
        var ready = PanicShukuchiRules.ObservePending(state, ValidPending(state, 1_001));

        True(ready.ShouldAttempt, "fully ready pending lease authorizes one attempt");
        Equal(PanicShukuchiDecisionReason.Ready, ready.Reason, "ready reason");
        False(ready.NextState.IsPending, "lease is spent before caller invokes native code");
        Equal(state.Pending!.Value.Intent, ready.Intent!.Value, "sole attempt uses frozen intent");

        // The caller stores NextState before UseActionLocation. A false return or
        // exception therefore has no state from which a retry could be made.
        var afterNativeFalse = PanicShukuchiRules.ObservePending(
            ready.NextState,
            ValidPending(state, 1_002));
        False(afterNativeFalse.ShouldAttempt, "client false cannot retry");
        Equal(PanicShukuchiDecisionReason.NoPending, afterNativeFalse.Reason, "spent lease is absent");
        True(afterNativeFalse.Intent is null, "no fallback intent is exposed");

        var shorter = Arm(candidate: Candidate(new PanicShukuchiPoint(0f, 0f, 18f)));
        False(shorter.IsPending, "invalid shorter fallback never arms");
    }

    public static void PendingDriftAndTerminalGatesFailClosed()
    {
        var state = Arm();
        Cleared(state, ValidPending(state, 1_001) with { PluginEnabled = false }, PanicShukuchiDecisionReason.PluginDisabled);
        Cleared(state, ValidPending(state, 1_001) with { MetadataVerified = false }, PanicShukuchiDecisionReason.MetadataUnverified);
        Cleared(state, ValidPending(state, 1_001) with { Context = SupportedPvPContext.None }, PanicShukuchiDecisionReason.UnsupportedContext);
        Cleared(state, ValidPending(state, 1_001) with { TerritoryId = 1_033 }, PanicShukuchiDecisionReason.TerritoryChanged);
        Cleared(state, ValidPending(state, 1_001) with { LocalPlayer = default }, PanicShukuchiDecisionReason.InvalidLocalPlayer);
        Cleared(state, ValidPending(state, 1_001) with { LocalPlayer = LocalB }, PanicShukuchiDecisionReason.LocalPlayerIdentityChanged);
        Cleared(state, ValidPending(state, 1_001) with { LocalPlayerAliveAndTargetable = false }, PanicShukuchiDecisionReason.InvalidLocalPlayer);
        Cleared(state, ValidPending(state, 1_001) with { LocalJobId = 31 }, PanicShukuchiDecisionReason.WrongJob);
        Cleared(state, ValidPending(state, 1_001) with { OwnGuardClear = false }, PanicShukuchiDecisionReason.OwnGuardActiveOrPropagating);
        Cleared(state, ValidPending(state, 1_001) with { Incapacitated = true }, PanicShukuchiDecisionReason.Incapacitated);
        Cleared(
            state,
            ValidPending(state, 1_001) with
            {
                RequestedDestination = state.Pending!.Value.Intent.Destination with { Y = 0.001f },
            },
            PanicShukuchiDecisionReason.DestinationChanged);
        Cleared(state, ValidPending(state, 1_001) with { ResolvedActionId = 1 }, PanicShukuchiDecisionReason.ResolvedActionInvalid);
        Cleared(state, ValidPending(state, 1_001) with { CooldownStateKnown = false }, PanicShukuchiDecisionReason.CooldownStateUnknown);
        Cleared(state, ValidPending(state, 1_001) with { CooldownReady = false }, PanicShukuchiDecisionReason.ActionNotReady);
        Cleared(state, ValidPending(state, 1_001) with { ActionStructurallyReady = false }, PanicShukuchiDecisionReason.ActionStructurallyUnavailable);
        Cleared(state, ValidPending(state, 1_001) with { HardReset = true }, PanicShukuchiDecisionReason.HardReset);
        Cleared(state, ValidPending(state, 999), PanicShukuchiDecisionReason.ClockMovedBackwards);

        var corrupted = state with
        {
            Pending = state.Pending!.Value with
            {
                Intent = state.Pending.Value.Intent with
                {
                    Destination = state.Pending.Value.Intent.Destination with { X = 1f },
                },
            },
        };
        Cleared(corrupted, ValidPending(state, 1_001), PanicShukuchiDecisionReason.InvalidPending);
    }

    private static PanicShukuchiArmObservation ValidArm(
        long now = 1_000,
        PanicShukuchiCandidate? candidate = null) => new(
        NowMilliseconds: now,
        PluginEnabled: true,
        MetadataVerified: true,
        Context: SupportedPvPContext.CrystallineConflict,
        WolvesDenTestingEnabled: false,
        TerritoryId: 1_032,
        LocalJobId: PanicShukuchiRules.NinjaJobId,
        LocalPlayer: LocalA,
        LocalPlayerAliveAndTargetable: true,
        OwnGuardClear: true,
        Incapacitated: false,
        ResolvedActionId: PanicShukuchiRules.ActionId,
        Candidate: candidate ?? Candidate(new PanicShukuchiPoint(0f, 0f, 19.5f)));

    private static PanicShukuchiPendingState Arm(
        PanicShukuchiCandidate? candidate = null)
    {
        var decision = PanicShukuchiRules.Arm(
            PanicShukuchiPendingState.Initial,
            ValidArm(candidate: candidate));
        return decision.NextState;
    }

    private static PanicShukuchiPendingObservation ValidPending(
        PanicShukuchiPendingState state,
        long now)
    {
        var pending = state.Pending!.Value;
        return new PanicShukuchiPendingObservation(
            NowMilliseconds: now,
            PluginEnabled: true,
            MetadataVerified: true,
            Context: pending.Context,
            WolvesDenTestingEnabled: pending.WolvesDenTestingEnabledAtArm,
            TerritoryId: pending.TerritoryId,
            LocalJobId: PanicShukuchiRules.NinjaJobId,
            LocalPlayer: pending.LocalPlayer,
            LocalPlayerAliveAndTargetable: true,
            OwnGuardClear: true,
            Incapacitated: false,
            HigherPriorityClaimed: false,
            NotCasting: true,
            NativeQueueClear: true,
            AnimationLockClear: true,
            ResolvedActionId: PanicShukuchiRules.ActionId,
            CooldownStateKnown: true,
            CooldownReady: true,
            ActionStructurallyReady: true,
            RequestedDestination: pending.Intent.Destination);
    }

    private static PanicShukuchiCandidate Candidate(PanicShukuchiPoint hit) => new(
        Origin: new PanicShukuchiPoint(0f, 0f, 0f),
        RotationRadians: 0f,
        GroundHit: new PanicShukuchiGroundHit(true, hit));

    private static void RejectedArm(
        PanicShukuchiArmObservation observation,
        PanicShukuchiDecisionReason expectedReason)
    {
        var decision = PanicShukuchiRules.Arm(PanicShukuchiPendingState.Initial, observation);
        False(decision.DidArm, expectedReason.ToString());
        Equal(expectedReason, decision.Reason, "arm failure reason");
        False(decision.NextState.IsPending, "rejected arm has no lease");
    }

    private static void Waiting(
        PanicShukuchiPendingDecision decision,
        PanicShukuchiPendingState expectedState,
        PanicShukuchiDecisionReason expectedReason,
        string label)
    {
        Equal(PanicShukuchiPendingDecisionKind.Waiting, decision.Kind, $"{label} waits");
        Equal(expectedReason, decision.Reason, $"{label} reason");
        Equal(expectedState, decision.NextState, $"{label} preserves frozen state");
        False(decision.ShouldAttempt, $"{label} spends no native attempt");
    }

    private static void Cleared(
        PanicShukuchiPendingState state,
        PanicShukuchiPendingObservation observation,
        PanicShukuchiDecisionReason expectedReason)
    {
        var decision = PanicShukuchiRules.ObservePending(state, observation);
        Equal(PanicShukuchiPendingDecisionKind.Cleared, decision.Kind, expectedReason.ToString());
        Equal(expectedReason, decision.Reason, "terminal failure reason");
        False(decision.NextState.IsPending, "terminal failure clears lease");
        False(decision.ShouldAttempt, "terminal failure makes no native call");
        True(decision.Intent is null, "terminal failure exposes no fallback");
    }

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

    private static void Near(float expected, float actual, float tolerance, string message)
    {
        if (!float.IsFinite(actual) || MathF.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"{message}: expected {expected} +/- {tolerance}, got {actual}");
        }
    }
}
