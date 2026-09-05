using FFXIVClientStructs.FFXIV.Client.Game;
using SeitonSense.Core;
using SeitonSense.Plugin.Services;

internal static class SamuraiCastProtectionCoordinatorSelfTests
{
    private const uint Ogi = SamuraiSmartActionCastRules.OgiNamikiriActionId;
    private const ulong Target = 0x1234;

    internal static void NativeRequestAndObservedCastHaveNoProtectionGap()
    {
        var test = new Harness();
        False(test.Coordinator.IsProtected(), "an armed macro alone is not protected");
        var request = test.Begin();
        True(test.Coordinator.Execute(request, () =>
        {
            test.NativeCalls++;
            False(test.Movement.Read(0, 321, SamuraiMovementInputPath.Down),
                "already-held movement is suppressed inside the real managed native wrapper");
            True(test.Coordinator.ShouldBlockAction(HeldCastCancellationRules.AutomaticRecuperateActionId, false),
                "Recup is blocked inside the request, before client acceptance");
            return true;
        }), "the original client result is preserved");
        True(test.Coordinator.IsProtected(), "accepted startup is protected before IsCasting propagates");
        True(test.Coordinator.Status.Phase == SamuraiCastProtectionPhase.AwaitingCast,
            "client acceptance is not reported as an observed cast");
        test.Now += 100;
        test.ObserveCast();
        True(test.Coordinator.IsProtected(), "the exact native cast retains protection");
        True(test.Coordinator.Status.ObservedCastCount == 1, "the exact cast is counted once");
        test.Snapshot = test.Snapshot with { CurrentTapGeneration = 8 };
        True(test.Coordinator.IsProtected(), "pressing the macro again does not release an already observed cast");
        test.Snapshot = test.Snapshot with { IsCasting = false };
        True(test.Movement.Read(0, 321, SamuraiMovementInputPath.Down),
            "native cast loss releases movement immediately, without a startup grace restart");
        True(test.NativeCalls == 1 && test.Coordinator.Status.AcceptedCastCount == 1,
            "one authored native request, one client acceptance and no recast");
    }

    internal static void ExactNativeQueueContinuationDoesNotBlockItself()
    {
        var test = new Harness();
        test.QueueOgi();
        False(test.Coordinator.IsProtected(), "waiting in the native queue does not freeze movement");
        True(test.Coordinator.Status.Phase == SamuraiCastProtectionPhase.Queued,
            "native queued acceptance has its own phase");
        test.Now += 1_000; // Longer than the old 750ms startup lease.
        test.Snapshot = test.Snapshot with { Queue = EmptyQueue };
        var continuation = test.Coordinator.TryClaimQueuedContinuation(Invocation(), Ogi);
        True(continuation is not null, "an exact cleared-queue handoff retains its authored owner");
        True(test.Coordinator.Execute(continuation, () =>
        {
            test.NativeCalls++;
            False(test.Movement.Read(0, 112, SamuraiMovementInputPath.ControlState),
                "actual queued dispatch, not its idle wait, suppresses movement");
            test.ObserveCast();
            return true;
        }), "the exact queued Ogi is allowed to reach native execution");
        True(test.Coordinator.IsProtected(), "the native queued execution owns its actual cast");
        True(test.Coordinator.TryClaimQueuedContinuation(Invocation(), Ogi) is null,
            "the same queue proof cannot authorize a duplicate request");
        True(test.NativeCalls == 2 && test.Coordinator.Status.ObservedCastCount == 1,
            "one initial queue request and one native continuation, with no plugin retries");
    }

