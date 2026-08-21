using Dalamud.Game.ClientState.Keys;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal readonly record struct GameInputContextSnapshot(
    bool ProbeSucceeded,
    bool IsTextInputActive,
    bool FreshGameplayKeyPressed,
    VirtualKey FreshGameplayKey,
    bool HeldGameplayKeyEligible,
    VirtualKey HeldGameplayKey,
    bool HeldMovementKeyEligible,
    VirtualKey HeldMovementKey)
{
    internal static GameInputContextSnapshot NotObserved => new(
        false,
        false,
        false,
        VirtualKey.NO_KEY,
        false,
        VirtualKey.NO_KEY,
        false,
        VirtualKey.NO_KEY);

    internal static GameInputContextSnapshot FailedClosed => new(
        false,
        true,
        false,
        VirtualKey.NO_KEY,
        false,
        VirtualKey.NO_KEY,
        false,
        VirtualKey.NO_KEY);
}

internal sealed class GameInputContextProbe
{
    private static readonly HashSet<int> CandidateVirtualKeyCodes = BuildCandidateVirtualKeyCodes();

    private readonly IKeyState keyState;
    private readonly VirtualKey[] gameplayKeys;
    private readonly PhysicalGameplayKeyState[] keyGenerations;
    private VirtualKey selectedHeldGameplayKey = VirtualKey.NO_KEY;
    private VirtualKey selectedHeldMovementKey = VirtualKey.NO_KEY;

    internal GameInputContextProbe(IKeyState keyState)
    {
        this.keyState = keyState;
        try
        {
            gameplayKeys = keyState
                .GetValidVirtualKeys()
                .Where(key => CandidateVirtualKeyCodes.Contains((int)key))
                .Distinct()
                .OrderBy(static key => (int)key)
                .ToArray();
        }
        catch
        {
            gameplayKeys = [];
        }

        keyGenerations = new PhysicalGameplayKeyState[gameplayKeys.Length];
    }

    internal unsafe GameInputContextSnapshot Observe()
    {
        if (gameplayKeys.Length == 0)
        {
            Reset();
            return GameInputContextSnapshot.FailedClosed;
        }

        try
        {
            var atkModule = RaptureAtkModule.Instance();
            if (atkModule == null)
            {
                Reset();
                return GameInputContextSnapshot.FailedClosed;
            }

            var io = ImGui.GetIO();
            // WantCaptureKeyboard is true for an ordinary focused ImGui window as
            // well, including our own settings window. It is not proof that the
            // player is typing and previously made the opt-in appear completely
            // inert while its checkbox was visible.
            var textInputActive = atkModule->IsTextInputActive() ||
                                  io.WantTextInput;
            var freshKey = VirtualKey.NO_KEY;
            var fallbackHeldKeyToken = 0;
            var fallbackHeldKeyIsFreshPress = false;
            var fallbackHeldKeyIsMovementKey = false;
            var fallbackHeldMovementKeyToken = 0;
            var fallbackHeldMovementKeyIsFreshPress = false;
            var selectedHeldGameplayKeyStillEligible = false;
            var selectedHeldMovementKeyStillEligible = false;
            for (var index = 0; index < gameplayKeys.Length; index++)
            {
                var pressed = keyState[gameplayKeys[index]];
                var decision = PhysicalGameplayKeyRules.Observe(
                    keyGenerations[index],
                    new PhysicalGameplayKeyObservation(pressed, textInputActive));
                keyGenerations[index] = decision.NextState;
                if (decision.IsFreshPress && freshKey == VirtualKey.NO_KEY)
                    freshKey = gameplayKeys[index];
                if (decision.IsHeldEligible)
                {
                    var candidateToken = (int)gameplayKeys[index];
                    var candidateIsMovementKey =
                        PressureEscapeRules.IsSupportedMovementVirtualKey(candidateToken);
                    selectedHeldGameplayKeyStillEligible |=
                        gameplayKeys[index] == selectedHeldGameplayKey;
                    selectedHeldMovementKeyStillEligible |=
                        candidateIsMovementKey && gameplayKeys[index] == selectedHeldMovementKey;
                    var selectedToken = PhysicalGameplayKeyRules.SelectPreferredHeldKeyToken(
                        fallbackHeldKeyToken,
                        fallbackHeldKeyIsFreshPress,
                        fallbackHeldKeyIsMovementKey,
                        candidateToken,
                        decision.IsFreshPress,
                        candidateIsMovementKey);
                    if (selectedToken != fallbackHeldKeyToken)
                    {
                        fallbackHeldKeyToken = selectedToken;
                        fallbackHeldKeyIsFreshPress = decision.IsFreshPress;
                        fallbackHeldKeyIsMovementKey = candidateIsMovementKey;
                    }

                    if (candidateIsMovementKey)
                    {
                        var selectedMovementToken =
                            PhysicalGameplayKeyRules.SelectPreferredHeldKeyToken(
                                fallbackHeldMovementKeyToken,
                                fallbackHeldMovementKeyIsFreshPress,
                                selectedIsMovementKey: true,
                                candidateToken,
                                decision.IsFreshPress,
                                candidateIsMovementKey: true);
                        if (selectedMovementToken != fallbackHeldMovementKeyToken)
                        {
                            fallbackHeldMovementKeyToken = selectedMovementToken;
                            fallbackHeldMovementKeyIsFreshPress = decision.IsFreshPress;
                        }
                    }
                }
            }

            var heldKeyToken = PhysicalGameplayKeyRules.RetainEligibleHeldKeyToken(
                (int)selectedHeldGameplayKey,
                selectedHeldGameplayKeyStillEligible,
                fallbackHeldKeyToken);
            var heldMovementKeyToken = PhysicalGameplayKeyRules.RetainEligibleHeldKeyToken(
                (int)selectedHeldMovementKey,
                selectedHeldMovementKeyStillEligible,
                fallbackHeldMovementKeyToken);
            var heldKey = heldKeyToken > 0
                ? (VirtualKey)heldKeyToken
                : VirtualKey.NO_KEY;
            var heldMovementKey = heldMovementKeyToken > 0
                ? (VirtualKey)heldMovementKeyToken
                : VirtualKey.NO_KEY;
            selectedHeldGameplayKey = heldKey;
            selectedHeldMovementKey = heldMovementKey;

            return new GameInputContextSnapshot(
                true,
                textInputActive,
                !textInputActive && freshKey != VirtualKey.NO_KEY,
                !textInputActive ? freshKey : VirtualKey.NO_KEY,
                !textInputActive && heldKey != VirtualKey.NO_KEY,
                !textInputActive ? heldKey : VirtualKey.NO_KEY,
                !textInputActive && heldMovementKey != VirtualKey.NO_KEY,
                !textInputActive ? heldMovementKey : VirtualKey.NO_KEY);
        }
        catch
        {
            Reset();
            return GameInputContextSnapshot.FailedClosed;
        }
    }

