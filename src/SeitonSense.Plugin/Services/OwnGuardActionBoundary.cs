namespace SeitonSense.Plugin.Services;

/// <summary>Fresh helper preflight; the central hook remains the final backstop.</summary>
internal static class OwnGuardActionBoundary
{
    internal static bool Invoke(
        Func<bool> readOwnGuardActiveOrPropagating,
        Func<bool> invokeAction,
        out bool attempted)
    {
        attempted = false;
        try
        {
            if (readOwnGuardActiveOrPropagating()) return false;
        }
        catch
        {
            // Unknown protection state must not spend an automatic action.
            return false;
        }

        attempted = true;
        return invokeAction();
    }
}
