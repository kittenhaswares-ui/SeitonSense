using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SeitonSense.Plugin.Services;

// Patch 7.5 / Dalamud API 15 post-match interoperability boundary. Only the
// numeric fields needed to confirm a personal map result are read. See
// THIRD_PARTY_NOTICES.md.
[StructLayout(LayoutKind.Explicit)]
internal unsafe struct CrystallineConflictMapResultPacket
{
    [FieldOffset(0x10)] public ushort MatchLength;
    [FieldOffset(0x3C)] public byte Result;
    [FieldOffset(0x40)] public uint AstraProgress;
    [FieldOffset(0x44)] public uint UmbraProgress;
    [FieldOffset(0x48)] public fixed byte Players[0x50 * 10];

    public Span<CrystallineConflictMapResultPlayer> PlayerSpan =>
        new(Unsafe.AsPointer(ref Players[0]), 10);
}

[StructLayout(LayoutKind.Explicit, Size = 0x50)]
internal struct CrystallineConflictMapResultPlayer
{
    [FieldOffset(0x08)] public ulong ContentId;
    [FieldOffset(0x10)] public int DamageDealt;
    [FieldOffset(0x14)] public int DamageTaken;
    [FieldOffset(0x18)] public int HpRestored;
    [FieldOffset(0x1E)] public byte ClassJobId;
    [FieldOffset(0x1F)] public byte Kills;
    [FieldOffset(0x20)] public byte Deaths;
    [FieldOffset(0x21)] public byte Assists;
    [FieldOffset(0x22)] public ushort TimeOnCrystal;
    [FieldOffset(0x25)] public byte Team;
}
