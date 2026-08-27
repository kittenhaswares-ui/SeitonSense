namespace SeitonSense.Core;

/// <summary>
/// Exact, language-pinned metadata sentence used to identify PvP actions whose
/// damage explicitly ignores Guard. Callers must resolve the current adjusted
/// action first and read its matching English ActionTransient row.
/// </summary>
public static class SmartActionGuardBypassRules
{
    public const string ExactEnglishDescriptionSentence =
        "Ignores the effects of Guard when dealing damage.";

    public static bool HasExactEnglishDescription(string? description) =>
        !string.IsNullOrEmpty(description) &&
        description.Contains(
            ExactEnglishDescriptionSentence,
            StringComparison.Ordinal);
}
