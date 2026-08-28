using SeitonSense.Core;

internal static class CrystallineConflictMapStatisticsSelfTests
{
    public static void EveryPublicArenaMapsExactlyAndPrivateContextsFailClosed()
    {
        var expected = new Dictionary<uint, CrystallineConflictArena>
        {
            [1032] = CrystallineConflictArena.ThePalaistra,
            [1033] = CrystallineConflictArena.TheVolcanicHeart,
            [1034] = CrystallineConflictArena.CloudNine,
            [1116] = CrystallineConflictArena.TheClockworkCastletown,
            [1138] = CrystallineConflictArena.TheRedSands,
            [1293] = CrystallineConflictArena.TheBaysideBattleground,
            [1357] = CrystallineConflictArena.ArcheiaHarmonias,
        };

        foreach (var pair in expected)
        {
            True(
                CrystallineConflictMapStatisticsRules.TryResolvePublicArena(pair.Key, out var arena),
                $"public arena {pair.Key}");
            Equal(pair.Value, arena, $"public arena identity {pair.Key}");
        }

        foreach (var territory in new uint[] { 250, 1058, 1059, 1060, 1117, 1139, 1294, 1358, 0 })
            False(
                CrystallineConflictMapStatisticsRules.TryResolvePublicArena(territory, out _),
                $"private or unknown territory {territory}");
    }

    public static void ExactCompleteLocalResultIsConfirmed()
    {
        var participants = CompleteParticipants();
        True(
            CrystallineConflictMapStatisticsRules.TryConfirmResult(
                true,
                1293,
                1,
                355,
                1003,
                participants,
                out var win),
            "complete public win");
        Equal(CrystallineConflictArena.TheBaysideBattleground, win.Arena, "captured arena");
        True(win.IsWin, "result one is local victory");

        True(
            CrystallineConflictMapStatisticsRules.TryConfirmResult(
                true,
                1293,
                2,
                355,
                1003,
                participants,
                out var loss),
            "complete public loss");
        False(loss.IsWin, "result two is local defeat");
    }

    public static void ContextResultAndDurationGatesFailClosed()
    {
        var participants = CompleteParticipants();
        True(
            CrystallineConflictMapStatisticsRules.IsExactLiveCaptureContext(
                true,
                1293,
                1003,
                true,
                1293,
                1003),
            "frozen and live public-match context agrees exactly");
        True(
            CrystallineConflictMapStatisticsRules.IsExactFrameworkDrainBoundary(
                4,
                4,
                true,
                1293,
                1003,
                true,
                1293,
                1003),
            "same reset generation may drain");
        False(
            CrystallineConflictMapStatisticsRules.IsExactFrameworkDrainBoundary(
                4,
                5,
                true,
                1293,
                1003,
                true,
                1293,
                1003),
            "pre-reset capture cannot repopulate cleared storage");
        False(
            CrystallineConflictMapStatisticsRules.IsExactLiveCaptureContext(
                false,
                1293,
                1003,
                true,
                1293,
                1003),
            "captured PvP context absent");
        False(
            CrystallineConflictMapStatisticsRules.IsExactLiveCaptureContext(
                true,
                1293,
                1003,
                false,
                1293,
                1003),
            "live PvP context drift");
        False(
            CrystallineConflictMapStatisticsRules.IsExactLiveCaptureContext(
                true,
                1293,
                1003,
                true,
                1032,
                1003),
            "territory drift");
        False(
            CrystallineConflictMapStatisticsRules.IsExactLiveCaptureContext(
                true,
                1293,
                1003,
                true,
                1293,
                2003),
            "local identity drift");
        False(
            CrystallineConflictMapStatisticsRules.IsExactLiveCaptureContext(
                true,
                250,
                1003,
                true,
                250,
                1003),
            "matching Wolves Den context is still private");
        False(Confirm(false, 1293, 1, 355, 1003, participants), "not PvP excluding Den");
        False(Confirm(true, 1294, 1, 355, 1003, participants), "custom territory");
        False(Confirm(true, 250, 1, 355, 1003, participants), "Wolves Den territory");
        False(Confirm(true, 1293, 0, 355, 1003, participants), "unknown result zero");
        False(Confirm(true, 1293, 3, 355, 1003, participants), "unknown result three");
        False(Confirm(true, 1293, 1, 9, 1003, participants), "duration below minimum");
        False(Confirm(true, 1293, 1, 1801, 1003, participants), "duration above maximum");
        True(Confirm(true, 1293, 1, 10, 1003, participants), "minimum duration inclusive");
        True(Confirm(true, 1293, 1, 1800, 1003, participants), "maximum duration inclusive");
        False(Confirm(true, 1293, 1, 355, 0, participants), "zero local ContentId");
    }

