using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Patch-guarded read of the local CC content director fields documented by
/// the MIT-licensed PvpStats reference. Every field is plausibility checked;
/// drift returns unavailable and contributes nothing to the prediction.
/// </summary>
internal static unsafe class CrystallineConflictPredictionDirectorReader
{
    internal static bool TryReadTeamProgress(
        uint localEntityId,
        out int ownProgressTenthsPercent,
        out int enemyProgressTenthsPercent)
    {
        ownProgressTenthsPercent = 0;
        enemyProgressTenthsPercent = 0;
        if (localEntityId is 0 or 0xE0000000u) return false;

        try
        {
            var framework = EventFramework.Instance();
            var instance = framework == null
                ? null
                : framework->GetInstanceContentDirector();
            if (instance == null) return false;

            var director = (PredictionContentDirector*)instance;
            if (director->Players == null ||
                director->AstraProgress is < 0 or >
                    CrystallineConflictPredictionRules.MaximumProgressTenthsPercent ||
                director->UmbraProgress is < 0 or >
                    CrystallineConflictPredictionRules.MaximumProgressTenthsPercent)
            {
                return false;
            }

            var astra = 0;
            var umbra = 0;
            var uniqueEntities = new HashSet<uint>();
            byte? localTeam = null;
            for (var index = 0; index < 10; index++)
            {
                var player = director->Players[index];
                if (player.EntityId is 0 or 0xE0000000u ||
                    player.Team > 1 ||
                    !PvpRangeHelperRules.TryGetProfile(player.ClassJobId, out _) ||
                    !uniqueEntities.Add(player.EntityId))
                {
                    return false;
                }

                if (player.Team == 0) astra++;
                else umbra++;
                if (player.EntityId == localEntityId) localTeam = player.Team;
            }

            if (astra != 5 || umbra != 5 || localTeam is null) return false;
            ownProgressTenthsPercent = localTeam.Value == 0
                ? director->AstraProgress
                : director->UmbraProgress;
            enemyProgressTenthsPercent = localTeam.Value == 0
                ? director->UmbraProgress
                : director->AstraProgress;
            return true;
        }
        catch
        {
            return false;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PredictionContentDirector
    {
        private const int Offset = 0x1F90;

        [FieldOffset(Offset + 0x058)] public PredictionPlayer* Players;
        [FieldOffset(Offset + 0x1AC)] public int AstraProgress;
        [FieldOffset(Offset + 0x1B0)] public int UmbraProgress;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x138)]
    private struct PredictionPlayer
    {
        [FieldOffset(0xE0)] public uint EntityId;
        [FieldOffset(0xE8)] public byte Team;
        [FieldOffset(0xE9)] public byte ClassJobId;
    }
}
