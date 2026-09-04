using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

internal static class SamuraiMovementBoundarySelfTests
{
    internal static void BufferedReplayRetainsTypedOwner()
    {
        var owner = SmartActionTapOwnership.Capture(7, samurai: true);
        var request = default(IntegratedActionBufferDispatchRequest) with
        {
            ActionType = ActionType.PvPAction,
            RequestedActionId = SamuraiSmartActionCastRules.OgiNamikiriActionId,
            ResolvedActionId = SamuraiSmartActionCastRules.OgiNamikiriActionId,
            TargetId = 0x1234,
            RequiresSmartActionProtectionRecheck = true,
            Ownership = owner,
        };
        var replay = IntegratedInputRuntime.CreateBufferedReplayIntent(request);
        True(replay.IsValid && replay.Ownership == owner,
            "the actual dispatcher conversion retains the frozen SAM origin and generation");
        True(replay.TargetId == request.TargetId && replay.RequestedActionId == request.RequestedActionId,
            "ownership travels with the same exact native action and target");

        request = request with { Ownership = SmartActionTapOwnership.Capture(8, samurai: false) };
        var ordinary = IntegratedInputRuntime.CreateBufferedReplayIntent(request);
        True(ordinary.IsValid && !ordinary.Ownership.RequiresSamuraiCastProtection,
            "an ordinary Smart Action replay never acquires SAM movement protection");
        True(replay.Ownership == owner, "a newer request cannot mutate the frozen earlier owner");
        False((replay with { RequiresSmartActionProtectionRecheck = false }).IsValid,
            "a SAM replay cannot drop its target protections");
        False((replay with { ResolvedActionId = SamuraiSmartActionCastRules.OgiNamikiriFollowUpActionId }).IsValid,
            "an adjusted follow-up cannot inherit the frozen cast owner");
    }

    internal static void GameplayControlAndDigitalPathsShareExactOwnership()
    {
        var ownedCast = false;
        var calls = new List<(nint, uint, SamuraiMovementInputPath)>();
        var boundary = new SamuraiCastMovementInputBoundary(
            (address, code, path) => { calls.Add((address, code, path)); return true; },
            () => ownedCast);

        True(boundary.Read(123, (uint)InputCode.MOVE_FORE, SamuraiMovementInputPath.ControlState),
            "arming alone leaves native movement available");
        ownedCast = true;
        False(boundary.Read(123, (uint)InputCode.MOVE_FORE, SamuraiMovementInputPath.ControlState),
            "the gameplay-control query is suppressed during the owned cast");
        False(boundary.Read(124, 321, SamuraiMovementInputPath.Down),
            "the already-held digital key is suppressed during the same cast");
        False(boundary.Read(0, 0, SamuraiMovementInputPath.Autorun),
            "autorun is temporarily hidden from gameplay queries");
        True(boundary.Read(123, (uint)InputCode.CAMERA_LEFT, SamuraiMovementInputPath.ControlState),
            "camera input remains native");
        ownedCast = false; // exact native cast disappears after CC/knockback/Guard
        True(boundary.Read(123, (uint)InputCode.MOVE_FORE, SamuraiMovementInputPath.ControlState),
            "movement is immediately available when cast ownership ends");
        True(calls.Count == 6, "every native original executes exactly once");
        True(calls[1] == ((nint)123, (uint)InputCode.MOVE_FORE, SamuraiMovementInputPath.ControlState),
            "the native reader receives the original address, code, and path");
        True(boundary.Diagnostics.SuppressedControlReads == 1 &&
             boundary.Diagnostics.SuppressedDigitalReads == 1 &&
             boundary.Diagnostics.SuppressedAutorunReads == 1,
            "diagnostics distinguish which input boundary actually suppressed movement");
    }

    internal static void OwnershipFailureAndRecursiveQueriesPreserveNativeInput()
    {
        var faulted = new SamuraiCastMovementInputBoundary((_, _, _) => true,
            () => throw new InvalidOperationException("missing cast snapshot"));
        True(faulted.Read(0, 112, SamuraiMovementInputPath.ControlState),
            "unavailable ownership never traps movement");
        True(faulted.Diagnostics.OwnershipReadFailures == 1, "the failure is observable");

        var ownershipCalls = 0;
        SamuraiCastMovementInputBoundary? recursive = null;
        recursive = new SamuraiCastMovementInputBoundary((_, _, _) => true, () =>
        {
            ownershipCalls++;
            True(recursive!.Read(0, 112, SamuraiMovementInputPath.ControlState),
                "a nested input query preserves native input without recursively checking ownership");
            return true;
        });
        False(recursive.Read(0, 112, SamuraiMovementInputPath.ControlState),
            "the original outer request is still suppressed");
        True(ownershipCalls == 1, "ownership reads cannot recurse");

        var falseNative = new SamuraiCastMovementInputBoundary((_, _, _) => false,
            () => throw new InvalidOperationException("must not be called"));
        False(falseNative.Read(0, 112, SamuraiMovementInputPath.ControlState),
            "native false stays false without touching ownership");
        True(falseNative.Diagnostics.OwnershipReadFailures == 0, "no invented input or unnecessary read");
    }

    internal static void GameplayControlCodesMatchDependencyMetadata()
    {
        foreach (var code in Enum.GetValues<InputCode>())
        {
            var movement = code.ToString().StartsWith("MOVE_", StringComparison.Ordinal);
            True(SamuraiOgiCastProtectionRules.IsMovementControlCode((uint)code) == movement,
                $"exact exported gameplay code {code} has the expected movement classification");
        }
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);
}
