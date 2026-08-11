using System.Numerics;

namespace SeitonSense.Plugin.Services;

internal sealed record EnemyExecuteSnapshot(
    int Slot,
    Vector3 WorldPosition,
    uint CurrentHp,
    uint MaxHp)
{
    public string Label => $"S{Slot}";
    public int HpPercent => MaxHp == 0
        ? 0
        : (int)Math.Clamp(((ulong)CurrentHp * 100UL) / MaxHp, 0UL, 100UL);
}
