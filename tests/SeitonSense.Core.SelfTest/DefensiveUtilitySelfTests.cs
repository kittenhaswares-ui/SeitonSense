using SeitonSense.Core;

internal static class DefensiveUtilitySelfTests
{
    public static void IndependentGuardianAndGuardPassesAggregateCurrentFrameOnly()
    {
        var guardianWait = new DefensiveUtilityFramePass(
            DefensiveUtilityActionKind.Guardian,
            InputClaimed: true,
            UseActionAttempted: false,
            UseActionAccepted: false);
        var idleGuard = new DefensiveUtilityFramePass(
            DefensiveUtilityActionKind.None,
            InputClaimed: false,
            UseActionAttempted: false,
            UseActionAccepted: false);
        var guardianOwned = DefensiveUtilityRules.AggregateFramePasses(
            guardianWait,
            idleGuard);
        True(guardianOwned.GuardianOwnsPresentation, "Guardian cast/throttle wait stays visible");
        True(guardianOwned.InputClaimed, "Guardian claim survives later Guard pass");
        False(guardianOwned.UseActionAttempted, "waiting Guardian invents no attempt");

        var idleGuardian = new DefensiveUtilityFramePass(
            DefensiveUtilityActionKind.None,
            InputClaimed: false,
            UseActionAttempted: false,
            UseActionAccepted: false);
        var acceptedGuard = new DefensiveUtilityFramePass(
            DefensiveUtilityActionKind.Guard,
            InputClaimed: true,
            UseActionAttempted: true,
            UseActionAccepted: true);
        var guardOwned = DefensiveUtilityRules.AggregateFramePasses(
            idleGuardian,
            acceptedGuard);
        False(guardOwned.GuardianOwnsPresentation, "idle Guardian cannot mask current Guard");
        True(guardOwned.InputClaimed, "Guard claim is aggregated");
        True(guardOwned.UseActionAttempted, "Guard attempt is aggregated");
        True(guardOwned.UseActionAccepted, "Guard acceptance is aggregated");

        var yieldedGuardian = new DefensiveUtilityFramePass(
            DefensiveUtilityActionKind.Guardian,
            InputClaimed: false,
            UseActionAttempted: false,
            UseActionAccepted: false);
        var guardAfterUnavailableGuardian = DefensiveUtilityRules.AggregateFramePasses(
            yieldedGuardian,
            acceptedGuard);
        False(
            guardAfterUnavailableGuardian.GuardianOwnsPresentation,
            "unready Guardian candidate cannot mask the Guard which actually acted");
        True(
            guardAfterUnavailableGuardian.UseActionAttempted,
            "later Guard attempt remains the presented frame owner");
        True(
            guardAfterUnavailableGuardian.UseActionAccepted,
            "later Guard acceptance remains visible");

        var unavailableGuardianWithIdleGuard = DefensiveUtilityRules.AggregateFramePasses(
            yieldedGuardian,
            idleGuard);
        True(
            unavailableGuardianWithIdleGuard.GuardianOwnsPresentation,
            "unready Guardian remains visible while the later Guard pass is idle");
        False(
            unavailableGuardianWithIdleGuard.InputClaimed,
            "background Guardian diagnostics cannot synthesize a claim");
        False(
            unavailableGuardianWithIdleGuard.UseActionAttempted,
            "background Guardian diagnostics cannot synthesize an attempt");

        var allIdle = DefensiveUtilityRules.AggregateFramePasses(
            idleGuardian,
            idleGuard);
        False(allIdle.GuardianOwnsPresentation, "no stale prior-frame owner is synthesized");
        False(allIdle.InputClaimed, "no stale prior-frame claim is synthesized");
        False(allIdle.UseActionAttempted, "no stale prior-frame attempt is synthesized");
    }

