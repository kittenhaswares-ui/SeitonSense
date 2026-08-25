using SeitonSense.Core;

internal static class ReactiveCounterCcLateDispatchSelfTests
{
    private const ulong TargetGameObjectId = 0x1_0000_1234;
    private const uint TargetEntityId = 0x1234;
    private const long IdealRequestAtMilliseconds = 10_000;

    internal static void MainGcdProfilesAreExplicit()
    {
        True(ReactiveCounterCcProfileRules.UsesMainGlobalCooldown(
            MiracleInterceptConfirmationRules.ForkedRaijuActionId), "Forked Raiju");
        True(ReactiveCounterCcProfileRules.UsesMainGlobalCooldown(
            MiracleInterceptConfirmationRules.FleetingRaijuActionId), "Fleeting Raiju");
        True(ReactiveCounterCcProfileRules.UsesMainGlobalCooldown(
            MiracleInterceptConfirmationRules.ResolutionActionId), "Resolution");
        True(ReactiveCounterCcProfileRules.UsesMainGlobalCooldown(
            MiracleInterceptConfirmationRules.FrostStarActionId), "Frost Star");

        False(ReactiveCounterCcProfileRules.UsesMainGlobalCooldown(
            MiracleInterceptConfirmationRules.MiracleOfNatureActionId), "Miracle");
        False(ReactiveCounterCcProfileRules.UsesMainGlobalCooldown(
            MiracleInterceptConfirmationRules.SilentNocturneActionId), "Silent Nocturne");
        False(ReactiveCounterCcProfileRules.UsesMainGlobalCooldown(
            MiracleInterceptConfirmationRules.InterveneActionId), "Intervene");
        False(ReactiveCounterCcProfileRules.UsesMainGlobalCooldown(
            MiracleInterceptConfirmationRules.ViceOfThornsActionId), "Vice of Thorns");
    }

    internal static void LateReservationUsesFrozenIdealDeadline()
    {
        var reservation = Reservation();
        True(CanDispatch(reservation, IdealRequestAtMilliseconds), "ready on ideal frame");
        True(CanDispatch(
            reservation,
            IdealRequestAtMilliseconds +
            ReactiveCounterCcLateDispatchRules.MaximumLateMilliseconds - 1),
            "last millisecond inside frozen lease");
        False(CanDispatch(
            reservation,
            IdealRequestAtMilliseconds +
            ReactiveCounterCcLateDispatchRules.MaximumLateMilliseconds),
            "exact one-second boundary expires");
        False(CanDispatch(reservation, IdealRequestAtMilliseconds - 1), "cannot dispatch early");
    }

    internal static void LateReservationNeverChangesActionTargetOrProtectionEpisode()
    {
        var reservation = Reservation();
        var now = IdealRequestAtMilliseconds + 500;
        False(CanDispatch(
            reservation,
            now,
            currentActionId: MiracleInterceptConfirmationRules.FrostStarActionId),
            "action drift");
        False(CanDispatch(
            reservation,
            now,
            currentTargetGameObjectId: TargetGameObjectId + 1),
            "game-object drift");
        False(CanDispatch(
            reservation,
            now,
            currentTargetEntityId: TargetEntityId + 1),
            "entity drift");
        False(CanDispatch(
            reservation,
            now,
            currentProtectionStatusId: MiracleGuardFollowupRules.GuardStatusId),
            "different protection episode");
        True(CanDispatch(reservation, now, currentProtectionStatusId: 0),
            "authoritative natural absence remains the same episode");

        False(CanDispatch(reservation, now, heldKeyGenerationValid: false), "held key drift");
        False(CanDispatch(reservation, now, rangeAndLineOfSightValid: false), "range drift");
        False(CanDispatch(reservation, now, structurallyReady: false), "GCD/resources unavailable");
        False(CanDispatch(reservation, now, globalQueueReady: false), "global queue busy");
        False(CanDispatch(reservation, now, protectionStateValid: false), "protection proof invalid");
    }

    private static ReactiveCounterCcLateReservation Reservation() => new(
        MiracleInterceptConfirmationRules.ResolutionActionId,
        TargetGameObjectId,
        TargetEntityId,
        MiracleCleanseFollowupRules.ResilienceStatusId,
        IdealRequestAtMilliseconds);

    private static bool CanDispatch(
        ReactiveCounterCcLateReservation reservation,
        long nowMilliseconds,
        uint currentActionId = MiracleInterceptConfirmationRules.ResolutionActionId,
        ulong currentTargetGameObjectId = TargetGameObjectId,
        uint currentTargetEntityId = TargetEntityId,
        uint currentProtectionStatusId = MiracleCleanseFollowupRules.ResilienceStatusId,
        bool protectionStateValid = true,
        bool heldKeyGenerationValid = true,
        bool rangeAndLineOfSightValid = true,
        bool structurallyReady = true,
        bool globalQueueReady = true) =>
        ReactiveCounterCcLateDispatchRules.CanDispatch(
            reservation,
            nowMilliseconds,
            currentActionId,
            currentTargetGameObjectId,
            currentTargetEntityId,
            currentProtectionStatusId,
            protectionStateValid,
            heldKeyGenerationValid,
            rangeAndLineOfSightValid,
            structurallyReady,
            globalQueueReady);

    private static void True(bool value, string label)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool value, string label) => True(!value, label);
}
