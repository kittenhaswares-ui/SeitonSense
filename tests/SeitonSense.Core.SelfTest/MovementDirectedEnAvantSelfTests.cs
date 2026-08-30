using SeitonSense.Core;

internal static class MovementDirectedEnAvantSelfTests
{
    public static void CardinalAndDiagonalWorldHeadingsAreExact()
    {
        var cases = new (float DeltaX, float DeltaZ, float ExpectedHeading, string Name)[]
        {
            (0f, 0.02f, 0f, "north"),
            (0.02f, 0.02f, MathF.PI / 4f, "north-east"),
            (0.02f, 0f, MathF.PI / 2f, "east"),
            (0.02f, -0.02f, 3f * MathF.PI / 4f, "south-east"),
            (0f, -0.02f, MathF.PI, "south"),
            (-0.02f, -0.02f, -3f * MathF.PI / 4f, "south-west"),
            (-0.02f, 0f, -MathF.PI / 2f, "west"),
            (-0.02f, 0.02f, -MathF.PI / 4f, "north-west"),
        };

        foreach (var item in cases)
        {
            var state = MovementDirectedEnAvantState.Initial;
            state = MovementDirectedEnAvantRules.Observe(
                state,
                Sample(0f, 0f, 1_000));
            state = MovementDirectedEnAvantRules.Observe(
                state,
                Sample(item.DeltaX, item.DeltaZ, 1_050));
            state = MovementDirectedEnAvantRules.Observe(
                state,
                Sample(2f * item.DeltaX, 2f * item.DeltaZ, 1_100));

            True(state.HasDirection, $"{item.Name} has two consistent segments");
            AngleNear(
                item.ExpectedHeading,
                state.HeadingRadians,
                0.0001f,
                $"{item.Name} uses the world displacement heading");
        }
    }

    public static void ExactlyTwoSegmentsAndFreshnessBoundaryAreRequired()
    {
        Equal(29_430u, MovementDirectedEnAvantRules.ActionId, "En Avant action ID");
        Equal(2, MovementDirectedEnAvantRules.RequiredConsistentSegmentCount, "segment gate");
        Equal(150L, MovementDirectedEnAvantRules.MaximumSampleGapMilliseconds, "sample gap");
        Equal(150L, MovementDirectedEnAvantRules.MaximumDirectionAgeMilliseconds, "direction age");

        var fingerprint = Fingerprint();
        var state = MovementDirectedEnAvantRules.Observe(
            MovementDirectedEnAvantState.Initial,
            Sample(0f, 0f, 1_000));
        state = MovementDirectedEnAvantRules.Observe(
            state,
            Sample(0f, 0.03f, 1_150));

        Equal(1, state.ConsistentSegmentCount, "first segment count");
        False(
            MovementDirectedEnAvantRules.TryCapture(state, fingerprint, 1_150, out _),
            "one long-enough segment cannot supply a direction");

        state = MovementDirectedEnAvantRules.Observe(
            state,
            Sample(0f, 0.06f, 1_300));
        Equal(2, state.ConsistentSegmentCount, "second segment count");
        True(
            MovementDirectedEnAvantRules.TryCapture(state, fingerprint, 1_450, out var snapshot),
            "exact freshness boundary is accepted");
        True(
            MovementDirectedEnAvantRules.IsFreshSnapshot(snapshot, 1_450),
            "the native boundary may recheck the exact freshness edge");
        Equal(1_300L, snapshot.ObservedAtMilliseconds, "snapshot freezes last movement time");
        Equal(2, snapshot.ConsistentSegmentCount, "snapshot freezes exact proof count");
        False(
            MovementDirectedEnAvantRules.TryCapture(state, fingerprint, 1_451, out _),
            "one millisecond past freshness fails closed");
        False(
            MovementDirectedEnAvantRules.IsFreshSnapshot(snapshot, 1_451),
            "the native boundary rejects an expired frozen snapshot");
        False(
            MovementDirectedEnAvantRules.TryCapture(state, fingerprint, 1_299, out _),
            "a clock before the last movement fails closed");
    }

