using System.Reflection;
using Dalamud.Game.ClientState.Keys;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

internal static class ServiceScenarioSelfTests
{
    // These tests call real service transitions. Only game reads are replaced;
    // no Dalamud service, hook, live process, file scan, or action is started.
    public static void AstRejectedThenAcceptedRetryKeepsDoubleCast()
    {
        var reads = 0;
        var probe = NewAst(() =>
        {
            reads++;
            return AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId;
        });
        var intent = ArmAst(probe, doubleCastReady: true);

        probe.CompleteAttempt(intent, AstrologianHarmonicOrbisRules.BaseDispatchAction,
            ClientActionAttemptOutcome.ClientRejected, 1_000);
        Require(Get<HeldActionRetryState>(probe, "retry").NativeAttemptCount == 1,
            "The actual service retains the first rejected attempt.");
        Require(reads == 0, "A rejected Orbis cannot inspect or promote Double Cast.");

        Set(probe, "frameworkFrame", 105UL);
        probe.CompleteAttempt(intent, AstrologianHarmonicOrbisRules.BaseDispatchAction,
            ClientActionAttemptOutcome.ClientAccepted, 1_060);
        Require(Get<AstrologianHarmonicOrbisProbePhase>(probe, "phase") ==
            AstrologianHarmonicOrbisProbePhase.AwaitingDoubleCast,
            "A later accepted retry must preserve the real service follow-up phase.");
        var sequence = Get<AstrologianHarmonicOrbisIntent>(probe, "sequenceIntent");
        Require(sequence.OrbisFrameworkFrame == 105,
            "The service itself stamps the accepted frame; the test never does it.");
        Require(sequence.Target == intent.Target && reads == 1,
            "Accepted Orbis retains its frozen ally and reads the live carrier once.");

        var followUp = AstrologianHarmonicOrbisRules.EvaluateFollowUp(
            sequence, Get<ClientActionAttemptOutcome>(probe, "acceptedBaseOutcome"),
            106, intent.Target, true,
            AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId);
        Require(followUp.ShouldDispatch && followUp.Target == intent.Target &&
            followUp.Action == AstrologianHarmonicOrbisRules.DoubleCastDispatchAction,
            "The next frame exposes only the same-target Orbis carrier.");
        Set(probe, "frameworkFrame", 106UL);
        probe.CompleteAttempt(intent, followUp.Action,
            ClientActionAttemptOutcome.ClientAccepted, 1_080);
        Require(Get<AstrologianHarmonicOrbisProbePhase>(probe, "phase") ==
            AstrologianHarmonicOrbisProbePhase.Waiting,
            "Accepted follow-up clears the real service episode.");
        Require(Get<AstrologianHarmonicOrbisProbe.FrozenIntent?>(probe, "frozenIntent") is null,
            "The finished service does not retain an old heal target.");
    }

    public static void AstUnknownAndUnavailableFollowUpsEndCleanly()
    {
        var reads = 0;
        var probe = NewAst(() => { reads++; return 29_247; });
        var intent = ArmAst(probe, doubleCastReady: true);
        probe.CompleteAttempt(intent, AstrologianHarmonicOrbisRules.BaseDispatchAction,
            ClientActionAttemptOutcome.AcceptanceUnknown, 1_000);
        Require(reads == 0 && Get<AstrologianHarmonicOrbisProbePhase>(probe, "phase") ==
            AstrologianHarmonicOrbisProbePhase.Waiting,
            "Ambiguous base acceptance cannot promote or retry the old episode.");

        intent = ArmAst(probe, doubleCastReady: false);
        probe.CompleteAttempt(intent, AstrologianHarmonicOrbisRules.BaseDispatchAction,
            ClientActionAttemptOutcome.ClientAccepted, 1_050);
        Require(Get<AstrologianHarmonicOrbisProbePhase>(probe, "phase") ==
            AstrologianHarmonicOrbisProbePhase.Waiting,
            "Orbis-only acceptance does not invent a Double Cast charge.");
        probe.Reset();
        Require(probe.Snapshot.Phase == AstrologianHarmonicOrbisProbePhase.Waiting &&
            probe.Snapshot.HeldGameplayKey == VirtualKey.NO_KEY,
            "Reset clears both the actual phase and the held-key latch.");
    }

