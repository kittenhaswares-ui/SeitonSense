using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace SeitonSense.Core;

public enum CombatLimitBreakPresentationKind : byte
{
    Instant = 0,
    Duration = 1,
}

[Flags]
public enum CombatLimitBreakActionRole : byte
{
    None = 0,
    Activation = 1 << 0,
    Damage = 1 << 1,
    FollowUp = 1 << 2,
}

public enum CombatLimitBreakDamageAttribution : byte
{
    None = 0,
    DirectCaster = 1,
    PetOwnerRequired = 2,
    PeriodicOwnerRequired = 3,
}

public enum CombatLimitBreakStatusCarrier : byte
{
    Caster = 0,
    Target = 1,
}

public readonly record struct CombatLimitBreakActionBinding(
    uint ActionId,
    CombatLimitBreakActionRole Role,
    CombatLimitBreakDamageAttribution DamageAttribution,
    uint IconIdOverride = 0);

/// <summary>
/// A status that can prove a live LB phase after the exact activation action.
/// Duplicate sheet rows are retained only where the installed game data does
/// not encode which same-named row the action applies. Runtime observation then
/// selects the row that actually appears; no countdown is fabricated.
/// </summary>
public readonly record struct CombatLimitBreakStatusBinding(
    uint StatusId,
    CombatLimitBreakStatusCarrier Carrier,
    bool RequireCasterAsSource,
    string Phase);

public sealed record CombatLimitBreakDefinition(
    uint JobId,
    string JobAbbreviation,
    string Name,
    uint IconId,
    ushort GaugeChargeSeconds,
    CombatLimitBreakPresentationKind Presentation,
    ImmutableArray<CombatLimitBreakActionBinding> Actions,
    ImmutableArray<CombatLimitBreakStatusBinding> Statuses)
{
    public ImmutableArray<uint> ActivationActionIds =>
        Actions
            .Where(static action => (action.Role & CombatLimitBreakActionRole.Activation) != 0)
            .Select(static action => action.ActionId)
            .ToImmutableArray();
}

/// <summary>
/// Patch 7.55 PvP LB catalog pinned to the installed 2026.08.11 game data.
/// Action IDs/icons and official charge times are source-verifiable. Status
/// associations are treated as runtime evidence only: if no mapped status is
/// observed on the exact carrier after activation, the UI must remain a flash.
/// </summary>
public static class CombatLimitBreakCatalog
{
    public const long InstantFlashMilliseconds = 1_800;