    internal static void QueueOwnershipRequiresExactBoundedNativeProof()
    {
        foreach (var wrong in new[]
        {
            Invocation() with { Mode = ActionManager.UseActionMode.None },
            Invocation() with { ActionType = ActionType.PvPAction },
            Invocation() with { ActionId = Ogi + 1 },
            Invocation() with { TargetId = Target + 1 },
            Invocation() with { ExtraParam = 1 },
            Invocation() with { ComboRouteId = 1 },
        })
        {
            var test = new Harness();
            test.QueueOgi();
            True(test.Coordinator.TryClaimQueuedContinuation(wrong, Ogi) is null,
                "a changed invocation cannot claim a same-ID or same-target exception");
            False(test.Coordinator.IsProtected(), "a mismatched queue request cannot gain movement protection");
        }
        foreach (var changed in new Func<SamuraiCastProtectionSnapshot, SamuraiCastProtectionSnapshot>[]
        {
            value => value with { TerritoryId = 2 },
            value => value with { LocalPlayer = new(101, 10) },
            value => value with { LocalPlayer = new(100, 11) },
            value => value with { Queue = QueuedOgi with { QueuedTargetId = Target + 1 } },
            value => value with { Queue = default },
        })
        {
            var test = new Harness();
            test.QueueOgi();
            test.Snapshot = changed(test.Snapshot);
            True(test.Coordinator.TryClaimQueuedContinuation(Invocation(), Ogi) is null,
                "queue, owner, context and generation must remain exact");
        }
        var expired = new Harness();
        expired.QueueOgi();
        expired.Snapshot = expired.Snapshot with { Queue = EmptyQueue };
        expired.Now += SamuraiCastProtectionCoordinator.MaximumQueueHandoffMilliseconds;
        True(expired.Coordinator.TryClaimQueuedContinuation(Invocation(), Ogi) is null,
            "a stale queue handoff is not revived by a later matching action");
        var repeatedArm = new Harness();
        repeatedArm.QueueOgi();
        repeatedArm.Snapshot = repeatedArm.Snapshot with { CurrentTapGeneration = 8 };
        var secondPress = repeatedArm.Coordinator.Begin(Ogi, Ogi, Target, 8, true);
        repeatedArm.Coordinator.Execute(secondPress, () => true); // Queue remains exactly unchanged.
        False(repeatedArm.Coordinator.IsProtected(), "a repeated arm does not freeze an unchanged waiting queue");
        True(repeatedArm.Coordinator.TryClaimQueuedContinuation(Invocation(), Ogi) is not null,
            "a new arm cannot erase already-proven unchanged native queue ownership");
        True(repeatedArm.Coordinator.TryClaimQueuedContinuation(Invocation(), Ogi) is null,
            "retaining queue provenance still permits only one continuation claim");
        var liveQueue = new Harness();
        liveQueue.QueueOgi();
        liveQueue.Now += 5_000;
        False(liveQueue.Coordinator.IsProtected(), "fresh exact live queue proof still never blocks movement");
        liveQueue.Snapshot = liveQueue.Snapshot with { Queue = EmptyQueue };
        liveQueue.Now += 100;
        True(liveQueue.Coordinator.TryClaimQueuedContinuation(Invocation(), Ogi) is not null,
            "the dispatch gap is measured from the latest exact native queue observation");
        var unchanged = new Harness { Snapshot = ValidSnapshot with { Queue = QueuedOgi } };
        unchanged.Coordinator.Execute(unchanged.Begin(), () => true);
        True(unchanged.Coordinator.TryClaimQueuedContinuation(Invocation(), Ogi) is null,
            "an unchanged pre-existing queue does not prove this authored request created it");
        var adjusted = new Harness();
        var tendo = SamuraiSmartActionCastRules.TendoSetsugekkaActionId;
        var carrier = SamuraiSmartActionCastRules.TendoSetsugekkaCarrierActionId;
        var request = adjusted.Coordinator.Begin(carrier, tendo, Target, 7, true);
        adjusted.Coordinator.Execute(request, () =>
        {
            adjusted.Snapshot = adjusted.Snapshot with { Queue = QueuedOgi with { QueuedActionId = tendo } };
            return true;
        });
        True(adjusted.Coordinator.TryClaimQueuedContinuation(Invocation() with { ActionId = tendo }, tendo) is not null,
            "the exact adjusted Tendo queue identity is retained, not confused with its instant follow-up");

        QueuePreflightVetoAndFailureRequireAnExactClaim();
    }

