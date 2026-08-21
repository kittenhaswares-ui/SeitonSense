namespace SeitonSense.Core;

public readonly record struct PhysicalGameplayKeyState(
    bool IsPrimed,
    bool IsDown,
    bool IsEligible,
    bool IsConsumed)
{
    public static PhysicalGameplayKeyState Initial => default;
}

public readonly record struct PhysicalGameplayKeyObservation(
    bool IsDown,
    bool IsTextInputActive,
    bool HardReset = false);

public readonly record struct PhysicalGameplayKeyDecision(
    PhysicalGameplayKeyState NextState,
    bool IsFreshPress,
    bool IsHeldEligible);

public static class PhysicalGameplayKeyRules
{
    /// <summary>
    /// Chooses the exact key used as continuous held consent. Stable movement
    /// wins first, then any other already-held key, with fresh keys retained as
    /// a fallback. Equal classes use the lower token deterministically.
    /// </summary>
    public static int SelectPreferredHeldKeyToken(
        int selectedKeyToken,
        bool selectedIsFreshPress,
        bool selectedIsMovementKey,
        int candidateKeyToken,
        bool candidateIsFreshPress,
        bool candidateIsMovementKey)
    {
        if (candidateKeyToken <= 0) return selectedKeyToken;
        if (selectedKeyToken <= 0) return candidateKeyToken;

        var selectedPriority = GetHeldKeyPriority(
            selectedIsFreshPress,
            selectedIsMovementKey);
        var candidatePriority = GetHeldKeyPriority(
            candidateIsFreshPress,
            candidateIsMovementKey);
        if (candidatePriority != selectedPriority)
            return candidatePriority < selectedPriority
                ? candidateKeyToken
                : selectedKeyToken;

        return Math.Min(selectedKeyToken, candidateKeyToken);
    }

    public static int RetainEligibleHeldKeyToken(
        int currentKeyToken,
        bool currentKeyStillEligible,
        int preferredFallbackKeyToken) =>
        currentKeyToken > 0 && currentKeyStillEligible
            ? currentKeyToken
            : Math.Max(0, preferredFallbackKeyToken);

    private static int GetHeldKeyPriority(bool isFreshPress, bool isMovementKey) =>
        (isFreshPress, isMovementKey) switch
        {
            (false, true) => 0,
            (false, false) => 1,
            (true, true) => 2,
            _ => 3,
        };

    public static PhysicalGameplayKeyDecision Observe(
        PhysicalGameplayKeyState previous,
        PhysicalGameplayKeyObservation observation)
    {
        if (observation.HardReset)
            return new PhysicalGameplayKeyDecision(PhysicalGameplayKeyState.Initial, false, false);

        // A key that is already down when observation starts is not new player
        // intent. It must be released before it can become eligible.
        if (!previous.IsPrimed)
        {
            var primed = new PhysicalGameplayKeyState(
                IsPrimed: true,
                IsDown: observation.IsDown,
                IsEligible: false,
                IsConsumed: observation.IsDown);
            return new PhysicalGameplayKeyDecision(primed, false, false);
        }

        if (!observation.IsDown)
        {
            var released = new PhysicalGameplayKeyState(
                IsPrimed: true,
                IsDown: false,
                IsEligible: false,
                IsConsumed: false);
            return new PhysicalGameplayKeyDecision(released, false, false);
        }

        var isFreshPress = !previous.IsDown;
        var pressedWhileTyping = observation.IsTextInputActive;
        var eligible = isFreshPress
            ? !pressedWhileTyping
            : previous.IsEligible && !pressedWhileTyping;
        var consumed = isFreshPress
            ? pressedWhileTyping
            : previous.IsConsumed || pressedWhileTyping;
        var next = new PhysicalGameplayKeyState(
            IsPrimed: true,
            IsDown: true,
            IsEligible: eligible,
            IsConsumed: consumed);

        return new PhysicalGameplayKeyDecision(
            next,
            isFreshPress && !pressedWhileTyping,
            eligible && !consumed && !pressedWhileTyping);
    }

    public static PhysicalGameplayKeyState Consume(PhysicalGameplayKeyState state) =>
        !state.IsDown
            ? state
            : state with
            {
                IsEligible = false,
                IsConsumed = true,
            };
}
