using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using NativeBounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;

namespace SeitonSense.Plugin.Services;

internal enum ResourceAuraSurface : byte
{
    SelfHotbar,
    PartyRow,
    CcAllyRow,
    CcEnemyRow,
}

internal sealed record ResourceAuraAnchorSnapshot(
    ResourceAuraSurface Surface,
    ulong GameObjectId,
    uint EntityId,
    Vector2 Minimum,
    Vector2 Maximum,
    ResourceAuraKind Kind);

/// <summary>
/// Copies current native HUD rectangles for a read-only ImGui aura. Native nodes are never
/// changed and pointers never survive this capture call.
/// </summary>
internal sealed class ResourceAuraAnchorTracker
{
    private static readonly string[] StandardHotbarNames =
    [
        "_ActionBar01", "_ActionBar02", "_ActionBar03", "_ActionBar04", "_ActionBar05",
        "_ActionBar06", "_ActionBar07", "_ActionBar08", "_ActionBar09",
    ];

    private readonly PluginConfiguration configuration;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;
    private readonly Dictionary<(ulong GameObjectId, uint EntityId), LowMpState> manaStates = [];
    private int lastSelfHotbarCount;
    private int lastPartyRowCount;
    private int lastCcRowCount;
    private long nextErrorLogAt;

    internal ResourceAuraAnchorTracker(
        PluginConfiguration configuration,
        IClientState clientState,
        IObjectTable objectTable,
        IGameGui gameGui,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.gameGui = gameGui;
        this.log = log;
    }

    internal int LastSelfHotbarCount => Volatile.Read(ref lastSelfHotbarCount);
    internal int LastPartyRowCount => Volatile.Read(ref lastPartyRowCount);
    internal int LastCcRowCount => Volatile.Read(ref lastCcRowCount);
    internal int LastAnchorCount => LastSelfHotbarCount + LastPartyRowCount + LastCcRowCount;

    internal unsafe IReadOnlyList<ResourceAuraAnchorSnapshot> Capture()
    {
        if (!configuration.Enabled || !configuration.EnableResourceAura || !clientState.IsPvP)
        {
            manaStates.Clear();
            PublishCounts([]);
            return [];
        }

        try
        {
            var localPlayer = objectTable.LocalPlayer;
            if (!IsValidPlayer(localPlayer))
            {
                manaStates.Clear();
                PublishCounts([]);
                return [];
            }

            var now = Environment.TickCount64;
            var seen = new HashSet<(ulong GameObjectId, uint EntityId)>();
            var results = new List<ResourceAuraAnchorSnapshot>(24);
            var localKind = Observe(localPlayer!, now, seen);
            if (localKind != ResourceAuraKind.None && configuration.ResourceAuraOnSelfHotbars)
                CaptureHotbars(localPlayer!, localKind, results);

            if (configuration.ResourceAuraOnPartyRows)
                CapturePartyRows(localPlayer!, now, seen, results);

            if (configuration.ResourceAuraOnCcTeamRows && clientState.IsPvPExcludingDen)
            {
                CaptureCcRows("PvPMKSPartyList1", friendly: true, localPlayer!, now, seen, results);
                CaptureCcRows("PvPMKSPartyList3", friendly: false, localPlayer!, now, seen, results);
            }

            foreach (var identity in manaStates.Keys.Where(identity => !seen.Contains(identity)).ToArray())
                manaStates.Remove(identity);

            PublishCounts(results);
            return results;
        }
        catch (Exception exception)
        {
            manaStates.Clear();
            PublishCounts([]);
            var now = Environment.TickCount64;
            if (now >= nextErrorLogAt)
            {
                nextErrorLogAt = now + 10_000;
                log.Error(exception, "Seiton Sense resource-aura anchoring failed closed.");
            }

            return [];
        }
    }

    private void PublishCounts(IReadOnlyList<ResourceAuraAnchorSnapshot> results)
    {
        Volatile.Write(
            ref lastSelfHotbarCount,
            results.Count(anchor => anchor.Surface == ResourceAuraSurface.SelfHotbar));
        Volatile.Write(
            ref lastPartyRowCount,
            results.Count(anchor => anchor.Surface == ResourceAuraSurface.PartyRow));
        Volatile.Write(
            ref lastCcRowCount,
            results.Count(anchor => anchor.Surface is ResourceAuraSurface.CcAllyRow or ResourceAuraSurface.CcEnemyRow));
    }

