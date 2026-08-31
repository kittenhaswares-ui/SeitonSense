namespace SeitonSense.Core;

/// <summary>
/// Value-only proof for the second authored macro line that follows an exact
/// Smart Action Chase reservation. Direct Mode-None input is represented by a
/// false macro-carrier flag and can never be swallowed.
/// </summary>
public readonly record struct SmartActionChaseMacroTailObservation(
    bool PendingChase,
    bool TailBudgetAvailable,
    bool CertifiedSmartActionMacroRoot,
    long FrozenTapGeneration,
    long SafetyLeaseTapGeneration,
    uint FrozenActionType,
    uint IncomingActionType,
    uint FrozenRequestedActionId,
    uint IncomingRequestedActionId,
    uint FrozenResolvedActionId,
    uint IncomingResolvedActionId,
    ulong CapturedVisibleGameObjectId,
    uint CapturedVisibleEntityId,
    ulong IncomingTargetId,
    bool IsMacroCarrier,
    bool IsQueueCarrier);

public static class SmartActionChaseMacroTailRules
{
    public static bool ShouldSuppress(
        SmartActionChaseMacroTailObservation observation) =>
        observation.PendingChase &&
        observation.TailBudgetAvailable &&
        observation.CertifiedSmartActionMacroRoot &&
        observation.FrozenTapGeneration > 0 &&
        observation.SafetyLeaseTapGeneration == observation.FrozenTapGeneration &&
        observation.FrozenActionType == observation.IncomingActionType &&
        observation.FrozenRequestedActionId != 0 &&
        observation.FrozenRequestedActionId == observation.IncomingRequestedActionId &&
        observation.FrozenResolvedActionId != 0 &&
        observation.FrozenResolvedActionId == observation.IncomingResolvedActionId &&
        observation.IsMacroCarrier &&
        !observation.IsQueueCarrier &&
        MatchesAuthoredVisibleCarrier(observation);

    private static bool MatchesAuthoredVisibleCarrier(
        SmartActionChaseMacroTailObservation observation)
    {
        var capturedVisibleTargetIsEmpty =
            observation.CapturedVisibleGameObjectId == 0 &&
            observation.CapturedVisibleEntityId == 0;
        if (capturedVisibleTargetIsEmpty)
        {
            return observation.IncomingTargetId is 0 or 0xE0000000;
        }

        var capturedVisibleTargetIsExactActor =
            new TargetPressureActorIdentity(
                observation.CapturedVisibleGameObjectId,
                observation.CapturedVisibleEntityId).IsValid;
        if (!capturedVisibleTargetIsExactActor) return false;

        return observation.IncomingTargetId ==
                   observation.CapturedVisibleGameObjectId ||
               observation.IncomingTargetId ==
                   observation.CapturedVisibleEntityId;
    }
}
