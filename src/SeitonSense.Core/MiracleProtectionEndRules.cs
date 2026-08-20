namespace SeitonSense.Core;

/// <summary>
/// Narrow consent retained only for verified post-Purify and post-Guard
/// protection-end episodes. The token is supplied by the shared emergency
/// input coordinator; this policy never discovers keys from raw key levels.
/// </summary>
public readonly record struct MiracleProtectionEndHeldConsentState(int GameplayKeyToken)
{
    public static MiracleProtectionEndHeldConsentState Initial => new(0);

    public bool IsLatched => GameplayKeyToken > 0;
}

public readonly record struct MiracleProtectionEndHeldConsentObservation(
    bool Enabled,
    bool IsTextInputActive,
    int UnconsumedEligibleGameplayKeyToken,
    bool LatchedKeyPhysicallyDown,
    bool HardReset = false);

/// <summary>
/// Immutable comparison values captured from one exact, currently
/// release-ready protection-end actor. A known team-pressure value of zero is
/// valid. An unknown sample remains eligible but ranks after every known one.
/// </summary>
public readonly record struct MiracleProtectionEndRankCandidate(
    MiracleInterceptThreatKind Threat,
    int EnemySlot,
    ulong GameObjectId,
    uint EntityId,
    uint JobId,
    bool TeamTargetCountKnown,
    int TeamTargetCount,
    uint CurrentHp,
    uint MaximumHp,
    bool HasTrustedMp,
    uint CurrentMp,
    uint MaximumMp)
{
    public bool IsValid =>
        Threat is MiracleInterceptThreatKind.PostPurifyCrowdControl or
            MiracleInterceptThreatKind.PostGuardCrowdControl &&
        EnemySlotRules.IsValidSlot(EnemySlot) &&
        TargetHighlightRules.IsValidGameObjectId(GameObjectId) &&
        MiracleGuardFollowupRules.IsValidEntityId(EntityId) &&
        JobId != 0 &&
        (!TeamTargetCountKnown || TeamTargetCount >= 0) &&
        CurrentHp > 0 &&
        MaximumHp >= CurrentHp &&
        (!HasTrustedMp ||
         (MaximumMp == CombatFrameRules.ExpectedMaximumMp && CurrentMp <= MaximumMp));
}

public static class MiracleProtectionEndRules
{
    public static bool DispatchConsumesHeldConsent(MiracleInterceptThreatKind threat) =>
        threat is MiracleInterceptThreatKind.MarksmanSpite or
            MiracleInterceptThreatKind.Zantetsuken or
            MiracleInterceptThreatKind.FuriousBacklash or
            MiracleInterceptThreatKind.Contradance;

    public static MiracleProtectionEndHeldConsentState ObserveHeldConsent(
        MiracleProtectionEndHeldConsentState previous,
        MiracleProtectionEndHeldConsentObservation observation)
    {
        if (observation.HardReset ||
            !observation.Enabled ||
            observation.IsTextInputActive)
        {
            return MiracleProtectionEndHeldConsentState.Initial;
        }

        if (previous.IsLatched && observation.LatchedKeyPhysicallyDown)
            return previous;

        return observation.UnconsumedEligibleGameplayKeyToken > 0
            ? new MiracleProtectionEndHeldConsentState(
                observation.UnconsumedEligibleGameplayKeyToken)
            : MiracleProtectionEndHeldConsentState.Initial;
    }

    /// <summary>
    /// Returns a negative value when <paramref name="left"/> ranks first.
    /// Pressure is descending; HP and trusted MP ratios are ascending; a known
    /// MP sample ranks before an unknown one. Exact slot/IDs close every tie.
    /// </summary>
    public static int Compare(
        MiracleProtectionEndRankCandidate left,
        MiracleProtectionEndRankCandidate right)
    {
        if (!left.IsValid) return right.IsValid ? 1 : 0;
        if (!right.IsValid) return -1;

        var pressureTrust = right.TeamTargetCountKnown.CompareTo(left.TeamTargetCountKnown);
        if (pressureTrust != 0) return pressureTrust;
        if (left.TeamTargetCountKnown)
        {
            var pressure = right.TeamTargetCount.CompareTo(left.TeamTargetCount);
            if (pressure != 0) return pressure;
        }

        var hpRatio = CompareRatio(
            left.CurrentHp,
            left.MaximumHp,
            right.CurrentHp,
            right.MaximumHp);
        if (hpRatio != 0) return hpRatio;

        var mpTrust = right.HasTrustedMp.CompareTo(left.HasTrustedMp);
        if (mpTrust != 0) return mpTrust;
        if (left.HasTrustedMp)
        {
            var mpRatio = CompareRatio(
                left.CurrentMp,
                left.MaximumMp,
                right.CurrentMp,
                right.MaximumMp);
            if (mpRatio != 0) return mpRatio;
        }

        var slot = left.EnemySlot.CompareTo(right.EnemySlot);
        if (slot != 0) return slot;
        var entity = left.EntityId.CompareTo(right.EntityId);
        if (entity != 0) return entity;
        var gameObject = left.GameObjectId.CompareTo(right.GameObjectId);
        if (gameObject != 0) return gameObject;
        var job = left.JobId.CompareTo(right.JobId);
        if (job != 0) return job;
        return left.Threat.CompareTo(right.Threat);
    }

    public static int SelectBestIndex(
        IReadOnlyList<MiracleProtectionEndRankCandidate>? candidates)
    {
        if (candidates is null || candidates.Count == 0) return -1;
        var selected = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            if (!candidates[index].IsValid) continue;
            if (selected < 0 || Compare(candidates[index], candidates[selected]) < 0)
                selected = index;
        }

        return selected;
    }

    private static int CompareRatio(
        uint leftCurrent,
        uint leftMaximum,
        uint rightCurrent,
        uint rightMaximum) =>
        ((UInt128)leftCurrent * rightMaximum).CompareTo(
            (UInt128)rightCurrent * leftMaximum);
}
