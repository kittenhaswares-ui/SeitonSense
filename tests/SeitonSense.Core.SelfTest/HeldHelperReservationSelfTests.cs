using SeitonSense.Core;

internal static class HeldHelperReservationSelfTests
{
    internal static void ReleaseWindowIsBoundedAndExclusive()
    {
        True(HeldHelperReservationRules.IsFrozenConsentValid(
            physicallyDown: true,
            releaseWasEligible: false,
            releasedAtMilliseconds: -1,
            releaseInputGeneration: -1,
            currentInputGeneration: 9,
            reservationEnabled: false,
            reservationWindowMilliseconds: 1_000,
            nowMilliseconds: 5_000,
            textInputActive: false), "physical hold remains authoritative");

        True(Released(now: 5_999), "same generation remains valid before deadline");
        False(Released(now: 6_000), "deadline is exclusive");
        False(Released(now: 5_500, enabled: false), "disabled policy cannot retain key-up");
        Equal(5_100L,
            HeldHelperReservationRules.ReleaseDeadlineMilliseconds(5_000, 1),
            "configured window clamps to minimum");
        Equal(long.MaxValue,
            HeldHelperReservationRules.ReleaseDeadlineMilliseconds(long.MaxValue - 5, 1_500),
            "deadline saturates");
    }

    internal static void ConsumedFrozenOwnerMayReserveButDiscoveryCannot()
    {
        var physical = PhysicalGameplayKeyRules.Observe(
            PhysicalGameplayKeyState.Initial,
            new PhysicalGameplayKeyObservation(
                IsDown: false,
                IsTextInputActive: false)).NextState;
        physical = PhysicalGameplayKeyRules.Observe(
            physical,
            new PhysicalGameplayKeyObservation(
                IsDown: true,
                IsTextInputActive: false)).NextState;
        True(physical.IsEligible && !physical.IsConsumed,
            "fresh physical generation starts eligible");
        True(HeldHelperReservationRules.CanObserveExactFrozenOwnership(
                physical,
                exactFrozenOwnershipAlreadyObserved: false,
                ownershipRetiredUntilRelease: false,
                textInputActive: false),
            "an eligible live generation may acquire exact frozen ownership");

        var exactFrozenOwnershipObserved =
            HeldHelperReservationRules.IsFrozenConsentValid(
                physicallyDown: physical.IsDown,
                releaseWasEligible: false,
                releasedAtMilliseconds: -1,
                releaseInputGeneration: -1,
                currentInputGeneration: 9,
                reservationEnabled: true,
                reservationWindowMilliseconds: 1_000,
                nowMilliseconds: 4_900,
                textInputActive: false);
        physical = PhysicalGameplayKeyRules.Consume(physical);
        True(physical.IsConsumed && !physical.IsEligible,
            "claim consumption retires ordinary discovery");
        True(HeldHelperReservationRules.CanObserveExactFrozenOwnership(
                physical,
                exactFrozenOwnershipAlreadyObserved: true,
                ownershipRetiredUntilRelease: false,
                textInputActive: false),
            "a legitimate exact owner survives later frame-claim consumption");
        False(HeldHelperReservationRules.CanObserveExactFrozenOwnership(
                physical,
                exactFrozenOwnershipAlreadyObserved: false,
                ownershipRetiredUntilRelease: true,
                textInputActive: false),
            "administrative retirement cannot reacquire ownership before release");

        var releaseWasEligible =
            HeldHelperReservationRules.CanBeginReleaseReservation(
                exactFrozenOwnershipObserved,
                textInputActive: false);
        True(releaseWasEligible,
            "freeze then frame-claim consumption retains only the frozen owner");
        physical = PhysicalGameplayKeyRules.Observe(
            physical,
            new PhysicalGameplayKeyObservation(
                IsDown: false,
                IsTextInputActive: false)).NextState;
        False(physical.IsDown, "physical key-up is authoritative");
        True(Released(now: 5_250, releaseWasEligible: releaseWasEligible),
            "key-up keeps the exact consumed frozen owner inside grace");

        False(HeldHelperReservationRules.CanBeginReleaseReservation(
            exactFrozenOwnershipObserved: false,
            textInputActive: false),
            "unconsumed discovery without a frozen owner cannot reserve");
        False(HeldHelperReservationRules.CanBeginReleaseReservation(
            exactFrozenOwnershipObserved: true,
            textInputActive: true),
            "text input poisons even an observed frozen owner");

        False(Released(now: 5_250, releaseWasEligible: false),
            "another unowned released key gets no grace");
    }

    internal static void NewInputAndSafetyDriftCancelReleaseReservation()
    {
        True(Released(now: 5_250),
            "released exact key remains reserved inside the configured window");
        foreach (var threat in new[]
                 {
                     MiracleInterceptThreatKind.PostPurifyCrowdControl,
                     MiracleInterceptThreatKind.PostGuardCrowdControl,
                 })
        {
            True(ReactiveCounterCcProfileRules.IsThreatSupportedByAction(
                    MiracleInterceptConfirmationRules.SilentNocturneActionId,
                    threat),
                $"Silent Nocturne supports the frozen {threat} episode");
            True(MiracleProtectionEndRules.CanAttempt(
                    HeldActionRetryState.Initial,
                    observedAtMilliseconds: 5_000,
                    nowMilliseconds: 5_250),
                $"released exact key may reach the first {threat} attempt");
        }

        False(Released(now: 5_250, currentGeneration: 10),
            "new physical input generation cancels old reservation");
        False(Released(now: 5_250, textInputActive: true),
            "text input cancels reservation");
        False(Released(now: 5_250, exactIntentValid: false),
            "action target context or Guard drift cancels reservation");
        False(Released(now: 4_999), "clock movement before release fails closed");
    }

    private static bool Released(
        long now,
        bool enabled = true,
        bool releaseWasEligible = true,
        long currentGeneration = 9,
        bool textInputActive = false,
        bool exactIntentValid = true) =>
        HeldHelperReservationRules.IsFrozenConsentValid(
            physicallyDown: false,
            releaseWasEligible,
            releasedAtMilliseconds: 5_000,
            releaseInputGeneration: 9,
            currentInputGeneration: currentGeneration,
            reservationEnabled: enabled,
            reservationWindowMilliseconds: 1_000,
            nowMilliseconds: now,
            textInputActive,
            exactIntentValid);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    private static void False(bool condition, string label) => True(!condition, label);
}
