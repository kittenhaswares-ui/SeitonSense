using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace SeitonSense.Plugin.UI;

/// <summary>
/// Small non-modal release-notes window. Version persistence remains owned by
/// the composition layer through the supplied visibility and acknowledgement
/// callbacks; this class never reads configuration or writes chat.
/// </summary>
internal sealed class WhatsNewWindow : Window
{
    private const ImGuiWindowFlags WindowFlags =
        ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoCollapse |
        ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoFocusOnAppearing;

    private static readonly Vector4 AccentColor = new(1f, 0.2f, 0.5f, 1f);

    private readonly string currentVersion;
    private readonly string[] bullets;
    private readonly Func<bool> shouldShow;
    private readonly Action acknowledge;
    private int acknowledgementClaimed;

    internal WhatsNewWindow(
        string currentVersion,
        IReadOnlyCollection<string> bullets,
        Func<bool> shouldShow,
        Action acknowledge)
        : base("What's New###SeitonSenseWhatsNew")
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
            throw new ArgumentException("A current version is required.", nameof(currentVersion));
        ArgumentNullException.ThrowIfNull(bullets);
        ArgumentNullException.ThrowIfNull(shouldShow);
        ArgumentNullException.ThrowIfNull(acknowledge);

        var sanitizedBullets = bullets
            .Where(static bullet => !string.IsNullOrWhiteSpace(bullet))
            .Select(static bullet => bullet.Trim())
            .ToArray();
        if (sanitizedBullets.Length is < 3 or > 5)
        {
            throw new ArgumentException(
                "What's New requires three to five non-empty bullets.",
                nameof(bullets));
        }

        this.currentVersion = currentVersion.Trim();
        this.bullets = sanitizedBullets;
        this.shouldShow = shouldShow;
        this.acknowledge = acknowledge;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = WindowFlags;
        Size = new Vector2(500f, 330f);
        SizeCondition = ImGuiCond.Appearing;
    }

    public override bool DrawConditions()
    {
        if (Volatile.Read(ref acknowledgementClaimed) != 0) return false;

        bool visible;
        try
        {
            visible = shouldShow();
        }
        catch
        {
            return false;
        }

        if (!visible) return false;

        // Center only on appearance and constrain the first-use size to the
        // current work area. The window remains ordinary and non-modal.
        var viewport = ImGui.GetMainViewport();
        var margin = 18f * Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var width = Math.Min(500f, Math.Max(280f, viewport.WorkSize.X - (margin * 2f)));
        var height = Math.Min(330f, Math.Max(230f, viewport.WorkSize.Y - (margin * 2f)));
        var size = new Vector2(width, height);
        Size = size;
        SizeCondition = ImGuiCond.Appearing;
        Position = viewport.WorkPos + Vector2.Max(Vector2.Zero, (viewport.WorkSize - size) * 0.5f);
        PositionCondition = ImGuiCond.Appearing;
        return true;
    }

    public override void Draw()
    {
        var scale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        ImGui.TextColored(AccentColor, "WHAT'S NEW");
        ImGui.SameLine();
        ImGui.TextDisabled(VersionLabel(currentVersion));
        ImGui.TextWrapped("Seiton Sense was updated. Here are the important changes:");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var buttonHeight = 34f * scale;
        var notesHeight = Math.Max(100f * scale, ImGui.GetContentRegionAvail().Y - buttonHeight - (18f * scale));
        if (ImGui.BeginChild("##SeitonSenseWhatsNewNotes", new Vector2(0f, notesHeight)))
        {
            foreach (var bullet in bullets)
            {
                ImGui.TextWrapped($"•  {bullet}");
                ImGui.Spacing();
            }
        }

        ImGui.EndChild();
        ImGui.Separator();
        ImGui.Spacing();
        if (!ImGui.Button("Got it", new Vector2(ImGui.GetContentRegionAvail().X, buttonHeight))) return;

        IsOpen = false;
        AcknowledgeOnce();
    }

    public override void OnClose() => AcknowledgeOnce();

    private void AcknowledgeOnce()
    {
        if (Interlocked.Exchange(ref acknowledgementClaimed, 1) != 0) return;

        try
        {
            acknowledge();
        }
        catch
        {
            // The callback is external state. A failure must not reopen or
            // repeatedly invoke it during this renderer lifetime.
        }
    }

    private static string VersionLabel(string version) =>
        version.StartsWith('v') || version.StartsWith('V')
            ? version
            : $"v{version}";
}
