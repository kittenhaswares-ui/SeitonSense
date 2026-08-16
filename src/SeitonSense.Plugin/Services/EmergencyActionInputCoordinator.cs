using Dalamud.Plugin.Services;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// One framework-frame view of the shared physical gameplay-key generations used
/// by emergency self-Purify, defensive utilities, ally rescue, Miracle
/// intercept, and fresh-key Ninja Seiton. Consumption is deliberately shared:
/// once any helper claims a generation, every later helper sees no input.
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
    private bool allyRescueHeldWasEnabled;
    private bool miracleInterceptHeldWasEnabled;

    internal EmergencyActionInputCoordinator(IKeyState keyState)
    {
        probe = new GameInputContextProbe(keyState);
    }

    internal EmergencyActionInputFrame Observe(
        bool shouldObserve,
        bool purifyHeldEnabled,
        bool defensiveUtilityHeldEnabled,
        bool allyRescueHeldEnabled,
        bool miracleInterceptHeldEnabled)
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
            (allyRescueHeldEnabled && !allyRescueHeldWasEnabled) ||
            (miracleInterceptHeldEnabled && !miracleInterceptHeldWasEnabled);
        purifyHeldWasEnabled = purifyHeldEnabled;
        defensiveUtilityHeldWasEnabled = defensiveUtilityHeldEnabled;
        allyRescueHeldWasEnabled = allyRescueHeldEnabled;
        miracleInterceptHeldWasEnabled = miracleInterceptHeldEnabled;

        if (heldOptionJustEnabled)
        {
            // Preserve a same-frame down-edge as fresh intent, but never let the
            // held-level branch inherit a generation from before the opt-in.
            probe.ConsumeHeldGameplayKeys();
            input = input with
            {
                HeldGameplayKeyEligible = false,
                HeldGameplayKey = Dalamud.Game.ClientState.Keys.VirtualKey.NO_KEY,
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
            allyRescueHeldEnabled,
            miracleInterceptHeldEnabled: false);

    internal void Reset()
    {
        probe.Reset();
        purifyHeldWasEnabled = false;
        defensiveUtilityHeldWasEnabled = false;
        allyRescueHeldWasEnabled = false;
        miracleInterceptHeldWasEnabled = false;
    }
}
