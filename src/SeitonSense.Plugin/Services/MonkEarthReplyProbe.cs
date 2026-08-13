using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal sealed record MonkEarthReplyProbeSnapshot(
    MonkEarthReplyPhase Phase,
    MonkEarthReplyDecisionKind Decision,
    MonkEarthReplyDecisionReason Reason,
    MonkEarthReplyTrigger Trigger,
    bool ResonancePresent,
    long ResonanceRemainingMilliseconds,
    uint CurrentHp,
    uint MaximumHp,
    int HpPercent,
    uint AdjustedActionId,
    bool HigherPriorityClaimed,
    bool UseActionAttempted,
    bool UseActionAccepted,
    long AttemptCount,
    long AcceptedCount)
{
    internal static MonkEarthReplyProbeSnapshot Initial { get; } = new(
        MonkEarthReplyPhase.WaitingForResonance,
        MonkEarthReplyDecisionKind.None,
        MonkEarthReplyDecisionReason.None,
        MonkEarthReplyTrigger.None,
        false,
        0,
        0,
        0,
        -1,
        0,
        false,
        false,
        false,
        0,
        0);
}

/// <summary>
/// Issues at most one exact Earth's Reply attempt for each continuous local
/// Earth Resonance observation. It deliberately invokes only action 29483;
/// Riddle of Earth 29482 is never used as a fallback.
/// </summary>
internal sealed class MonkEarthReplyProbe
{
    private readonly NearAssistRedirector nearAssist;
    private readonly IPluginLog log;
    private MonkEarthReplyState state = MonkEarthReplyState.Initial;
    private MonkEarthReplyProbeSnapshot snapshot = MonkEarthReplyProbeSnapshot.Initial;
    private long attemptCount;
    private long acceptedCount;
    private long nextErrorLogAt;

    internal MonkEarthReplyProbe(NearAssistRedirector nearAssist, IPluginLog log)
    {
        this.nearAssist = nearAssist;
        this.log = log;
    }

    internal MonkEarthReplyProbeSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal unsafe MonkEarthReplyProbeSnapshot Observe(
        IPlayerCharacter? localPlayer,
        bool isSupportedPvPContext,
        bool configurationEnabled,
        bool metadataVerified,
        bool triggerOnLowHp,
        bool triggerBeforeExpiry,
        int lowHpThresholdPercent,
        float expiryThresholdSeconds,
        bool higherPriorityClaimed,
        long nowMilliseconds,
        bool hardReset = false)
    {
        var alive = IsAlive(localPlayer);
        var localMonkValid = alive && IsMonk(localPlayer!);
        var localIdentityValid = localMonkValid && HasValidLocalPlayer(localPlayer!);
        var remainingSeconds = 0f;
        var resonancePresent = localIdentityValid &&
                               TryGetExactEarthResonance(localPlayer!, out remainingSeconds);
        var actionManager = ActionManager.Instance();
        var adjustedActionId = localIdentityValid && actionManager != null
            ? actionManager->GetAdjustedActionId(MonkEarthReplyRules.RiddleOfEarthActionId)
            : 0u;

        var decision = MonkEarthReplyRules.Observe(
            state,
            new MonkEarthReplyObservation(
                configurationEnabled,
                isSupportedPvPContext,
                localMonkValid,
                localIdentityValid,
                metadataVerified,
                higherPriorityClaimed,
                resonancePresent,
                localPlayer?.CurrentHp ?? 0,
                localPlayer?.MaxHp ?? 0,
                resonancePresent ? remainingSeconds : 0f,
                adjustedActionId,
                triggerOnLowHp,
                triggerBeforeExpiry,
                lowHpThresholdPercent,
                expiryThresholdSeconds,
                nowMilliseconds,
                hardReset));

        // Store the spent decision before crossing the native action boundary.
        // A rejected or throwing call therefore remains terminal for this buff.
        state = decision.NextState;
        var attempted = false;
        var accepted = false;
        if (decision.ShouldDispatch)
        {
            try
            {
                accepted = TryUseEarthsReplyOnce(localPlayer!, out attempted);
                if (accepted) Interlocked.Increment(ref acceptedCount);
            }
            catch (Exception exception)
            {
                LogAttemptFailure(exception, nowMilliseconds);
            }

            if (attempted) Interlocked.Increment(ref attemptCount);
        }

        var result = new MonkEarthReplyProbeSnapshot(
            state.Phase,
            decision.Kind,
            decision.Reason,
            decision.Trigger,
            resonancePresent,
            resonancePresent
                ? Math.Max(1L, (long)Math.Round(Math.Min(remainingSeconds, 3_600f) * 1_000f))
                : 0,
            localPlayer?.CurrentHp ?? 0,
            localPlayer?.MaxHp ?? 0,
            CalculateHpPercent(localPlayer),
            adjustedActionId,
            higherPriorityClaimed,
            attempted,
            accepted,
            Interlocked.Read(ref attemptCount),
            Interlocked.Read(ref acceptedCount));
        Volatile.Write(ref snapshot, result);
        return result;
    }

