using SeitonSense.Core;

internal static class BackwardDashSelfTests
{
    public static void ReviewedJobActionCatalogIsExact()
    {
        var expected = new (uint JobId, uint ActionId, string Name)[]
        {
            (33, 41_506, "Epicycle"),
            (38, 29_430, "En Avant"),
            (22, 29_494, "Elusive Jump"),
            (39, 29_550, "Hell's Ingress"),
            (42, 39_210, "Smudge"),
        };

        Equal(expected.Length, BackwardDashRules.DirectionalProfiles.Count, "profile count");
        foreach (var item in expected)
        {
            True(
                BackwardDashRules.TryGetDirectionalProfile(item.JobId, out var profile),
                $"job {item.JobId} has one reviewed profile");
            Equal(item.ActionId, profile.ActionId, $"job {item.JobId} action");
            Equal(item.Name, profile.Name, $"job {item.JobId} name");
            True(profile.IsValid, $"job {item.JobId} profile is internally valid");
            True(
                BackwardDashRules.IsReviewedDirectionalAction(item.ActionId),
                $"action {item.ActionId} is in the closed Guard allowlist");
        }

        False(
            BackwardDashRules.TryGetDirectionalProfile(30, out _),
            "NIN remains on the separate location-action path");
        False(
            BackwardDashRules.TryGetDirectionalProfile(35, out _),
            "RDM hostile-target Displacement is excluded");
        False(
            BackwardDashRules.IsReviewedDirectionalAction(29_399),
            "BRD hostile-target Repelling Shot is excluded");
        False(
            BackwardDashRules.IsReviewedDirectionalAction(29_551),
            "RPR stored-origin Regress is excluded");
    }

    public static void ForwardAndNativeBackwardHeadingsReachScreenBack()
    {
        True(
            BackwardDashRules.TryResolveActorFacing(
                MathF.PI / 2f,
                BackwardDashMovementKind.ForwardFromActorFacing,
                out var forwardFacing),
            "forward dash heading resolves");
        Near(MathF.PI / 2f, forwardFacing, 0.0001f, "forward dash faces screen-back");

        True(
            BackwardDashRules.TryResolveActorFacing(
                MathF.PI / 2f,
                BackwardDashMovementKind.BackwardFromActorFacing,
                out var nativeBackwardFacing),
            "native backward dash heading resolves");
        Near(-MathF.PI / 2f, nativeBackwardFacing, 0.0001f, "Elusive faces opposite screen-back");

        True(
            BackwardDashRules.AreHeadingsEquivalent(
                MathF.PI,
                -MathF.PI),
            "wrapped half-turn headings compare equal");
    }

    public static void InvalidHeadingsAndUnknownActionsFailClosed()
    {
        False(
            BackwardDashRules.TryResolveActorFacing(
                float.NaN,
                BackwardDashMovementKind.ForwardFromActorFacing,
                out _),
            "non-finite camera heading fails closed");
        False(
            BackwardDashRules.TryResolveActorFacing(
                0f,
                (BackwardDashMovementKind)99,
                out _),
            "unknown movement shape fails closed");
        False(
            BackwardDashRules.AreHeadingsEquivalent(float.PositiveInfinity, 0f),
            "non-finite readback fails closed");
        False(
            BackwardDashRules.IsReviewedDirectionalAction(41_507),
            "AST transformed Retrograde is not a reviewed dispatch action");
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
