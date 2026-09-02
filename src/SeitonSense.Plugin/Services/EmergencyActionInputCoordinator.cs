using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// One framework-frame view of the shared physical gameplay-key generations.
/// Canonical order starts Purify, PLD Guardian, Recuperate, and Auto-Guard,
/// followed by AST same-target healing, RDM Guard engage, SAM reactive actions,
/// Ninja Seiton, VPR Serpent's Tail, GNB Continuation, reactive counter-CC, Ally Rescue, NIN
/// Guard-Shukuchi, Scholar Critical Strategy, DRK Dark-Arts Shadowbringer, DRK
/// Hiebsprung, DRK safe-fallback Shadowbringer, held Monk combo, Emergency
/// Teleport, then high-pressure Sprint.
/// Accepted-Eukrasia Kardia and Monk Earth's Reply follow as event lanes.
/// Consumption is deliberately frame-local: one helper can own the current
/// framework frame without destroying the still-held physical consent needed
/// by a later distinct action episode.
/// </summary>
internal sealed class EmergencyActionInputFrame
{
    private readonly GameInputContextProbe? probe;
    private readonly Action? onConsumed;
    private readonly bool heldHelperReservationEnabled;
    private readonly int heldHelperReservationWindowMilliseconds;
    private readonly long observedAtMilliseconds;

    internal EmergencyActionInputFrame(
        GameInputContextSnapshot snapshot,
        GameInputContextProbe? probe,
        Action? onConsumed = null,
        bool heldHelperReservationEnabled = false,
        int heldHelperReservationWindowMilliseconds =
            HeldActionRetryRules.DefaultLatencyResponseWindowMilliseconds,
        long observedAtMilliseconds = -1)
    {
        Snapshot = snapshot;
        this.probe = probe;
        this.onConsumed = onConsumed;
        this.heldHelperReservationEnabled = heldHelperReservationEnabled;
        this.heldHelperReservationWindowMilliseconds =
            heldHelperReservationWindowMilliseconds;
        this.observedAtMilliseconds = observedAtMilliseconds;
    }

    internal GameInputContextSnapshot Snapshot { get; }
    internal bool IsConsumed { get; private set; }

    internal void Consume()
    {
        if (IsConsumed) return;
        IsConsumed = true;
        onConsumed?.Invoke();
    }

    /// <summary>
    /// Retires this frame and every currently held gameplay-key generation for
    /// an administrative lifecycle transition. The keys remain ineligible until
    /// physical release, and no action owner is advertised to cooperating input
    /// plugins.
    /// </summary>
    internal void RetireWithoutActionClaim()
    {
        probe?.ConsumeHeldGameplayKeys();
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

    internal bool IsFrozenGameplayKeyConsentValid(VirtualKey key) =>
        Snapshot.ProbeSucceeded &&
        !Snapshot.IsTextInputActive &&
        probe?.IsFrozenGameplayKeyConsentValid(
            key,
            heldHelperReservationEnabled,
            heldHelperReservationWindowMilliseconds,
            observedAtMilliseconds,
            Snapshot.IsTextInputActive) == true;
}

/// <summary>
/// Owns the single IKeyState-backed generation tracker shared by all emergency
/// action helpers. A newly enabled held-key option cannot inherit a key which was
/// already down before the opt-in.
/// </summary>
internal sealed class EmergencyActionInputCoordinator
{
    private readonly GameInputContextProbe probe;
    private readonly Action? onFrameConsumed;
    private bool purifyHeldWasEnabled;
    private bool defensiveUtilityHeldWasEnabled;
    private bool paladinGuardianHeldWasEnabled;
    private bool smartRecuperateHeldWasEnabled;
    private bool emergencyTeleportHeldWasEnabled;
    private bool allyRescueHeldWasEnabled;
    private bool miracleInterceptHeldWasEnabled;
    private bool scholarCriticalStrategyHeldWasEnabled;
    private bool astrologianHarmonicOrbisHeldWasEnabled;
    private bool redMageGuardEngageHeldWasEnabled;
    private bool pressureEscapeHeldWasEnabled;
    private bool smartSprintHeldWasEnabled;
    private bool darkKnightPlungeHeldWasEnabled;
    private bool ninjaGuardShukuchiHeldWasEnabled;
    private bool ninjaSeitonHeldWasEnabled;
    private bool viperSerpentTailHeldWasEnabled;
    private bool gunbreakerContinuationHeldWasEnabled;
    private bool darkKnightShadowbringerHeldWasEnabled;
    private bool monkHeldComboWasEnabled;
    private bool samuraiCounterCcHeldWasEnabled;
    private bool samuraiZantetsukenHeldWasEnabled;

