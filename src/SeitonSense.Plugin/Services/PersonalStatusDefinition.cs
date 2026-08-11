using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum PersonalStatusFeature
{
    Wildfire = 0,
    DeathWarrant = 1,
    Purify = 2,
}

internal sealed record PersonalStatusDefinition(
    uint StatusId,
    string Name,
    uint IconId,
    PersonalDebuffAlertKind AlertKind,
    PersonalStatusFeature RequiredFeature,
    bool CanTriggerPurifyBuffer);

internal static class PersonalStatusDefinitions
{
    internal static PersonalStatusDefinition Wildfire { get; } = new(
        EnemyCombatConstants.WildfireStatusId,
        "Wildfire",
        EnemyCombatConstants.WildfireStatusIconId,
        PersonalDebuffAlertKind.Warning,
        PersonalStatusFeature.Wildfire,
        false);

    internal static PersonalStatusDefinition DeathWarrant { get; } = new(
        EnemyCombatConstants.DeathWarrantStatusId,
        "Death Warrant",
        EnemyCombatConstants.DeathWarrantStatusIconId,
        PersonalDebuffAlertKind.Warning,
        PersonalStatusFeature.DeathWarrant,
        false);

    internal static PersonalStatusDefinition Stun { get; } = new(
        EnemyCombatConstants.PvPStunStatusId,
        "Stun",
        EnemyCombatConstants.StunStatusIconId,
        PersonalDebuffAlertKind.CleanseUrgent,
        PersonalStatusFeature.Purify,
        true);

    internal static PersonalStatusDefinition MiracleOfNature { get; } = new(
        EnemyCombatConstants.MiracleOfNatureStatusId,
        "Miracle of Nature",
        EnemyCombatConstants.MiracleOfNatureStatusIconId,
        PersonalDebuffAlertKind.CleanseUrgent,
        PersonalStatusFeature.Purify,
        true);

    internal static IReadOnlyList<PersonalStatusDefinition> All { get; } =
        [Wildfire, DeathWarrant, Stun, MiracleOfNature];

    internal static PersonalStatusDefinition? Find(uint statusId) =>
        statusId switch
        {
            EnemyCombatConstants.WildfireStatusId => Wildfire,
            EnemyCombatConstants.DeathWarrantStatusId => DeathWarrant,
            EnemyCombatConstants.PvPStunStatusId => Stun,
            EnemyCombatConstants.MiracleOfNatureStatusId => MiracleOfNature,
            _ => null,
        };

    internal static bool IsMetadataVerified(
        PersonalStatusDefinition definition,
        PvPMetadataValidation metadata) =>
        definition.RequiredFeature switch
        {
            PersonalStatusFeature.Wildfire => metadata.WildfireVerified,
            PersonalStatusFeature.DeathWarrant => metadata.DeathWarrantVerified,
            PersonalStatusFeature.Purify => metadata.PurifyVerified,
            _ => false,
        };
}
