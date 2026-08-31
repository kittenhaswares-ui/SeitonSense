using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.UI;

internal enum BufferLearningPendingKind
{
    None = 0,
    EarlyTiming = 1,
    TapToLand = 2,
}

internal readonly record struct BufferLearningSnapshot(
    bool HasInput,
    BufferLearningPendingKind PendingKind,
    bool InputHeld,
    string InputLabel,
    string LogicalInputName,
    string ActionLabel,
    uint ActionId,
    int ConfiguredBufferMilliseconds,
    int RemainingBufferMilliseconds,
    int CapturedEarlyMilliseconds)
{
    public bool BufferPending => PendingKind != BufferLearningPendingKind.None;

    public static BufferLearningSnapshot Empty(int configuredMilliseconds) => new(
        HasInput: false,
        PendingKind: BufferLearningPendingKind.None,
        InputHeld: false,
        InputLabel: "NO INPUT",
        LogicalInputName: "Waiting for a standard-hotbar input",
        ActionLabel: "No action observed",
        ActionId: 0,
        ConfiguredBufferMilliseconds: configuredMilliseconds,
        RemainingBufferMilliseconds: 0,
        CapturedEarlyMilliseconds: 0);
}

internal interface IBufferLearningSnapshotSource
{
    BufferLearningSnapshot BufferLearningSnapshot { get; }
}

/// <summary>
/// Compact teaching surface for the general action buffer. Runtime input and
/// action services provide only a read-only snapshot; the window never issues
/// an action or infers a key that the input layer did not certify.
/// </summary>
internal sealed class BufferLearningWindow : Window
{
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.NoDocking;

    private readonly PluginConfiguration configuration;
    private readonly IBufferLearningSnapshotSource snapshotSource;
    private bool resetPosition;

    public BufferLearningWindow(
        PluginConfiguration configuration,
        IBufferLearningSnapshotSource snapshotSource)
        : base("Seiton Sense buffer###SeitonSenseBufferLearning")
    {
        this.configuration = configuration;
        this.snapshotSource = snapshotSource;
        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = BaseFlags;
        Position = new Vector2(520f, 520f);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions() =>
        configuration.Enabled &&
        configuration.EnableSmartActionBuffer &&
        configuration.ShowBufferLearningWindow;

    public override void PreDraw()
    {
        Flags = configuration.BufferLearningWindowLocked
            ? BaseFlags | ImGuiWindowFlags.NoMove
            : BaseFlags;
        BgAlpha = 0.92f;
        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowPadding,
            new Vector2(10f, 9f) * ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(
            ImGuiStyleVar.ItemSpacing,
            new Vector2(7f, 5f) * ImGuiHelpers.GlobalScale);

        if (!resetPosition) return;
        ImGui.SetNextWindowPos(new Vector2(520f, 520f), ImGuiCond.Always);
        resetPosition = false;
    }

    public override void PostDraw() => ImGui.PopStyleVar(2);

    public override void Draw()
    {
        var snapshot = snapshotSource.BufferLearningSnapshot;
        var tapToLandPending =
            snapshot.PendingKind == BufferLearningPendingKind.TapToLand;
        var scale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var width = 286f * scale;
        var accent = snapshot.BufferPending
            ? new Vector4(1f, 0.68f, 0.16f, 1f)
            : snapshot.InputHeld
                ? new Vector4(0.32f, 0.92f, 0.56f, 1f)
                : new Vector4(0.42f, 0.84f, 1f, 1f);

        ImGui.TextColored(
            accent,
            tapToLandPending
                ? "TAP-TO-LAND"
                : snapshot.BufferPending
                    ? "EARLY TIMING BUFFER"
                    : snapshot.InputHeld
                        ? "HELD INPUT"
                        : "LATEST INPUT");

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.08f, 0.10f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.10f, 0.13f, 0.20f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.08f, 0.10f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, accent);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.5f * scale);
        _ = ImGui.Button(
            $"{SafeLabel(snapshot.InputLabel, "NO INPUT")}##SeitonSenseLearningInput",
            new Vector2(width, 31f * scale));
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width);
        var actionLabel = SafeLabel(snapshot.ActionLabel, "No action observed");
        ImGui.TextWrapped(snapshot.ActionId == 0
            ? actionLabel
            : $"{actionLabel}  ·  {snapshot.ActionId}");
        ImGui.PopTextWrapPos();

        var configured = Math.Max(1, snapshot.ConfiguredBufferMilliseconds);
        var fraction = snapshot.BufferPending
            ? Math.Clamp(
                snapshot.RemainingBufferMilliseconds / (float)configured,
                0f,
                1f)
            : 0f;
        var overlay = snapshot.BufferPending
            ? $"{snapshot.RemainingBufferMilliseconds} / {configured} ms"
            : $"Ready · {configured} ms window";
        ImGui.ProgressBar(fraction, new Vector2(width, 18f * scale), overlay);

        if (tapToLandPending)
            ImGui.TextDisabled("Waiting for the same target to enter range");
        else if (snapshot.BufferPending)
            ImGui.TextDisabled(
                $"Slightly early press · {snapshot.CapturedEarlyMilliseconds} ms before ready");
        else if (!snapshot.HasInput)
            ImGui.TextDisabled("Waiting for a logical standard-hotbar press");
        else
            ImGui.TextDisabled(snapshot.InputHeld
                ? "The logical input is still held"
                : SafeLabel(snapshot.LogicalInputName, "Standard-hotbar input"));

        if (!ImGui.IsWindowHovered()) return;
        ImGui.BeginTooltip();
        ImGui.TextUnformatted("Seiton Sense buffer learning window");
        ImGui.TextUnformatted(configuration.BufferLearningWindowLocked
            ? "Position locked in settings"
            : "Drag this panel to move it");
        ImGui.TextUnformatted("Available in PvE, PvP, and the Wolves' Den.");
        ImGui.TextUnformatted(
            "Shows your key when possible; otherwise it shows the matching standard-hotbar slot.");
        ImGui.TextUnformatted(
            "Normal buffer: remembers a slightly early GCD, cooldown, or animation-timing press.");
        ImGui.TextUnformatted(
            "Tap-to-land: waits for the same target to enter range after a supported out-of-range tap.");
        ImGui.TextUnformatted(
            "Neither mode changes targets or buffers resource and server-rejection failures.");
        ImGui.EndTooltip();
    }

    public void ResetWindowPosition() => resetPosition = true;

    private static string SafeLabel(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}
