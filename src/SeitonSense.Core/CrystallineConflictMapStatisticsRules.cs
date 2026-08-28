using System.Globalization;

namespace SeitonSense.Core;

public readonly record struct CrystallineConflictMapParticipantIdentity(
    ulong ContentId,
    byte ClassJobId,
    byte Team);

public readonly record struct ConfirmedCrystallineConflictMapResult(
    CrystallineConflictArena Arena,
    bool IsWin);

public readonly record struct CrystallineConflictMapWinLossSnapshot(
    long Wins,
    long Losses)
{
    public bool IsValid => Wins >= 0 && Losses >= 0 && Wins <= long.MaxValue - Losses;
    public long Matches => IsValid ? Wins + Losses : 0;
    public bool HasData => IsValid && Matches > 0;
    public double WinRate => HasData ? Wins / (double)Matches : 0d;
}

/// <summary>
/// Pure validation and presentation rules for locally observed personal
/// Crystalline Conflict map results. A result is confirmed only when the
/// player-relative post-match packet, public arena territory, and complete
/// ten-player identity set agree. Missing or ambiguous evidence records
/// nothing.
/// </summary>
public static class CrystallineConflictMapStatisticsRules
{
    public const int ExpectedParticipantCount = 10;
    public const int ExpectedParticipantsPerTeam = 5;
    public const int MinimumMatchDurationSeconds = 10;
    public const int MaximumMatchDurationSeconds = 1_800;

    public static bool IsExactLiveCaptureContext(
        bool capturedIsPvpExcludingWolvesDen,
        uint capturedTerritoryId,
        ulong capturedLocalContentId,
        bool liveIsPvpExcludingWolvesDen,
        uint liveTerritoryId,
        ulong liveLocalContentId) =>
        capturedIsPvpExcludingWolvesDen &&
        liveIsPvpExcludingWolvesDen &&
        capturedTerritoryId == liveTerritoryId &&
        capturedLocalContentId != 0 &&
        capturedLocalContentId == liveLocalContentId &&
        TryResolvePublicArena(capturedTerritoryId, out _);

    public static bool IsExactFrameworkDrainBoundary(
        long capturedResetGeneration,
        long liveResetGeneration,
        bool capturedIsPvpExcludingWolvesDen,
        uint capturedTerritoryId,
        ulong capturedLocalContentId,
        bool liveIsPvpExcludingWolvesDen,
        uint liveTerritoryId,
        ulong liveLocalContentId) =>
        capturedResetGeneration == liveResetGeneration &&
        IsExactLiveCaptureContext(
            capturedIsPvpExcludingWolvesDen,
            capturedTerritoryId,
            capturedLocalContentId,
            liveIsPvpExcludingWolvesDen,
            liveTerritoryId,
            liveLocalContentId);

    public static bool TryResolvePublicArena(
        uint territoryId,
        out CrystallineConflictArena arena)
    {
        arena = territoryId switch
        {
            1032 => CrystallineConflictArena.ThePalaistra,
            1033 => CrystallineConflictArena.TheVolcanicHeart,
            1034 => CrystallineConflictArena.CloudNine,
            1116 => CrystallineConflictArena.TheClockworkCastletown,
            1138 => CrystallineConflictArena.TheRedSands,
            1293 => CrystallineConflictArena.TheBaysideBattleground,
            1357 => CrystallineConflictArena.ArcheiaHarmonias,
            _ => default,
        };
        return PvPMatchRules.IsPublicCrystallineConflictTerritory(territoryId);
    }

    public static bool TryConfirmResult(
        bool isPvpExcludingWolvesDen,
        uint territoryId,
        byte result,
        int durationSeconds,
        ulong localContentId,
        IReadOnlyList<CrystallineConflictMapParticipantIdentity>? participants,
        out ConfirmedCrystallineConflictMapResult confirmed)
    {
        confirmed = default;
        if (!isPvpExcludingWolvesDen ||
            !TryResolvePublicArena(territoryId, out var arena) ||
            result is not (1 or 2) ||
            durationSeconds is < MinimumMatchDurationSeconds or > MaximumMatchDurationSeconds ||
            localContentId == 0 ||
            participants is null ||
            participants.Count != ExpectedParticipantCount)
        {
            return false;
        }

        var uniqueContentIds = new HashSet<ulong>();
        var localMatches = 0;
        var astraCount = 0;
        var umbraCount = 0;
        foreach (var participant in participants)
        {
            if (participant.ContentId == 0 ||
                !PvpRangeHelperRules.TryGetProfile(participant.ClassJobId, out _) ||
                participant.Team > 1 ||
                !uniqueContentIds.Add(participant.ContentId))
            {
                return false;
            }

            if (participant.ContentId == localContentId) localMatches++;
            if (participant.Team == 0) astraCount++;
            else umbraCount++;
        }

        if (localMatches != 1 ||
            astraCount != ExpectedParticipantsPerTeam ||
            umbraCount != ExpectedParticipantsPerTeam)
        {
            return false;
        }

        // For the non-spectator result packet, 1 is victory and 2 is defeat.
        // The exact local ContentId proof above excludes a spectated result.
        confirmed = new ConfirmedCrystallineConflictMapResult(arena, result == 1);
        return true;
    }

    public static bool TryCreateSnapshot(
        long wins,
        long losses,
        out CrystallineConflictMapWinLossSnapshot snapshot)
    {
        snapshot = default;
        if (wins < 0 || losses < 0 || wins > long.MaxValue - losses) return false;
        snapshot = new CrystallineConflictMapWinLossSnapshot(wins, losses);
        return true;
    }

    public static string FormatRecord(CrystallineConflictMapWinLossSnapshot snapshot) =>
        snapshot.HasData
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{snapshot.Wins}W  ·  {snapshot.Losses}L")
            : "NO DATA";

    public static string FormatWinRate(CrystallineConflictMapWinLossSnapshot snapshot) =>
        snapshot.HasData
            ? string.Create(CultureInfo.InvariantCulture, $"{snapshot.WinRate * 100d:0.0}%")
            : string.Empty;
}
