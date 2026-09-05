using FFXIVClientStructs.FFXIV.Client.Game;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

internal static class QueuedHelperGuardOwnershipSelfTests
{
    private static readonly QueuedHelperGuardContext Context = new(250, new(0x100, 0x200));
    private static readonly QueuedHelperGuardRequest Request = new(ActionType.Action, 100, 101, 0x300, 7, 9);
    private static readonly QueuedHelperQueueInvocation Replay = new(
        ActionType.Action, 101, 0x300, 7, ActionManager.UseActionMode.Queue, 9);

    internal static void CaptureRequiresChangedExactInvokedHelperQueue()
    {
        var owner = new QueuedHelperGuardOwnership();
        var after = Queued();
        Require(!owner.Capture(false, Empty(), after, Request, Context, 1_000), "a helper that never entered native cannot own queue");
        Require(!owner.Capture(true, after, after with { LastUsedActionSequence = 88 }, Request, Context, 1_000),
            "an accepted immediate action cannot claim a pre-existing unchanged queued tuple");
        Require(!owner.Capture(true, Empty() with { Captured = false }, after, Request, Context, 1_000),
            "both boundaries must be observed");
        foreach (var changed in new[]
        {
            after with { ActionQueued = false }, after with { QueuedActionId = 555 },
            after with { QueuedActionType = (uint)ActionType.Item },
            after with { QueuedTargetId = 0x400 }, after with { QueuedExtraParam = 8 },
            after with { QueuedComboRouteId = 10 }, after with { AdjustedActionId = 102 },
        })
            Require(!owner.Capture(true, Empty(), changed, Request, Context, 1_000), "only exact helper tuple may be captured");
        Require(owner.Capture(true, Empty(), after, Request, Context, 1_000), "new exact adjusted queue is owned");
        owner.Clear();
        Require(owner.Capture(true, Empty(), after with { QueuedActionId = Request.RequestedActionId }, Request, Context, 1_000),
            "native queue may retain the exact raw helper ID");
    }

    internal static void QueuedHelperRemainsProtectedAcrossRepeatedGuardVetoes()
    {
        var owner = CapturedOwner();
        var boundary = new PluginOwnedGuardBoundary();
        owner.ObserveFreshUserAction(Replay with { ActionId = EnemyCombatConstants.GuardActionId, Mode = ActionManager.UseActionMode.None },
            isManualGuard: true);
        var nativeCalls = 0;
        for (var frame = 0; frame < 3; frame++)
        {
            Require(owner.TryMatchExactQueueReplay(Replay, Queued(), Context, 1_010 + frame, out var token),
                "later native queue replay keeps helper attribution after synchronous scope ended");
            if (!boundary.ShouldBlock(true, false, () => true)) nativeCalls++;
            owner.ObservePostCall(Queued(), Context, 1_010 + frame, token, replayAccepted: false);
        }
        Require(nativeCalls == 0 && owner.HasOwnership, "Guard veto must not consume attribution and allow next retry through");
        Require(owner.TryMatchExactQueueReplay(Replay, Queued(), Context, 1_100, out var acceptedToken), "ownership remains after vetoes");
        Require(!boundary.ShouldBlock(true, false, () => false), "Guard ending allows exact helper continuation");
        owner.ObservePostCall(Empty(), Context, 1_100, acceptedToken, replayAccepted: true);
        Require(!owner.HasOwnership, "accepted exact queue replay consumes once");
    }

    internal static void QueueClearedBeforeReplayIsBoundedAndFreshManualOverrides()
    {
        var owner = CapturedOwner();
        Require(owner.TryMatchExactQueueReplay(Replay, Empty(), Context, 1_010, out var token),
            "ActionQueued may already be cleared immediately before exact native Mode.Queue replay");
        owner.ObservePostCall(Empty(), Context, 1_011, token, replayAccepted: false);
        Require(owner.TryMatchExactQueueReplay(Replay, Empty(), Context, 1_012, out _),
            "vetoed replay remains attributed even during the cleared-queue window");
        Require(!owner.TryMatchExactQueueReplay(Replay, Empty(), Context,
                1_000 + QueuedHelperGuardOwnership.MaximumAttributionMilliseconds, out _),
            "repeated vetoes never extend attribution");

        owner = CapturedOwner();
        owner.ObserveFreshUserAction(Replay with { Mode = ActionManager.UseActionMode.None, TargetId = 0x999 }, isManualGuard: false);
        Require(!owner.HasOwnership, "fresh same-action user intent overrides even when selecting a new target");
        owner = CapturedOwner();
        owner.ObservePostCall(Empty(), Context, 1_020);
        Require(!owner.HasOwnership, "a gone queue outside an exact replay releases attribution");
    }

