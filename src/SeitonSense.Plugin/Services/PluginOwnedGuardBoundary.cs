namespace SeitonSense.Plugin.Services;

/// <summary>
/// Distinguishes helper execution from a fresh manual command. Redirect bypass
/// alone is not ownership: explicit user macros also bypass target rewriting.
/// </summary>
internal sealed class PluginOwnedGuardBoundary
{
    [ThreadStatic]
    private static Scope? current;

    internal IDisposable Enter() => new Scope(this);
    internal bool IsCurrentOwner => ReferenceEquals(current?.Owner, this);

    internal bool ShouldBlock(
        bool additionalPluginOwnedRequest,
        bool exactExplicitGuardBreak,
        Func<bool> readGuardActiveOrPropagating,
        bool exactOwnedQueuedGuardContinuation = false,
        Func<bool>? readLiveGuard = null)
    {
        if (exactExplicitGuardBreak ||
            !additionalPluginOwnedRequest && !ReferenceEquals(current?.Owner, this))
            return false;

        try
        {
            // The accepted queue submission is not Guard activation. Only
            // this exact attributed native Guard continuation may ignore its
            // own propagation marker; a live Guard still vetoes the replay.
            return exactOwnedQueuedGuardContinuation && additionalPluginOwnedRequest
                ? readLiveGuard?.Invoke() ?? true
                : readGuardActiveOrPropagating();
        }
        catch { return true; } // Only plugin-owned requests fail closed.
    }

    private sealed class Scope : IDisposable
    {
        internal readonly PluginOwnedGuardBoundary Owner;
        private readonly Scope? previous;
        private bool disposed;

        internal Scope(PluginOwnedGuardBoundary owner)
        {
            Owner = owner;
            previous = current;
            current = this;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ReferenceEquals(current, this)) current = previous;
        }
    }
}
