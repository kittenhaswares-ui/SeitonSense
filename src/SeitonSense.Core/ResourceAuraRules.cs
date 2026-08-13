namespace SeitonSense.Core;

[Flags]
public enum ResourceAuraKind : byte
{
    None = 0,
    LowHp = 1,
    LowMp = 2,
    LowHpAndMp = LowHp | LowMp,
}

public readonly record struct ResourceAuraObservation(
    uint CurrentHp,
    uint MaximumHp,
    int CurrentMp,
    int MaximumMp,
    bool MpTrusted,
    bool LowMpLatched,
    bool Alive);

public static class ResourceAuraRules
{
    public static ResourceAuraKind Resolve(
        ResourceAuraObservation observation,
        int hpPercentThreshold,
        int mpThreshold)
    {
        if (!observation.Alive ||
            observation.CurrentHp == 0 ||
            observation.MaximumHp == 0 ||
            observation.CurrentHp > observation.MaximumHp ||
            observation.CurrentMp < 0 ||
            observation.MaximumMp < 0 ||
            observation.CurrentMp > observation.MaximumMp ||
            hpPercentThreshold is < 1 or > 100 ||
            mpThreshold < 0)
        {
            return ResourceAuraKind.None;
        }

        var lowHp = (ulong)observation.CurrentHp * 100UL <=
                    (ulong)observation.MaximumHp * (uint)hpPercentThreshold;
        var lowMp = observation.MpTrusted && observation.LowMpLatched &&
                    observation.MaximumMp > 0 &&
                    observation.CurrentMp <= observation.MaximumMp;

        return (lowHp, lowMp) switch
        {
            (true, true) => ResourceAuraKind.LowHpAndMp,
            (true, false) => ResourceAuraKind.LowHp,
            (false, true) => ResourceAuraKind.LowMp,
            _ => ResourceAuraKind.None,
        };
    }
}
