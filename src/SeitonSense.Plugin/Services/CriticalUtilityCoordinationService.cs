using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;

namespace SeitonSense.Plugin.Services;

internal readonly record struct CriticalUtilityCoordinationSnapshot(
    bool ProviderAvailable,
    bool Eligible,
    bool Claimed,
    bool IntegratedEligible,
    bool IntegratedClaimed,
    SupportedPvPContext Context,
    long ClaimCount,
    long QueryCount,
    long PositiveQueryCount,
    string LastEvent);

/// <summary>
/// Publishes a read-only bounded claim for the existing shared held-action
/// scheduler. Seiton's own input paths use exact framework-frame ownership;
/// external IPC readers receive a short raw-clock lease so subscription order
/// cannot hide the claim. This service does not own an action queue and never
/// dispatches, retargets, or retries anything.
/// </summary>
internal sealed class CriticalUtilityCoordinationService : IDisposable
{
    internal const string IpcName = "SeitonSense.IsCriticalUtilityClaimed";
    internal const long ClaimLeaseMilliseconds = 125;

    private readonly PluginConfiguration configuration;
    private readonly SeitonResponseClock clock;
    private readonly IPluginLog log;
    private readonly ICallGateProvider<bool>? provider;
    private int providerAvailable;
    private int eligible;
    private int claimed;
    private int integratedEligible;
    private int integratedClaimed;
    private int context;
    private long claimExpiresAtTimestamp = -1;
    private long claimFrameEpoch = -1;
    private long claimCount;
    private long queryCount;
    private long positiveQueryCount;
    private string lastEvent = "Not registered";
    private bool disposed;

    internal CriticalUtilityCoordinationService(
        IDalamudPluginInterface pluginInterface,
        PluginConfiguration configuration,
        SeitonResponseClock clock,
        IPluginLog log)
    {
        this.configuration = configuration;
        this.clock = clock;
        this.log = log;
        try
        {
            provider = pluginInterface.GetIpcProvider<bool>(IpcName);
            provider.RegisterFunc(IsClaimedForIpc);
            Volatile.Write(ref providerAvailable, 1);
            lastEvent = "IPC ready; waiting for an owned held frame";
        }
        catch (Exception exception)
        {
            lastEvent = "IPC unavailable";
            log.Warning(
                exception,
                "Seiton Sense critical-utility coordination IPC is unavailable; held helpers remain active without cross-plugin yielding.");
        }
    }

    internal CriticalUtilityCoordinationSnapshot Snapshot => new(
        Volatile.Read(ref providerAvailable) != 0,
        Volatile.Read(ref eligible) != 0,
        IsClaimedWithoutCounting(),
        Volatile.Read(ref integratedEligible) != 0,
        IsIntegratedInputPriorityClaimed,
        (SupportedPvPContext)Volatile.Read(ref context),
        Interlocked.Read(ref claimCount),
        Interlocked.Read(ref queryCount),
        Interlocked.Read(ref positiveQueryCount),
        Volatile.Read(ref lastEvent));

    internal void BeginFrame(
        bool pluginEnabled,
        bool coordinationEnabled,
        SupportedPvPContext currentContext,
        bool localPlayerAlive,
        bool hardReset)
    {
        Volatile.Write(ref context, (int)currentContext);
        var canReserveIntegratedInput =
            CriticalUtilityCoordinationRules.ShouldReserveIntegratedInput(
                pluginEnabled,
                currentContext,
                localPlayerAlive,
                hardReset,
                sharedHeldFrameConsumed: true);
        var canPublish = CriticalUtilityCoordinationRules.ShouldPublish(
            pluginEnabled,
            coordinationEnabled,
            currentContext,
            localPlayerAlive,
            hardReset,
            sharedHeldFrameConsumed: true);
        Volatile.Write(ref integratedEligible, canReserveIntegratedInput ? 1 : 0);
        Volatile.Write(ref eligible, canPublish ? 1 : 0);
        if (!canReserveIntegratedInput)
        {
            Volatile.Write(ref claimed, 0);
            Volatile.Write(ref integratedClaimed, 0);
            Volatile.Write(ref claimExpiresAtTimestamp, -1);
            Volatile.Write(ref claimFrameEpoch, -1);
            lastEvent = "Inactive";
        }
        else
        {
            if (!IsInternalFrameClaimAlive())
                Volatile.Write(ref integratedClaimed, 0);
            if (!canPublish || !IsExternalLeaseAlive())
                Volatile.Write(ref claimed, 0);
            lastEvent = "Integrated input eligible; waiting for an owned held frame";
        }
    }

