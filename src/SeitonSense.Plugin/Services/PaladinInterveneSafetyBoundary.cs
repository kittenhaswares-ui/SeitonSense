using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal static class PaladinInterveneSafetyBoundary
{
    internal static bool TryInvoke(
        uint actionId,
        Func<PaladinInterveneBlockReason> readCurrentSafety,
        Func<bool> invoke,
        out bool attempted)
    {
        attempted = false;
        if (actionId == MiracleInterceptConfirmationRules.InterveneActionId &&
            readCurrentSafety() != PaladinInterveneBlockReason.None)
            return false;
        attempted = true;
        return invoke();
    }
}