    private static void QueuePreflightVetoAndFailureRequireAnExactClaim()
    {
        var unclaimed = new Harness();
        unclaimed.QueueOgi();
        var preparation = unclaimed.Coordinator.TryPrepareQueuedContinuation(
            Invocation() with { ActionId = 123 },
            () => throw new InvalidOperationException("unrelated action resolution"),
            _ => throw new InvalidOperationException("must not resolve unrelated target"),
            (_, _) => throw new InvalidOperationException("must not inspect unrelated protection"), true);
        False(preparation.Blocked, "an exception before an exact queue claim preserves the unrelated native action");
        var nativeInvoked = false;
        unclaimed.Coordinator.Execute(preparation.Request, () => { nativeInvoked = true; return true; });
        True(nativeInvoked, "an unclaimed queue still reaches its original native boundary");

        foreach (var invalidIdentity in new[] { new TargetPressureActorIdentity(Target, 23), new(Target + 1, 22), default })
        {
            var changed = new Harness();
            changed.QueueOgi();
            var protectionReads = 0;
            nativeInvoked = false;
            preparation = changed.Coordinator.TryPrepareQueuedContinuation(Invocation(), () => Ogi,
                _ => invalidIdentity, (_, _) => { protectionReads++; return true; }, true);
            if (!preparation.Blocked)
                changed.Coordinator.Execute(preparation.Request, () => { nativeInvoked = true; return true; });
            True(preparation.Blocked && !nativeInvoked && protectionReads == 0,
                "both frozen IDs are checked before protection inspection or native-boundary marking");
            True(changed.Coordinator.Status.LastReleaseReason == "Queued cast exact target identity changed",
                "the half-ID or missing target failure is diagnosed explicitly");
        }
        var claimed = new Harness();
        claimed.QueueOgi();
        preparation = claimed.Coordinator.TryPrepareQueuedContinuation(Invocation(), () => Ogi,
            _ => throw new InvalidOperationException("claimed target resolution"), (_, _) => true, true);
        True(preparation.Blocked && preparation.Failure is not null,
            "an exception after an exact claim fails closed for that owned action only");
        False(claimed.Coordinator.IsProtected(), "a failed claimed preflight releases its movement ownership");

        var safe = new Harness();
        safe.QueueOgi();
        nativeInvoked = false;
        preparation = safe.Coordinator.TryPrepareQueuedContinuation(Invocation(), () => Ogi,
            request => new(request.TargetId, request.TargetEntityId), (_, _) =>
            {
                False(nativeInvoked, "preflight must finish before native invocation is marked");
                return true;
            }, true);
        True(!preparation.Blocked && preparation.Request is not null, "the exact safe continuation survives preflight");
        safe.Coordinator.Execute(preparation.Request, () => { nativeInvoked = true; return true; });
        True(nativeInvoked, "only entry into the final native callback marks invocation");

        var missingEntity = new Harness();
        var noEntity = missingEntity.Coordinator.Begin(Ogi, Ogi, Target, 7, true);
        missingEntity.Coordinator.Execute(noEntity, () =>
        {
            missingEntity.Snapshot = missingEntity.Snapshot with { Queue = QueuedOgi };
            return true;
        });
        preparation = missingEntity.Coordinator.TryPrepareQueuedContinuation(Invocation(), () => Ogi,
            _ => new(Target, 22), (_, _) => true, true);
        True(preparation.Blocked, "a queue captured without an exact entity ID cannot acquire a later actor");
    }

    internal static void RejectedFaultedOrRevokedRequestsCannotResurrect()
    {
        var rejected = new Harness();
        False(rejected.Coordinator.Execute(rejected.Begin(), () => { rejected.NativeCalls++; return false; }),
            "a native rejection stays rejected");
        False(rejected.Coordinator.IsProtected(), "native rejection releases immediately");
        True(rejected.NativeCalls == 1, "rejection does not retry");
        var faulted = new Harness();
        try
        {
            faulted.Coordinator.Execute(faulted.Begin(), () =>
            {
                faulted.NativeCalls++;
                throw new InvalidOperationException("native test fault");
            });
            throw new InvalidOperationException("Expected the original exception.");
        }
        catch (InvalidOperationException exception) when (exception.Message == "native test fault") { }
        False(faulted.Coordinator.IsProtected(), "native exceptions release in finally");
        True(faulted.NativeCalls == 1, "unknown delivery never triggers a second native call");
        foreach (var invalidate in new Action<Harness>[]
        {
            test => test.Coordinator.Reset(),
            test => test.Snapshot = test.Snapshot with { CurrentTapGeneration = 8 },
            test => test.Snapshot = test.Snapshot with { TerritoryId = 2 },
            test => test.Snapshot = test.Snapshot with { CastBreakingCrowdControl = true },
            test => test.Now--,
        })
        {
            var test = new Harness();
            test.Coordinator.Execute(test.Begin(), () => { invalidate(test); return true; });
            False(test.Coordinator.IsProtected(), "a revoked in-flight owner cannot be promoted on native return");
        }
        var nested = new Harness();
        nested.Coordinator.Execute(nested.Begin(), () =>
        {
            nested.Snapshot = nested.Snapshot with { CurrentTapGeneration = 8 };
            var newer = nested.Coordinator.Begin(Ogi, Ogi, Target, 8, true);
            nested.Coordinator.Execute(newer, () => true);
            return true;
        });
        True(nested.Coordinator.IsProtected(), "an older finally cannot clear a newer exact accepted owner");
        True(nested.Coordinator.Status.AcceptedCastCount == 1,
            "the revoked older request is not promoted or counted as another protected acceptance");
    }

