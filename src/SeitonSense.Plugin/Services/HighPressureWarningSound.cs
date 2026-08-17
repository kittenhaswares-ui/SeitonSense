using Dalamud.Plugin.Services;

namespace SeitonSense.Plugin.Services;

internal sealed class HighPressureWarningSound
{
    private const long EpisodeCooldownMilliseconds = 3_000;
    private const long PreviewCooldownMilliseconds = 350;

    private readonly IPluginLog log;
    private ulong consumedEpisodeToken;
    private long nextEpisodeSoundAt;
    private long nextPreviewSoundAt;

    internal HighPressureWarningSound(IPluginLog log)
    {
        this.log = log;
    }

    internal bool TryPlayEpisode(ulong episodeToken, int soundId, long nowMilliseconds)
    {
        if (episodeToken == 0 ||
            episodeToken == consumedEpisodeToken ||
            nowMilliseconds < 0)
        {
            return false;
        }

        // Consume every new exact episode before testing the global rate limit.
        // A cooldown-suppressed, failed, or throwing request is never retried.
        consumedEpisodeToken = episodeToken;
        if (nowMilliseconds < nextEpisodeSoundAt) return false;
        nextEpisodeSoundAt = SaturatingAdd(nowMilliseconds, EpisodeCooldownMilliseconds);
        return MachinistLimitBreakWarningSound.TryPlayShared(
            soundId,
            log,
            "Seiton Sense high-pressure warning sound failed closed.");
    }

    internal bool TryPlayPreview(int soundId, long nowMilliseconds)
    {
        if (nowMilliseconds < 0 || nowMilliseconds < nextPreviewSoundAt) return false;
        nextPreviewSoundAt = SaturatingAdd(nowMilliseconds, PreviewCooldownMilliseconds);
        return MachinistLimitBreakWarningSound.TryPlayShared(
            soundId,
            log,
            "Seiton Sense high-pressure warning sound preview failed closed.");
    }

    internal void Reset()
    {
        consumedEpisodeToken = 0;
        nextEpisodeSoundAt = 0;
    }

    private static long SaturatingAdd(long value, long addition) =>
        addition > 0 && value > long.MaxValue - addition ? long.MaxValue : value + addition;
}