    internal static void QueueReplacementContextAndInvocationDriftInvalidate()
    {
        foreach (var changed in new[]
        {
            Queued() with { QueuedActionType = (uint)ActionType.Item },
            Queued() with { QueuedActionId = 102 }, Queued() with { QueuedTargetId = 0x401 },
            Queued() with { QueuedExtraParam = 8 }, Queued() with { QueueMode = 88 },
            Queued() with { QueuedComboRouteId = 10 }, Queued() with { Captured = false },
        })
        {
            var owner = CapturedOwner();
            Require(!owner.TryMatchExactQueueReplay(Replay, changed, Context, 1_010, out _),
                "every queued tuple field participates in identity");
            Require(!owner.HasOwnership, "queue replacement invalidates permanently");
        }
        foreach (var context in new[] { Context with { TerritoryId = 251 }, Context with { LocalPlayer = new(0x100, 0x201) }, default })
        {
            var owner = CapturedOwner();
            Require(!owner.TryMatchExactQueueReplay(Replay, Queued(), context, 1_010, out _), "actor/territory drift releases");
        }
        var wrongReplay = CapturedOwner();
        Require(!wrongReplay.TryMatchExactQueueReplay(Replay with { TargetId = 0x900 }, Empty(), Context, 1_010, out _),
            "a different replay cannot borrow the saved helper lease");
        Require(!wrongReplay.HasOwnership, "mismatched replay releases permanently");
        var clock = CapturedOwner();
        Require(!clock.TryMatchExactQueueReplay(Replay, Queued(), Context, 999, out _), "backwards clock releases");
    }

    internal static void OldCompletionCannotClearNewHelperQueue()
    {
        var owner = CapturedOwner();
        Require(owner.TryMatchExactQueueReplay(Replay, Queued(), Context, 1_010, out var oldToken), "original replay matched");
        var nextRequest = Request with { TargetId = 0x500 };
        var nextQueue = Queued() with { QueuedTargetId = 0x500 };
        Require(owner.Capture(true, Queued(), nextQueue, nextRequest, Context, 1_020), "new helper replaces one managed slot");
        owner.ObservePostCall(nextQueue, Context, 1_021, oldToken, replayAccepted: true);
        Require(owner.TryMatchExactQueueReplay(Replay with { TargetId = 0x500 }, nextQueue, Context, 1_022, out var newToken) && newToken != oldToken,
            "late old native completion cannot consume newer helper ownership");
    }

    internal static void ExactQueuedHelperRemainsAttributedUntilGuardEnds()
    {
        var owner = CapturedOwner();
        Require(owner.TryMatchExactQueueReplay(Replay, Queued(), Context, 3_500, out var token,
                ownGuardActiveOrAcceptedPropagation: true),
            "full Guard preserves an exact queued helper 2.5 seconds after capture");
        owner.ObservePostCall(Queued(), Context, 3_510, token, replayAccepted: false,
            ownGuardActiveOrAcceptedPropagation: true);
        Require(owner.TryMatchExactQueueReplay(Replay, Queued(), Context, 3_520, out _,
                ownGuardActiveOrAcceptedPropagation: true),
            "repeated vetoes remain owned while fresh Guard proof persists");
        Require(owner.TryMatchExactQueueReplay(Replay, Empty(), Context, 3_530, out _,
                ownGuardActiveOrAcceptedPropagation: true),
            "fresh Guard proof also preserves the exact cleared-queue handoff");
        Require(!owner.TryMatchExactQueueReplay(Replay, Empty(), Context, 3_600, out _,
                ownGuardActiveOrAcceptedPropagation: false),
            "Guard ending reapplies original cleared-queue expiry instead of a refreshed deadline");

        owner = CapturedOwner();
        Require(!owner.TryMatchExactQueueReplay(Replay, Queued() with { QueuedTargetId = 0x999 }, Context,
                3_500, out _, ownGuardActiveOrAcceptedPropagation: true),
            "Guard can never preserve a replaced queue tuple");
        owner = CapturedOwner();
        Require(!owner.TryMatchExactQueueReplay(Replay, Queued(), Context with { TerritoryId = 999 },
                3_500, out _, ownGuardActiveOrAcceptedPropagation: true),
            "Guard cannot preserve ownership across a context change");
        owner = CapturedOwner();
        owner.ObserveFreshUserAction(Replay with { Mode = ActionManager.UseActionMode.None }, isManualGuard: false);
        Require(!owner.TryMatchExactQueueReplay(Replay, Queued(), Context, 3_500, out _,
                ownGuardActiveOrAcceptedPropagation: true),
            "fresh manual same-action intent still wins while Guard is active");
    }

