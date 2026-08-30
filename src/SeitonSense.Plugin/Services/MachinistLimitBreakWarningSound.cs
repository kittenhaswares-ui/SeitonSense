using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace SeitonSense.Plugin.Services;

internal sealed class MachinistLimitBreakWarningSound
{
    private const long ThreatCooldownMilliseconds = 2_000;
    private const long PreviewCooldownMilliseconds = 350;

    private readonly IPluginLog log;
    private readonly HashSet<ulong> consumedThreatTokens = [];
    private readonly Queue<ulong> consumedThreatTokenOrder = [];
    private long nextThreatSoundAt;
    private long nextPreviewSoundAt;

    internal MachinistLimitBreakWarningSound(IPluginLog log)
    {
        this.log = log;
    }

    internal bool TryPlayThreat(ulong threatToken, int soundId, long nowMilliseconds)
    {
        if (threatToken == 0 || !consumedThreatTokens.Add(threatToken)) return false;

        consumedThreatTokenOrder.Enqueue(threatToken);
        while (consumedThreatTokenOrder.Count > 64)
            consumedThreatTokens.Remove(consumedThreatTokenOrder.Dequeue());

        // Consume before the shared cooldown check. Simultaneous warnings make
        // one cue now instead of queuing a stale sound for a later frame.
        if (nowMilliseconds < nextThreatSoundAt) return false;

        // Consume before the native call. A failed/throwing sound request is not retried.
        nextThreatSoundAt = SaturatingAdd(nowMilliseconds, ThreatCooldownMilliseconds);
        return TryPlay(soundId);
    }

    internal bool TryPlayPreview(int soundId, long nowMilliseconds)
    {
        if (nowMilliseconds < nextPreviewSoundAt) return false;
        nextPreviewSoundAt = SaturatingAdd(nowMilliseconds, PreviewCooldownMilliseconds);
        return TryPlay(soundId);
    }

    internal void Reset()
    {
        consumedThreatTokens.Clear();
        consumedThreatTokenOrder.Clear();
        nextThreatSoundAt = 0;
    }

    private bool TryPlay(int soundId) =>
        TryPlayShared(
            soundId,
            log,
            "Seiton Sense MCH warning sound failed closed.");

    internal static unsafe bool TryPlayShared(
        int soundId,
        IPluginLog log,
        string failureMessage)
    {
        if (soundId is < 1 or > 16) return false;
        try
        {
            UIGlobals.PlayChatSoundEffect((uint)soundId);
            return true;
        }
        catch (Exception exception)
        {
            log.Warning(exception, failureMessage);
            return false;
        }
    }

    private static long SaturatingAdd(long value, long addition) =>
        addition > 0 && value > long.MaxValue - addition ? long.MaxValue : value + addition;
}
