using System.Numerics;
using SeitonSense.Core;

internal static class AggressorArrowSelfTests
{
    private static readonly TargetPressureActorIdentity Local = new(100, 10);
    private static readonly TargetPressureActorIdentity Enemy = new(200, 20);

    internal static void OverallVisualScaleIsVisibleBoundedAndFinite()
    {
        Check(AggressorArrowRules.DefaultOverallScale == 1.35f, "new overall default is visibly larger");
        Check(AggressorArrowRules.ResolveVisualScale(1f, 1.35f) == 1.35f, "normal UI uses the chosen overall scale");
        Check(AggressorArrowRules.ResolveVisualScale(2f, 3f) == 6f, "UI and overall scale compose");
        Check(AggressorArrowRules.ResolveVisualScale(100f, 100f) == 6f, "oversized values clamp to finite maximum");
        Check(AggressorArrowRules.ResolveVisualScale(-1f, -1f) == 0.5625f, "negative values clamp to lower bounds");
        Check(AggressorArrowRules.ResolveVisualScale(float.NaN, float.NaN) == 1.35f, "NaN values use safe defaults");
        Check(AggressorArrowRules.ResolveVisualScale(float.PositiveInfinity, float.NegativeInfinity) == 1.35f,
            "infinite scales cannot create invalid draw geometry");
    }

    internal static void BaselinesAndDirectTransitionsAreOneShot()
    {
        var tracker = new AggressorArrowTracker();
        Empty(Observe(tracker, 1_000, TargetPressureSources.HardTarget), "startup aggression is a silent baseline");
        Empty(Observe(tracker, 1_100, TargetPressureSources.CastTarget), "hard-to-cast is the same episode");
        Empty(Observe(tracker, 1_200, TargetPressureSources.None), "clear evidence rearms");
        var first = Observe(tracker, 1_300, TargetPressureSources.CastTarget);
        Pulse(first, Enemy, 1_300, "new cast aggression flashes");
        Pulse(Observe(tracker, 1_400, TargetPressureSources.HardTarget), Enemy, 1_300,
            "cast-to-hard does not replace pulse timestamp");
        Pulse(Observe(tracker, 1_500, TargetPressureSources.HardTarget | TargetPressureSources.CastTarget),
            Enemy, 1_300, "both direct sources still count once");
        Observe(tracker, 1_600, TargetPressureSources.None);
        Pulse(Observe(tracker, 1_700, TargetPressureSources.HardTarget), Enemy, 1_700,
            "a fully cleared union rearms the next hard target");
    }