    internal static void EmergencyActionsAndExactCastReleaseRemainAuthoritative()
    {
        var canonical = new Harness();
        canonical.Coordinator.Execute(canonical.Begin(), () => true);
        False(canonical.Coordinator.TryAllowCanonicalEmergencyAction(
                ActionType.PvPAction, EnemyCombatConstants.GuardActionId, false),
            "a PvP sheet-index request is not guessed to be canonical manual Guard");
        False(canonical.Coordinator.TryAllowCanonicalEmergencyAction(
                ActionType.Action, EnemyCombatConstants.GuardActionId, true),
            "canonical plugin-owned Guard still cannot bypass SAM protection");
        True(canonical.Coordinator.TryAllowCanonicalEmergencyAction(
                ActionType.Action, HeldCastCancellationRules.AutomaticPurifyActionId, true),
            "canonical Purify passes before any potentially faulting adjusted-action lookup");
        True(canonical.Coordinator.IsProtected(), "Purify permission leaves the cast lease intact");
        True(canonical.Coordinator.TryAllowCanonicalEmergencyAction(
                ActionType.Action, EnemyCombatConstants.GuardActionId, false),
            "canonical manual Guard passes before any potentially faulting adjusted-action lookup");
        False(canonical.Coordinator.HasOwnership, "canonical manual Guard retires every SAM owner immediately");
        True(canonical.Coordinator.Status.LastReleaseReason == "Manual Guard override",
            "canonical manual Guard keeps its exact release diagnostic");

        foreach (var ownership in new[] { (true, false, false), (false, true, false), (false, false, true) })
        {
            var owned = new Harness();
            owned.Coordinator.Execute(owned.Begin(), () => true);
            var classified = SamuraiCastProtectionCoordinator.IsPluginOwnedAction(
                ownership.Item1, ownership.Item2, ownership.Item3);
            True(classified && owned.Coordinator.ShouldBlockAction(EnemyCombatConstants.GuardActionId, classified),
                "the production ownership seam protects Ogi from helper, Turbo/Buffer/queue or exact automatic Guard");
            True(owned.Coordinator.IsProtected(), "a plugin-owned Guard cannot retire the accepted cast");
        }
        False(SamuraiCastProtectionCoordinator.IsPluginOwnedAction(false, false, false),
            "an ordinary manual action is not claimed by any plugin ownership source");
        var test = new Harness();
        test.Coordinator.Execute(test.Begin(), () => true);
        True(test.Coordinator.ShouldBlockAction(HeldCastCancellationRules.AutomaticRecuperateActionId, false),
            "manual Recup cannot interrupt a protected cast");
        True(test.Coordinator.ShouldBlockAction(EnemyCombatConstants.GuardActionId, true),
            "automatic or buffered Guard is not mistaken for a manual override");
        False(test.Coordinator.ShouldBlockAction(HeldCastCancellationRules.AutomaticPurifyActionId, true),
            "Purify always passes");
        True(test.Coordinator.IsProtected(), "Purify permission alone does not invent a cast cancellation");
        False(test.Coordinator.ShouldBlockAction(EnemyCombatConstants.GuardActionId, false),
            "manual Guard passes and retires startup ownership");
        False(test.Coordinator.IsProtected(), "manual Guard does not leave a 750ms movement hold");
        True(test.Coordinator.Status.LastReleaseReason == "Manual Guard override", "the release reason is explicit");

        foreach (var change in new Func<SamuraiCastProtectionSnapshot, SamuraiCastProtectionSnapshot>[]
        {
            value => value with { RuntimeEnabled = false },
            value => value with { TerritoryId = 2 },
            value => value with { LocalPlayer = new(101, 10) },
            value => value with { LocalPlayer = new(100, 11) },
            value => value with { LocalPlayerAlive = false },
            value => value with { LocalJobId = 19 },
            value => value with { CastBreakingCrowdControl = true },
            value => value with { GuardActive = true },
            value => value with { IsCasting = false },
            value => value with { CastActionId = Ogi + 1, AdjustedCastActionId = Ogi + 1 },
            value => value with { CastTargetGameObjectId = Target + 1 },
        })
        {
            var release = new Harness();
            release.Coordinator.Execute(release.Begin(), () => { release.ObserveCast(); return true; });
            release.Snapshot = change(release.Snapshot);
            False(release.Coordinator.IsProtected(), "exact native loss/CC/context change releases without immunity");
            True(release.Coordinator.Status.LastReleaseReason != "None", "the actual release is diagnosed");
        }
    }

