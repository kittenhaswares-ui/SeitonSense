using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Plugin;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

internal enum IntegratedActionBufferCompatibilityCheck
{
    Arm,
    Dispatch,
}

internal readonly record struct IntegratedActionBufferCompatibilityDiagnostics(
    bool Started,
    bool AssessmentAvailable,
    bool BufferMutationAllowed,
    bool QuarantinedThisFrame,
    int RemainingCleanFrames,
    SmartActionBufferReActionProfile ReActionProfile,
    bool MOActionLoaded,
    bool MOActionOwnershipPublished,
    int MOActionOwnedActionCount,
    long AssessmentCount,
    long ArmCheckCount,
    long DispatchCheckCount,
    long BlockedCheckCount,
    long LiveProfileMismatchCount,
    string LastEvent);

/// <summary>
/// Narrow compatibility boundary for synthetic generic-buffer replay. It does
/// not participate in native hotbar execution or native Turbo cadence.
/// Foreign plugin configuration is held weakly and read only from loaded
/// assemblies owned by the matching exposed plugin.
/// </summary>
internal sealed class IntegratedActionBufferCompatibilityService : IDisposable
{
    private const string MOActionRetargetedActionsIpc = "MOAction.RetargetedActions";
    private const string SignatureFormat = "seiton-buffer-compat-v1";
    private const int RefreshIntervalMilliseconds = 5_000;
    private const int MaximumPublishedActionIds = 4_096;
    private const int MaximumDiagnosticLength = 180;

    private static readonly Version SupportedReActionVersion = new(1, 3, 5, 1);
    private static readonly Version SupportedMOActionVersion = new(4, 10, 1, 0);

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly object gate = new();
    private CompatibilityAssessment assessment = CompatibilityAssessment.Unavailable;
    private WeakReference<object>? liveReActionConfiguration;
    private ReActionConfigurationSnapshot? auditedReActionConfiguration;
    private long assessmentGeneration;
    private long assessmentCount;
    private long armCheckCount;
    private long dispatchCheckCount;
    private long blockedCheckCount;
    private long liveProfileMismatchCount;
    private long nextRefreshAt;
    private int remainingCleanFrames;
    private int topologyDirty;
    private bool quarantinedThisFrame;
    private bool hasAssessment;
    private bool subscribed;
    private bool started;
    private bool disposed;
    private string lastEvent = "Not started";

    internal IntegratedActionBufferCompatibilityService(
        IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface ??
            throw new ArgumentNullException(nameof(pluginInterface));
    }

    internal IntegratedActionBufferCompatibilityDiagnostics Diagnostics
    {
        get
        {
            lock (gate)
            {
                var input = ToRulesInput(assessment, quarantinedThisFrame);
                return new IntegratedActionBufferCompatibilityDiagnostics(
                    started,
                    assessment.Available,
                    SmartActionBufferCompatibilityRules.AllowsMutation(input),
                    quarantinedThisFrame,
                    remainingCleanFrames,
                    assessment.ReActionProfile,
                    assessment.MOActionLoaded,
                    assessment.MOActionOwnershipPublished,
                    assessment.MOActionOwnedActionCount,
                    assessmentCount,
                    armCheckCount,
                    dispatchCheckCount,
                    blockedCheckCount,
                    liveProfileMismatchCount,
                    lastEvent);
            }
        }
    }

    internal void Start()
    {
        lock (gate)
        {
            if (disposed || started) return;
            started = true;
            lastEvent = "Initializing compatibility profile";
        }

        try
        {
            pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
            lock (gate) subscribed = true;
        }
        catch
        {
            lock (gate)
            {
                assessment = CompatibilityAssessment.Unavailable;
                lastEvent = "Plugin topology events are unavailable; buffer replay is blocked";
            }

            return;
        }

        RefreshAssessment(
            Environment.TickCount64,
            dirtyTransition: false,
            initialAssessment: true);
    }

    /// <summary>
    /// Called once at Seiton's stable framework boundary. A detected topology,
    /// profile, or ownership-signature transition blocks this complete frame;
    /// the following unchanged frame is eligible again.
    /// </summary>
    internal void OnFrameworkBoundary()
    {
        if (disposed || !started) return;

        var now = Environment.TickCount64;
        var dirty = Interlocked.Exchange(ref topologyDirty, 0) != 0;
        bool refresh;
        lock (gate)
        {
            quarantinedThisFrame = false;
            refresh = dirty || now >= nextRefreshAt;
        }

        if (refresh)
            RefreshAssessment(now, dirty, initialAssessment: false);

        lock (gate)
        {
            if (remainingCleanFrames <= 0) return;

            quarantinedThisFrame = true;
            remainingCleanFrames =
                SmartActionBufferCompatibilityRules.ConsumeCleanFrameworkFrame(
                    remainingCleanFrames);
            lastEvent = "Compatibility profile is settling for one clean framework frame";
        }
    }

