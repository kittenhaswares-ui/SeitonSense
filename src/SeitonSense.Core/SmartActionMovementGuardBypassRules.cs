using System.Collections.ObjectModel;

namespace SeitonSense.Core;

/// <summary>
/// One current ordinary PvP action whose hostile-target execution moves the
/// local player. Limit breaks, ground-target movement, self-only dashes, and
/// actions that move only the enemy are deliberately absent.
/// </summary>
public sealed record SmartActionMovementGuardBypassDefinition(
    uint JobId,
    uint ActionId,
    string DisplayName);

/// <summary>
/// Closed current-PvP catalog for ordinary hostile-target gap closers and
/// disengages which must remain targetable through Guard. This says nothing
/// about damage bypass: Chiten, Cover, and invulnerability remain independent
/// protection decisions.
/// </summary>
public static class SmartActionMovementGuardBypassRules
{
    public const uint ForkedRaijuActionId = 29_510;
    public const uint FleetingRaijuActionId = 29_707;

    private static readonly SmartActionMovementGuardBypassDefinition[] DefinitionArray =
    [
        new(19, 29_065, "Intervene"),
        new(21, 29_079, "Onslaught"),
        new(21, 29_084, "Primal Rend"),
        new(32, 29_092, "Plunge"),
        new(37, 29_123, "Rough Divide"),
        new(24, 29_229, "Seraph Strike"),
        new(40, 29_261, "Icarus"),
        new(23, 29_399, "Repelling Shot"),
        new(20, 29_484, "Thunderclap"),
        new(22, 29_493, "High Jump"),
        new(34, 29_532, "Hissatsu: Soten"),
        new(25, 29_660, "Aetherial Manipulation"),
        new(27, 29_667, "Crimson Cyclone"),
        new(35, 29_699, "Corps-a-corps"),
        new(35, 29_700, "Displacement"),
        new(41, 39_184, "Slither"),
    ];

    private static readonly ReadOnlyCollection<SmartActionMovementGuardBypassDefinition>
        ReadOnlyDefinitions = Array.AsReadOnly(DefinitionArray);

    private static readonly Dictionary<uint, SmartActionMovementGuardBypassDefinition>
        DefinitionsByAction = DefinitionArray.ToDictionary(
            static definition => definition.ActionId);

    public static IReadOnlyList<SmartActionMovementGuardBypassDefinition> Definitions =>
        ReadOnlyDefinitions;

    public static bool IsReviewedAction(uint actionId) =>
        DefinitionsByAction.ContainsKey(actionId);

    public static bool IsReviewedAction(uint jobId, uint actionId) =>
        DefinitionsByAction.TryGetValue(actionId, out var definition) &&
        definition.JobId == jobId;

    public static bool IsGuardStatus(uint statusId) =>
        statusId is SmartActionProtectionRules.GuardStatusId or
            SmartActionProtectionRules.GuardLargeScaleStatusId;

    /// <summary>
    /// Both Raiju variants move the local player, but their defining hostile
    /// effect is Stun. They remain categorically blocked against Guard even if
    /// future localized action text were to match a generic Guard-bypass rule.
    /// </summary>
    public static bool IsGuardBlockedCcMovement(uint resolvedActionId) =>
        resolvedActionId is ForkedRaijuActionId or FleetingRaijuActionId;

    public static bool AllowsGuardTarget(uint jobId, uint actionId) =>
        !IsGuardBlockedCcMovement(actionId) &&
        IsReviewedAction(jobId, actionId);
}
