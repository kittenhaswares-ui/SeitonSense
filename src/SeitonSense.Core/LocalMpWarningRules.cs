namespace SeitonSense.Core;

[Flags]
public enum LocalMpWarningEdge : byte
{
    None = 0,
    FourThousand = 1 << 0,
    TwoThousand = 1 << 1,
}

public readonly record struct LocalMpWarningState(
    bool HasContinuousTrustedSample,
    uint LastTrustedMp,
    bool FourThousandArmed,
    bool TwoThousandArmed)
{
    public static LocalMpWarningState Initial => new(
        false,
        0,
        true,
        true);
}

public readonly record struct LocalMpWarningDecision(
    LocalMpWarningState NextState,
    LocalMpWarningEdge Edges)
{
    public LocalMpWarningEdge MostSevereEdge =>
        HasEdge(LocalMpWarningEdge.TwoThousand)
            ? LocalMpWarningEdge.TwoThousand
            : HasEdge(LocalMpWarningEdge.FourThousand)
                ? LocalMpWarningEdge.FourThousand
                : LocalMpWarningEdge.None;

    public bool HasEdge(LocalMpWarningEdge edge) =>
        edge != LocalMpWarningEdge.None &&
        (Edges & edge) == edge;
}

/// <summary>
/// Pure local-player MP edge detector. The first trustworthy observation only
/// establishes a baseline; subsequent continuous downward crossings emit one
/// edge each. Independent 300-MP recovery hysteresis prevents threshold jitter
/// from rearming either sound.
/// </summary>
public static class LocalMpWarningRules
{
    public const uint FourThousandThreshold = 4_000;
    public const uint TwoThousandThreshold = 2_000;
    public const uint RecoveryHysteresis = 300;
    public const uint FourThousandRearmThreshold =
        FourThousandThreshold + RecoveryHysteresis;
    public const uint TwoThousandRearmThreshold =
        TwoThousandThreshold + RecoveryHysteresis;

    public static LocalMpWarningDecision Observe(
        LocalMpWarningState state,
        uint currentMp,
        uint maximumMp,
        bool telemetryTrusted,
        bool localPlayerAlive,
        bool hardReset = false)
    {
        if (hardReset || !localPlayerAlive)
        {
            return new LocalMpWarningDecision(
                LocalMpWarningState.Initial,
                LocalMpWarningEdge.None);
        }

        if (!telemetryTrusted ||
            maximumMp != CombatFrameRules.ExpectedMaximumMp ||
            currentMp > maximumMp)
        {
            return new LocalMpWarningDecision(
                state with { HasContinuousTrustedSample = false },
                LocalMpWarningEdge.None);
        }

        var fourThousandArmed = state.FourThousandArmed;
        var twoThousandArmed = state.TwoThousandArmed;
        if (currentMp >= FourThousandRearmThreshold)
            fourThousandArmed = true;
        if (currentMp >= TwoThousandRearmThreshold)
            twoThousandArmed = true;

        if (!state.HasContinuousTrustedSample)
        {
            if (currentMp <= FourThousandThreshold)
                fourThousandArmed = false;
            if (currentMp <= TwoThousandThreshold)
                twoThousandArmed = false;

            return new LocalMpWarningDecision(
                new LocalMpWarningState(
                    true,
                    currentMp,
                    fourThousandArmed,
                    twoThousandArmed),
                LocalMpWarningEdge.None);
        }

        var edges = LocalMpWarningEdge.None;
        if (fourThousandArmed &&
            state.LastTrustedMp > FourThousandThreshold &&
            currentMp <= FourThousandThreshold)
        {
            edges |= LocalMpWarningEdge.FourThousand;
            fourThousandArmed = false;
        }

        if (twoThousandArmed &&
            state.LastTrustedMp > TwoThousandThreshold &&
            currentMp <= TwoThousandThreshold)
        {
            edges |= LocalMpWarningEdge.TwoThousand;
            twoThousandArmed = false;
        }

        return new LocalMpWarningDecision(
            new LocalMpWarningState(
                true,
                currentMp,
                fourThousandArmed,
                twoThousandArmed),
            edges);
    }
}