    internal static void HarmfulEvidenceRequiresFreshTimeAndDeduplicatesHits()
    {
        var tracker = new AggressorArrowTracker();
        Observe(tracker, 1_000, TargetPressureSources.None);
        Pulse(Observe(tracker, 1_100, TargetPressureSources.RecentHarmfulAction, 1_050), Enemy, 1_100,
            "fresh harmful-only onset flashes");
        Pulse(Observe(tracker, 1_200, TargetPressureSources.RecentHarmfulAction, 1_150), Enemy, 1_100,
            "new hits within the same recent-evidence episode do not flash again");
        Pulse(Observe(tracker, 1_300, TargetPressureSources.HardTarget, 1_150), Enemy, 1_300,
            "new direct targeting flashes independently of lingering recent evidence");
        Pulse(Observe(tracker, 1_400, TargetPressureSources.RecentHarmfulAction, 1_150), Enemy, 1_300,
            "losing direct focus while recent evidence remains does not flash");
        Observe(tracker, 1_500, TargetPressureSources.None, 1_150);
        Pulse(Observe(tracker, 1_600, TargetPressureSources.RecentHarmfulAction, 1_550), Enemy, 1_600,
            "fresh timestamp after a full clear rearms");

        Pulse(Observe(tracker, 1_700, TargetPressureSources.HardTarget |
            TargetPressureSources.RecentHarmfulAction, 1_550), Enemy, 1_700,
            "direct targeting acquisition is an independent edge");
        Pulse(Observe(tracker, 1_800, TargetPressureSources.CastTarget |
            TargetPressureSources.RecentHarmfulAction, 1_750), Enemy, 1_700,
            "new harmful hit and hard-to-cast handover do not refresh continuous direct focus");
        Pulse(Observe(tracker, 1_900, TargetPressureSources.RecentHarmfulAction, 1_750), Enemy, 1_700,
            "retained recent evidence remains after direct focus clears");
        Pulse(Observe(tracker, 2_000, TargetPressureSources.HardTarget |
            TargetPressureSources.RecentHarmfulAction, 1_750), Enemy, 2_000,
            "retarget during the old recent-harmful window creates a new direct pulse");
        Pulse(Observe(tracker, 2_100, TargetPressureSources.HardTarget |
            TargetPressureSources.RecentHarmfulAction, 2_050), Enemy, 2_000,
            "subsequent damage during continuous focus never refreshes the direct pulse");

        tracker.Reset();
        Observe(tracker, 2_000, TargetPressureSources.None, 1_000);
        Empty(Observe(tracker, 2_100, TargetPressureSources.RecentHarmfulAction, 1_000),
            "old replay cannot flash");
        Observe(tracker, 2_200, TargetPressureSources.None, 1_000);
        Empty(Observe(tracker, 2_300, TargetPressureSources.RecentHarmfulAction, 1_799),
            "a timestamp over 500 ms old cannot start a pulse");
        Observe(tracker, 2_400, TargetPressureSources.None, 1_799);
        Empty(Observe(tracker, 2_500, TargetPressureSources.RecentHarmfulAction, 2_501),
            "future harmful timestamps cannot flash");
        Empty(Observe(tracker, 2_600, TargetPressureSources.RecentHarmfulAction, -1),
            "unknown harmful timestamps cannot flash");
        Pulse(Observe(tracker, 2_700, TargetPressureSources.RecentHarmfulAction, 2_200), Enemy, 2_700,
            "freshness boundary is inclusive");
    }

    internal static void ContextIdentityAndPublicationGapsFailClosed()
    {
        var tracker = new AggressorArrowTracker();
        Observe(tracker, 1_000, TargetPressureSources.None);
        Pulse(Observe(tracker, 1_100, TargetPressureSources.HardTarget), Enemy, 1_100, "setup pulse");
        Empty(Observe(tracker, 1_601, TargetPressureSources.HardTarget), "stale publication gap resets active pulse");
        Observe(tracker, 1_700, TargetPressureSources.None);
        Pulse(Observe(tracker, 1_800, TargetPressureSources.HardTarget), Enemy, 1_800, "setup second pulse");
        Empty(Observe(tracker, 1_750, TargetPressureSources.HardTarget), "reversed clock resets baseline");
        Empty(tracker.Observe(true, new TargetPressureActorIdentity(101, 11), 1_800,
            [Observation(Enemy, TargetPressureSources.HardTarget)]), "local identity change resets baseline");
        Empty(tracker.Observe(false, Local, 1_900, [Observation(Enemy, TargetPressureSources.HardTarget)]),
            "disabled clears all state");
        Empty(tracker.Observe(true, default, 2_000, [Observation(Enemy, TargetPressureSources.HardTarget)]),
            "invalid local identity fails closed");

        Observe(tracker, 2_100, TargetPressureSources.None);
        var reusedGameId = new TargetPressureActorIdentity(Enemy.GameObjectId, 21);
        Empty(tracker.Observe(true, Local, 2_200,
            [Observation(reusedGameId, TargetPressureSources.HardTarget)]), "half-ID reuse is a new silent baseline");
        var reusedEntityId = new TargetPressureActorIdentity(201, reusedGameId.EntityId);
        Empty(tracker.Observe(true, Local, 2_300,
            [Observation(reusedGameId, TargetPressureSources.None),
             Observation(reusedEntityId, TargetPressureSources.HardTarget)]), "conflicting entity IDs are ambiguous");
        Empty(tracker.Observe(true, Local, 2_400,
            [Observation(Enemy, TargetPressureSources.HardTarget),
             Observation(Enemy, TargetPressureSources.HardTarget)]), "duplicate actor entries fail closed");
        Empty(tracker.Observe(true, Local, 2_450,
            [Observation(Enemy, TargetPressureSources.HardTarget)]),
            "resolving duplicate identity ambiguity is a silent baseline");
        Empty(tracker.Observe(true, Local, 2_500,
            [Observation(new TargetPressureActorIdentity(0xE0000000, 20), TargetPressureSources.HardTarget),
             Observation(Local, TargetPressureSources.HardTarget)]), "sentinels and local actor are never enemies");
    }