    internal EmergencyActionInputCoordinator(
        IKeyState keyState,
        Action? onFrameConsumed = null)
    {
        probe = new GameInputContextProbe(keyState);
        this.onFrameConsumed = onFrameConsumed;
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
        bool emergencyTeleportHeldEnabled = false,
        bool pressureEscapeHeldEnabled = false,
        bool darkKnightPlungeHeldEnabled = false,
        bool ninjaGuardShukuchiHeldEnabled = false,
        bool ninjaSeitonHeldEnabled = false,
        bool viperSerpentTailHeldEnabled = false,
        bool gunbreakerContinuationHeldEnabled = false,
        bool darkKnightShadowbringerHeldEnabled = false,
        bool monkHeldComboEnabled = false,
        bool samuraiCounterCcHeldEnabled = false,
        bool samuraiZantetsukenHeldEnabled = false,
        bool astrologianHarmonicOrbisHeldEnabled = false,
        bool redMageGuardEngageHeldEnabled = false,
        bool smartSprintHeldEnabled = false,
        bool heldHelperReservationEnabled = false,
        int heldHelperReservationWindowMilliseconds =
            HeldActionRetryRules.DefaultLatencyResponseWindowMilliseconds,
        long nowMilliseconds = -1,
        bool heldHelpersSuppressedByGuard = false)
    {
        var observationMilliseconds = nowMilliseconds >= 0
            ? nowMilliseconds
            : Math.Max(0, Environment.TickCount64);
        if (!shouldObserve)
        {
            Reset();
            return new EmergencyActionInputFrame(
                GameInputContextSnapshot.NotObserved,
                null,
                onFrameConsumed,
                heldHelperReservationEnabled,
                heldHelperReservationWindowMilliseconds,
                observationMilliseconds);
        }

        var input = probe.Observe(observationMilliseconds);
        var heldOptionJustEnabled =
            (purifyHeldEnabled && !purifyHeldWasEnabled) ||
            (defensiveUtilityHeldEnabled && !defensiveUtilityHeldWasEnabled) ||
            (paladinGuardianHeldEnabled && !paladinGuardianHeldWasEnabled) ||
            (smartRecuperateHeldEnabled && !smartRecuperateHeldWasEnabled) ||
            (allyRescueHeldEnabled && !allyRescueHeldWasEnabled) ||
            (miracleInterceptHeldEnabled && !miracleInterceptHeldWasEnabled) ||
            (scholarCriticalStrategyHeldEnabled && !scholarCriticalStrategyHeldWasEnabled) ||
            (astrologianHarmonicOrbisHeldEnabled && !astrologianHarmonicOrbisHeldWasEnabled) ||
            (redMageGuardEngageHeldEnabled && !redMageGuardEngageHeldWasEnabled) ||
            (emergencyTeleportHeldEnabled && !emergencyTeleportHeldWasEnabled) ||
            (pressureEscapeHeldEnabled && !pressureEscapeHeldWasEnabled) ||
            (smartSprintHeldEnabled && !smartSprintHeldWasEnabled) ||
            (darkKnightPlungeHeldEnabled && !darkKnightPlungeHeldWasEnabled) ||
            (ninjaGuardShukuchiHeldEnabled && !ninjaGuardShukuchiHeldWasEnabled) ||
            (ninjaSeitonHeldEnabled && !ninjaSeitonHeldWasEnabled) ||
            (viperSerpentTailHeldEnabled && !viperSerpentTailHeldWasEnabled) ||
            (gunbreakerContinuationHeldEnabled && !gunbreakerContinuationHeldWasEnabled) ||
            (darkKnightShadowbringerHeldEnabled && !darkKnightShadowbringerHeldWasEnabled) ||
            (monkHeldComboEnabled && !monkHeldComboWasEnabled) ||
            (samuraiCounterCcHeldEnabled && !samuraiCounterCcHeldWasEnabled) ||
            (samuraiZantetsukenHeldEnabled && !samuraiZantetsukenHeldWasEnabled);
        purifyHeldWasEnabled = purifyHeldEnabled;
        defensiveUtilityHeldWasEnabled = defensiveUtilityHeldEnabled;
        paladinGuardianHeldWasEnabled = paladinGuardianHeldEnabled;
        smartRecuperateHeldWasEnabled = smartRecuperateHeldEnabled;
        allyRescueHeldWasEnabled = allyRescueHeldEnabled;
        miracleInterceptHeldWasEnabled = miracleInterceptHeldEnabled;
        scholarCriticalStrategyHeldWasEnabled = scholarCriticalStrategyHeldEnabled;
        astrologianHarmonicOrbisHeldWasEnabled = astrologianHarmonicOrbisHeldEnabled;
        redMageGuardEngageHeldWasEnabled = redMageGuardEngageHeldEnabled;
        emergencyTeleportHeldWasEnabled = emergencyTeleportHeldEnabled;
        pressureEscapeHeldWasEnabled = pressureEscapeHeldEnabled;
        smartSprintHeldWasEnabled = smartSprintHeldEnabled;
        darkKnightPlungeHeldWasEnabled = darkKnightPlungeHeldEnabled;
        ninjaGuardShukuchiHeldWasEnabled = ninjaGuardShukuchiHeldEnabled;
        ninjaSeitonHeldWasEnabled = ninjaSeitonHeldEnabled;
        viperSerpentTailHeldWasEnabled = viperSerpentTailHeldEnabled;
        gunbreakerContinuationHeldWasEnabled = gunbreakerContinuationHeldEnabled;
        darkKnightShadowbringerHeldWasEnabled = darkKnightShadowbringerHeldEnabled;
        monkHeldComboWasEnabled = monkHeldComboEnabled;
        samuraiCounterCcHeldWasEnabled = samuraiCounterCcHeldEnabled;
        samuraiZantetsukenHeldWasEnabled = samuraiZantetsukenHeldEnabled;

        if (heldOptionJustEnabled)
        {
            // Preserve a same-frame down-edge as fresh intent, but never let the
            // held-level branch inherit a generation from before the opt-in.
            probe.ConsumeHeldGameplayKeys(
                input.FreshGameplayKeyPressed
                    ? input.FreshGameplayKey
                    : VirtualKey.NO_KEY);
            input = input with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKey = Dalamud.Game.ClientState.Keys.VirtualKey.NO_KEY,
                HeldMovementKeyEligible = false,
                HeldMovementKey = Dalamud.Game.ClientState.Keys.VirtualKey.NO_KEY,
            };
        }

