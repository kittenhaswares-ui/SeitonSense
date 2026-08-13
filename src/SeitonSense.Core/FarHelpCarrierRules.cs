namespace SeitonSense.Core;

/// <summary>
/// Distinguishes an authored party-slot carrier such as &lt;2&gt; from the
/// player's own compact target. Mixed EntityId/GameObjectId forms are accepted
/// only for the exact resolved carrier actor.
/// </summary>
public static class FarHelpCarrierRules
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
        objectId is not 0 and not InvalidObjectId and not ulong.MaxValue;
}
