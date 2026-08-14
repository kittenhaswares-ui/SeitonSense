using SeitonSense.Core;

internal static class CcImmunityBrakeSelfTests
{
    private static readonly TargetPressureActorIdentity ExactTarget = new(10_001, 101);

    internal static void ActionCatalogIsExactAndConservative()
    {
        var expected = new (uint Job, uint Action, string Name, CcImmunityBrakeBlockerFamily Family)[]
        {
            (19, 29_065, "Intervene", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (21, 29_081, "Blota", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (23, 29_395, "Silent Nocturne", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (23, 29_399, "Repelling Shot", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (24, 29_228, "Miracle of Nature", CcImmunityBrakeBlockerFamily.Miracle),
            (25, 41_510, "Lethargy", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (30, 29_510, "Forked Raiju", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (30, 29_707, "Fleeting Raiju", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (31, 29_407, "Air Anchor", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (33, 29_244, "Gravity II", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (33, 29_248, "Gravity II (Double Cast)", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            (34, 29_535, "Mineuchi", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        };

        Equal(expected.Length, CcImmunityBrakeActionCatalog.Definitions.Count, "catalog size");
        Equal(
            expected.Length,
            CcImmunityBrakeActionCatalog.Definitions.Select(static definition => definition.ActionId).Distinct().Count(),
            "action IDs are unique");

        for (var index = 0; index < expected.Length; index++)
        {
            var actual = CcImmunityBrakeActionCatalog.Definitions[index];
            Equal(expected[index].Job, actual.JobId, $"job at {index}");
            Equal(expected[index].Action, actual.ActionId, $"action at {index}");
            Equal(expected[index].Name, actual.DisplayName, $"name at {index}");
            Equal(expected[index].Family, actual.BlockerFamily, $"family at {index}");
            True(
                CcImmunityBrakeActionCatalog.TryGet(actual.JobId, actual.ActionId, out var exact) &&
                ReferenceEquals(actual, exact),
                $"exact lookup at {index}");
            False(
                CcImmunityBrakeActionCatalog.TryGet(actual.JobId + 1, actual.ActionId, out _),
                $"wrong-job lookup at {index}");
        }

        Equal(2, CcImmunityBrakeActionCatalog.ForJob(23).Count, "BRD actions");
        Equal(1, CcImmunityBrakeActionCatalog.ForJob(25).Count, "BLM actions");
        Equal(0, CcImmunityBrakeActionCatalog.ForJob(1).Count, "unknown job");

        uint[] deliberatelyExcludedAreaActions =
        [
            29_084, // Primal Rend
            29_095, // Salt and Darkness
            29_259, // Phlegma III
            29_406, // Bioblaster
            29_547, // Grim Swathe
            39_190, // World-swallower
            39_782, // Mog of the Ages
            41_481, // Frost Star
            41_492, // Resolution
        ];
        foreach (var actionId in deliberatelyExcludedAreaActions)
            False(CcImmunityBrakeActionCatalog.TryGet(actionId, out _), $"area action {actionId}");
    }

    internal static void ToggleAndIdentityGatesPassWithoutMutation()
    {
        var baseline = Evaluate(19, 29_065, [3_054]);
        True(baseline.ShouldBlock, "enabled exact attempt blocks");

        var masterOff = Evaluate(19, 29_065, [3_054], master: false);
        Pass(masterOff, CcImmunityBrakeDecisionReason.MasterDisabled, "master off");
        var jobOff = Evaluate(19, 29_065, [3_054], job: false);
        Pass(jobOff, CcImmunityBrakeDecisionReason.JobDisabled, "job off");
        var actionOff = Evaluate(19, 29_065, [3_054], action: false);
        Pass(actionOff, CcImmunityBrakeDecisionReason.ActionDisabled, "action off");

        var unknown = Evaluate(19, 123, [3_054]);
        Pass(unknown, CcImmunityBrakeDecisionReason.ActionNotCataloged, "unknown action");
        var wrongJob = Evaluate(21, 29_065, [3_054]);
        Pass(wrongJob, CcImmunityBrakeDecisionReason.JobMismatch, "wrong job");
    }

    internal static void TargetMustBeExactValidAndMatchIncomingCall()
    {
        var ambiguous = Evaluate(19, 29_065, [3_054], exactTarget: false);
        Pass(ambiguous, CcImmunityBrakeDecisionReason.TargetNotResolvedExactly, "ambiguous target");

        var invalidGameId = Evaluate(
            19,
            29_065,
            [3_054],
            resolvedTarget: ExactTarget with { GameObjectId = 0 });
        Pass(invalidGameId, CcImmunityBrakeDecisionReason.InvalidTargetIdentity, "missing game ID");
        var invalidEntityId = Evaluate(
            19,
            29_065,
            [3_054],
            resolvedTarget: ExactTarget with { EntityId = 0xE0000000 });
        Pass(invalidEntityId, CcImmunityBrakeDecisionReason.InvalidTargetIdentity, "invalid entity ID");

        var invalidIncoming = Evaluate(19, 29_065, [3_054], incomingTargetId: 0xE0000000);
        Pass(invalidIncoming, CcImmunityBrakeDecisionReason.IncomingTargetMismatch, "invalid incoming target");
        var otherIncoming = Evaluate(19, 29_065, [3_054], incomingTargetId: 999);
        Pass(otherIncoming, CcImmunityBrakeDecisionReason.IncomingTargetMismatch, "different incoming target");

        True(
            Evaluate(19, 29_065, [3_054], incomingTargetId: ExactTarget.GameObjectId).ShouldBlock,
            "game object ID identifies exact target");
        True(
            Evaluate(19, 29_065, [3_054], incomingTargetId: ExactTarget.EntityId).ShouldBlock,
            "entity ID identifies exact target");
    }

    internal static void DefaultTargetCarrierResolvesOnlyTheNativeHardTarget()
    {
        Equal(
            ExactTarget.GameObjectId,
            CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(
                CcImmunityBrakeTargetRules.DefaultTargetSentinel,
                CcImmunityBrakeTargetRules.DefaultTargetSentinel,
                ExactTarget.GameObjectId),
            "default sentinel resolves exact native hard target");
        Equal(
            (ulong)ExactTarget.EntityId,
            CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(0, 0, ExactTarget.EntityId),
            "native raw-zero carrier resolves exact hard target");
        Equal(
            ExactTarget.GameObjectId,
            CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(
                ExactTarget.GameObjectId,
                ExactTarget.GameObjectId,
                999),
            "explicit final target remains authoritative");

        Equal(
            0UL,
            CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(123, 0, ExactTarget.GameObjectId),
            "Seiton-injected zero is never reinterpreted as selected target");
        Equal(
            0UL,
            CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(
                0,
                0,
                ExactTarget.GameObjectId,
                targetSuppressedByRedirect: true),
            "explicit suppression provenance keeps raw zero inert");
        Equal(
            0UL,
            CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(
                CcImmunityBrakeTargetRules.DefaultTargetSentinel,
                0,
                ExactTarget.GameObjectId),
            "default carrier rewritten to zero remains suppressed");
        Equal(
            ExactTarget.GameObjectId,
            CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(
                CcImmunityBrakeTargetRules.DefaultTargetSentinel,
                ExactTarget.GameObjectId,
                999),
            "explicit redirect target wins over native selected target");
        Equal(
            0UL,
            CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(
                CcImmunityBrakeTargetRules.DefaultTargetSentinel,
                CcImmunityBrakeTargetRules.DefaultTargetSentinel,
                CcImmunityBrakeTargetRules.DefaultTargetSentinel),
            "missing native hard target remains unresolved");
        Equal(
            0UL,
            CcImmunityBrakeTargetRules.ResolveEffectiveTargetId(
                ulong.MaxValue,
                ulong.MaxValue,
                ExactTarget.GameObjectId),
            "unknown carrier is never resolved");
    }

    internal static void StandardBlockerMatrixIsExact()
    {
        uint[] expectedBlockers = [3_054, 3_673, 3_248, 1_303, 1_320, 4_096, 3_143];
        EqualSequence(
            expectedBlockers,
            CcImmunityBrakeActionCatalog.GetBlockerStatusIds(CcImmunityBrakeBlockerFamily.StandardPurifyCc),
            "standard blocker IDs");

        foreach (var definition in CcImmunityBrakeActionCatalog.Definitions
                     .Where(static definition =>
                         definition.BlockerFamily == CcImmunityBrakeBlockerFamily.StandardPurifyCc))
        {
            foreach (var statusId in new uint[] { 3_054, 3_673, 3_248, 3_143 })
            {
                var decision = Evaluate(definition.JobId, definition.ActionId, [statusId]);
                True(decision.ShouldBlock, $"{definition.ActionId} blocked by {statusId}");
                Equal(statusId, decision.BlockerStatusId, "exact blocker reported");
            }

            foreach (var statusId in new uint[] { 0, 1, 2_708, 3_086, 3_052, 3_162, 4_477, uint.MaxValue })
            {
                var decision = Evaluate(definition.JobId, definition.ActionId, [statusId]);
                Pass(decision, CcImmunityBrakeDecisionReason.NoVerifiedBlocker, $"{statusId} excluded");
            }

            True(Evaluate(definition.JobId, definition.ActionId, [1_303], targetJobId: 21).ShouldBlock, "WAR Inner Release");
            True(Evaluate(definition.JobId, definition.ActionId, [1_320], targetJobId: 34).ShouldBlock, "SAM Meikyo");
            True(Evaluate(definition.JobId, definition.ActionId, [4_096], targetJobId: 41).ShouldBlock, "VPR scales");

            foreach (var (statusId, ownerJobId) in new[] { (1_303u, 21u), (1_320u, 34u), (4_096u, 41u) })
            {
                var wrongJob = ownerJobId == 21 ? 34u : 21u;
                Pass(
                    Evaluate(definition.JobId, definition.ActionId, [statusId], targetJobId: wrongJob),
                    CcImmunityBrakeDecisionReason.NoVerifiedBlocker,
                    $"status {statusId} cannot protect job {wrongJob}");
            }
        }
    }

    internal static void MiracleBlockerMatrixIsExact()
    {
        uint[] expectedBlockers = [3_248, 1_320, 4_096, 3_143, 3_052, 3_162];
        EqualSequence(
            expectedBlockers,
            CcImmunityBrakeActionCatalog.GetBlockerStatusIds(CcImmunityBrakeBlockerFamily.Miracle),
            "Miracle blocker IDs");

        foreach (var statusId in new uint[] { 3_248, 3_143 })
        {
            var decision = Evaluate(24, 29_228, [statusId]);
            True(decision.ShouldBlock, $"Miracle blocked by {statusId}");
            Equal(statusId, decision.BlockerStatusId, "exact Miracle blocker reported");
        }

        True(Evaluate(24, 29_228, [1_320], targetJobId: 34).ShouldBlock, "Miracle SAM Meikyo");
        True(Evaluate(24, 29_228, [4_096], targetJobId: 41).ShouldBlock, "Miracle VPR Hardened Scales");
        True(Evaluate(24, 29_228, [3_052], targetJobId: 37).ShouldBlock, "Miracle GNB rush");
        True(Evaluate(24, 29_228, [3_162], targetJobId: 38).ShouldBlock, "Miracle DNC dance");

        foreach (var (statusId, ownerJobId) in new[]
                 {
                     (1_320u, 34u),
                     (4_096u, 41u),
                     (3_052u, 37u),
                     (3_162u, 38u),
                 })
        {
            Pass(
                Evaluate(24, 29_228, [statusId], targetJobId: ownerJobId + 1),
                CcImmunityBrakeDecisionReason.NoVerifiedBlocker,
                $"Miracle status {statusId} wrong owner job");
        }

        foreach (var statusId in new uint[] { 3_054, 3_673, 1_303, 4_477, 2_708, 3_086 })
        {
            var decision = Evaluate(24, 29_228, [statusId]);
            Pass(decision, CcImmunityBrakeDecisionReason.NoVerifiedBlocker, $"Miracle exclusion {statusId}");
        }
    }

    internal static void ExactMiracleUsesTheSharedFinalDecision()
    {
        var resilience = Evaluate(
            24,
            29_228,
            [3_248],
            incomingTargetId: ExactTarget.GameObjectId,
            resolvedTarget: ExactTarget,
            targetJobId: 41,
            exactTarget: true);
        True(resilience.ShouldBlock, "exact plugin-owned Miracle remains eligible for the shared final brake");
        Equal(CcImmunityBrakeDecisionReason.VerifiedBlocker, resilience.Reason, "shared decision reason");
        Equal(3_248u, resilience.BlockerStatusId, "shared decision reports Resilience");

        var hardenedScales = Evaluate(
            24,
            29_228,
            [4_096],
            incomingTargetId: ExactTarget.EntityId,
            resolvedTarget: ExactTarget,
            targetJobId: 41,
            exactTarget: true);
        True(hardenedScales.ShouldBlock, "exact internal Miracle also respects VPR Hardened Scales");
        Equal(4_096u, hardenedScales.BlockerStatusId, "shared decision reports Hardened Scales");
    }

    internal static void StatusOrderingIsStableAndRulesAreStateless()
    {
        var first = Evaluate(19, 29_065, [3_143, 1_320, 3_054]);
        var second = Evaluate(19, 29_065, [1_320, 3_054, 3_143, 3_054]);
        Equal(3_054u, first.BlockerStatusId, "catalog order wins");
        Equal(first, second, "input order and duplicates do not change decision");

        var repeated = Evaluate(19, 29_065, [3_054]);
        Equal(first, repeated, "same event is evaluated without retained state");

        var noStatuses = Evaluate(19, 29_065, null);
        Pass(noStatuses, CcImmunityBrakeDecisionReason.NoVerifiedBlocker, "null observations");
        var emptyStatuses = Evaluate(19, 29_065, []);
        Pass(emptyStatuses, CcImmunityBrakeDecisionReason.NoVerifiedBlocker, "empty observations");

        False(
            CcImmunityBrakeActionCatalog.IsBlockerStatus((CcImmunityBrakeBlockerFamily)99, 3_054, 21),
            "unknown family has no blockers");
        Equal(
            0,
            CcImmunityBrakeActionCatalog.GetBlockerStatusIds((CcImmunityBrakeBlockerFamily)99).Count,
            "unknown family returns empty list");
    }

    private static CcImmunityBrakeDecision Evaluate(
        uint jobId,
        uint actionId,
        IEnumerable<uint>? statuses,
        bool master = true,
        bool job = true,
        bool action = true,
        ulong? incomingTargetId = null,
        TargetPressureActorIdentity? resolvedTarget = null,
        uint targetJobId = 19,
        bool exactTarget = true) =>
        CcImmunityBrakeRules.Evaluate(
            master,
            job,
            action,
            jobId,
            actionId,
            incomingTargetId ?? ExactTarget.GameObjectId,
            resolvedTarget ?? ExactTarget,
            targetJobId,
            exactTarget,
            statuses);

    private static void Pass(
        CcImmunityBrakeDecision decision,
        CcImmunityBrakeDecisionReason expectedReason,
        string message)
    {
        False(decision.ShouldBlock, message);
        Equal(CcImmunityBrakeDecisionKind.Pass, decision.Kind, message);
        Equal(expectedReason, decision.Reason, message);
        Equal(0u, decision.BlockerStatusId, message);
    }

    private static void EqualSequence<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual,
        string message)
        where T : notnull
    {
        Equal(expected.Count, actual.Count, $"{message} count");
        for (var index = 0; index < expected.Count; index++)
            Equal(expected[index], actual[index], $"{message} at {index}");
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
