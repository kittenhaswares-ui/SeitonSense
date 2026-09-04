using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private uint CurrentSettingsJobId
    {
        get
        {
            try { return playerState.ClassJob.RowId; }
            catch { return 0; }
        }
    }

    private void DrawJobToolsFilter()
    {
        ImGui.Spacing();
        ImGui.TextUnformatted($"Current job: {JobLabel(CurrentSettingsJobId)}");
        ImGui.Checkbox("Show all jobs##SeitonJobFilter", ref showAllJobTools);
        ImGui.TextWrapped("This only filters the settings page. Your saved options stay unchanged.");
        if (!showAllJobTools && CurrentSettingsJobId is not (0 or 19 or 20 or 23 or 28 or 30 or 32 or 33 or 34 or 35 or 37 or 40 or 41))
            ImGui.TextWrapped("This job has no dedicated section here. Shared survival and counter-CC options are in Action Helpers.");
        if (ImGui.SmallButton("Open shared Action Helpers")) selectedPage = SettingsPage.ActionHelpers;
    }

    private bool DrawJobSectionHeader(uint jobId, string label)
    {
        var currentJob = CurrentSettingsJobId;
        if (!HelperStatusPresentationRules.ShowJob(currentJob, jobId, showAllJobTools)) return false;
        ImGui.Spacing();
        ImGui.Separator();
        return ImGui.CollapsingHeader(label,
            currentJob == jobId ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
    }

    private void DrawHelperStatusOverview()
    {
        var job = CurrentSettingsJobId;
        var context = tracker.Diagnostics;
        var supported = context.IsCrystallineConflict || context.IsWolvesDen && configuration.EnableWolvesDenTesting;
        var defense = personalStatus.DefensiveUtilityDiagnostics;
        var recup = personalStatus.SmartRecuperateDiagnostics;
        // Recuperate's legacy GuardSuppressed flag also includes Ninja stealth.
        // It must not make every other helper appear blocked by Guard.
        var guard = defense.GuardActive || defense.GuardPropagationLatchActive;
        ImGui.TextUnformatted($"Helper Status — {JobLabel(job)}");
        ImGui.TextWrapped("A quick look at what is enabled and what it is waiting for. Auto needs no key; Hold needs gameplay input such as WASD.");
        ImGui.TextDisabled(context.IsCrystallineConflict ? "Mode: Crystalline Conflict" : context.IsWolvesDen ? "Mode: Wolves' Den" : "Mode: outside supported PvP");
        if (ImGui.SmallButton("Action settings")) selectedPage = SettingsPage.ActionHelpers;
        ImGui.SameLine();
        if (ImGui.SmallButton("My job settings")) selectedPage = SettingsPage.JobTools;
        ImGui.Spacing();

        if (!ImGui.BeginTable("##SeitonSimpleHelperStatus", 3,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp)) return;
        ImGui.TableSetupColumn("Helper", ImGuiTableColumnFlags.WidthFixed, 155f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 84f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("What it is waiting for", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        void Row(string name, string mode, bool enabled, string reason, string waiting,
            bool attempted = false, bool accepted = false, long attempts = 0, long accepts = 0,
            bool ccOnly = false)
        {
            var view = HelperStatusPresentationRules.Describe(configuration.Enabled && enabled,
                ccOnly ? context.IsCrystallineConflict : supported, guard, accepted, attempted, reason, waiting);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextWrapped(name);
            ImGui.TextDisabled(mode);
            ImGui.TableSetColumnIndex(1);
            var color = view.Tone switch
            {
                HelperStatusTone.Accepted => new Vector4(0.4f, 0.9f, 0.62f, 1f),
                HelperStatusTone.Attention => new Vector4(1f, 0.65f, 0.35f, 1f),
                HelperStatusTone.Paused => new Vector4(0.95f, 0.8f, 0.45f, 1f),
                _ => new Vector4(0.76f, 0.8f, 0.85f, 1f),
            };
            ImGui.TextColored(color, view.State);
            ImGui.TableSetColumnIndex(2);
            ImGui.TextWrapped(view.Detail);
            if (attempts > 0) ImGui.TextDisabled($"Requests: {accepts} accepted / {attempts} attempted");
        }

        var purify = personalStatus.Snapshot.Purify;
        var autoPuri = configuration.EnableAutomaticPurify || configuration.EnableDefensiveUtilities && configuration.GuardOnStunPressure;
        Row("Purify", autoPuri ? "Auto" : "Input", autoPuri || configuration.ExperimentalPurifyOnNextKey,
            purify.CancelReason != EmergencyPurifyBufferCancelReason.None ? purify.CancelReason.ToString() : purify.Phase.ToString(),
            "Watching for a CC status selected in your Purify settings.", purify.UseActionAttempted,
            purify.UseActionAccepted, purify.TotalNativeAttempts, purify.TotalClientAccepted);
        Row("Recuperate", configuration.EnableAutomaticRecuperate ? "Auto" : "Hold",
            configuration.EnableAutomaticRecuperate || configuration.EnableSmartRecuperateOnHeldKey,
            recup.GuardSuppressed && !guard ? "RecoveryProtected" : recup.Reason.ToString(),
            "Watching HP and MP against your healing settings.",
            recup.UseActionAttempted, recup.UseActionAccepted, recup.AttemptCount, recup.AcceptedCount);
        var guardRequest = defense.Action == DefensiveUtilityActionKind.Guard;
        Row("Auto Guard", "Auto", configuration.EnableDefensiveUtilities && configuration.GuardOnStunPressure,
            string.Empty, "Watching for the configured danger or post-Purify opportunity.",
            guardRequest && defense.UseActionAttempted, guardRequest && defense.UseActionAccepted, ccOnly: true);
        var sprint = personalStatus.SmartSprintDiagnostics;
        Row("Idle Sprint", "Hold", configuration.EnableIdleSmartSprintOnHeldKey,
            !sprint.MetadataVerified ? "MetadataUnverified" : string.Empty,
            sprint.SprintActive ? "Sprint is already active." :
            sprint.IdleEpisodeSpent ? "Already handled this idle period. Waiting for your next action." :
            sprint.HeldGameplayKey == VirtualKey.NO_KEY ? "Hold a gameplay key such as WASD." :
            $"Waiting for {configuration.SmartSprintInactivityMilliseconds / 1000f:0.#} seconds without an action-bar action. Movement does not reset this timer.",
            sprint.Attempted, sprint.Accepted);

        if (job == 33)
        {
            var a = personalStatus.AstrologianHarmonicOrbisDiagnostics;
            var reason = !personalStatus.AstrologianHarmonicOrbisMetadataVerified ? "MetadataUnverified" :
                a.Phase == AstrologianHarmonicOrbisProbePhase.Waiting && a.HeldGameplayKey == VirtualKey.NO_KEY ? "NoHeldGameplayKey" : string.Empty;
            Row("Orbis + Double Cast", "Hold", configuration.EnableAstrologianHarmonicOrbisOnHeldKey, reason,
                a.Phase is AstrologianHarmonicOrbisProbePhase.AwaitingDoubleCast or AstrologianHarmonicOrbisProbePhase.FollowUpBuffered
                    ? "Keeping the same ally for Double Cast; waiting for the follow-up to be usable."
                    : $"Watching for a reachable ally at {AstrologianHarmonicOrbisRules.MaximumTargetHealthPercent}% HP or lower and an Orbis charge.",
                a.UseActionAttempted, a.UseActionAccepted, a.BaseAttemptCount + a.FollowUpAttemptCount, a.BaseAcceptedCount + a.FollowUpAcceptedCount);
        }
        if (job == 35)
        {
            var a = personalStatus.RedMageGuardEngageDiagnostics;
            Row("Guard engage", "Hold", configuration.EnableRedMageGuardEngageOnHeldKey, a.Reason.ToString(),
                "Watching for fresh enemy Guard, a ready melee starter, and your HP/MP limits.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount);
        }
        if (job == 19)
        {
            var guardianRequest = defense.Action == DefensiveUtilityActionKind.Guardian;
            Row("Guardian", "Input", configuration.PaladinGuardianLowAlly,
                string.Empty, "Watching for a critical or focused ally; your own Guard must also be ready.",
                guardianRequest && defense.UseActionAttempted, guardianRequest && defense.UseActionAccepted, ccOnly: true);
        }
        if (job == 32)
        {
            var a = personalStatus.DarkKnightShadowbringerDiagnostics;
            Row("Shadowbringer", "Hold", configuration.EnableDarkKnightShadowbringerOnHeldKey, a.Reason.ToString(),
                !a.BlackbloodDispatchAllowed ? "Waiting until Blackblood is spent or expires." :
                !a.AutomaticCadenceReady ? "Waiting for the 1.8-second automatic use interval." :
                "Watching for Dark Arts, or your HP and enemy-focus limits.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount);
            var p = personalStatus.DarkKnightPlungeDiagnostics;
            Row("Hiebsprung", "Hold", configuration.EnableDarkKnightPlungeOnHeldKey, p.Reason.ToString(),
                $"Watching for an enemy at {DarkKnightPlungeRules.MaximumHpPercent}% HP or lower within {DarkKnightPlungeRules.MaximumCenterDistanceYalms:0.#} yalms.",
                p.UseActionAttempted, p.UseActionAccepted, p.AttemptCount, p.AcceptedCount, ccOnly: true);
        }
        if (job == 30)
        {
            var a = personalStatus.NinjaSeitonDiagnostics;
            Row("Seiton", "Auto", configuration.EnableNinjaSeitonOnHeldGameplayKey, a.Reason.ToString(),
                "Watching for ready Seiton and a reachable, unprotected enemy below 50% HP.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount);
            var s = personalStatus.NinjaGuardShukuchiDiagnostics;
            Row("Guard Shukuchi", "Hold", configuration.EnableNinjaGuardShukuchiOnHeldGameplayKey, s.Reason.ToString(),
                "Watching for a low-HP enemy using Guard.", s.UseActionAttempted, s.UseActionAccepted, s.AttemptCount, s.AcceptedCount, ccOnly: true);
        }
        if (job == 34)
        {
            var a = personalStatus.SamuraiReactiveDiagnostics;
            Row("Zantetsuken", "Auto", configuration.EnableSamuraiZantetsukenOnHeldKey,
                a.ZantetsukenMetadataVerified ? string.Empty : "MetadataUnverified",
                "Watching for your Kuzushi. After the first mark, collection lasts 0.5 seconds.");
            Row("Soten + Mineuchi", configuration.ReactiveCcOnHeldKey ? "Hold / Input" : "Input",
                configuration.EnableReactiveCcUtilities && configuration.ReactiveCcSamuraiSotenMineuchi,
                !configuration.ReactiveCcAfterEnemyPurify && !configuration.ReactiveCcAfterEnemyGuard ? "TriggerModeDisabled" : string.Empty,
                "Watching for enemy Purify or Guard to end, then timing the dash and stun.");
        }
        if (job == 41)
        {
            var a = personalStatus.ViperSerpentTailDiagnostics;
            Row("Serpent's Tail", "Hold", configuration.EnableViperSerpentTailOnHeldKey, a.Reason.ToString(),
                "Watching for an available Serpent's Tail follow-up and a suitable enemy.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount);
        }
        if (job == 37)
        {
            var a = personalStatus.GunbreakerContinuationDiagnostics;
            Row("Continuation", "Hold", configuration.EnableGunbreakerContinuationOnHeldKey, a.Reason.ToString(),
                "Watching for a Continuation proc and a suitable enemy.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount);
        }
        if (job == 28)
        {
            var a = personalStatus.ScholarCriticalStrategyDiagnostics;
            Row("Critical Strategy", "Hold", configuration.EnableScholarCriticalStrategyOnHeldKey, a.Reason.ToString(),
                "Watching for a reachable enemy using Guard.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount, ccOnly: true);
        }
        if (job == 40)
        {
            var a = personalStatus.SmartKardiaDiagnostics;
            Row("Smart Kardia", "After Eukrasia", configuration.EnableSageKardiaAfterEukrasia, a.Reason.ToString(),
                "Watching for your accepted Eukrasia trigger and a suitable ally.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount, ccOnly: true);
        }
        if (job == 20)
        {
            var a = personalStatus.MonkHeldComboDiagnostics;
            Row("Monk combo", "Hold", configuration.EnableMonkHeldComboOnHeldKey, a.Reason.ToString(),
                "Watching for the next ready combo action and a reachable enemy.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount);
            var e = personalStatus.MonkEarthReplyDiagnostics;
            Row("Earth's Reply", "Event", configuration.EnableMonkEarthReplyHelper, e.Reason.ToString(),
                "Watching Earth's Resonance and your configured heal trigger.", e.UseActionAttempted, e.UseActionAccepted, e.AttemptCount, e.AcceptedCount);
        }
        if (job is 23 or 24)
        {
            var a = personalStatus.AllyRescueDiagnostics;
            Row(job == 23 ? "Paean cleanse" : "Aquaveil cleanse", "Input", configuration.ExperimentalAllyRescueOnNextKey,
                a.CancelReason.ToString(), "Watching for a party member with a supported CC status.",
                a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount, ccOnly: true);
        }
        if (job is 19 or 23 or 24 or 25 or 30 or 35)
        {
            var a = personalStatus.MiracleInterceptDiagnostics;
            var actionEnabled = job switch
            {
                19 => configuration.ReactiveCcPaladinIntervene,
                25 => configuration.ReactiveCcBlackMageFrostStar,
                35 => configuration.ReactiveCcRedMageResolution || configuration.ReactiveCcRedMageViceOfThorns,
                _ => true,
            };
            Row(job == 23 ? "Silent Nocturne" : job == 24 ? "Miracle of Nature" : "Counter-CC",
                configuration.ReactiveCcOnHeldKey ? "Hold / Input" : "Input",
                configuration.EnableReactiveCcUtilities && actionEnabled,
                a.OtherCcProtectionPresent ? "OtherProtection" : a.TargetGameObjectId != 0 && !a.HasNativeRangeAndLineOfSight ? "RangeOrLineOfSight" : string.Empty,
                "Watching the enabled enemy actions and immunity-ending opportunities.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount);
        }
        if (job == 23)
        {
            var a = personalStatus.BardRepellingShotDiagnostics;
            Row("Mannstopper", "Auto", configuration.EnableBardRepellingShotProximityHelper, a.Reason.ToString(),
                "Watching for a nearby enemy who can be crowd-controlled.", a.UseActionAttempted, a.UseActionAccepted, a.AttemptCount, a.AcceptedCount);
        }
        if (job is 20 or 25 or 40 or 41)
        {
            var a = personalStatus.EmergencyTeleportDiagnostics;
            Row("Emergency Teleport", "Hold", configuration.EnableEmergencyTeleportOnHeldKey, a.Reason.ToString(),
                "Watching your danger limits and looking for a safe, distant ally.", a.UseActionAttempted,
                a.NativeOutcome == ClientActionAttemptOutcome.ClientAccepted, a.AttemptCount, a.AcceptedCount);
        }
        ImGui.EndTable();
        ImGui.Spacing();
        ImGui.TextWrapped("This is the latest helper snapshot, not a combat log. Request counters do not prove a heal, hit, or save landed. Detailed reasons remain below.");
        if (job == 34) DrawSamuraiInputStatus();
    }

    private void DrawSamuraiInputStatus()
    {
        var status = personalStatus.SamuraiCastInputStatus;
        var enabled = configuration.Enabled && configuration.EnableSmartActionMacro;
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("/seitonsam cast protection");
        ImGui.TextWrapped(!enabled ? "Off — enable Smart Action in Macro Helpers." :
            status.ProtectedCastActive ? "Your protected cast is recognized right now." :
            status.RequestsInFlight > 0 ? "Your cast request is being checked." :
            "Waiting for a protected cast started through /seitonsam.");
        ImGui.TextWrapped(!status.CastMetadataVerified ? "Cast information is not ready." :
            status.GameplayMovementHooksReady ? "The gameplay movement blocker is available." :
            "The gameplay movement blocker is unavailable; movement protection cannot be relied on.");
        var movement = status.Movement;
        ImGui.TextWrapped($"Recognized cast requests: {status.AcceptedOwnedCasts}. Blocked movement reads: " +
            $"keys {movement.SuppressedDigitalReads}, gameplay controls {movement.SuppressedControlReads}, autorun {movement.SuppressedAutorunReads}.");
        ImGui.TextDisabled("These counters show observed input only, not proof that every mouse/controller path works.");
        if (movement.OwnershipReadFailures > 0)
            ImGui.TextWrapped("Some input checks could not confirm the protected cast. Those checks left movement unchanged.");
    }

    private static string JobLabel(uint jobId) => jobId switch
    {
        19 => "Paladin", 20 => "Monk", 21 => "Warrior", 22 => "Dragoon", 23 => "Bard",
        24 => "White Mage", 25 => "Black Mage", 27 => "Summoner", 28 => "Scholar", 30 => "Ninja",
        31 => "Machinist", 32 => "Dark Knight", 33 => "Astrologian", 34 => "Samurai", 35 => "Red Mage",
        37 => "Gunbreaker", 38 => "Dancer", 39 => "Reaper", 40 => "Sage", 41 => "Viper", 42 => "Pictomancer",
        _ => "No supported job selected",
    };
}