        if (heldHelpersSuppressedByGuard)
        {
            // Guard is an exact held-intent boundary, not a temporary queue wait.
            // Retire every physical generation without consuming the framework
            // frame so automatic Purify/Recuperate remain independent.
            probe.ConsumeHeldGameplayKeys();
            input = input with
            {
                FreshGameplayKeyPressed = false,
                FreshGameplayKey = VirtualKey.NO_KEY,
                HeldGameplayKeyEligible = false,
                HeldGameplayKey = VirtualKey.NO_KEY,
                HeldMovementKeyEligible = false,
                HeldMovementKey = VirtualKey.NO_KEY,
            };
        }

        return new EmergencyActionInputFrame(
            input,
            probe,
            onFrameConsumed,
            heldHelperReservationEnabled,
            heldHelperReservationWindowMilliseconds,
            observationMilliseconds);
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
            emergencyTeleportHeldEnabled: false,
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
        astrologianHarmonicOrbisHeldWasEnabled = false;
        redMageGuardEngageHeldWasEnabled = false;
        emergencyTeleportHeldWasEnabled = false;
        pressureEscapeHeldWasEnabled = false;
        smartSprintHeldWasEnabled = false;
        darkKnightPlungeHeldWasEnabled = false;
        ninjaGuardShukuchiHeldWasEnabled = false;
        ninjaSeitonHeldWasEnabled = false;
        viperSerpentTailHeldWasEnabled = false;
        gunbreakerContinuationHeldWasEnabled = false;
        darkKnightShadowbringerHeldWasEnabled = false;
        monkHeldComboWasEnabled = false;
        samuraiCounterCcHeldWasEnabled = false;
        samuraiZantetsukenHeldWasEnabled = false;
    }
}
