namespace SeitonSense.Core;

public enum SmartActionProtectionStatusSemantic : byte
{
    Guard = 1,
    Covered = 2,
    HallowedGround = 3,
    UndeadRedemption = 4,
}

public readonly record struct SmartActionProtectionStatusDefinition(
    uint StatusId,
    SmartActionProtectionStatusSemantic Semantic);

/// <summary>
/// Current exact Status-sheet identities for the non-Chiten protections used by
/// Smart Action. Multiple rows may carry the same meaning and row IDs may move
/// between game-data revisions. Every meaning remains mandatory, while an old
/// duplicate row may disappear without disabling unrelated targeting.
/// </summary>
public sealed class SmartActionProtectionStatusCatalog
{
    private readonly IReadOnlyDictionary<uint, SmartActionProtectionKind> entries;
    private readonly uint verifiedWeakenedGuardStatusId;

    private SmartActionProtectionStatusCatalog(
        IReadOnlyDictionary<uint, SmartActionProtectionKind> entries,
        bool isVerified,
        uint verifiedWeakenedGuardStatusId = 0)
    {
        this.entries = entries;
        IsVerified = isVerified;
        this.verifiedWeakenedGuardStatusId = verifiedWeakenedGuardStatusId;
    }

    public static SmartActionProtectionStatusCatalog Empty { get; } =
        new(new Dictionary<uint, SmartActionProtectionKind>(), isVerified: false);

    public bool IsVerified { get; }

    public int Count => entries.Count;

    public SmartActionProtectionKind Classify(uint statusId) =>
        IsWeakenedGuardStatus(statusId)
            ? SmartActionProtectionKind.None
            : entries.TryGetValue(statusId, out var kind)
            ? kind
            : SmartActionProtectionKind.None;

    public bool IsWeakenedGuardStatus(uint statusId) =>
        statusId != 0 && statusId == verifiedWeakenedGuardStatusId;

    // This modifies damage targeting only. CC immunity is independently read
    // from the unchanged CC status catalog, including status 3673.
    public SmartActionProtectionStatusCatalog WithVerifiedWeakenedGuard(uint statusId) =>
        IsVerified && statusId == SmartActionProtectionRules.WeakenedGuardStatusId &&
        entries.TryGetValue(statusId, out var kind) && kind == SmartActionProtectionKind.Guard
            ? new SmartActionProtectionStatusCatalog(entries, true, statusId)
            : this;

    public static SmartActionProtectionStatusCatalog Create(
        IEnumerable<SmartActionProtectionStatusDefinition>? definitions)
    {
        if (definitions is null) return Empty;

        var entries = new Dictionary<uint, SmartActionProtectionKind>();
        var semanticsById = new Dictionary<uint, SmartActionProtectionStatusSemantic>();
        var meanings = new HashSet<SmartActionProtectionStatusSemantic>();
        foreach (var definition in definitions)
        {
            if (!IsValidStatusId(definition.StatusId) ||
                !TryMap(definition.Semantic, out var kind))
            {
                return Empty;
            }

            if (semanticsById.TryGetValue(
                    definition.StatusId,
                    out var existingSemantic))
            {
                if (existingSemantic != definition.Semantic) return Empty;
            }
            else
            {
                semanticsById.Add(definition.StatusId, definition.Semantic);
                entries.Add(definition.StatusId, kind);
            }

            meanings.Add(definition.Semantic);
        }

        var complete = Enum
            .GetValues<SmartActionProtectionStatusSemantic>()
            .All(meanings.Contains);
        return complete
            ? new SmartActionProtectionStatusCatalog(entries, isVerified: true)
            : Empty;
    }

    private static bool TryMap(
        SmartActionProtectionStatusSemantic semantic,
        out SmartActionProtectionKind kind)
    {
        kind = semantic switch
        {
            SmartActionProtectionStatusSemantic.Guard =>
                SmartActionProtectionKind.Guard,
            SmartActionProtectionStatusSemantic.Covered =>
                SmartActionProtectionKind.Covered,
            SmartActionProtectionStatusSemantic.HallowedGround or
                SmartActionProtectionStatusSemantic.UndeadRedemption =>
                SmartActionProtectionKind.Invulnerability,
            _ => SmartActionProtectionKind.None,
        };
        return kind != SmartActionProtectionKind.None;
    }

    private static bool IsValidStatusId(uint statusId) =>
        statusId is not 0 and not 0xE0000000u and not uint.MaxValue;
}