    public static ImmutableArray<CombatLimitBreakDefinition> Definitions { get; } =
    [
        Definition(
            19, "PLD", "Phalanx", 9_586, 135, CombatLimitBreakPresentationKind.Duration,
            [
                Activation(29_069),
                FollowUpDamage(29_071),
                FollowUpDamage(29_072),
                FollowUpDamage(29_073),
            ],
            [CasterStatus(1_302, "Hallowed Ground"), CasterStatus(3_250, "Blade of Faith Ready")]),
        Definition(
            21, "WAR", "Primal Scream", 9_592, 90, CombatLimitBreakPresentationKind.Duration,
            [Activation(29_083), FollowUpDamage(41_433)],
            [
                CasterStatus(1_303, "Inner Release"),
                CasterStatus(3_185, "Thrill of Battle"),
                CasterStatus(4_287, "Burgeoning Fury"),
                CasterStatus(3_833, "Burgeoning Fury (legacy row candidate)"),
                CasterStatus(4_286, "Wrathful"),
                CasterStatus(3_901, "Wrathful (legacy row candidate)"),
            ]),
        Definition(
            32, "DRK", "Eventide", 9_597, 105, CombatLimitBreakPresentationKind.Duration,
            [ActivationDamage(29_097), FollowUpDamage(41_437)],
            [
                CasterStatus(3_039, "Undead Redemption"),
                CasterStatus(3_033, "Blackblood"),
                CasterStatus(4_290, "Scorn"),
                CasterStatus(3_837, "Scorn (legacy row candidate)"),
            ]),
        Definition(
            37, "GNB", "Relentless Rush", 9_603, 60, CombatLimitBreakPresentationKind.Duration,
            [
                Activation(29_130),
                FollowUpDamage(29_557),
                FollowUpDamage(29_131),
                FollowUpDamage(29_469),
            ],
            [CasterStatus(3_052, "Relentless Rush")]),
        Definition(
            24, "WHM", "Afflatus Purgation", 9_610, 60, CombatLimitBreakPresentationKind.Duration,
            [ActivationDamage(29_230)],
            [CasterStatus(2_037, "Temperance")]),
        Definition(
            28, "SCH", "Seraphism", 9_068, 90, CombatLimitBreakPresentationKind.Duration,
            [Activation(41_502), FollowUpDamage(41_500)],
            [CasterStatus(4_327, "Seraphism"), CasterStatus(3_094, "Recitation")]),
        Definition(
            33, "AST", "Celestial River", 9_621, 105, CombatLimitBreakPresentationKind.Duration,
            [Activation(29_255), FollowUpDamage(41_508)],
            [
                CasterStatus(3_105, "Celestial River"),
                CasterStatus(4_332, "Divining"),
                CasterStatus(3_893, "Divining (legacy row candidate)"),
            ]),
        Definition(
            40, "SGE", "Mesotes", 9_624, 120, CombatLimitBreakPresentationKind.Duration,
            [
                Activation(29_266),
                FollowUp(29_267, CombatLimitBreakDamageAttribution.PeriodicOwnerRequired),
            ],
            [CasterStatus(3_118, "Mesotes")]),
        Definition(
            20, "MNK", "Meteodrive", 9_646, 75, CombatLimitBreakPresentationKind.Duration,
            [ActivationDamage(29_485)],
            [TargetStatus(3_174, "Meteodrive")]),
        Definition(
            22, "DRG", "Sky High", 9_652, 90, CombatLimitBreakPresentationKind.Duration,
            [Activation(29_497), FollowUpDamage(29_498), FollowUpDamage(29_499)],
            [CasterStatus(3_180, "Sky High"), CasterStatus(3_181, "Sky Shatter")]),
        Definition(
            30, "NIN", "Seiton Tenchu", 9_661, 90, CombatLimitBreakPresentationKind.Duration,
            [ActivationDamage(29_515), FollowUpDamage(29_516)],
            [TargetStatus(3_191, "Death Link"), CasterStatus(3_192, "Unsealed Seiton Tenchu")]),
        Definition(
            34, "SAM", "Zantetsuken", 9_666, 120, CombatLimitBreakPresentationKind.Instant,
            [ActivationDamage(29_537)],
            []),
        Definition(
            39, "RPR", "Tenebrae Lemurum", 9_670, 60, CombatLimitBreakPresentationKind.Duration,
            [Activation(29_553)],
            [
                CasterStatus(2_863, "Enshrouded"),
                CasterStatus(2_593, "Enshrouded (legacy row candidate)"),
            ]),
        Definition(
            41, "VPR", "World-swallower", 9_731, 90, CombatLimitBreakPresentationKind.Duration,
            [ActivationDamage(39_190), FollowUpDamage(39_173)],
            [CasterStatus(4_094, "Reawakened")]),
        Definition(
            23, "BRD", "Final Fantasia", 9_629, 120, CombatLimitBreakPresentationKind.Duration,
            [Activation(29_401), FollowUpDamage(41_467)],
            [CasterStatus(3_144, "Final Fantasia"), CasterStatus(4_312, "Encore of Light Ready")]),
        Definition(
            31, "MCH", "Marksman's Spite", 9_636, 90, CombatLimitBreakPresentationKind.Instant,
            [ActivationDamage(29_415)],
            []),
        Definition(
            38, "DNC", "Contradance", 9_641, 90, CombatLimitBreakPresentationKind.Duration,
            [Activation(29_432)],
            [TargetStatus(3_024, "Seduced")]),
        Definition(
            25, "BLM", "Soul Resonance", 9_673, 60, CombatLimitBreakPresentationKind.Duration,
            [Activation(29_662), FollowUpDamage(41_480), FollowUpDamage(41_481)],
            [CasterStatus(3_222, "Soul Resonance"), CasterStatus(4_317, "Elemental Star")]),
        Definition(
            27, "SMN", "Summon Bahamut / Phoenix", 9_681, 90, CombatLimitBreakPresentationKind.Duration,
            [
                Activation(29_673),
                Activation(29_678, 9_683),
                FollowUpDamage(41_484),
                FollowUpDamage(41_485),
                PetDamage(29_675),
                PetDamage(29_676),
                PetDamage(29_681),
                FollowUp(29_680, CombatLimitBreakDamageAttribution.PetOwnerRequired),
            ],
            [CasterStatus(3_228, "Dreadwyrm Trance"), CasterStatus(3_229, "Firebird Trance")]),
        Definition(
            35, "RDM", "Southern Cross", 9_692, 90, CombatLimitBreakPresentationKind.Instant,
            [ActivationDamage(41_498)],
            []),
        Definition(
            42, "PCT", "Advent of Chocobastion", 9_757, 105, CombatLimitBreakPresentationKind.Duration,
            [Activation(39_215), FollowUpDamage(39_216), FollowUpDamage(39_217)],
            [CasterStatus(4_116, "Advent of Chocobastion"), CasterStatus(4_118, "Starstruck")]),
    ];

