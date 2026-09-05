using System.Globalization;
using Dalamud.Game;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Constructs only the reviewed Guardian Quick Chat command. Names come from
/// the closed language whitelist, never from player names or arbitrary text.
/// Construction and unit tests do not prove live client parsing or delivery.
/// </summary>
internal static class GuardianQuickChatCommand
{
    internal const string FormatLabel = "action-first-v2";

    internal static string? Build(ClientLanguage language, int partySlot)
    {
        if (partySlot is < 1 or > 8) return null;

        var localizedActionName = language switch
        {
            ClientLanguage.English => "Covering Target",
            ClientLanguage.German => "Ziel decken",
            ClientLanguage.French => "Soutien : cible",
            ClientLanguage.Japanese => "援護：ターゲット",
            _ => null,
        };
        if (localizedActionName is null) return null;

        // Use the common command name and one action-first shape in every
        // language. The caller must revalidate this frozen party slot against
        // the accepted Guardian target immediately before the single dispatch.
        return $"/quickchat \"{localizedActionName}\" <{partySlot.ToString(CultureInfo.InvariantCulture)}>";
    }
}