    internal bool CanMutateAction(
        uint requestedActionId,
        uint resolvedActionId,
        IntegratedActionBufferCompatibilityCheck check,
        out string reason)
    {
        reason = string.Empty;
        CompatibilityAssessment expected;
        ReActionConfigurationSnapshot? expectedReAction;
        WeakReference<object>? weakReAction;
        long generation;

        lock (gate)
        {
            if (check == IntegratedActionBufferCompatibilityCheck.Arm)
                armCheckCount++;
            else
                dispatchCheckCount++;

            expected = assessment;
            expectedReAction = auditedReActionConfiguration;
            weakReAction = liveReActionConfiguration;
            generation = assessmentGeneration;

            var profileInput = ToRulesInput(
                expected,
                quarantinedThisFrame || Volatile.Read(ref topologyDirty) != 0);
            if (!started || disposed || requestedActionId == 0 || resolvedActionId == 0 ||
                !SmartActionBufferCompatibilityRules.AllowsMutation(profileInput))
            {
                reason = DescribeProfileBlock(profileInput);
                RecordBlockedLocked(reason);
                return false;
            }
        }

        if (!IsLiveReActionProfileCurrent(expectedReAction, weakReAction))
        {
            reason = "ReAction safety settings changed or became unreadable";
            MarkProfileDirty(reason);
            return false;
        }

        var actionIsUnowned = true;
        if (expected.MOActionLoaded &&
            !TryReadMOActionOwnership(
                requestedActionId,
                resolvedActionId,
                expected.MOActionOwnershipSignature,
                out actionIsUnowned,
                out var ownershipReason))
        {
            reason = ownershipReason;
            MarkProfileDirty(reason);
            return false;
        }

        if (expected.MOActionLoaded && !actionIsUnowned)
        {
            reason = "MOAction owns the requested or resolved action";
            lock (gate) RecordBlockedLocked(reason);
            return false;
        }

        lock (gate)
        {
            var stable = generation == assessmentGeneration &&
                Volatile.Read(ref topologyDirty) == 0 &&
                !quarantinedThisFrame &&
                SmartActionBufferCompatibilityRules.AllowsMutation(
                    ToRulesInput(assessment, quarantinedThisFrame));
            if (!stable)
            {
                reason = "Compatibility profile changed during validation";
                RecordBlockedLocked(reason);
                return false;
            }

            lastEvent = check == IntegratedActionBufferCompatibilityCheck.Arm
                ? "Arm compatibility check passed"
                : "Dispatch compatibility check passed";
            return true;
        }
    }

    /// <summary>
    /// Cheap cached gate for per-frame validation. It never reflects foreign
    /// assemblies and never invokes IPC; live checks remain arm/dispatch only.
    /// </summary>
    internal bool IsCachedMutationAllowed(out string reason)
    {
        lock (gate)
        {
            var input = ToRulesInput(
                assessment,
                quarantinedThisFrame || Volatile.Read(ref topologyDirty) != 0);
            if (started && !disposed &&
                SmartActionBufferCompatibilityRules.AllowsMutation(input))
            {
                reason = string.Empty;
                return true;
            }

            reason = DescribeProfileBlock(input);
            return false;
        }
    }

    public void Dispose()
    {
        bool removeSubscription;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            started = false;
            removeSubscription = subscribed;
            subscribed = false;
            liveReActionConfiguration = null;
            auditedReActionConfiguration = null;
            assessment = CompatibilityAssessment.Unavailable;
            remainingCleanFrames = 0;
            quarantinedThisFrame = false;
            lastEvent = "Disposed";
        }

