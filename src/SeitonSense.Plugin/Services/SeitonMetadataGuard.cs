using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using ActionSheet = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal sealed record PvPMetadataValidation(
    bool SeitonVerified,
    bool GuardVerified,
    bool RecuperateVerified,
    bool WildfireVerified,
    bool DeathWarrantVerified,
    bool MarksmanSpiteVerified,
    bool PurifyVerified,
    bool AllyRescueStatusesVerified,
    bool MiracleOfNatureActionVerified,
    bool ZantetsukenVerified,
    bool FuriousBacklashVerified)
{
    public static PvPMetadataValidation None { get; } = new(false, false, false, false, false, false, false, false, false, false, false);
}

internal static class PvPMetadataGuard
{
    internal static PvPMetadataValidation Validate(IDataManager dataManager, IPluginLog log)
    {
        var seitonVerified = ValidateFeature("Seiton", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);

            return ValidateSeitonAction(actions, descriptions, SeitonReadinessProbe.BaseActionId, 100, 14, 1) &&
                   ValidateSeitonAction(actions, descriptions, SeitonReadinessProbe.FollowUpActionId, 10, 10, 3192) &&
                   statuses.TryGetRow(SeitonReadinessProbe.UnsealedStatusId, out var unsealed) &&
                   unsealed.Name.ToString() == "Unsealed Seiton Tenchu" &&
                   unsealed.Icon == 214945 &&
                   unsealed.Description.ToString().Contains(
                       "Able to execute Seiton Tenchu.",
                       StringComparison.Ordinal);
        });

        var guardVerified = ValidateFeature("Guard", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);

            return actions.TryGetRow(EnemyCombatConstants.GuardActionId, out var guard) &&
                   guard.Name.ToString() == "Guard" &&
                   guard.Icon == EnemyCombatConstants.GuardIconId &&
                   guard.IsPvP &&
                   guard.Recast100ms == 300 &&
                   statuses.TryGetRow(EnemyCombatConstants.GuardStatusId, out var guardStatus) &&
                   guardStatus.Name.ToString() == "Guard" &&
                   statuses.TryGetRow(EnemyCombatConstants.GuardStatusAlternateId, out var alternateGuardStatus) &&
                   alternateGuardStatus.Name.ToString() == "Guard";
        });

        var recuperateVerified = ValidateFeature("Recuperate", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);

            return actions.TryGetRow(EnemyCombatConstants.RecuperateActionId, out var recuperate) &&
                   recuperate.Name.ToString() == "Recuperate" &&
                   recuperate.Icon == EnemyCombatConstants.RecuperateIconId &&
                   recuperate.IsPvP &&
                   recuperate.PrimaryCostValue == EnemyCombatConstants.RecuperateMpCost;
        });

        var wildfireVerified = ValidateFeature("Wildfire", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);

            return actions.TryGetRow(EnemyCombatConstants.WildfireActionId, out var action) &&
                   ValidateHostilePvPAction(
                       action,
                       "Wildfire",
                       EnemyCombatConstants.WildfireActionIconId,
                       EnemyCombatConstants.MachinistJobId,
                       EnemyCombatConstants.WildfireRecast100ms) &&
                   descriptions.TryGetRow(EnemyCombatConstants.WildfireActionId, out var transient) &&
                   transient.Description.ToString().Contains(
                       "Action is changed to Detonator for the duration of the effect.",
                       StringComparison.Ordinal) &&
                   statuses.TryGetRow(EnemyCombatConstants.WildfireStatusId, out var status) &&
                   ValidateWarningDebuff(
                       status,
                       "Wildfire",
                       EnemyCombatConstants.WildfireStatusIconId) &&
                   status.Description.ToString().Contains(
                       "Damage is being accumulated",
                       StringComparison.Ordinal);
        });

        var deathWarrantVerified = ValidateFeature("Death Warrant", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);

            return actions.TryGetRow(EnemyCombatConstants.DeathWarrantActionId, out var action) &&
                   ValidateHostilePvPAction(
                       action,
                       "Death Warrant",
                       EnemyCombatConstants.DeathWarrantActionIconId,
                       EnemyCombatConstants.ReaperJobId,
                       EnemyCombatConstants.DeathWarrantRecast100ms) &&
                   descriptions.TryGetRow(EnemyCombatConstants.DeathWarrantActionId, out var transient) &&
                   transient.Description.ToString().Contains(
                       "Afflicts target with Death Warrant",
                       StringComparison.Ordinal) &&
                   statuses.TryGetRow(EnemyCombatConstants.DeathWarrantStatusId, out var status) &&
                   ValidateWarningDebuff(
                       status,
                       "Death Warrant",
                       EnemyCombatConstants.DeathWarrantStatusIconId) &&
                   status.Description.ToString().Contains(
                       "Damage taken from the reaper who applied this effect is compiled.",
                       StringComparison.Ordinal);
        });

        var marksmanSpiteVerified = ValidateFeature("Marksman's Spite", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);

            if (!actions.TryGetRow(EnemyCombatConstants.MarksmanSpiteActionId, out var action) ||
                !descriptions.TryGetRow(EnemyCombatConstants.MarksmanSpiteActionId, out var transient))
            {
                return false;
            }

            var description = transient.Description.ToString();
            return action.Name.ToString() == "Marksman's Spite" &&
                   action.Icon == EnemyCombatConstants.MarksmanSpiteIconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.RowId == EnemyCombatConstants.MachinistJobId &&
                   action.Range == 50 &&
                   action.EffectRange == 0 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == EnemyCombatConstants.MarksmanSpiteRecast100ms &&
                   action.CanTargetHostile &&
                   !action.CanTargetSelf &&
                   !action.CanTargetParty &&
                   !action.CanTargetAlly &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight &&
                   !action.AffectsPosition &&
                   action.CastType == 1 &&
                   action.AnimationEnd.RowId == EnemyCombatConstants.MarksmanSpiteTimelineId &&
                   description.Contains("potency of 40,000", StringComparison.Ordinal) &&
                   description.Contains("limit gauge is full", StringComparison.Ordinal);
        });

        var purifyVerified = ValidateFeature("Purify", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);

            if (!actions.TryGetRow(EnemyCombatConstants.PurifyActionId, out var purify) ||
                purify.Name.ToString() != "Purify" ||
                purify.Icon != EnemyCombatConstants.PurifyIconId ||
                !purify.IsPvP ||
                !purify.IsPlayerAction ||
                !purify.CanTargetSelf ||
                purify.CanTargetHostile ||
                purify.Range != 0 ||
                purify.EffectRange != 0 ||
                purify.Recast100ms != EnemyCombatConstants.PurifyRecast100ms ||
                purify.PrimaryCostType != EnemyCombatConstants.PurifyCostType ||
                purify.PrimaryCostValue != EnemyCombatConstants.PurifyMpCost ||
                !descriptions.TryGetRow(EnemyCombatConstants.PurifyActionId, out var transient))
            {
                return false;
            }

            var description = transient.Description.ToString();
            return description.Contains(
                       "Removes Stun, Heavy, Bind, Silence, Deep Freeze, and Miracle of Nature.",
                       StringComparison.Ordinal) &&
                   description.Contains("Additional Effect: Grants Resilience", StringComparison.Ordinal) &&
                   statuses.TryGetRow(EnemyCombatConstants.PvPStunStatusId, out var stun) &&
                   ValidatePurifiableStatus(
                       stun,
                       "Stun",
                       EnemyCombatConstants.StunStatusIconId,
                       expectMovementLock: true,
                       expectActionLock: true,
                       expectTransfiguration: false) &&
                   statuses.TryGetRow(EnemyCombatConstants.PvPHeavyStatusId, out var heavy) &&
                   ValidatePurifiableStatus(
                       heavy,
                       "Heavy",
                       EnemyCombatConstants.HeavyStatusIconId,
                       expectMovementLock: false,
                       expectActionLock: false,
                       expectTransfiguration: false) &&
                   statuses.TryGetRow(EnemyCombatConstants.PvPBindStatusId, out var bind) &&
                   ValidatePurifiableStatus(
                       bind,
                       "Bind",
                       EnemyCombatConstants.BindStatusIconId,
                       expectMovementLock: false,
                       expectActionLock: false,
                       expectTransfiguration: false) &&
                   statuses.TryGetRow(EnemyCombatConstants.PvPSilenceStatusId, out var silence) &&
                   ValidatePurifiableStatus(
                       silence,
                       "Silence",
                       EnemyCombatConstants.SilenceStatusIconId,
                       expectMovementLock: false,
                       expectActionLock: true,
                       expectTransfiguration: false) &&
                   statuses.TryGetRow(EnemyCombatConstants.DeepFreezeStatusId, out var deepFreeze) &&
                   ValidatePurifiableStatus(
                       deepFreeze,
                       "Deep Freeze",
                       EnemyCombatConstants.DeepFreezeStatusIconId,
                       expectMovementLock: true,
                       expectActionLock: true,
                       expectTransfiguration: false) &&
                   statuses.TryGetRow(EnemyCombatConstants.MiracleOfNatureStatusId, out var miracle) &&
                   ValidatePurifiableStatus(
                       miracle,
                       "Miracle of Nature",
                       EnemyCombatConstants.MiracleOfNatureStatusIconId,
                       expectMovementLock: false,
                       expectActionLock: false,
                       expectTransfiguration: true) &&
                   statuses.TryGetRow(EnemyCombatConstants.ResilienceStatusId, out var resilience) &&
                   resilience.Name.ToString() == "Resilience" &&
                   resilience.Icon == EnemyCombatConstants.ResilienceStatusIconId &&
                   resilience.StatusCategory == 1 &&
                   !resilience.CanDispel &&
                   !resilience.IsPermanent &&
                   resilience.Description.ToString().Contains(
                       "status afflictions that can be removed by Purify",
                       StringComparison.Ordinal);
        });

        var allyRescueStatusesVerified = ValidateFeature("Ally Rescue statuses", log, () =>
        {
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
            return statuses.TryGetRow(EnemyCombatConstants.PvPStunStatusId, out var stun) &&
                   ValidatePurifiableStatus(
                       stun,
                       "Stun",
                       EnemyCombatConstants.StunStatusIconId,
                       expectMovementLock: true,
                       expectActionLock: true,
                       expectTransfiguration: false) &&
                   statuses.TryGetRow(EnemyCombatConstants.PvPSilenceStatusId, out var silence) &&
                   ValidatePurifiableStatus(
                       silence,
                       "Silence",
                       EnemyCombatConstants.SilenceStatusIconId,
                       expectMovementLock: false,
                       expectActionLock: true,
                       expectTransfiguration: false) &&
                   statuses.TryGetRow(EnemyCombatConstants.DeepFreezeStatusId, out var deepFreeze) &&
                   ValidatePurifiableStatus(
                       deepFreeze,
                       "Deep Freeze",
                       EnemyCombatConstants.DeepFreezeStatusIconId,
                       expectMovementLock: true,
                       expectActionLock: true,
                       expectTransfiguration: false) &&
                   statuses.TryGetRow(EnemyCombatConstants.MiracleOfNatureStatusId, out var miracle) &&
                   ValidatePurifiableStatus(
                       miracle,
                       "Miracle of Nature",
                       EnemyCombatConstants.MiracleOfNatureStatusIconId,
                       expectMovementLock: false,
                       expectActionLock: false,
                       expectTransfiguration: true);
        });

        var miracleOfNatureActionVerified = ValidateFeature("Miracle of Nature action", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
            if (!actions.TryGetRow(EnemyCombatConstants.MiracleOfNatureActionId, out var action) ||
                !descriptions.TryGetRow(EnemyCombatConstants.MiracleOfNatureActionId, out var transient) ||
                !statuses.TryGetRow(EnemyCombatConstants.MiracleOfNatureStatusId, out var status))
            {
                return false;
            }

            var description = transient.Description.ToString();
            return string.Equals(action.Name.ToString(), "Miracle of Nature", StringComparison.OrdinalIgnoreCase) &&
                   action.Icon == EnemyCombatConstants.MiracleOfNatureActionIconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == EnemyCombatConstants.WhiteMageJobId &&
                   action.Range == EnemyCombatConstants.MiracleOfNatureRange &&
                   action.EffectRange == 0 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == EnemyCombatConstants.MiracleOfNatureRecast100ms &&
                   action.CanTargetHostile &&
                   !action.CanTargetSelf &&
                   !action.CanTargetParty &&
                   !action.CanTargetAlly &&
                   !action.CanTargetAlliance &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight &&
                   !action.AffectsPosition &&
                   description.Contains("Forcibly transforms target", StringComparison.OrdinalIgnoreCase) &&
                   description.Contains(
                       "preventing them from using actions other than Purify",
                       StringComparison.OrdinalIgnoreCase) &&
                   description.Contains(
                       "nullifies status afflictions that can be removed by Purify",
                       StringComparison.OrdinalIgnoreCase) &&
                   ValidatePurifiableStatus(
                       status,
                       "Miracle of Nature",
                       EnemyCombatConstants.MiracleOfNatureStatusIconId,
                       expectMovementLock: false,
                       expectActionLock: false,
                       expectTransfiguration: true);
        });

        var zantetsukenVerified = ValidateFeature("Zantetsuken", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            return actions.TryGetRow(EnemyCombatConstants.ZantetsukenActionId, out var action) &&
                   string.Equals(action.Name.ToString(), "Zantetsuken", StringComparison.OrdinalIgnoreCase) &&
                   action.Icon == EnemyCombatConstants.ZantetsukenIconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == EnemyCombatConstants.SamuraiJobId &&
                   action.Range == 20 &&
                   action.EffectRange == 5 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == EnemyCombatConstants.ZantetsukenRecast100ms &&
                   action.CanTargetHostile &&
                   !action.CanTargetSelf &&
                   !action.CanTargetParty &&
                   !action.CanTargetAlly &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight &&
                   action.AffectsPosition;
        });

        var furiousBacklashVerified = ValidateFeature("Furious Backlash", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
            return actions.TryGetRow(EnemyCombatConstants.FuriousBacklashActionId, out var action) &&
                   string.Equals(action.Name.ToString(), "Furious Backlash", StringComparison.OrdinalIgnoreCase) &&
                   action.Icon == EnemyCombatConstants.FuriousBacklashIconId &&
                   action.IsPvP &&
                   !action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == EnemyCombatConstants.ViperJobId &&
                   action.Range == 0 &&
                   action.EffectRange == 15 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == EnemyCombatConstants.FuriousBacklashRecast100ms &&
                   action.CanTargetSelf &&
                   !action.CanTargetHostile &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight &&
                   !action.AffectsPosition &&
                   statuses.TryGetRow(EnemyCombatConstants.HardenedScalesStatusId, out var status) &&
                   string.Equals(status.Name.ToString(), "Hardened Scales", StringComparison.OrdinalIgnoreCase) &&
                   status.Icon == 214992 &&
                   status.StatusCategory == 1 &&
                   !status.CanDispel &&
                   !status.IsPermanent;
        });

        var validation = new PvPMetadataValidation(
            seitonVerified,
            guardVerified,
            recuperateVerified,
            wildfireVerified,
            deathWarrantVerified,
            marksmanSpiteVerified,
            purifyVerified,
            allyRescueStatusesVerified,
            miracleOfNatureActionVerified,
            zantetsukenVerified,
            furiousBacklashVerified);

        log.Information(
            "Seiton Sense metadata: Seiton={Seiton}, Guard={Guard}, Recuperate={Recuperate}, " +
            "Wildfire={Wildfire}, DeathWarrant={DeathWarrant}, MarksmanSpite={MarksmanSpite}, " +
            "Purify={Purify}, AllyRescueStatuses={AllyRescueStatuses}, MiracleAction={MiracleAction}, " +
            "Zantetsuken={Zantetsuken}, FuriousBacklash={FuriousBacklash}.",
            validation.SeitonVerified,
            validation.GuardVerified,
            validation.RecuperateVerified,
            validation.WildfireVerified,
            validation.DeathWarrantVerified,
            validation.MarksmanSpiteVerified,
            validation.PurifyVerified,
            validation.AllyRescueStatusesVerified,
            validation.MiracleOfNatureActionVerified,
            validation.ZantetsukenVerified,
            validation.FuriousBacklashVerified);

        return validation;
    }

    private static bool ValidateFeature(string feature, IPluginLog log, Func<bool> validate)
    {
        try
        {
            return validate();
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense {Feature} metadata validation failed closed; other features remain independent.",
                feature);
            return false;
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

    private static bool ValidateHostilePvPAction(
        ActionSheet action,
        string expectedName,
        uint expectedIcon,
        uint expectedJobId,
        ushort expectedRecast100ms) =>
        action.Name.ToString() == expectedName &&
        action.Icon == expectedIcon &&
        action.IsPvP &&
        action.IsPlayerAction &&
        action.ClassJob.RowId == expectedJobId &&
        action.Range == 25 &&
        action.EffectRange == 0 &&
        action.Recast100ms == expectedRecast100ms &&
        action.CanTargetHostile &&
        !action.CanTargetSelf &&
        !action.CanTargetParty &&
        !action.CanTargetAlly &&
        !action.TargetArea &&
        action.RequiresLineOfSight;

    private static bool ValidateWarningDebuff(
        Status status,
        string expectedName,
        uint expectedIcon) =>
        status.Name.ToString() == expectedName &&
        status.Icon == expectedIcon &&
        status.StatusCategory == 2 &&
        !status.CanDispel &&
        !status.IsPermanent &&
        status.CanStatusOff;

    private static bool ValidatePurifiableStatus(
        Status status,
        string expectedName,
        uint expectedIcon,
        bool expectMovementLock,
        bool expectActionLock,
        bool expectTransfiguration) =>
        status.Name.ToString() == expectedName &&
        status.Icon == expectedIcon &&
        status.StatusCategory == 2 &&
        status.CanDispel &&
        !status.IsPermanent &&
        status.CanStatusOff &&
        status.LockMovement == expectMovementLock &&
        status.LockActions == expectActionLock &&
        status.Transfiguration == expectTransfiguration;
}