    internal static void EligibilityMarkersNewActorsAndRetentionAreBounded()
    {
        var tracker = new AggressorArrowTracker();
        Observe(tracker, 1_000, TargetPressureSources.None);
        Empty(Observe(tracker, 1_100, TargetPressureSources.MachinistLimitBreakEarlyMarker),
            "MCH early marker alone is not aggression");
        Pulse(Observe(tracker, 1_200, TargetPressureSources.HardTarget |
            TargetPressureSources.MachinistLimitBreakEarlyMarker), Enemy, 1_200,
            "actual hard target remains eligible alongside MCH marker");
        Empty(tracker.Observe(true, Local, 1_300,
            [new AggressorArrowObservation(Enemy, TargetPressureSources.HardTarget, -1, false)]),
            "dead or ineligible actor removes pulse");
        Empty(Observe(tracker, 1_400, TargetPressureSources.HardTarget), "returning actor is a new silent baseline");
        Empty(tracker.Observe(true, Local, 1_500, []), "absent actor is removed");
        Empty(Observe(tracker, 1_600, TargetPressureSources.HardTarget), "reappearing actor cannot cause a storm");
        Observe(tracker, 1_700, TargetPressureSources.None);
        Pulse(Observe(tracker, 1_800, TargetPressureSources.HardTarget), Enemy, 1_800, "setup retained pulse");
        var retentionEnds = 1_800 + AggressorArrowRules.MaximumPulseRetentionMilliseconds;
        for (var now = 2_000L; now < retentionEnds; now += 200)
            Pulse(Observe(tracker, now, TargetPressureSources.HardTarget), Enemy, 1_800,
                "continuous publications do not refresh pulse lifetime");
        Empty(Observe(tracker, retentionEnds, TargetPressureSources.HardTarget), "pulse retention ends exactly at configured bound");
        Empty(Observe(tracker, retentionEnds + 100, TargetPressureSources.HardTarget), "expired pulse cannot repeat while aggressive");

        tracker.Reset();
        Empty(tracker.Observe(true, Local, 5_000,
            [Observation(Enemy, TargetPressureSources.HardTarget)]), "whole startup publication is silent");
        var newDirect = new TargetPressureActorIdentity(300, 30);
        Pulse(tracker.Observe(true, Local, 5_100,
            [Observation(Enemy, TargetPressureSources.HardTarget),
             Observation(newDirect, TargetPressureSources.CastTarget)]), newDirect, 5_100,
            "brand-new exact actor in a fresh continuous publication can flash direct acquisition");
        Empty(tracker.Observe(true, Local, 5_200,
            [Observation(Enemy, TargetPressureSources.HardTarget)]), "absent new actor loses active pulse");
        Empty(tracker.Observe(true, Local, 5_300,
            [Observation(Enemy, TargetPressureSources.HardTarget),
             Observation(newDirect, TargetPressureSources.HardTarget)]), "returning exact actor baselines silently");
        var newHarmful = new TargetPressureActorIdentity(400, 40);
        Pulse(tracker.Observe(true, Local, 5_400,
            [Observation(Enemy, TargetPressureSources.HardTarget),
             Observation(newDirect, TargetPressureSources.HardTarget),
             Observation(newHarmful, TargetPressureSources.RecentHarmfulAction, 5_350)]), newHarmful, 5_400,
            "fresh harmful-only new arrival flashes in a complete publication");
        var staleHarmful = new TargetPressureActorIdentity(500, 50);
        Pulse(tracker.Observe(true, Local, 5_500,
            [Observation(Enemy, TargetPressureSources.HardTarget),
             Observation(newDirect, TargetPressureSources.HardTarget),
             Observation(newHarmful, TargetPressureSources.RecentHarmfulAction, 5_350),
             Observation(staleHarmful, TargetPressureSources.RecentHarmfulAction, 4_999)]), newHarmful, 5_400,
            "stale harmful-only arrival does not add a pulse");
        var markerOnly = new TargetPressureActorIdentity(600, 60);
        Pulse(tracker.Observe(true, Local, 5_600,
            [Observation(Enemy, TargetPressureSources.HardTarget),
             Observation(newDirect, TargetPressureSources.HardTarget),
             Observation(newHarmful, TargetPressureSources.RecentHarmfulAction, 5_350),
             Observation(staleHarmful, TargetPressureSources.RecentHarmfulAction, 4_999),
             Observation(markerOnly, TargetPressureSources.MachinistLimitBreakEarlyMarker)]), newHarmful, 5_400,
            "brand-new marker-only arrival is not aggression");
    }

