using System.Reflection;
using System.Runtime.CompilerServices;
using SeitonSense.Plugin.Services;

internal static class PluginOwnedGuardBoundarySelfTests
{
    public static void ActualHelperScopeBlocksGuardButLeavesManualInputAlone()
    {
        var boundary = new PluginOwnedGuardBoundary();
        var owner = (NearAssistRedirector)RuntimeHelpers.GetUninitializedObject(typeof(NearAssistRedirector));
        typeof(NearAssistRedirector).GetField("pluginOwnedGuardBoundary", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(owner, boundary);
        var guard = false;
        Require(!owner.RunWithoutRedirect(() => boundary.ShouldBlock(false, false, () => guard)),
            "The actual helper scope allows its first action when Guard is absent.");
        guard = true;
        Require(owner.RunWithoutRedirect(() => boundary.ShouldBlock(false, false, () => guard)),
            "The actual helper scope must not cancel Guard, without requiring an Auto-Guard owner.");
        Require(!boundary.ShouldBlock(false, false, () => guard),
            "A fresh manual action outside the helper scope stays native.");

        try { owner.RunWithoutRedirect<bool>(() => throw new InvalidOperationException("fixture")); }
        catch (InvalidOperationException) { }
        Require(!boundary.ShouldBlock(false, false, () => true),
            "An exception cannot leak automatic ownership into future manual input.");
    }

    public static void EveryOwnedSourceChecksCurrentGuardAndHonorsExactEscape()
    {
        var boundary = new PluginOwnedGuardBoundary();
        var guard = false;
        Require(!boundary.ShouldBlock(true, false, () => guard), "Initial synthetic action remains allowed.");
        // An earlier eligibility snapshot was clear, but Guard won before this boundary.
        guard = true;
        Require(boundary.ShouldBlock(true, false, () => guard), "Turbo/Chase reread the final Guard state.");
        using (boundary.Enter())
        {
            Require(boundary.ShouldBlock(false, false, () => guard), "Ordinary automatic helpers also block.");
            Require(!boundary.ShouldBlock(false, true, () => throw new InvalidOperationException()),
                "An already verified explicit panic escape remains an intentional Guard break.");
            guard = false;
            Require(!boundary.ShouldBlock(false, false, () => guard), "Action resumes as soon as Guard really ends.");
        }
    }

    public static void UncertainGuardBlocksOnlyOwnedRequestsAndNestedScopesRestore()
    {
        var first = new PluginOwnedGuardBoundary();
        var second = new PluginOwnedGuardBoundary();
        static bool Unknown() => throw new InvalidOperationException("unavailable game read");
        Require(!first.ShouldBlock(false, false, Unknown), "Manual input never depends on helper Guard telemetry.");
        using (first.Enter())
        {
            Require(first.ShouldBlock(false, false, Unknown), "Owned helpers stop on an unreadable Guard state.");
            using (second.Enter())
            {
                Require(!first.ShouldBlock(false, false, Unknown), "Another owner cannot inherit the outer scope.");
                Require(second.ShouldBlock(false, false, Unknown), "Nested owner retains its own protection.");
            }
            Require(first.ShouldBlock(false, false, Unknown), "Disposal restores the exact previous owner.");
        }
        Require(!first.ShouldBlock(false, false, Unknown), "All ownership is released after the callback.");
    }

    public static void ExactQueuedGuardContinuationRequiresLiveGuardToVeto()
    {
        var boundary = new PluginOwnedGuardBoundary();
        var liveGuard = false;
        Require(!boundary.ShouldBlock(true, false, () => true,
                exactOwnedQueuedGuardContinuation: true, readLiveGuard: () => liveGuard),
            "an exact owned native Guard continuation may activate despite its submission propagation marker");
        liveGuard = true;
        Require(boundary.ShouldBlock(true, false, () => true,
                exactOwnedQueuedGuardContinuation: true, readLiveGuard: () => liveGuard),
            "the same queued Guard cannot cancel a genuinely live Guard");
        Require(boundary.ShouldBlock(true, false, () => true,
                exactOwnedQueuedGuardContinuation: false, readLiveGuard: () => false),
            "queued Recuperate and other helpers remain blocked by Guard propagation");
        using (boundary.Enter())
            Require(boundary.ShouldBlock(false, false, () => true,
                    exactOwnedQueuedGuardContinuation: false, readLiveGuard: () => false),
                "a fresh helper Guard repeat cannot borrow the exact native continuation exception");
        Require(boundary.ShouldBlock(true, false, () => false,
                exactOwnedQueuedGuardContinuation: true, readLiveGuard: () => throw new InvalidOperationException()),
            "unreadable live Guard still blocks an owned continuation");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
