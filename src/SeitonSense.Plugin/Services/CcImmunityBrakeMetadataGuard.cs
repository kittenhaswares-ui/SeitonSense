using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using GameAction = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal sealed record CcImmunityBrakeMetadataValidation(
    IReadOnlySet<uint> VerifiedActionIds,
    IReadOnlySet<uint> VerifiedStatusIds)
{
    internal static CcImmunityBrakeMetadataValidation None { get; } =
        new(new HashSet<uint>(), new HashSet<uint>());
}

/// <summary>
/// Pins the numeric CC-brake allowlist to stable action/status semantics.
/// Cosmetic icon, recast-balance, and English-description changes cannot
/// globally disable a reviewed action; identity, job, targeting, range, cast,
/// position, and blocker-category semantics still fail closed.
/// </summary>
internal static class CcImmunityBrakeMetadataGuard
{
    private static readonly ActionExpectation[] ActionExpectations =
    [
        new(29_065, 19, "Intervene", 9_369, 20, 0, 1, 150, true, "Additional Effect: Stun"),
        new(29_081, 21, "Blota", 9_590, 15, 0, 1, 160, false, "Additional Effect: Heavy +75%"),
        new(29_395, 23, "Silent Nocturne", 9_627, 20, 0, 1, 200, false, "Silences target."),
        new(29_399, 23, "Repelling Shot", 9_215, 10, 0, 1, 100, true, "Additional Effect: Bind"),
        new(29_228, 24, "Miracle of Nature", 9_608, 10, 0, 1, 240, false, "Relentless Rush, Honing Dance"),
        new(41_510, 25, "Lethargy", 9_054, 25, 0, 1, 150, false, "Additional Effect: Heavy +75%"),
        new(29_510, 30, "Forked Raiju", 9_656, 20, 0, 1, 25, true, "Additional Effect: Stun"),
        new(29_707, 30, "Fleeting Raiju", 9_693, 20, 0, 1, 25, true, "Additional Effect: Stun"),
        new(29_407, 31, "Air Anchor", 9_392, 25, 0, 1, 100, false, "Additional Effect: Bind"),
        new(29_244, 33, "Gravity II", 9_617, 25, 8, 2, 160, false, "Afflicts first target with Heavy +75%"),
        new(29_248, 33, "Gravity II", 9_617, 25, 8, 2, 120, false, "When cast via Double Cast, Binds first target."),
        new(29_535, 34, "Mineuchi", 9_665, 5, 0, 1, 160, false, "Additional Effect: Stun"),
    ];

    private static readonly StatusExpectation[] StatusExpectations =
    [
        new(3_054, "Guard", 214_890, "All Stun, Heavy, Bind, Silence"),
        new(3_673, "Guard", 214_715, "All Stun, Heavy, Bind, Silence"),
        new(3_248, "Resilience", 214_891, "Nullifying status afflictions that can be removed by Purify"),
        new(1_303, "Inner Release", 212_556, "All Stun, Heavy, Bind, Silence"),
        new(1_320, "Meikyo Shisui", 214_955, "Status afflictions that can be removed by Purify"),
        new(4_096, "Hardened Scales", 214_992, "All Stun, Heavy, Bind, Silence"),
        new(3_143, "The Warden's Paean", 212_611, "Nullifying status afflictions that can be removed by Purify"),
        new(3_052, "Relentless Rush", 214_904, "Swinging blade wildly"),
        new(3_162, "Honing Dance", 214_930, "Dancing blades are dealing damage over time"),
    ];

    internal static CcImmunityBrakeMetadataValidation Validate(
        IDataManager dataManager,
        IPluginLog log)
    {
        var verifiedActions = new HashSet<uint>();
        var verifiedStatuses = new HashSet<uint>();
        try
        {
            ValidateActions(dataManager, log, verifiedActions);
            ValidateStatuses(dataManager, log, verifiedStatuses);
        }
        catch (Exception exception)
        {
            // A sheet-level failure makes the whole filter observational only:
            // no incoming action may be suppressed without pinned metadata.
            log.Warning(
                exception,
                "Seiton Sense could not validate CC-brake metadata; the brake stays fail-open.");
            verifiedActions.Clear();
            verifiedStatuses.Clear();
        }

        log.Information(
            "Seiton Sense verified CC brake metadata: {Actions}/{ActionTotal} actions, " +
            "{Statuses}/{StatusTotal} blockers.",
            verifiedActions.Count,
            CcImmunityBrakeActionCatalog.Definitions.Count,
            verifiedStatuses.Count,
            RequiredStatusIds().Count);
        return new CcImmunityBrakeMetadataValidation(verifiedActions, verifiedStatuses);
    }

    private static void ValidateActions(
        IDataManager dataManager,
        IPluginLog log,
        HashSet<uint> verified)
    {
        var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
        foreach (var definition in CcImmunityBrakeActionCatalog.Definitions)
        {
            var expected = ActionExpectations.FirstOrDefault(item => item.ActionId == definition.ActionId);
            var validExpectation = expected is not null &&
                                   expected.JobId == definition.JobId &&
                                   string.Equals(expected.Name, definition.DisplayName.Replace(" (Double Cast)", string.Empty), StringComparison.Ordinal);
            var valid = validExpectation &&
                        actions.TryGetRow(definition.ActionId, out var action) &&
                        ValidateAction(action, expected!);
            if (valid)
            {
                verified.Add(definition.ActionId);
                continue;
            }

            log.Warning(
                "Seiton Sense disabled unverified CC-brake action {ActionId} ({Name}).",
                definition.ActionId,
                definition.DisplayName);
        }
    }

    private static void ValidateStatuses(
        IDataManager dataManager,
        IPluginLog log,
        HashSet<uint> verified)
    {
        var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
        foreach (var statusId in RequiredStatusIds())
        {
            var expected = StatusExpectations.FirstOrDefault(item => item.StatusId == statusId);
            var valid = expected is not null &&
                        statuses.TryGetRow(statusId, out var status) &&
                        string.Equals(status.Name.ExtractText(), expected.Name, StringComparison.Ordinal) &&
                        status.StatusCategory == 1 &&
                        !status.CanDispel &&
                        !status.IsPermanent;
            if (valid)
            {
                verified.Add(statusId);
                continue;
            }

            log.Warning(
                "Seiton Sense disabled unverified CC-brake blocker status {StatusId}.",
                statusId);
        }
    }

    private static bool ValidateAction(
        GameAction action,
        ActionExpectation expected) =>
        action.RowId == expected.ActionId &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == expected.JobId &&
        action.IsPvP &&
        action.CanTargetHostile &&
        !action.CanTargetSelf &&
        !action.TargetArea &&
        action.Range == expected.Range &&
        action.EffectRange == expected.EffectRange &&
        action.CastType == expected.CastType &&
        action.AffectsPosition == expected.AffectsPosition;

    private static HashSet<uint> RequiredStatusIds() =>
        Enum.GetValues<CcImmunityBrakeBlockerFamily>()
            .SelectMany(CcImmunityBrakeActionCatalog.GetBlockerStatusIds)
            .ToHashSet();

    private sealed record ActionExpectation(
        uint ActionId,
        uint JobId,
        string Name,
        uint IconId,
        byte Range,
        byte EffectRange,
        byte CastType,
        ushort Recast100ms,
        bool AffectsPosition,
        string DescriptionFragment);

    private sealed record StatusExpectation(
        uint StatusId,
        string Name,
        uint IconId,
        string DescriptionFragment);
}
