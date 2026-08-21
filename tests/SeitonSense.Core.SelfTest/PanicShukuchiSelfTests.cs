using SeitonSense.Core;

internal static class PanicShukuchiSelfTests
{
    public static void ConstantsAndForwardAxesAreExact()
    {
        Equal(30u, PanicShukuchiRules.NinjaJobId, "NIN job ID");
        Equal(29_513u, PanicShukuchiRules.ActionId, "PvP Shukuchi action ID");
        Equal(20f, PanicShukuchiRules.NativeMaximumRangeYalms, "native action range");
        Equal(19.5f, PanicShukuchiRules.SafeForwardDistanceYalms, "safe destination range");

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
            "a ground hit behind the current facing is rejected");
    }

    public static void ValidCommandProducesOneImmediateIntent()
    {
        var decision = PanicShukuchiRules.Evaluate(ValidObservation());

        True(decision.ShouldAttempt, "valid command produces an immediate native intent");
        Equal(PanicShukuchiDecisionReason.Ready, decision.Reason, "ready reason");
        True(decision.Intent is { IsValid: true }, "one exact intent is present");
        Equal(PanicShukuchiRules.ActionId, decision.Intent!.Value.ActionId, "exact action ID");
        Equal(
            new PanicShukuchiPoint(0f, 0f, 19.5f),
            decision.Intent.Value.Destination,
            "exact destination");
    }

    public static void RepeatedCommandsAreIndependent()
    {
        var first = PanicShukuchiRules.Evaluate(ValidObservation());
        var second = PanicShukuchiRules.Evaluate(
            ValidObservation() with
            {
                Candidate = new PanicShukuchiCandidate(
                    new PanicShukuchiPoint(0f, 0f, 0f),
                    MathF.PI / 2f,
                    new PanicShukuchiGroundHit(
                        true,
                        new PanicShukuchiPoint(19.5f, 0f, 0f))),
            });

        True(first.ShouldAttempt, "first command is immediate");
        True(second.ShouldAttempt, "second command has no pending predecessor");
        Equal(
            new PanicShukuchiPoint(0f, 0f, 19.5f),
            first.Intent!.Value.Destination,
            "first command keeps its own point");
        Equal(
            new PanicShukuchiPoint(19.5f, 0f, 0f),
            second.Intent!.Value.Destination,
            "second command computes its own point");
    }

    public static void CommandPolicyHasNoGuardOrSchedulerInputs()
    {
        var observationNames = typeof(PanicShukuchiCommandObservation)
            .GetProperties()
            .Select(static property => property.Name)
            .ToArray();
        var forbidden = new[]
        {
            "Guard",
            "Purify",
            "CrowdControl",
            "Incapacitated",
            "Cast",
            "Queue",
            "Animation",
            "Cooldown",
            "Resource",
            "Pending",
            "Wait",
            "Time",
            "Expiry",
        };

        foreach (var token in forbidden)
        {
            False(
                observationNames.Any(name =>
                    name.Contains(token, StringComparison.OrdinalIgnoreCase)),
                $"command policy must not expose {token} input");
        }

        var reasonNames = Enum.GetNames<PanicShukuchiDecisionReason>();
        foreach (var token in new[] { "Guard", "Purify", "Pending", "Waiting", "Expired" })
        {
            False(
                reasonNames.Any(name =>
                    name.Contains(token, StringComparison.OrdinalIgnoreCase)),
                $"command policy must not expose {token} outcome");
        }
    }

    public static void StaticCommandGatesFailClosed()
    {
        Rejected(
            ValidObservation() with { PluginEnabled = false },
            PanicShukuchiDecisionReason.PluginDisabled);
        Rejected(
            ValidObservation() with { MetadataVerified = false },
            PanicShukuchiDecisionReason.MetadataUnverified);
        Rejected(
            ValidObservation() with { Context = SupportedPvPContext.None },
            PanicShukuchiDecisionReason.UnsupportedContext);
        Rejected(
            ValidObservation() with { LocalPlayerAliveAndTargetable = false },
            PanicShukuchiDecisionReason.InvalidLocalPlayer);
        Rejected(
            ValidObservation() with { LocalJobId = 31 },
            PanicShukuchiDecisionReason.WrongJob);

        var wolvesBlocked = PanicShukuchiRules.Evaluate(
            ValidObservation() with
            {
                Context = SupportedPvPContext.WolvesDen,
                WolvesDenTestingEnabled = false,
            });
        False(wolvesBlocked.ShouldAttempt, "Wolves' Den needs explicit test consent");
        var wolvesAllowed = PanicShukuchiRules.Evaluate(
            ValidObservation() with
            {
                Context = SupportedPvPContext.WolvesDen,
                WolvesDenTestingEnabled = true,
            });
        True(wolvesAllowed.ShouldAttempt, "Wolves' Den command is immediate with test consent");
    }

    public static void InvalidActionOrTerrainExposesNoFallback()
    {
        Rejected(
            ValidObservation() with { ResolvedActionId = 29_514 },
            PanicShukuchiDecisionReason.ResolvedActionInvalid);
        Rejected(
            ValidObservation() with
            {
                Candidate = Candidate(new PanicShukuchiPoint(0f, 0f, 18f)),
            },
            PanicShukuchiDecisionReason.InvalidForwardGroundHit);
        Rejected(
            ValidObservation() with
            {
                Candidate = Candidate(new PanicShukuchiPoint(float.NaN, 0f, 19.5f)),
            },
            PanicShukuchiDecisionReason.InvalidForwardGroundHit);
    }

    private static PanicShukuchiCommandObservation ValidObservation() => new(
        PluginEnabled: true,
        MetadataVerified: true,
        Context: SupportedPvPContext.CrystallineConflict,
        WolvesDenTestingEnabled: false,
        LocalJobId: PanicShukuchiRules.NinjaJobId,
        LocalPlayerAliveAndTargetable: true,
        ResolvedActionId: PanicShukuchiRules.ActionId,
        Candidate: Candidate(new PanicShukuchiPoint(0f, 0f, 19.5f)));

    private static PanicShukuchiCandidate Candidate(PanicShukuchiPoint hit) => new(
        Origin: new PanicShukuchiPoint(0f, 0f, 0f),
        RotationRadians: 0f,
        GroundHit: new PanicShukuchiGroundHit(true, hit));

    private static void Rejected(
        PanicShukuchiCommandObservation observation,
        PanicShukuchiDecisionReason expectedReason)
    {
        var decision = PanicShukuchiRules.Evaluate(observation);
        False(decision.ShouldAttempt, expectedReason.ToString());
        Equal(expectedReason, decision.Reason, "command refusal reason");
        True(decision.Intent is null, "refused command exposes no alternate intent");
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