        if (!removeSubscription) return;
        try
        {
            pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
        }
        catch
        {
            // Disposal must not hold the rest of the plugin open.
        }
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs _)
    {
        if (disposed) return;
        MarkProfileDirty("Plugin topology changed", liveMismatch: false);
    }

    private void RefreshAssessment(
        long now,
        bool dirtyTransition,
        bool initialAssessment)
    {
        var refreshed = Assess();
        lock (gate)
        {
            if (disposed) return;

            var changed = SmartActionBufferCompatibilityRules.SignatureChanged(
                hasAssessment,
                assessment.Signature,
                refreshed.Assessment.Signature);
            assessment = refreshed.Assessment;
            liveReActionConfiguration = refreshed.LiveReActionConfiguration is null
                ? null
                : new WeakReference<object>(refreshed.LiveReActionConfiguration);
            auditedReActionConfiguration = refreshed.ReActionConfiguration;
            hasAssessment = true;
            assessmentGeneration++;
            assessmentCount++;
            nextRefreshAt = SaturatingAdd(now, RefreshIntervalMilliseconds);

            if (!initialAssessment && (dirtyTransition || changed))
            {
                remainingCleanFrames =
                    SmartActionBufferCompatibilityRules.MarkChanged(
                        remainingCleanFrames);
            }

            lastEvent = refreshed.Assessment.Available
                ? DescribeAssessment(refreshed.Assessment)
                : "Compatibility assessment is unavailable; buffer replay is blocked";
        }
    }

    private AssessmentResult Assess()
    {
        IExposedPlugin[] loadedPlugins;
        try
        {
            loadedPlugins = pluginInterface.InstalledPlugins
                .Where(plugin => plugin.IsLoaded)
                .ToArray();
        }
        catch
        {
            return new AssessmentResult(
                CompatibilityAssessment.Unavailable,
                null,
                null);
        }

        var reActionMatches = loadedPlugins
            .Where(plugin => string.Equals(
                plugin.InternalName,
                "ReAction",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var reActionProfile = SmartActionBufferReActionProfile.NotLoaded;
        ReActionConfigurationSnapshot? reActionConfiguration = null;
        object? liveReActionConfiguration = null;
        string reActionSignature;
        if (reActionMatches.Length == 0)
        {
            reActionSignature = "none";
        }
        else if (reActionMatches.Length != 1 ||
                 reActionMatches[0].Version != SupportedReActionVersion ||
                 !TryReadReActionConfiguration(
                     reActionMatches[0],
                     out var configuration,
                     out var liveConfiguration))
        {
            reActionProfile = SmartActionBufferReActionProfile.LoadedUnknown;
            reActionSignature = string.Join(
                ",",
                reActionMatches
                    .Select(plugin => plugin.Version.ToString())
                    .Order(StringComparer.Ordinal));
        }
        else
        {
            reActionConfiguration = configuration;
            liveReActionConfiguration = liveConfiguration;
            reActionProfile = configuration.AutoTargetEnabled ||
                              configuration.ActionStackCount != 0
                ? SmartActionBufferReActionProfile.AuditedMutationActive
                : SmartActionBufferReActionProfile.AuditedSafe;
            reActionSignature =
                $"{reActionMatches[0].Version}:{configuration.AutoTargetEnabled}:" +
                $"{configuration.ActionStackCount}";
        }

        var moActionMatches = loadedPlugins
            .Where(plugin => string.Equals(
                plugin.InternalName,
                "MOAction",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var moActionLoaded = moActionMatches.Length > 0;
        var moActionPublished = false;
        var moActionCount = 0;
        var moActionOwnershipSignature = "none";
        if (moActionMatches.Length == 1 &&
            moActionMatches[0].Version == SupportedMOActionVersion &&
            TryCaptureMOActionOwnership(
                out moActionCount,
                out moActionOwnershipSignature))
        {
            moActionPublished = true;
        }
        else if (moActionLoaded)
        {
            moActionOwnershipSignature = string.Join(
                ",",
                moActionMatches
                    .Select(plugin => plugin.Version.ToString())
                    .Order(StringComparer.Ordinal));
        }

        var signature = CreateSignature(
            reActionProfile,
            reActionSignature,
            moActionLoaded,
            moActionPublished,
            moActionOwnershipSignature);
        return new AssessmentResult(
            new CompatibilityAssessment(
                Available: true,
                reActionProfile,
                moActionLoaded,
                moActionPublished,
                moActionCount,
                moActionOwnershipSignature,
                signature),
            reActionConfiguration,
            liveReActionConfiguration);
    }

    private bool TryReadReActionConfiguration(
        IExposedPlugin expectedPlugin,
        out ReActionConfigurationSnapshot configuration,
        out object liveConfiguration)
    {
        configuration = default;
        liveConfiguration = null!;

        try
        {
            Type? pluginType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                IExposedPlugin? owner;
                try
                {
                    owner = pluginInterface.GetPlugin(assembly);
                }
                catch
                {
                    continue;
                }

                if (owner is null ||
                    !owner.IsLoaded ||
                    !string.Equals(
                        owner.InternalName,
                        expectedPlugin.InternalName,
                        StringComparison.OrdinalIgnoreCase) ||
                    owner.Version != expectedPlugin.Version)
                {
                    continue;
                }

                var candidate = assembly.GetType(
                    "ReAction.ReAction",
                    throwOnError: false,
                    ignoreCase: false);
                if (candidate is null) continue;
                if (pluginType is not null) return false;
                pluginType = candidate;
            }

            if (pluginType is null) return false;
            var configProperty = pluginType.GetProperty(
                "Config",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (configProperty?.GetMethod is null ||
                configProperty.GetIndexParameters().Length != 0)
            {
                return false;
            }

            var config = configProperty.GetValue(null);
            if (config is null ||
                !string.Equals(
                    config.GetType().FullName,
                    "ReAction.Configuration",
                    StringComparison.Ordinal) ||
                !TryReadReActionConfigurationObject(config, out configuration))
            {
                return false;
            }

            liveConfiguration = config;
            return true;
        }
        catch
        {
            configuration = default;
            liveConfiguration = null!;
            return false;
        }
    }

    private static bool TryReadReActionConfigurationObject(
        object config,
        out ReActionConfigurationSnapshot configuration)
    {
        configuration = default;
        if (!string.Equals(
                config.GetType().FullName,
                "ReAction.Configuration",
                StringComparison.Ordinal))
        {
            return false;
        }

        var type = config.GetType();
        if (!TryReadCollectionCount(type, config, "ActionStacks", out var actionStackCount) ||
            !TryReadBoolean(type, config, "EnableAutoTarget", out var autoTargetEnabled))
        {
            return false;
        }

        configuration = new ReActionConfigurationSnapshot(
            actionStackCount,
            autoTargetEnabled);
        return true;
    }

    private static bool IsLiveReActionProfileCurrent(
        ReActionConfigurationSnapshot? expected,
        WeakReference<object>? weak)
    {
        if (expected is null) return true;
        return weak is not null &&
            weak.TryGetTarget(out var configuration) &&
            TryReadReActionConfigurationObject(configuration, out var current) &&
            current == expected.Value;
    }

    private bool TryCaptureMOActionOwnership(
        out int actionCount,
        out string ownershipSignature)
    {
        actionCount = 0;
        ownershipSignature = string.Empty;
        try
        {
            var subscriber =
                pluginInterface.GetIpcSubscriber<uint[]>(MOActionRetargetedActionsIpc);
            if (!subscriber.HasFunction) return false;
            var actionIds = subscriber.InvokeFunc();
            if (actionIds is null || actionIds.Length > MaximumPublishedActionIds)
                return false;

            var canonical = actionIds
                .Where(actionId => actionId != 0)
                .Distinct()
                .Order()
                .ToArray();
            actionCount = canonical.Length;
            ownershipSignature = CreateActionOwnershipSignature(canonical);
            return true;
        }
        catch
        {
            actionCount = 0;
            ownershipSignature = string.Empty;
            return false;
        }
    }

    private bool TryReadMOActionOwnership(
        uint requestedActionId,
        uint resolvedActionId,
        string expectedOwnershipSignature,
        out bool actionIsUnowned,
        out string reason)
    {
        actionIsUnowned = false;
        reason = "MOAction ownership data became unavailable";
        try
        {
            var subscriber =
                pluginInterface.GetIpcSubscriber<uint[]>(MOActionRetargetedActionsIpc);
            if (!subscriber.HasFunction) return false;
            var actionIds = subscriber.InvokeFunc();
            if (actionIds is null || actionIds.Length > MaximumPublishedActionIds)
                return false;

            var canonical = actionIds
                .Where(actionId => actionId != 0)
                .Distinct()
                .Order()
                .ToArray();
            if (!string.Equals(
                    CreateActionOwnershipSignature(canonical),
                    expectedOwnershipSignature,
                    StringComparison.Ordinal))
            {
                reason = "MOAction ownership signature changed";
                return false;
            }

            actionIsUnowned = Array.BinarySearch(canonical, requestedActionId) < 0 &&
                Array.BinarySearch(canonical, resolvedActionId) < 0;
            reason = string.Empty;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadCollectionCount(
        Type configType,
        object config,
        string fieldName,
        out int count)
    {
        count = 0;
        var field = configType.GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Instance);
        if (field is null) return false;

        var value = field.GetValue(config);
        if (value is ICollection collection)
        {
            count = collection.Count;
            return count >= 0;
        }

        if (value is null) return false;
        var countProperty = value.GetType().GetProperty(
            "Count",
            BindingFlags.Public | BindingFlags.Instance);
        if (countProperty?.PropertyType != typeof(int) ||
            countProperty.GetMethod is null ||
            countProperty.GetIndexParameters().Length != 0)
        {
            return false;
        }

        if (countProperty.GetValue(value) is not int typedCount || typedCount < 0)
            return false;
        count = typedCount;
        return true;
    }

    private static bool TryReadBoolean(
        Type configType,
        object config,
        string fieldName,
        out bool value)
    {
        value = false;
        var field = configType.GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Instance);
        if (field?.FieldType != typeof(bool) ||
            field.GetValue(config) is not bool typedValue)
        {
            return false;
        }

        value = typedValue;
        return true;
    }

    private void MarkProfileDirty(string reason, bool liveMismatch = true)
    {
        Interlocked.Exchange(ref topologyDirty, 1);
        lock (gate)
        {
            remainingCleanFrames =
                SmartActionBufferCompatibilityRules.MarkChanged(
                    remainingCleanFrames);
            quarantinedThisFrame = true;
            if (liveMismatch) liveProfileMismatchCount++;
            RecordBlockedLocked(reason);
        }
    }

    private void RecordBlockedLocked(string reason)
    {
        blockedCheckCount++;
        lastEvent = Bound(reason);
    }

    private static SmartActionBufferCompatibilityInput ToRulesInput(
        CompatibilityAssessment current,
        bool quarantined) => new(
        current.ReActionProfile,
        current.MOActionLoaded,
        current.MOActionOwnershipPublished,
        current.Available,
        quarantined);

    private static string DescribeProfileBlock(
        SmartActionBufferCompatibilityInput input)
    {
        if (!input.AssessmentAvailable) return "Compatibility assessment unavailable";
        if (input.Quarantined) return "Compatibility profile is in one-frame quarantine";
        if (input.ReActionProfile == SmartActionBufferReActionProfile.LoadedUnknown)
            return "Loaded ReAction profile is not audited";
        if (input.ReActionProfile == SmartActionBufferReActionProfile.AuditedMutationActive)
            return "ReAction Auto Target or Action Stacks can mutate this action";
        if (input.MOActionLoaded && !input.MOActionOwnershipPublished)
            return "MOAction ownership IPC is unavailable or unaudited";
        return "Compatibility profile blocks generic buffer replay";
    }

    private static string DescribeAssessment(CompatibilityAssessment current)
    {
        var allowed = SmartActionBufferCompatibilityRules.AllowsMutation(
            ToRulesInput(current, quarantined: false));
        return Bound(
            $"Profile {(allowed ? "ready" : "blocked")}: " +
            $"ReAction={current.ReActionProfile}, " +
            $"MOAction={current.MOActionLoaded}/{current.MOActionOwnershipPublished}/" +
            $"{current.MOActionOwnedActionCount}");
    }

    private static string CreateSignature(
        SmartActionBufferReActionProfile reActionProfile,
        string reActionSignature,
        bool moActionLoaded,
        bool moActionPublished,
        string moActionOwnershipSignature)
    {
        var canonical =
            $"{SignatureFormat}\n" +
            $"reaction:{reActionProfile}:{reActionSignature}\n" +
            $"moaction:{moActionLoaded}:{moActionPublished}:{moActionOwnershipSignature}";
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string CreateActionOwnershipSignature(uint[] canonical)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        foreach (var actionId in canonical)
        {
            BitConverter.TryWriteBytes(bytes, actionId);
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string Bound(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "No compatibility detail"
            : value.Length <= MaximumDiagnosticLength
                ? value
                : value[..MaximumDiagnosticLength];

    private static long SaturatingAdd(long left, int right) =>
        left > long.MaxValue - right ? long.MaxValue : left + right;

    private readonly record struct ReActionConfigurationSnapshot(
        int ActionStackCount,
        bool AutoTargetEnabled);

    private readonly record struct CompatibilityAssessment(
        bool Available,
        SmartActionBufferReActionProfile ReActionProfile,
        bool MOActionLoaded,
        bool MOActionOwnershipPublished,
        int MOActionOwnedActionCount,
        string MOActionOwnershipSignature,
        string Signature)
    {
        internal static CompatibilityAssessment Unavailable => new(
            Available: false,
            SmartActionBufferReActionProfile.LoadedUnknown,
            MOActionLoaded: true,
            MOActionOwnershipPublished: false,
            MOActionOwnedActionCount: 0,
            MOActionOwnershipSignature: string.Empty,
            Signature: "unavailable");
    }

    private readonly record struct AssessmentResult(
        CompatibilityAssessment Assessment,
        ReActionConfigurationSnapshot? ReActionConfiguration,
        object? LiveReActionConfiguration);
}
