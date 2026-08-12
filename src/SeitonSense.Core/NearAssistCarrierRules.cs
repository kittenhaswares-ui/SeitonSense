namespace SeitonSense.Core;

public static class NearAssistCarrierRules
{
    private const ulong InvalidObjectId = 0xE0000000UL;

    public static bool IsFallbackCarrier(
        ulong currentHardTargetId,
        ulong incomingTargetId,
        ulong carrierEnemyGameObjectId,
        uint carrierEnemyEntityId)
    {
        if (!IsActorIdentity(incomingTargetId)) return false;

        var incomingIsCarrier = incomingTargetId == carrierEnemyGameObjectId ||
                                incomingTargetId == carrierEnemyEntityId;
        if (!incomingIsCarrier) return false;

        var currentIsSameCarrierActor = currentHardTargetId == carrierEnemyGameObjectId ||
                                        currentHardTargetId == carrierEnemyEntityId;
        return !currentIsSameCarrierActor;
    }

    private static bool IsActorIdentity(ulong objectId) =>
        objectId is not 0 and not InvalidObjectId;
}
