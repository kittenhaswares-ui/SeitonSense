using SeitonSense.Core;

internal static class FarHelpFallbackSuppressionSelfTests
{
    private const uint ActionType = 1;
    private const uint MovementAction = 29660;

    public static void ExactFollowingActionIsSuppressedThroughQuarantine()
    {
        var armed = Arm();
        var suppressed = FarHelpFallbackSuppressionRules.Observe(
            armed,
            Attempt(MovementAction, 1_001));

        True(suppressed.ShouldSuppress, "exact immediate fallback is suppressed");
        True(suppressed.NextState.IsArmed, "quarantine remains armed");

        var following = FarHelpFallbackSuppressionRules.Observe(
            suppressed.NextState,
            Attempt(7, 1_002) with { ActionType = 14, ResolvedActionId = MovementAction });
        True(following.ShouldSuppress, "raw-id and action-type variants of the same adjusted action are suppressed");
        True(following.NextState.IsArmed, "quarantine remains bounded by time");
    }

    public static void UnrelatedActionsCannotConsumeSuppression()
    {
        var armed = Arm();
        var unrelated = FarHelpFallbackSuppressionRules.Observe(
            armed,
            Attempt(29261, 1_001));
        Equal(FarHelpFallbackSuppressionDecisionKind.Waiting, unrelated.Kind, "unrelated action waits");
        True(unrelated.NextState.IsArmed, "unrelated action preserves suppression");

        var transformed = FarHelpFallbackSuppressionRules.Observe(
            unrelated.NextState,
            Attempt(MovementAction, 1_002) with { ResolvedActionId = 39184 });
        Equal(FarHelpFallbackSuppressionDecisionKind.Waiting, transformed.Kind, "different resolved action waits");
        True(transformed.NextState.IsArmed, "different resolved action preserves suppression");

        var exact = FarHelpFallbackSuppressionRules.Observe(
            transformed.NextState,
            Attempt(MovementAction, 1_003));
        True(exact.ShouldSuppress, "later exact action is suppressed regardless of invocation mode");
    }

    public static void InvalidClockAndExpiryClearWithoutSuppressing()
    {
        False(
            FarHelpFallbackSuppressionRules.Arm(ActionType, 0, MovementAction, 1_000).IsArmed,
            "zero action cannot arm");
        False(
            FarHelpFallbackSuppressionRules.Arm(ActionType, MovementAction, 0, 1_000).IsArmed,
            "zero resolved action cannot arm");
        False(
            FarHelpFallbackSuppressionRules.Arm(ActionType, MovementAction, MovementAction, -1).IsArmed,
            "negative clock cannot arm");

        var backwards = FarHelpFallbackSuppressionRules.Observe(
            Arm(),
            Attempt(MovementAction, 999));
        Equal(FarHelpFallbackSuppressionDecisionKind.Cleared, backwards.Kind, "backward clock clears");
        False(backwards.ShouldSuppress, "backward clock does not suppress");

        var expired = FarHelpFallbackSuppressionRules.Observe(
            Arm(),
            Attempt(MovementAction, 1_750));
        Equal(FarHelpFallbackSuppressionDecisionKind.Cleared, expired.Kind, "deadline is exclusive");
        False(expired.ShouldSuppress, "expired token does not suppress");
    }

    private static FarHelpFallbackSuppressionState Arm() =>
        FarHelpFallbackSuppressionRules.Arm(
            ActionType,
            MovementAction,
            MovementAction,
            1_000);

    private static FarHelpFallbackSuppressionAttempt Attempt(uint actionId, long now) =>
        new(ActionType, actionId, actionId, now);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
