using SeitonSense.Core;

internal static class HelperStatusPresentationSelfTests
{
    internal static void JobFilterPreservesDiscoverability()
    {
        Check(HelperStatusPresentationRules.ShowJob(33, 33, false), "current job is visible");
        Check(!HelperStatusPresentationRules.ShowJob(33, 30, false), "other job is filtered by default");
        Check(HelperStatusPresentationRules.ShowJob(33, 30, true), "show all keeps other settings accessible");
        Check(HelperStatusPresentationRules.ShowJob(0, 30, false), "unavailable local job never hides all settings");
    }

    internal static void CurrentGatesOverrideStaleActionSamples()
    {
        var disabled = Describe(enabled: false, accepted: true);
        Check(disabled.State == "Off", "disabled wins over stale accepted sample");
        var outside = Describe(supported: false, accepted: true);
        Check(outside.State == "Paused", "unsupported context cannot claim a current action");
        var guard = Describe(guard: true, accepted: true);
        Check(guard.State == "Paused" && guard.Detail.Contains("Guard", StringComparison.Ordinal),
            "current Guard pause remains visible");
        var accepted = Describe(accepted: true);
        Check(accepted.State == "Accepted" && accepted.Detail.Contains("not confirmed", StringComparison.Ordinal),
            "client acceptance is never described as a confirmed effect");
        var attempted = Describe(attempted: true);
        Check(attempted.State == "Attempted", "a false request cannot look successful");
    }

    internal static void ReasonsStaySimpleWithoutInventingUnknownBlockers()
    {
        var held = Describe(reason: "NoHeldGameplayKey");
        Check(held.Detail.Contains("gameplay key", StringComparison.Ordinal), "missing held input is explained");
        var busy = Describe(reason: "NativeBoundaryUnavailable");
        Check(busy.Detail.Contains("cast, animation, or queued action", StringComparison.Ordinal), "native busy explanation");
        var mp = Describe(reason: "InsufficientMp");
        Check(mp.Detail == "Not enough MP for this action.", "known MP blocker stays precise");
        var gated = Describe(reason: "ConfigurationDisabled");
        Check(gated.State == "Paused", "runtime gating must not misreport the saved enabled toggle as off");
        var recovery = Describe(reason: "RecoveryProtected");
        Check(recovery.Detail.Contains("stealth", StringComparison.Ordinal),
            "mixed recovery suppression must not be described as proven Guard");
        var unknown = Describe(reason: "FutureReasonNotYetMapped");
        Check(unknown.State == "Waiting" && unknown.Detail == "Watching the configured trigger.",
            "unknown reasons retain neutral wording instead of guessed readiness");
    }

    private static HelperStatusPresentation Describe(bool enabled = true, bool supported = true,
        bool guard = false, bool accepted = false, bool attempted = false, string reason = "None") =>
        HelperStatusPresentationRules.Describe(enabled, supported, guard, accepted, attempted,
            reason, "Watching the configured trigger.");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