    private ResourceAuraKind Observe(
        IPlayerCharacter player,
        long now,
        HashSet<(ulong GameObjectId, uint EntityId)> seen)
    {
        var identity = (player.GameObjectId, player.EntityId);
        seen.Add(identity);
        manaStates.TryGetValue(identity, out var manaState);
        var plausibleMp = player.MaxMp > 0 && player.CurrentMp <= player.MaxMp;
        var trustedMp = plausibleMp && (player.CurrentMp > 0 || manaState.HasTrustedSample);
        var threshold = Math.Clamp(configuration.ResourceAuraMpThreshold, 0, 10_000);
        var exitThreshold = Math.Clamp(threshold + 300, threshold, 10_000);
        manaState = LowMpRules.Observe(
            manaState,
            (int)Math.Min(player.CurrentMp, int.MaxValue),
            trustedMp,
            now,
            enterThreshold: threshold,
            exitThreshold: exitThreshold);
        manaStates[identity] = manaState;

        return ResourceAuraRules.Resolve(
            new ResourceAuraObservation(
                player.CurrentHp,
                player.MaxHp,
                (int)Math.Min(player.CurrentMp, int.MaxValue),
                (int)Math.Min(player.MaxMp, int.MaxValue),
                manaState.HasTrustedSample,
                LowMpRules.ShouldShowCrossedIcon(manaState),
                !player.IsDead && player.CurrentHp > 0),
            Math.Clamp(configuration.ResourceAuraHpPercent, 1, 100),
            threshold);
    }

    private unsafe void CaptureHotbars(
        IPlayerCharacter localPlayer,
        ResourceAuraKind kind,
        List<ResourceAuraAnchorSnapshot> results)
    {
        var primary = gameGui.GetAddonByName<AddonActionBar>("_ActionBar");
        if (primary != null && IsVisible((AtkUnitBase*)primary))
            AddAnchor(ResourceAuraSurface.SelfHotbar, localPlayer, primary->ContainerNode, kind, results);

        foreach (var name in StandardHotbarNames)
        {
            var bar = gameGui.GetAddonByName<AddonActionBarX>(name);
            if (bar != null && IsVisible((AtkUnitBase*)bar))
                AddAnchor(ResourceAuraSurface.SelfHotbar, localPlayer, bar->ContainerNode, kind, results);
        }

        var cross = gameGui.GetAddonByName<AddonActionCross>("_ActionCross");
        if (cross != null && IsVisible((AtkUnitBase*)cross))
            AddAnchor(ResourceAuraSurface.SelfHotbar, localPlayer, cross->ContainerNode, kind, results);

        foreach (var name in new[] { "_ActionDoubleCrossL", "_ActionDoubleCrossR" })
        {
            var doubleCross = gameGui.GetAddonByName<AddonActionDoubleCrossBase>(name);
            if (doubleCross != null && IsVisible((AtkUnitBase*)doubleCross))
                AddAnchor(ResourceAuraSurface.SelfHotbar, localPlayer, doubleCross->ContainerNode, kind, results);
        }
    }

    private unsafe void CapturePartyRows(
        IPlayerCharacter localPlayer,
        long now,
        HashSet<(ulong GameObjectId, uint EntityId)> seen,
        List<ResourceAuraAnchorSnapshot> results)
    {
        var addon = gameGui.GetAddonByName<AddonPartyList>("_PartyList");
        var agent = AgentHUD.Instance();
        if (addon == null ||
            !IsVisible(&addon->AtkUnitBase) ||
            agent == null ||
            agent->PartyMemberCount is < 1 or > 8)
            return;

        var usedRows = new HashSet<byte>();
        foreach (var nativeMember in agent->PartyMembers[..agent->PartyMemberCount])
        {
            if (nativeMember.EntityId is 0 or 0xE0000000u ||
                nativeMember.EntityId == localPlayer.EntityId ||
                nativeMember.Index >= addon->MemberCount ||
                nativeMember.Index >= 8 ||
                nativeMember.Object == null ||
                !usedRows.Add(nativeMember.Index))
            {
                continue;
            }

            var player = objectTable.SearchByEntityId(nativeMember.EntityId) as IPlayerCharacter;
            if (!IsValidPlayer(player) ||
                player!.Address != (nint)nativeMember.Object ||
                player.EntityId != nativeMember.EntityId)
            {
                continue;
            }

            var row = addon->PartyMembers[nativeMember.Index];
            if (row.PartyMemberComponent == null || row.PartyMemberComponent->AtkResNode == null)
                continue;

            var kind = Observe(player, now, seen);
            if (kind != ResourceAuraKind.None)
                AddAnchor(ResourceAuraSurface.PartyRow, player, row.PartyMemberComponent->AtkResNode, kind, results);
        }
    }

