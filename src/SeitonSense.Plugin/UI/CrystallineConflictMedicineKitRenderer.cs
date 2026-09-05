using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.UI;

internal readonly record struct CrystallineConflictMedicineKitDiagnostics(
    bool Enabled,
    bool ExactContext,
    bool NativeTimerAvailable,
    float ContentTimeLeftSeconds,
    float FirstSpawnCountdownSeconds,
    int ScannedObjectCount,
    int VisibleKitCount,
    int LearnedBaseIdCount,
    int NamedObjectCount,
    int IdentifiedObjectCount,
    int NotReadyObjectCount,
    string SampleBaseIds,
    string LastEvent)
{
    internal string ToChatLine() =>
        $"cc-kits[enabled/context/timer={Enabled}/{ExactContext}/{NativeTimerAvailable}," +
        $"content={ContentTimeLeftSeconds:0.0}s,first={FirstSpawnCountdownSeconds:0.0}s," +
        $"scan/visible/learned={ScannedObjectCount}/{VisibleKitCount}/{LearnedBaseIdCount}," +
        $"named/matched/notReady={NamedObjectCount}/{IdentifiedObjectCount}/{NotReadyObjectCount}," +
        $"baseIds={SampleBaseIds}," +
        $"last={LastEvent}]";
}

/// <summary>
/// Read-only CC medicine-kit overlay. It reads the native five-minute content
/// timer and, at a bounded 10 Hz, only the dedicated EventObject and
/// ReactionEventObject slices exposed by Dalamud. No combat logs, filesystem
/// polling, terrain queries, targeting, or action calls are involved.
/// </summary>
internal sealed class CrystallineConflictMedicineKitRenderer
{
    private const int ScanIntervalMilliseconds = 100;
    private const float BeaconWorldHeightYalms = 32f;
    private const float EdgePadding = 42f;
    private static readonly Vector4 BeaconColor = new(0.18f, 1f, 0.34f, 1f);

    private readonly PluginConfiguration configuration;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IGameGui gameGui;
    private readonly Dictionary<uint, HashSet<uint>> learnedBaseIdsByTerritory = [];
    private MedicineKitAnchor[] anchors = [];
    private long nextScanAtMilliseconds;
    private uint activeTerritory;
    private int scannedObjectCount;
    private int namedObjectCount;
    private int identifiedObjectCount;
    private int notReadyObjectCount;
    private uint[] sampleBaseIds = [];
    private float contentTimeLeftSeconds = float.NaN;
    private float firstSpawnCountdownSeconds;
    private bool nativeTimerAvailable;
    private string lastEvent = "Idle; no public CC context.";

    internal CrystallineConflictMedicineKitRenderer(
        PluginConfiguration configuration,
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        IObjectTable objectTable,
        IGameGui gameGui)
    {
        this.configuration = configuration;
        this.pluginInterface = pluginInterface;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.gameGui = gameGui;
    }

    internal CrystallineConflictMedicineKitDiagnostics Diagnostics => new(
        configuration.Enabled &&
        (configuration.ShowCrystallineConflictMedicineKitCountdown ||
         configuration.ShowCrystallineConflictMedicineKitBeacons),
        IsExactContext,
        nativeTimerAvailable,
        contentTimeLeftSeconds,
        firstSpawnCountdownSeconds,
        scannedObjectCount,
        anchors.Length,
        learnedBaseIdsByTerritory.Values.Sum(static values => values.Count),
        namedObjectCount,
        identifiedObjectCount,
        notReadyObjectCount,
        sampleBaseIds.Length == 0 ? "none" : string.Join('/', sampleBaseIds),
        lastEvent);

    internal void Draw()
    {
        if (!configuration.Enabled ||
            (!configuration.ShowCrystallineConflictMedicineKitCountdown &&
             !configuration.ShowCrystallineConflictMedicineKitBeacons) ||
            !pluginInterface.UiBuilder.ShouldModifyUi ||
            !clientState.IsLoggedIn ||
            gameGui.GameUiHidden ||
            !IsExactContext)
        {
            ResetTransient("Idle; no enabled public CC medicine-kit overlay context.");
            return;
        }

        if (activeTerritory != clientState.TerritoryType)
        {
            activeTerritory = clientState.TerritoryType;
            anchors = [];
            scannedObjectCount = 0;
            nextScanAtMilliseconds = 0;
        }

        var now = Environment.TickCount64;
        if (now >= nextScanAtMilliseconds)
        {
            ReadNativeTimer();
            if (configuration.ShowCrystallineConflictMedicineKitBeacons)
            {
                ScanVisibleMedicineKits();
            }
            else
            {
                anchors = [];
                scannedObjectCount = 0;
            }

            nextScanAtMilliseconds = SaturatingAdd(now, ScanIntervalMilliseconds);
        }

        var viewport = ImGui.GetIO().DisplaySize;
        if (!IsFinite(viewport) || viewport.X <= 1f || viewport.Y <= 1f) return;
        var draw = ImGui.GetForegroundDrawList();
        if (configuration.ShowCrystallineConflictMedicineKitCountdown &&
            firstSpawnCountdownSeconds > 0f)
        {
            DrawFirstSpawnCountdown(draw, viewport);
        }

        if (!configuration.ShowCrystallineConflictMedicineKitBeacons) return;
        draw.PushClipRect(Vector2.Zero, viewport, true);
        try
        {
            foreach (var anchor in anchors) DrawBeacon(draw, viewport, anchor);
        }
        finally
        {
            draw.PopClipRect();
        }
    }

