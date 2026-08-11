using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using ActionSheet = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal sealed record PvPMetadataValidation(
    bool SeitonVerified,
    bool GuardVerified,
    bool RecuperateVerified)
{
    public static PvPMetadataValidation None { get; } = new(false, false, false);
}

internal static class PvPMetadataGuard
{
    internal static PvPMetadataValidation Validate(IDataManager dataManager, IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);

            var seitonVerified =
                ValidateSeitonAction(actions, descriptions, SeitonReadinessProbe.BaseActionId, 100, 14, 1) &&
                ValidateSeitonAction(actions, descriptions, SeitonReadinessProbe.FollowUpActionId, 10, 10, 3192) &&
                statuses.TryGetRow(SeitonReadinessProbe.UnsealedStatusId, out var unsealed) &&
                unsealed.Name.ToString() == "Unsealed Seiton Tenchu" &&
                unsealed.Icon == 214945 &&
                unsealed.Description.ToString().Contains(
                    "Able to execute Seiton Tenchu.",
                    StringComparison.Ordinal);

            var guardVerified =
                actions.TryGetRow(EnemyCombatConstants.GuardActionId, out var guard) &&
                guard.Name.ToString() == "Guard" &&
                guard.Icon == EnemyCombatConstants.GuardIconId &&
                guard.IsPvP &&
                guard.Recast100ms == 300 &&
                statuses.TryGetRow(EnemyCombatConstants.GuardStatusId, out var guardStatus) &&
                guardStatus.Name.ToString() == "Guard" &&
                statuses.TryGetRow(EnemyCombatConstants.GuardStatusAlternateId, out var alternateGuardStatus) &&
                alternateGuardStatus.Name.ToString() == "Guard";

            var recuperateVerified =
                actions.TryGetRow(EnemyCombatConstants.RecuperateActionId, out var recuperate) &&
                recuperate.Name.ToString() == "Recuperate" &&
                recuperate.Icon == EnemyCombatConstants.RecuperateIconId &&
                recuperate.IsPvP &&
                recuperate.PrimaryCostValue == EnemyCombatConstants.RecuperateMpCost;

            var validation = new PvPMetadataValidation(
                seitonVerified,
                guardVerified,
                recuperateVerified);

            log.Information(
                "Seiton Sense metadata: Seiton={Seiton}, Guard={Guard}, Recuperate={Recuperate}.",
                validation.SeitonVerified,
                validation.GuardVerified,
                validation.RecuperateVerified);

            return validation;
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Seiton Sense metadata validation failed closed.");
            return PvPMetadataValidation.None;
        }
    }

    private static bool ValidateSeitonAction(
        ExcelSheet<ActionSheet> actions,
        ExcelSheet<ActionTransient> descriptions,
        uint actionId,
        ushort expectedRecast100ms,
        byte expectedCostType,
        ushort expectedCostValue)
    {
        if (!actions.TryGetRow(actionId, out var action) ||
            !descriptions.TryGetRow(actionId, out var transient))
        {
            return false;
        }

        var description = transient.Description.ToString();
        return action.Name.ToString() == "Seiton Tenchu" &&
               action.Icon == 9661 &&
               action.Range == SeitonReadinessProbe.MaximumRange &&
               action.EffectRange == 0 &&
               action.IsPvP &&
               action.ClassJob.RowId == 30 &&
               action.CanTargetHostile &&
               !action.CanTargetSelf &&
               !action.CanTargetParty &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.AffectsPosition &&
               action.CastType == 1 &&
               action.Recast100ms == expectedRecast100ms &&
               action.PrimaryCostType == expectedCostType &&
               action.PrimaryCostValue == expectedCostValue &&
               description.Contains(
                   "incapacitating foes whose HP is below 50%.",
                   StringComparison.Ordinal) &&
               description.Contains(
                   "Ignores the effects of Guard when dealing damage.",
                   StringComparison.Ordinal) &&
               description.Contains(
                   "Can only be executed when the limit gauge is full or while under the effect of Unsealed Seiton Tenchu.",
                   StringComparison.Ordinal);
    }
}
