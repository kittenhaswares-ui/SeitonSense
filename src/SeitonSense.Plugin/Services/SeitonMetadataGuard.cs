using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using ActionSheet = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal sealed record PvPMetadataValidation(
    bool SeitonVerified,
    bool ViperSerpentTailVerified,
    bool WolvesDenStrikingDummyVerified,
    bool GuardVerified,
    bool SmartActionProtectionStatusesVerified,
    bool GuardianVerified,
    bool RecuperateVerified,
    bool WildfireVerified,
    bool DeathWarrantVerified,
    bool MarksmanSpiteVerified,
    bool PurifyVerified,
    bool AllyRescueStatusesVerified,
    bool MiracleOfNatureActionVerified,
    bool SilentNocturneVerified,
    bool PanicShukuchiVerified,
    bool ContradanceVerified,
    bool ZantetsukenVerified,
    bool FuriousBacklashVerified,
    bool MonkEarthReplyVerified,
    bool ScholarCriticalStrategyVerified,
    bool EmergencyTeleportMonkVerified,
    bool EmergencyTeleportBlackMageVerified,
    bool EmergencyTeleportSageVerified,
    bool EmergencyTeleportViperVerified,
    bool SmartKardiaVerified,
    bool AutoLowMpFocusProbeVerified,
    bool DarkKnightPlungeVerified,
    bool GunbreakerContinuationVerified,
    bool DarkKnightShadowbringerVerified,
    bool DarkKnightBlackbloodVerified,
    bool RedMageResolutionVerified,
    bool RedMageViceOfThornsVerified,
    bool BlackMageFrostStarVerified,
    bool MonkHeldComboVerified,
    NinjaShukuchiHiddenStatusCatalog NinjaShukuchiHiddenStatuses,
    SmartActionGuardBypassCatalog SmartActionGuardBypassActions)
{
    public static PvPMetadataValidation None { get; } = new(
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false,
        false, false, false, false, false, false, false, false, false, false, false,
        false,
        NinjaShukuchiHiddenStatusCatalog.Empty,
        SmartActionGuardBypassCatalog.Empty);

    internal bool IsEmergencyTeleportVerified(uint jobId) => jobId switch
    {
        EnemyCombatConstants.MonkJobId => EmergencyTeleportMonkVerified,
        EnemyCombatConstants.BlackMageJobId => EmergencyTeleportBlackMageVerified,
        EnemyCombatConstants.SageJobId => EmergencyTeleportSageVerified,
        EnemyCombatConstants.ViperJobId => EmergencyTeleportViperVerified,
        _ => false,
    };
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
                       StringComparison.Ordinal) &&
                   ValidateSeitonProtectionStatus(
                       statuses,
                       NinjaSeitonProtectionStatusCatalog.CoveredLegacyStatusId,
                       "Covered") &&
                   ValidateSeitonProtectionStatus(
                       statuses,
                       NinjaSeitonProtectionStatusCatalog.CoveredStatusId,
                       "Covered") &&
                   ValidateSeitonProtectionStatus(
                       statuses,
                       NinjaSeitonProtectionStatusCatalog.CoveredPvpStatusId,
                       "Covered") &&
                   ValidateSeitonProtectionStatus(
                       statuses,
                       NinjaSeitonProtectionStatusCatalog.CoveredPvpAlternateStatusId,
                       "Covered") &&
                   ValidateSeitonProtectionStatus(
                       statuses,
                       NinjaSeitonProtectionStatusCatalog.HallowedGroundStatusId,
                       "Hallowed Ground") &&
                   ValidateSeitonProtectionStatus(
                       statuses,
                       NinjaSeitonProtectionStatusCatalog.UndeadRedemptionStatusId,
                       "Undead Redemption");
        });

        var viperSerpentTailVerified = ValidateFeature("Viper Serpent's Tail", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var procStatuses = dataManager.GetExcelSheet<ActionProcStatus>(ClientLanguage.English);

            return ValidateViperSerpentTailCarrier(actions, descriptions) &&
                   ValidateViperSerpentTailFollowUp(
                       actions, descriptions, procStatuses,
                       ViperSerpentTailRules.DeathRattleActionId,
                       "Death Rattle", 9_715, 10, 5, 0, 1, 198, 4_085) &&
                   ValidateViperSerpentTailFollowUp(
                       actions, descriptions, procStatuses,
                       ViperSerpentTailRules.TwinfangBiteActionId,
                       "Twinfang Bite", 9_716, 10, 5, 0, 1, 199, 4_086) &&
                   ValidateViperSerpentTailFollowUp(
                       actions, descriptions, procStatuses,
                       ViperSerpentTailRules.TwinbloodBiteActionId,
                       "Twinblood Bite", 9_717, 10, 5, 0, 1, 200, 4_087) &&
                   ValidateViperSerpentTailFollowUp(
                       actions, descriptions, procStatuses,
                       ViperSerpentTailRules.UncoiledTwinfangActionId,
                       "Uncoiled Twinfang", 9_722, 7, 20, 5, 2, 201, 4_088) &&
                   ValidateViperSerpentTailFollowUp(
                       actions, descriptions, procStatuses,
                       ViperSerpentTailRules.UncoiledTwinbloodActionId,
                       "Uncoiled Twinblood", 9_723, 7, 20, 5, 2, 202, 4_089) &&
                   ValidateViperSerpentTailFollowUp(
                       actions, descriptions, procStatuses,
                       ViperSerpentTailRules.FirstLegacyActionId,
                       "First Legacy", 9_718, 10, 5, 5, 2, 203, 4_090) &&
                   ValidateViperSerpentTailFollowUp(
                       actions, descriptions, procStatuses,
                       ViperSerpentTailRules.SecondLegacyActionId,
                       "Second Legacy", 9_719, 10, 5, 5, 2, 204, 4_091) &&
                   ValidateViperSerpentTailFollowUp(
                       actions, descriptions, procStatuses,
                       ViperSerpentTailRules.ThirdLegacyActionId,
                       "Third Legacy", 9_720, 10, 5, 5, 2, 205, 4_092) &&
                   ValidateViperSerpentTailFollowUp(
                       actions, descriptions, procStatuses,
                       ViperSerpentTailRules.FourthLegacyActionId,
                       "Fourth Legacy", 9_721, 10, 5, 5, 2, 206, 4_093);
        });

        var wolvesDenStrikingDummyVerified = ValidateFeature(
            "Wolves' Den striking dummy",
            log,
            () =>
            {
                var names = dataManager.GetExcelSheet<BNpcName>(ClientLanguage.English);
                return names.TryGetRow(
                           ViperSerpentTailRules.WolvesDenStrikingDummyNameId,
                           out var strikingDummy) &&
                       strikingDummy.Singular.ToString() == "striking dummy" &&
                       strikingDummy.Plural.ToString() == "striking dummies";
            });

        var autoLowMpFocusProbeVerified = ValidateFeature("Auto Low-MP Focus probe", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            return actions.TryGetRow(AutoLowMpFocusTargetRules.ProbeActionId, out var action) &&
                   action.Name.ToString() == "Seiton Tenchu" &&
                   action.Icon == EnemyCombatConstants.SeitonIconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == 30 &&
                   action.Range == AutoLowMpFocusTargetRules.ProbeRange &&
                   action.EffectRange == 0 &&
                   action.CanTargetHostile &&
                   !action.CanTargetSelf &&
                   !action.CanTargetParty &&
                   !action.CanTargetAlly &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight;
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

        // Smart Action depends only on these exact status meanings. Keep that
        // proof independent from NIN/Guard action costs, recasts, and other
        // balance metadata so an unrelated patch cannot disable safe targeting.
        var smartActionProtectionStatusesVerified = ValidateFeature(
            "Smart Action protection statuses",
            log,
            () =>
            {
                var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
                return ValidateSeitonProtectionStatus(
                           statuses,
                           NinjaSeitonProtectionStatusCatalog.CoveredLegacyStatusId,
                           "Covered") &&
                       ValidateSeitonProtectionStatus(
                           statuses,
                           NinjaSeitonProtectionStatusCatalog.CoveredStatusId,
                           "Covered") &&
                       ValidateSeitonProtectionStatus(
                           statuses,
                           NinjaSeitonProtectionStatusCatalog.CoveredPvpStatusId,
                           "Covered") &&
                       ValidateSeitonProtectionStatus(
                           statuses,
                           NinjaSeitonProtectionStatusCatalog.CoveredPvpAlternateStatusId,
                           "Covered") &&
                       ValidateSeitonProtectionStatus(
                           statuses,
                           NinjaSeitonProtectionStatusCatalog.HallowedGroundStatusId,
                           "Hallowed Ground") &&
                       ValidateSeitonProtectionStatus(
                           statuses,
                           NinjaSeitonProtectionStatusCatalog.UndeadRedemptionStatusId,
                           "Undead Redemption") &&
                       ValidateNamedStatus(
                           statuses,
                           EnemyCombatConstants.GuardStatusId,
                           "Guard") &&
                       ValidateNamedStatus(
                           statuses,
                           EnemyCombatConstants.GuardStatusAlternateId,
                           "Guard");
            });

        var smartActionGuardBypassActions = SmartActionGuardBypassCatalog.Empty;
        _ = ValidateFeature("Smart Action Guard-bypass actions", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var actionIds = new List<uint>();
            foreach (var action in actions)
            {
                if (action.RowId == 0 ||
                    !action.IsPvP ||
                    !action.CanTargetHostile ||
                    action.TargetArea ||
                    action.Range <= 0 ||
                    !descriptions.TryGetRow(action.RowId, out var transient) ||
                    transient.RowId != action.RowId ||
                    !SmartActionGuardBypassRules.HasExactEnglishDescription(
                        transient.Description.ToString()))
                {
                    continue;
                }

                actionIds.Add(action.RowId);
            }

            var resolved = SmartActionGuardBypassCatalog.Create(actionIds);
            if (!resolved.IsVerified) return false;

            smartActionGuardBypassActions = resolved;
            return true;
        });

        var guardianVerified = ValidateFeature("Guardian", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            if (!actions.TryGetRow(EnemyCombatConstants.GuardianActionId, out var guardian) ||
                !descriptions.TryGetRow(EnemyCombatConstants.GuardianActionId, out var transient))
            {
                return false;
            }

            var description = transient.Description.ToString();
            return guardian.Name.ToString() == "Guardian" &&
                   guardian.Icon == EnemyCombatConstants.GuardianIconId &&
                   guardian.IsPvP &&
                   guardian.IsPlayerAction &&
                   guardian.ClassJob.IsValid &&
                   guardian.ClassJob.RowId == EnemyCombatConstants.PaladinJobId &&
                   guardian.Range == EnemyCombatConstants.GuardianSheetRange &&
                   guardian.EffectRange == 0 &&
                   guardian.Cast100ms == 0 &&
                   guardian.Recast100ms == EnemyCombatConstants.GuardianRecast100ms &&
                   !guardian.CanTargetSelf &&
                   guardian.CanTargetParty &&
                   !guardian.CanTargetAlly &&
                   !guardian.CanTargetAlliance &&
                   !guardian.CanTargetHostile &&
                   !guardian.TargetArea &&
                   guardian.RequiresLineOfSight &&
                   guardian.AffectsPosition &&
                   description.Contains(
                       "Take all damage intended for the targeted party member",
                       StringComparison.Ordinal) &&
                   description.Contains("Duration: 8s", StringComparison.Ordinal) &&
                   description.Contains("closer than 10 yalms", StringComparison.Ordinal) &&
                   description.Contains("Cannot be executed while bound", StringComparison.Ordinal);
        });

        var scholarCriticalStrategyVerified = ValidateFeature("Scholar Critical Strategy", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            if (!actions.TryGetRow(EnemyCombatConstants.ScholarCriticalStrategyActionId, out var action) ||
                !descriptions.TryGetRow(EnemyCombatConstants.ScholarCriticalStrategyActionId, out var transient))
            {
                return false;
            }

            var description = transient.Description.ToString();
            return action.Name.ToString() == "Chain Stratagem" &&
                   action.Icon == EnemyCombatConstants.ScholarCriticalStrategyIconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == EnemyCombatConstants.ScholarJobId &&
                   action.ClassJobCategory.IsValid &&
                   action.ClassJobCategory.RowId == 29 &&
                   action.ActionCategory.IsValid &&
                   action.ActionCategory.RowId == 4 &&
                   action.Range == EnemyCombatConstants.ScholarCriticalStrategySheetRange &&
                   action.EffectRange == 0 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == EnemyCombatConstants.ScholarCriticalStrategyRecast100ms &&
                   action.PrimaryCostType == 0 &&
                   action.PrimaryCostValue == 0 &&
                   action.CooldownGroup == 3 &&
                   action.MaxCharges == 0 &&
                   !action.CanTargetSelf &&
                   !action.CanTargetParty &&
                   !action.CanTargetAlliance &&
                   action.CanTargetHostile &&
                   !action.CanTargetAlly &&
                   !action.CanTargetOwnPet &&
                   !action.CanTargetPartyPet &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight &&
                   action.NeedToFaceTarget &&
                   !action.AffectsPosition &&
                   action.CastType == 1 &&
                   description.Contains("Increases target's damage taken by 10%", StringComparison.Ordinal) &&
                   description.Contains(
                       "Halves the defensive bonus of Guard instead when targeting enemies under its effect.",
                       StringComparison.Ordinal);
        });

        var emergencyTeleportMonkVerified = ValidateFeature(
            "Emergency Teleport: Monk",
            log,
            () => ValidateEmergencyTeleportAction(
                dataManager,
                EnemyCombatConstants.EmergencyTeleportMonkActionId,
                "Thunderclap",
                EnemyCombatConstants.EmergencyTeleportMonkActionIconId,
                EnemyCombatConstants.MonkJobId,
                21,
                20,
                80,
                6,
                71,
                2,
                needToFaceTarget: false));
        var emergencyTeleportBlackMageVerified = ValidateFeature(
            "Emergency Teleport: Black Mage",
            log,
            () => ValidateEmergencyTeleportAction(
                dataManager,
                EnemyCombatConstants.EmergencyTeleportBlackMageActionId,
                "Aetherial Manipulation",
                EnemyCombatConstants.EmergencyTeleportBlackMageActionIconId,
                EnemyCombatConstants.BlackMageJobId,
                26,
                25,
                80,
                4,
                0,
                0,
                needToFaceTarget: true));
        var emergencyTeleportSageVerified = ValidateFeature(
            "Emergency Teleport: Sage",
            log,
            () => ValidateEmergencyTeleportAction(
                dataManager,
                EnemyCombatConstants.EmergencyTeleportSageActionId,
                "Icarus",
                EnemyCombatConstants.EmergencyTeleportSageActionIconId,
                EnemyCombatConstants.SageJobId,
                181,
                25,
                100,
                3,
                71,
                2,
                needToFaceTarget: false));
        var emergencyTeleportViperVerified = ValidateFeature(
            "Emergency Teleport: Viper",
            log,
            () => ValidateEmergencyTeleportAction(
                dataManager,
                EnemyCombatConstants.EmergencyTeleportViperActionId,
                "Slither",
                EnemyCombatConstants.EmergencyTeleportViperActionIconId,
                EnemyCombatConstants.ViperJobId,
                196,
                20,
                120,
                4,
                71,
                2,
                needToFaceTarget: true));

        var smartKardiaVerified = ValidateFeature("Smart Kardia", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
            if (!actions.TryGetRow(SmartKardiaRules.ActionId, out var action) ||
                !descriptions.TryGetRow(SmartKardiaRules.ActionId, out var transient) ||
                !actions.TryGetRow(SmartKardiaRules.EukrasiaActionId, out var eukrasiaAction) ||
                !descriptions.TryGetRow(SmartKardiaRules.EukrasiaActionId, out var eukrasiaTransient) ||
                !statuses.TryGetRow(SmartKardiaRules.KardiaStatusId, out var kardia) ||
                !statuses.TryGetRow(SmartKardiaRules.KardionStatusId, out var kardion) ||
                !statuses.TryGetRow(SmartKardiaRules.EukrasiaStatusId, out var eukrasiaStatus))
            {
                return false;
            }

            var description = transient.Description.ToString();
            var eukrasiaDescription = eukrasiaTransient.Description.ToString();
            return string.Equals(action.Name.ToString(), "Kardia", StringComparison.Ordinal) &&
                   action.Icon == SmartKardiaProbe.KardiaIconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == SmartKardiaRules.SageJobId &&
                   action.ClassJobCategory.IsValid &&
                   action.ClassJobCategory.RowId == 181 &&
                   action.ActionCategory.IsValid &&
                   action.ActionCategory.RowId == 4 &&
                   action.Range == SmartKardiaProbe.ExpectedRange &&
                   action.EffectRange == 0 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == SmartKardiaProbe.ExpectedRecast100ms &&
                   action.PrimaryCostType == 0 &&
                   action.PrimaryCostValue == 0 &&
                   action.SecondaryCostType == 0 &&
                   action.SecondaryCostValue.RowId == 0 &&
                   action.CooldownGroup == 6 &&
                   action.MaxCharges == 0 &&
                   action.CanTargetSelf &&
                   action.CanTargetParty &&
                   !action.CanTargetAlliance &&
                   !action.CanTargetHostile &&
                   !action.CanTargetAlly &&
                   !action.CanTargetOwnPet &&
                   !action.CanTargetPartyPet &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight &&
                   !action.NeedToFaceTarget &&
                   !action.AffectsPosition &&
                   action.CastType == 1 &&
                   action.StatusGainSelf.RowId == 0 &&
                   action.ActionProcStatus.RowId == 0 &&
                   description.Contains(
                       "Grants self the effect of Kardia and a selected party member or self the effect of Kardion",
                       StringComparison.Ordinal) &&
                   description.Contains(
                       "additional effects of Dosis III and Eukrasian Dosis III",
                       StringComparison.Ordinal) &&
                   string.Equals(kardia.Name.ToString(), "Kardia", StringComparison.Ordinal) &&
                   kardia.Icon == SmartKardiaProbe.KardiaStatusIconId &&
                   kardia.StatusCategory == 1 &&
                   !kardia.CanDispel &&
                   kardia.IsPermanent &&
                   kardia.Description.ToString().Contains(
                       "Kardion granted by you",
                       StringComparison.Ordinal) &&
                   string.Equals(kardion.Name.ToString(), "Kardion", StringComparison.Ordinal) &&
                   kardion.Icon == SmartKardiaProbe.KardionStatusIconId &&
                   kardion.StatusCategory == 1 &&
                   !kardion.CanDispel &&
                   kardion.IsPermanent &&
                   kardion.Description.ToString().Contains(
                       "the sage who applied this status",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       eukrasiaAction.Name.ToString(),
                       "Eukrasia",
                       StringComparison.Ordinal) &&
                   eukrasiaAction.Icon == SmartKardiaProbe.EukrasiaIconId &&
                   eukrasiaAction.IsPvP &&
                   eukrasiaAction.IsPlayerAction &&
                   eukrasiaAction.ClassJob.IsValid &&
                   eukrasiaAction.ClassJob.RowId == SmartKardiaRules.SageJobId &&
                   eukrasiaAction.ClassJobCategory.IsValid &&
                   eukrasiaAction.ClassJobCategory.RowId == 181 &&
                   eukrasiaAction.ActionCategory.IsValid &&
                   eukrasiaAction.ActionCategory.RowId == 2 &&
                   eukrasiaAction.Range == 0 &&
                   eukrasiaAction.EffectRange == 0 &&
                   eukrasiaAction.Cast100ms == 0 &&
                   eukrasiaAction.Recast100ms ==
                   SmartKardiaProbe.ExpectedEukrasiaRecast100ms &&
                   eukrasiaAction.PrimaryCostType == 0 &&
                   eukrasiaAction.PrimaryCostValue == 0 &&
                   eukrasiaAction.SecondaryCostType == 0 &&
                   eukrasiaAction.SecondaryCostValue.RowId == 0 &&
                   eukrasiaAction.CooldownGroup == 9 &&
                   eukrasiaAction.AdditionalCooldownGroup == 58 &&
                   eukrasiaAction.MaxCharges ==
                   SmartKardiaRules.EukrasiaMaximumCharges &&
                   eukrasiaAction.CanTargetSelf &&
                   !eukrasiaAction.CanTargetParty &&
                   !eukrasiaAction.CanTargetAlliance &&
                   !eukrasiaAction.CanTargetHostile &&
                   !eukrasiaAction.CanTargetAlly &&
                   !eukrasiaAction.CanTargetOwnPet &&
                   !eukrasiaAction.CanTargetPartyPet &&
                   !eukrasiaAction.TargetArea &&
                   eukrasiaAction.RequiresLineOfSight &&
                   eukrasiaAction.NeedToFaceTarget &&
                   !eukrasiaAction.AffectsPosition &&
                   eukrasiaAction.CastType == 1 &&
                   eukrasiaAction.StatusGainSelf.RowId == 0 &&
                   eukrasiaAction.ActionProcStatus.RowId == 0 &&
                   eukrasiaDescription.Contains(
                       "Upgrades Dosis III to Eukrasian Dosis III",
                       StringComparison.Ordinal) &&
                   eukrasiaDescription.Contains(
                       "Duration: 10s",
                       StringComparison.Ordinal) &&
                   eukrasiaDescription.Contains(
                       "Maximum Charges: 2",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       eukrasiaStatus.Name.ToString(),
                       "Eukrasia",
                       StringComparison.Ordinal) &&
                   eukrasiaStatus.Icon == SmartKardiaProbe.EukrasiaStatusIconId &&
                   eukrasiaStatus.StatusCategory == 1 &&
                   !eukrasiaStatus.CanDispel &&
                   !eukrasiaStatus.IsPermanent &&
                   eukrasiaStatus.CanStatusOff &&
                   eukrasiaStatus.Description.ToString().Contains(
                       "Certain actions are being augmented",
                       StringComparison.Ordinal);
        });

        var darkKnightPlungeVerified = ValidateFeature("Dark Knight Plunge", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            if (!actions.TryGetRow(DarkKnightPlungeRules.ActionId, out var action) ||
                !descriptions.TryGetRow(DarkKnightPlungeRules.ActionId, out var transient))
            {
                return false;
            }

            var description = transient.Description.ToString();
            return guardVerified &&
                   string.Equals(action.Name.ToString(), "Plunge", StringComparison.Ordinal) &&
                   action.Icon == DarkKnightPlungeRules.IconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == DarkKnightPlungeRules.DarkKnightJobId &&
                   action.ClassJobCategory.IsValid &&
                   action.ClassJobCategory.RowId ==
                   DarkKnightPlungeRules.DarkKnightClassJobCategoryId &&
                   action.ActionCategory.IsValid &&
                   action.ActionCategory.RowId == 4 &&
                   action.Range == 20 &&
                   action.EffectRange == 0 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == 120 &&
                   action.PrimaryCostType == 0 &&
                   action.PrimaryCostValue == 0 &&
                   action.SecondaryCostType == 0 &&
                   action.SecondaryCostValue.RowId == 0 &&
                   action.CooldownGroup == 2 &&
                   action.AdditionalCooldownGroup == 0 &&
                   action.MaxCharges == 0 &&
                   !action.CanTargetSelf &&
                   !action.CanTargetParty &&
                   !action.CanTargetAlliance &&
                   action.CanTargetHostile &&
                   !action.CanTargetAlly &&
                   !action.CanTargetOwnPet &&
                   !action.CanTargetPartyPet &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight &&
                   action.NeedToFaceTarget &&
                   action.PreservesCombo &&
                   action.AffectsPosition &&
                   action.CastType == 1 &&
                   action.StatusGainSelf.RowId == 0 &&
                   action.ActionProcStatus.RowId == 0 &&
                   description.Contains(
                       "Rushes target and delivers an attack with a potency of 2,000.",
                       StringComparison.Ordinal) &&
                   description.Contains("Afflicts target with Sole Survivor", StringComparison.Ordinal) &&
                   description.Contains("Duration: 12s", StringComparison.Ordinal) &&
                   description.Contains(
                       "the recast time of Plunge will be reset.",
                       StringComparison.Ordinal) &&
                   description.Contains("Cannot be executed while bound.", StringComparison.Ordinal);
        });

        var recuperateVerified = ValidateFeature("Recuperate", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);

            if (!actions.TryGetRow(EnemyCombatConstants.RecuperateActionId, out var recuperate) ||
                !descriptions.TryGetRow(EnemyCombatConstants.RecuperateActionId, out var transient))
            {
                return false;
            }

            var description = transient.Description.ToString();
            return string.Equals(recuperate.Name.ToString(), "Recuperate", StringComparison.Ordinal) &&
                   recuperate.Icon == EnemyCombatConstants.RecuperateIconId &&
                   recuperate.IsPvP &&
                   recuperate.IsPlayerAction &&
                   recuperate.ClassJob.RowId == 0 &&
                   recuperate.ClassJobCategory.IsValid &&
                   recuperate.ClassJobCategory.RowId == 85 &&
                   recuperate.ActionCategory.IsValid &&
                   recuperate.ActionCategory.RowId == 4 &&
                   recuperate.Range == 0 &&
                   recuperate.EffectRange == 0 &&
                   recuperate.Cast100ms == 0 &&
                   recuperate.Recast100ms == 10 &&
                   recuperate.PrimaryCostType == 51 &&
                   recuperate.PrimaryCostValue == EnemyCombatConstants.RecuperateMpCost &&
                   recuperate.SecondaryCostType == 0 &&
                   recuperate.SecondaryCostValue.RowId == 0 &&
                   recuperate.CooldownGroup == 29 &&
                   recuperate.AdditionalCooldownGroup == 0 &&
                   recuperate.MaxCharges == 0 &&
                   recuperate.CastType == 1 &&
                   recuperate.CanTargetSelf &&
                   !recuperate.CanTargetParty &&
                   !recuperate.CanTargetAlliance &&
                   !recuperate.CanTargetHostile &&
                   !recuperate.CanTargetAlly &&
                   !recuperate.CanTargetOwnPet &&
                   !recuperate.CanTargetPartyPet &&
                   !recuperate.TargetArea &&
                   recuperate.RequiresLineOfSight &&
                   recuperate.NeedToFaceTarget &&
                   recuperate.PreservesCombo &&
                   !recuperate.AffectsPosition &&
                   recuperate.StatusGainSelf.RowId == 0 &&
                   recuperate.ActionProcStatus.RowId == 0 &&
                   description.Contains("Restores own HP.", StringComparison.Ordinal) &&
                   description.Contains("Cure Potency: 16,000", StringComparison.Ordinal);
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

        var silentNocturneVerified = ValidateFeature("Silent Nocturne", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            return actions.TryGetRow(EnemyCombatConstants.SilentNocturneActionId, out var action) &&
                   descriptions.TryGetRow(
                       EnemyCombatConstants.SilentNocturneActionId,
                       out var transient) &&
                   string.Equals(
                       action.Name.ToString(),
                       "Silent Nocturne",
                       StringComparison.Ordinal) &&
                   action.Icon == EnemyCombatConstants.SilentNocturneActionIconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == EnemyCombatConstants.BardJobId &&
                   action.Range == EnemyCombatConstants.SilentNocturneRange &&
                   action.EffectRange == 0 &&
                   action.CastType == 1 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == EnemyCombatConstants.SilentNocturneRecast100ms &&
                   action.CanTargetHostile &&
                   !action.CanTargetSelf &&
                   !action.CanTargetParty &&
                   !action.CanTargetAlly &&
                   !action.CanTargetAlliance &&
                   !action.TargetArea &&
                   action.RequiresLineOfSight &&
                   !action.AffectsPosition &&
                   transient.Description.ToString().Contains(
                       "Silences target.",
                       StringComparison.Ordinal);
        });

        var panicShukuchiVerified = ValidateFeature("Panic Shukuchi", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            if (!actions.TryGetRow(EnemyCombatConstants.PanicShukuchiActionId, out var action) ||
                !descriptions.TryGetRow(EnemyCombatConstants.PanicShukuchiActionId, out var transient))
            {
                return false;
            }

            var description = transient.Description.ToString();
            return string.Equals(action.Name.ToString(), "Shukuchi", StringComparison.Ordinal) &&
                   action.Icon == EnemyCombatConstants.PanicShukuchiActionIconId &&
                   action.IsPvP &&
                   action.IsPlayerAction &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == EnemyCombatConstants.NinjaJobId &&
                   action.Range == EnemyCombatConstants.PanicShukuchiSheetRange &&
                   action.EffectRange == 1 &&
                   action.CastType == 7 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == EnemyCombatConstants.PanicShukuchiRecast100ms &&
                   !action.CanTargetSelf &&
                   !action.CanTargetHostile &&
                   !action.CanTargetParty &&
                   !action.CanTargetAlly &&
                   !action.CanTargetAlliance &&
                   action.TargetArea &&
                   action.RequiresLineOfSight &&
                   action.NeedToFaceTarget &&
                   action.AffectsPosition &&
                   description.Contains(
                       "Move quickly to the specified location.",
                       StringComparison.Ordinal) &&
                   description.Contains("Grants Hidden", StringComparison.Ordinal) &&
                   description.Contains(
                       "Cannot be executed while bound.",
                       StringComparison.Ordinal) &&
                   description.Contains(
                       "Action changes to Doton while under the effect of Three Mudra.",
                       StringComparison.Ordinal);
        });

        var ninjaShukuchiHiddenStatuses = NinjaShukuchiHiddenStatusCatalog.Empty;
        _ = ValidateFeature("Ninja Shukuchi Hidden statuses", log, () =>
        {
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
            var hiddenStatusIds = new List<uint>();
            foreach (var status in statuses)
            {
                if (string.Equals(
                        status.Name.ToString(),
                        "Hidden",
                        StringComparison.Ordinal))
                {
                    hiddenStatusIds.Add(status.RowId);
                }
            }

            var resolved = NinjaShukuchiHiddenStatusCatalog.Create(hiddenStatusIds);
            if (!resolved.IsVerified) return false;

            ninjaShukuchiHiddenStatuses = resolved;
            return true;
        });

        var contradanceVerified = ValidateFeature("Contradance", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
            return actions.TryGetRow(EnemyCombatConstants.ContradanceActionId, out var action) &&
                   descriptions.TryGetRow(
                       EnemyCombatConstants.ContradanceActionId,
                       out var transient) &&
                   statuses.TryGetRow(EnemyCombatConstants.SeducedStatusId, out var seduced) &&
                   string.Equals(action.Name.ToString(), "Contradance", StringComparison.Ordinal) &&
                   action.Icon == EnemyCombatConstants.ContradanceActionIconId &&
                   action.IsPvP &&
                   action.ClassJob.IsValid &&
                   action.ClassJob.RowId == EnemyCombatConstants.DancerJobId &&
                   action.Range == 0 &&
                   action.EffectRange == 15 &&
                   action.CastType == 2 &&
                   action.Cast100ms == 0 &&
                   action.Recast100ms == EnemyCombatConstants.ContradanceRecast100ms &&
                   action.CanTargetSelf &&
                   !action.CanTargetHostile &&
                   !action.TargetArea &&
                   !action.AffectsPosition &&
                   transient.Description.ToString().Contains("Seduced", StringComparison.Ordinal) &&
                   string.Equals(seduced.Name.ToString(), "Seduced", StringComparison.Ordinal) &&
                   seduced.Icon == EnemyCombatConstants.SeducedStatusIconId;
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

        var monkEarthReplyVerified = ValidateFeature("Monk Earth's Reply", log, () =>
        {
            var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
            var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
            var procStatuses = dataManager.GetExcelSheet<ActionProcStatus>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
            if (!actions.TryGetRow(MonkEarthReplyRules.RiddleOfEarthActionId, out var baseAction) ||
                !actions.TryGetRow(MonkEarthReplyRules.EarthsReplyActionId, out var followUp) ||
                !descriptions.TryGetRow(MonkEarthReplyRules.RiddleOfEarthActionId, out var baseTransient) ||
                !descriptions.TryGetRow(MonkEarthReplyRules.EarthsReplyActionId, out var followUpTransient) ||
                !procStatuses.TryGetRow(MonkEarthReplyRules.EarthsReplyProcStatusRowId, out var procStatus) ||
                !statuses.TryGetRow(MonkEarthReplyRules.EarthResonanceStatusId, out var resonance))
            {
                return false;
            }

            return string.Equals(baseAction.Name.ToString(), "Riddle of Earth", StringComparison.Ordinal) &&
                   baseAction.Icon == MonkEarthReplyRules.RiddleOfEarthIconId &&
                   baseAction.IsPvP &&
                   baseAction.IsPlayerAction &&
                   baseAction.ClassJob.IsValid &&
                   baseAction.ClassJob.RowId == MonkEarthReplyRules.MonkJobId &&
                   baseAction.Range == 0 &&
                   baseAction.EffectRange == 0 &&
                   baseAction.Cast100ms == 0 &&
                   baseAction.Recast100ms == 240 &&
                   baseAction.CanTargetSelf &&
                   !baseAction.CanTargetHostile &&
                   !baseAction.CanTargetParty &&
                   !baseAction.CanTargetAlly &&
                   !baseAction.CanTargetAlliance &&
                   !baseAction.TargetArea &&
                   !baseAction.AffectsPosition &&
                   baseTransient.Description.ToString().Contains(
                       "Grants Earth Resonance, changing Riddle of Earth to Earth's Reply",
                       StringComparison.Ordinal) &&
                   baseTransient.Description.ToString().Contains("Duration: 8s", StringComparison.Ordinal) &&
                   string.Equals(followUp.Name.ToString(), "Earth's Reply", StringComparison.Ordinal) &&
                   followUp.Icon == MonkEarthReplyRules.EarthsReplyIconId &&
                   followUp.IsPvP &&
                   !followUp.IsPlayerAction &&
                   followUp.ClassJob.IsValid &&
                   followUp.ClassJob.RowId == MonkEarthReplyRules.MonkJobId &&
                   followUp.Range == 0 &&
                   followUp.EffectRange == 6 &&
                   followUp.Cast100ms == 0 &&
                   followUp.Recast100ms == 10 &&
                   followUp.CanTargetSelf &&
                   !followUp.CanTargetHostile &&
                   !followUp.CanTargetParty &&
                   !followUp.CanTargetAlly &&
                   !followUp.CanTargetAlliance &&
                   !followUp.TargetArea &&
                   !followUp.AffectsPosition &&
                   followUp.ActionProcStatus.RowId == MonkEarthReplyRules.EarthsReplyProcStatusRowId &&
                   procStatus.Status.RowId == MonkEarthReplyRules.EarthResonanceStatusId &&
                   followUpTransient.Description.ToString().Contains(
                       "Can only be executed while under the effect of Earth Resonance.",
                       StringComparison.Ordinal) &&
                   followUpTransient.Description.ToString().Contains(
                       "This action cannot be assigned to a hotbar.",
                       StringComparison.Ordinal) &&
                   string.Equals(resonance.Name.ToString(), "Earth Resonance", StringComparison.Ordinal) &&
                   resonance.Icon == MonkEarthReplyRules.EarthResonanceIconId &&
                   resonance.StatusCategory == 1 &&
                   !resonance.CanDispel &&
                   !resonance.IsPermanent &&
                   resonance.Description.ToString().Contains(
                       "healing potency of Earth's Reply",
                       StringComparison.Ordinal);
        });

        var gunbreakerContinuationVerified =
            GunbreakerContinuationProbe.ValidateMetadata(dataManager, log);

        var darkKnightShadowbringerVerified = ValidateFeature(
            "Dark Knight Shadowbringer",
            log,
            () =>
            {
                var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
                var procStatuses = dataManager.GetExcelSheet<ActionProcStatus>(ClientLanguage.English);
                var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
                if (!actions.TryGetRow(
                        DarkKnightShadowbringerRules.ShadowbringerActionId,
                        out var baseAction) ||
                    !actions.TryGetRow(
                        DarkKnightShadowbringerRules.DarkArtsShadowbringerActionId,
                        out var darkArtsAction) ||
                    !actions.TryGetRow(
                        DarkKnightShadowbringerRules.TheBlackestNightActionId,
                        out var blackestNight) ||
                    !procStatuses.TryGetRow(baseAction.ActionProcStatus.RowId, out var proc) ||
                    !statuses.TryGetRow(
                        DarkKnightShadowbringerRules.DarkArtsStatusId,
                        out var darkArtsStatus))
                {
                    return false;
                }

                return ValidateShadowbringerAction(
                           baseAction,
                           DarkKnightShadowbringerRules.ShadowbringerHpCost,
                           expectedCostType: 105) &&
                       ValidateShadowbringerAction(
                           darkArtsAction,
                           DarkKnightShadowbringerRules.DarkArtsStatusId,
                           expectedCostType: 10) &&
                       string.Equals(
                           blackestNight.Name.ToString(),
                           "the Blackest Night",
                           StringComparison.Ordinal) &&
                       blackestNight.Icon == DarkKnightShadowbringerRules.TheBlackestNightIconId &&
                       blackestNight.IsPvP &&
                       blackestNight.IsPlayerAction &&
                       blackestNight.ClassJob.IsValid &&
                       blackestNight.ClassJob.RowId == DarkKnightShadowbringerRules.DarkKnightJobId &&
                       blackestNight.Range == 30 &&
                       blackestNight.Cast100ms == 0 &&
                       blackestNight.Recast100ms == 160 &&
                       blackestNight.CooldownGroup == 3 &&
                       blackestNight.MaxCharges == 2 &&
                       blackestNight.CanTargetSelf &&
                       blackestNight.CanTargetParty &&
                       !blackestNight.CanTargetHostile &&
                       !blackestNight.TargetArea &&
                       blackestNight.RequiresLineOfSight &&
                       baseAction.ActionProcStatus.RowId != 0 &&
                       darkArtsAction.ActionProcStatus.RowId == baseAction.ActionProcStatus.RowId &&
                       proc.Status.RowId == DarkKnightShadowbringerRules.DarkArtsStatusId &&
                       string.Equals(darkArtsStatus.Name.ToString(), "Dark Arts", StringComparison.Ordinal) &&
                       darkArtsStatus.Icon == DarkKnightShadowbringerRules.DarkArtsStatusIconId &&
                       darkArtsStatus.StatusCategory == 1 &&
                       !darkArtsStatus.CanDispel &&
                       !darkArtsStatus.IsPermanent;
            });

        var darkKnightBlackbloodVerified = ValidateFeature(
            "Dark Knight Blackblood",
            log,
            () =>
            {
                var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
                if (!statuses.TryGetRow(
                        DarkKnightShadowbringerRules.BlackbloodStatusId,
                        out var blackblood))
                {
                    return false;
                }

                return string.Equals(
                           blackblood.Name.ToString(),
                           "Blackblood",
                           StringComparison.Ordinal) &&
                       blackblood.Icon ==
                           DarkKnightShadowbringerRules.BlackbloodStatusIconId &&
                       blackblood.ClassJobCategory.IsValid &&
                       blackblood.ClassJobCategory.RowId ==
                           DarkKnightShadowbringerRules
                               .DarkKnightClassJobCategoryId &&
                       blackblood.StatusCategory == 1 &&
                       !blackblood.CanDispel &&
                       !blackblood.IsPermanent &&
                       blackblood.CanStatusOff &&
                       blackblood.Description.ToString().Contains(
                           "Able to execute powerful weaponskills.",
                           StringComparison.Ordinal);
            });

        var redMageResolutionVerified = ValidateFeature(
            "Red Mage Resolution",
            log,
            () =>
            {
                var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
                if (!actions.TryGetRow(
                        MiracleInterceptConfirmationRules.ResolutionActionId,
                        out var action))
                {
                    return false;
                }

                return string.Equals(action.Name.ToString(), "Resolution", StringComparison.Ordinal) &&
                       action.Icon == ReactiveCounterCcProfileRules.ResolutionIconId &&
                       action.IsPvP &&
                       action.IsPlayerAction &&
                       action.ClassJob.IsValid &&
                       action.ClassJob.RowId == ReactiveCounterCcProfileRules.RedMageJobId &&
                       action.ActionCategory.IsValid &&
                       action.ActionCategory.RowId == 2 &&
                       action.Range == ReactiveCounterCcProfileRules.ResolutionMaximumRangeYalms &&
                       action.EffectRange == 25 &&
                       action.Cast100ms == 0 &&
                       action.Recast100ms == 200 &&
                       action.CooldownGroup == 1 &&
                       action.MaxCharges == 0 &&
                       action.CanTargetHostile &&
                       !action.CanTargetSelf &&
                       !action.CanTargetParty &&
                       !action.CanTargetAlly &&
                       !action.TargetArea &&
                       action.RequiresLineOfSight &&
                       action.NeedToFaceTarget &&
                       !action.AffectsPosition &&
                       action.CastType == 4 &&
                       action.PrimaryCostType == 0 &&
                       action.PrimaryCostValue == 0;
            });

        var redMageViceOfThornsVerified = ValidateFeature(
            "Red Mage Vice of Thorns",
            log,
            () =>
            {
                var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
                var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
                var procStatuses = dataManager.GetExcelSheet<ActionProcStatus>(ClientLanguage.English);
                var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
                if (!actions.TryGetRow(
                        ReactiveCounterCcProfileRules.ForteCarrierActionId,
                        out var carrier) ||
                    !actions.TryGetRow(
                        MiracleInterceptConfirmationRules.ViceOfThornsActionId,
                        out var action) ||
                    !descriptions.TryGetRow(carrier.RowId, out var carrierDescription) ||
                    !descriptions.TryGetRow(action.RowId, out var actionDescription) ||
                    !procStatuses.TryGetRow(
                        ReactiveCounterCcProfileRules.ViceOfThornsProcStatusRowId,
                        out var proc) ||
                    !statuses.TryGetRow(
                        ReactiveCounterCcProfileRules.ThornedFlourishStatusId,
                        out var status))
                {
                    return false;
                }

                return string.Equals(carrier.Name.ToString(), "Forte", StringComparison.Ordinal) &&
                       carrier.Icon == 9_064 &&
                       carrier.IsPvP &&
                       carrier.IsPlayerAction &&
                       carrier.ClassJob.IsValid &&
                       carrier.ClassJob.RowId == ReactiveCounterCcProfileRules.RedMageJobId &&
                       carrier.Cast100ms == 0 &&
                       carrier.Recast100ms == 200 &&
                       carrier.CooldownGroup == 6 &&
                       carrier.CanTargetSelf &&
                       !carrier.CanTargetHostile &&
                       carrierDescription.Description.ToString().Contains(
                           "Action changes to Vice of Thorns",
                           StringComparison.Ordinal) &&
                       string.Equals(action.Name.ToString(), "Vice of Thorns", StringComparison.Ordinal) &&
                       action.Icon == ReactiveCounterCcProfileRules.ViceOfThornsIconId &&
                       action.IsPvP &&
                       !action.IsPlayerAction &&
                       action.ClassJob.IsValid &&
                       action.ClassJob.RowId == ReactiveCounterCcProfileRules.RedMageJobId &&
                       action.ActionCategory.IsValid &&
                       action.ActionCategory.RowId == 4 &&
                       action.Range == ReactiveCounterCcProfileRules.ViceOfThornsMaximumRangeYalms &&
                       action.EffectRange == 5 &&
                       action.Cast100ms == 0 &&
                       action.Recast100ms == 10 &&
                       action.CooldownGroup == 7 &&
                       action.CanTargetHostile &&
                       !action.CanTargetSelf &&
                       !action.TargetArea &&
                       action.RequiresLineOfSight &&
                       action.NeedToFaceTarget &&
                       action.CastType == 2 &&
                       action.PrimaryCostType == 10 &&
                       action.PrimaryCostValue ==
                           ReactiveCounterCcProfileRules.ThornedFlourishStatusId &&
                       action.ActionProcStatus.RowId ==
                           ReactiveCounterCcProfileRules.ViceOfThornsProcStatusRowId &&
                       proc.Status.RowId ==
                           ReactiveCounterCcProfileRules.ThornedFlourishStatusId &&
                       string.Equals(status.Name.ToString(), "Thorned Flourish", StringComparison.Ordinal) &&
                       status.Icon == ReactiveCounterCcProfileRules.ThornedFlourishStatusIconId &&
                       status.StatusCategory == 1 &&
                       !status.CanDispel &&
                       !status.IsPermanent &&
                       status.CanStatusOff &&
                       actionDescription.Description.ToString().Contains(
                           "Additional Effect: Stun",
                           StringComparison.Ordinal);
            });

        var blackMageFrostStarVerified = ValidateFeature(
            "Black Mage Frost Star",
            log,
            () =>
            {
                var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
                var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
                var procStatuses = dataManager.GetExcelSheet<ActionProcStatus>(ClientLanguage.English);
                var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
                if (!actions.TryGetRow(
                        ReactiveCounterCcProfileRules.SoulResonanceCarrierActionId,
                        out var carrier) ||
                    !actions.TryGetRow(
                        MiracleInterceptConfirmationRules.FrostStarActionId,
                        out var action) ||
                    !descriptions.TryGetRow(carrier.RowId, out var carrierDescription) ||
                    !descriptions.TryGetRow(action.RowId, out var actionDescription) ||
                    !procStatuses.TryGetRow(
                        ReactiveCounterCcProfileRules.FrostStarProcStatusRowId,
                        out var proc) ||
                    !statuses.TryGetRow(
                        ReactiveCounterCcProfileRules.ElementalStarStatusId,
                        out var status))
                {
                    return false;
                }

                return string.Equals(carrier.Name.ToString(), "Soul Resonance", StringComparison.Ordinal) &&
                       carrier.Icon == 9_673 &&
                       carrier.IsPvP &&
                       carrier.IsPlayerAction &&
                       carrier.ClassJob.IsValid &&
                       carrier.ClassJob.RowId == ReactiveCounterCcProfileRules.BlackMageJobId &&
                       carrierDescription.Description.ToString().Contains(
                           "Frost Star while under the effect of Umbral Ice",
                           StringComparison.Ordinal) &&
                       string.Equals(action.Name.ToString(), "Frost Star", StringComparison.Ordinal) &&
                       action.Icon == ReactiveCounterCcProfileRules.FrostStarIconId &&
                       action.IsPvP &&
                       !action.IsPlayerAction &&
                       action.ClassJob.IsValid &&
                       action.ClassJob.RowId == ReactiveCounterCcProfileRules.BlackMageJobId &&
                       action.ActionCategory.IsValid &&
                       action.ActionCategory.RowId == 2 &&
                       action.Range == ReactiveCounterCcProfileRules.FrostStarMaximumRangeYalms &&
                       action.EffectRange == 5 &&
                       action.Cast100ms == 0 &&
                       action.Recast100ms == 25 &&
                       action.CooldownGroup == 58 &&
                       action.CanTargetHostile &&
                       !action.CanTargetSelf &&
                       !action.TargetArea &&
                       action.RequiresLineOfSight &&
                       action.NeedToFaceTarget &&
                       action.CastType == 2 &&
                       action.PrimaryCostType == 10 &&
                       action.PrimaryCostValue ==
                           ReactiveCounterCcProfileRules.ElementalStarStatusId &&
                       action.ActionProcStatus.RowId ==
                           ReactiveCounterCcProfileRules.FrostStarProcStatusRowId &&
                       proc.Status.RowId ==
                           ReactiveCounterCcProfileRules.ElementalStarStatusId &&
                       string.Equals(status.Name.ToString(), "Elemental Star", StringComparison.Ordinal) &&
                       status.Icon == ReactiveCounterCcProfileRules.ElementalStarStatusIconId &&
                       status.StatusCategory == 1 &&
                       !status.CanDispel &&
                       !status.IsPermanent &&
                       status.CanStatusOff &&
                       actionDescription.Description.ToString().Contains(
                           "Additional Effect: Afflicts target with Deep Freeze",
                           StringComparison.Ordinal);
            });

        var monkHeldComboVerified =
            MonkHeldComboProbe.ValidateMetadata(dataManager, log);

        var validation = new PvPMetadataValidation(
            seitonVerified,
            viperSerpentTailVerified,
            wolvesDenStrikingDummyVerified,
            guardVerified,
            smartActionProtectionStatusesVerified,
            guardianVerified,
            recuperateVerified,
            wildfireVerified,
            deathWarrantVerified,
            marksmanSpiteVerified,
            purifyVerified,
            allyRescueStatusesVerified,
            miracleOfNatureActionVerified,
            silentNocturneVerified,
            panicShukuchiVerified,
            contradanceVerified,
            zantetsukenVerified,
            furiousBacklashVerified,
            monkEarthReplyVerified,
            scholarCriticalStrategyVerified,
            emergencyTeleportMonkVerified,
            emergencyTeleportBlackMageVerified,
            emergencyTeleportSageVerified,
            emergencyTeleportViperVerified,
            smartKardiaVerified,
            autoLowMpFocusProbeVerified,
            darkKnightPlungeVerified,
            gunbreakerContinuationVerified,
            darkKnightShadowbringerVerified,
            darkKnightBlackbloodVerified,
            redMageResolutionVerified,
            redMageViceOfThornsVerified,
            blackMageFrostStarVerified,
            monkHeldComboVerified,
            ninjaShukuchiHiddenStatuses,
            smartActionGuardBypassActions);

        log.Information(
            "Seiton Sense metadata: Seiton={Seiton}, ViperSerpentTail={ViperSerpentTail}, " +
            "WolvesDenStrikingDummy={WolvesDenStrikingDummy}, Guard={Guard}, " +
            "SmartActionProtectionStatuses={SmartActionProtectionStatuses}, Guardian={Guardian}, Recuperate={Recuperate}, " +
            "Wildfire={Wildfire}, DeathWarrant={DeathWarrant}, MarksmanSpite={MarksmanSpite}, " +
            "Purify={Purify}, AllyRescueStatuses={AllyRescueStatuses}, MiracleAction={MiracleAction}, " +
            "SilentNocturne={SilentNocturne}, PanicShukuchi={PanicShukuchi}, ShukuchiHidden={ShukuchiHidden}, " +
            "Contradance={Contradance}, Zantetsuken={Zantetsuken}, " +
            "FuriousBacklash={FuriousBacklash}, MonkEarthReply={MonkEarthReply}, " +
            "ScholarCriticalStrategy={ScholarCriticalStrategy}, " +
            "EmergencyTeleport={EmergencyTeleportMonk}/{EmergencyTeleportBlackMage}/" +
            "{EmergencyTeleportSage}/{EmergencyTeleportViper}, SmartKardia={SmartKardia}, " +
            "AutoLowMpFocusProbe={AutoLowMpFocusProbe}, DarkKnightPlunge={DarkKnightPlunge}, " +
            "GunbreakerContinuation={GunbreakerContinuation}, DarkKnightShadowbringer={DarkKnightShadowbringer}, " +
            "DarkKnightBlackblood={DarkKnightBlackblood}, " +
            "RedMageResolution={RedMageResolution}, RedMageViceOfThorns={RedMageViceOfThorns}, " +
            "BlackMageFrostStar={BlackMageFrostStar}, MonkHeldCombo={MonkHeldCombo}, " +
            "SmartActionGuardBypassActions={SmartActionGuardBypassActions}.",
            validation.SeitonVerified,
            validation.ViperSerpentTailVerified,
            validation.WolvesDenStrikingDummyVerified,
            validation.GuardVerified,
            validation.SmartActionProtectionStatusesVerified,
            validation.GuardianVerified,
            validation.RecuperateVerified,
            validation.WildfireVerified,
            validation.DeathWarrantVerified,
            validation.MarksmanSpiteVerified,
            validation.PurifyVerified,
            validation.AllyRescueStatusesVerified,
            validation.MiracleOfNatureActionVerified,
            validation.SilentNocturneVerified,
            validation.PanicShukuchiVerified,
            validation.NinjaShukuchiHiddenStatuses.Count,
            validation.ContradanceVerified,
            validation.ZantetsukenVerified,
            validation.FuriousBacklashVerified,
            validation.MonkEarthReplyVerified,
            validation.ScholarCriticalStrategyVerified,
            validation.EmergencyTeleportMonkVerified,
            validation.EmergencyTeleportBlackMageVerified,
            validation.EmergencyTeleportSageVerified,
            validation.EmergencyTeleportViperVerified,
            validation.SmartKardiaVerified,
            validation.AutoLowMpFocusProbeVerified,
            validation.DarkKnightPlungeVerified,
            validation.GunbreakerContinuationVerified,
            validation.DarkKnightShadowbringerVerified,
            validation.DarkKnightBlackbloodVerified,
            validation.RedMageResolutionVerified,
            validation.RedMageViceOfThornsVerified,
            validation.BlackMageFrostStarVerified,
            validation.MonkHeldComboVerified,
            validation.SmartActionGuardBypassActions.Count);

        return validation;
    }

    private static bool ValidateShadowbringerAction(
        ActionSheet action,
        uint expectedCostValue,
        byte expectedCostType) =>
        string.Equals(action.Name.ToString(), "Shadowbringer", StringComparison.Ordinal) &&
        action.Icon == DarkKnightShadowbringerRules.ShadowbringerIconId &&
        action.IsPvP &&
        DarkKnightShadowbringerRules.HasExpectedPlayerActionFlag(
            action.RowId,
            action.IsPlayerAction) &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == DarkKnightShadowbringerRules.DarkKnightJobId &&
        action.ClassJobCategory.IsValid &&
        action.ClassJobCategory.RowId ==
        DarkKnightShadowbringerRules.DarkKnightClassJobCategoryId &&
        action.ActionCategory.IsValid &&
        action.ActionCategory.RowId == 4 &&
        action.Range == DarkKnightShadowbringerRules.MaximumRangeYalms &&
        action.EffectRange == DarkKnightShadowbringerRules.MaximumRangeYalms &&
        action.Cast100ms == 0 &&
        action.Recast100ms == 10 &&
        action.CooldownGroup == 1 &&
        action.AdditionalCooldownGroup == 0 &&
        action.MaxCharges == 0 &&
        action.CanTargetHostile &&
        !action.CanTargetSelf &&
        !action.CanTargetParty &&
        !action.CanTargetAlly &&
        !action.TargetArea &&
        action.RequiresLineOfSight &&
        action.NeedToFaceTarget &&
        !action.AffectsPosition &&
        action.CastType == 4 &&
        action.PrimaryCostType == expectedCostType &&
        action.PrimaryCostValue == expectedCostValue;

    private static bool ValidateViperSerpentTailCarrier(
        ExcelSheet<ActionSheet> actions,
        ExcelSheet<ActionTransient> descriptions)
    {
        if (!actions.TryGetRow(ViperSerpentTailRules.CarrierActionId, out var action) ||
            !descriptions.TryGetRow(ViperSerpentTailRules.CarrierActionId, out var transient))
        {
            return false;
        }

        var description = transient.Description.ToString();
        return action.Name.ToString() == "Serpent's Tail" &&
               action.Icon == 9_726 &&
               action.IsPvP &&
               action.IsPlayerAction &&
               action.ClassJob.IsValid &&
               action.ClassJob.RowId == ViperSerpentTailRules.ViperJobId &&
               action.ClassJobCategory.IsValid &&
               action.ClassJobCategory.RowId == 196 &&
               action.ActionCategory.IsValid &&
               action.ActionCategory.RowId == 4 &&
               action.Range == 0 &&
               action.EffectRange == 0 &&
               action.CastType == 0 &&
               action.Cast100ms == 0 &&
               action.Recast100ms == 10 &&
               action.CooldownGroup == 3 &&
               action.AdditionalCooldownGroup == 0 &&
               action.MaxCharges == 0 &&
               action.ActionProcStatus.RowId == 0 &&
               !action.CanTargetSelf &&
               !action.CanTargetParty &&
               !action.CanTargetAlliance &&
               !action.CanTargetHostile &&
               !action.CanTargetAlly &&
               !action.CanTargetOwnPet &&
               !action.CanTargetPartyPet &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.NeedToFaceTarget &&
               action.PreservesCombo &&
               !action.AffectsPosition &&
               description.Contains("Changes to Death Rattle, Twinfang Bite, Twinblood Bite", StringComparison.Ordinal) &&
               description.Contains("Uncoiled Twinfang, Uncoiled Twinblood", StringComparison.Ordinal) &&
               description.Contains("First Legacy, Second Legacy, Third Legacy, or Fourth Legacy", StringComparison.Ordinal);
    }

    private static bool ValidateViperSerpentTailFollowUp(
        ExcelSheet<ActionSheet> actions,
        ExcelSheet<ActionTransient> descriptions,
        ExcelSheet<ActionProcStatus> procStatuses,
        uint actionId,
        string expectedName,
        uint expectedIcon,
        ushort expectedRecast100ms,
        sbyte expectedRange,
        byte expectedEffectRange,
        byte expectedCastType,
        uint expectedProcStatusId,
        uint expectedHiddenStatusId)
    {
        if (!ViperSerpentTailRules.IsExactFollowUpAction(actionId) ||
            !actions.TryGetRow(actionId, out var action) ||
            !descriptions.TryGetRow(actionId, out var transient) ||
            !procStatuses.TryGetRow(expectedProcStatusId, out var procStatus))
        {
            return false;
        }

        var description = transient.Description.ToString();
        return action.Name.ToString() == expectedName &&
               action.Icon == expectedIcon &&
               action.IsPvP &&
               !action.IsPlayerAction &&
               action.ClassJob.IsValid &&
               action.ClassJob.RowId == ViperSerpentTailRules.ViperJobId &&
               action.ClassJobCategory.IsValid &&
               action.ClassJobCategory.RowId == 196 &&
               action.ActionCategory.IsValid &&
               action.ActionCategory.RowId == 4 &&
               action.Range == expectedRange &&
               action.EffectRange == expectedEffectRange &&
               action.CastType == expectedCastType &&
               action.Cast100ms == 0 &&
               action.Recast100ms == expectedRecast100ms &&
               action.CooldownGroup == 3 &&
               action.AdditionalCooldownGroup == 0 &&
               action.MaxCharges == 0 &&
               action.ActionProcStatus.RowId == expectedProcStatusId &&
               procStatus.Status.RowId == expectedHiddenStatusId &&
               !action.CanTargetSelf &&
               !action.CanTargetParty &&
               !action.CanTargetAlliance &&
               action.CanTargetHostile &&
               !action.CanTargetAlly &&
               !action.CanTargetOwnPet &&
               !action.CanTargetPartyPet &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.NeedToFaceTarget &&
               action.PreservesCombo &&
               !action.AffectsPosition &&
               description.Contains("potency of 4,000", StringComparison.Ordinal) &&
               description.Contains("Ignores the effects of Guard when dealing damage.", StringComparison.Ordinal) &&
               description.Contains("Adds 3 seconds of charge to the limit gauge", StringComparison.Ordinal) &&
               description.Contains("This action cannot be assigned to a hotbar.", StringComparison.Ordinal);
    }

    private static bool ValidateEmergencyTeleportAction(
        IDataManager dataManager,
        uint actionId,
        string expectedName,
        uint expectedIcon,
        uint expectedJobId,
        uint expectedJobCategoryId,
        sbyte expectedRange,
        ushort expectedRecast100ms,
        byte expectedCooldownGroup,
        byte expectedAdditionalCooldownGroup,
        byte expectedMaximumCharges,
        bool needToFaceTarget)
    {
        var actions = dataManager.GetExcelSheet<ActionSheet>(ClientLanguage.English);
        var descriptions = dataManager.GetExcelSheet<ActionTransient>(ClientLanguage.English);
        if (!actions.TryGetRow(actionId, out var action) ||
            !descriptions.TryGetRow(actionId, out var transient))
        {
            return false;
        }

        return string.Equals(action.Name.ToString(), expectedName, StringComparison.Ordinal) &&
               action.Icon == expectedIcon &&
               action.IsPvP &&
               action.IsPlayerAction &&
               action.ClassJob.IsValid &&
               action.ClassJob.RowId == expectedJobId &&
               action.ClassJobCategory.IsValid &&
               action.ClassJobCategory.RowId == expectedJobCategoryId &&
               action.ActionCategory.IsValid &&
               action.ActionCategory.RowId == 4 &&
               action.Range == expectedRange &&
               action.EffectRange == 0 &&
               action.Cast100ms == 0 &&
               action.Recast100ms == expectedRecast100ms &&
               action.PrimaryCostType == 0 &&
               action.PrimaryCostValue == 0 &&
               action.SecondaryCostType == 0 &&
               action.SecondaryCostValue.RowId == 0 &&
               action.CooldownGroup == expectedCooldownGroup &&
               action.AdditionalCooldownGroup == expectedAdditionalCooldownGroup &&
               action.MaxCharges == expectedMaximumCharges &&
               !action.CanTargetSelf &&
               action.CanTargetParty &&
               action.CanTargetAlliance &&
               action.CanTargetHostile &&
               action.CanTargetAlly &&
               !action.CanTargetOwnPet &&
               !action.CanTargetPartyPet &&
               !action.TargetArea &&
               action.RequiresLineOfSight &&
               action.NeedToFaceTarget == needToFaceTarget &&
               action.AffectsPosition &&
               action.CastType == 1 &&
               transient.Description.ToString().Contains(
                   "Rush to a target's side.",
                   StringComparison.Ordinal);
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

    private static bool ValidateSeitonProtectionStatus(
        ExcelSheet<Status> statuses,
        uint statusId,
        string expectedName) =>
        NinjaSeitonProtectionStatusCatalog.IsExecuteBlockingStatus(statusId) &&
        statuses.TryGetRow(statusId, out var status) &&
        string.Equals(
            status.Name.ToString(),
            expectedName,
            StringComparison.Ordinal);

    private static bool ValidateNamedStatus(
        ExcelSheet<Status> statuses,
        uint statusId,
        string expectedName) =>
        statuses.TryGetRow(statusId, out var status) &&
        status.RowId == statusId &&
        string.Equals(status.Name.ToString(), expectedName, StringComparison.Ordinal);

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