    private bool IsExactContext =>
        clientState.IsPvPExcludingDen &&
        PvPMatchRules.IsPublicCrystallineConflictTerritory(clientState.TerritoryType);

    private unsafe void ReadNativeTimer()
    {
        nativeTimerAvailable = false;
        contentTimeLeftSeconds = float.NaN;
        firstSpawnCountdownSeconds = 0f;
        try
        {
            var eventFramework = EventFramework.Instance();
            var director = eventFramework == null
                ? null
                : eventFramework->GetContentDirector();
            if (director == null || !float.IsFinite(director->ContentTimeLeft))
            {
                lastEvent = "Native CC content timer unavailable; countdown hidden.";
                return;
            }

            nativeTimerAvailable = true;
            contentTimeLeftSeconds = director->ContentTimeLeft;
            CrystallineConflictMedicineKitRules.TryGetFirstSpawnCountdown(
                contentTimeLeftSeconds,
                out firstSpawnCountdownSeconds);
            lastEvent = firstSpawnCountdownSeconds > 0f
                ? "Native first-spawn countdown active."
                : anchors.Length > 0
                    ? "Ready-to-draw medicine-kit objects detected."
                    : "Waiting for a visible localized medicine-kit runtime object.";
        }
        catch
        {
            lastEvent = "Native CC content timer read failed closed; countdown hidden.";
        }
    }

    private void ScanVisibleMedicineKits()
    {
        var territoryId = clientState.TerritoryType;
        if (!learnedBaseIdsByTerritory.TryGetValue(territoryId, out var learnedBaseIds))
        {
            learnedBaseIds = [];
            learnedBaseIdsByTerritory[territoryId] = learnedBaseIds;
        }

        var found = new List<MedicineKitAnchor>(12);
        var seen = new HashSet<MedicineKitAnchorKey>();
        var scanned = 0;
        var named = 0;
        var identified = 0;
        var notReady = 0;
        var samples = new HashSet<uint>();
        try
        {
            ScanObjectSlice(objectTable.EventObjects, territoryId, learnedBaseIds, found, seen,
                samples, ref scanned, ref named, ref identified, ref notReady);
            ScanObjectSlice(objectTable.ReactionEventObjects, territoryId, learnedBaseIds, found, seen,
                samples, ref scanned, ref named, ref identified, ref notReady);
            anchors = found.ToArray();
            scannedObjectCount = scanned;
            namedObjectCount = named;
            identifiedObjectCount = identified;
            notReadyObjectCount = notReady;
            sampleBaseIds = samples.Order().ToArray();
            lastEvent = anchors.Length > 0
                ? "Ready-to-draw medicine-kit objects detected from bounded event-object slices."
                : firstSpawnCountdownSeconds > 0f
                    ? "Native first-spawn countdown active; no medicine-kit object detected yet."
                    : identified > 0
                        ? "Medicine-kit identity found, but no matched object is ready to draw."
                        : "No medicine-kit identity matched; this is detection, not beam projection.";
        }
        catch
        {
            anchors = [];
            scannedObjectCount = scanned;
            lastEvent = "Medicine-kit event-object scan failed closed; beacons hidden.";
        }
    }

