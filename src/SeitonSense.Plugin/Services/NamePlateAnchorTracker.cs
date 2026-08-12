using System.Numerics;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using NativeBounds = FFXIVClientStructs.FFXIV.Common.Math.Bounds;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Copies the native job-icon rectangle from each active nameplate every frame.
/// No game node is changed and no native pointer or Dalamud handler survives the callback.
/// </summary>
internal sealed class NamePlateAnchorTracker : IDisposable
{
    private const long MissingHandlerGraceMilliseconds = 200;

    private readonly INamePlateGui namePlateGui;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;
    private NamePlateAnchorSnapshot[] anchors = [];
    private long nextErrorLogAt;
    private bool started;
    private bool disposed;

    public NamePlateAnchorTracker(
        INamePlateGui namePlateGui,
        IGameGui gameGui,
        IPluginLog log)
    {
        this.namePlateGui = namePlateGui;
        this.gameGui = gameGui;
        this.log = log;
    }

    public IReadOnlyList<NamePlateAnchorSnapshot> Anchors => Volatile.Read(ref anchors);

    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started) return;

        started = true;
        // An OnDataUpdate subscriber requests the full all-nameplate path every frame.
        // The post callback then reads the final positions after the game's own update.
        namePlateGui.OnDataUpdate += OnDataUpdate;
        namePlateGui.OnPostDataUpdate += OnPostDataUpdate;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started)
        {
            namePlateGui.OnDataUpdate -= OnDataUpdate;
            namePlateGui.OnPostDataUpdate -= OnPostDataUpdate;
        }

        Interlocked.Exchange(ref anchors, []);
    }

    private static void OnDataUpdate(
        INamePlateUpdateContext _,
        IReadOnlyList<INamePlateUpdateHandler> __)
    {
        // Intentionally empty. Subscribing is what asks Dalamud for the complete frame list.
    }

    private void OnPostDataUpdate(
        INamePlateUpdateContext _,
        IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (disposed) return;

        try
        {
            Capture(handlers);
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref anchors, []);
            var now = Environment.TickCount64;
            if (now < nextErrorLogAt) return;
            nextErrorLogAt = now + 10_000;
            log.Error(exception, "Seiton Sense nameplate anchoring failed closed.");
        }
    }

    private unsafe void Capture(IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        var now = Environment.TickCount64;
        var snapshots = new List<NamePlateAnchorSnapshot>(handlers.Count);
        var seen = new HashSet<(ulong GameObjectId, uint EntityId)>();
        var handlerIdentities = new HashSet<(ulong GameObjectId, uint EntityId)>();
        var handlerObjectIds = new HashSet<ulong>();
        var addon = gameGui.GetAddonByName<AddonNamePlate>("NamePlate");
        if (addon == null ||
            !addon->IsVisible ||
            addon->NamePlateObjectArray == null)
        {
            Interlocked.Exchange(ref anchors, []);
            return;
        }

        var maximumPlateIndex = AddonNamePlate.NumNamePlateObjects;

        foreach (var handler in handlers)
        {
            var player = handler.PlayerCharacter;
            if (handler.GameObjectId != 0) handlerObjectIds.Add(handler.GameObjectId);
            if (player is not null && handler.GameObjectId != 0)
                handlerIdentities.Add((handler.GameObjectId, player.EntityId));
            (ulong GameObjectId, uint EntityId) identity = player is null
                ? default
                : (handler.GameObjectId, player.EntityId);
            if (player is null ||
                handler.GameObjectId == 0 ||
                player.EntityId is 0 or 0xE0000000u ||
                player.GameObjectId != handler.GameObjectId ||
                handler.NamePlateIndex < 0 ||
                handler.NamePlateIndex >= maximumPlateIndex ||
                !seen.Add(identity))
            {
                continue;
            }

            var plate = &addon->NamePlateObjectArray[handler.NamePlateIndex];
            // Current API15 returns this exact visual NamePlateObject address. Requiring equality
            // also makes the addon/index path fail closed if Dalamud changes that mapping later.
            if (handler.NamePlateObjectAddress != (nint)plate) continue;
            if (!plate->IsVisible || !plate->IsPlayerCharacter || plate->NameIcon == null) continue;

            var iconNode = &plate->NameIcon->AtkResNode;
            if (!iconNode->IsVisible()) continue;

            NativeBounds bounds;
            iconNode->GetBounds(&bounds);
            var left = Math.Min(bounds.Pos1.X, bounds.Pos2.X);
            var top = Math.Min(bounds.Pos1.Y, bounds.Pos2.Y);
            var right = Math.Max(bounds.Pos1.X, bounds.Pos2.X);
            var bottom = Math.Max(bounds.Pos1.Y, bounds.Pos2.Y);
            if (right <= left || bottom <= top) continue;

            snapshots.Add(new NamePlateAnchorSnapshot(
                handler.GameObjectId,
                player.EntityId,
                new Vector2(left, top),
                new Vector2(right, bottom),
                now));
        }

        // The all-plate callback is expected every frame. If one handler alone is absent for a
        // single frame, retain only its last copied rectangle briefly. A present-but-hidden or
        // invalid handler is never retained, so native visibility still fails closed immediately.
        foreach (var previous in Anchors)
        {
            var identity = (previous.GameObjectId, previous.EntityId);
            if (seen.Contains(identity) ||
                handlerIdentities.Contains(identity) ||
                handlerObjectIds.Contains(previous.GameObjectId) ||
                now - previous.CapturedAtMilliseconds is < 0 or > MissingHandlerGraceMilliseconds)
            {
                continue;
            }

            snapshots.Add(previous);
        }

        Interlocked.Exchange(ref anchors, snapshots.ToArray());
    }
}
