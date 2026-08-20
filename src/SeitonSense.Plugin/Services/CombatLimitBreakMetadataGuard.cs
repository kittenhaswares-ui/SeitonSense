using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using GameAction = Lumina.Excel.Sheets.Action;
using GameStatus = Lumina.Excel.Sheets.Status;

namespace SeitonSense.Plugin.Services;

internal sealed record CombatLimitBreakMetadataValidation(
    bool Verified,
    int VerifiedActivationActions,
    int ExpectedActivationActions,
    int VerifiedDamageActions,
    int ExpectedDamageActions,
    int VerifiedStatuses,
    int ExpectedStatuses,
    uint FirstInvalidActionId,
    uint FirstInvalidStatusId)
{
    internal static CombatLimitBreakMetadataValidation None { get; } = new(
        false,
        0,
        CombatLimitBreakCatalog.Definitions.Sum(static definition =>
            definition.Actions.Count(static action => CombatLimitBreakCatalog.IsActivation(action))),
        0,
        CombatLimitBreakCatalog.Definitions.Sum(static definition =>
            definition.Actions.Count(static action =>
                CombatLimitBreakCatalog.IsDirectlyAttributableDamage(action))),
        0,
        CombatLimitBreakCatalog.Definitions.Sum(static definition => definition.Statuses.Length),
        0,
        0);
}

/// <summary>
/// Pins the capture catalog to the installed English game sheets. A mismatch
/// disables only LB capture/session presentation; the ordinary combat-frame
/// snapshot remains independent. No runtime action is attempted here.
/// </summary>
internal static class CombatLimitBreakMetadataGuard
{
    private readonly record struct ExpectedDamageAction(
        string Name,
        uint IconId,
        bool IsPlayerAction,
        uint ClassJobCategoryId,
        uint ActionCategoryId);

    private static readonly IReadOnlyDictionary<uint, string> ActivationNames =
        new Dictionary<uint, string>
        {
            [29_069] = "Phalanx",
            [29_083] = "Primal Scream",
            [29_097] = "Eventide",
            [29_130] = "Relentless Rush",
            [29_230] = "Afflatus Purgation",
            [41_502] = "Seraphism",
            [29_255] = "Celestial River",
            [29_266] = "Mesotes",
            [29_485] = "Meteodrive",
            [29_497] = "Sky High",
            [29_515] = "Seiton Tenchu",
            [29_537] = "Zantetsuken",
            [29_553] = "Tenebrae Lemurum",
            [39_190] = "World-swallower",
            [29_401] = "Final Fantasia",
            [29_415] = "Marksman's Spite",
            [29_432] = "Contradance",
            [29_662] = "Soul Resonance",
            [29_673] = "Summon Bahamut",
            [29_678] = "Summon Phoenix",
            [41_498] = "Southern Cross",
            [39_215] = "Advent of Chocobastion",
        };

    // ActionEffect damage attribution consumes both the primary LB rows and
    // reviewed player-owned follow-ups. Follow-up names/icons/categories are
    // intentionally pinned per row; they must never inherit the activation
    // action's identity merely because they belong to the same catalog entry.
    private static readonly IReadOnlyDictionary<uint, ExpectedDamageAction> DamageActions =
        new Dictionary<uint, ExpectedDamageAction>
        {
            [29_071] = new("Blade of Faith", 9_587, false, 20, 2),
            [29_072] = new("Blade of Truth", 9_588, false, 20, 2),
            [29_073] = new("Blade of Valor", 9_589, false, 20, 2),
            [41_433] = new("Primal Wrath", 9_765, false, 22, 4),
            [29_097] = new("Eventide", 9_597, true, 98, 15),
            [41_437] = new("Disesteem", 9_770, false, 98, 3),
            [29_557] = new("Relentless Rush", 405, false, 0, 4),
            [29_131] = new("Terminal Trigger", 9_604, false, 149, 15),
            [29_469] = new("Terminal Trigger", 9_604, false, 149, 15),
            [29_230] = new("Afflatus Purgation", 9_610, true, 25, 15),
            [41_500] = new("Seraphic Halo", 9_066, false, 29, 2),
            [41_508] = new("Oracle", 9_071, false, 99, 4),
            [29_485] = new("Meteodrive", 9_646, true, 21, 15),
            [29_498] = new("Sky Shatter", 9_653, false, 23, 15),
            [29_499] = new("Sky Shatter", 9_653, false, 23, 15),
            [29_515] = new("Seiton Tenchu", 9_661, true, 92, 15),
            [29_516] = new("Seiton Tenchu", 9_661, false, 92, 15),
            [29_537] = new("Zantetsuken", 9_666, true, 111, 15),
            [39_190] = new("World-swallower", 9_731, true, 196, 15),
            [39_173] = new("Ouroboros", 9_713, false, 196, 3),
            [41_467] = new("Encore of Light", 9_793, false, 24, 4),
            [29_415] = new("Marksman's Spite", 9_636, true, 96, 15),
            [41_480] = new("Flare Star", 9_055, false, 26, 2),
            [41_481] = new("Frost Star", 9_056, false, 26, 2),
            [41_484] = new("Deathflare", 9_307, false, 28, 4),
            [41_485] = new("Brand of Purgatory", 9_416, false, 28, 4),
            [41_498] = new("Southern Cross", 9_692, true, 112, 15),
            [39_216] = new("Star Prism", 9_748, false, 197, 2),
            [39_217] = new("Star Prism", 405, false, 197, 2),
        };

