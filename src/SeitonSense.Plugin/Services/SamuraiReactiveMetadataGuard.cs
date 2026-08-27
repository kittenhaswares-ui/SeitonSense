using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using GameAction = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal sealed record SamuraiReactiveMetadataValidation(
    bool SotenVerified,
    bool MineuchiVerified,
    bool ZantetsukenVerified,
    bool KuzushiVerified,
    bool ChitenVerified,
    bool ProtectionSignalPrerequisitesVerified,
    bool WolvesDenStrikingDummyVerified)
{
    internal bool CounterCcVerified =>
        SotenVerified && MineuchiVerified && ProtectionSignalPrerequisitesVerified;
    internal bool ZantetsukenWorkflowVerified => ZantetsukenVerified && KuzushiVerified;

    internal static SamuraiReactiveMetadataValidation None { get; } = new(
        false,
        false,
        false,
        false,
        false,
        false,
        false);
}

/// <summary>
/// Isolated English-sheet pin for the SAM runtime. Every native boundary is
/// disabled independently when its current action/status shape drifts.
/// </summary>
internal static class SamuraiReactiveMetadataGuard
{
    internal const uint SotenIconId = 9_209;
    internal const uint MineuchiIconId = 9_665;
    internal const uint ZantetsukenIconId = 9_666;
    internal const uint KuzushiIconId = 214_954;
    internal const uint ChitenStatusId = 1_240;
    internal const uint ChitenIconId = 214_820;

