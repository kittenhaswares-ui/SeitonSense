using System.Numerics;
using SeitonSense.Core;

internal static class SmartActionProtectionSelfTests
{
    private static readonly TargetPressureActorIdentity LocalPlayer = new(0x100, 0x100);

    public static void ExactProtectionStatusKindsArePinned()
    {
        var exact = new Dictionary<uint, SmartActionProtectionKind>
        {
            [SmartActionProtectionRules.ChitenStatusId] = SmartActionProtectionKind.Chiten,
            [SmartActionProtectionRules.GuardStatusId] = SmartActionProtectionKind.Guard,
            [SmartActionProtectionRules.GuardLargeScaleStatusId] = SmartActionProtectionKind.Guard,
            [NinjaSeitonProtectionStatusCatalog.CoveredLegacyStatusId] = SmartActionProtectionKind.Covered,
            [NinjaSeitonProtectionStatusCatalog.CoveredStatusId] = SmartActionProtectionKind.Covered,
            [NinjaSeitonProtectionStatusCatalog.CoveredPvpStatusId] = SmartActionProtectionKind.Covered,
            [NinjaSeitonProtectionStatusCatalog.CoveredPvpAlternateStatusId] = SmartActionProtectionKind.Covered,
            [NinjaSeitonProtectionStatusCatalog.HallowedGroundStatusId] = SmartActionProtectionKind.Invulnerability,
            [NinjaSeitonProtectionStatusCatalog.UndeadRedemptionStatusId] = SmartActionProtectionKind.Invulnerability,
        };

        foreach (var pair in exact)
        {
            Equal(pair.Value, SmartActionProtectionRules.ClassifyExactStatus(pair.Key),
                $"status {pair.Key} has one exact protection meaning");
            True(SmartActionProtectionRules.IsExactProtectionKind(pair.Value),
                $"{pair.Value} is a real protection kind");
        }

        foreach (var statusId in new uint[] { 0, 1, 1_239, 1_241, 2_708, 3_248, uint.MaxValue })
        {
            Equal(SmartActionProtectionKind.None,
                SmartActionProtectionRules.ClassifyExactStatus(statusId),
                $"unknown status {statusId} cannot acquire protection semantics");
        }

        False(SmartActionProtectionRules.IsExactProtectionKind(SmartActionProtectionKind.None),
            "None is not a protection");
        False(SmartActionProtectionRules.IsExactProtectionKind((SmartActionProtectionKind)255),
            "unknown enum values fail closed");
    }

    public static void DirectAndTargetCircleSafetyAreExact()
    {
        var target = Geometry(1, x: 0f);
        var sameTarget = Protected(target, SmartActionProtectionKind.Chiten);
        var peerAtBoundary = Protected(
            Geometry(2, x: 6f, hitboxRadius: 1f),
            SmartActionProtectionKind.Chiten);
        var peerOutside = peerAtBoundary with
        {
            Geometry = peerAtBoundary.Geometry with { Position = new Vector3(6.001f, 100f, 0f) },
        };

        False(SmartActionProtectionRules.IsDirectTargetSafe(target, [sameTarget]),
            "a direct attack never selects its protected target");
        True(SmartActionProtectionRules.IsDirectTargetSafe(target, [peerAtBoundary]),
            "a protected peer cannot block a genuine direct attack");

        False(SmartActionProtectionRules.IsTargetCenteredCircleSafe(
                target,
                effectRange: 5f,
                [peerAtBoundary]),
            "circle touching a protected hitbox at the exact boundary is unsafe");
        True(SmartActionProtectionRules.IsTargetCenteredCircleSafe(
                target,
                effectRange: 5f,
                [peerOutside]),
            "circle just outside the protected hitbox is safe");
        False(SmartActionProtectionRules.IsTargetCenteredCircleSafe(
                target,
                effectRange: 5f,
                [sameTarget]),
            "the selected protected actor is always inside its centered circle");
    }

