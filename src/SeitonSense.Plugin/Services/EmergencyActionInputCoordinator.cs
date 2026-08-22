using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// One framework-frame view of the shared physical gameplay-key generations.
/// Canonical order is Purify, reactive counter-CC, Ally Rescue, PLD Guardian,
/// NIN Guard-Shukuchi, Ninja Seiton, Scholar Critical Strategy, DRK Hiebsprung,
/// Smart Recuperate, reactive Guard, then high-pressure Sprint. Accepted-Eukrasia Kardia and
/// Monk Earth's Reply do not originate from this physical-key frame, but their
/// attempts still suppress lower work in the runtime priority chain.
/// Consumption is deliberately frame-local: one helper can own the current
/// framework frame without destroying the still-held physical consent needed
/// by a later distinct action episode.
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
    }

    internal bool FreshGameplayKeyPressed =>
        !IsConsumed && Snapshot.ProbeSucceeded && Snapshot.FreshGameplayKeyPressed;

    internal bool HeldGameplayKeyEligible =>
        !IsConsumed && Snapshot.ProbeSucceeded && Snapshot.HeldGameplayKeyEligible;

    internal bool HeldMovementKeyEligible =>
        !IsConsumed && Snapshot.ProbeSucceeded && Snapshot.HeldMovementKeyEligible;

    internal bool IsGameplayKeyPhysicallyDown(VirtualKey key) =>
        Snapshot.ProbeSucceeded && probe?.IsGameplayKeyPhysicallyDown(key) == true;

    internal bool IsGameplayKeyGenerationEligible(VirtualKey key) =>
        Snapshot.ProbeSucceeded &&
        !Snapshot.IsTextInputActive &&
        probe?.IsGameplayKeyGenerationEligible(key) == true;
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
    private bool darkKnightPlungeHeldWasEnabled;
    private bool ninjaGuardShukuchiHeldWasEnabled;
    private bool ninjaSeitonHeldWasEnabled;

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
        bool pressureEscapeHeldEnabled = false,
        bool darkKnightPlungeHeldEnabled = false,
        bool ninjaGuardShukuchiHeldEnabled = false,
        bool ninjaSeitonHeldEnabled = false)
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
            (pressureEscapeHeldEnabled && !pressureEscapeHeldWasEnabled) ||
            (darkKnightPlungeHeldEnabled && !darkKnightPlungeHeldWasEnabled) ||
            (ninjaGuardShukuchiHeldEnabled && !ninjaGuardShukuchiHeldWasEnabled) ||
            (ninjaSeitonHeldEnabled && !ninjaSeitonHeldWasEnabled);
        purifyHeldWasEnabled = purifyHeldEnabled;
        defensiveUtilityHeldWasEnabled = defensiveUtilityHeldEnabled;
        paladinGuardianHeldWasEnabled = paladinGuardianHeldEnabled;
        smartRecuperateHeldWasEnabled = smartRecuperateHeldEnabled;
        allyRescueHeldWasEnabled = allyRescueHeldEnabled;
        miracleInterceptHeldWasEnabled = miracleInterceptHeldEnabled;
        scholarCriticalStrategyHeldWasEnabled = scholarCriticalStrategyHeldEnabled;
        pressureEscapeHeldWasEnabled = pressureEscapeHeldEnabled;
        darkKnightPlungeHeldWasEnabled = darkKnightPlungeHeldEnabled;
        ninjaGuardShukuchiHeldWasEnabled = ninjaGuardShukuchiHeldEnabled;
        ninjaSeitonHeldWasEnabled = ninjaSeitonHeldEnabled;

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
        darkKnightPlungeHeldWasEnabled = false;
        ninjaGuardShukuchiHeldWasEnabled = false;
        ninjaSeitonHeldWasEnabled = false;
    }
}
