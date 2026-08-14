using System.Collections.ObjectModel;

namespace SeitonSense.Core;

/// <summary>
/// Conservative allowlist of current single-primary-target PvP CC actions.
/// Untargeted, cone, line and target-centered area CC is deliberately absent:
/// one protected actor is not enough evidence that the whole cast is wasted.
/// </summary>
public static class CcImmunityBrakeActionCatalog
{
    private static readonly CcImmunityBrakeActionDefinition[] DefinitionArray =
    [
        new(19, 29_065, "Intervene", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(21, 29_081, "Blota", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(23, 29_395, "Silent Nocturne", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(23, 29_399, "Repelling Shot", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(24, 29_228, "Miracle of Nature", CcImmunityBrakeBlockerFamily.Miracle),
        new(25, 41_510, "Lethargy", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(30, 29_510, "Forked Raiju", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(30, 29_707, "Fleeting Raiju", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(31, 29_407, "Air Anchor", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(33, 29_244, "Gravity II", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(33, 29_248, "Gravity II (Double Cast)", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
        new(34, 29_535, "Mineuchi", CcImmunityBrakeBlockerFamily.StandardPurifyCc),
    ];

    // Stable priority is intentional. If several protections are present, the
    // same blocker is reported regardless of status-list enumeration order.
    private static readonly uint[] StandardPurifyCcBlockerArray =
    [
        3_054, // Guard
        3_673, // alternate/current Guard
        3_248, // Resilience
        1_303, // Inner Release
        1_320, // Meikyo Shisui
        4_096, // Hardened Scales
        3_143, // The Warden's Paean
    ];

    private static readonly uint[] MiracleBlockerArray =
    [
        3_248, // Resilience
        1_320, // Meikyo Shisui
        4_096, // Hardened Scales
        3_143, // The Warden's Paean
        3_052, // Relentless Rush
        3_162, // Honing Dance
    ];

    private static readonly ReadOnlyCollection<CcImmunityBrakeActionDefinition> ReadOnlyDefinitions =
        Array.AsReadOnly(DefinitionArray);

    private static readonly ReadOnlyCollection<uint> ReadOnlyStandardPurifyCcBlockers =
        Array.AsReadOnly(StandardPurifyCcBlockerArray);

    private static readonly ReadOnlyCollection<uint> ReadOnlyMiracleBlockers =
        Array.AsReadOnly(MiracleBlockerArray);

    private static readonly Dictionary<uint, CcImmunityBrakeActionDefinition> DefinitionsByAction =
        DefinitionArray.ToDictionary(static definition => definition.ActionId);

    private static readonly HashSet<uint> StandardPurifyCcBlockers =
        StandardPurifyCcBlockerArray.ToHashSet();

    private static readonly HashSet<uint> MiracleBlockers =
        MiracleBlockerArray.ToHashSet();

    public static IReadOnlyList<CcImmunityBrakeActionDefinition> Definitions => ReadOnlyDefinitions;

    public static bool TryGet(uint actionId, out CcImmunityBrakeActionDefinition definition)
    {
        if (DefinitionsByAction.TryGetValue(actionId, out var exact))
        {
            definition = exact;
            return true;
        }

        definition = null!;
        return false;
    }

    public static bool TryGet(
        uint jobId,
        uint actionId,
        out CcImmunityBrakeActionDefinition definition)
    {
        if (TryGet(actionId, out var exact) && exact.JobId == jobId)
        {
            definition = exact;
            return true;
        }

        definition = null!;
        return false;
    }

    public static IReadOnlyList<CcImmunityBrakeActionDefinition> ForJob(uint jobId) =>
        DefinitionArray.Where(definition => definition.JobId == jobId).ToArray();

    public static IReadOnlyList<uint> GetBlockerStatusIds(CcImmunityBrakeBlockerFamily family) =>
        family switch
        {
            CcImmunityBrakeBlockerFamily.StandardPurifyCc => ReadOnlyStandardPurifyCcBlockers,
            CcImmunityBrakeBlockerFamily.Miracle => ReadOnlyMiracleBlockers,
            _ => Array.Empty<uint>(),
        };

    public static bool IsBlockerStatus(
        CcImmunityBrakeBlockerFamily family,
        uint statusId,
        uint targetJobId) =>
        family switch
        {
            CcImmunityBrakeBlockerFamily.StandardPurifyCc =>
                StandardPurifyCcBlockers.Contains(statusId) &&
                StandardBlockerMatchesTargetJob(statusId, targetJobId),
            CcImmunityBrakeBlockerFamily.Miracle =>
                MiracleBlockers.Contains(statusId) &&
                MiracleBlockerMatchesTargetJob(statusId, targetJobId),
            _ => false,
        };

    private static bool StandardBlockerMatchesTargetJob(uint statusId, uint targetJobId) =>
        statusId switch
        {
            1_303 => targetJobId == 21, // Inner Release: WAR
            1_320 => targetJobId == 34, // Meikyo Shisui: SAM
            4_096 => targetJobId == 41, // Hardened Scales: VPR
            _ => true,
        };

    private static bool MiracleBlockerMatchesTargetJob(uint statusId, uint targetJobId) =>
        statusId switch
        {
            1_320 => targetJobId == 34, // Meikyo Shisui: SAM
            4_096 => targetJobId == 41, // Hardened Scales: VPR
            3_052 => targetJobId == 37, // Relentless Rush: GNB
            3_162 => targetJobId == 38, // Honing Dance: DNC
            _ => true,
        };
}