    internal static void StartupIsBoundedAndUnreviewedActionsRemainNative()
    {
        var test = new Harness();
        True(test.Coordinator.Begin(Ogi + 1, Ogi + 1, Target, 7, true) is null,
            "instant follow-ups never gain movement protection");
        True(test.Coordinator.Begin(Ogi, Ogi, Target, 7, false) is null, "unverified metadata cannot acquire ownership");
        True(test.Coordinator.Begin(Ogi, Ogi, Target, 6, true) is null, "stale tap generations are not rearmed");
        test.Coordinator.Execute(test.Begin(), () => true);
        test.Now += 100;
        test.Snapshot = test.Snapshot with { IsCasting = true, CastTargetGameObjectId = Target };
        True(test.Coordinator.IsProtected(), "a transient not-yet-published cast ID uses only the bounded startup gap");
        test.Now += SamuraiOgiCastProtectionRules.StartPropagationMilliseconds;
        False(test.Coordinator.IsProtected(), "missing exact native cast proof times out without a recast");
        var readFails = false;
        var unavailable = new SamuraiCastProtectionCoordinator(
            () => readFails ? throw new InvalidOperationException("snapshot") : ValidSnapshot, () => 1_000);
        unavailable.Execute(unavailable.Begin(Ogi, Ogi, Target, 7, true), () => true);
        readFails = true;
        False(unavailable.IsProtected(), "snapshot failure cannot trap movement");

        LateFacingRequiresExactObservedTargetAndOneBoundedClaim();
    }