    public static void UnsupportedShapesAndInvalidGeometryFailClosed()
    {
        var target = Geometry(1);
        var farProtected = Protected(
            Geometry(2, x: 100f),
            SmartActionProtectionKind.Invulnerability);

        Equal(SmartActionAttackShape.DirectSingleTarget,
            SmartActionProtectionRules.ClassifyAttackShape(effectRange: 0, castType: 1),
            "zero EffectRange with the reviewed direct CastType is direct");
        Equal(SmartActionAttackShape.TargetCenteredCircle,
            SmartActionProtectionRules.ClassifyAttackShape(effectRange: 5, castType: 2),
            "positive EffectRange with the reviewed circle CastType is target-centered");
        foreach (var (effectRange, castType) in new (byte EffectRange, byte CastType)[]
                 {
                     (0, 0),
                     (0, 2),
                     (5, 1),
                     (5, byte.MaxValue),
                 })
        {
            Equal(SmartActionAttackShape.UnsupportedAreaOfEffect,
                SmartActionProtectionRules.ClassifyAttackShape(effectRange, castType),
                $"unreviewed geometry {effectRange}/{castType} fails closed");
        }

        False(SmartActionProtectionRules.IsActionProtectionSafe(
                SmartActionAttackShape.UnsupportedAreaOfEffect,
                target,
                effectRange: 20f,
                [farProtected]),
            "unsupported AoE is unsafe whenever any protected actor exists");
        True(SmartActionProtectionRules.IsActionProtectionSafe(
                SmartActionAttackShape.UnsupportedAreaOfEffect,
                target,
                effectRange: 20f,
                []),
            "unsupported AoE is harmless to protections when none exist");
        False(SmartActionProtectionRules.IsActionProtectionSafe(
                (SmartActionAttackShape)255,
                target,
                effectRange: 0f,
                []),
            "unknown action geometry cannot be treated as direct");
        False(SmartActionProtectionRules.IsActionProtectionSafe(
                SmartActionAttackShape.DirectSingleTarget,
                target,
                effectRange: 1f,
                []),
            "a claimed direct action must have zero EffectRange");
        False(SmartActionProtectionRules.IsActionProtectionSafe(
                SmartActionAttackShape.TargetCenteredCircle,
                target,
                effectRange: 0f,
                []),
            "a claimed circle must have positive EffectRange");
        False(SmartActionProtectionRules.IsActionProtectionSafe(
                SmartActionAttackShape.DirectSingleTarget,
                target,
                effectRange: 0f,
                null),
            "an unknown protected-actor set is never assumed empty");

        var invalidTargets = new[]
        {
            target with { EnemySlot = 0 },
            target with { Actor = default },
            target with { ExactCanonicalIdentity = false },
            target with { Position = new Vector3(float.NaN, 0f, 0f) },
            target with { HitboxRadius = -0.001f },
            target with { HitboxRadius = float.PositiveInfinity },
        };
        foreach (var invalid in invalidTargets)
        {
            False(SmartActionProtectionRules.IsDirectTargetSafe(invalid, []),
                "invalid target identity or geometry fails closed");
        }

        var partialIdentityCollision = farProtected with
        {
            Geometry = farProtected.Geometry with
            {
                Actor = new TargetPressureActorIdentity(target.Actor.GameObjectId, 0x999),
            },
        };
        False(SmartActionProtectionRules.IsDirectTargetSafe(target, [partialIdentityCollision]),
            "a partial identity match cannot be interpreted as a different actor");

        var staleSlotCollision = farProtected with
        {
            Geometry = farProtected.Geometry with
            {
                EnemySlot = target.EnemySlot,
                Actor = new TargetPressureActorIdentity(0x999, 0x999),
            },
        };
        False(SmartActionProtectionRules.IsDirectTargetSafe(target, [staleSlotCollision]),
            "one canonical enemy slot cannot identify two different actors");

        var duplicateIdentity = farProtected with
        {
            Geometry = farProtected.Geometry with { EnemySlot = 3 },
        };
        False(SmartActionProtectionRules.IsDirectTargetSafe(
                target,
                [farProtected, duplicateIdentity]),
            "duplicate protected identities make the actor set ambiguous");
    }