    internal static CombatLimitBreakMetadataValidation Validate(
        IDataManager dataManager,
        IPluginLog log)
    {
        ArgumentNullException.ThrowIfNull(dataManager);
        ArgumentNullException.ThrowIfNull(log);

        var expectedActions = CombatLimitBreakMetadataValidation.None.ExpectedActivationActions;
        var expectedDamageActions = CombatLimitBreakMetadataValidation.None.ExpectedDamageActions;
        var expectedStatuses = CombatLimitBreakMetadataValidation.None.ExpectedStatuses;
        var verifiedActions = 0;
        var verifiedDamageActions = 0;
        var verifiedStatuses = 0;
        var firstInvalidAction = 0u;
        var firstInvalidStatus = 0u;

        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<GameStatus>(ClientLanguage.English);

            foreach (var definition in CombatLimitBreakCatalog.Definitions)
            {
                foreach (var binding in definition.Actions)
                {
                    if (CombatLimitBreakCatalog.IsActivation(binding))
                    {
                        if (!actions.TryGetRow(binding.ActionId, out var activation) ||
                            !ValidateActivation(definition, binding, activation))
                        {
                            if (firstInvalidAction == 0) firstInvalidAction = binding.ActionId;
                        }
                        else
                            verifiedActions++;
                    }

                    if (!CombatLimitBreakCatalog.IsDirectlyAttributableDamage(binding)) continue;
                    if (!actions.TryGetRow(binding.ActionId, out var damageAction) ||
                        !ValidateDamageAction(definition, binding, damageAction))
                    {
                        if (firstInvalidAction == 0) firstInvalidAction = binding.ActionId;
                    }
                    else
                        verifiedDamageActions++;
                }

                foreach (var binding in definition.Statuses)
                {
                    if (!statuses.TryGetRow(binding.StatusId, out var status) ||
                        !ValidateStatus(binding, status))
                    {
                        if (firstInvalidStatus == 0) firstInvalidStatus = binding.StatusId;
                        continue;
                    }

                    verifiedStatuses++;
                }
            }
        }
        catch (Exception exception)
        {
            log.Error(exception, "Seiton Sense LB metadata validation failed closed.");
            return new CombatLimitBreakMetadataValidation(
                false,
                verifiedActions,
                expectedActions,
                verifiedDamageActions,
                expectedDamageActions,
                verifiedStatuses,
                expectedStatuses,
                firstInvalidAction,
                firstInvalidStatus);
        }

        var verified = verifiedActions == expectedActions &&
                       verifiedDamageActions == expectedDamageActions &&
                       verifiedStatuses == expectedStatuses &&
                       firstInvalidAction == 0 &&
                       firstInvalidStatus == 0;
        if (!verified)
        {
            log.Warning(
                "Seiton Sense LB metadata rejected: activations {VerifiedActions}/{ExpectedActions}, " +
                "damage actions {VerifiedDamageActions}/{ExpectedDamageActions}, " +
                "statuses {VerifiedStatuses}/{ExpectedStatuses}, first invalid action={ActionId}, " +
                "first invalid status={StatusId}. LB capture remains disabled.",
                verifiedActions,
                expectedActions,
                verifiedDamageActions,
                expectedDamageActions,
                verifiedStatuses,
                expectedStatuses,
                firstInvalidAction,
                firstInvalidStatus);
        }

        return new CombatLimitBreakMetadataValidation(
            verified,
            verifiedActions,
            expectedActions,
            verifiedDamageActions,
            expectedDamageActions,
            verifiedStatuses,
            expectedStatuses,
            firstInvalidAction,
            firstInvalidStatus);
    }

    private static bool ValidateActivation(
        CombatLimitBreakDefinition definition,
        CombatLimitBreakActionBinding binding,
        GameAction action) =>
        ActivationNames.TryGetValue(binding.ActionId, out var expectedName) &&
        string.Equals(action.Name.ToString(), expectedName, StringComparison.Ordinal) &&
        action.RowId == binding.ActionId &&
        action.Icon == CombatLimitBreakCatalog.ResolveIconId(definition, binding) &&
        action.IsPvP &&
        action.IsPlayerAction &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == definition.JobId &&
        action.ClassJobCategory.IsValid &&
        action.ActionCategory.IsValid &&
        (action.CanTargetSelf ||
         action.CanTargetParty ||
         action.CanTargetHostile ||
         action.CanTargetAlly ||
         action.TargetArea);

    private static bool ValidateStatus(
        CombatLimitBreakStatusBinding binding,
        GameStatus status)
    {
        var expectedName = binding.Phase.EndsWith(
            " (legacy row candidate)",
            StringComparison.Ordinal)
            ? binding.Phase[..^" (legacy row candidate)".Length]
            : binding.Phase;
        return status.RowId == binding.StatusId &&
               string.Equals(status.Name.ToString(), expectedName, StringComparison.Ordinal) &&
               status.Icon != 0 &&
               !status.IsPermanent &&
               status.StatusCategory is 1 or 2 &&
               (binding.Carrier != CombatLimitBreakStatusCarrier.Target ||
                status.StatusCategory == 2);
    }

    private static bool ValidateDamageAction(
        CombatLimitBreakDefinition definition,
        CombatLimitBreakActionBinding binding,
        GameAction action) =>
        DamageActions.TryGetValue(binding.ActionId, out var expected) &&
        action.RowId == binding.ActionId &&
        string.Equals(action.Name.ToString(), expected.Name, StringComparison.Ordinal) &&
        action.Icon == expected.IconId &&
        action.IsPvP &&
        action.IsPlayerAction == expected.IsPlayerAction &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == definition.JobId &&
        action.ClassJobCategory.RowId == expected.ClassJobCategoryId &&
        action.ActionCategory.IsValid &&
        action.ActionCategory.RowId == expected.ActionCategoryId &&
        (action.CanTargetSelf ||
         action.CanTargetParty ||
         action.CanTargetHostile ||
         action.CanTargetAlly ||
         action.TargetArea);
}