    public static void ParticipantIdentityAndTeamsMustBeExact()
    {
        var participants = CompleteParticipants();
        False(Confirm(true, 1293, 1, 355, 1003, participants[..9]), "only nine players");
        False(Confirm(true, 1293, 1, 355, 9999, participants), "local player absent");

        var duplicate = participants.ToArray();
        duplicate[9] = duplicate[0];
        False(Confirm(true, 1293, 1, 355, 1003, duplicate), "duplicate ContentId");

        var zero = participants.ToArray();
        zero[4] = zero[4] with { ContentId = 0 };
        False(Confirm(true, 1293, 1, 355, 1003, zero), "zero participant ContentId");

        var noJob = participants.ToArray();
        noJob[4] = noJob[4] with { ClassJobId = 0 };
        False(Confirm(true, 1293, 1, 355, 1003, noJob), "missing participant job");

        var unknownJob = participants.ToArray();
        unknownJob[4] = unknownJob[4] with { ClassJobId = 99 };
        False(Confirm(true, 1293, 1, 355, 1003, unknownJob), "unknown participant job");

        var invalidTeam = participants.ToArray();
        invalidTeam[4] = invalidTeam[4] with { Team = 2 };
        False(Confirm(true, 1293, 1, 355, 1003, invalidTeam), "invalid team");

        var unbalanced = participants.ToArray();
        unbalanced[4] = unbalanced[4] with { Team = 1 };
        False(Confirm(true, 1293, 1, 355, 1003, unbalanced), "teams are not five and five");
    }

    public static void WinLossFormattingIsHonestAndInvariant()
    {
        True(CrystallineConflictMapStatisticsRules.TryCreateSnapshot(12, 8, out var stats), "valid stats");
        Equal(20L, stats.Matches, "match count");
        Equal("12W  ·  8L", CrystallineConflictMapStatisticsRules.FormatRecord(stats), "record text");
        Equal("60.0%", CrystallineConflictMapStatisticsRules.FormatWinRate(stats), "win rate text");

        True(CrystallineConflictMapStatisticsRules.TryCreateSnapshot(0, 0, out var empty), "empty is valid");
        Equal("NO DATA", CrystallineConflictMapStatisticsRules.FormatRecord(empty), "empty is not fake zero percent");
        Equal(string.Empty, CrystallineConflictMapStatisticsRules.FormatWinRate(empty), "empty has no rate");
    }

    public static void InvalidOrOverflowingCountersFailClosed()
    {
        False(CrystallineConflictMapStatisticsRules.TryCreateSnapshot(-1, 0, out _), "negative wins");
        False(CrystallineConflictMapStatisticsRules.TryCreateSnapshot(0, -1, out _), "negative losses");
        False(
            CrystallineConflictMapStatisticsRules.TryCreateSnapshot(long.MaxValue, 1, out _),
            "overflowing total");
        var forged = new CrystallineConflictMapWinLossSnapshot(long.MaxValue, 1);
        False(forged.IsValid, "forged overflow snapshot invalid");
        Equal("NO DATA", CrystallineConflictMapStatisticsRules.FormatRecord(forged), "forged overflow is not rendered");
    }

    private static CrystallineConflictMapParticipantIdentity[] CompleteParticipants() =>
    [
        new(1001, 19, 0),
        new(1002, 21, 0),
        new(1003, 35, 0),
        new(1004, 24, 0),
        new(1005, 30, 0),
        new(2001, 32, 1),
        new(2002, 23, 1),
        new(2003, 25, 1),
        new(2004, 34, 1),
        new(2005, 39, 1),
    ];

    private static bool Confirm(
        bool isPvpExcludingWolvesDen,
        uint territoryId,
        byte result,
        int durationSeconds,
        ulong localContentId,
        IReadOnlyList<CrystallineConflictMapParticipantIdentity>? participants) =>
        CrystallineConflictMapStatisticsRules.TryConfirmResult(
            isPvpExcludingWolvesDen,
            territoryId,
            result,
            durationSeconds,
            localContentId,
            participants,
            out _);

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
