using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SeitonSense.Plugin.Services;

// Patch 7.5 / Dalamud API 15 post-match interoperability boundary. Only the
// bounded fields needed to confirm a personal result and key locally observed
// player history are read. See THIRD_PARTY_NOTICES.md.
[StructLayout(LayoutKind.Explicit, Size = 0x368)]
internal unsafe struct CrystallineConflictMapResultPacket
{
    internal const int PlayerCount = 10;
    internal const int PlayerRowSize = 0x50;

    [FieldOffset(0x10)] public ushort MatchLength;
    [FieldOffset(0x3C)] public byte Result;
    [FieldOffset(0x40)] public uint AstraProgress;
    [FieldOffset(0x44)] public uint UmbraProgress;
    [FieldOffset(0x48)] public fixed byte Players[PlayerRowSize * PlayerCount];

    public Span<CrystallineConflictMapResultPlayer> PlayerSpan =>
        new(Unsafe.AsPointer(ref Players[0]), PlayerCount);
}

[StructLayout(LayoutKind.Explicit, Size = 0x50)]
internal unsafe struct CrystallineConflictMapResultPlayer
{
    internal const int PlayerNameBufferLength = 42;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    [FieldOffset(0x08)] public ulong ContentId;
    [FieldOffset(0x10)] public int DamageDealt;
    [FieldOffset(0x14)] public int DamageTaken;
    [FieldOffset(0x18)] public int HpRestored;
    [FieldOffset(0x1C)] public ushort WorldId;
    [FieldOffset(0x1E)] public byte ClassJobId;
    [FieldOffset(0x1F)] public byte Kills;
    [FieldOffset(0x20)] public byte Deaths;
    [FieldOffset(0x21)] public byte Assists;
    [FieldOffset(0x22)] public ushort TimeOnCrystal;
    [FieldOffset(0x25)] public byte Team;
    [FieldOffset(0x26)] public fixed byte PlayerName[PlayerNameBufferLength];

    internal bool TryReadPlayerName(out string playerName)
    {
        playerName = string.Empty;
        fixed (byte* pointer = PlayerName)
        {
            var length = 0;
            while (length < PlayerNameBufferLength && pointer[length] != 0) length++;

            // Real character names are shorter than this fixed native field.
            // Missing termination or invalid UTF-8 is treated as an unreadable
            // identity instead of manufacturing a replacement-character key.
            if (length is 0 or PlayerNameBufferLength) return false;
            try
            {
                playerName = StrictUtf8.GetString(pointer, length);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }
    }
}