    internal static SamuraiReactiveMetadataValidation Validate(
        IDataManager dataManager,
        IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var statuses = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
            var soten = actions.TryGetRow(
                            SamuraiReactiveCounterCcRules.SotenActionId,
                            out var sotenRow) &&
                        ValidateAction(
                            sotenRow,
                            "Hissatsu: Soten",
                            SotenIconId,
                            range: 20,
                            effectRange: 0,
                            recast100ms: 100,
                            maxCharges: 3,
                            affectsPosition: true);
            var mineuchi = actions.TryGetRow(
                               SamuraiReactiveCounterCcRules.MineuchiActionId,
                               out var mineuchiRow) &&
                           ValidateAction(
                               mineuchiRow,
                               "Mineuchi",
                               MineuchiIconId,
                               range: 5,
                               effectRange: 0,
                               recast100ms: 160,
                               maxCharges: 0,
                               affectsPosition: false);
            var zantetsuken = actions.TryGetRow(
                                  SamuraiZantetsukenRules.ActionId,
                                  out var zantetsukenRow) &&
                              ValidateAction(
                                  zantetsukenRow,
                                  "Zantetsuken",
                                  ZantetsukenIconId,
                                  range: 20,
                                  effectRange: 5,
                                  recast100ms: 100,
                                  maxCharges: 0,
                                  affectsPosition: true);
            var kuzushi = statuses.TryGetRow(
                              SamuraiZantetsukenRules.KuzushiStatusId,
                              out var kuzushiRow) &&
                          kuzushiRow.RowId == SamuraiZantetsukenRules.KuzushiStatusId &&
                          string.Equals(
                              kuzushiRow.Name.ExtractText(),
                              "Kuzushi",
                              StringComparison.Ordinal) &&
                          kuzushiRow.Icon == KuzushiIconId &&
                          kuzushiRow.StatusCategory == 2 &&
                          !kuzushiRow.CanDispel &&
                          !kuzushiRow.IsPermanent &&
                          kuzushiRow.Description.ExtractText().Contains(
                              "samurai who applied this effect",
                              StringComparison.Ordinal);
            var chiten = statuses.TryGetRow(ChitenStatusId, out var chitenRow) &&
                         chitenRow.RowId == ChitenStatusId &&
                         string.Equals(
                             chitenRow.Name.ExtractText(),
                             "Chiten",
                             StringComparison.Ordinal) &&
                         chitenRow.Icon == ChitenIconId &&
                         chitenRow.StatusCategory == 1 &&
                         !chitenRow.CanDispel &&
                         !chitenRow.IsPermanent &&
                         chitenRow.Description.ExtractText().Contains(
                             "countering attacks",
                             StringComparison.Ordinal);
            var protectionSignalPrerequisites =
                actions.TryGetRow(
                    SamuraiReactiveRuntimeRules.PurifyActionId,
                    out var purifyRow) &&
                ValidateCommonSelfAction(
                    purifyRow,
                    "Purify",
                    iconId: 9_112,
                    recast100ms: 40) &&
                actions.TryGetRow(
                    SamuraiReactiveRuntimeRules.GuardActionId,
                    out var guardActionRow) &&
                ValidateCommonSelfAction(
                    guardActionRow,
                    "Guard",
                    iconId: 9_581,
                    recast100ms: 300) &&
                ValidateProtectionStatus(
                    statuses,
                    SamuraiReactiveRuntimeRules.ResilienceStatusId,
                    "Resilience",
                    iconId: 214_891,
                    "Nullifying status afflictions") &&
                ValidateProtectionStatus(
                    statuses,
                    SamuraiReactiveRuntimeRules.GuardStatusId,
                    "Guard",
                    iconId: 214_890,
                    "All Stun, Heavy, Bind, Silence") &&
                ValidateProtectionStatus(
                    statuses,
                    SamuraiReactiveRuntimeRules.GuardAlternateStatusId,
                    "Guard",
                    iconId: 214_715,
                    "All Stun, Heavy, Bind, Silence");
            var dummy = StrictWolvesDenStrikingDummyResolver.ValidateMetadata(
                dataManager,
                log);

            if (!soten) log.Warning("Seiton Sense disabled unverified SAM Soten runtime.");
            if (!mineuchi) log.Warning("Seiton Sense disabled unverified SAM Mineuchi runtime.");
            if (!zantetsuken)
                log.Warning("Seiton Sense disabled unverified SAM Zantetsuken runtime.");
            if (!kuzushi)
                log.Warning("Seiton Sense disabled unverified SAM Kuzushi runtime.");
            if (!chiten)
            {
                log.Warning(
                    "Seiton Sense will conservatively exclude SAM from Smart Action because Chiten metadata drifted.");
            }
            if (!protectionSignalPrerequisites)
            {
                log.Warning(
                    "Seiton Sense disabled SAM counter-CC because shared Purify/Guard metadata drifted.");
            }

            return new SamuraiReactiveMetadataValidation(
                soten,
                mineuchi,
                zantetsuken,
                kuzushi,
                chiten,
                protectionSignalPrerequisites,
                dummy);
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense SAM reactive metadata lookup failed closed.");
            return SamuraiReactiveMetadataValidation.None;
        }
    }

    // Both reviewed stages are Ability rows on their independent PvP cooldown
    // lane, not main-GCD weaponskills. The staged scheduler therefore stays
    // tight and never reserves or blocks a manual GCD.
    private static bool ValidateAction(
        GameAction action,
        string name,
        uint iconId,
        sbyte range,
        byte effectRange,
        ushort recast100ms,
        byte maxCharges,
        bool affectsPosition) =>
        action.RowId != 0 &&
        string.Equals(action.Name.ExtractText(), name, StringComparison.Ordinal) &&
        action.Icon == iconId &&
        action.IsPvP &&
        action.IsPlayerAction &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == SamuraiReactiveCounterCcRules.SamuraiJobId &&
        action.Range == range &&
        action.EffectRange == effectRange &&
        action.Cast100ms == 0 &&
        action.Recast100ms == recast100ms &&
        action.MaxCharges == maxCharges &&
        action.ActionCategory.IsValid &&
        action.ActionCategory.RowId == 4 &&
        action.CooldownGroup == 3 &&
        action.AdditionalCooldownGroup == 0 &&
        action.CanTargetHostile &&
        !action.CanTargetSelf &&
        !action.CanTargetParty &&
        !action.CanTargetAlly &&
        !action.TargetArea &&
        action.RequiresLineOfSight &&
        action.AffectsPosition == affectsPosition;

    private static bool ValidateCommonSelfAction(
        GameAction action,
        string name,
        uint iconId,
        ushort recast100ms) =>
        action.RowId != 0 &&
        string.Equals(action.Name.ExtractText(), name, StringComparison.Ordinal) &&
        action.Icon == iconId &&
        action.IsPvP &&
        action.IsPlayerAction &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == 0 &&
        action.Range == 0 &&
        action.EffectRange == 0 &&
        action.Cast100ms == 0 &&
        action.Recast100ms == recast100ms &&
        action.CanTargetSelf &&
        !action.CanTargetHostile &&
        !action.CanTargetParty &&
        !action.CanTargetAlly &&
        !action.TargetArea &&
        action.RequiresLineOfSight &&
        !action.AffectsPosition;

    private static bool ValidateProtectionStatus(
        ExcelSheet<Status> statuses,
        uint statusId,
        string name,
        uint iconId,
        string descriptionFragment) =>
        statuses.TryGetRow(statusId, out var status) &&
        status.RowId == statusId &&
        string.Equals(status.Name.ExtractText(), name, StringComparison.Ordinal) &&
        status.Icon == iconId &&
        status.StatusCategory == 1 &&
        !status.CanDispel &&
        !status.IsPermanent &&
        status.Description.ExtractText().Contains(
            descriptionFragment,
            StringComparison.Ordinal);
}
