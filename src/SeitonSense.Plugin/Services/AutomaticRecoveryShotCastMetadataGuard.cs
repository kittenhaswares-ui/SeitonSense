using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using SeitonSense.Core;
using GameAction = Lumina.Excel.Sheets.Action;

namespace SeitonSense.Plugin.Services;

internal sealed record AutomaticRecoveryShotCastMetadataValidation(
    bool BardPowerfulShotVerified,
    bool MachinistBlastChargeVerified)
{
    internal static AutomaticRecoveryShotCastMetadataValidation None { get; } =
        new(false, false);

    internal int VerifiedCount =>
        (BardPowerfulShotVerified ? 1 : 0) +
        (MachinistBlastChargeVerified ? 1 : 0);

    internal bool IsVerified(uint jobId, uint actionId) =>
        AutomaticRecoveryShotCastRules.IsExactAllowedPair(jobId, actionId) &&
        actionId switch
        {
            AutomaticRecoveryShotCastRules.BardPowerfulShotActionId =>
                BardPowerfulShotVerified,
            AutomaticRecoveryShotCastRules.MachinistBlastChargeActionId =>
                MachinistBlastChargeVerified,
            _ => false,
        };
}

/// <summary>
/// Pins the two automatic-recovery cast-cancel pairs to exact English current
/// PvP Action rows. A missing, unreadable, or drifted row disables that pair;
/// a sheet-level failure disables both.
/// </summary>
internal static class AutomaticRecoveryShotCastMetadataGuard
{
    private const byte ExpectedActionCategoryId = 3; // Weaponskill
    private const ushort ExpectedCast100ms = 15;
    private const ushort ExpectedRecast100ms = 25;
    private const byte ExpectedCooldownGroup = 58;
    private const byte ExpectedAdditionalCooldownGroup = 0;
    private const byte ExpectedRange = 25;
    private const byte ExpectedEffectRange = 0;
    private const byte ExpectedCastType = 1;

    private static readonly ActionExpectation[] Expectations =
    [
        new(
            AutomaticRecoveryShotCastRules.BardJobId,
            AutomaticRecoveryShotCastRules.BardPowerfulShotActionId,
            "Powerful Shot",
            9_625),
        new(
            AutomaticRecoveryShotCastRules.MachinistJobId,
            AutomaticRecoveryShotCastRules.MachinistBlastChargeActionId,
            "Blast Charge",
            9_630),
    ];

    internal static AutomaticRecoveryShotCastMetadataValidation Validate(
        IDataManager dataManager,
        IPluginLog log)
    {
        try
        {
            var actions = dataManager.GetExcelSheet<GameAction>(ClientLanguage.English);
            var verified = new HashSet<uint>();

            foreach (var definition in AutomaticRecoveryShotCastRules.Definitions)
            {
                var expected = Expectations.FirstOrDefault(item =>
                    item.JobId == definition.JobId &&
                    item.ActionId == definition.RawActionId &&
                    string.Equals(
                        item.Name,
                        definition.DisplayName,
                        StringComparison.Ordinal));
                var valid = expected is not null &&
                            actions.TryGetRow(definition.RawActionId, out var action) &&
                            ValidateAction(action, expected);
                if (valid)
                {
                    verified.Add(definition.RawActionId);
                    continue;
                }

                log.Warning(
                    "Seiton Sense disabled unverified automatic-recovery shot cast " +
                    "{ActionId} ({Name}) for job {JobId}.",
                    definition.RawActionId,
                    definition.DisplayName,
                    definition.JobId);
            }

            var result = new AutomaticRecoveryShotCastMetadataValidation(
                verified.Contains(
                    AutomaticRecoveryShotCastRules.BardPowerfulShotActionId),
                verified.Contains(
                    AutomaticRecoveryShotCastRules.MachinistBlastChargeActionId));
            log.Information(
                "Seiton Sense verified automatic-recovery shot cast metadata: " +
                "{Verified}/{Total} actions.",
                result.VerifiedCount,
                AutomaticRecoveryShotCastRules.Definitions.Count);
            return result;
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense could not validate automatic-recovery shot cast " +
                "metadata; both cast-cancel pairs stay disabled.");
            return AutomaticRecoveryShotCastMetadataValidation.None;
        }
    }

    private static bool ValidateAction(
        GameAction action,
        ActionExpectation expected) =>
        action.RowId == expected.ActionId &&
        string.Equals(
            action.Name.ExtractText(),
            expected.Name,
            StringComparison.Ordinal) &&
        action.Icon == expected.IconId &&
        action.IsPvP &&
        action.IsPlayerAction &&
        action.ClassJob.IsValid &&
        action.ClassJob.RowId == expected.JobId &&
        action.ActionCategory.IsValid &&
        action.ActionCategory.RowId == ExpectedActionCategoryId &&
        action.Cast100ms == ExpectedCast100ms &&
        action.Recast100ms == ExpectedRecast100ms &&
        action.CooldownGroup == ExpectedCooldownGroup &&
        action.AdditionalCooldownGroup == ExpectedAdditionalCooldownGroup &&
        action.Range == ExpectedRange &&
        action.EffectRange == ExpectedEffectRange &&
        action.CanTargetHostile &&
        !action.CanTargetSelf &&
        !action.CanTargetParty &&
        !action.CanTargetAlly &&
        !action.CanTargetAlliance &&
        !action.CanTargetOwnPet &&
        !action.CanTargetPartyPet &&
        !action.TargetArea &&
        action.RequiresLineOfSight &&
        action.CastType == ExpectedCastType;

    private sealed record ActionExpectation(
        uint JobId,
        uint ActionId,
        string Name,
        uint IconId);
}
