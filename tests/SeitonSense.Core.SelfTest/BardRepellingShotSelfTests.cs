using SeitonSense.Core;

internal static class BardRepellingShotSelfTests
{
    public static void ExactIdsAndBasicShotCancellationArePinned()
    {
        Equal(23U, BardRepellingShotRules.BardJobId, "BRD job");
        Equal(29_391U, BardRepellingShotRules.PowerfulShotActionId, "Powerful Shot");
        Equal(29_399U, BardRepellingShotRules.RepellingShotActionId, "Repelling Shot");
        Equal(10, BardRepellingShotRules.NativeRangeYalms, "native range");

        var cancel = BardRepellingShotRules.Evaluate(
            ValidObservation() with
            {
                LocalPlayerIsCasting = true,
                CastActionId = BardRepellingShotRules.PowerfulShotActionId,
                AdjustedCastActionId = BardRepellingShotRules.PowerfulShotActionId,
                NativeBoundaryNearQueueable = false,
            });
        True(cancel.ShouldCancelCast, "exact BRD Powerful Shot may be cancelled");

        var otherCast = BardRepellingShotRules.Evaluate(
            ValidObservation() with
            {
                LocalPlayerIsCasting = true,
                CastActionId = BardRepellingShotRules.PowerfulShotActionId + 1,
                AdjustedCastActionId = BardRepellingShotRules.PowerfulShotActionId + 1,
                NativeBoundaryNearQueueable = false,
            });
        Equal(
            BardRepellingShotDecisionReason.OtherCastActive,
            otherCast.Reason,
            "another cast is never cancelled");

        var adjusted = BardRepellingShotRules.Evaluate(
            ValidObservation() with
            {
                LocalPlayerIsCasting = true,
                CastActionId = BardRepellingShotRules.PowerfulShotActionId,
                AdjustedCastActionId = BardRepellingShotRules.PowerfulShotActionId + 1,
                NativeBoundaryNearQueueable = false,
            });
        Equal(
            BardRepellingShotDecisionReason.OtherCastActive,
            adjusted.Reason,
            "adjusted identity drift cannot inherit cast cancellation");
    }

    public static void EverySafetyGateBlocksBeforeDispatch()
    {
        var valid = ValidObservation();
        True(BardRepellingShotRules.Evaluate(valid).ShouldDispatch, "valid opportunity");

        var blocked = new[]
        {
            valid with { Enabled = false },
            valid with { SupportedContext = false },
            valid with { LocalPlayer = default },
            valid with { LocalJobId = 24 },
            valid with { LocalPlayerAliveAndTargetable = false },
            valid with { MetadataVerified = false },
            valid with { GuardStateKnown = false },
            valid with { GuardActive = true },
            valid with { TextInputStateKnown = false },
            valid with { TextInputActive = true },
            valid with { HigherPriorityClaimed = true },
            valid with { Target = default },
            valid with { TargetResolvedExactly = false },
            valid with { TargetAliveAndTargetable = false },
            valid with { TargetInNativeRangeAndLineOfSight = false },
            valid with { ResolvedActionId = 29_398 },
            valid with { ActionOffCooldown = false },
            valid with { ActionResourcesReady = false },
            valid with { NativeBoundaryNearQueueable = false },
        };

        foreach (var observation in blocked)
        {
            var decision = BardRepellingShotRules.Evaluate(observation);
            False(decision.ShouldDispatch, $"dispatch gate: {decision.Reason}");
            False(decision.ShouldCancelCast, $"cancel gate: {decision.Reason}");
        }
    }

    public static void CastCancellationNeedsVerifiedBasicShotMetadata()
    {
        var observation = ValidObservation() with
        {
            LocalPlayerIsCasting = true,
            CastActionId = BardRepellingShotRules.PowerfulShotActionId,
            AdjustedCastActionId = BardRepellingShotRules.PowerfulShotActionId,
            BasicShotMetadataVerified = false,
            NativeBoundaryNearQueueable = false,
        };

        var decision = BardRepellingShotRules.Evaluate(observation);
        Equal(
            BardRepellingShotDecisionReason.BasicShotMetadataUnverified,
            decision.Reason,
            "unverified cast metadata fails closed");
        False(decision.OwnsOpportunity, "unverified cast cannot be cancelled or dispatched");
    }

    private static BardRepellingShotObservation ValidObservation() => new(
        Enabled: true,
        SupportedContext: true,
        LocalPlayer: new TargetPressureActorIdentity(100, 101),
        LocalJobId: BardRepellingShotRules.BardJobId,
        LocalPlayerAliveAndTargetable: true,
        MetadataVerified: true,
        GuardStateKnown: true,
        GuardActive: false,
        TextInputStateKnown: true,
        TextInputActive: false,
        HigherPriorityClaimed: false,
        Target: new TargetPressureActorIdentity(200, 201),
        TargetResolvedExactly: true,
        TargetAliveAndTargetable: true,
        TargetInNativeRangeAndLineOfSight: true,
        ResolvedActionId: BardRepellingShotRules.RepellingShotActionId,
        ActionOffCooldown: true,
        ActionResourcesReady: true,
        LocalPlayerIsCasting: false,
        CastActionId: 0,
        AdjustedCastActionId: 0,
        BasicShotMetadataVerified: true,
        NativeBoundaryNearQueueable: true);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
