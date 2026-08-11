namespace SeitonSense.Core;

public readonly record struct WolvesDenOpponentCandidate(
    uint EntityId,
    ulong GameObjectId,
    bool MatchesNativeDuelEnemyId,
    bool HasValidAddress,
    bool IsPlayerCharacter,
    bool IsSelf,
    bool HasHostileFlag,
    bool IsTargetable);

public readonly record struct WolvesDenOpponentSlot(int Slot, uint EntityId);

public static class WolvesDenOpponentRules
{
    private const uint InvalidEntityId = 0xE0000000;

    public static WolvesDenOpponentSlot? ResolveSingleSlot(
        IReadOnlyList<WolvesDenOpponentCandidate> candidates)
    {
        WolvesDenOpponentCandidate match = default;
        var foundMatch = false;

        foreach (var candidate in candidates)
        {
            if (!IsEligible(candidate)) continue;
            if (foundMatch) return null;

            match = candidate;
            foundMatch = true;
        }

        return foundMatch
            ? new WolvesDenOpponentSlot(EnemySlotRules.FirstSlot, match.EntityId)
            : null;
    }

    public static bool IsEligible(WolvesDenOpponentCandidate candidate) =>
        candidate.EntityId is not 0 and not InvalidEntityId &&
        candidate.GameObjectId is not 0 and not InvalidEntityId &&
        candidate.MatchesNativeDuelEnemyId &&
        candidate.HasValidAddress &&
        candidate.IsPlayerCharacter &&
        !candidate.IsSelf &&
        candidate.HasHostileFlag &&
        candidate.IsTargetable;
}
