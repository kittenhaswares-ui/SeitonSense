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
        var astrologianOrbis = personalStatus.AstrologianHarmonicOrbisDiagnostics;
        var emergencyTeleport = personalStatus.EmergencyTeleportDiagnostics;
        var rescue = personalStatus.AllyRescueDiagnostics;
        var miracle = personalStatus.MiracleInterceptDiagnostics;
        var samurai = personalStatus.SamuraiReactiveDiagnostics;
        var samuraiCapture = personalStatus.SamuraiReactiveCaptureDiagnostics;
        var samuraiMetadata = personalStatus.SamuraiReactiveMetadata;
        var guardShukuchi = personalStatus.NinjaGuardShukuchiDiagnostics;
        var viper = personalStatus.ViperSerpentTailDiagnostics;
        var gunbreaker = personalStatus.GunbreakerContinuationDiagnostics;
        var shadowbringer = personalStatus.DarkKnightShadowbringerDiagnostics;
        var monkCombo = personalStatus.MonkHeldComboDiagnostics;
        var castCancellation = personalStatus.HeldCastCancellationDiagnostics;
        var criticalCoordination = personalStatus.CriticalUtilityCoordinationDiagnostics;
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
            $"Shared ActionEffect hook / MCH LB queue: hook={mchLimitBreak.CaptureRunning}, " +
            $"mch-queue={mchLimitBreak.QueueDepth}, mch-accepted={mchLimitBreak.AcceptedWarnings}, " +
            $"mch-active={mchLimitBreak.WarningActive}, shared-errors={mchLimitBreak.CaptureErrors}, " +
            $"mch-drops={mchLimitBreak.DroppedWarnings}");
        ImGui.TextWrapped(
            $"PvP latency helper: enabled={configuration.EnablePvpLatencyResponseHelper}, " +
            $"window={configuration.PvpLatencyResponseWindowMilliseconds} ms, " +
            $"new-intent-native-budget={HeldActionRetryRules.CurrentMaximumNativeAttempts}, " +
            $"internal eligible/claimed={criticalCoordination.IntegratedEligible}/" +
            $"{criticalCoordination.IntegratedClaimed}, legacy IPC eligible/claimed=" +
            $"{criticalCoordination.Eligible}/{criticalCoordination.Claimed}");
        ImGui.TextWrapped(
            $"General smart buffer: enabled={configuration.EnableSmartActionBuffer}, " +
            $"window={configuration.SmartActionBufferWindowMilliseconds} ms, learning=" +
            $"{configuration.ShowBufferLearningWindow}/locked={configuration.BufferLearningWindowLocked}");
        ImGui.TextWrapped(
            $"Native standard-hotbar Turbo: enabled={configuration.EnableNativeHotbarTurbo}, " +
            $"initial/repeat={configuration.TurboInitialDelayMilliseconds}/" +
            $"{configuration.TurboRepeatIntervalMilliseconds} ms, " +
            $"outside-combat={configuration.TurboOutsideCombat}");
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
            $"Auto-Guard popup={defense.AutoGuardPopup?.Token ?? 0}/" +
            $"{Math.Max(0, (defense.AutoGuardPopup?.EndsAtMilliseconds ?? 0) - Environment.TickCount64)} ms, " +
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
            $"AST held Near Help: metadata={personalStatus.AstrologianHarmonicOrbisMetadataVerified}, " +
            $"state={astrologianOrbis.Phase}/{astrologianOrbis.Decision}, action/adjusted=" +
            $"{astrologianOrbis.ResolvedActionId}/{astrologianOrbis.AdjustedDoubleCastActionId}, " +
            $"candidates={astrologianOrbis.CandidateCount}, P={astrologianOrbis.PartySlot}, target=" +
            $"{astrologianOrbis.TargetGameObjectId:X}/{astrologianOrbis.TargetEntityId:X}, HP=" +
            $"{astrologianOrbis.TargetCurrentHp}/{astrologianOrbis.TargetMaximumHp}, pressure-ranking=" +
            $"{astrologianOrbis.PreferIncomingPressure}, Double Cast ready/charges=" +
            $"{astrologianOrbis.DoubleCastWasReadyBeforeBase}/" +
            $"{astrologianOrbis.DoubleCastChargesBeforeBase}, transition=" +
            $"{astrologianOrbis.TransitionRemainingMilliseconds} ms, ready/boundary=" +
            $"{astrologianOrbis.LocallyReady}/{astrologianOrbis.NativeBoundaryReady}, key=" +
            $"{astrologianOrbis.HeldGameplayKey}, claim={astrologianOrbis.InputClaimed}, native=" +
            $"{astrologianOrbis.NativeAttemptCount}/{astrologianOrbis.LastNativeOutcome}, attempt=" +
            $"{astrologianOrbis.UseActionAttempted}/{astrologianOrbis.UseActionAccepted}, base=" +
            $"{astrologianOrbis.BaseAttemptCount}/{astrologianOrbis.BaseAcceptedCount}, double=" +
            $"{astrologianOrbis.FollowUpAttemptCount}/{astrologianOrbis.FollowUpAcceptedCount}, " +
            $"last={astrologianOrbis.LastEvent}");
        ImGui.TextWrapped(
            $"Emergency Teleport: {emergencyTeleport.Decision}/{emergencyTeleport.Reason}, " +
            $"danger={emergencyTeleport.Danger}, action={emergencyTeleport.ResolvedActionId}, " +
            $"HP={emergencyTeleport.CurrentHp}/{emergencyTeleport.MaximumHp}, " +
            $"MP={emergencyTeleport.CurrentMp}/{emergencyTeleport.MaximumMp}, pressure=" +
            $"{emergencyTeleport.DirectPressureKnown}/{emergencyTeleport.DirectEnemyCount}, " +
            $"episode={emergencyTeleport.EpisodeToken}/{emergencyTeleport.EpisodeOpen}/" +
            $"{emergencyTeleport.EpisodeSpent}, candidates={emergencyTeleport.CandidateCount}, " +
            $"P={emergencyTeleport.PartySlot}, target={emergencyTeleport.TargetGameObjectId:X}/" +
            $"{emergencyTeleport.TargetEntityId:X}, distance={emergencyTeleport.TravelDistanceYalms:0.0}, " +
            $"nearby/clearance={emergencyTeleport.NearbyEnemyCount}/" +
            $"{emergencyTeleport.MinimumEnemyClearanceYalms:0.0}, key={emergencyTeleport.HeldGameplayKey}, " +
            $"claim={emergencyTeleport.InputClaimed}, attempt={emergencyTeleport.UseActionAttempted}/" +
            $"{emergencyTeleport.NativeOutcome}, count={emergencyTeleport.AttemptCount}/" +
            $"{emergencyTeleport.AcceptedCount}, last={emergencyTeleport.LastEvent}");
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
            $"GNB Continuation: {gunbreaker.Phase}/{gunbreaker.Decision}/{gunbreaker.Reason}, " +
            $"action/proc/generation={gunbreaker.ResolvedActionId}/{gunbreaker.ResolvedProcStatusId}/" +
            $"{gunbreaker.ExposureGeneration}, spent={gunbreaker.ExposureSpent}, S={gunbreaker.EnemySlot}, " +
            $"target={gunbreaker.TargetGameObjectId:X}/{gunbreaker.TargetEntityId:X}, ready/boundary=" +
            $"{gunbreaker.LocallyReady}/{gunbreaker.NativeBoundaryReady}, key={gunbreaker.HeldGameplayKey}, " +
            $"claim={gunbreaker.InputClaimed}, attempt={gunbreaker.UseActionAttempted}/{gunbreaker.UseActionAccepted}, " +
            $"native={gunbreaker.NativeAttemptCount}/{gunbreaker.LastNativeOutcome}, last={gunbreaker.LastEvent}");
        ImGui.TextWrapped(
            $"DRK Shadowbringer: {shadowbringer.Decision}/{shadowbringer.Reason}/{shadowbringer.Opportunity}, " +
            $"action={shadowbringer.ResolvedAdjustedActionId}, dark-arts={shadowbringer.DarkArtsGeneration}/" +
            $"{shadowbringer.DarkArtsExposed}/{shadowbringer.DarkArtsSpent}, fallback=" +
            $"{shadowbringer.FallbackGeneration}/{shadowbringer.FallbackEligible}/{shadowbringer.FallbackSpent}, " +
            $"blackblood={shadowbringer.BlackbloodPreservationEnabled}/" +
            $"{shadowbringer.BlackbloodMetadataVerified}/{shadowbringer.BlackbloodStatusPresent}/" +
            $"{shadowbringer.BlackbloodGatePhase}/{shadowbringer.BlackbloodAbsentObservations}/" +
            $"{shadowbringer.BlackbloodLastObservedAtMilliseconds}/" +
            $"{shadowbringer.BlackbloodDispatchAllowed}, " +
            $"cadence={shadowbringer.AutomaticCadenceReady}/" +
            $"{shadowbringer.AutomaticCadenceRemainingMilliseconds} ms/" +
            $"last={shadowbringer.LastAutomaticBoundaryAtMilliseconds}, " +
            $"pressure={shadowbringer.PressureKnown}/{shadowbringer.IncomingPressure}/" +
            $"{shadowbringer.PressureAgeMilliseconds} ms/den-zero=" +
            $"{shadowbringer.WolvesDenTestPressureAssumed}, ready={shadowbringer.ActionLocallyReady}/" +
            $"{shadowbringer.NativeBoundaryReady}, deferred={shadowbringer.CanRunDeferredSafeFallback}/" +
            $"{shadowbringer.DeferredFrameToken}, S={shadowbringer.EnemySlot}, target=" +
            $"{shadowbringer.TargetGameObjectId:X}/{shadowbringer.TargetEntityId:X}, key=" +
            $"{shadowbringer.HeldGameplayKey}, claim={shadowbringer.InputClaimed}, attempt=" +
            $"{shadowbringer.UseActionAttempted}/{shadowbringer.UseActionAccepted}, last={shadowbringer.LastEvent}");
        ImGui.TextWrapped(
            $"Monk held combo: {monkCombo.Phase}/{monkCombo.Decision}/{monkCombo.Reason}, " +
            $"combo/pending={monkCombo.ResolvedComboActionId}/{monkCombo.PendingActionId}/" +
            $"{monkCombo.PendingPurpose}, S={monkCombo.EnemySlot}, target=" +
            $"{monkCombo.TargetGameObjectId:X}/{monkCombo.TargetEntityId:X}, proof pressure/fire=" +
            $"{monkCombo.PressurePointConfirmed}/{monkCombo.FireResonanceConfirmed}, boundary=" +
            $"{monkCombo.NativeBoundaryReady}, route-resolver={monkCombo.NativeRouteResolverReady}, " +
            $"key={monkCombo.HeldGameplayKey}, claim=" +
            $"{monkCombo.InputClaimed}, attempt={monkCombo.UseActionAttempted}/" +
            $"{monkCombo.UseActionAccepted}, native={monkCombo.NativeAttemptCount}/" +
            $"{monkCombo.LastNativeOutcome}, last={monkCombo.LastEvent}");
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
            $"Reactive CC (WHM/BRD/NIN/PLD/RDM/BLM): " +
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
            $"main-GCD-late={miracle.MainGcdLateReservationActive}/" +
            $"{miracle.MainGcdLateRemainingMilliseconds} ms, " +
            $"last-winner pressure={protectionEndRankPressure}, " +
            $"HP={protectionEndRankHp}, trusted-MP={protectionEndRankMp}");
        ImGui.TextWrapped(
            $"SAM reactive: counter={samurai.CounterPhase}/{samurai.ProtectionKind}, " +
            $"protection-seen={samurai.ProtectionObserved}, S={samurai.EnemySlot}, target=" +
            $"{samurai.TargetGameObjectId:X}/{samurai.TargetEntityId:X}, job={samurai.TargetJobId}, " +
            $"key={samurai.ReservedKey}, claim={samurai.InputClaimed}, last-action/outcome=" +
            $"{samurai.LastAttemptedActionId}/{samurai.LastAttemptOutcome}, attempts Soten/Mineuchi/Zan=" +
            $"{samurai.SotenAttemptCount}/{samurai.MineuchiAttemptCount}/{samurai.ZantetsukenAttemptCount}, " +
            $"accepted={samurai.AcceptedCount}, own-Zan enabled/metadata/phase=" +
            $"{samurai.ZantetsukenHeldHelperEnabled}/{samurai.ZantetsukenMetadataVerified}/" +
            $"{samurai.ZantetsukenPhase}, mirror queue/capture/drop=" +
            $"{samurai.ProtectionSignalQueueDepth}/{samurai.CapturedProtectionSignalCount}/" +
            $"{samurai.DroppedProtectionSignalCount}, timing Soten/Mineuchi samples=" +
            $"{samurai.SotenTimingSampleCount}/{samurai.MineuchiTimingSampleCount}, " +
            $"lead Soten/Mineuchi={samurai.PredictiveSotenLeadMilliseconds}/" +
            $"{samurai.PredictiveMineuchiLeadMilliseconds} ms, Soten-effect=" +
            $"{samurai.SotenArrivalConfirmed}, shared hook/protection q-c-d/effect q-c-d/gen=" +
            $"{samuraiCapture.CaptureRunning}/{samuraiCapture.QueueDepth}/" +
            $"{samuraiCapture.CapturedSignals}/{samuraiCapture.DroppedSignals}/" +
            $"{samuraiCapture.ActionEffectQueueDepth}/{samuraiCapture.CapturedActionEffects}/" +
            $"{samuraiCapture.DroppedActionEffects}/{samuraiCapture.FeatureGeneration}, metadata counter/Zan/dummy=" +
            $"{samuraiMetadata.CounterCcVerified}/{samuraiMetadata.ZantetsukenWorkflowVerified}/" +
            $"{samuraiMetadata.WolvesDenStrikingDummyVerified}, last={samurai.LastEvent}");
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
            "The current request order is Purify > AST held Near Help > SAM staged counter-CC / Zantetsuken > NIN Seiton > VPR Serpentiner Geist > GNB Continuation > reactive counter-CC > Ally Rescue > PLD Guardian > NIN Guard-Shukuchi > " +
            "SCH Critical Strategy > DRK Shadowbringer (Dark Arts) > DRK Hiebsprung > DRK Shadowbringer (safe fallback) > Monk combo > Smart Recuperate > Emergency Teleport > generic Guard > pressure Sprint > event " +
            "Kardia > event Monk. The job-specific physical-hold helpers use that deterministic order; AST runs directly after Purify and SAM follows AST, " +
            "and reactive stays before BRD/WHM cleanse because its windows are shorter. Kardia still requires its separate " +
            "accepted-Eukrasia trigger. Viper instead polls only FFXIV's currently transformed Serpent's Tail carrier; " +
            "it requires no preceding-action proof, uses the shared Smart Action target policy in CC, and never visibly " +
            "changes a target or cancels a cast. " +
            "One continuous physical hold may authorize later distinct exact held episodes, including Guard after " +
            "Purify; only one held native boundary is allowed per framework frame. Every action-request helper is " +
            "blocked while your own Guard is active. Held DRK Shadowbringer joins the physical-generation chain at " +
            "separate Dark Arts and safe-fallback positions without mutating a selected target. Automatic " +
            "action helpers, Auto Low-MP Focus, and the team-visible Attack1 marker are disabled by default. " +
            "Like all third-party modifications, use " +
            "it at your own risk.");
        ImGui.PopTextWrapPos();
        return changed;
    }
}
