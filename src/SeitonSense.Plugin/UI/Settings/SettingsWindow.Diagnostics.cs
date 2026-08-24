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
        var autoGuardProtection = personalStatus.AutoGuardProtectionDiagnostics;
        var recuperate = personalStatus.SmartRecuperateDiagnostics;
        var rescue = personalStatus.AllyRescueDiagnostics;
        var miracle = personalStatus.MiracleInterceptDiagnostics;
        var guardShukuchi = personalStatus.NinjaGuardShukuchiDiagnostics;
        var viper = personalStatus.ViperSerpentTailDiagnostics;
        var castCancellation = personalStatus.HeldCastCancellationDiagnostics;
        var protectionEndRankPresent = miracle.ProtectionEndRankMaximumHp > 0;
        var protectionEndRankPressure = !protectionEndRankPresent
            ? "none"
            : miracle.ProtectionEndRankTeamPressureKnown
                ? miracle.ProtectionEndRankTeamPressure.ToString()
                : "unknown";
        var protectionEndRankHp = protectionEndRankPresent
            ? $"{miracle.ProtectionEndRankCurrentHp}/{miracle.ProtectionEndRankMaximumHp}"
            : "none";
        var protectionEndRankMp = !protectionEndRankPresent
            ? "none"
            : miracle.ProtectionEndRankMpKnown
                ? $"{miracle.ProtectionEndRankCurrentMp}/{miracle.ProtectionEndRankMaximumMp}"
                : "unknown";
        var monk = personalStatus.MonkEarthReplyDiagnostics;
        ImGui.TextWrapped(
            $"Personal statuses={personal.Statuses.Length}, Purify={personal.Purify.Phase}/" +
            $"{personal.Purify.Decision}, cancel={personal.Purify.CancelReason}, " +
            $"trigger={personal.Purify.InputTrigger}, ready={personal.Purify.LocallyReady}, " +
            $"fresh={personal.Purify.FreshGameplayKey}, held={personal.Purify.HeldGameplayKey}, " +
            $"frozen-key={personal.Purify.FrozenKeyCode}, claim={personal.Purify.InputClaimed}, " +
            $"attempt={personal.Purify.UseActionAttempted}/{personal.Purify.UseActionAccepted}, " +
            $"native={personal.Purify.NativeAttemptCount}/{personal.Purify.LastNativeOutcome}, " +
            $"count attempt/rejected/accepted/unknown/soft/retry=" +
            $"{personal.Purify.TotalNativeAttempts}/{personal.Purify.TotalClientRejected}/" +
            $"{personal.Purify.TotalClientAccepted}/{personal.Purify.TotalAcceptanceUnknown}/" +
            $"{personal.Purify.TotalStructuralSoftWaits}/{personal.Purify.TotalNativeRetriesScheduled}, " +
            $"lease={(personal.Purify.FrozenKeyCode > 0 &&
                personal.Purify.Phase != EmergencyPurifyBufferPhase.WaitingForStatus
                    ? "status+key"
                    : "none")}, " +
            $"last={personal.Purify.LastEvent}");
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
            $"Auto-Guard protection: hook={autoGuardProtection.HookAvailable}, " +
            $"armed/status={autoGuardProtection.Armed}/{autoGuardProtection.ExactGuardObserved}, " +
            $"remaining={autoGuardProtection.RemainingMilliseconds} ms, " +
            $"armed/blocked/released={autoGuardProtection.ArmedCount}/" +
            $"{autoGuardProtection.BlockedActionCount}/{autoGuardProtection.ReleasedCount}, " +
            $"last={autoGuardProtection.LastEvent}");
        ImGui.TextWrapped(
            $"Smart Recuperate: {recuperate.Phase}/{recuperate.Decision}/{recuperate.Reason}, " +
            $"action={recuperate.ResolvedActionId}, " +
            $"HP={recuperate.CurrentHp}/{recuperate.MaximumHp}, missing={recuperate.MissingHp}, " +
            $"MP={recuperate.CurrentMp}/{recuperate.MaximumMp}, ready/guard=" +
            $"{recuperate.LocallyReady}/{recuperate.GuardSuppressed}, key={recuperate.HeldGameplayKey}, " +
            $"frozen-key/event={recuperate.FrozenKeyCode}/{recuperate.HealthEventToken}, " +
            $"claim={recuperate.InputClaimed}, native={recuperate.NativeAttemptCount}/" +
            $"{recuperate.LastNativeOutcome}, attempt={recuperate.UseActionAttempted}/" +
            $"{recuperate.UseActionAccepted}, count={recuperate.AttemptCount}/{recuperate.AcceptedCount}, " +
            $"rejected/unknown/soft={recuperate.RejectedCount}/{recuperate.UnknownCount}/" +
            $"{recuperate.SoftWaitCount}, " +
            $"last={recuperate.LastEvent}");
        ImGui.TextWrapped(
            $"Viper Serpentiner Geist: {viper.Phase}/{viper.Decision}/{viper.Reason}, " +
            $"action/generation={viper.ResolvedActionId}/{viper.ExposureGeneration}, " +
            $"spent/reset={viper.ExposureSpent}/{viper.NonFollowUpObservations}, S={viper.EnemySlot}, " +
            $"target={viper.TargetGameObjectId:X}/{viper.TargetEntityId:X}, ready/boundary=" +
            $"{viper.LocallyReady}/{viper.NativeBoundaryReady}, key={viper.HeldGameplayKey}, " +
            $"claim={viper.InputClaimed}, attempt={viper.UseActionAttempted}/{viper.UseActionAccepted}, " +
            $"native={viper.NativeAttemptCount}/{viper.LastNativeOutcome}, " +
            $"count accepted/rejected/unknown/soft={viper.AcceptedCount}/{viper.RejectedCount}/" +
            $"{viper.UnknownCount}/{viper.SoftWaitCount}, last={viper.LastEvent}");
        ImGui.TextWrapped(
            $"Held cast cancellation: enabled={configuration.AllowHeldHelpersToCancelOwnCast}, " +
            $"state={castCancellation.Decision}/{castCancellation.Reason}, " +
            $"cast={castCancellation.CastActionId}, epoch={castCancellation.CastEpochToken}, " +
            $"current-helper={castCancellation.Request?.HelperKind ?? HeldCastCancellationHelperKind.None}, " +
            $"last-helper={castCancellation.LastRequestedIntent?.HelperKind ?? HeldCastCancellationHelperKind.None}, " +
            $"last-action={castCancellation.LastRequestedIntent?.HelperActionId ?? 0}, " +
            $"last-target={castCancellation.LastRequestedIntent?.Target.GameObjectId ?? 0:X}, " +
            $"last-key={castCancellation.LastRequestedIntent?.FrozenKeyCode ?? 0}, " +
            $"last-intent={castCancellation.LastRequestedIntent?.IntentEpochToken ?? 0}, " +
            $"native/last-native={castCancellation.NativeStatus}/{castCancellation.LastNativeStatus}, " +
            $"requested/faulted=" +
            $"{castCancellation.NativeRequestCount}/{castCancellation.NativeFaultCount}, " +
            $"last={castCancellation.LastEvent}");
        ImGui.TextWrapped(
            $"Ally Rescue (confirmation counters are captured exact status-removal ActionEffects): " +
            $"{rescue.Phase}/{rescue.Decision}, cancel={rescue.CancelReason}, " +
            $"trigger={rescue.InputTrigger}, candidates={rescue.CandidateCount}, action={rescue.ActionId}, " +
            $"target={rescue.TargetGameObjectId:X}, status={rescue.TargetStatusId}, ready={rescue.LocallyReady}, " +
            $"key={rescue.FreshGameplayKey}/{rescue.HeldGameplayKey}, claim={rescue.InputClaimed}, " +
            $"attempt={rescue.UseActionAttempted}/{rescue.UseActionAccepted}, " +
            $"count={rescue.AttemptCount}/{rescue.AcceptedCount}, confirm-pending={rescue.ConfirmationPending}, " +
            $"confirmed-cleanses match/session={rescue.MatchConfirmations.TotalConfirmed}/" +
            $"{rescue.SessionConfirmations.TotalConfirmed}, " +
            $"capture/drop={rescue.ConfirmationCaptureCount}/{rescue.ConfirmationDropCount}, " +
            $"last={rescue.LastEvent}");
        ImGui.TextWrapped(
            $"Reactive CC (WHM Miracle; BRD Silent Nocturne; NIN Forked/Fleeting Raiju): " +
            $"{miracle.Phase}/{miracle.Threat}, action={miracle.CounterActionId}, " +
            $"target={miracle.TargetGameObjectId:X}/" +
            $"{miracle.TargetEntityId:X}, job={miracle.TargetJobId}, remaining={miracle.ThreatRemainingMilliseconds} ms, " +
            $"blocker scales/other={miracle.HardenedScalesPresent}/{miracle.OtherCcProtectionPresent}, " +
            $"range/LoS={miracle.HasNativeRangeAndLineOfSight}, key={miracle.InputKey}, " +
            $"claim={miracle.InputClaimed}, " +
            $"attempt={miracle.UseActionAttempted}/{miracle.UseActionAccepted}, " +
            $"count={miracle.AttemptCount}/{miracle.AcceptedCount}, " +
            $"capture/queue/drop={miracle.CapturedThreatCount}/{miracle.CaptureQueueDepth}/{miracle.DroppedThreatCount}, " +
            $"seen/armed/rejected={miracle.RecognizedThreatCount}/{miracle.ArmedThreatCount}/" +
            $"{miracle.RejectedThreatCount}, waits protection/range/input/priority=" +
            $"{miracle.ProtectionWaitCount}/{miracle.RangeWaitCount}/{miracle.NoInputWaitCount}/" +
            $"{miracle.PriorityWaitCount}, expired={miracle.ExpiredThreatCount}, " +
            $"landed/pending={miracle.ConfirmedLandingCount}/{miracle.ConfirmationPendingCount}, " +
            $"confirm-capture/queue/drop=" +
            $"{miracle.CapturedConfirmationCount}/{miracle.ConfirmationQueueDepth}/{miracle.DroppedConfirmationCount}, " +
            $"last={miracle.LastEvent}, last-opportunity={miracle.LastOpportunity}, " +
            $"cleanse-followup tracked/release-ready={miracle.CleanseFollowupTrackedCount}/" +
            $"{miracle.CleanseFollowupReleaseReadyCount}, phase={miracle.CleanseFollowupPhase}, removed=" +
            $"{miracle.CleanseFollowupRemovedStatusId}, team-pressure-sample=" +
            $"{miracle.CleanseFollowupTeamPressure}, target=" +
            $"{miracle.CleanseFollowupTargetGameObjectId:X}/{miracle.CleanseFollowupTargetEntityId:X}, " +
            $"resilience-seen={miracle.CleanseFollowupResilienceObserved}, signal/promote/cancel=" +
            $"{miracle.CleanseFollowupSignalCount}/{miracle.CleanseFollowupPromotionCount}/" +
            $"{miracle.CleanseFollowupCancellationCount}, cleanse-last={miracle.CleanseFollowupLastEvent}");
        ImGui.TextWrapped(
            $"Reactive CC Guard follow-up: tracked/release-ready={miracle.GuardFollowupTrackedCount}/" +
            $"{miracle.GuardFollowupReleaseReadyCount}, target={miracle.GuardFollowupTargetGameObjectId:X}/" +
            $"{miracle.GuardFollowupTargetEntityId:X}, team-pressure-sample={miracle.GuardFollowupTeamPressure}, " +
            $"episode/promote/expired/retired={miracle.GuardFollowupEpisodeCount}/" +
            $"{miracle.GuardFollowupPromotionCount}/{miracle.GuardFollowupExpiredCount}/" +
            $"{miracle.GuardFollowupRetiredCount}, last={miracle.GuardFollowupLastEvent}");
        ImGui.TextWrapped(
            $"Reactive CC protection-end hold/rank (pressure >0 bonus; zero/unknown/stale neutral; then HP/MP/ID): " +
            $"consent={miracle.ProtectionEndHeldConsentActive}/" +
            $"{miracle.ProtectionEndHeldConsentKey}, reserved={miracle.ProtectionEndReservedKey}, " +
            $"expected-end={miracle.ProtectionEndExpectedRemainingMilliseconds} ms, " +
            $"last-winner pressure={protectionEndRankPressure}, " +
            $"HP={protectionEndRankHp}, trusted-MP={protectionEndRankMp}");
        ImGui.TextWrapped(
            $"NIN Guard-Shukuchi: {guardShukuchi.Decision}/{guardShukuchi.Reason}, " +
            $"ready/action={guardShukuchi.LocallyReady}/{guardShukuchi.ResolvedActionId}, " +
            $"candidates={guardShukuchi.CandidateCount}, S={guardShukuchi.EnemySlot}, " +
            $"target={guardShukuchi.TargetGameObjectId:X}/{guardShukuchi.TargetEntityId:X}, " +
            $"HP={guardShukuchi.RevalidatedCurrentHp}/{guardShukuchi.RevalidatedMaximumHp}, " +
            $"guard/distance={guardShukuchi.RevalidatedGuardActive}/{guardShukuchi.RevalidatedDistanceYalms:0.00}, " +
            $"pressure={guardShukuchi.PressureKnown}/{guardShukuchi.TeamTargetCount}, " +
            $"key={guardShukuchi.HeldGameplayKey}, claim={guardShukuchi.InputClaimed}, " +
            $"attempt/accepted/targeted={guardShukuchi.UseActionAttempted}/" +
            $"{guardShukuchi.UseActionAccepted}/{guardShukuchi.HardTargetConfirmed}, " +
            $"count={guardShukuchi.AttemptCount}/{guardShukuchi.AcceptedCount}/" +
            $"{guardShukuchi.TargetConfirmedCount}, last={guardShukuchi.LastEvent}");
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
            "cooldowns are never guessed. The separate default-off Auto Low-MP Focus setter may fill an empty " +
            "native Focus Target; it never clears, replaces, restores, or retries one. The retired Combat Frames add no " +
            "click or mouseover mutation path. Opt-in Smart Tab replaces only the nested native forward world-target " +
            "cycle inside FFXIV's original target handler, after its input gates, and may set one frozen exact CC DPS enemy as the visible hard " +
            "target after one revalidation and readback; toggle-off and unrelated input remain vanilla. An explicitly " +
            "enabled NIN Guard-Shukuchi may separately " +
            "set its exact jumped-to enemy once after client acceptance. Every other module leaves selected hard, soft, " +
            "Focus, and mouseover targets unchanged. Seiton Sense uploads no gameplay data to an external service. " +
            "Smart Action, Near Assist, Near Help, and Far Help may replace only " +
            "the target ID on one armed macro action. The optional CC brake can invalidate only one already incoming, " +
            "enabled action attempt against an exact protected enemy; it adds no action, repeat, or retry. " +
            "The current request order is Purify > NIN Seiton / VPR Serpentiner Geist > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > " +
            "SCH Critical Strategy > DRK Hiebsprung > Smart Recuperate > generic Guard > pressure Sprint > event " +
            "Kardia > event Monk. The job-specific physical-hold helpers share the second tier; NIN Seiton and VPR Serpentiner Geist are first for their jobs, " +
            "and reactive stays before BRD/WHM cleanse because its windows are shorter. Kardia still requires its separate " +
            "accepted-Eukrasia trigger. Viper instead polls only FFXIV's currently transformed Serpent's Tail carrier; " +
            "it requires no preceding-action proof and never changes a target or cancels a cast. " +
            "One continuous physical hold may authorize later distinct exact held episodes, including Guard after " +
            "Purify; only one held native boundary is allowed per framework frame. Every action-request helper is " +
            "blocked while your own Guard is active. The " +
            "separate DRK macro may make one exact Shadowbringer attempt from its authored " +
            "two-line macro but does not join the physical-generation chain or mutate a selected target. Automatic " +
            "action helpers, Auto Low-MP Focus, and the team-visible Attack1 marker are disabled by default. " +
            "Like all third-party modifications, use " +
            "it at your own risk.");
        ImGui.PopTextWrapPos();
        return changed;
    }
}