    private static void ScanObjectSlice(
        IEnumerable<IGameObject> objects,
        uint territoryId,
        HashSet<uint> learnedBaseIds,
        List<MedicineKitAnchor> found,
        HashSet<MedicineKitAnchorKey> seen,
        HashSet<uint> samples,
        ref int scanned,
        ref int named,
        ref int identified,
        ref int notReady)
    {
        foreach (var gameObject in objects)
        {
            scanned++;
            if (gameObject is null ||
                gameObject.Address == nint.Zero ||
                !gameObject.IsValid() ||
                !IsFinite(gameObject.Position))
            {
                continue;
            }

            var localizedName = gameObject.Name.TextValue?.Trim() ?? string.Empty;
            if (localizedName.Length > 0) named++;
            if (gameObject.BaseId != 0 && samples.Count < 8) samples.Add(gameObject.BaseId);
            var nameMatches =
                CrystallineConflictMedicineKitRules.IsMedicineKitName(localizedName);
            if (nameMatches && gameObject.BaseId != 0)
                learnedBaseIds.Add(gameObject.BaseId);

            if (!nameMatches &&
                (gameObject.BaseId == 0 || !learnedBaseIds.Contains(gameObject.BaseId)))
            {
                continue;
            }

            identified++;
            if (!IsReadyToDraw(gameObject))
            {
                notReady++;
                continue;
            }
            var position = gameObject.Position;
            var key = new MedicineKitAnchorKey(
                territoryId,
                gameObject.BaseId,
                Quantize(position.X),
                Quantize(position.Y),
                Quantize(position.Z));
            if (!seen.Add(key)) continue;
            found.Add(new MedicineKitAnchor(position, gameObject.BaseId));
        }
    }

    private static unsafe bool IsReadyToDraw(IGameObject gameObject)
    {
        try
        {
            var native = (NativeGameObject*)gameObject.Address;
            return native != null && native->IsReadyToDraw();
        }
        catch
        {
            return false;
        }
    }

    private void DrawFirstSpawnCountdown(ImDrawListPtr draw, Vector2 viewport)
    {
        var scale = SafeScale(configuration.CrystallineConflictMedicineKitOverlayScale);
        var text = $"MEDICINE KITS  {firstSpawnCountdownSeconds:0.0}s";
        var size = ImGui.CalcTextSize(text) * scale;
        var center = new Vector2(viewport.X * 0.5f, viewport.Y * 0.18f);
        var padding = new Vector2(18f, 11f) * scale;
        var min = center - (size * 0.5f) - padding;
        var max = center + (size * 0.5f) + padding;
        draw.AddRectFilled(
            min,
            max,
            Pack(new Vector4(0.015f, 0.055f, 0.025f, 0.88f), 1f),
            9f * scale);
        draw.AddRect(min, max, Pack(BeaconColor, 0.9f), 9f * scale, ImDrawFlags.None, 2f * scale);
        draw.AddText(
            ImGui.GetFont(),
            ImGui.GetFontSize() * scale,
            center - (size * 0.5f) + Vector2.One,
            Pack(new Vector4(0f, 0f, 0f, 1f), 0.95f),
            text);
        draw.AddText(
            ImGui.GetFont(),
            ImGui.GetFontSize() * scale,
            center - (size * 0.5f),
            Pack(new Vector4(0.72f, 1f, 0.78f, 1f), 1f),
            text);
    }

    private void DrawBeacon(
        ImDrawListPtr draw,
        Vector2 viewport,
        MedicineKitAnchor anchor)
    {
        var projected = gameGui.WorldToScreen(anchor.Position, out var basePoint, out var inViewport);
        if (!projected || !IsFinite(basePoint)) return;

        var local = objectTable.LocalPlayer;
        var distance = local is not null && IsFinite(local.Position)
            ? Vector3.Distance(local.Position, anchor.Position)
            : float.NaN;
        var scale = SafeScale(configuration.CrystallineConflictMedicineKitOverlayScale);
        var color = BeaconColor;
        var skyWorld = anchor.Position + new Vector3(0f, BeaconWorldHeightYalms, 0f);
        var skyProjected = gameGui.WorldToScreen(skyWorld, out var skyPoint, out _);
        if (!CrystallineConflictMedicineKitRules.TryGetBeaconScreenSegment(
                basePoint, projected, skyPoint, skyProjected, viewport, scale,
                out var visibleBase, out var visibleTop))
        {
            if (inViewport) return;
            var padding = Math.Min(EdgePadding, Math.Min(viewport.X, viewport.Y) * 0.25f);
            var edge = Vector2.Clamp(
                basePoint,
                new Vector2(padding),
                viewport - new Vector2(padding));
            draw.AddCircleFilled(edge, 13f * scale, Pack(color, 0.22f), 24);
            draw.AddCircle(edge, 9f * scale, Pack(color, 1f), 24, 2.5f * scale);
            DrawBeaconLabel(draw, edge + new Vector2(0f, 13f * scale), distance, scale, viewport);
            return;
        }

        var pulse = pluginInterface.UiBuilder.ShouldUseReducedMotion ? 1f : 0.82f +
            (0.18f * MathF.Sin((Environment.TickCount64 % 1_200L) / 1_200f * MathF.PI * 2f));
        draw.AddLine(visibleBase, visibleTop, Pack(color, 0.12f * pulse), 18f * scale);
        draw.AddLine(visibleBase, visibleTop, Pack(color, 0.3f * pulse), 8f * scale);
        draw.AddLine(visibleBase, visibleTop, Pack(color, 0.92f * pulse), 2.5f * scale);
        draw.AddLine(visibleBase, visibleTop, Pack(new Vector4(0.9f, 1f, 0.92f, 1f), 0.8f), 0.8f * scale);
        if (inViewport)
        {
            draw.AddCircleFilled(basePoint, 18f * scale, Pack(color, 0.16f * pulse), 32);
            draw.AddCircle(basePoint, 11f * scale, Pack(color, 0.95f), 32, 2.5f * scale);
        }
        DrawBeaconLabel(draw, visibleTop + new Vector2(0f, 5f), distance, scale, viewport);
    }