    private static readonly ImmutableDictionary<uint, CombatLimitBreakDefinition> ByJob =
        Definitions.ToImmutableDictionary(static definition => definition.JobId);

    private static readonly ImmutableDictionary<uint, (CombatLimitBreakDefinition Definition, CombatLimitBreakActionBinding Action)> ByAction =
        Definitions
            .SelectMany(static definition => definition.Actions.Select(action => (definition, action)))
            .ToImmutableDictionary(static pair => pair.action.ActionId, static pair => (pair.definition, pair.action));

    public static bool TryFindByJob(
        uint jobId,
        [NotNullWhen(true)] out CombatLimitBreakDefinition? definition) =>
        ByJob.TryGetValue(jobId, out definition);

    public static bool TryFindByAction(
        uint actionId,
        [NotNullWhen(true)] out CombatLimitBreakDefinition? definition,
        out CombatLimitBreakActionBinding action)
    {
        if (ByAction.TryGetValue(actionId, out var result))
        {
            definition = result.Definition;
            action = result.Action;
            return true;
        }

        definition = null;
        action = default;
        return false;
    }

    public static bool IsActivation(CombatLimitBreakActionBinding action) =>
        (action.Role & CombatLimitBreakActionRole.Activation) != 0;

    public static bool IsDirectlyAttributableDamage(CombatLimitBreakActionBinding action) =>
        (action.Role & CombatLimitBreakActionRole.Damage) != 0 &&
        action.DamageAttribution == CombatLimitBreakDamageAttribution.DirectCaster;

    public static uint ResolveIconId(
        CombatLimitBreakDefinition definition,
        CombatLimitBreakActionBinding action) =>
        action.IconIdOverride != 0 ? action.IconIdOverride : definition.IconId;

    private static CombatLimitBreakDefinition Definition(
        uint jobId,
        string jobAbbreviation,
        string name,
        uint iconId,
        ushort gaugeChargeSeconds,
        CombatLimitBreakPresentationKind presentation,
        CombatLimitBreakActionBinding[] actions,
        CombatLimitBreakStatusBinding[] statuses) =>
        new(
            jobId,
            jobAbbreviation,
            name,
            iconId,
            gaugeChargeSeconds,
            presentation,
            [.. actions],
            [.. statuses]);

    private static CombatLimitBreakActionBinding Activation(uint actionId) =>
        new(actionId, CombatLimitBreakActionRole.Activation, CombatLimitBreakDamageAttribution.None);

    private static CombatLimitBreakActionBinding Activation(uint actionId, uint iconIdOverride) =>
        new(
            actionId,
            CombatLimitBreakActionRole.Activation,
            CombatLimitBreakDamageAttribution.None,
            iconIdOverride);

    private static CombatLimitBreakActionBinding ActivationDamage(uint actionId) =>
        new(
            actionId,
            CombatLimitBreakActionRole.Activation | CombatLimitBreakActionRole.Damage,
            CombatLimitBreakDamageAttribution.DirectCaster);

    private static CombatLimitBreakActionBinding FollowUpDamage(uint actionId) =>
        new(
            actionId,
            CombatLimitBreakActionRole.FollowUp | CombatLimitBreakActionRole.Damage,
            CombatLimitBreakDamageAttribution.DirectCaster);

    private static CombatLimitBreakActionBinding PetDamage(uint actionId) =>
        new(
            actionId,
            CombatLimitBreakActionRole.FollowUp | CombatLimitBreakActionRole.Damage,
            CombatLimitBreakDamageAttribution.PetOwnerRequired);

    private static CombatLimitBreakActionBinding FollowUp(
        uint actionId,
        CombatLimitBreakDamageAttribution attribution) =>
        new(actionId, CombatLimitBreakActionRole.FollowUp, attribution);

    private static CombatLimitBreakStatusBinding CasterStatus(uint statusId, string phase) =>
        new(statusId, CombatLimitBreakStatusCarrier.Caster, false, phase);

    private static CombatLimitBreakStatusBinding TargetStatus(uint statusId, string phase) =>
        new(statusId, CombatLimitBreakStatusCarrier.Target, true, phase);
}
