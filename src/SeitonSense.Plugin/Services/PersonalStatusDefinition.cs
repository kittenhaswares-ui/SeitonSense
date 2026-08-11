using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum PersonalStatusFeature
{
    Wildfire = 0,
    DeathWarrant = 1,
    MarksmanSpite = 2,
    Purify = 3,
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

    // This is an incoming-action warning rather than a status-sheet row. It is
    // deliberately absent from Find(), which scans only the local StatusList.
    internal static PersonalStatusDefinition MarksmanSpite { get; } = new(
        EnemyCombatConstants.MarksmanSpiteActionId,
        "Marksman's Spite",
        EnemyCombatConstants.MarksmanSpiteIconId,
        PersonalDebuffAlertKind.Warning,
        PersonalStatusFeature.MarksmanSpite,
        false);

    internal static PersonalStatusDefinition Stun { get; } = new(
        EnemyCombatConstants.PvPStunStatusId,
        "Stun",
        EnemyCombatConstants.StunStatusIconId,
        PersonalDebuffAlertKind.CleanseUrgent,
        PersonalStatusFeature.Purify,
        true);

    internal static PersonalStatusDefinition Heavy { get; } = new(
        EnemyCombatConstants.PvPHeavyStatusId,
        "Heavy",
        EnemyCombatConstants.HeavyStatusIconId,
        PersonalDebuffAlertKind.CleanseUrgent,
        PersonalStatusFeature.Purify,
        true);

    internal static PersonalStatusDefinition Bind { get; } = new(
        EnemyCombatConstants.PvPBindStatusId,
        "Bind",
        EnemyCombatConstants.BindStatusIconId,
        PersonalDebuffAlertKind.CleanseUrgent,
        PersonalStatusFeature.Purify,
        true);

    internal static PersonalStatusDefinition Silence { get; } = new(
        EnemyCombatConstants.PvPSilenceStatusId,
        "Silence",
        EnemyCombatConstants.SilenceStatusIconId,
        PersonalDebuffAlertKind.CleanseUrgent,
        PersonalStatusFeature.Purify,
        true);

    internal static PersonalStatusDefinition DeepFreeze { get; } = new(
        EnemyCombatConstants.DeepFreezeStatusId,
        "Deep Freeze",
        EnemyCombatConstants.DeepFreezeStatusIconId,
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
        [MarksmanSpite, Wildfire, DeathWarrant, Stun, Heavy, Bind, Silence, DeepFreeze, MiracleOfNature];

    internal static PersonalStatusDefinition? Find(uint statusId) =>
        statusId switch
        {
            EnemyCombatConstants.WildfireStatusId => Wildfire,
            EnemyCombatConstants.DeathWarrantStatusId => DeathWarrant,
            EnemyCombatConstants.PvPStunStatusId => Stun,
            EnemyCombatConstants.PvPHeavyStatusId => Heavy,
            EnemyCombatConstants.PvPBindStatusId => Bind,
            EnemyCombatConstants.PvPSilenceStatusId => Silence,
            EnemyCombatConstants.DeepFreezeStatusId => DeepFreeze,
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
            PersonalStatusFeature.MarksmanSpite => metadata.MarksmanSpiteVerified,
            PersonalStatusFeature.Purify => metadata.PurifyVerified,
            _ => false,
        };
}
