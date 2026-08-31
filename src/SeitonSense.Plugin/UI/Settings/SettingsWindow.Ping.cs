using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawPingHelpersPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextColored(
            new Vector4(0.35f, 0.9f, 1f, 1f),
            "ADAPTIVE RESPONSE / PING HELPERS");
        ImGui.TextWrapped(
            "These helpers do not change your network ping or extend an action's real range. " +
            "They preserve an exact input briefly, observe native readiness transitions on framework " +
            "frames, and coordinate Seiton's recovery, held-helper, buffer, and Turbo paths.");

        if (ImGui.CollapsingHeader("Response core", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Enable adaptive readiness-edge recovery",
                configuration.EnableAdaptiveResponseEngine,
                value => configuration.EnableAdaptiveResponseEngine = value);
            changed |= Checkbox(
                "Let Purify / Recuperate / Auto-Guard try through an occupied native queue",
                configuration.AllowCriticalRecoveryThroughNativeQueue,
                value => configuration.AllowCriticalRecoveryThroughNativeQueue = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "A shared high-resolution monotonic clock supplies frame identity and ordering internally. " +
                "Priority is Purify > Recuperate > Auto-Guard > job helpers > action buffer / Turbo. " +
                "Critical recovery never edits the native queue directly: it uses only FFXIV's normal action " +
                "boundary. If FFXIV accepts a recovery request while another action is queued, that recovery may " +
                "replace the queued action by design. Disable this option to require an empty queue. A rejected " +
                "call may retry only when the complete pre-existing queue stayed unchanged; acceptance or any " +
                "ambiguous mutation is terminal. The shared scheduler admits at most one of its own action " +
                "requests per framework frame.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader(
                "Automatic action buffer / native Turbo",
                ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawGeneralActionBufferControls();

            ImGui.Spacing();
            ImGui.TextDisabled(
                "No /buffer macro or command is required. One certified physical standard-hotbar action can be " +
                "frozen automatically; Seiton never parses a user macro and never changes its action or target.");

            changed |= Checkbox(
                "Hold-to-land chase buffer",
                configuration.EnableHoldToLandChaseBuffer,
                value => configuration.EnableHoldToLandChaseBuffer = value);
            ImGui.TextDisabled(
                "For an instant harmful single-target action that failed only native range/line of sight, " +
                "keep the exact action and actor while the same hotbar key stays held. The first legal native " +
                "edge gets one attempt. The exact physical control is sampled again immediately before dispatch; " +
                "release, another press, drift, expiry, Stun/forced movement, action-blocking CC, death, or " +
                "zoning cancels it. Heavy or Bind alone do not discard an otherwise legal held action.");
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader(
                "Held-helper retry window",
                ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawPvpLatencyResponseControls();
        }

        return changed;
    }
}
