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
    public const string ExactEnglishGuardReductionSentence =
        "Halves the defensive bonus of Guard instead when targeting enemies under its effect.";

    public static bool HasExactEnglishDescription(string? description) =>
        !string.IsNullOrEmpty(description) &&
        description.Contains(
            ExactEnglishDescriptionSentence,
            StringComparison.Ordinal);

    /// <summary>
    /// Exact current wording shared by PLD Shield Smite and SCH Chain
    /// Stratagem. Runtime still requires each full action row to pass its own
    /// strict metadata proof before this text can grant Guard targeting.
    /// </summary>
    public static bool HasExactEnglishGuardReductionDescription(string? description) =>
        !string.IsNullOrEmpty(description) &&
        description.Contains(
            ExactEnglishGuardReductionSentence,
            StringComparison.Ordinal);
}