    private unsafe void CaptureCcRows(
        string addonName,
        bool friendly,
        IPlayerCharacter localPlayer,
        long now,
        HashSet<(ulong GameObjectId, uint EntityId)> seen,
        List<ResourceAuraAnchorSnapshot> results)
    {
        var addon = gameGui.GetAddonByName<AtkUnitBase>(addonName);
        if (!IsVisible(addon)) return;

        var usedActors = new HashSet<uint>();
        for (var slot = 1; slot <= 5; slot++)
        {
            var player = friendly
                ? PartySlotResolver.Resolve(objectTable, slot)
                : EnemySlotResolver.Resolve(objectTable, slot);
            if (!IsValidPlayer(player) ||
                player!.EntityId == localPlayer.EntityId ||
                !usedActors.Add(player.EntityId))
            {
                continue;
            }

            var row = addon->GetComponentByNodeId((uint)(5 + slot));
            var name = row == null ? null : row->GetTextNodeById(21);
            if (row == null || row->AtkResNode == null || name == null ||
                !string.Equals(name->GetText().ToString(), player.Name.TextValue, StringComparison.Ordinal))
            {
                continue;
            }

            var kind = Observe(player, now, seen);
            if (kind == ResourceAuraKind.None) continue;
            AddAnchor(
                friendly ? ResourceAuraSurface.CcAllyRow : ResourceAuraSurface.CcEnemyRow,
                player,
                row->AtkResNode,
                kind,
                results);
        }
    }

    private static unsafe void AddAnchor(
        ResourceAuraSurface surface,
        IPlayerCharacter player,
        AtkResNode* node,
        ResourceAuraKind kind,
        List<ResourceAuraAnchorSnapshot> results)
    {
        if (!TryGetBounds(node, out var minimum, out var maximum)) return;
        results.Add(new ResourceAuraAnchorSnapshot(
            surface,
            player.GameObjectId,
            player.EntityId,
            minimum,
            maximum,
            kind));
    }

    private static unsafe bool IsVisible(AtkUnitBase* addon) =>
        addon != null && addon->IsVisible && addon->VisibilityFlags != 1 && IsNodeVisible(addon->RootNode);

    private static unsafe bool TryGetBounds(AtkResNode* node, out Vector2 minimum, out Vector2 maximum)
    {
        minimum = default;
        maximum = default;
        if (!IsNodeVisible(node)) return false;

        NativeBounds bounds;
        node->GetBounds(&bounds);
        minimum = new Vector2(Math.Min(bounds.Pos1.X, bounds.Pos2.X), Math.Min(bounds.Pos1.Y, bounds.Pos2.Y));
        maximum = new Vector2(Math.Max(bounds.Pos1.X, bounds.Pos2.X), Math.Max(bounds.Pos1.Y, bounds.Pos2.Y));
        var size = maximum - minimum;
        return float.IsFinite(minimum.X) && float.IsFinite(minimum.Y) &&
               float.IsFinite(maximum.X) && float.IsFinite(maximum.Y) &&
               size.X is > 2f and < 10_000f && size.Y is > 2f and < 10_000f;
    }

    private static unsafe bool IsNodeVisible(AtkResNode* node)
    {
        var depth = 0;
        while (node != null && depth++ < 64)
        {
            if (!node->IsVisible()) return false;
            node = node->ParentNode;
        }

        return depth is > 0 and < 64;
    }

    private static bool IsValidPlayer(IPlayerCharacter? player) =>
        player is not null &&
        player.Address != 0 &&
        player.GameObjectId != 0 &&
        player.EntityId is not (0 or 0xE0000000u) &&
        !player.IsDead &&
        player.CurrentHp > 0 &&
        player.MaxHp >= player.CurrentHp;
}
