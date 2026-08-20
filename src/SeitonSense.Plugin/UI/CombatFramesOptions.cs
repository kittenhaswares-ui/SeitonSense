namespace SeitonSense.Plugin.UI;

/// <summary>
/// Immutable integration surface for the standalone combat-frame renderer.
/// Screen coordinates are normalized centers so layout remains stable across
/// resolution changes. Configuration ownership stays outside the renderer.
/// </summary>
internal readonly record struct CombatFramesOptions(
    bool Enabled,
    bool PreviewEnabled,
    float EnemyScreenX,
    float EnemyScreenY,
    float SelfScreenX,
    float SelfScreenY,
    float Scale,
    float BackgroundOpacity,
    bool ShowNames,
    bool ShowExactValues,
    bool ShowStatuses,
    bool ShowPressure)
{
    internal static CombatFramesOptions Default => new(
        false,
        false,
        0.82f,
        0.48f,
        0.5f,
        0.78f,
        1f,
        0.92f,
        true,
        true,
        true,
        true);
}