    internal void Reset()
    {
        Array.Clear(keyGenerations);
        selectedHeldGameplayKey = VirtualKey.NO_KEY;
        selectedHeldMovementKey = VirtualKey.NO_KEY;
    }

    internal void ConsumeHeldGameplayKeys()
    {
        for (var index = 0; index < keyGenerations.Length; index++)
        {
            if (!keyGenerations[index].IsDown) continue;
            keyGenerations[index] = PhysicalGameplayKeyRules.Consume(keyGenerations[index]);
        }

        selectedHeldGameplayKey = VirtualKey.NO_KEY;
        selectedHeldMovementKey = VirtualKey.NO_KEY;
    }

    /// <summary>
    /// Reports only the already-observed physical level for one exact supported
    /// gameplay key. Consumption deliberately does not erase IsDown, allowing a
    /// feature with an explicit continuous-hold contract to retain that exact
    /// key without making the shared generation eligible again.
    /// </summary>
    internal bool IsGameplayKeyPhysicallyDown(VirtualKey key)
    {
        if (key == VirtualKey.NO_KEY) return false;
        for (var index = 0; index < gameplayKeys.Length; index++)
        {
            if (gameplayKeys[index] != key) continue;
            return keyGenerations[index].IsPrimed && keyGenerations[index].IsDown;
        }

        return false;
    }

    /// <summary>
    /// Reports whether the exact observed key generation remains eligible.
    /// Unlike the physical-level check, this becomes false for a key pressed
    /// or retained through text input and stays false until a real release.
    /// </summary>
    internal bool IsGameplayKeyGenerationEligible(VirtualKey key)
    {
        if (key == VirtualKey.NO_KEY) return false;
        for (var index = 0; index < gameplayKeys.Length; index++)
        {
            if (gameplayKeys[index] != key) continue;
            var generation = keyGenerations[index];
            return generation.IsPrimed &&
                   generation.IsDown &&
                   generation.IsEligible &&
                   !generation.IsConsumed;
        }

        return false;
    }

    private static HashSet<int> BuildCandidateVirtualKeyCodes()
    {
        var keys = new HashSet<int>
        {
            (int)VirtualKey.TAB,
            (int)VirtualKey.SPACE,
            (int)VirtualKey.LEFT,
            (int)VirtualKey.UP,
            (int)VirtualKey.RIGHT,
            (int)VirtualKey.DOWN,
        };

        AddRange(keys, (int)VirtualKey.KEY_0, (int)VirtualKey.KEY_9);
        AddRange(keys, (int)VirtualKey.A, (int)VirtualKey.Z);
        AddRange(keys, (int)VirtualKey.NUMPAD0, (int)VirtualKey.DIVIDE);
        AddRange(keys, (int)VirtualKey.F1, (int)VirtualKey.F12);
        AddRange(keys, (int)VirtualKey.OEM_1, (int)VirtualKey.OEM_3);
        AddRange(keys, (int)VirtualKey.OEM_4, (int)VirtualKey.OEM_8);
        keys.Add((int)VirtualKey.OEM_102);
        return keys;
    }

    private static void AddRange(HashSet<int> keys, int first, int last)
    {
        for (var key = first; key <= last; key++) keys.Add(key);
    }
}
