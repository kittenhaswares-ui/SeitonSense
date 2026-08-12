namespace SeitonSense.Core;

public static class NearHelpCarrierRules
{
    private const ulong InvalidObjectId = 0xE0000000UL;

    public static bool IsFallbackCarrier(
        ulong currentHardTargetId,
        ulong incomingTargetId,
        ulong carrierGameObjectId,
        uint carrierEntityId)
    {
        if (!IsActorIdentity(incomingTargetId)) return false;

        var incomingIsCarrier = incomingTargetId == carrierGameObjectId ||
                                incomingTargetId == carrierEntityId;
        if (!incomingIsCarrier) return false;

        var currentIsSameCarrierActor = currentHardTargetId == carrierGameObjectId ||
                                        currentHardTargetId == carrierEntityId;
        return !currentIsSameCarrierActor;
    }

    private static bool IsActorIdentity(ulong objectId) =>
        objectId is not 0 and not InvalidObjectId;
}