    private static void LateFacingRequiresExactObservedTargetAndOneBoundedClaim()
    {
        var target = new TargetPressureActorIdentity(Target, 22);
        var test = new Harness();
        test.Coordinator.Execute(test.Begin(), () => true);
        True(test.Coordinator.TryClaimLateFacing(target, 0.15f) is null,
            "native acceptance without an observed cast cannot auto-face");
        test.ObserveCast();
        test.Snapshot = test.Snapshot with { CurrentCastTime = 0.5f, TotalCastTime = 1.5f };
        True(test.Coordinator.GetLateFacingTarget(0.15f) is null,
            "the frame adapter need not resolve any target before the late window");
        True(test.Coordinator.TryClaimLateFacing(target, 0.15f) is null,
            "facing cannot begin before the final configured window");
        test.Snapshot = test.Snapshot with { CurrentCastTime = 1.4f };
        True(test.Coordinator.TryClaimLateFacing(target with { EntityId = 23 }, 0.15f) is null &&
             test.Coordinator.TryClaimLateFacing(target with { GameObjectId = Target + 1 }, 0.15f) is null,
            "both frozen target IDs must match; no target switch or half-ID reuse");
        foreach (var window in new[] { float.NaN, float.PositiveInfinity, 0f, 0.04f, 1.01f })
            True(test.Coordinator.TryClaimLateFacing(target, window) is null, "invalid facing windows fail closed");
        var facing = test.Coordinator.TryClaimLateFacing(target, 0.15f);
        True(facing is { TargetEntityId: 22, TargetId: Target }, "one claim retains the same exact cast target");
        True(test.Coordinator.TryClaimLateFacing(target, 0.15f) is null,
            "facing never repeats, even if its void native boundary fails");
        True(test.Coordinator.GetLateFacingTarget(0.15f) is null,
            "no further target lookup is requested after the one-shot claim");
        True(test.Coordinator.Status.LateFacingAttempts == 1, "attempts are counted, not reported as hit success");

        var dummy = new Harness();
        const ulong fullObjectId = 0x100001234;
        var dummyRequest = dummy.Coordinator.Begin(Ogi, Ogi, fullObjectId, 7, true, targetEntityId: 22);
        True(dummyRequest is not null, "an admitted battle character or dummy retains its full 64-bit object identity");
        dummy.Coordinator.Execute(dummyRequest, () =>
        {
            dummy.Snapshot = dummy.Snapshot with
            {
                IsCasting = true, CastActionId = Ogi, AdjustedCastActionId = Ogi,
                CastTargetGameObjectId = fullObjectId, CurrentCastTime = 1.4f, TotalCastTime = 1.5f,
            };
            return true;
        });
        True(dummy.Coordinator.TryClaimLateFacing(new(fullObjectId, 22), 0.15f) is not null,
            "coordinator target proof is actor identity, not a player-only job filter");

        foreach (var changed in new Func<SamuraiCastProtectionSnapshot, SamuraiCastProtectionSnapshot>[]
        {
            value => value with { CurrentCastTime = float.NaN },
            value => value with { TotalCastTime = float.PositiveInfinity },
            value => value with { CurrentCastTime = -1 },
            value => value with { CurrentCastTime = 1.6f },
            value => value with { CurrentCastTime = 1.5f },
            value => value with { CastTargetGameObjectId = Target + 1 },
            value => value with { CastBreakingCrowdControl = true },
            value => value with { GuardActive = true },
        })
        {
            var invalid = new Harness();
            invalid.Coordinator.Execute(invalid.Begin(), () => { invalid.ObserveCast(); return true; });
            invalid.Snapshot = changed(invalid.Snapshot with { CurrentCastTime = 1.4f, TotalCastTime = 1.5f });
            True(invalid.Coordinator.TryClaimLateFacing(target, 0.15f) is null,
                "invalid native timing, changed cast target or interruption prevents facing");
        }

        EarlierFacingLeadPrecedesIllustrativeCastLossForBothStarters();
    }

