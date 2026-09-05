using System.Reflection;
using System.Runtime.CompilerServices;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

internal static class RecoveryGuardSelfTests
{
    public static void GuardianFallbackSettingsDefaultClampAndReset()
    {
        var config = System.Text.Json.JsonSerializer.Deserialize<PluginConfiguration>("{}")!;
        Require(config.GuardianNoGuardMinimumHpPercent == 80 && config.GuardianNoGuardMinimumMpPercent == 60,
            "Existing configurations without the new properties receive 80% HP and 60% MP defaults.");
        config.GuardianNoGuardMinimumHpPercent = -10;
        config.GuardianNoGuardMinimumMpPercent = 120;
        config.Initialize(null!); // No Dalamud service or configuration file is used.
        Require(config.GuardianNoGuardMinimumHpPercent == 0 && config.GuardianNoGuardMinimumMpPercent == 100,
            "Loaded settings clamp to the supported 0–100 range.");
        config.GuardianNoGuardMinimumHpPercent = 45;
        config.GuardianNoGuardMinimumMpPercent = 35;
        var restored = System.Text.Json.JsonSerializer.Deserialize<PluginConfiguration>(
            System.Text.Json.JsonSerializer.Serialize(config))!;
        Require(restored.GuardianNoGuardMinimumHpPercent == 45 && restored.GuardianNoGuardMinimumMpPercent == 35,
            "Both user thresholds survive configuration serialization.");
        restored.ResetToDefaults();
        Require(restored.GuardianNoGuardMinimumHpPercent == 80 && restored.GuardianNoGuardMinimumMpPercent == 60,
            "Reset restores both intended defaults.");
    }

    public static void ManualGuardWinsLateRecuperateAndGuardianPreflight()
    {
        foreach (var helper in new[] { "Recuperate", "Guardian" })
        {
            var guardActive = false;
            var calls = 0;
            var earlierEligibilitySample = !guardActive;
            Require(earlierEligibilitySample, $"{helper} was ready before manual Guard.");

            // Manual Guard lands after the helper's earlier eligibility sample.
            // This is the real shared final probe boundary, not copied logic.
            guardActive = true;
            var accepted = OwnGuardActionBoundary.Invoke(
                () => guardActive,
                () => { calls++; return true; },
                out var attempted);
            Require(!accepted && !attempted && calls == 0,
                $"A fresh manual Guard must stop {helper} before the action call.");

            guardActive = false;
            accepted = OwnGuardActionBoundary.Invoke(
                () => guardActive,
                () => { calls++; return true; },
                out attempted);
            Require(accepted && attempted && calls == 1,
                $"After Guard ends {helper} is not artificially delayed.");
        }
    }

    public static void AcceptedPropagationAndWholeGuardBlockRecovery()
    {
        Require(DefensiveUtilityRules.IsOwnGuardStatusPresent(EnemyCombatConstants.GuardStatusId) &&
            DefensiveUtilityRules.IsOwnGuardStatusPresent(EnemyCombatConstants.GuardStatusAlternateId),
            "Both exact Guard status slots protect helpers independently of a duration read.");
        Require(!DefensiveUtilityRules.IsOwnGuardStatusPresent(0) &&
            !DefensiveUtilityRules.IsOwnGuardStatusPresent(1),
            "Empty or unrelated statuses cannot invent a Guard.");

        var state = GuardPropagationState.Initial;
        var calls = 0;
        void Check(bool liveGuard, long acceptedAt, long now, bool expectedBlocked)
        {
            var observed = DefensiveUtilityRules.ObserveGuardPropagation(
                state, liveGuard, acceptedAt, now);
            state = observed.NextState;
            var blocked = SmartRecuperateRules.ShouldSuppressForOwnGuard(
                observed.ExactGuardActive || observed.PropagationLatchActive);
            var callsBefore = calls;
            OwnGuardActionBoundary.Invoke(() => blocked, () => { calls++; return true; },
                out var attempted);
            Require(attempted != expectedBlocked && calls == callsBefore + (expectedBlocked ? 0 : 1),
                $"Guard protection at {now} must be independent of the repeat-Guard lock.");
        }

        Check(false, 1_000, 1_010, true); // accepted request, status not visible
        Check(false, 1_000, 1_300, true); // propagation does not require live status
        Check(true, 1_000, 1_500, true);  // exact status takes ownership
        Check(true, -1, 3_500, true);    // well past the one-second repeat lock
        Check(true, -1, 5_000, true);    // full remaining Guard, not just start
        Check(false, -1, 6_000, false);  // status removed: recovery resumes
    }

