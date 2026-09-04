namespace SeitonSense.Core;

public enum SmartActionTapOrigin : byte
{
    None,
    SmartAction,
    SeitonSam,
}

/// <summary>
/// The same immutable owner follows one authored macro through target
/// selection, a delayed Chase replay, and its optional Samurai cast shield.
/// </summary>
public readonly record struct SmartActionTapOwnership(
    SmartActionTapOrigin Origin,
    long Generation)
{
    public bool IsValid =>
        Generation > 0 && Origin is SmartActionTapOrigin.SmartAction or SmartActionTapOrigin.SeitonSam;

    public bool RequiresSmartActionProtection => IsValid;
    public bool RequiresSamuraiCastProtection => IsValid && Origin == SmartActionTapOrigin.SeitonSam;

    public static SmartActionTapOwnership Capture(long generation, bool samurai) =>
        generation > 0
            ? new(samurai ? SmartActionTapOrigin.SeitonSam : SmartActionTapOrigin.SmartAction, generation)
            : default;

    public bool MatchesGeneration(long generation) => IsValid && Generation == generation;
}