    internal void ClaimCurrentFrame()
    {
        if (Volatile.Read(ref integratedEligible) == 0 || disposed) return;
        var now = clock.Capture();
        if (!now.IsValid) return;
        var deadline = AdaptiveResponseTimeRules.DeadlineAfterMilliseconds(
            now.Timestamp,
            ClaimLeaseMilliseconds,
            clock.TimestampFrequency);
        if (deadline < 0) return;
        Volatile.Write(
            ref claimExpiresAtTimestamp,
            deadline);
        Volatile.Write(ref claimFrameEpoch, now.FrameEpoch);
        Interlocked.Exchange(ref integratedClaimed, 1);
        if (Volatile.Read(ref eligible) != 0 &&
            Interlocked.Exchange(ref claimed, 1) == 0)
            Interlocked.Increment(ref claimCount);
        lastEvent = Volatile.Read(ref eligible) != 0
            ? "Critical Seiton held frame claimed internally and published"
            : "Critical Seiton held frame claimed internally";
    }

    /// <summary>
    /// Direct, in-process priority used by Seiton's own buffer and Turbo paths.
    /// It is intentionally independent from the legacy external IPC opt-in.
    /// </summary>
    internal bool IsIntegratedInputPriorityClaimed =>
        !disposed &&
        configuration.Enabled &&
        Volatile.Read(ref integratedEligible) != 0 &&
        Volatile.Read(ref integratedClaimed) != 0 &&
        IsInternalFrameClaimAlive();

    internal void Clear(string reason = "Cleared")
    {
        Volatile.Write(ref claimed, 0);
        Volatile.Write(ref integratedClaimed, 0);
        Volatile.Write(ref claimExpiresAtTimestamp, -1);
        Volatile.Write(ref claimFrameEpoch, -1);
        Volatile.Write(ref eligible, 0);
        Volatile.Write(ref integratedEligible, 0);
        Volatile.Write(ref context, (int)SupportedPvPContext.None);
        lastEvent = reason;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Clear("Disposed");
        if (provider is null) return;

        try
        {
            provider.UnregisterFunc();
        }
        catch (Exception exception)
        {
            log.Warning(
                exception,
                "Seiton Sense critical-utility coordination IPC could not be unregistered cleanly.");
        }
        finally
        {
            Volatile.Write(ref providerAvailable, 0);
        }
    }

    private bool IsClaimedForIpc()
    {
        Interlocked.Increment(ref queryCount);
        var result = IsClaimedWithoutCounting();
        if (result) Interlocked.Increment(ref positiveQueryCount);
        return result;
    }

    private bool IsClaimedWithoutCounting() =>
        !disposed &&
        configuration.Enabled &&
        configuration.EnablePvpLatencyResponseHelper &&
        Volatile.Read(ref providerAvailable) != 0 &&
        Volatile.Read(ref eligible) != 0 &&
        Volatile.Read(ref claimed) != 0 &&
        IsExternalLeaseAlive();

    private bool IsInternalFrameClaimAlive()
    {
        var expiresAt = Volatile.Read(ref claimExpiresAtTimestamp);
        var claimedFrame = Volatile.Read(ref claimFrameEpoch);
        var now = clock.Capture();
        return now.IsValid &&
               expiresAt >= 0 &&
               claimedFrame == now.FrameEpoch &&
               now.Timestamp < expiresAt;
    }

    private bool IsExternalLeaseAlive()
    {
        var expiresAt = Volatile.Read(ref claimExpiresAtTimestamp);
        var now = clock.Capture();
        return now.IsValid &&
               expiresAt >= 0 &&
               now.Timestamp < expiresAt;
    }
}
