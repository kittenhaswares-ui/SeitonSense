using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

/// <summary>
/// Runtime-owned presentation options. The window deliberately receives the
/// toggle value and persistence callback from its owner instead of reaching
/// into plugin configuration itself.
/// </summary>
internal readonly record struct AutoSeitonToggleWidgetOptions(
    bool MasterEnabled,
    SupportedPvPContext Context,
    bool MetadataVerified,
    bool AutoSeitonEnabled,
    float Scale = 1f,
    bool Locked = false);

/// <summary>
/// Compact action-bar-style switch for persistent Auto-Seiton. It is visible
/// only to NIN in an already resolved supported PvP context; that includes the
/// Wolves' Den when the owner's context resolver has explicitly enabled it.
/// </summary>
internal sealed class AutoSeitonToggleWindow : Window
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.NoBackground;

    private readonly IObjectTable objectTable;
    private readonly ITextureProvider textureProvider;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;
    private readonly Func<AutoSeitonToggleWidgetOptions> optionsProvider;
    private readonly Action<bool> setEnabled;
    private AutoSeitonToggleWidgetOptions currentOptions;
    private long nextErrorLogAt;

    internal AutoSeitonToggleWindow(
        IObjectTable objectTable,
        ITextureProvider textureProvider,
        IGameGui gameGui,
        IPluginLog log,
        Func<AutoSeitonToggleWidgetOptions> optionsProvider,
        Action<bool> setEnabled)
        : base("Auto Seiton###SeitonSenseAutoSeitonToggle")
    {
        this.objectTable = objectTable;
        this.textureProvider = textureProvider;
        this.gameGui = gameGui;
        this.log = log;
        this.optionsProvider = optionsProvider;
        this.setEnabled = setEnabled;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = BaseFlags;
        Position = new Vector2(520f, 640f);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions()
    {
        if (gameGui.GameUiHidden || !TryCaptureOptions(out currentOptions)) return false;
        if (!currentOptions.MasterEnabled ||
            currentOptions.Context is not (SupportedPvPContext.CrystallineConflict or
                SupportedPvPContext.WolvesDen))
        {
            return false;
        }

        var localPlayer = objectTable.LocalPlayer;
        var visible = localPlayer?.ClassJob.IsValid == true &&
                      ExecuteThreshold.IsNinja(localPlayer.ClassJob.RowId);
        if (!visible) return false;

        var viewport = ImGui.GetMainViewport();
        Position = viewport.WorkPos + new Vector2(
            Math.Max(0f, (viewport.WorkSize.X * 0.5f) - 30f),
            Math.Max(0f, viewport.WorkSize.Y * 0.78f));
        PositionCondition = ImGuiCond.FirstUseEver;
        return true;
    }

    public override void PreDraw()
    {
        Flags = currentOptions.Locked
            ? BaseFlags | ImGuiWindowFlags.NoMove
            : BaseFlags;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(3f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
    }

    public override void Draw()
    {
        var scale = Math.Clamp(
            float.IsFinite(currentOptions.Scale) ? currentOptions.Scale : 1f,
            0.65f,
            1.8f) * Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var size = MathF.Round(58f * scale);
        var extent = new Vector2(size);
        var minimum = PixelSnap(ImGui.GetCursorScreenPos());

        var clicked = ImGui.InvisibleButton("##AutoSeitonToggleButton", extent);
        var hovered = ImGui.IsItemHovered();
        var maximum = minimum + extent;
        var draw = ImGui.GetWindowDrawList();
        var enabled = currentOptions.AutoSeitonEnabled;
        var localPlayer = objectTable.LocalPlayer;
        var resolvedActionId = 0u;
        var ready = enabled &&
                    currentOptions.MetadataVerified &&
                    localPlayer is { IsDead: false, CurrentHp: > 0 } &&
                    SeitonReadinessProbe.TryGetReadyAction(localPlayer, out resolvedActionId);

        DrawTile(draw, minimum, maximum, scale, enabled, hovered, ready);

        if (clicked)
        {
            try
            {
                setEnabled(!enabled);
            }
            catch (Exception exception)
            {
                LogFailure(exception, "Auto-Seiton toggle callback failed closed.");
            }
        }

        if (!hovered) return;
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(enabled ? "AUTO SEITON: ON" : "AUTO SEITON: OFF");
        ImGui.TextUnformatted("Click to toggle");
        ImGui.Separator();
        if (!currentOptions.MetadataVerified)
            ImGui.TextUnformatted("Seiton metadata is not verified");
        else if (ready)
            ImGui.TextUnformatted($"Ready now ({resolvedActionId})");
        else
            ImGui.TextUnformatted("Not ready");
        ImGui.TextUnformatted(
            currentOptions.Context == SupportedPvPContext.WolvesDen
                ? "Wolves' Den test context"
                : "Crystalline Conflict");
        ImGui.EndTooltip();
    }

    private void DrawTile(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        float scale,
        bool enabled,
        bool hovered,
        bool ready)
    {
        var rounding = 5f * scale;
        var inset = Math.Max(2f, 3f * scale);
        var iconMinimum = minimum + new Vector2(inset);
        var iconMaximum = maximum - new Vector2(inset);
        var background = enabled
            ? new Vector4(0.045f, 0.025f, 0.085f, 0.98f)
            : new Vector4(0.035f, 0.04f, 0.055f, 0.98f);
        var accent = enabled
            ? new Vector4(1f, 0.18f, 0.56f, 1f)
            : new Vector4(0.46f, 0.5f, 0.58f, 1f);

        draw.AddRectFilled(minimum, maximum, Pack(background), rounding);
        var iconId = enabled
            ? EnemyCombatConstants.SeitonIconId
            : EnemyCombatConstants.JobIconBaseId + ExecuteThreshold.NinjaJobId;
        if (!TryDrawIcon(draw, iconId, iconMinimum, iconMaximum, enabled ? 1f : 0.54f))
        {
            draw.AddRectFilled(iconMinimum, iconMaximum, Pack(accent with { W = 0.24f }), 3f * scale);
            DrawCenteredText(draw, iconMinimum, iconMaximum, enabled ? "LB" : "NIN", scale, Vector4.One);
        }

        if (!enabled)
        {
            draw.AddRectFilled(iconMinimum, iconMaximum, Pack(new Vector4(0f, 0f, 0f, 0.32f)), 3f * scale);
            draw.AddLine(
                iconMinimum + new Vector2(3f * scale),
                iconMaximum - new Vector2(3f * scale),
                Pack(new Vector4(1f, 0.22f, 0.22f, 0.92f)),
                Math.Max(2f, 3f * scale));
        }

        var border = ready
            ? ReadyColor()
            : hovered
                ? new Vector4(0.94f, 0.96f, 1f, 1f)
                : accent;
        draw.AddRect(
            minimum,
            maximum,
            Pack(border),
            rounding,
            ImDrawFlags.None,
            Math.Max(1.5f, (ready ? 2.8f : 1.8f) * scale));

        DrawStateBadge(draw, minimum, maximum, scale, enabled);
        if (ready) DrawReadySparkle(draw, minimum, maximum, scale);
    }

    private bool TryDrawIcon(
        ImDrawListPtr draw,
        uint iconId,
        Vector2 minimum,
        Vector2 maximum,
        float alpha)
    {
        if (!textureProvider.TryGetFromGameIcon(new GameIconLookup(iconId), out var shared) ||
            !shared.TryGetWrap(out var wrap, out _))
        {
            return false;
        }

        draw.AddImage(
            wrap.Handle,
            minimum,
            maximum,
            Vector2.Zero,
            Vector2.One,
            Pack(new Vector4(1f, 1f, 1f, Math.Clamp(alpha, 0f, 1f))));
        return true;
    }

    private static void DrawStateBadge(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        float scale,
        bool enabled)
    {
        var label = enabled ? "ON" : "OFF";
        var color = enabled
            ? new Vector4(0.18f, 1f, 0.48f, 1f)
            : new Vector4(1f, 0.22f, 0.22f, 1f);
        var font = ImGui.GetFont();
        var fontSize = Math.Max(9f, ImGui.GetFontSize() * 0.72f);
        var textSize = ImGui.CalcTextSize(label) * (fontSize / Math.Max(1f, ImGui.GetFontSize()));
        var padding = new Vector2(4f, 1.5f) * scale;
        var badgeMaximum = maximum - new Vector2(2f * scale);
        var badgeMinimum = badgeMaximum - textSize - (padding * 2f);
        draw.AddRectFilled(
            badgeMinimum,
            badgeMaximum,
            Pack(new Vector4(0.01f, 0.012f, 0.02f, 0.94f)),
            3f * scale);
        draw.AddRect(
            badgeMinimum,
            badgeMaximum,
            Pack(color),
            3f * scale,
            ImDrawFlags.None,
            Math.Max(1f, scale));
        draw.AddText(font, fontSize, badgeMinimum + padding, Pack(color), label);
    }

    private static void DrawReadySparkle(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        float scale)
    {
        var time = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var wave = 0.5f + (0.5f * MathF.Sin((float)(time * 8d)));
        var radius = (4f + (wave * 2.5f)) * scale;
        var center = new Vector2(maximum.X - (7f * scale), minimum.Y + (7f * scale));
        var color = ReadyColor();
        draw.AddCircleFilled(center, Math.Max(1.5f, 2.2f * scale), Pack(color));
        draw.AddLine(center - new Vector2(radius, 0f), center + new Vector2(radius, 0f), Pack(color), Math.Max(1f, 1.5f * scale));
        draw.AddLine(center - new Vector2(0f, radius), center + new Vector2(0f, radius), Pack(color), Math.Max(1f, 1.5f * scale));
        var diagonal = radius * 0.62f;
        draw.AddLine(center - new Vector2(diagonal), center + new Vector2(diagonal), Pack(color), Math.Max(1f, scale));
        draw.AddLine(
            center + new Vector2(-diagonal, diagonal),
            center + new Vector2(diagonal, -diagonal),
            Pack(color),
            Math.Max(1f, scale));
    }

    private static void DrawCenteredText(
        ImDrawListPtr draw,
        Vector2 minimum,
        Vector2 maximum,
        string text,
        float scale,
        Vector4 color)
    {
        var font = ImGui.GetFont();
        var fontSize = Math.Max(10f, ImGui.GetFontSize() * 0.8f * scale);
        var textSize = ImGui.CalcTextSize(text) * (fontSize / Math.Max(1f, ImGui.GetFontSize()));
        var position = minimum + (((maximum - minimum) - textSize) * 0.5f);
        draw.AddText(font, fontSize, position, Pack(color), text);
    }

    private bool TryCaptureOptions(out AutoSeitonToggleWidgetOptions options)
    {
        try
        {
            options = optionsProvider();
            return true;
        }
        catch (Exception exception)
        {
            options = default;
            LogFailure(exception, "Auto-Seiton widget options failed closed.");
            return false;
        }
    }

    private void LogFailure(Exception exception, string message)
    {
        var now = Environment.TickCount64;
        if (now < nextErrorLogAt) return;
        nextErrorLogAt = now + 10_000;
        log.Warning(exception, message);
    }

    private static Vector4 ReadyColor()
    {
        var time = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var wave = 0.5f + (0.5f * MathF.Sin((float)(time * 5d)));
        return new Vector4(1f, 0.72f + (0.24f * wave), 0.18f + (0.52f * wave), 1f);
    }

    private static Vector2 PixelSnap(Vector2 value) =>
        new(MathF.Round(value.X), MathF.Round(value.Y));

    private static uint Pack(Vector4 color) =>
        ImGui.ColorConvertFloat4ToU32(new Vector4(
            Math.Clamp(float.IsFinite(color.X) ? color.X : 1f, 0f, 1f),
            Math.Clamp(float.IsFinite(color.Y) ? color.Y : 1f, 0f, 1f),
            Math.Clamp(float.IsFinite(color.Z) ? color.Z : 1f, 0f, 1f),
            Math.Clamp(float.IsFinite(color.W) ? color.W : 1f, 0f, 1f)));
}
