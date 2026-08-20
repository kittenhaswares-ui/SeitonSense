using SeitonSense.Core;

internal static class CombatFrameRulesSelfTests
{
    internal static void EnemyRowsStayFixedAndOrdered()
    {
        var rows = CombatFrameRules.BuildEnemyRows(
        [
            Enemy(5, 105, 205),
            Enemy(2, 102, 202),
        ]);

        Equal(5, rows.Length, "fixed enemy row count");
        for (var index = 0; index < rows.Length; index++)
            Equal(index + 1, rows[index].Slot, $"fixed slot {index + 1}");
        Equal(CombatFrameAvailability.Unknown, rows[0].Availability, "missing S1 stays unknown");
        Equal(CombatFrameAvailability.Alive, rows[1].Availability, "known S2 stays in S2");
        Equal(CombatFrameAvailability.Alive, rows[4].Availability, "known S5 stays in S5");
    }

    internal static void AmbiguousSlotsAndActorsFailClosed()
    {
        var rows = CombatFrameRules.BuildEnemyRows(
        [
            Enemy(1, 101, 201),
            Enemy(1, 111, 211),
            Enemy(2, 102, 202),
            Enemy(3, 102, 203),
            Enemy(4, 104, 204),
        ]);

        Equal(CombatFrameAvailability.Unknown, rows[0].Availability, "duplicate S1 is blank");
        Equal(CombatFrameAvailability.Unknown, rows[1].Availability, "shared GOID blanks first actor");
        Equal(CombatFrameAvailability.Unknown, rows[2].Availability, "shared GOID blanks second actor");
        Equal(CombatFrameAvailability.Alive, rows[3].Availability, "independent S4 survives");
    }

    internal static void DeadAndUnknownRowsRemainStable()
    {
        var dead = Enemy(3, 103, 203) with
        {
            CurrentHp = 0,
            IsDead = true,
            IsTargetable = false,
            IsCurrentTarget = true,
            TeamTargetCount = 4,
        };
        var untargetable = Enemy(4, 104, 204) with { IsTargetable = false };
        var uninitialized = Enemy(5, 105, 205) with
        {
            CurrentHp = 0,
            MaximumHp = 0,
            IsDead = false,
        };
        var rows = CombatFrameRules.BuildEnemyRows([dead, untargetable, uninitialized]);

        Equal(CombatFrameAvailability.Dead, rows[2].Availability, "dead S3 remains a dead row");
        False(rows[2].IsCurrentTarget, "dead row clears target accent");
        Equal(0, rows[2].TeamTargetCount, "dead row clears pressure claims");
        Equal(CombatFrameAvailability.Unknown, rows[3].Availability, "untargetable actor is unknown");
        Equal(4, rows[3].Slot, "unknown actor cannot collapse S4");
        Equal(
            CombatFrameAvailability.Unknown,
            rows[4].Availability,
            "uninitialized 0/0 HP is unknown, not dead");
    }

