namespace SeitonSense.Core;

public readonly record struct CombatLimitBreakStatusObservation(
    uint CarrierEntityId,
    uint StatusId,
    uint SourceEntityId,
    float RemainingSeconds);

public readonly record struct CombatLimitBreakDurationEvidence(
    uint StatusId,
    string Phase,
    long RemainingMilliseconds);

public readonly record struct CombatLimitBreakEventKey(
    uint CasterEntityId,
    uint ActionId,
    uint TargetEntityId,
    uint GlobalSequence,
    ushort SourceSequence);

public static class CombatLimitBreakEventRules
{
    public const byte MissEffectType = 1;
    public const byte FullResistEffectType = 2;
    public const byte DamageEffectType = 3;
    public const byte BlockedDamageEffectType = 5;
    public const byte ParriedDamageEffectType = 6;
    public const byte InvulnerableEffectType = 7;
    public const byte LargeValueFlag = 0x40;
    public const byte AppliedToSourceFlag = 0x80;
    public const long MaximumTrackedDurationMilliseconds = 3_600_000;

    /// <summary>
    /// Decodes the native 24-bit damage amount. Source-applied effects are
    /// rejected because the packet target would otherwise be attributed to the
    /// wrong actor. Invulnerability is represented by a non-damage effect or a
    /// zero amount; no additional undocumented Param4 bits are filtered.
    /// </summary>
    public static bool TryDecodeDirectDamage(
        byte effectType,
        byte param3,
        byte param4,
        ushort value,
        out uint damage)
    {
        damage = 0;
        if (effectType is not (DamageEffectType or BlockedDamageEffectType or ParriedDamageEffectType) ||
            (param4 & AppliedToSourceFlag) != 0 ||
            (param4 & LargeValueFlag) == 0 && param3 != 0)
        {
            return false;
        }

        damage = value;
        if ((param4 & LargeValueFlag) != 0) damage += (uint)param3 << 16;
        return damage > 0;
    }

    public static bool TryResolveDuration(
        CombatLimitBreakDefinition definition,
        uint casterEntityId,
        IEnumerable<CombatLimitBreakStatusObservation>? observations,
        out CombatLimitBreakDurationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(definition);
        evidence = default;
        if (!IsNetworkEntityId(casterEntityId) ||
            definition.Presentation != CombatLimitBreakPresentationKind.Duration ||
            definition.Statuses.IsDefaultOrEmpty ||
            observations is null)
        {
            return false;
        }

        var found = false;
        foreach (var observation in observations)
        {
            if (!IsNetworkEntityId(observation.CarrierEntityId) ||
                !float.IsFinite(observation.RemainingSeconds) ||
                observation.RemainingSeconds <= 0f)
            {
                continue;
            }

            foreach (var binding in definition.Statuses)
            {
                if (binding.StatusId != observation.StatusId ||
                    !CarrierMatches(binding, casterEntityId, observation))
                {
                    continue;
                }

                var remaining = Math.Clamp(
                    (long)Math.Ceiling(observation.RemainingSeconds * 1000d),
                    1,
                    MaximumTrackedDurationMilliseconds);
                if (!found ||
                    remaining > evidence.RemainingMilliseconds ||
                    remaining == evidence.RemainingMilliseconds && binding.StatusId < evidence.StatusId)
                {
                    found = true;
                    evidence = new CombatLimitBreakDurationEvidence(
                        binding.StatusId,
                        binding.Phase,
                        remaining);
                }
            }
        }

        return found;
    }

    public static bool IsNetworkEntityId(uint entityId) =>
        entityId is not 0 and not 0xE0000000u;

    private static bool CarrierMatches(
        CombatLimitBreakStatusBinding binding,
        uint casterEntityId,
        CombatLimitBreakStatusObservation observation) =>
        binding.Carrier switch
        {
            CombatLimitBreakStatusCarrier.Caster =>
                observation.CarrierEntityId == casterEntityId &&
                (!binding.RequireCasterAsSource || observation.SourceEntityId == casterEntityId),
            CombatLimitBreakStatusCarrier.Target =>
                observation.CarrierEntityId != casterEntityId &&
                binding.RequireCasterAsSource &&
                observation.SourceEntityId == casterEntityId,
            _ => false,
        };
}