    public static void StationaryStaleAndDiscontinuousSamplesFailClosed()
    {
        var fingerprint = Fingerprint();
        var stationary = MovementDirectedEnAvantRules.Observe(
            MovementDirectedEnAvantState.Initial,
            Sample(4f, 9f, 1_000));
        stationary = MovementDirectedEnAvantRules.Observe(
            stationary,
            Sample(4f, 9f, 1_050));
        stationary = MovementDirectedEnAvantRules.Observe(
            stationary,
            Sample(4f, 9f, 1_100));
        False(stationary.HasDirection, "stationary samples never manufacture a direction");
        False(
            MovementDirectedEnAvantRules.TryCapture(stationary, fingerprint, 1_100, out _),
            "stationary-only history cannot be captured");

        var intermittent = MovementDirectedEnAvantRules.Observe(
            MovementDirectedEnAvantState.Initial,
            Sample(0f, 0f, 2_000));
        intermittent = MovementDirectedEnAvantRules.Observe(
            intermittent,
            Sample(0f, 0.02f, 2_025));
        intermittent = MovementDirectedEnAvantRules.Observe(
            intermittent,
            Sample(0f, 0.02f, 2_050));
        Equal(1, intermittent.ConsistentSegmentCount, "one repeated render position retains partial proof");
        intermittent = MovementDirectedEnAvantRules.Observe(
            intermittent,
            Sample(0f, 0.04f, 2_075));
        True(intermittent.HasDirection, "intermittent position updates still prove current movement");

        var fresh = ReadyState();
        var freshStationary = MovementDirectedEnAvantRules.Observe(
            fresh,
            Sample(0f, 0.06f, 1_450));
        True(freshStationary.HasDirection, "fresh direction survives at the exact stationary age boundary");
        True(
            MovementDirectedEnAvantRules.TryCapture(
                freshStationary,
                fingerprint,
                1_450,
                out _),
            "stationarity retains only the still-fresh movement proof");

        var staleStationary = MovementDirectedEnAvantRules.Observe(
            freshStationary,
            Sample(0f, 0.06f, 1_451));
        False(staleStationary.HasDirection, "continued stationarity expires the direction");
        False(
            MovementDirectedEnAvantRules.TryCapture(staleStationary, fingerprint, 1_451, out _),
            "expired stationary history exposes no fallback");

        var delayed = MovementDirectedEnAvantRules.Observe(
            fresh,
            Sample(0f, 0.09f, 1_451));
        Equal(0, delayed.ConsistentSegmentCount, "sample-gap discontinuity starts a new baseline");
        False(delayed.HasDirection, "sample-gap discontinuity clears the old direction");

        var duplicateClock = MovementDirectedEnAvantRules.Observe(
            fresh,
            Sample(0f, 0.09f, 1_300));
        False(duplicateClock.HasDirection, "duplicate observation time breaks continuity");
        var regressedClock = MovementDirectedEnAvantRules.Observe(
            fresh,
            Sample(0f, 0.09f, 1_299));
        False(regressedClock.HasDirection, "regressed observation time breaks continuity");
    }

    public static void TeleportAndNonFiniteSamplesFailClosed()
    {
        var fingerprint = Fingerprint();
        var fresh = ReadyState();
        var maximumSegment = MovementDirectedEnAvantRules.Observe(
            fresh,
            Sample(0f, 1.56f, 1_350));
        True(maximumSegment.HasDirection, "exact maximum segment distance remains continuous");

        var teleport = MovementDirectedEnAvantRules.Observe(
            fresh,
            Sample(0f, 1.5601f, 1_350));
        False(teleport.HasDirection, "teleport-sized displacement clears direction");
        False(
            MovementDirectedEnAvantRules.TryCapture(teleport, fingerprint, 1_350, out _),
            "teleport exposes no fallback snapshot");

        foreach (var sample in new[]
                 {
                     Sample(float.NaN, 0.06f, 1_350),
                     Sample(0f, float.PositiveInfinity, 1_350),
                     Sample(0f, 0.06f, -1),
                 })
        {
            var rejected = MovementDirectedEnAvantRules.Observe(fresh, sample);
            Equal(
                MovementDirectedEnAvantState.Initial,
                rejected,
                "invalid samples clear the complete movement state");
            False(
                MovementDirectedEnAvantRules.TryCapture(rejected, fingerprint, 1_350, out _),
                "invalid samples expose no direction");
        }

        var forged = fresh with { HeadingRadians = float.NaN };
        False(
            MovementDirectedEnAvantRules.TryCapture(forged, fingerprint, 1_300, out _),
            "non-finite stored heading fails closed");
    }