    internal static void AlphaAndProjectionBoundsRejectInvalidGeometry()
    {
        Check(AggressorArrowRules.DefaultDurationSeconds == 2f, "longer default duration is pinned");
        Check(AggressorArrowRules.PulseAlpha(1_000, 1_000, .75f, false) == 1, "pulse begins visible");
        Check(AggressorArrowRules.PulseAlpha(1_000, 1_375, .75f, false) == .5f, "normal pulse fades");
        Check(AggressorArrowRules.PulseAlpha(1_000, 1_750, .75f, false) == 0, "duration boundary is exclusive");
        Check(AggressorArrowRules.PulseAlpha(1_000, 1_375, .75f, true) == .8f, "reduced motion is steady");
        Check(AggressorArrowRules.PulseAlpha(1_000, 999, .75f, false) == 0, "reversed alpha time fails closed");
        Check(AggressorArrowRules.PulseAlpha(1_000, 1_000, float.NaN, false) == 0, "NaN duration fails closed");
        Check(AggressorArrowRules.PulseAlpha(1_000, 1_000, float.PositiveInfinity, false) == 0,
            "infinite duration fails closed");
        Check(AggressorArrowRules.PulseAlpha(1_000, 1_000, 0, false) == 0, "zero duration is invisible");
        var min = Vector2.Zero;
        var max = new Vector2(1_920, 1_080);
        Check(AggressorArrowRules.IsValidProjectedSegment(new(0, 0), new(8, 0), min, max),
            "finite segment at inclusive viewport and minimum length boundaries is valid");
        Check(!AggressorArrowRules.IsValidProjectedSegment(new(0, 0), new(7, 0), min, max),
            "too-short segment fails closed");
        Check(!AggressorArrowRules.IsValidProjectedSegment(new(-1, 0), new(20, 0), min, max),
            "off-screen origin fails closed");
        Check(!AggressorArrowRules.IsValidProjectedSegment(new(20, 0), new(1_921, 0), min, max),
            "off-screen endpoint fails closed");
        Check(!AggressorArrowRules.IsValidProjectedSegment(new(float.NaN, 0), new(20, 0), min, max),
            "NaN projection fails closed");
        Check(!AggressorArrowRules.IsValidProjectedSegment(new(0, 0), new(20, float.PositiveInfinity), min, max),
            "infinite projection fails closed");
        Check(!AggressorArrowRules.IsValidProjectedSegment(new(0, 0), new(20, 0), max, min),
            "reversed viewport fails closed");
    }

    private static IReadOnlyList<AggressorArrowPulse> Observe(
        AggressorArrowTracker tracker, long now, TargetPressureSources sources, long harmfulAt = -1) =>
        tracker.Observe(true, Local, now, [Observation(Enemy, sources, harmfulAt)]);

    private static AggressorArrowObservation Observation(
        TargetPressureActorIdentity actor, TargetPressureSources sources, long harmfulAt = -1) =>
        new(actor, sources, harmfulAt, true);

    private static void Empty(IReadOnlyList<AggressorArrowPulse> pulses, string message) =>
        Check(pulses.Count == 0, message);

    private static void Pulse(
        IReadOnlyList<AggressorArrowPulse> pulses, TargetPressureActorIdentity actor, long at, string message) =>
        Check(pulses.Count == 1 && pulses[0].Actor == actor && pulses[0].StartedAtMilliseconds == at, message);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
