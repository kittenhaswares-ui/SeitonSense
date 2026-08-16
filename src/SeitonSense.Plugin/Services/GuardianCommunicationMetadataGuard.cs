using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace SeitonSense.Plugin.Services;

internal readonly record struct GuardianCommunicationMetadataValidation(
    bool Verified,
    ClientLanguage Language);

internal static class GuardianCommunicationMetadataGuard
{
    internal const uint QuickChatRowId = 35;
    internal const int QuickChatIconId = 9964;
    internal const uint QuickChatAddonRowId = 11718;
    internal const uint QuickChatTransientRowId = 52;

    internal static GuardianCommunicationMetadataValidation Validate(
        IDataManager dataManager,
        ClientLanguage language,
        IPluginLog log)
    {
        try
        {
            var expectedName = language switch
            {
                ClientLanguage.English => "Covering Target",
                ClientLanguage.German => "Ziel decken",
                ClientLanguage.French => "Soutien : cible",
                ClientLanguage.Japanese => "援護：ターゲット",
                _ => null,
            };
            var sheet = dataManager.GetExcelSheet<QuickChat>(language);
            var verified = expectedName is not null &&
                           sheet.TryGetRow(QuickChatRowId, out var quickChat) &&
                           quickChat.RowId == QuickChatRowId &&
                           quickChat.NameAction.ToString() == expectedName &&
                           quickChat.Icon == QuickChatIconId &&
                           quickChat.Addon.RowId == QuickChatAddonRowId &&
                           quickChat.QuickChatTransient.RowId == QuickChatTransientRowId;
            if (!verified)
            {
                log.Warning(
                    "Seiton Sense Guardian communication metadata failed closed for {Language}; " +
                    "QuickChat row 35 no longer matches the reviewed command.",
                    language);
            }

            return new GuardianCommunicationMetadataValidation(verified, language);
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense Guardian communication metadata validation failed closed for {Language}.",
                language);
            return new GuardianCommunicationMetadataValidation(false, language);
        }
    }
}