    internal static void LiveQueuedHelperOutlivesTimerWithoutInventingGuard()
    {
        var owner = CapturedOwner();
        var boundary = new PluginOwnedGuardBoundary();
        owner.ObserveFreshUserAction(Replay with
        {
            ActionId = EnemyCombatConstants.GuardActionId, Mode = ActionManager.UseActionMode.None,
        }, isManualGuard: true);
        // A manual Guard may reach entry after the old two-second deadline,
        // or cross that deadline in native. Neither side has live Guard yet.
        owner.ObservePostCall(Queued(), Context, 3_500);
        owner.ObservePostCall(Queued(), Context, 3_510);
        Require(owner.TryMatchExactQueueReplay(Replay, Queued(), Context, 3_520, out var token),
            "the unchanged live queue proves ownership even without Guard and after the timer");
        Require(boundary.ShouldBlock(true, false, () => true),
            "when manual Guard really activates, the old exact helper remains protected");
        Require(!boundary.ShouldBlock(true, false, () => false),
            "a rejected manual Guard cannot invent protection from queue ownership alone");
        owner.ObservePostCall(Queued(), Context, 3_530, token, replayAccepted: false);
        Require(!owner.TryMatchExactQueueReplay(Replay, Empty(), Context, 3_600, out _),
            "after the live queue is gone, the original handoff deadline expires without Guard proof");

        owner = CapturedOwner();
        owner.ObservePostCall(Queued(), Context, 2_999);
        owner.ObservePostCall(Queued(), Context, 3_001);
        Require(owner.TryMatchExactQueueReplay(Replay, Queued(), Context, 3_002, out _),
            "crossing the timer inside a manual Guard call cannot lose a still-exact queue");
    }

    internal static void ChangedExactQueueOwnershipDoesNotClaimNativeSuccess()
    {
        var before = Empty();
        var after = Queued();
        var outcome = ClientActionAttemptBoundaryRules.Classify(
            clientReturnedAccepted: false, Request.ResolvedActionId, before, after);
        Require(outcome == ClientActionAttemptOutcome.AcceptanceUnknown,
            "false native return with a changed queue is ambiguous, not accepted or retryable rejection");
        var owner = new QueuedHelperGuardOwnership();
        Require(owner.Capture(nativeBoundaryInvoked: true, before, after, Request, Context, 1_000),
            "an entered native boundary with an exact changed queue still proves helper provenance");
        Require(owner.TryMatchExactQueueReplay(Replay, after, Context, 1_010, out _),
            "ambiguous native result cannot turn its exact queued helper into manual input");
        var boundary = new PluginOwnedGuardBoundary();
        Require(boundary.ShouldBlock(true, false, () => true),
            "the attributed continuation remains subject to Guard without changing acceptance");
        Require(outcome == ClientActionAttemptOutcome.AcceptanceUnknown,
            "ownership capture never promotes the action result to success");
    }

    private static QueuedHelperGuardOwnership CapturedOwner()
    {
        var owner = new QueuedHelperGuardOwnership();
        Require(owner.Capture(true, Empty(), Queued(), Request, Context, 1_000), "test captures actual tracker");
        return owner;
    }

    private static ClientActionAttemptFingerprint Empty() => new(
        true, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, Request.ResolvedActionId, true, 0);
    private static ClientActionAttemptFingerprint Queued() => Empty() with
    {
        ActionQueued = true, QueuedActionType = (uint)ActionType.Action, QueuedActionId = 101,
        QueuedTargetId = 0x300, QueuedExtraParam = 7, QueueMode = 0, QueuedComboRouteId = 9,
    };
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