    public static void ExactThresholdsAreInclusiveAndSafe()
    {
        True(DefensiveUtilityRules.IsHighPressure(true, 3), "three enemies is high pressure");
        False(DefensiveUtilityRules.IsHighPressure(true, 2), "two enemies is not high pressure");
        False(DefensiveUtilityRules.IsHighPressure(false, 99), "unknown pressure fails closed");

        True(DefensiveUtilityRules.IsAtOrBelowHpPercent(5_000, 10_000, 50), "50 percent inclusive");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(5_001, 10_000, 50), "above 50 percent");
        True(DefensiveUtilityRules.IsAtOrBelowHpPercent(2_000, 10_000, 20), "20 percent inclusive");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(2_001, 10_000, 20), "above 20 percent");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(0, 10_000, 50), "dead actor rejected");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(1, 0, 50), "zero maximum rejected");
        False(DefensiveUtilityRules.IsAtOrBelowHpPercent(101, 100, 50), "invalid health rejected");

        True(
            DefensiveUtilityRules.IsAtOrBelowHpPercent(uint.MaxValue / 5, uint.MaxValue, 20),
            "large health values remain overflow safe");
    }

    public static void PostPurifyGuardRequiresPositiveConfirmation()
    {
        True(
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                false,
                true,
                false,
                3_000,
                2_000),
            "positive Resilience plus CC absence inside the window");
        False(
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                true,
                false,
                true,
                3_000,
                2_000),
            "an attempted Purify alone never releases Guard");
        False(
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                false,
                true,
                true,
                3_000,
                2_000),
            "remaining CC blocks Guard despite Resilience");
        False(
            DefensiveUtilityRules.CanDispatchPostPurifyGuard(
                false,
                true,
                false,
                3_000,
                3_000),
            "expiry boundary fails closed");
    }

    public static void GuardPropagationLatchIsBoundedAndNonRearming()
    {
        var armed = DefensiveUtilityRules.ObserveGuardPropagation(
            GuardPropagationState.Initial,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 1_000);
        True(armed.PropagationLatchActive, "a real Guard attempt arms propagation suppression");
        True(armed.SuppressDirectActionHelpers, "the latch blocks direct helpers");
        Equal(1_500L, armed.RemainingMilliseconds, "the latch is bounded from the attempt timestamp");

        var duplicate = DefensiveUtilityRules.ObserveGuardPropagation(
            armed.NextState,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 1_400);
        Equal(1_100L, duplicate.RemainingMilliseconds, "re-observing one attempt cannot extend its deadline");

        var exact = DefensiveUtilityRules.ObserveGuardPropagation(
            duplicate.NextState,
            exactGuardActive: true,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 1_500);
        True(exact.SuppressDirectActionHelpers, "exact live Guard owns the gate once visible");
        False(exact.PropagationLatchActive, "the propagation latch retires when exact Guard appears");

        var ended = DefensiveUtilityRules.ObserveGuardPropagation(
            exact.NextState,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 2_600);
        False(ended.SuppressDirectActionHelpers, "an old observation cannot rearm after Guard ends");

        var timedOut = DefensiveUtilityRules.ObserveGuardPropagation(
            armed.NextState,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_000,
            nowMilliseconds: 2_500);
        False(timedOut.SuppressDirectActionHelpers, "the exact timeout boundary releases suppression");

        var future = DefensiveUtilityRules.ObserveGuardPropagation(
            GuardPropagationState.Initial,
            exactGuardActive: false,
            observedGuardAttemptAtMilliseconds: 1_001,
            nowMilliseconds: 1_000);
        False(future.SuppressDirectActionHelpers, "a future timestamp fails closed without inventing a latch");
    }

    public static void GuardRejectionRollbackIsExactAndSynchronous()
    {
        True(
            DefensiveUtilityRules.CanRetractRejectedGuardAttempt(
                latestGeneration: 42,
                generationBeforeCall: 41,
                clientExplicitlyRejected: true,
                acceptanceAmbiguous: false,
                identityMatches: true),
            "the exact immediately rejected generation may retract");
        False(
            DefensiveUtilityRules.CanRetractRejectedGuardAttempt(42, 41, false, false, true),
            "client true preserves propagation");
        False(
            DefensiveUtilityRules.CanRetractRejectedGuardAttempt(42, 41, true, true, true),
            "exception ambiguity preserves propagation");
        False(
            DefensiveUtilityRules.CanRetractRejectedGuardAttempt(43, 41, true, false, true),
            "an intervening generation cannot retract");
        False(
            DefensiveUtilityRules.CanRetractRejectedGuardAttempt(42, 41, true, false, false),
            "wrong local identity cannot retract");
    }

    public static void AutoGuardProtectionOwnershipRequiresTheExactConfirmedAttempt()
    {
        var local = new TargetPressureActorIdentity(0x1001, 0x2001);
        True(
            AutoGuardProtectionRules.CanArmFromConfirmedAttempt(
                latestGuardAttemptGeneration: 42,
                generationBeforeCall: 41,
                latestTerritoryId: 250,
                currentTerritoryId: 250,
                latestLocalPlayer: local,
                currentLocalPlayer: local,
                observedAtMilliseconds: 1_000,
                nowMilliseconds: 1_001,
                exactGuardActive: true),
            "the exact hook generation plus live Guard status may own protection");
        False(
            AutoGuardProtectionRules.CanArmFromConfirmedAttempt(
                42,
                41,
                250,
                250,
                local,
                local,
                1_000,
                1_001,
                exactGuardActive: false),
            "a provisional client-true request without exact status cannot own protection");
        False(
            AutoGuardProtectionRules.CanArmFromConfirmedAttempt(
                41,
                41,
                250,
                250,
                local,
                local,
                1_000,
                1_001,
                exactGuardActive: true),
            "a manual or missing hook generation cannot own Guard");
        False(
            AutoGuardProtectionRules.CanArmFromConfirmedAttempt(
                42,
                41,
                250,
                250,
                local,
                local with { EntityId = 0x2002 },
                1_000,
                1_001,
                exactGuardActive: true),
            "local identity drift cannot own Guard");
        True(
            AutoGuardProtectionRules.CanArmFromConfirmedAttempt(
                1,
                long.MaxValue,
                250,
                250,
                local,
                local,
                1_000,
                1_001,
                exactGuardActive: true),
            "generation wrap remains exact");
    }

    public static void AutoGuardProtectionStartsOnlyAfterExactStatus()
    {
        var local = new TargetPressureActorIdentity(0x1001, 0x2001);
        var provisional = AutoGuardProtectionRules.ArmConfirmed(
            42,
            250,
            local,
            1_000,
            exactGuardActive: false);
        False(provisional.IsArmed, "client acceptance without exact status cannot arm protection");

        var armed = AutoGuardProtectionRules.ArmConfirmed(
            42,
            250,
            local,
            1_100,
            exactGuardActive: true);
        True(armed.IsArmed, "exact live Guard confirmation arms protection");

        var protectedNow = AutoGuardProtectionRules.Observe(
            armed,
            ProtectionObservation(local, exactGuardActive: true, actionCanCancelGuard: false, now: 1_100));
        False(protectedNow.ShouldBlockAction, "non-cancelling observation remains untouched");
        True(protectedNow.NextState.ExactGuardObserved, "armed ownership is already confirmed");

        var protectedLate = AutoGuardProtectionRules.Observe(
            protectedNow.NextState,
            ProtectionObservation(local, exactGuardActive: true, actionCanCancelGuard: true, now: 5_500));
        True(protectedLate.ShouldBlockAction, "protection follows the full live automatic Guard status");

        var ended = AutoGuardProtectionRules.Observe(
            protectedLate.NextState,
            ProtectionObservation(local, exactGuardActive: false, actionCanCancelGuard: true, now: 5_501));
        False(ended.ShouldBlockAction, "the first exact absent frame releases normal actions");
        False(ended.NextState.IsArmed, "ended Guard cannot retain stale ownership");
        Equal(AutoGuardProtectionDecisionReason.GuardEnded, ended.Reason, "status-end reason");
    }

    public static void AutoGuardProtectionHasExplicitAndBoundedReleasePaths()
    {
        var local = new TargetPressureActorIdentity(0x1001, 0x2001);
        var armed = AutoGuardProtectionRules.ArmConfirmed(42, 250, local, 1_000, true);

        var exactRepeat = new GuardRepeatProtectionObservation(
            RuntimeEnabled: true,
            IsSupportedPvpContext: true,
            ExactGuardRequest: true,
            ExactLocalGuardActive: true,
            ExactOwnGuardAttemptObserved: true,
            OwnGuardAttemptAtMilliseconds: 1_000,
            NowMilliseconds: 1_999);
        True(
            GuardRepeatProtectionRules.ShouldBlock(exactRepeat),
            "manual or automatic Guard repeat is blocked before one second");
        False(
            GuardRepeatProtectionRules.ShouldBlock(
                exactRepeat with { NowMilliseconds = 2_000 }),
            "Guard repeat passes at the exact one-second boundary");
        False(
            GuardRepeatProtectionRules.ShouldBlock(
                exactRepeat with { ExactGuardRequest = false }),
            "a different action is never blocked by the repeat-only policy");
        False(
            GuardRepeatProtectionRules.ShouldBlock(
                exactRepeat with { ExactLocalGuardActive = false }),
            "a provisional or rejected request cannot create a phantom block");
        False(
            GuardRepeatProtectionRules.ShouldBlock(
                exactRepeat with { ExactOwnGuardAttemptObserved = false }),
            "missing exact own attempt fails open");
        False(
            GuardRepeatProtectionRules.ShouldBlock(
                exactRepeat with { IsSupportedPvpContext = false }),
            "unsupported context fails open");
        False(
            GuardRepeatProtectionRules.ShouldBlock(
                exactRepeat with { RuntimeEnabled = false }),
            "disabled runtime fails open");
        False(
            GuardRepeatProtectionRules.ShouldBlock(
                exactRepeat with { NowMilliseconds = 999 }),
            "clock rollback fails open");

        var explicitRelease = AutoGuardProtectionRules.Observe(
            armed,
            ProtectionObservation(
                local,
                exactGuardActive: true,
                actionCanCancelGuard: false,
                now: 1_100,
                explicitGuardReuse: true));
        False(explicitRelease.ShouldBlockAction, "the independent repeat gate owns the one-second block");
        False(explicitRelease.NextState.IsArmed, "an allowed Guard reuse atomically releases automatic ownership");
        Equal(
            AutoGuardProtectionDecisionReason.ExplicitGuardReuse,
            explicitRelease.Reason,
            "explicit release reason");

        var maximum = AutoGuardProtectionRules.Observe(
            armed,
            ProtectionObservation(local, exactGuardActive: true, actionCanCancelGuard: true, now: 7_000));
        False(maximum.ShouldBlockAction, "the hard maximum boundary fails open");
        False(maximum.NextState.IsArmed, "the hard maximum clears stale status ownership");

        var ended = AutoGuardProtectionRules.Observe(
            armed,
            ProtectionObservation(local, exactGuardActive: false, actionCanCancelGuard: true, now: 2_500));
        False(ended.ShouldBlockAction, "missing exact status releases immediately after ownership");
        Equal(
            AutoGuardProtectionDecisionReason.GuardEnded,
            ended.Reason,
            "status-end reason");
    }

    public static void AutoGuardProtectionContextDriftAlwaysFailsOpen()
    {
        var local = new TargetPressureActorIdentity(0x1001, 0x2001);
        var armed = AutoGuardProtectionRules.ArmConfirmed(42, 250, local, 1_000, true);

        var disabled = AutoGuardProtectionRules.Observe(
            armed,
            ProtectionObservation(local, true, true, 1_100) with { RuntimeEnabled = false });
        False(disabled.ShouldBlockAction, "disabling Auto-Guard releases input");

        var territory = AutoGuardProtectionRules.Observe(
            armed,
            ProtectionObservation(local, true, true, 1_100) with { TerritoryId = 251 });
        False(territory.ShouldBlockAction, "territory drift releases input");

        var player = AutoGuardProtectionRules.Observe(
            armed,
            ProtectionObservation(local, true, true, 1_100) with
            {
                LocalPlayer = local with { EntityId = 0x2002 },
            });
        False(player.ShouldBlockAction, "player identity drift releases input");

        var unavailable = AutoGuardProtectionRules.Observe(
            armed,
            ProtectionObservation(local, true, true, 1_100) with { LocalPlayerLive = false });
        False(unavailable.ShouldBlockAction, "death or unavailable local state releases input");

        var unknownAction = AutoGuardProtectionRules.Observe(
            armed,
            ProtectionObservation(local, true, actionCanCancelGuard: false, now: 1_100));
        False(unknownAction.ShouldBlockAction, "unknown action resolution fails open");
        True(unknownAction.NextState.IsArmed, "one unknown action does not destroy valid ownership");
    }

    public static void AutoGuardConfirmationIsStatusFirstAndRetriesOnlyOnce()
    {
        True(
            AutoGuardConfirmationRules.ShouldRetainUnspentRetry(
                ClientActionAttemptOutcome.SoftUnavailable),
            "a pre-native retry race retains the unspent retry");
        False(
            AutoGuardConfirmationRules.ShouldRetainUnspentRetry(
                ClientActionAttemptOutcome.ClientRejected),
            "a crossed clean-false boundary spends the retry");

        var local = new TargetPressureActorIdentity(0x1001, 0x2001);
        var pending = AutoGuardConfirmationRules.ArmProvisional(
            generationBeforeCall: 41,
            territoryId: 250,
            local,
            requestedAtMilliseconds: 1_000,
            opportunityExpiresAtMilliseconds: 4_000,
            confirmationRetrySpent: false);
        True(pending.IsPending, "a bounded provisional request starts confirmation only");

        var waiting = AutoGuardConfirmationRules.Observe(
            pending,
            ConfirmationObservation(local, exactGuardActive: false, AutoGuardRetryReadiness.Ready, 2_499));
        False(waiting.Confirmed, "client true alone is not confirmation");
        False(waiting.ShouldRetry, "no retry occurs before the exact confirmation timeout");

        var retry = AutoGuardConfirmationRules.Observe(
            waiting.NextState,
            ConfirmationObservation(local, exactGuardActive: false, AutoGuardRetryReadiness.Ready, 2_500));
        True(retry.ShouldRetry, "exact readiness allows one retry at the timeout");

        var retried = AutoGuardConfirmationRules.ArmProvisional(
            generationBeforeCall: 42,
            territoryId: 250,
            local,
            requestedAtMilliseconds: 2_500,
            opportunityExpiresAtMilliseconds: 4_000,
            confirmationRetrySpent: true);
        var retryExhausted = AutoGuardConfirmationRules.Observe(
            retried,
            ConfirmationObservation(local, exactGuardActive: false, AutoGuardRetryReadiness.Ready, 4_000));
        False(retryExhausted.ShouldRetry, "a second provisional true can never dispatch a third call");
        False(retryExhausted.NextState.IsPending, "the one-retry episode retires without exact status");

        var confirmed = AutoGuardConfirmationRules.Observe(
            pending,
            ConfirmationObservation(local, exactGuardActive: true, AutoGuardRetryReadiness.Unknown, 1_100));
        True(confirmed.Confirmed, "exact live Guard confirms before any retry decision");
        False(confirmed.ShouldRetry, "confirmation never also retries");
    }

    public static void AutoGuardConfirmationFailsClosedOnReadinessOrContextDrift()
    {
        var local = new TargetPressureActorIdentity(0x1001, 0x2001);
        var pending = AutoGuardConfirmationRules.ArmProvisional(
            41,
            250,
            local,
            1_000,
            4_000,
            confirmationRetrySpent: false);

        var busy = AutoGuardConfirmationRules.Observe(
            pending,
            ConfirmationObservation(local, false, AutoGuardRetryReadiness.NativeBoundaryBusy, 2_500));
        True(busy.NextState.IsPending, "a transient native boundary waits inside the original lease");
        False(busy.ShouldRetry, "busy native state cannot cross the boundary");

        var cooldown = AutoGuardConfirmationRules.Observe(
            pending,
            ConfirmationObservation(local, false, AutoGuardRetryReadiness.CooldownUnavailable, 2_500));
        False(cooldown.NextState.IsPending, "cooldown evidence retires without another request");
        False(cooldown.ShouldRetry, "cooldown-unavailable Guard is never retried");

        var unknown = AutoGuardConfirmationRules.Observe(
            pending,
            ConfirmationObservation(local, false, AutoGuardRetryReadiness.Unknown, 2_500));
        False(unknown.NextState.IsPending, "unknown readiness retires fail closed");

        var drift = AutoGuardConfirmationRules.Observe(
            pending,
            ConfirmationObservation(local with { EntityId = 0x2002 }, false, AutoGuardRetryReadiness.Ready, 1_100));
        False(drift.NextState.IsPending, "local actor drift retires provisional ownership");
        False(drift.Confirmed, "another actor's Guard cannot confirm this request");
    }

    public static void GuardianEligibilityUsesNativeReachability()
    {
        var valid = Candidate(10, hp: 20, maxHp: 100, distance: 15f);
        True(DefensiveUtilityRules.IsGuardianCandidate(valid), "ten-to-twenty yalms accepted when native reachability succeeds");
        True(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { DistanceSquared = 21f * 21f }),
            "native hitbox-aware reachability remains authoritative above a raw center-distance boundary");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { CurrentHp = 21 }),
            "above twenty percent rejected");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { IsExactPartyMember = false }),
            "non-party actor rejected");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { HasNativeRangeAndLineOfSight = false }),
            "native reachability required");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { DistanceSquared = float.NaN }),
            "non-finite distance rejected");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { DistanceSquared = -1f }),
            "negative distance rejected");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(valid with { GameObjectId = 0 }),
            "invalid identity rejected");
    }

    public static void GuardianProactiveRiskRequiresExactHighPressure()
    {
        var proactive = Candidate(10, hp: 50, maxHp: 100, pressure: 3);
        Equal(
            PaladinGuardianRiskTier.ProactiveHighPressure,
            DefensiveUtilityRules.ClassifyGuardianRisk(proactive),
            "exactly 50 percent with exact 3+ pressure enters the proactive tier");
        True(
            DefensiveUtilityRules.IsGuardianCandidate(proactive),
            "proactive risk remains subject to the ordinary exact actor and native reachability gates");
        Equal(
            PaladinGuardianRiskTier.None,
            DefensiveUtilityRules.ClassifyGuardianRisk(proactive with { CurrentHp = 51 }),
            "above 50 percent is not proactive");
        Equal(
            PaladinGuardianRiskTier.None,
            DefensiveUtilityRules.ClassifyGuardianRisk(proactive with { IncomingEnemyCount = 2 }),
            "two enemies cannot open the 50-percent tier");
        var moderate = proactive with { CurrentHp = 40, IncomingEnemyCount = 2 };
        Equal(
            PaladinGuardianRiskTier.ProactiveHighPressure,
            DefensiveUtilityRules.ClassifyGuardianRisk(moderate),
            "exactly 40 percent with exact 2+ pressure enters the moderate tier");
        Equal(
            PaladinGuardianRiskTier.None,
            DefensiveUtilityRules.ClassifyGuardianRisk(moderate with { CurrentHp = 41 }),
            "two enemies cannot open the tier above 40 percent");
        Equal(
            PaladinGuardianRiskTier.None,
            DefensiveUtilityRules.ClassifyGuardianRisk(moderate with { IncomingEnemyCount = 1 }),
            "one enemy cannot open either proactive tier");
        Equal(
            PaladinGuardianRiskTier.None,
            DefensiveUtilityRules.ClassifyGuardianRisk(proactive with { IncomingEnemyCount = null }),
            "unknown or stale pressure does not raise the legacy threshold");
        Equal(
            PaladinGuardianRiskTier.None,
            DefensiveUtilityRules.ClassifyGuardianRisk(proactive with { IncomingEnemyCount = 6 }),
            "malformed impossible pressure fails closed");
        Equal(
            PaladinGuardianRiskTier.Critical,
            DefensiveUtilityRules.ClassifyGuardianRisk(
                proactive with { CurrentHp = 20, IncomingEnemyCount = null }),
            "the original 20-percent boundary stays unconditional");
        Equal(
            PaladinGuardianRiskTier.Critical,
            DefensiveUtilityRules.ClassifyGuardianRisk(
                proactive with { CurrentHp = 20, IncomingEnemyCount = 99 }),
            "malformed pressure cannot disable a critical rescue");
        False(
            DefensiveUtilityRules.IsGuardianCandidate(
                proactive with { HasNativeRangeAndLineOfSight = false }),
            "high pressure never bypasses native reachability");
    }

    public static void GuardianPressurePublicationFreshnessIsBounded()
    {
        True(
            DefensiveUtilityRules.IsFreshGuardianPressurePublication(1_000, 750),
            "the exact 250-ms pressure-age boundary is inclusive");
        False(
            DefensiveUtilityRules.IsFreshGuardianPressurePublication(1_000, 749),
            "pressure older than 250 ms cannot raise the legacy threshold");
        False(
            DefensiveUtilityRules.IsFreshGuardianPressurePublication(1_000, 1_001),
            "a future pressure publication fails closed");
        False(
            DefensiveUtilityRules.IsFreshGuardianPressurePublication(-1, 0),
            "an invalid current clock fails closed");
        False(
            DefensiveUtilityRules.IsFreshGuardianPressurePublication(0, -1),
            "an invalid publication clock fails closed");
    }

    public static void GuardianRankingIsDeterministic()
    {
        var candidates = new[]
        {
            Candidate(10, hp: 20, maxHp: 100, pressure: 5, distance: 3f, partySlot: 2),
            Candidate(20, hp: 10, maxHp: 100, pressure: 1, distance: 8f, partySlot: 3),
            Candidate(30, hp: 10, maxHp: 100, pressure: 4, distance: 7f, partySlot: 4),
            Candidate(40, hp: 10, maxHp: 100, pressure: 4, distance: 4f, partySlot: 5),
        };

        Equal(3, DefensiveUtilityRules.SelectGuardianCandidateIndex(candidates),
            "health, pressure, then distance decide");

        var spent = new HashSet<TargetPressureActorIdentity> { candidates[3].Actor };
        Equal(2, DefensiveUtilityRules.SelectGuardianCandidateIndex(candidates, spent),
            "spent exact actor is excluded without changing target identity");
        Equal(-1, DefensiveUtilityRules.SelectGuardianCandidateIndex(null),
            "missing candidates fail closed");

        var tiered = new[]
        {
            Candidate(50, hp: 21, maxHp: 100, pressure: 5, partySlot: 2),
            Candidate(60, hp: 20, maxHp: 100, pressure: 0, partySlot: 3),
        };
        Equal(
            1,
            DefensiveUtilityRules.SelectGuardianCandidateIndex(tiered),
            "the unconditional critical tier always precedes proactive pressure");

        var proactive = new[]
        {
            Candidate(70, hp: 21, maxHp: 100, pressure: 3, distance: 2f, partySlot: 2),
            Candidate(80, hp: 34, maxHp: 100, pressure: 5, distance: 8f, partySlot: 3),
            Candidate(90, hp: 25, maxHp: 100, pressure: 5, distance: 4f, partySlot: 4),
        };
        Equal(
            2,
            DefensiveUtilityRules.SelectGuardianCandidateIndex(proactive),
            "inside the proactive tier pressure wins first, then exact HP");
    }

    public static void GuardianTriggerPopupIsAcceptedOnlyAndBounded()
    {
        var rejected = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.PaladinGuardianLowAlly,
            useActionAttempted: true,
            useActionAccepted: false,
            selectedPartySlot: 3,
            nowMilliseconds: 1_000);
        True(rejected is null, "a rejected request never creates a popup");

        var wrongAction = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guard,
            DefensiveUtilityTrigger.ReservedRemovedPreGuard,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 3,
            nowMilliseconds: 1_000);
        True(wrongAction is null, "Guard acceptance cannot masquerade as Guardian");

        var wrongTrigger = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.ReservedRemovedPreGuard,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 3,
            nowMilliseconds: 1_000);
        True(wrongTrigger is null, "a non-Guardian trigger cannot create the card");

        var invalidSlot = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.PaladinGuardianLowAlly,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 0,
            nowMilliseconds: 1_000);
        True(invalidSlot is null, "invalid party slot fails closed");

        var invalidTime = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.PaladinGuardianLowAlly,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 3,
            nowMilliseconds: -1);
        True(invalidTime is null, "invalid time fails closed");

        var accepted = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            DefensiveUtilityTrigger.PaladinGuardianLowAlly,
            useActionAttempted: true,
            useActionAccepted: true,
            selectedPartySlot: 3,
            nowMilliseconds: 1_000);
        True(accepted is not null, "an accepted automatic Guardian creates a popup");
        var popup = accepted!.Value;
        Equal(3, popup.PartySlot, "popup retains only the selected party slot");
        Equal(2_500L, popup.EndsAtMilliseconds, "popup lifetime is exactly 1500 ms");

        var retained = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            accepted,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.None,
            DefensiveUtilityTrigger.None,
            useActionAttempted: false,
            useActionAccepted: false,
            selectedPartySlot: 0,
            nowMilliseconds: 2_499);
        Equal(2_500L, retained!.Value.EndsAtMilliseconds, "later idle frames cannot extend the popup");

        var expired = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            retained,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.None,
            DefensiveUtilityTrigger.None,
            useActionAttempted: false,
            useActionAccepted: false,
            selectedPartySlot: 0,
            nowMilliseconds: 2_500);
        True(expired is null, "popup expires at the exact duration boundary");

        var disabled = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            accepted,
            runtimeEnabled: false,
            DefensiveUtilityActionKind.None,
            DefensiveUtilityTrigger.None,
            useActionAttempted: false,
            useActionAccepted: false,
            selectedPartySlot: 0,
            nowMilliseconds: 1_100);
        True(disabled is null, "disabling the runtime clears the popup immediately");

        var reset = DefensiveUtilityRules.ObserveGuardianTriggerPopup(
            accepted,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.None,
            DefensiveUtilityTrigger.None,
            useActionAttempted: false,
            useActionAccepted: false,
            selectedPartySlot: 0,
            nowMilliseconds: 1_100,
            hardReset: true);
        True(reset is null, "hard reset clears the popup immediately");
    }

    public static void AutoGuardTriggerPopupIsConfirmedOnlyAndDeduplicated()
    {
        var provisional = DefensiveUtilityRules.ObserveAutoGuardTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guard,
            exactActivationConfirmed: false,
            confirmedAttemptToken: 1,
            nowMilliseconds: 1_000);
        True(provisional is null, "a provisional client-true Guard never creates a popup");

        var guardian = DefensiveUtilityRules.ObserveAutoGuardTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guardian,
            exactActivationConfirmed: true,
            confirmedAttemptToken: 1,
            nowMilliseconds: 1_000);
        True(guardian is null, "Guardian confirmation cannot masquerade as Auto-Guard");

        var confirmed = DefensiveUtilityRules.ObserveAutoGuardTriggerPopup(
            previous: null,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guard,
            exactActivationConfirmed: true,
            confirmedAttemptToken: 7,
            nowMilliseconds: 1_000);
        True(confirmed is not null, "confirmed Auto-Guard creates a popup");
        Equal(7L, confirmed!.Value.Token, "popup retains exact confirmed-attempt token");
        Equal(3_000L, confirmed.Value.EndsAtMilliseconds, "Auto-Guard popup matches the two-second reuse lock");

        var duplicate = DefensiveUtilityRules.ObserveAutoGuardTriggerPopup(
            confirmed,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.Guard,
            exactActivationConfirmed: true,
            confirmedAttemptToken: 7,
            nowMilliseconds: 1_100);
        Equal(confirmed.Value, duplicate!.Value, "same confirmed attempt cannot restart the popup");

        var expired = DefensiveUtilityRules.ObserveAutoGuardTriggerPopup(
            duplicate,
            runtimeEnabled: true,
            DefensiveUtilityActionKind.None,
            exactActivationConfirmed: false,
            confirmedAttemptToken: 0,
            nowMilliseconds: 3_000);
        True(expired is null, "popup expires exactly at its bounded end");
    }

    private static PaladinGuardianCandidate Candidate(
        uint entityId,
        uint hp,
        uint maxHp,
        int? pressure = 0,
        float distance = 5f,
        int partySlot = 2) =>
        new(
            0x1000UL + entityId,
            entityId,
            partySlot,
            hp,
            maxHp,
            pressure,
            distance * distance,
            IsExactPartyMember: true,
            IsSelf: false,
            IsAlive: true,
            IsTargetable: true,
            HasValidNativeTarget: true,
            HasNativeRangeAndLineOfSight: true);

    private static AutoGuardProtectionObservation ProtectionObservation(
        TargetPressureActorIdentity local,
        bool exactGuardActive,
        bool actionCanCancelGuard,
        long now,
        bool explicitGuardReuse = false) =>
        new(
            RuntimeEnabled: true,
            TerritoryId: 250,
            LocalPlayer: local,
            LocalPlayerLive: true,
            ExactGuardActive: exactGuardActive,
            ActionCanCancelGuard: actionCanCancelGuard,
            IsExplicitGuardReuse: explicitGuardReuse,
            NowMilliseconds: now);

    private static AutoGuardConfirmationObservation ConfirmationObservation(
        TargetPressureActorIdentity local,
        bool exactGuardActive,
        AutoGuardRetryReadiness retryReadiness,
        long now) =>
        new(
            RuntimeEnabled: true,
            TerritoryId: 250,
            LocalPlayer: local,
            LocalPlayerLive: true,
            ExactGuardActive: exactGuardActive,
            RetryReadiness: retryReadiness,
            NowMilliseconds: now);

    private static void True(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException($"Expected true: {label}");
    }

    private static void False(bool condition, string label) => True(!condition, label);

    private static void Equal<T>(T expected, T actual, string label)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }
}
