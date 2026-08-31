using System.Numerics;
using Dalamud.Bindings.ImGui;
using SeitonSense.Core;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawPingHelpersPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextColored(
            new Vector4(0.35f, 0.9f, 1f, 1f),
            "PING HELPERS");
        ImGui.TextWrapped(
            "These options cannot lower your real ping or increase skill range. " +
            "They help Seiton react as soon as FFXIV says an action can be used.");

        if (ImGui.CollapsingHeader("Recovery response", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "React as soon as recovery becomes usable",
                configuration.EnableAdaptiveResponseEngine,
                value => configuration.EnableAdaptiveResponseEngine = value);
            changed |= Checkbox(
                "Let emergency recovery override a queued action",
                configuration.AllowCriticalRecoveryThroughNativeQueue,
                value => configuration.AllowCriticalRecoveryThroughNativeQueue = value);
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextDisabled(
                "Priority: Purify → Recuperate → Auto-Guard → job helpers. With queue override enabled, " +
                "an accepted recovery can replace the action currently waiting in FFXIV. Turn it off if " +
                "you want the queued action to win.");
            ImGui.PopTextWrapPos();
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader(
                "Action buffer and Turbo",
                ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawGeneralActionBufferControls();

            ImGui.Spacing();
            ImGui.TextDisabled(
                "No extra macro is needed. If you press a supported action a little too early, " +
                "Seiton can remember that same action and target for a short time.");

            changed |= Checkbox(
                "Remember an out-of-range attack after I release the key",
                configuration.EnableHoldToLandChaseBuffer,
                value => configuration.EnableHoldToLandChaseBuffer = value);
            changed |= SliderInt(
                "Tap-to-land time",
                configuration.TapToLandReservationMilliseconds,
                HeldChaseBufferWindowRules.MinimumMilliseconds,
                HeldChaseBufferWindowRules.MaximumMilliseconds,
                value => configuration.TapToLandReservationMilliseconds = value,
                "%d ms");
            ImGui.TextDisabled(
                "Default 2200 ms; set it to 0 to disable. When a supported attack misses only because the " +
                "same target is out of range or sight, Seiton keeps trying that exact action briefly — even " +
                "after you release the key. A new action, target change, Guard, crowd control, death, or area " +
                "change cancels it. Supported single-target casts wait too, but never cancel another cast or " +
                "turn toward a target you have switched away from.");
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