    internal static void ResourceTrustAndPipsAreExact()
    {
        var untrustedZero = Enemy(1, 101, 201) with
        {
            CurrentMp = 0,
            MaximumMp = 10_000,
            MpTrusted = false,
        };
        var exactCost = Enemy(2, 102, 202) with
        {
            CurrentMp = 2_000,
            MaximumMp = 10_000,
            MpTrusted = true,
        };
        var full = Enemy(3, 103, 203) with
        {
            CurrentMp = 10_000,
            MaximumMp = 10_000,
            MpTrusted = true,
        };
        var impossible = Enemy(4, 104, 204) with
        {
            CurrentMp = 10_001,
            MaximumMp = 10_000,
            MpTrusted = true,
        };
        var corruptMaximum = Enemy(5, 105, 205) with
        {
            CurrentMp = uint.MaxValue,
            MaximumMp = uint.MaxValue,
            MpTrusted = true,
        };
        var rows = CombatFrameRules.BuildEnemyRows(
            [untrustedZero, exactCost, full, impossible, corruptMaximum]);

        False(rows[0].HasTrustedMp, "initial zero stays unknown");
        Equal(-1, rows[0].AffordableRecuperates, "unknown MP has no pips");
        Equal(1, rows[1].AffordableRecuperates, "exactly 2000 affords one Recuperate");
        Equal(5, rows[2].AffordableRecuperates, "full MP affords five Recuperates");
        False(rows[3].HasTrustedMp, "impossible MP fails closed");
        Equal(0f, rows[3].MpFraction, "impossible MP has no fill");
        False(rows[4].HasTrustedMp, "non-PvP maximum MP fails closed");
        Equal(-1, rows[4].AffordableRecuperates, "corrupt maximum MP cannot create pips");
        Equal(
            -1,
            CombatFrameRules.AffordableRecuperates(uint.MaxValue, uint.MaxValue, true),
            "sentinel MP cannot escape the exact maximum gate");

        False(
            CombatFrameRules.AdvanceMpTrust(
                LowMpState.Initial,
                uint.MaxValue,
                uint.MaxValue,
                1_000,
                out var corruptState),
            "corrupt MP cannot prime trust");
        False(corruptState.HasTrustedSample, "corrupt MP clears the trust latch");
        False(
            CombatFrameRules.AdvanceMpTrust(
                corruptState,
                0,
                CombatFrameRules.ExpectedMaximumMp,
                1_001,
                out var zeroAfterCorrupt),
            "zero after corrupt MP stays unknown");
        True(
            CombatFrameRules.AdvanceMpTrust(
                zeroAfterCorrupt,
                8_000,
                CombatFrameRules.ExpectedMaximumMp,
                1_002,
                out var trustedState),
            "positive exact PvP MP establishes trust");
        True(
            CombatFrameRules.AdvanceMpTrust(
                trustedState,
                0,
                CombatFrameRules.ExpectedMaximumMp,
                1_003,
                out _),
            "zero after a trusted exact sample stays trusted");
    }

    internal static void SelfAndPresentationFlagsAreSanitized()
    {
        var self = CombatFrameRules.BuildSelfRow(Enemy(0, 100, 200) with
        {
            IsTargetable = false,
            DirectPressureCount = 99,
            TeamTargetCount = -5,
            IncomingEvidence =
                CombatFrameIncomingEvidence.HardTarget |
                (CombatFrameIncomingEvidence)(1 << 7),
        });

        Equal(CombatFrameAvailability.Alive, self.Availability, "self does not require targetability");
        Equal(5, self.DirectPressureCount, "direct pressure clamps to CC capacity");
        Equal(0, self.TeamTargetCount, "negative team pressure clears");
        Equal(CombatFrameIncomingEvidence.HardTarget, self.IncomingEvidence, "unknown evidence bits clear");
        var unknownPressure = CombatFrameRules.BuildSelfRow(Enemy(0, 100, 200) with
        {
            PressureTrusted = false,
            DirectPressureCount = 3,
            IncomingEvidence = CombatFrameIncomingEvidence.HardTarget,
        });
        False(unknownPressure.PressureTrusted, "unknown pressure stays explicit");
        Equal(0, unknownPressure.DirectPressureCount, "unknown pressure cannot claim a direct count");
        Equal(CombatFrameIncomingEvidence.None, unknownPressure.IncomingEvidence, "unknown pressure clears hints");
        Equal(
            CombatFrameAvailability.Unknown,
            CombatFrameRules.BuildSelfRow(Enemy(1, 101, 201)).Availability,
            "enemy slot cannot become self");
    }

    internal static void SnapshotFreshnessIsExact()
    {
        False(CombatFrameRules.IsSnapshotFresh(-1, 1_000), "negative publication is unknown");
        False(CombatFrameRules.IsSnapshotFresh(1_001, 1_000), "future publication is rejected");
        True(CombatFrameRules.IsSnapshotFresh(500, 1_000), "500 ms boundary is fresh");
        False(CombatFrameRules.IsSnapshotFresh(499, 1_000), "501 ms is stale");
    }

    private static CombatFrameObservation Enemy(int slot, ulong gameObjectId, uint entityId) => new(
        slot,
        new TargetPressureActorIdentity(gameObjectId, entityId),
        30,
        40_000,
        50_000,
        8_000,
        10_000,
        true,
        false,
        true,
        true,
        false,
        false,
        0,
        0,
        CombatFrameIncomingEvidence.None);

    private static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void False(bool condition, string message) => True(!condition, message);

    private static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