    public static void GuardPreflightReadFailureDoesNotBecomeAnAttempt()
    {
        var calls = 0;
        var accepted = OwnGuardActionBoundary.Invoke(
            () => throw new InvalidOperationException("unreadable Guard"),
            () => { calls++; return true; }, out var attempted);
        Require(!accepted && !attempted && calls == 0,
            "Unknown protection cannot send recovery or spend its native retry budget.");

        accepted = OwnGuardActionBoundary.Invoke(() => false, () => { calls++; return false; },
            out attempted);
        Require(!accepted && attempted && calls == 1,
            "An actual clean rejection remains distinguishable from Guard suppression.");

        attempted = false;
        try
        {
            OwnGuardActionBoundary.Invoke(() => false,
                () => throw new InvalidOperationException("native failure"), out attempted);
            throw new InvalidOperationException("The native failure should propagate.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "native failure")
        {
            Require(attempted, "A thrown native call remains an ambiguous attempted request.");
        }
    }

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
        SetField(owner, "latestClientAcceptedLocalGuardActionAttempt", rejected);

        Require(!Read(owner, territoryId, localGameObjectId, localEntityId, 1_100, out var timestamp),
            "A false or ambiguous Guard result cannot start helper suppression.");
        Require(timestamp == -1, "An unaccepted Guard must not expose a propagation timestamp.");

        SetField(owner, "latestClientAcceptedLocalGuardActionAttempt", rejected with { ClientAccepted = true });
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

    public static void AmbiguousRepeatPreservesOriginalAcceptedGuardProof()
    {
        const uint territory = 250;
        const ulong local = 0x1001;
        const uint entity = 0x2001;
        var owner = (NearAssistRedirector)RuntimeHelpers.GetUninitializedObject(typeof(NearAssistRedirector));
        SetField(owner, "guardAttemptGate", new object());
        var first = new LocalGuardActionAttempt(territory, local, entity, 1_000, false, -1, 1);
        SetField(owner, "latestLocalGuardActionAttempt", first);
        var markAccepted = typeof(NearAssistRedirector).GetMethod(
            "MarkClientAcceptedLocalGuardAttempt", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var boundary = new LocalGuardActionBoundaryObservation(local, entity, 0, null,
            default(ClientActionAttemptFingerprint) with { Captured = true });
        markAccepted.Invoke(owner, [boundary]);
        Require(Read(owner, territory, local, entity, 1_050, out var time) && time == 1_000,
            "The actual exact-generation acceptance method records the original proof.");

        // The next native call returns ambiguous: its provisional record is
        // neither accepted nor cleanly retracted, exactly as in the detour.
        SetField(owner, "latestLocalGuardActionAttempt", first with
        {
            Generation = 2, ObservedAtMilliseconds = 1_100, ClientAccepted = false,
        });
        Require(Read(owner, territory, local, entity, 1_200, out time) && time == 1_000,
            "A later ambiguous press cannot erase or refresh already accepted propagation.");
        var readAccepted = typeof(NearAssistRedirector).GetMethod(
            "TryGetRecentExactClientAcceptedLocalGuardAttempt", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object?[] acceptedArgs = [territory, local, entity, 1_200L, 1_500L, -1L];
        Require((bool)readAccepted.Invoke(owner, acceptedArgs)! && (long)acceptedArgs[5]! == 1_000,
            "The native-queue/repeat reader uses the same independent accepted proof.");
        markAccepted.Invoke(owner, [boundary]); // stale completion for generation 1
        Require(Read(owner, territory, local, entity, 1_300, out time) && time == 1_000,
            "An old completion cannot accept the replacement or extend protection.");
        Require(!Read(owner, territory + 1, local, entity, 1_200, out _) &&
                !Read(owner, territory, local + 1, entity, 1_200, out _) &&
                !Read(owner, territory, local, entity + 1, 1_200, out _) &&
                !Read(owner, territory, local, entity, 999, out _) &&
                !Read(owner, territory, local, entity, 2_500, out _),
            "Accepted proof remains exact-context, rollback-safe and bounded by its original timestamp.");
        owner.ClearLocalGuardActionObservations();
        Require(!Read(owner, territory, local, entity, 1_300, out _),
            "The actual reset/dispose seam clears accepted as well as provisional observations.");
        Require(!(bool)readAccepted.Invoke(owner, acceptedArgs)!,
            "Reset also clears the strict accepted-only native-queue reader.");
        markAccepted.Invoke(owner, [boundary]);
        Require(!Read(owner, territory, local, entity, 1_300, out _),
            "A late completion cannot resurrect Guard after reset.");
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