    private static void EarlierFacingLeadPrecedesIllustrativeCastLossForBothStarters()
    {
        var target = new TargetPressureActorIdentity(Target, 22);
        const float castSeconds = 1.3f;
        var lead = SamuraiOgiCastProtectionRules.DefaultFacingLeadSeconds;
        foreach (var (rawActionId, resolvedActionId) in new[]
        {
            (Ogi, Ogi),
            (SamuraiSmartActionCastRules.TendoSetsugekkaCarrierActionId,
                SamuraiSmartActionCastRules.TendoSetsugekkaActionId),
        })
        {
            // These are injected timing regressions, not measurements of the
            // server snapshot or proof that either action lands in live PvP.
            foreach (var lossRemainingSeconds in new[] { 0.33f, 0.27f, 0.25f })
            {
                var test = new Harness();
                var request = test.Coordinator.Begin(rawActionId, resolvedActionId, Target, 7, true,
                    targetEntityId: target.EntityId);
                True(request is not null, "both reviewed 1.3s cast starters acquire exact ownership");
                test.Coordinator.Execute(request, () =>
                {
                    test.Snapshot = test.Snapshot with
                    {
                        IsCasting = true,
                        CastActionId = resolvedActionId,
                        AdjustedCastActionId = resolvedActionId,
                        CastTargetGameObjectId = Target,
                        CurrentCastTime = 0f,
                        TotalCastTime = castSeconds,
                    };
                    return true;
                });

                test.Now += 680;
                test.Snapshot = test.Snapshot with { CurrentCastTime = 0.68f };
                True(test.Coordinator.GetLateFacingTarget(lead) is null &&
                     test.Coordinator.TryClaimLateFacing(target, lead) is null,
                    "0.62s remaining is before the default 0.60s lead and cannot face");
                True(test.Coordinator.Status.LateFacingAttempts == 0,
                    "frames before the configured lead do not spend the one-shot claim");

                test.Now += 40;
                test.Snapshot = test.Snapshot with { CurrentCastTime = 0.72f };
                True(test.Coordinator.GetLateFacingTarget(lead) == target,
                    "the first frame crossing the lead exposes only the same frozen target");
                True(test.Coordinator.TryClaimLateFacing(target, lead) is not null,
                    "the first eligible frame claims facing at 0.58s remaining");
                True(castSeconds - test.Snapshot.CurrentCastTime > lossRemainingSeconds,
                    "the earlier claim precedes each injected 0.33/0.27/0.25s cast-state loss");

                test.Now = 1_000 + (long)((castSeconds - lossRemainingSeconds) * 1_000);
                test.Snapshot = test.Snapshot with
                {
                    CurrentCastTime = castSeconds - lossRemainingSeconds,
                };
                True(test.Coordinator.GetLateFacingTarget(lead) is null &&
                     test.Coordinator.TryClaimLateFacing(target, lead) is null,
                    "later frames never repeat the already claimed facing attempt");
                test.Snapshot = test.Snapshot with { IsCasting = false };
                True(test.Coordinator.TryClaimLateFacing(target, lead) is null,
                    "an early cast-state loss never authorizes a post-cast facing attempt");
                False(test.Coordinator.HasOwnership, "native cast loss still releases ownership immediately");
                True(test.Coordinator.Status.LateFacingAttempts == 1,
                    "crossing the lead produces one attempt, not a retry or a claimed hit");
            }
        }

        var upperBound = new Harness();
        upperBound.Coordinator.Execute(upperBound.Begin(), () =>
        {
            upperBound.ObserveCast();
            upperBound.Snapshot = upperBound.Snapshot with
            {
                CurrentCastTime = 0.4f,
                TotalCastTime = castSeconds,
            };
            return true;
        });
        True(upperBound.Coordinator.TryClaimLateFacing(target,
                SamuraiOgiCastProtectionRules.MaximumFacingLeadSeconds) is not null,
            "the supported 1s upper lead bound remains a one-shot exact-cast option");
    }

    private static ClientActionAttemptFingerprint EmptyQueue => default(ClientActionAttemptFingerprint) with { Captured = true };
    private static ClientActionAttemptFingerprint QueuedOgi => EmptyQueue with
    {
        ActionQueued = true, QueuedActionType = (uint)ActionType.Action,
        QueuedActionId = Ogi, QueuedTargetId = Target,
    };
    private static SamuraiCastProtectionSnapshot ValidSnapshot =>
        new(true, 1, new(100, 10), true, 34, 7, false, false, false, 0, 0, 0) { Queue = EmptyQueue };
    private static QueuedHelperQueueInvocation Invocation() => new(
        ActionType.Action, Ogi, Target, 0, ActionManager.UseActionMode.Queue, 0);

    private sealed class Harness
    {
        internal long Now = 1_000;
        internal int NativeCalls;
        internal SamuraiCastProtectionSnapshot Snapshot = ValidSnapshot;
        internal SamuraiCastProtectionCoordinator Coordinator { get; }
        internal SamuraiCastMovementInputBoundary Movement { get; }
        internal Harness()
        {
            Coordinator = new(() => Snapshot, () => Now);
            Movement = new((_, _, _) => true, Coordinator.IsProtected);
        }
        internal SamuraiCastProtectionRequest Begin() => Coordinator.Begin(Ogi, Ogi, Target, 7, true, targetEntityId: 22)
            ?? throw new InvalidOperationException("Expected exact request ownership.");
        internal void ObserveCast() => Snapshot = Snapshot with
        {
            IsCasting = true, CastActionId = Ogi, AdjustedCastActionId = Ogi, CastTargetGameObjectId = Target,
        };
        internal void QueueOgi() => Coordinator.Execute(Begin(), () =>
        {
            NativeCalls++;
            Snapshot = Snapshot with { Queue = QueuedOgi };
            return true;
        });
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void False(bool value, string message) => True(!value, message);
}