    public static void ProtectedCandidatesCannotWinOrReplaceFrozenIntent()
    {
        var protections = new[]
        {
            SmartActionProtectionKind.Chiten,
            SmartActionProtectionKind.Guard,
            SmartActionProtectionKind.Covered,
            SmartActionProtectionKind.Invulnerability,
        };
        var unsafeCandidates = new List<SmartTargetSelectionCandidate>();
        for (var index = 0; index < protections.Length; index++)
        {
            var slot = index + 1;
            var geometry = Geometry(slot, x: slot);
            var safety = SmartActionProtectionRules.IsDirectTargetSafe(
                geometry,
                [Protected(geometry, protections[index])]);
            False(safety, $"{protections[index]} target is unsafe");
            unsafeCandidates.Add(Candidate(
                slot,
                hp: 1,
                reach: SmartTargetReachTier.Melee,
                callerProvenProtectionSafe: safety));
        }

        var safePeer = Candidate(
            5,
            hp: 99,
            reach: SmartTargetReachTier.RangedOrOther,
            callerProvenProtectionSafe: true);
        var allCandidates = unsafeCandidates.Append(safePeer).ToArray();
        Equal(4, SmartTargetSelectionRules.SelectBestCandidateIndex(allCandidates, LocalPlayer),
            "the safe peer wins even though every protected target ranks better");
        False(SmartTargetSelectionRules.TryCreateIntent(
                29_507,
                unsafeCandidates,
                LocalPlayer,
                out _),
            "only protected candidates produce no Smart Action intent");

        True(SmartTargetSelectionRules.TryCreateIntent(
                29_507,
                [safePeer, unsafeCandidates[0]],
                LocalPlayer,
                out var intent),
            "one safe actor freezes the exact intent");
        False(SmartTargetSelectionRules.CanUseExactIntent(
                intent,
                safePeer with { CallerProvenProtectionSafe = false },
                LocalPlayer,
                29_507),
            "a frozen actor becoming protected cancels the intent");
        False(SmartTargetSelectionRules.CanUseExactIntent(
                intent,
                Candidate(1, hp: 1, callerProvenProtectionSafe: true),
                LocalPlayer,
                29_507),
            "protection drift never reranks to another now-safe actor");
    }

    private static SmartActionActorGeometry Geometry(
        int slot,
        float x = 0f,
        float hitboxRadius = 0.5f) =>
        new(
            slot,
            new TargetPressureActorIdentity((ulong)(0x400 + slot), (uint)(0x300 + slot)),
            ExactCanonicalIdentity: true,
            new Vector3(x, 0f, 0f),
            hitboxRadius);

    private static SmartActionProtectedActor Protected(
        SmartActionActorGeometry geometry,
        SmartActionProtectionKind kind) =>
        new(geometry, kind);

    private static SmartTargetSelectionCandidate Candidate(
        int slot,
        uint hp,
        SmartTargetReachTier reach = SmartTargetReachTier.RangedOrOther,
        bool callerProvenProtectionSafe = true) =>
        new(
            slot,
            new TargetPressureActorIdentity((ulong)(0x400 + slot), (uint)(0x300 + slot)),
            ExactCanonicalIdentity: true,
            IsHostile: true,
            Alive: true,
            Targetable: true,
            hp,
            MaximumHp: 100,
            reach,
            HasValidActionTarget: true,
            HasNativeRangeAndLineOfSight: true,
            FreshTeamPressureCount: 0,
            GuardAvailability.Ready,
            HasTrustedMp: true,
            CurrentMp: 5_000,
            MaximumMp: 10_000,
            CallerProvenProtectionSafe: callerProvenProtectionSafe);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
