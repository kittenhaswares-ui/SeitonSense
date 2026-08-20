using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using SeitonSense.Core;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Owns only the target mutations explicitly requested by the interactive enemy
/// combat frames. Every write is preceded by an exact same-thread S-slot and
/// object-table revalidation. Hard-target clicks are never retried.
/// </summary>
internal sealed class CombatFramesTargetingService : IDisposable
{
    private const long RendererHoverLifetimeMilliseconds = 200;

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ITargetManager targetManager;
    private readonly IFramework framework;
    private readonly ExecuteTracker executeTracker;
    private readonly IPluginLog log;

    private OwnedMouseoverTarget ownedMouseOver;
    private OwnedMouseoverTarget ownedMouseOverNameplate;
    private CombatFrameTargetIntent hoveredIntent;
    private bool mouseOverExternallyReplaced;
    private bool mouseOverNameplateExternallyReplaced;
    private long lastRendererTouchAtMilliseconds;
    private long nextErrorLogAtMilliseconds;
    private bool started;
    private bool disposed;

    internal CombatFramesTargetingService(
        IClientState clientState,
        IObjectTable objectTable,
        ITargetManager targetManager,
        IFramework framework,
        ExecuteTracker executeTracker,
        IPluginLog log)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.targetManager = targetManager;
        this.framework = framework;
        this.executeTracker = executeTracker;
        this.log = log;
    }

    internal void Start()
    {
        if (started || disposed) return;
        framework.Update += OnFrameworkUpdate;
        started = true;
    }

    /// <summary>
    /// Performs at most one public hard-target setter invocation for this click.
    /// Readback is diagnostic only and a mismatch is terminal for the click.
    /// </summary>
    internal bool TrySetHardTargetOnce(
        CombatFrameTargetIntent intent,
        long nowMilliseconds)
    {
        if (disposed ||
            !TryResolveExactTarget(intent, nowMilliseconds, out var exactTarget))
        {
            return false;
        }

        try
        {
            targetManager.Target = exactTarget;
            return MatchesExactTarget(targetManager.Target, exactTarget);
        }
        catch (Exception exception)
        {
            LogFailure(exception, "hard-target click failed; the click will not be retried");
            return false;
        }
    }

    /// <summary>
    /// Publishes the hovered actor through both native mouseover slots. These are
    /// ephemeral hover values, not soft, focus, previous, or hard targets.
    /// </summary>
    internal void TouchMouseover(
        CombatFrameTargetIntent intent,
        long nowMilliseconds)
    {
        if (disposed ||
            !TryResolveExactTarget(intent, nowMilliseconds, out var exactTarget))
        {
            ReleaseOwnedMouseover();
            return;
        }

        var newHover = !CombatFrameInteractionRules.IsSameFrozenTarget(
            hoveredIntent,
            intent);
        if (newHover)
        {
            ReleaseMouseoverSlot(nameplate: false, ref ownedMouseOver);
            ReleaseMouseoverSlot(nameplate: true, ref ownedMouseOverNameplate);
            mouseOverExternallyReplaced = false;
            mouseOverNameplateExternallyReplaced = false;
            hoveredIntent = intent;
        }

        // Keep the newest exact snapshot publication for the framework-side
        // refresh. A same-actor hover is intentionally not a new hover, so it
        // must not reset either external-replacement latch.
        hoveredIntent = intent;
        lastRendererTouchAtMilliseconds = nowMilliseconds;

        ReleaseOwnedIfDifferent(exactTarget, nameplate: false, ref ownedMouseOver);
        ReleaseOwnedIfDifferent(exactTarget, nameplate: true, ref ownedMouseOverNameplate);
        TouchMouseoverSlot(
            exactTarget,
            nameplate: false,
            newHover,
            ref mouseOverExternallyReplaced,
            ref ownedMouseOver);
        TouchMouseoverSlot(
            exactTarget,
            nameplate: true,
            newHover,
            ref mouseOverNameplateExternallyReplaced,
            ref ownedMouseOverNameplate);
    }

    /// <summary>
    /// Clears only values whose exact identity and native address still match a
    /// value successfully written by this service. External replacement wins.
    /// </summary>
    internal void ReleaseOwnedMouseover()
    {
        hoveredIntent = default;
        lastRendererTouchAtMilliseconds = 0;
        mouseOverExternallyReplaced = false;
        mouseOverNameplateExternallyReplaced = false;
        ReleaseMouseoverSlot(nameplate: false, ref ownedMouseOver);
        ReleaseMouseoverSlot(nameplate: true, ref ownedMouseOverNameplate);
    }

    public void Dispose()
    {
        if (disposed) return;
        if (started) framework.Update -= OnFrameworkUpdate;
        started = false;
        ReleaseOwnedMouseover();
        disposed = true;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (disposed || !started) return;

        var nowMilliseconds = Environment.TickCount64;
        if (!hoveredIntent.IsValid ||
            lastRendererTouchAtMilliseconds <= 0 ||
            nowMilliseconds < lastRendererTouchAtMilliseconds ||
            nowMilliseconds - lastRendererTouchAtMilliseconds > RendererHoverLifetimeMilliseconds)
        {
            ReleaseOwnedMouseover();
            return;
        }

        if (!TryResolveExactTarget(hoveredIntent, nowMilliseconds, out var exactTarget))
        {
            ReleaseOwnedMouseover();
            return;
        }

        // This is a refresh of the last renderer-proven hover, never a new
        // hover. If either slot changed in the meantime, its replacement latch
        // wins and no framework tick will rewrite that slot.
        ReleaseOwnedIfDifferent(exactTarget, nameplate: false, ref ownedMouseOver);
        ReleaseOwnedIfDifferent(exactTarget, nameplate: true, ref ownedMouseOverNameplate);
        TouchMouseoverSlot(
            exactTarget,
            nameplate: false,
            newHover: false,
            ref mouseOverExternallyReplaced,
            ref ownedMouseOver);
        TouchMouseoverSlot(
            exactTarget,
            nameplate: true,
            newHover: false,
            ref mouseOverNameplateExternallyReplaced,
            ref ownedMouseOverNameplate);
    }

    private bool TryResolveExactTarget(
        CombatFrameTargetIntent intent,
        long nowMilliseconds,
        out IPlayerCharacter exactTarget)
    {
        exactTarget = null!;

        try
        {
            var diagnosticsBefore = executeTracker.Diagnostics;
            var contextExact = IsExactCrystallineConflictContext(diagnosticsBefore);
            var canonical = EnemySlotResolver.Resolve(objectTable, intent.EnemySlot);
            var canonicalExact = HasValidIdentity(canonical) &&
                                 canonical!.GameObjectId == intent.Actor.GameObjectId &&
                                 canonical.EntityId == intent.Actor.EntityId;
            var tableObject = canonicalExact
                ? objectTable.SearchByEntityId(canonical!.EntityId) as IPlayerCharacter
                : null;
            var objectTableExact = canonicalExact &&
                                   HasValidIdentity(tableObject) &&
                                   tableObject!.Address == canonical!.Address &&
                                   tableObject.GameObjectId == canonical.GameObjectId &&
                                   tableObject.EntityId == canonical.EntityId;
            var alive = objectTableExact &&
                        !canonical!.IsDead &&
                        canonical.CurrentHp > 0 &&
                        canonical.MaxHp >= canonical.CurrentHp;
            var candidate = new CombatFrameTargetCandidate(
                intent.EnemySlot,
                canonicalExact
                    ? new TargetPressureActorIdentity(canonical!.GameObjectId, canonical.EntityId)
                    : default,
                canonicalExact,
                objectTableExact,
                contextExact,
                alive,
                objectTableExact && canonical!.IsTargetable);
            if (!CombatFrameInteractionRules.CanApplyIntent(intent, candidate, nowMilliseconds))
                return false;

            // Re-resolve immediately before returning the wrapper to the sole
            // setter so a slot/pointer change cannot reuse a frozen identity.
            var stableCanonical = EnemySlotResolver.Resolve(objectTable, intent.EnemySlot);
            var stableTable = stableCanonical is not null
                ? objectTable.SearchByEntityId(stableCanonical.EntityId) as IPlayerCharacter
                : null;
            var diagnosticsAfter = executeTracker.Diagnostics;
            if (!ReferenceEquals(diagnosticsBefore, diagnosticsAfter) ||
                !IsExactCrystallineConflictContext(diagnosticsAfter) ||
                !HasValidIdentity(stableCanonical) ||
                !HasValidIdentity(stableTable) ||
                stableCanonical!.Address != canonical!.Address ||
                stableCanonical.GameObjectId != intent.Actor.GameObjectId ||
                stableCanonical.EntityId != intent.Actor.EntityId ||
                stableTable!.Address != stableCanonical.Address ||
                stableTable.GameObjectId != stableCanonical.GameObjectId ||
                stableTable.EntityId != stableCanonical.EntityId ||
                stableCanonical.IsDead ||
                stableCanonical.CurrentHp == 0 ||
                stableCanonical.MaxHp < stableCanonical.CurrentHp ||
                !stableCanonical.IsTargetable)
            {
                return false;
            }

            exactTarget = stableCanonical;
            return true;
        }
        catch (Exception exception)
        {
            LogFailure(exception, "exact enemy target preflight failed closed");
            return false;
        }
    }

    private bool IsExactCrystallineConflictContext(TrackerDiagnostics diagnostics) =>
        diagnostics.Active &&
        diagnostics.IsPvP &&
        diagnostics.IsCrystallineConflict &&
        !diagnostics.IsWolvesDen &&
        diagnostics.TerritoryId != 0 &&
        diagnostics.TerritoryId == clientState.TerritoryType;

    private void TouchMouseoverSlot(
        IPlayerCharacter exactTarget,
        bool nameplate,
        bool newHover,
        ref bool externallyReplaced,
        ref OwnedMouseoverTarget ownership)
    {
        try
        {
            var current = ReadMouseoverSlot(nameplate);
            if (MatchesExactTarget(current, exactTarget))
            {
                // If the game or another plugin already supplied this exact
                // value, do not claim ownership merely by observing it.
                if (!ownership.Matches(exactTarget)) ownership = default;
                return;
            }

            if (ownership.IsValid && !ownership.Matches(current))
            {
                ownership = default;
                externallyReplaced = true;
                return;
            }

            if (externallyReplaced && !newHover) return;
            if (!newHover)
            {
                externallyReplaced = true;
                return;
            }

            WriteMouseoverSlot(nameplate, exactTarget);
            var readback = ReadMouseoverSlot(nameplate);
            ownership = MatchesExactTarget(readback, exactTarget)
                ? OwnedMouseoverTarget.From(exactTarget)
                : default;
            externallyReplaced = !ownership.IsValid;
        }
        catch (Exception exception)
        {
            ownership = default;
            externallyReplaced = true;
            LogFailure(exception, nameplate
                ? "nameplate mouseover publication failed closed"
                : "mouseover publication failed closed");
        }
    }

    private void ReleaseOwnedIfDifferent(
        IPlayerCharacter exactTarget,
        bool nameplate,
        ref OwnedMouseoverTarget ownership)
    {
        if (ownership.IsValid && !ownership.Matches(exactTarget))
            ReleaseMouseoverSlot(nameplate, ref ownership);
    }

    private void ReleaseMouseoverSlot(
        bool nameplate,
        ref OwnedMouseoverTarget ownership)
    {
        if (!ownership.IsValid) return;

        try
        {
            var current = ReadMouseoverSlot(nameplate);
            if (!ownership.Matches(current))
            {
                ownership = default;
                return;
            }

            WriteMouseoverSlot(nameplate, null);
            if (!ownership.Matches(ReadMouseoverSlot(nameplate)))
                ownership = default;
        }
        catch (Exception exception)
        {
            LogFailure(exception, nameplate
                ? "owned nameplate mouseover cleanup failed"
                : "owned mouseover cleanup failed");
        }
    }

    private IGameObject? ReadMouseoverSlot(bool nameplate) =>
        nameplate ? targetManager.MouseOverNameplateTarget : targetManager.MouseOverTarget;

    private void WriteMouseoverSlot(bool nameplate, IGameObject? target)
    {
        if (nameplate)
            targetManager.MouseOverNameplateTarget = target;
        else
            targetManager.MouseOverTarget = target;
    }

    private static bool MatchesExactTarget(IGameObject? observed, IGameObject expected) =>
        HasValidIdentity(observed) &&
        HasValidIdentity(expected) &&
        observed!.Address == expected.Address &&
        observed.GameObjectId == expected.GameObjectId &&
        observed.EntityId == expected.EntityId;

    private static bool HasValidIdentity(IGameObject? gameObject) =>
        gameObject is not null &&
        gameObject.Address != nint.Zero &&
        gameObject.IsValid() &&
        gameObject.GameObjectId is not 0 and not 0xE0000000UL &&
        gameObject.EntityId is not 0 and not 0xE0000000u;

    private void LogFailure(Exception exception, string message)
    {
        var now = Environment.TickCount64;
        if (now < nextErrorLogAtMilliseconds) return;
        nextErrorLogAtMilliseconds = now + 10_000;
        log.Error(exception, $"Seiton Sense Combat Frames {message}.");
    }

    private readonly record struct OwnedMouseoverTarget(
        TargetPressureActorIdentity Actor,
        nint Address)
    {
        internal bool IsValid => Actor.IsValid && Address != nint.Zero;

        internal bool Matches(IGameObject? candidate) =>
            IsValid &&
            HasValidIdentity(candidate) &&
            candidate!.Address == Address &&
            candidate.GameObjectId == Actor.GameObjectId &&
            candidate.EntityId == Actor.EntityId;

        internal static OwnedMouseoverTarget From(IGameObject target) => new(
            new TargetPressureActorIdentity(target.GameObjectId, target.EntityId),
            target.Address);
    }
}
