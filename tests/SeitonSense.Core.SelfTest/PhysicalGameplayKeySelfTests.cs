using SeitonSense.Core;

internal static class PhysicalGameplayKeySelfTests
{
    public static void PrimingAndReleaseDefineGenerations()
    {
        var primedDown = Observe(PhysicalGameplayKeyState.Initial, isDown: true);
        False(primedDown.IsFreshPress, "a key already down on the first sample is not fresh");
        False(primedDown.IsHeldEligible, "a key already down on the first sample is not held-eligible");

        var stillDown = Observe(primedDown.NextState, isDown: true);
        False(stillDown.IsHeldEligible, "initially-down key stays blocked until release");

        var released = Observe(stillDown.NextState, isDown: false);
        var pressed = Observe(released.NextState, isDown: true);
        True(pressed.IsFreshPress, "release and press creates a fresh physical generation");
        True(pressed.IsHeldEligible, "the new gameplay generation is held-eligible");

        var held = Observe(pressed.NextState, isDown: true);
        False(held.IsFreshPress, "holding does not create another edge");
        True(held.IsHeldEligible, "the same unconsumed generation remains eligible");
    }

    public static void ConsumptionSurvivesUntilRelease()
    {
        var state = Observe(PhysicalGameplayKeyState.Initial, isDown: false).NextState;
        state = Observe(state, isDown: true).NextState;
        state = PhysicalGameplayKeyRules.Consume(state);

        var held = Observe(state, isDown: true);
        False(held.IsFreshPress, "consumed hold has no new edge");
        False(held.IsHeldEligible, "consumed hold cannot be reused");

        var released = Observe(held.NextState, isDown: false);
        var pressedAgain = Observe(released.NextState, isDown: true);
        True(pressedAgain.IsFreshPress, "release rearms a new physical press");
        True(pressedAgain.IsHeldEligible, "new generation becomes eligible");
    }

    public static void TextInputPoisonsOnlyTheCurrentHold()
    {
        var state = Observe(PhysicalGameplayKeyState.Initial, isDown: false).NextState;
        var typed = Observe(state, isDown: true, textInput: true);
        False(typed.IsFreshPress, "typing is never a gameplay edge");
        False(typed.IsHeldEligible, "typing cannot become a held gameplay trigger");

        var chatClosed = Observe(typed.NextState, isDown: true, textInput: false);
        False(chatClosed.IsFreshPress, "closing chat does not synthesize an edge");
        False(chatClosed.IsHeldEligible, "typed hold stays blocked after chat closes");

        var released = Observe(chatClosed.NextState, isDown: false);
        var gameplayPress = Observe(released.NextState, isDown: true, textInput: false);
        True(gameplayPress.IsFreshPress, "a later gameplay press is valid");
        True(gameplayPress.IsHeldEligible, "only the later generation is eligible");
    }

    public static void HardResetRequiresAnotherRelease()
    {
        var state = Observe(PhysicalGameplayKeyState.Initial, isDown: false).NextState;
        state = Observe(state, isDown: true).NextState;
        var reset = PhysicalGameplayKeyRules.Observe(
            state,
            new PhysicalGameplayKeyObservation(IsDown: true, IsTextInputActive: false, HardReset: true));
        Equal(PhysicalGameplayKeyState.Initial, reset.NextState, "hard reset clears the generation");

        var rePrimeDown = Observe(reset.NextState, isDown: true);
        False(rePrimeDown.IsHeldEligible, "still-held key after reset is ineligible");
    }

    public static void OneHoldCanCrossDistinctPurifyStatusGenerations()
    {
        var keyState = Observe(PhysicalGameplayKeyState.Initial, isDown: false).NextState;
        var physicalPress = Observe(keyState, isDown: true);
        var statusA = new PurifyCcStatusInstance(1343, 1);
        var first = EmergencyPurifyBufferRules.Observe(
            EmergencyPurifyBufferState.Initial,
            PurifyObservation(statusA, physicalPress.IsHeldEligible, now: 1_000));
        True(first.ShouldDispatch, "eligible held generation owns the first status");
        True(first.ShouldConsumeInputGeneration, "first status claims only the current framework frame");
        var accepted = EmergencyPurifyBufferRules.ApplyNativeAttemptOutcome(
            first.NextState,
            ClientActionAttemptOutcome.ClientAccepted,
            1_000);
        keyState = physicalPress.NextState;

        var stillHeld = Observe(keyState, isDown: true);
        True(stillHeld.IsHeldEligible, "frame claim leaves held consent eligible");
        var statusB = new PurifyCcStatusInstance(1345, 2);
        var replacement = EmergencyPurifyBufferRules.Observe(
            accepted.NextState,
            PurifyObservation(statusB, stillHeld.IsHeldEligible, now: 1_001));
        True(replacement.ShouldDispatch, "same hold can trigger a distinct exact CC status");

        keyState = Observe(stillHeld.NextState, isDown: false).NextState;
        var newPress = Observe(keyState, isDown: true);
        var statusC = new PurifyCcStatusInstance(1347, 3);
        var nextGeneration = EmergencyPurifyBufferRules.Observe(
            replacement.NextState,
            PurifyObservation(statusC, newPress.IsHeldEligible, now: 1_002));
        True(nextGeneration.ShouldDispatch, "release and repress may own a later status");
    }

    public static void GuardSuppressionPreservesObservedHold()
    {
        var state = Observe(PhysicalGameplayKeyState.Initial, isDown: false).NextState;
        var pressed = Observe(state, isDown: true);
        True(pressed.IsHeldEligible, "pre-Guard physical hold is eligible");

        // Guard is an action-eligibility gate, not an input-observer reset. The
        // coordinator must continue sampling the same down generation here.
        var whileGuarded = Observe(pressed.NextState, isDown: true);
        True(whileGuarded.IsHeldEligible, "same hold remains observed during Guard");

        var afterGuard = Observe(whileGuarded.NextState, isDown: true);
        False(afterGuard.IsFreshPress, "Guard ending does not synthesize a new edge");
        True(afterGuard.IsHeldEligible, "same physical hold remains consent after Guard");
    }

    private static EmergencyPurifyBufferObservation PurifyObservation(
        PurifyCcStatusInstance status,
        bool heldEligible,
        long now) =>
        new(
            ConfigurationEnabled: true,
            IsSupportedPvPContext: true,
            IsAlive: true,
            IsLocalPlayerIdentityValid: true,
            IsResilienceActive: false,
            IsTextInputActive: false,
            StatusInstance: status,
            FreshKeyPressed: false,
            HeldKeyEligible: heldEligible,
            AllowHeldKeyAtStatusEntry: true,
            PurifyLocallyReady: true,
            NowMilliseconds: now,
            HeldKeyCode: heldEligible ? 65 : 0);

    private static PhysicalGameplayKeyDecision Observe(
        PhysicalGameplayKeyState state,
        bool isDown,
        bool textInput = false) =>
        PhysicalGameplayKeyRules.Observe(
            state,
            new PhysicalGameplayKeyObservation(isDown, textInput));

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
