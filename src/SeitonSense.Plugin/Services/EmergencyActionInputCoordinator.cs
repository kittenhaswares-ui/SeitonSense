using Dalamud.Plugin.Services;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// One framework-frame view of the shared physical gameplay-key generations used
/// by emergency self-Purify, reactive Guard, Smart Recuperate, PLD Guardian,
/// ally rescue, Miracle intercept, fresh-key Ninja Seiton, held-key Scholar
/// Critical Strategy, and the exact high-pressure movement-key Sprint helper.
/// Consumption is deliberately shared: once any helper claims a generation,
/// every later helper sees no input.
/// </summary>
internal sealed class EmergencyActionInputFrame
{
    private readonly GameInputContextProbe? probe;

    internal EmergencyActionInputFrame(
        GameInputContextSnapshot snapshot,
        GameInputContextProbe? probe)
    {
        Snapshot = snapshot;
        this.probe = probe;
    }

    internal GameInputContextSnapshot Snapshot { get; }
    internal bool IsConsumed { get; private set; }

    internal void Consume()
    {
        if (IsConsumed) return;
        IsConsumed = true;
        probe?.ConsumeHeldGameplayKeys();
    }

    internal bool FreshGameplayKeyPressed =>
        !IsConsumed && Snapshot.ProbeSucceeded && Snapshot.FreshGameplayKeyPressed;

    internal bool HeldGameplayKeyEligible =>
        !IsConsumed && Snapshot.ProbeSucceeded && Snapshot.HeldGameplayKeyEligible;

    internal bool HeldMovementKeyEligible =>
        !IsConsumed && Snapshot.ProbeSucceeded && Snapshot.HeldMovementKeyEligible;
}

/// <summary>
/// Owns the single IKeyState-backed generation tracker shared by all emergency
/// action helpers. A newly enabled held-key option cannot inherit a key which was
/// already down before the opt-in.
/// </summary>
internal sealed class EmergencyActionInputCoordinator
{
    private readonly GameInputContextProbe probe;
    private bool purifyHeldWasEnabled;
    private bool defensiveUtilityHeldWasEnabled;
    private bool paladinGuardianHeldWasEnabled;
    private bool smartRecuperateHeldWasEnabled;
    private bool allyRescueHeldWasEnabled;
    private bool miracleInterceptHeldWasEnabled;
    private bool scholarCriticalStrategyHeldWasEnabled;
    private bool pressureEscapeHeldWasEnabled;

    internal EmergencyActionInputCoordinator(IKeyState keyState)
    {
        probe = new GameInputContextProbe(keyState);
    }

    internal EmergencyActionInputFrame Observe(
        bool shouldObserve,
        bool purifyHeldEnabled,
        bool defensiveUtilityHeldEnabled,
        bool paladinGuardianHeldEnabled,
        bool smartRecuperateHeldEnabled,
        bool allyRescueHeldEnabled,
        bool miracleInterceptHeldEnabled,
        bool scholarCriticalStrategyHeldEnabled,
        bool pressureEscapeHeldEnabled = false)
    {
        if (!shouldObserve)
        {
            Reset();
            return new EmergencyActionInputFrame(
                GameInputContextSnapshot.NotObserved,
                null);
        }

        var input = probe.Observe();
        var heldOptionJustEnabled =
            (purifyHeldEnabled && !purifyHeldWasEnabled) ||
            (defensiveUtilityHeldEnabled && !defensiveUtilityHeldWasEnabled) ||
            (paladinGuardianHeldEnabled && !paladinGuardianHeldWasEnabled) ||
            (smartRecuperateHeldEnabled && !smartRecuperateHeldWasEnabled) ||
            (allyRescueHeldEnabled && !allyRescueHeldWasEnabled) ||
            (miracleInterceptHeldEnabled && !miracleInterceptHeldWasEnabled) ||
            (scholarCriticalStrategyHeldEnabled && !scholarCriticalStrategyHeldWasEnabled) ||
            (pressureEscapeHeldEnabled && !pressureEscapeHeldWasEnabled);
        purifyHeldWasEnabled = purifyHeldEnabled;
        defensiveUtilityHeldWasEnabled = defensiveUtilityHeldEnabled;
        paladinGuardianHeldWasEnabled = paladinGuardianHeldEnabled;
        smartRecuperateHeldWasEnabled = smartRecuperateHeldEnabled;
        allyRescueHeldWasEnabled = allyRescueHeldEnabled;
        miracleInterceptHeldWasEnabled = miracleInterceptHeldEnabled;
        scholarCriticalStrategyHeldWasEnabled = scholarCriticalStrategyHeldEnabled;
        pressureEscapeHeldWasEnabled = pressureEscapeHeldEnabled;

        if (heldOptionJustEnabled)
        {
            // Preserve a same-frame down-edge as fresh intent, but never let the
            // held-level branch inherit a generation from before the opt-in.
            probe.ConsumeHeldGameplayKeys();
            input = input with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKey = Dalamud.Game.ClientState.Keys.VirtualKey.NO_KEY,
                HeldMovementKeyEligible = false,
                HeldMovementKey = Dalamud.Game.ClientState.Keys.VirtualKey.NO_KEY,
            };
        }

        return new EmergencyActionInputFrame(input, probe);
    }

    internal EmergencyActionInputFrame Observe(
        bool shouldObserve,
        bool purifyHeldEnabled,
        bool allyRescueHeldEnabled) =>
        Observe(
            shouldObserve,
            purifyHeldEnabled,
            defensiveUtilityHeldEnabled: false,
            paladinGuardianHeldEnabled: false,
            smartRecuperateHeldEnabled: false,
            allyRescueHeldEnabled,
            miracleInterceptHeldEnabled: false,
            scholarCriticalStrategyHeldEnabled: false,
            pressureEscapeHeldEnabled: false);

    internal void Reset()
    {
        probe.Reset();
        purifyHeldWasEnabled = false;
        defensiveUtilityHeldWasEnabled = false;
        paladinGuardianHeldWasEnabled = false;
        smartRecuperateHeldWasEnabled = false;
        allyRescueHeldWasEnabled = false;
        miracleInterceptHeldWasEnabled = false;
        scholarCriticalStrategyHeldWasEnabled = false;
        pressureEscapeHeldWasEnabled = false;
    }
}