    public static void FingerprintDriftInvalidatesDirectionAndSnapshot()
    {
        var fingerprint = Fingerprint();
        var state = ReadyState();
        True(
            MovementDirectedEnAvantRules.TryCapture(state, fingerprint, 1_300, out var snapshot),
            "stable exact identity captures");
        True(
            MovementDirectedEnAvantRules.MatchesCurrentIdentity(
                snapshot,
                fingerprint.TerritoryId,
                fingerprint.LocalActorAddress,
                fingerprint.LocalGameObjectId,
                fingerprint.LocalEntityId,
                fingerprint.LocalJobId),
            "snapshot matches the complete frozen identity");

        var drifted = new[]
        {
            fingerprint with { TerritoryId = fingerprint.TerritoryId + 1 },
            fingerprint with { LocalActorAddress = fingerprint.LocalActorAddress + 1 },
            fingerprint with { LocalGameObjectId = fingerprint.LocalGameObjectId + 1 },
            fingerprint with { LocalEntityId = fingerprint.LocalEntityId + 1 },
            fingerprint with { LocalJobId = fingerprint.LocalJobId + 1 },
        };

        foreach (var current in drifted)
        {
            False(
                MovementDirectedEnAvantRules.TryCapture(state, current, 1_300, out _),
                "current fingerprint drift cannot capture an old direction");
            False(
                MovementDirectedEnAvantRules.MatchesCurrentIdentity(
                    snapshot,
                    current.TerritoryId,
                    current.LocalActorAddress,
                    current.LocalGameObjectId,
                    current.LocalEntityId,
                    current.LocalJobId),
                "frozen snapshot cannot match a drifted fingerprint");

            var reset = MovementDirectedEnAvantRules.Observe(
                state,
                Sample(0f, 0.09f, 1_350, current));
            Equal(0, reset.ConsistentSegmentCount, "identity drift starts a fresh baseline");
            False(reset.HasDirection, "identity drift clears the old direction");
        }
    }

    private static MovementDirectedEnAvantState ReadyState()
    {
        var state = MovementDirectedEnAvantRules.Observe(
            MovementDirectedEnAvantState.Initial,
            Sample(0f, 0f, 1_000));
        state = MovementDirectedEnAvantRules.Observe(
            state,
            Sample(0f, 0.03f, 1_150));
        return MovementDirectedEnAvantRules.Observe(
            state,
            Sample(0f, 0.06f, 1_300));
    }

    private static MovementDirectedEnAvantSample Sample(
        float x,
        float z,
        long observedAtMilliseconds,
        MovementDirectedEnAvantFingerprint? fingerprint = null) =>
        new(fingerprint ?? Fingerprint(), x, z, observedAtMilliseconds);

    private static MovementDirectedEnAvantFingerprint Fingerprint() =>
        new(
            TerritoryId: 1_031,
            LocalActorAddress: 0x1234,
            LocalGameObjectId: 0x5678,
            LocalEntityId: 0x9ABC,
            LocalJobId: BackwardDashRules.DancerJobId);

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

    private static void AngleNear(
        float expected,
        float actual,
        float tolerance,
        string message)
    {
        if (!float.IsFinite(actual))
            throw new InvalidOperationException($"{message}: heading is not finite");

        var difference = MathF.Abs(MathF.IEEERemainder(actual - expected, 2f * MathF.PI));
        if (difference > tolerance)
        {
            throw new InvalidOperationException(
                $"{message}: expected {expected} +/- {tolerance}, got {actual}");
        }
    }
}