    public static void PredictionIncompleteRosterRetriesAndFreezesOnce()
    {
        var calls = 0;
        bool Read(out CrystallineConflictPredictionService.PlayerRuntime[] rows)
        {
            rows = ++calls == 1 ? [] : CompleteRoster();
            return calls > 1;
        }
        var service = NewPrediction(Read);
        Require(!service.TryPrepareRoster(), "First incomplete native roster waits.");
        Require(service.Snapshot.IsActive && !service.Snapshot.IsComplete,
            "Preparation stays visible while players are still loading.");
        Require(Get<CrystallineConflictPredictionService.PlayerRuntime[]?>(service, "roster") is null,
            "Failed capture cannot publish an empty non-null roster.");
        Require(service.TryPrepareRoster() && calls == 2,
            "The next service update retries and accepts the complete roster.");
        Require(service.TryPrepareRoster() && calls == 2,
            "A valid frozen roster is reused, not rescanned on every update.");
        Require(Get<long>(service, "matchGeneration") == 1,
            "One successful capture creates exactly one match generation.");
        service.Dispose();
        Require(!service.TryPrepareRoster() && calls == 2,
            "Disposal does not allow another roster read.");
    }

    public static void PredictionMalformedSuccessDoesNotPoisonNextFrame()
    {
        var calls = 0;
        bool Read(out CrystallineConflictPredictionService.PlayerRuntime[] rows)
        {
            rows = ++calls == 1 ? [] : CompleteRoster();
            return true;
        }
        var service = NewPrediction(Read);
        Require(!service.TryPrepareRoster(), "An empty success result is still incomplete.");
        Require(service.TryPrepareRoster() && calls == 2,
            "An invalid successful read cannot stop the next real roster retry.");
        service.Dispose();
    }

    private static AstrologianHarmonicOrbisProbe NewAst(Func<uint> readCarrier) =>
        new(null!, null!, null!, null!, null!, readCarrier);

    private static AstrologianHarmonicOrbisProbe.FrozenIntent ArmAst(
        AstrologianHarmonicOrbisProbe probe, bool doubleCastReady)
    {
        var local = new TargetPressureActorIdentity(0x1001, 0x2001);
        var target = new TargetPressureActorIdentity(0x1002, 0x2002);
        var intent = new AstrologianHarmonicOrbisProbe.FrozenIntent(
            local, target, (nint)1, (nint)2, 250,
            SupportedPvPContext.WolvesDen, 2, false, VirtualKey.W, 1, 1_000);
        Set(probe, "frozenIntent", intent);
        Set(probe, "phase", AstrologianHarmonicOrbisProbePhase.BaseBuffered);
        Set(probe, "frameworkFrame", 100UL);
        Set(probe, "retry", HeldActionRetryState.Initial);
        Set(probe, "baseChargeEpoch", new AstrologianHarmonicOrbisBaseChargeEpochState(true, 2, 11, 0));
        Set(probe, "sequenceIntent", new AstrologianHarmonicOrbisIntent(target, 2, doubleCastReady, 11, 100));
        return intent;
    }

    private static CrystallineConflictPredictionService NewPrediction(
        CrystallineConflictPredictionService.RosterReader read) =>
        new(new PluginConfiguration(), null!, null!, null!, null!, null!, null!,
            null!, null!, new CrystallineConflictPredictionCaptureBuffer(), null!, read);

    private static CrystallineConflictPredictionService.PlayerRuntime[] CompleteRoster() =>
        Enumerable.Range(0, 10).Select(index => new CrystallineConflictPredictionService.PlayerRuntime(
            index % 5 + 1, (uint)(100 + index), $"test-{index}", $"Test {index}",
            1, 33, index < 5, index == 0)).ToArray();

    private static FieldInfo Field(object owner, string name) =>
        owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new InvalidOperationException($"Missing tested service state: {name}");
    private static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    private static T Get<T>(object owner, string name) => (T)Field(owner, name).GetValue(owner)!;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
