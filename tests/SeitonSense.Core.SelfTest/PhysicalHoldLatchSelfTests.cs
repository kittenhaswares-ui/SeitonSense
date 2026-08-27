using SeitonSense.Core;

internal static class PhysicalHoldLatchSelfTests
{
    private static readonly RawPhysicalChord PlainOne = new(
        PhysicalKey: 0x31,
        ChordFingerprint: 0x31);

    private static readonly RawPhysicalChord ShiftOne = new(
        PhysicalKey: 0x31,
        ChordFingerprint: 0x0001_0031);

    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("physical latch certifies one fresh raw press", FreshRawPressCertifiesOnce);
        yield return ("logical gaps and typematic retain one physical identity", LogicalGapsAndTypematicKeepIdentity);
        yield return ("raw key-up is the only release authority", RawKeyUpIsReleaseAuthority);
        yield return ("modifier drift cannot forge another press", ModifierChangesFailClosed);
        yield return ("a different key cannot replace a latched hold", DifferentPhysicalKeyFailsClosed);
        yield return ("an already-held key needs release before certification", AlreadyHeldKeyNeedsRelease);
    }

    private static void FreshRawPressCertifiesOnce()
    {
        var latch = new PhysicalHoldLatch();
        var fresh = latch.Observe(Fresh(PlainOne));

        Equal(PhysicalHoldDecisionKind.Fresh, fresh.Kind);
        Equal(1L, fresh.PressId);
        True(fresh.StartsNewPress);
        False(fresh.SuppressDuplicateStart);
        False(fresh.PreserveCurrentDeadline);
        True(latch.Snapshot.HasCertifiedHold);
        Equal(PlainOne, latch.Snapshot.Chord);
    }

    private static void LogicalGapsAndTypematicKeepIdentity()
    {
        var latch = new PhysicalHoldLatch();
        Equal(1L, latch.Observe(Fresh(PlainOne)).PressId);

        for (var iteration = 0; iteration < 20; iteration++)
        {
            var logicalGap = latch.Observe(new PhysicalHoldObservation(
                PlainOne,
                LogicalPressed: false,
                LogicalDown: false,
                RawPressed: false,
                RawDown: true));
            Equal(PhysicalHoldDecisionKind.HeldContinuation, logicalGap.Kind);
            Equal(1L, logicalGap.PressId);
            True(logicalGap.SuppressDuplicateStart);
            True(logicalGap.PreserveCurrentDeadline);

            var typematic = latch.Observe(new PhysicalHoldObservation(
                PlainOne,
                LogicalPressed: true,
                LogicalDown: true,
                RawPressed: true,
                RawDown: true));
            Equal(PhysicalHoldDecisionKind.HeldContinuation, typematic.Kind);
            Equal(1L, typematic.PressId);
            False(typematic.StartsNewPress);
        }
    }

    private static void RawKeyUpIsReleaseAuthority()
    {
        var latch = new PhysicalHoldLatch();
        Equal(1L, latch.Observe(Fresh(PlainOne)).PressId);

        var logicalReleaseOnly = latch.Observe(new PhysicalHoldObservation(
            PlainOne,
            LogicalPressed: false,
            LogicalDown: false,
            RawPressed: false,
            RawDown: true));
        Equal(PhysicalHoldDecisionKind.HeldContinuation, logicalReleaseOnly.Kind);
        True(latch.Snapshot.HasCertifiedHold);

        var rawRelease = latch.Observe(new PhysicalHoldObservation(
            PlainOne,
            LogicalPressed: false,
            LogicalDown: true,
            RawPressed: false,
            RawDown: false));
        Equal(PhysicalHoldDecisionKind.Released, rawRelease.Kind);
        Equal(1L, rawRelease.PressId);
        False(latch.Snapshot.HasCertifiedHold);

        Equal(2L, latch.Observe(Fresh(PlainOne)).PressId);
    }

    private static void ModifierChangesFailClosed()
    {
        var latch = new PhysicalHoldLatch();
        Equal(1L, latch.Observe(Fresh(PlainOne)).PressId);

        var changed = latch.Observe(Fresh(ShiftOne));
        Equal(PhysicalHoldDecisionKind.Untrusted, changed.Kind);
        Equal(1L, changed.PressId);
        Equal(PlainOne, latch.Snapshot.Chord);

        var rawReleaseWithChangedModifiers = latch.Observe(new PhysicalHoldObservation(
            ShiftOne,
            LogicalPressed: false,
            LogicalDown: false,
            RawPressed: false,
            RawDown: false));
        Equal(PhysicalHoldDecisionKind.Released, rawReleaseWithChangedModifiers.Kind);
        Equal(2L, latch.Observe(Fresh(ShiftOne)).PressId);
    }

    private static void DifferentPhysicalKeyFailsClosed()
    {
        var latch = new PhysicalHoldLatch();
        Equal(1L, latch.Observe(Fresh(PlainOne)).PressId);
        var other = new RawPhysicalChord(PhysicalKey: 0x32, ChordFingerprint: 0x32);

        var replacement = latch.Observe(Fresh(other));

        Equal(PhysicalHoldDecisionKind.Untrusted, replacement.Kind);
        Equal(1L, replacement.PressId);
        Equal(PlainOne, latch.Snapshot.Chord);
    }

    private static void AlreadyHeldKeyNeedsRelease()
    {
        var latch = new PhysicalHoldLatch();
        var firstSeenHeld = latch.Observe(new PhysicalHoldObservation(
            PlainOne,
            LogicalPressed: false,
            LogicalDown: true,
            RawPressed: false,
            RawDown: true));
        Equal(PhysicalHoldDecisionKind.Untrusted, firstSeenHeld.Kind);
        Equal(PhysicalHoldLatchState.NeedsRelease, latch.Snapshot.State);

        Equal(PhysicalHoldDecisionKind.Untrusted, latch.Observe(Fresh(PlainOne)).Kind);
        Equal(
            PhysicalHoldDecisionKind.Untrusted,
            latch.ObserveRequiredRelease(logicalPressed: true, rawDown: true).Kind);
        Equal(
            PhysicalHoldDecisionKind.Released,
            latch.ObserveRequiredRelease(logicalPressed: false, rawDown: false).Kind);
        Equal(1L, latch.Observe(Fresh(PlainOne)).PressId);
    }

    private static PhysicalHoldObservation Fresh(RawPhysicalChord chord) => new(
        chord,
        LogicalPressed: true,
        LogicalDown: true,
        RawPressed: true,
        RawDown: true);

    private static void True(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected true, got false.");
    }

    private static void False(bool condition) => True(!condition);

    private static void Equal<T>(T expected, T actual)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}; got {actual}.");
        }
    }
}
