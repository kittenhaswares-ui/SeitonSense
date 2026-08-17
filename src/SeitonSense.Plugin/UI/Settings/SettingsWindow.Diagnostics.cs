using Dalamud.Bindings.ImGui;
using SeitonSense.Core;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private bool DrawDiagnosticsPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextUnformatted("Live diagnostics");
        ImGui.TextDisabled("For the complete multi-line snapshot, use /seiton debug in chat.");
        ImGui.TextWrapped(
            $"{tracker.Diagnostics.ToChatLine()}, native-anchors={overlay.NativeAnchorCount}, " +
            $"resource-anchors={overlay.ResourceAuraAnchorCount} " +
            $"(hotbar {overlay.ResourceAuraSelfHotbarCount}, party {overlay.ResourceAuraPartyRowCount}, " +
            $"CC rows {overlay.ResourceAuraCcRowCount})");
        var personal = personalStatus.Snapshot;
        var mchLimitBreak = personalStatus.MachinistLimitBreakDiagnostics;
        var pressureEscape = personalStatus.PressureEscapeDiagnostics;
        var defense = personalStatus.DefensiveUtilityDiagnostics;
        var rescue = personalStatus.AllyRescueDiagnostics;
        var miracle = personalStatus.MiracleInterceptDiagnostics;
        var monk = personalStatus.MonkEarthReplyDiagnostics;
        ImGui.TextWrapped(
            $"Personal statuses={personal.Statuses.Length}, Purify={personal.Purify.Phase}/" +
            $"{personal.Purify.Decision}, cancel={personal.Purify.CancelReason}, " +
            $"trigger={personal.Purify.InputTrigger}, ready={personal.Purify.LocallyReady}, " +
            $"fresh={personal.Purify.FreshGameplayKey}, held={personal.Purify.HeldGameplayKey}, " +
            $"attempt={personal.Purify.UseActionAttempted}/{personal.Purify.UseActionAccepted}, " +
            $"buffered={personal.Purify.BufferRemainingMilliseconds} ms");
        ImGui.TextWrapped(
            $"MCH LB capture: hook={mchLimitBreak.CaptureRunning}, queue={mchLimitBreak.QueueDepth}, " +
            $"accepted={mchLimitBreak.AcceptedWarnings}, active={mchLimitBreak.WarningActive}, " +
            $"errors={mchLimitBreak.CaptureErrors}, drops={mchLimitBreak.DroppedWarnings}");
        ImGui.TextWrapped(
            $"High-pressure escape: active={pressureEscape.Active}, visual/sound/sprint=" +
            $"{pressureEscape.WarningEnabled}/{configuration.PlayHighPressureWarningSound}/" +
            $"{pressureEscape.SprintEnabled}, pressure=" +
            $"{pressureEscape.PressureKnown}/{pressureEscape.DirectEnemyCount}, high=" +
            $"{pressureEscape.HighPressure}, warning={pressureEscape.WarningActive}, episode=" +
            $"{pressureEscape.WarningEpisodeToken}, guard/sprint={pressureEscape.GuardSuppressed}/" +
            $"{pressureEscape.SprintActive}, metadata={pressureEscape.SprintMetadataVerified}, key=" +
            $"{pressureEscape.HeldGameplayKey}, claim={pressureEscape.InputClaimed}, attempt=" +
            $"{pressureEscape.UseActionAttempted}/{pressureEscape.UseActionAccepted}, " +
            $"last={pressureEscape.LastEvent}");
        ImGui.TextWrapped(
            $"Defensive utility: active={defense.Active}, action/trigger={defense.Action}/{defense.Trigger}, " +
            $"pressure={defense.PressureKnown}/{defense.IncomingEnemyCount}, guard={defense.GuardActive}, " +
            $"stun={defense.HighPressureStunObserved}, post-Purify={defense.WaitingForPostPurifyGuard}/" +
            $"{defense.PostPurifyGuardRemainingMilliseconds} ms, Guardian candidates={defense.GuardianCandidateCount}, " +
            $"target={defense.TargetGameObjectId:X}/{defense.TargetEntityId:X}, " +
            $"key={defense.FreshGameplayKey}/{defense.HeldGameplayKey}, claim={defense.InputClaimed}, " +
            $"attempt={defense.UseActionAttempted}/{defense.UseActionAccepted}, " +
            $"Guardian popup={defense.GuardianPopup?.PartySlot ?? 0}/" +
            $"{Math.Max(0, (defense.GuardianPopup?.EndsAtMilliseconds ?? 0) - Environment.TickCount64)} ms, " +
            $"count={defense.AttemptCount}/{defense.AcceptedCount}, metadata=" +
            $"{defense.GuardMetadataVerified}/{defense.GuardianMetadataVerified}, last={defense.LastEvent}");
        ImGui.TextWrapped(
            $"Ally Rescue: {rescue.Phase}/{rescue.Decision}, cancel={rescue.CancelReason}, " +
            $"trigger={rescue.InputTrigger}, candidates={rescue.CandidateCount}, action={rescue.ActionId}, " +
            $"target={rescue.TargetGameObjectId:X}, status={rescue.TargetStatusId}, ready={rescue.LocallyReady}, " +
            $"attempt={rescue.UseActionAttempted}/{rescue.UseActionAccepted}, " +
            $"count={rescue.AttemptCount}/{rescue.AcceptedCount}, confirm-pending={rescue.ConfirmationPending}, " +
            $"confirmed={rescue.MatchConfirmations.TotalConfirmed}/{rescue.SessionConfirmations.TotalConfirmed}, " +
            $"capture/drop={rescue.ConfirmationCaptureCount}/{rescue.ConfirmationDropCount}");
        ImGui.TextWrapped(
            $"Reactive CC: {miracle.Phase}/{miracle.Threat}, action={miracle.CounterActionId}, " +
            $"target={miracle.TargetGameObjectId:X}/" +
            $"{miracle.TargetEntityId:X}, job={miracle.TargetJobId}, remaining={miracle.ThreatRemainingMilliseconds} ms, " +
            $"blocker scales/other={miracle.HardenedScalesPresent}/{miracle.OtherCcProtectionPresent}, " +
            $"range/LoS={miracle.HasNativeRangeAndLineOfSight}, key={miracle.InputKey}, " +
            $"attempt={miracle.UseActionAttempted}/{miracle.UseActionAccepted}, " +
            $"count={miracle.AttemptCount}/{miracle.AcceptedCount}, " +
            $"capture/queue/drop={miracle.CapturedThreatCount}/{miracle.CaptureQueueDepth}/{miracle.DroppedThreatCount}, " +
            $"seen/armed/rejected={miracle.RecognizedThreatCount}/{miracle.ArmedThreatCount}/" +
            $"{miracle.RejectedThreatCount}, waits protection/range/input/priority=" +
            $"{miracle.ProtectionWaitCount}/{miracle.RangeWaitCount}/{miracle.NoInputWaitCount}/" +
            $"{miracle.PriorityWaitCount}, expired={miracle.ExpiredThreatCount}, " +
            $"landed={miracle.ConfirmedLandingCount}, confirm-capture/queue/drop=" +
            $"{miracle.CapturedConfirmationCount}/{miracle.ConfirmationQueueDepth}/{miracle.DroppedConfirmationCount}, " +
            $"last={miracle.LastEvent}, last-opportunity={miracle.LastOpportunity}, " +
            $"cleanse-followup={miracle.CleanseFollowupPhase}, removed=" +
            $"{miracle.CleanseFollowupRemovedStatusId}, team-focus={miracle.CleanseFollowupTeamPressure}, target=" +
            $"{miracle.CleanseFollowupTargetGameObjectId:X}/{miracle.CleanseFollowupTargetEntityId:X}, " +
            $"resilience-seen={miracle.CleanseFollowupResilienceObserved}, signal/promote/cancel=" +
            $"{miracle.CleanseFollowupSignalCount}/{miracle.CleanseFollowupPromotionCount}/" +
            $"{miracle.CleanseFollowupCancellationCount}, cleanse-last={miracle.CleanseFollowupLastEvent}");
        ImGui.TextWrapped(
            $"Monk Earth's Reply: {monk.Phase}/{monk.Decision}, reason={monk.Reason}, trigger={monk.Trigger}, " +
            $"resonance={monk.ResonancePresent}/{monk.ResonanceRemainingMilliseconds} ms, " +
            $"HP={monk.CurrentHp}/{monk.MaximumHp}, adjusted={monk.AdjustedActionId}, " +
            $"priority={monk.HigherPriorityClaimed}, attempt={monk.UseActionAttempted}/{monk.UseActionAccepted}, " +
            $"count={monk.AttemptCount}/{monk.AcceptedCount}");

        ImGui.Separator();
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        ImGui.TextDisabled(
            "Guard cooldown is shown only after this client actually observed that enemy's Guard. Unknown " +
            "cooldowns are never guessed. Only the separate default-off Auto Low-MP Focus setter may fill an empty " +
            "native Focus Target; it never clears, replaces, restores, or retries one. Every other module leaves " +
            "selected hard, soft, and Focus Targets unchanged. Seiton Sense uploads no gameplay data to an external " +
            "service. Near Assist, Near Help, and Far Help may replace only " +
            "the target ID on one armed macro action. The optional CC brake can invalidate only one already incoming, " +
            "enabled action attempt against an exact protected enemy; it adds no action, repeat, or retry. " +
            "Purify, defensive utilities, pressure Sprint, Ally Rescue, reactive counter-CC, Ninja, and Scholar share " +
            "one physical input generation and can initiate at most one exact action attempt, in that priority order. " +
            "Guard after Purify requires a later " +
            "physical generation, and every action-request helper is blocked while your own Guard is active. Monk Earth's " +
            "Reply is a separate automatic follow-up that yields whenever an earlier helper already attempted an action " +
            "in the same update. The separate DRK macro may make one exact Shadowbringer attempt from its authored " +
            "two-line macro but does not join the physical-generation chain or mutate a selected target. Automatic " +
            "action helpers, Auto Low-MP Focus, and the team-visible Attack1 marker are disabled by default. " +
            "Like all third-party modifications, use " +
            "it at your own risk.");
        ImGui.PopTextWrapPos();
        return changed;
    }
}
