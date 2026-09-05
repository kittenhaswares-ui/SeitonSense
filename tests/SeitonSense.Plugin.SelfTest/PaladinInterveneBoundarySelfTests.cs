using SeitonSense.Core;
using SeitonSense.Plugin.Services;

internal static class PaladinInterveneBoundarySelfTests
{
    internal static void SafetyChangesBeforeDispatchVetoNativeAttempt()
    {
        const uint action = MiracleInterceptConfirmationRules.InterveneActionId;
        foreach (var changed in new[]
                 {
                     PaladinInterveneBlockReason.OwnGuard,
                     PaladinInterveneBlockReason.LowMp,
                     PaladinInterveneBlockReason.ProtectingAlly,
                     PaladinInterveneBlockReason.LocalPlayerUnavailable,
                 })
        {
            var liveReason = PaladinInterveneBlockReason.None;
            var calls = 0;
            Check(liveReason == PaladinInterveneBlockReason.None, "target selected while eligible");
            liveReason = changed;
            var accepted = PaladinInterveneSafetyBoundary.TryInvoke(
                action, () => liveReason, () => { calls++; return true; }, out var attempted);
            Check(!accepted && !attempted && calls == 0,
                $"{changed} after selection must reach no native call");
        }

        var rejectedCalls = 0;
        Check(!PaladinInterveneSafetyBoundary.TryInvoke(action, () => PaladinInterveneBlockReason.None,
                  () => { rejectedCalls++; return false; }, out var rejectedAttempted) &&
              rejectedAttempted && rejectedCalls == 1,
            "a native false result remains an attempted call, not a safety veto");
    }

    internal static void OtherCountersDoNotReadPaladinSafety()
    {
        var calls = 0;
        var accepted = PaladinInterveneSafetyBoundary.TryInvoke(
            MiracleInterceptConfirmationRules.SilentNocturneActionId,
            () => throw new InvalidOperationException("BRD must not read PLD-only state"),
            () => { calls++; return true; }, out var attempted);
        Check(accepted && attempted && calls == 1, "other CC helpers retain their normal native dispatch");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