    internal void Reset()
    {
        state = MonkEarthReplyState.Initial;
        Volatile.Write(ref snapshot, MonkEarthReplyProbeSnapshot.Initial with
        {
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
        });
    }

    internal MonkEarthReplyProbeSnapshot FailClosed(long nowMilliseconds)
    {
        var decision = MonkEarthReplyRules.Observe(
            state,
            new MonkEarthReplyObservation(
                ConfigurationEnabled: false,
                IsSupportedPvPContext: false,
                IsLocalMonkValid: false,
                IsLocalPlayerIdentityValid: false,
                MetadataVerified: false,
                HigherPriorityClaimed: false,
                ResonancePresent: false,
                CurrentHp: 0,
                MaximumHp: 0,
                ResonanceRemainingSeconds: 0,
                AdjustedActionId: 0,
                TriggerOnLowHp: false,
                TriggerBeforeExpiry: false,
                LowHpThresholdPercent: 0,
                ExpiryThresholdSeconds: 0,
                NowMilliseconds: Math.Max(0, nowMilliseconds),
                HardReset: true));
        state = decision.NextState;
        var result = MonkEarthReplyProbeSnapshot.Initial with
        {
            Decision = decision.Kind,
            Reason = decision.Reason,
            AttemptCount = Interlocked.Read(ref attemptCount),
            AcceptedCount = Interlocked.Read(ref acceptedCount),
        };
        Volatile.Write(ref snapshot, result);
        return result;
    }

    private unsafe bool TryUseEarthsReplyOnce(
        IPlayerCharacter localPlayer,
        out bool attempted)
    {
        attempted = false;
        if (!IsMonk(localPlayer) || !HasValidLocalPlayer(localPlayer)) return false;

        var actionManager = ActionManager.Instance();
        if (actionManager == null ||
            actionManager->GetAdjustedActionId(MonkEarthReplyRules.RiddleOfEarthActionId) !=
            MonkEarthReplyRules.EarthsReplyActionId)
        {
            return false;
        }

        attempted = true;
        return nearAssist.RunWithoutRedirect(() =>
            actionManager->UseAction(
                ActionType.Action,
                MonkEarthReplyRules.EarthsReplyActionId,
                localPlayer.GameObjectId,
                0,
                ActionManager.UseActionMode.None,
                0));
    }

    private static bool TryGetExactEarthResonance(
        IPlayerCharacter localPlayer,
        out float remainingSeconds)
    {
        remainingSeconds = 0f;
        var matches = 0;
        foreach (var status in localPlayer.StatusList)
        {
            if (status.StatusId != MonkEarthReplyRules.EarthResonanceStatusId ||
                !float.IsFinite(status.RemainingTime) ||
                status.RemainingTime <= 0f)
            {
                continue;
            }

            matches++;
            remainingSeconds = status.RemainingTime;
            if (matches > 1)
            {
                remainingSeconds = 0f;
                return false;
            }
        }

        return matches == 1;
    }

    private static bool IsAlive(IPlayerCharacter? localPlayer) =>
        localPlayer is not null &&
        !localPlayer.IsDead &&
        localPlayer.CurrentHp > 0 &&
        localPlayer.MaxHp >= localPlayer.CurrentHp;

    private static bool IsMonk(IPlayerCharacter localPlayer) =>
        localPlayer.ClassJob.IsValid &&
        localPlayer.ClassJob.RowId == MonkEarthReplyRules.MonkJobId;

    private static unsafe bool HasValidLocalPlayer(IPlayerCharacter localPlayer)
    {
        if (!IsAlive(localPlayer) ||
            localPlayer.Address == 0 ||
            localPlayer.EntityId is 0 or 0xE0000000 ||
            localPlayer.GameObjectId is 0 or 0xE0000000)
        {
            return false;
        }

        var native = (GameObject*)localPlayer.Address;
        return native != null && native->EntityId == localPlayer.EntityId;
    }

    private static int CalculateHpPercent(IPlayerCharacter? localPlayer)
    {
        if (!IsAlive(localPlayer)) return -1;
        return (int)Math.Min(
            100UL,
            ((ulong)localPlayer!.CurrentHp * 100UL) / localPlayer.MaxHp);
    }

    private void LogAttemptFailure(Exception exception, long nowMilliseconds)
    {
        if (nowMilliseconds < nextErrorLogAt) return;
        nextErrorLogAt = nowMilliseconds + 10_000;
        log.Error(
            exception,
            "Seiton Sense Earth's Reply attempt failed closed and will not be retried for this Earth Resonance.");
    }
}
