using SeitonSense.Core;

internal static class CombatLimitBreakNotificationSelfTests
{
    internal static void SelfBannerRequiresExactFreshEvidence()
    {
        const long now = 10_000;
        var observation = new CombatLimitBreakSelfNotificationObservation(
            true,
            0,
            9_624,
            CombatLimitBreakPresentationKind.Duration,
            true,
            now - 1_000,
            now + 8_000,
            now);

        True(
            CombatLimitBreakNotificationRules.TryBuildSelfPlan(
                observation,
                now,
                out var duration),
            "confirmed self duration");
        True(duration.ShowCountdown, "confirmed duration countdown");
        Equal(8_000L, duration.RemainingMilliseconds, "exact duration remaining");

        False(TrySelf(observation with { IsSelf = false }, now), "ally is not self");
        False(TrySelf(observation with { Slot = 1 }, now), "self slot must be exact");
        False(TrySelf(observation with { IconId = 0 }, now), "missing icon");
        False(
            TrySelf(
                observation with
                {
                    SnapshotPublishedAtMilliseconds =
                        now - CombatLimitBreakNotificationRules.MaximumSnapshotAgeMilliseconds - 1,
                },
                now),
            "stale snapshot");

        var instant = observation with
        {
            Presentation = CombatLimitBreakPresentationKind.Instant,
            DurationConfirmed = false,
            ActivatedAtMilliseconds = now - 500,
            ExpiresAtMilliseconds = now + 1_300,
        };
        True(
            CombatLimitBreakNotificationRules.TryBuildSelfPlan(
                instant,
                now,
                out var flash),
            "bounded instant flash");
        False(flash.ShowCountdown, "instant has no invented countdown");
        False(
            TrySelf(instant with { ExpiresAtMilliseconds = now + 1_301 }, now),
            "overlong instant flash");
    }

    internal static void AllyDamageCardsRequireExactBoundedEvents()
    {
        const long now = 10_000;
        var observation = new CombatLimitBreakDamageNotificationObservation(
            new TargetPressureActorIdentity(100, 200),
            2,
            new TargetPressureActorIdentity(300, 400),
            3,
            9_610,
            38_250,
            now - 500,
            now + 2_500,
            now,
            7,
            8);

        True(
            CombatLimitBreakNotificationRules.TryBuildDamagePlan(
                observation,
                now,
                out var plan),
            "fresh exact ally damage event");
        Equal(2, plan.CasterPartySlot, "caster slot");
        Equal(3, plan.TargetEnemySlot, "target slot");
        Equal(38_250u, plan.Damage, "damage");
        Equal(2_500L, plan.RemainingMilliseconds, "remaining lifetime");

        False(TryDamage(observation with { Caster = default }, now), "invalid caster");
        False(TryDamage(observation with { Target = observation.Caster }, now), "same actor");
        False(TryDamage(observation with { CasterPartySlot = 0 }, now), "invalid party slot");
        False(TryDamage(observation with { TargetEnemySlot = 6 }, now), "invalid enemy slot");
        False(TryDamage(observation with { Damage = 0 }, now), "zero damage");
        False(TryDamage(observation with { EventToken = 0 }, now), "missing event token");
        False(
            TryDamage(observation with { ExpiresAtMilliseconds = now + 2_501 }, now),
            "overlong event lifetime");
    }

    internal static void NotificationLayoutStaysInsideSafeScreenLanes()
    {
        True(
            CombatLimitBreakNotificationRules.TryBuildSelfBannerRectangle(
                0f,
                0f,
                1_920f,
                1_080f,
                1f,
                out var self),
            "normal self banner layout");
        NearlyEqual(700f, self.Left, "self centered left");
        NearlyEqual(1_220f, self.Right, "self centered right");
        True(self.Bottom <= 1_080f * 0.45f, "self remains in top safe lane");

        True(
            CombatLimitBreakNotificationRules.TryBuildDamageCardRectangles(
                0f,
                0f,
                1_920f,
                1_080f,
                1f,
                3,
                out var cards),
            "three damage cards");
        Equal(3, cards.Length, "damage card count");
        for (var index = 0; index < cards.Length; index++)
        {
            True(cards[index].IsValid, $"card {index} valid");
            True(cards[index].Left >= 0f && cards[index].Right <= 1_920f, $"card {index} horizontal bounds");
            True(cards[index].Top >= 0f && cards[index].Bottom <= 1_080f, $"card {index} vertical bounds");
            if (index > 0) True(cards[index - 1].Bottom < cards[index].Top, $"card {index} gap");
        }

        False(
            CombatLimitBreakNotificationRules.TryBuildSelfBannerRectangle(
                0f,
                0f,
                200f,
                200f,
                1f,
                out _),
            "undersized viewport fails closed");
        False(
            CombatLimitBreakNotificationRules.TryBuildDamageCardRectangles(
                0f,
                0f,
                1_920f,
                1_080f,
                1f,
                4,
                out _),
            "too many cards fail closed");
    }

    private static bool TrySelf(
        in CombatLimitBreakSelfNotificationObservation observation,
        long now) =>
        CombatLimitBreakNotificationRules.TryBuildSelfPlan(observation, now, out _);

    private static bool TryDamage(
        in CombatLimitBreakDamageNotificationObservation observation,
        long now) =>
        CombatLimitBreakNotificationRules.TryBuildDamagePlan(observation, now, out _);

    private static void NearlyEqual(float expected, float actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001f)
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void Equal<T>(T expected, T actual, string message)
        where T : IEquatable<T>
    {
        if (!actual.Equals(expected))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException($"Expected true: {message}");
    }

    private static void False(bool value, string message)
    {
        if (value) throw new InvalidOperationException($"Expected false: {message}");
    }
}
