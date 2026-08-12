using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed class PressureCounterWindow : Window, IDisposable
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.NoDocking;

    private static readonly TargetPressureOpponentSnapshot[] PreviewOpponents =
    [
        new(1, 1, 32, 1, TargetPressureEvidence.HardTarget, 2, []),
        new(2, 2, 38, 2, TargetPressureEvidence.CastTarget, 1, []),
        new(3, 3, 40, 3, TargetPressureEvidence.RecentHarmfulAction, 0, []),
    ];

    private readonly PluginConfiguration configuration;
    private readonly TargetPressureTracker tracker;
    private readonly ITextureProvider textureProvider;
    private readonly IGameGui gameGui;
    private readonly IFontHandle numberFont;
    private bool resetPosition;
    private bool disposed;

    internal PressureCounterWindow(
        PluginConfiguration configuration,
        TargetPressureTracker tracker,
        ITextureProvider textureProvider,
        IGameGui gameGui,
        IDalamudPluginInterface pluginInterface)
        : base("Seiton Pressure###SeitonSensePressureOverlay")
    {
        this.configuration = configuration;
        this.tracker = tracker;
        this.textureProvider = textureProvider;
        this.gameGui = gameGui;
        numberFont = pluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(
            new GameFontStyle(GameFontFamilyAndSize.Jupiter90));

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = BaseFlags;
        Position = new Vector2(360, 240);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    internal bool PreviewEnabled { get; set; }

    public override bool DrawConditions() =>
        !gameGui.GameUiHidden &&
        (PreviewEnabled ||
         (configuration.Enabled &&
          configuration.ShowPressureCounter &&
          tracker.Snapshot.PressureActive));

    public override void PreDraw()
    {
        var flags = BaseFlags;
        if (configuration.PressureLocked) flags |= ImGuiWindowFlags.NoMove;
        if (configuration.PressureLocked && configuration.PressureClickThroughWhenLocked)
            flags |= ImGuiWindowFlags.NoInputs;
        if (!configuration.PressureShowBackground) flags |= ImGuiWindowFlags.NoBackground;
        Flags = flags;
        BgAlpha = configuration.PressureShowBackground
            ? Math.Clamp(configuration.PressureBackgroundOpacity, 0f, 1f)
            : 0f;

        if (!resetPosition) return;
        ImGui.SetNextWindowPos(new Vector2(360, 240), ImGuiCond.Always);
        resetPosition = false;
    }

    public override void Draw()
    {
        var opponents = PreviewEnabled
            ? PreviewOpponents
            : tracker.Snapshot.Opponents.Where(static opponent => opponent.IsIncoming).ToArray();
        var count = opponents.Length;
        var color = CountColor(count, configuration.PressureUseThreatColors);
        DrawSharpCount(count.ToString(), color);

        if (!configuration.PressureShowJobIcons || count == 0) return;

        ImGui.SameLine(0, 10f * ImGuiHelpers.GlobalScale);
        ImGui.BeginGroup();
        var iconSize = Math.Clamp(configuration.PressureIconSize, 16f, 72f) * ImGuiHelpers.GlobalScale;
        var spacing = Math.Clamp(configuration.PressureIconSpacing, 0f, 16f) * ImGuiHelpers.GlobalScale;
        var iconsPerRow = Math.Clamp(configuration.PressureIconsPerRow, 1, 16);
        for (var index = 0; index < opponents.Length; index++)
        {
            if (index > 0 && index % iconsPerRow != 0) ImGui.SameLine(0, spacing);
            DrawOpponentIcon(opponents[index], iconSize);
        }

        ImGui.EndGroup();
    }

    internal void ResetWindowPosition() => resetPosition = true;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        numberFont.Dispose();
    }

    private void DrawSharpCount(string text, Vector4 color)
    {
        using var pushed = numberFont.Push();
        var font = ImGui.GetFont();
        var size = Math.Clamp(configuration.PressureNumberPixelSize, 36f, 128f) *
                   Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var position = PixelRound(ImGui.GetCursorScreenPos());
        var nativeSize = Math.Max(1f, ImGui.GetFontSize());
        var textSize = ImGui.CalcTextSize(text) * (size / nativeSize);
        ImGui.GetWindowDrawList().AddText(font, size, position, ImGui.ColorConvertFloat4ToU32(color), text);
        ImGui.Dummy(new Vector2(MathF.Ceiling(textSize.X), MathF.Ceiling(textSize.Y)));
    }

    private void DrawOpponentIcon(TargetPressureOpponentSnapshot opponent, float size)
    {
        var topLeft = PixelRound(ImGui.GetCursorScreenPos());
        var bottomRight = topLeft + new Vector2(size);
        if (!TryDrawJobIcon(opponent.JobId, topLeft, bottomRight))
        {
            ImGui.GetWindowDrawList().AddRectFilled(
                topLeft,
                bottomRight,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.12f, 0.18f, 1f)),
                4f * ImGuiHelpers.GlobalScale);
        }

        var accent = EvidenceColor(opponent.IncomingEvidence);
        ImGui.GetWindowDrawList().AddRect(
            topLeft,
            bottomRight,
            ImGui.ColorConvertFloat4ToU32(accent),
            4f * ImGuiHelpers.GlobalScale,
            ImDrawFlags.None,
            Math.Max(2f, size * 0.07f));

        if (configuration.PressureShowEnemySlots && !string.IsNullOrEmpty(opponent.SlotLabel))
            DrawCornerLabel(opponent.SlotLabel, topLeft, bottomRight);
        ImGui.Dummy(new Vector2(size));
    }

    private bool TryDrawJobIcon(uint jobId, Vector2 topLeft, Vector2 bottomRight)
    {
        if (jobId == 0 ||
            !textureProvider.TryGetFromGameIcon(new GameIconLookup(62000u + jobId), out var shared) ||
            !shared.TryGetWrap(out var wrap, out _))
        {
            return false;
        }

        ImGui.GetWindowDrawList().AddImage(wrap.Handle, topLeft, bottomRight);
        return true;
    }

    private static void DrawCornerLabel(string label, Vector2 topLeft, Vector2 bottomRight)
    {
        var draw = ImGui.GetWindowDrawList();
        var textSize = ImGui.CalcTextSize(label);
        var padding = new Vector2(3f, 1f) * ImGuiHelpers.GlobalScale;
        var labelMin = new Vector2(topLeft.X, bottomRight.Y - textSize.Y - (padding.Y * 2f));
        var labelMax = labelMin + textSize + (padding * 2f);
        draw.AddRectFilled(labelMin, labelMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.88f)), 3f);
        draw.AddText(labelMin + padding, 0xFFFFFFFF, label);
    }

    private static Vector4 EvidenceColor(TargetPressureEvidence evidence)
    {
        if ((evidence & TargetPressureEvidence.MachinistLimitBreakMarker) != 0)
            return new Vector4(1f, 0.06f, 0.56f, 1f);
        if ((evidence & TargetPressureEvidence.CastTarget) != 0)
            return new Vector4(1f, 0.18f, 0.12f, 1f);
        if ((evidence & TargetPressureEvidence.HardTarget) != 0)
            return new Vector4(1f, 0.42f, 0.1f, 1f);
        return new Vector4(1f, 0.72f, 0.18f, 1f);
    }

    private static Vector4 CountColor(int count, bool useThreatColors) => !useThreatColors
        ? Vector4.One
        : count switch
        {
            0 => new Vector4(0.62f, 0.66f, 0.72f, 1f),
            1 => new Vector4(0.92f, 0.96f, 1f, 1f),
            2 => new Vector4(1f, 0.72f, 0.18f, 1f),
            _ => new Vector4(1f, 0.24f, 0.22f, 1f),
        };

    private static Vector2 PixelRound(Vector2 value) => new(MathF.Round(value.X), MathF.Round(value.Y));
}
