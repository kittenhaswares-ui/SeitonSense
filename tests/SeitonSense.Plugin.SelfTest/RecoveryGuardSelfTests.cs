using System.Reflection;
using System.Runtime.CompilerServices;
using SeitonSense.Plugin.Services;

internal static class RecoveryGuardSelfTests
{
    public static void OnlyAcceptedGuardAttemptsSuppressHelpers()
    {
        const uint territoryId = 250;
        const ulong localGameObjectId = 0x1001;
        const uint localEntityId = 0x2001;
        // This test exercises only the managed observation reader. No game
        // services, native hooks, or live FFXIV process are created or touched.
        var owner = (NearAssistRedirector)RuntimeHelpers.GetUninitializedObject(
            typeof(NearAssistRedirector));
        SetField(owner, "guardAttemptGate", new object());
        var rejected = new LocalGuardActionAttempt(
            territoryId,
            localGameObjectId,
            localEntityId,
            ObservedAtMilliseconds: 1_000,
            ClientAccepted: false,
            GuardActivatedAtMilliseconds: -1,
            Generation: 1);
        SetField(owner, "latestLocalGuardActionAttempt", rejected);

        Require(!Read(owner, territoryId, localGameObjectId, localEntityId, 1_100, out var timestamp),
            "A false or ambiguous Guard result cannot start helper suppression.");
        Require(timestamp == -1, "An unaccepted Guard must not expose a propagation timestamp.");

        SetField(owner, "latestLocalGuardActionAttempt", rejected with { ClientAccepted = true });
        Require(Read(owner, territoryId, localGameObjectId, localEntityId, 1_100, out timestamp),
            "A matching accepted Guard retains its propagation protection.");
        Require(timestamp == 1_000, "Propagation uses the original accepted request time.");
        Require(!Read(owner, territoryId, localGameObjectId, localEntityId, 2_500, out _),
            "An expired accepted Guard cannot keep suppressing helpers.");
        Require(!Read(owner, territoryId + 1, localGameObjectId, localEntityId, 1_100, out _),
            "A different territory cannot inherit the old Guard.");
        Require(!Read(owner, territoryId, localGameObjectId + 1, localEntityId, 1_100, out _),
            "A different actor cannot inherit the old Guard.");
        Require(!Read(owner, territoryId, localGameObjectId, localEntityId + 1, 1_100, out _),
            "A different entity cannot inherit the old Guard.");
        Require(!Read(owner, territoryId, localGameObjectId, localEntityId, 999, out _),
            "A future-dated Guard cannot start helper suppression.");
    }

    private static bool Read(
        NearAssistRedirector owner,
        uint territoryId,
        ulong localGameObjectId,
        uint localEntityId,
        long nowMilliseconds,
        out long observedAtMilliseconds) =>
        owner.TryGetRecentExactLocalGuardAttempt(
            territoryId,
            localGameObjectId,
            localEntityId,
            nowMilliseconds,
            maximumAgeMilliseconds: 1_500,
            out observedAtMilliseconds);

    private static void SetField(NearAssistRedirector owner, string name, object value)
    {
        var field = typeof(NearAssistRedirector).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException($"Missing Guard observation field: {name}");
        field.SetValue(owner, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