    internal static void DrawSettingsPreview(float configuredScale)
    {
        var scale = SafeScale(configuredScale);
        var size = new Vector2(Math.Min(ImGui.GetContentRegionAvail().X, 450f), 245f * scale);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(size);
        var draw = ImGui.GetWindowDrawList();
        var bottom = origin + new Vector2(size.X * 0.5f, size.Y - 20f);
        var top = bottom - new Vector2(0f, 180f * scale);
        draw.AddLine(bottom, top, Pack(BeaconColor, 0.12f), 18f * scale);
        draw.AddLine(bottom, top, Pack(BeaconColor, 0.3f), 8f * scale);
        draw.AddLine(bottom, top, Pack(BeaconColor, 0.92f), 2.5f * scale);
        draw.AddCircle(bottom, 11f * scale, Pack(BeaconColor, 0.95f), 32, 2.5f * scale);
        draw.AddText(top + new Vector2(-65f, -20f), Pack(BeaconColor, 1f), "MEDICINE KIT - PREVIEW");
    }

    private static void DrawBeaconLabel(
        ImDrawListPtr draw,
        Vector2 anchor,
        float distance,
        float scale,
        Vector2 viewport)
    {
        var text = float.IsFinite(distance)
            ? $"MEDICINE KIT  {distance:0}y"
            : "MEDICINE KIT";
        var size = ImGui.CalcTextSize(text) * scale;
        var inset = new Vector2(6f);
        var position = Vector2.Clamp(anchor - new Vector2(size.X * 0.5f, 0f),
            inset, Vector2.Max(inset, viewport - size - inset));
        draw.AddRectFilled(position - new Vector2(4f, 2f), position + size + new Vector2(4f, 2f),
            Pack(new Vector4(0.01f, 0.05f, 0.02f, 1f), 0.82f), 3f);
        draw.AddText(
            ImGui.GetFont(),
            ImGui.GetFontSize() * scale,
            position + Vector2.One,
            Pack(new Vector4(0f, 0f, 0f, 1f), 0.95f),
            text);
        draw.AddText(
            ImGui.GetFont(),
            ImGui.GetFontSize() * scale,
            position,
            Pack(new Vector4(0.72f, 1f, 0.78f, 1f), 1f),
            text);
    }

    private void ResetTransient(string reason)
    {
        if (activeTerritory == 0 && anchors.Length == 0 && !nativeTimerAvailable) return;
        activeTerritory = 0;
        anchors = [];
        scannedObjectCount = 0;
        namedObjectCount = 0;
        identifiedObjectCount = 0;
        notReadyObjectCount = 0;
        sampleBaseIds = [];
        nextScanAtMilliseconds = 0;
        nativeTimerAvailable = false;
        contentTimeLeftSeconds = float.NaN;
        firstSpawnCountdownSeconds = 0f;
        lastEvent = reason;
    }

    private static int Quantize(float value) => (int)MathF.Round(value * 100f);

    private static float SafeScale(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, 0.6f, 2f) : 1f;

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static uint Pack(Vector4 color, float alpha) =>
        ImGui.ColorConvertFloat4ToU32(new Vector4(
            color.X,
            color.Y,
            color.Z,
            Math.Clamp(alpha * color.W, 0f, 1f)));

    private static long SaturatingAdd(long left, int right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private readonly record struct MedicineKitAnchor(Vector3 Position, uint BaseId);

    private readonly record struct MedicineKitAnchorKey(
        uint TerritoryId,
        uint BaseId,
        int PositionX,
        int PositionY,
        int PositionZ);
}
