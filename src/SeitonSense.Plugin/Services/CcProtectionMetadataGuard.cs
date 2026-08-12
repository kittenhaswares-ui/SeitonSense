using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal static class CcProtectionMetadataGuard
{
    internal static IReadOnlySet<uint> Validate(IDataManager dataManager, IPluginLog log)
    {
        var verified = new HashSet<uint>();
        try
        {
            var sheet = dataManager.GetExcelSheet<Status>(ClientLanguage.English);
            foreach (var definition in CcProtectionStatusCatalog.Definitions)
            {
                var row = sheet.GetRowOrDefault(definition.StatusId);
                var valid = row.HasValue &&
                            row.Value.Icon == definition.IconId &&
                            row.Value.StatusCategory == 1 &&
                            !row.Value.CanDispel &&
                            !row.Value.IsPermanent &&
                            string.Equals(
                                row.Value.Name.ExtractText(),
                                definition.Name,
                                StringComparison.Ordinal) &&
                            row.Value.Description.ExtractText().Contains(
                                definition.ExpectedDescriptionFragment,
                                StringComparison.Ordinal);
                if (valid)
                {
                    verified.Add(definition.StatusId);
                    continue;
                }

                log.Warning(
                    "Seiton Sense disabled unverified CC-protection status {StatusId} ({Name}).",
                    definition.StatusId,
                    definition.Name);
            }
        }
        catch (Exception exception)
        {
            log.Warning(exception, "Seiton Sense could not validate CC-protection metadata; protection icons stay off.");
            verified.Clear();
        }

        log.Information(
            "Seiton Sense verified {Verified}/{Total} CC-protection statuses.",
            verified.Count,
            CcProtectionStatusCatalog.Definitions.Count);
        return verified;
    }
}
