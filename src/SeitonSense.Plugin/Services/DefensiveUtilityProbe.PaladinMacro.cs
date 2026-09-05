using Dalamud.Game.ClientState.Objects.SubKinds;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed partial class DefensiveUtilityProbe
{
    internal string PaladinMacroLastEvent { get; private set; } = "Not used";

    // One explicit command, one freshly selected party member, at most one native
    // request. This does not create a held-key lease or a delayed retry.
    internal bool TryUsePaladinGuardianMacro(IPlayerCharacter localPlayer, long nowMilliseconds)
    {
        if (!configuration.Enabled || !IsPaladin(localPlayer) || !HasValidLocalPlayer(localPlayer))
            return RefusePaladinMacro("Requires a living Paladin in PvP.");
        if (HasActiveGuard(localPlayer) || nearAssist.IsExactLocalGuardActiveOrPropagating(
                new(localPlayer.GameObjectId, localPlayer.EntityId)))
            return RefusePaladinMacro("Your active Guard is protected.");
        if (!IsGuardianReadyForRequest(localPlayer, explicitMacro: true))
            return RefusePaladinMacro("Guardian is not ready.");

        var candidates = BuildGuardianCandidates(localPlayer, nowMilliseconds, out _, out var pressureAge);
        var publishedAt = pressureAge >= 0 ? nowMilliseconds - pressureAge : -1;
        var selected = PaladinGuardianMacroRules.SelectCandidateIndex(candidates, nowMilliseconds, publishedAt);
        if (selected < 0)
            return RefusePaladinMacro("No endangered ally in Guardian range and no reachable ally within 6y.");
        var intent = candidates[selected];
        // Re-read identities, health, pressure and native reach without choosing
        // a replacement. The following boundary resolves the frozen slot again.
        var finalNow = Environment.TickCount64;
        var current = BuildGuardianCandidates(localPlayer, finalNow, out _, out var finalPressureAge);
        var exact = current.FindIndex(candidate => candidate.Actor == intent.Actor &&
                                                   candidate.PartySlot == intent.PartySlot);
        if (exact < 0) return RefusePaladinMacro("The selected ally is no longer available.");
        var outcome = TryUseGuardianOnce(localPlayer, intent, current[exact], out var attempted,
            explicitMacro: true,
            macroPressurePublishedAtMilliseconds: finalPressureAge >= 0 ? finalNow - finalPressureAge : -1);
        if (attempted) attemptCount++;
        if (outcome != ClientActionAttemptOutcome.ClientAccepted)
            return RefusePaladinMacro(attempted
                ? $"Guardian request was not confirmed ({outcome}); no automatic retry."
                : "Guardian is currently blocked by the game or the selected ally is no longer eligible.");

        acceptedCount++;
        ResetGuardianOpportunityRuntime();
        lastAcceptedGuardianEpisode = new AcceptedAutoGuardianEpisode(
            NextGuardianEpisodeToken(), Environment.TickCount64,
            new(localPlayer.GameObjectId, localPlayer.EntityId), intent.Actor, intent.PartySlot)
            { IsExplicitMacro = true };
        PaladinMacroLastEvent = $"Guardian accepted for party slot {intent.PartySlot}.";
        Volatile.Write(ref snapshot, Snapshot with
        {
            LastAcceptedGuardianEpisode = lastAcceptedGuardianEpisode,
            LastEvent = PaladinMacroLastEvent,
        });
        return true;
    }

    private bool RefusePaladinMacro(string reason)
    {
        PaladinMacroLastEvent = reason;
        return false;
    }
}
