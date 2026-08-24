using SeitonSense.Core;

var tests = new (string Name, Action Run)[]
{
    ("Ninja job gate is exact", NinjaJobGateIsExact),
    ("execute threshold is strictly below half", ExecuteThresholdIsStrict),
    ("invalid HP fails closed", InvalidHpFailsClosed),
    ("enemy slot labels are exact", EnemySlotLabelsAreExact),
    ("enemy validation requires hostility or complete CC fallback", EnemyValidationFailsClosed),
    ("native range result accepts facing-only failure", NativeRangeResultIsExact),
    ("held-action proven-false retries are throttled and bounded", HeldActionRetrySelfTests.ProvenFalseRetriesAreThrottledAndBounded),
    ("held-action retries require a proven client false", HeldActionRetrySelfTests.OnlyProvenFalseCanRetainTheFrozenIntent),
    ("native action false requires a stable ready boundary fingerprint", HeldActionRetrySelfTests.NativeFalseRequiresAStableReadyBoundaryFingerprint),
    ("accepted held action leaves later distinct episodes available", HeldActionRetrySelfTests.AcceptedEpisodeDoesNotLatchAContinuousHeldKey),
    ("frozen throttle and global waits retain only eligible priority", HeldActionRetrySelfTests.FrozenThrottleAndGlobalWaitRetainOnlyEligiblePriority),
    ("initial exact intents claim cast soft waits without spending attempts", HeldActionRetrySelfTests.InitialExactIntentClaimsCastSoftWaitWithoutSpendingBudget),
    ("held helper scheduler priority order is pinned", HeldCastCancellationSelfTests.CanonicalHelperPriorityOrderIsPinned),
    ("held cast cancel requests once per observed cast epoch", HeldCastCancellationSelfTests.ExactRequestIsOncePerObservedCastEpoch),
    ("held cast cancel can become eligible inside the same cast", HeldCastCancellationSelfTests.IntentMayBecomeEligibleInsideTheSameCast),
    ("held cast cancel requires consistent clear signals", HeldCastCancellationSelfTests.OnlyConsistentClearRearmsAndSignalDriftFailsClosed),
    ("held cast cancel central safety gates fail closed", HeldCastCancellationSelfTests.EveryCentralSafetyGateFailsClosed),
    ("held cast cancel request identity and lock boundary are exact", HeldCastCancellationSelfTests.RequestIdentityAndLockBoundaryAreExact),
    ("held cast cancel never retries after a terminal request", HeldCastCancellationSelfTests.TerminalRequestSurvivesLaterGateChanges),
    ("known CC territories are complete", KnownCcTerritoriesAreComplete),
    ("CC matching remains fail closed", CcMatchingIsFailClosed),
    ("supported PvP context keeps Wolves' Den opt-in", SupportedPvpContextIsExact),
    ("Wolves' Den opponent selection is strict", WolvesDenOpponentSelectionIsStrict),
    ("visibility ignores a short false sample", VisibilityHasFalseGrace),
    ("visibility hard reset is immediate", VisibilityHardResetIsImmediate),
    ("unknown Guard never claims cooldown", UnknownGuardFailsClosed),
    ("Guard action starts a 30 second cooldown", GuardActionTracksCooldown),
    ("Guard status infers a stable deadline", GuardStatusInfersDeadline),
    ("Guard status tracks a later activation", GuardStatusTracksLaterActivation),
    ("Guard revive resets the cooldown", GuardReviveResetsCooldown),
    ("low MP requires a stable trusted sample", LowMpRequiresStableTrustedSample),
    ("low MP uses exit hysteresis", LowMpUsesExitHysteresis),
    ("low MP trust loss cancels only a pending transition", LowMpTrustLossIsStable),
    ("low MP thresholds are exact", LowMpThresholdsAreExact),
    ("local MP warning first sample is a silent baseline", LocalMpWarningSelfTests.FirstTrustedSampleOnlyEstablishesBaseline),
    ("local MP warning edges are inclusive and one-shot", LocalMpWarningSelfTests.ThresholdEdgesAreInclusiveAndOneShot),
    ("local MP warning direct drop reports both edges", LocalMpWarningSelfTests.DirectDropReportsBothCrossedEdges),
    ("local MP warning thresholds rearm independently", LocalMpWarningSelfTests.ThresholdsRearmIndependentlyWithHysteresis),
    ("local MP warning invalid telemetry breaks continuity", LocalMpWarningSelfTests.InvalidTelemetryDeathAndResetAreSafe),
    ("resource aura thresholds and combined state are exact", ResourceAuraSelfTests.ExactThresholdsAndCombinedState),
    ("resource aura invalid telemetry fails closed", ResourceAuraSelfTests.InvalidAndUntrustedTelemetryFailsClosed),
    ("combat frame enemy rows stay fixed", CombatFrameRulesSelfTests.EnemyRowsStayFixedAndOrdered),
    ("combat frame identity ambiguity fails closed", CombatFrameRulesSelfTests.AmbiguousSlotsAndActorsFailClosed),
    ("combat frame dead and unknown rows stay fixed", CombatFrameRulesSelfTests.DeadAndUnknownRowsRemainStable),
    ("combat frame resource trust and MP pips are exact", CombatFrameRulesSelfTests.ResourceTrustAndPipsAreExact),
    ("combat frame presentation flags are sanitized", CombatFrameRulesSelfTests.SelfAndPresentationFlagsAreSanitized),
    ("combat frame freshness boundary is exact", CombatFrameRulesSelfTests.SnapshotFreshnessIsExact),
    ("combat frame interactions require a fresh real enemy", CombatFrameInteractionSelfTests.OnlyFreshRealAliveEnemyRowsCreateIntents),
    ("combat frame click identity stays frozen", CombatFrameInteractionSelfTests.PressAndReleaseRequireTheSameFrozenActor),
    ("combat frame target preflight fails closed", CombatFrameInteractionSelfTests.EveryFinalTargetGateFailsClosed),
    ("combat frame LB rendered bounds fail closed", CombatFrameLimitGaugeSelfTests.RenderedBoundsAreNormalizedFailClosed),
    ("combat frame LB remote values require complete proof", CombatFrameLimitGaugeSelfTests.RemoteGaugeRequiresCompleteLocalProof),
    ("combat frame LB duplicate samples do not calibrate", CombatFrameLimitGaugeSelfTests.DuplicateAndNearIdenticalPartialsDoNotCompleteProof),
    ("combat frame LB fingerprint drift invalidates", CombatFrameLimitGaugeSelfTests.FingerprintDriftInvalidatesCalibration),
    ("combat frame LB contradictory geometry invalidates", CombatFrameLimitGaugeSelfTests.ContradictoryGeometryInvalidatesCalibration),
    ("combat frame LB readings never claim invalid telemetry", CombatFrameLimitGaugeSelfTests.ReadingFactoriesNeverClaimInvalidTelemetry),
    ("auto focus mark eligibility is strict", AutoEnemyFocusMarkSelfTests.EligibilityIsStrict),
    ("auto focus mark ranking is deterministic", AutoEnemyFocusMarkSelfTests.RankingIsDeterministic),
    ("auto focus mark ownership checks are exact", AutoEnemyFocusMarkSelfTests.OwnershipChecksAreExact),
    ("auto low-MP focus canonical eligibility is strict", AutoLowMpFocusTargetSelfTests.CanonicalSetAndEligibilityAreStrict),
    ("auto low-MP focus ranking is deterministic", AutoLowMpFocusTargetSelfTests.RankingIsMpThenHpThenStableIdentity),
    ("auto low-MP focus uses an independent inclusive latch", AutoLowMpFocusTargetSelfTests.InclusiveThresholdUsesAnIndependentTrustedLatch),
    ("auto low-MP focus empty state and wave are bounded", AutoLowMpFocusTargetSelfTests.EmptyFocusMustBeStableAndWaveIsOneShot),
    ("auto low-MP focus never delays after manual occupancy", AutoLowMpFocusTargetSelfTests.OccupiedFocusSpendsWaveWithoutDelayedMutation),
    ("auto low-MP focus rearms only after wave separation", AutoLowMpFocusTargetSelfTests.ASeparatedWaveCanRearmWithoutRetryingFailure),
    ("auto low-MP focus intermediate MP cannot rearm", AutoLowMpFocusTargetSelfTests.IntermediateMpCannotRearmASpentWave),
    ("auto low-MP focus unknown MP cannot rearm", AutoLowMpFocusTargetSelfTests.UnknownMpCannotRearmASpentWave),
    ("auto low-MP focus drift latches until reset", AutoLowMpFocusTargetSelfTests.ConfirmedFocusDriftLatchesUntilExplicitReset),
    ("auto low-MP focus frozen intent is exact", AutoLowMpFocusTargetSelfTests.FrozenIntentRequiresEveryFinalGate),
    ("Guardian and Guard passes aggregate current-frame state only", DefensiveUtilitySelfTests.IndependentGuardianAndGuardPassesAggregateCurrentFrameOnly),
    ("defensive utility thresholds are exact", DefensiveUtilitySelfTests.ExactThresholdsAreInclusiveAndSafe),
    ("Smart Recuperate IDs and thresholds are exact", SmartRecuperateSelfTests.ExactIdsAndInclusiveThresholdsArePinned),
    ("Smart Recuperate waits for a real MP tick", SmartRecuperateSelfTests.MpTickWaitDoesNotConsumeTheHold),
    ("Smart Recuperate initial safety gates fail closed", SmartRecuperateSelfTests.EveryInitialSafetyGateFailsClosed),
    ("Smart Recuperate frozen intent is exact", SmartRecuperateSelfTests.FrozenIntentRequiresEveryTerminalGate),
    ("Smart Recuperate clean false retries are bounded", SmartRecuperateSelfTests.CleanFalseRetriesAreBounded),
    ("Smart Recuperate repeats only after an accepted cooldown epoch", SmartRecuperateSelfTests.SoftUnavailableIsFreeAndAcceptedCooldownDefinesRepeat),
    ("Smart Recuperate never starves Purify", SmartRecuperateSelfTests.PurifyPriorityNeverGetsStarved),
    ("VPR Serpent's Tail identities and ranges are exact", ViperSerpentTailSelfTests.ExactCarrierFollowupsMappingsAndRangesArePinned),
    ("VPR Serpent's Tail accepted trigger is exact", ViperSerpentTailSelfTests.AcceptedTriggerIsExactAndBounded),
    ("VPR Serpent's Tail initial gates fail closed", ViperSerpentTailSelfTests.InitialSafetyGatesAndPriorityFailClosed),
    ("VPR Serpent's Tail freezes one exact intent", ViperSerpentTailSelfTests.ExactIntentFreezesActionTargetContextAndKey),
    ("VPR Serpent's Tail retries only clean false", ViperSerpentTailSelfTests.KnownWaitsAreFreeAndCleanFalseRetriesAreBounded),
    ("VPR Serpent's Tail continuous hold needs distinct triggers", ViperSerpentTailSelfTests.ContinuousHoldUsesDistinctAcceptedFollowupTriggersOnly),
    ("pressure escape direct threshold is exact", PressureEscapeSelfTests.DirectThresholdIsInclusiveAndUnknownFailsClosed),
    ("pressure escape warning entry and clear are bounded", PressureEscapeSelfTests.WarningEntryIsImmediateAndClearIsDebounced),
    ("pressure escape unknown state fails closed", PressureEscapeSelfTests.UnknownOrStalePressureClearsImmediately),
    ("pressure escape Sprint requires every exact gate", PressureEscapeSelfTests.SprintRequiresEveryExactGateAndMovementKey),
    ("pressure escape movement keys are narrow", PressureEscapeSelfTests.MovementKeySetIsNarrow),
    ("pressure escape episode token remains nonzero", PressureEscapeSelfTests.WarningEpisodeTokenWrapsToANonZeroValue),
    ("DRK Shadowbringer IDs and carriers are exact", DarkKnightShadowbringerMacroSelfTests.ExactIdsAndCarrierSetArePinned),
    ("DRK Shadowbringer cycle requires a proven reset", DarkKnightShadowbringerMacroSelfTests.CycleRequiresAProvenExactReset),
    ("DRK Shadowbringer unknown state preserves spent ownership", DarkKnightShadowbringerMacroSelfTests.UnknownPreservesSpentOwnershipAndNextResetRearms),
    ("DRK Shadowbringer cycle token remains nonzero", DarkKnightShadowbringerMacroSelfTests.CycleTokenWrapsToOne),
    ("DRK Shadowbringer macro pairing is exact", DarkKnightShadowbringerMacroSelfTests.PairingRequiresImmediateQueueableSouleaterLine),
    ("DRK Shadowbringer no-clip window is exact", DarkKnightShadowbringerMacroSelfTests.NoClipWindowIsInclusiveAndNeverLate),
    ("DRK Shadowbringer HP and Dark Arts gates are exact", DarkKnightShadowbringerMacroSelfTests.HpAndDarkArtsGateIsExact),
    ("DRK Shadowbringer attempt requires every safety gate", DarkKnightShadowbringerMacroSelfTests.AttemptRequiresEverySafetyGate),
    ("DRK Plunge identity threshold and range are exact", DarkKnightPlungeSelfTests.ExactIdentityThresholdAndRangeArePinned),
    ("DRK Plunge ranking and ambiguity are deterministic", DarkKnightPlungeSelfTests.CandidateRankingAndAmbiguityAreDeterministic),
    ("DRK Plunge continuous hold requires a cooldown epoch", DarkKnightPlungeSelfTests.ContinuousHoldRequiresAProvenCooldownEpoch),
    ("DRK Plunge initial and repeat ownership are distinct", DarkKnightPlungeSelfTests.InitialAndRepeatDispatchUseDistinctOwnership),
    ("DRK Plunge frozen intent requires every terminal gate", DarkKnightPlungeSelfTests.FrozenIntentRequiresEveryTerminalGate),
    ("post-Purify Guard requires positive confirmation", DefensiveUtilitySelfTests.PostPurifyGuardRequiresPositiveConfirmation),
    ("Guard propagation latch is bounded and non-rearming", DefensiveUtilitySelfTests.GuardPropagationLatchIsBoundedAndNonRearming),
    ("Guard rejection rollback is exact and synchronous", DefensiveUtilitySelfTests.GuardRejectionRollbackIsExactAndSynchronous),
    ("Auto-Guard protection ownership requires the exact accepted attempt", DefensiveUtilitySelfTests.AutoGuardProtectionOwnershipRequiresTheExactAcceptedAttempt),
    ("Auto-Guard protection bridges propagation and follows exact Guard", DefensiveUtilitySelfTests.AutoGuardProtectionBridgesPropagationAndFollowsTheExactStatus),
    ("Auto-Guard protection has explicit and bounded release paths", DefensiveUtilitySelfTests.AutoGuardProtectionHasExplicitAndBoundedReleasePaths),
    ("Auto-Guard protection context drift always fails open", DefensiveUtilitySelfTests.AutoGuardProtectionContextDriftAlwaysFailsOpen),
    ("Guardian eligibility uses native reachability", DefensiveUtilitySelfTests.GuardianEligibilityUsesNativeReachability),
    ("Guardian proactive risk requires exact high pressure", DefensiveUtilitySelfTests.GuardianProactiveRiskRequiresExactHighPressure),
    ("Guardian pressure publication freshness is bounded", DefensiveUtilitySelfTests.GuardianPressurePublicationFreshnessIsBounded),
    ("Guardian ranking is deterministic", DefensiveUtilitySelfTests.GuardianRankingIsDeterministic),
    ("Guardian trigger popup is accepted only and bounded", DefensiveUtilitySelfTests.GuardianTriggerPopupIsAcceptedOnlyAndBounded),
    ("Guardian communication quick chat is one-shot", GuardianTeamCommunicationSelfTests.AcceptedEpisodeQuickChatIsOneShot),
    ("Guardian communication initial failures consume", GuardianTeamCommunicationSelfTests.InitialFailuresConsumeWithoutCommands),
    ("Guardian communication occupied markers stay chat-only", GuardianTeamCommunicationSelfTests.OccupiedOrUnknownMarkersStayQuickChatOnly),
    ("Guardian communication accepts the native empty marker sentinel", GuardianTeamCommunicationSelfTests.InvalidNativeMarkerSentinelIsExactlyEmpty),
    ("Guardian communication marker pair is sequential", GuardianTeamCommunicationSelfTests.MarkerPairIsSequentialAndExactlyConfirmed),
    ("Guardian communication set confirmation is exact", GuardianTeamCommunicationSelfTests.SetConfirmationRequiresActorAndChangedTime),
    ("Guardian communication partial failure cleans exact ownership", GuardianTeamCommunicationSelfTests.PartialBind1FailureCleansOnlyOwnedBind2),
    ("Guardian communication deadline cleanup is ordered", GuardianTeamCommunicationSelfTests.DeadlineCleanupIsBind2ThenBind1),
    ("Guardian communication external drift is per marker", GuardianTeamCommunicationSelfTests.ExternalDriftCleansOnlyRemainingOwnership),
    ("Guardian communication reset cleanup is ownership-safe", GuardianTeamCommunicationSelfTests.ResetAndContextLossOnlyUseSafeCleanup),
    ("Guardian communication pending confirmation survives safe cleanup gates", GuardianTeamCommunicationSelfTests.PendingConfirmationSurvivesConfigAndTextUntilCleanupIsSafe),
    ("Guardian communication repeats only pre-invocation deferrals", GuardianTeamCommunicationSelfTests.DeferredBeforeInvocationIsTheOnlyRepeatableDecision),
    ("Guardian communication consumes newer busy episodes", GuardianTeamCommunicationSelfTests.NewEpisodeWhileBusyIsConsumedWithoutReplacement),
    ("Scholar Critical Strategy eligibility is Guard-only", ScholarCriticalStrategySelfTests.CandidateEligibilityRequiresLiveGuardAndNativeReachability),
    ("Scholar Critical Strategy canonical set is complete", ScholarCriticalStrategySelfTests.CompleteCanonicalSetIsExactAndUnique),
    ("Scholar Critical Strategy ranks trusted pressure first", ScholarCriticalStrategySelfTests.TrustedPositivePressureRanksBeforeExactHp),
    ("Scholar Critical Strategy falls back to exact HP", ScholarCriticalStrategySelfTests.UnknownOrAllZeroPressureFallsBackToHp),
    ("Scholar Critical Strategy requires one held generation", ScholarCriticalStrategySelfTests.DispatchRequiresEveryGateAndHeldGeneration),
    ("Scholar Critical Strategy freezes one exact intent", ScholarCriticalStrategySelfTests.DispatchFreezesOneIntentWithoutPressureRevalidation),
    ("Scholar Critical Strategy cannot retry a consumed hold", ScholarCriticalStrategySelfTests.ConsumedHeldGenerationCannotRetry),
    ("Scholar Critical Strategy repeats only on accepted cooldown epochs", ScholarCriticalStrategySelfTests.AcceptedHoldRepeatsOnlyAfterCooldownEpoch),
    ("Smart Paean eligibility requires exact pressure", SmartWardensPaeanTargetSelfTests.EligibilityRequiresKnownPressureAndNativeReachability),
    ("Smart Paean party view rejects identity ambiguity", SmartWardensPaeanTargetSelfTests.CompletePartyViewRejectsIdentityAmbiguity),
    ("Smart Paean ranking is deterministic", SmartWardensPaeanTargetSelfTests.RankingIsPressureThenExactHpThenStableSlot),
    ("Smart Paean preserves vanilla on unknown pressure", SmartWardensPaeanTargetSelfTests.UnknownOrMissingPressurePreservesVanillaCall),
    ("Smart Paean freezes one exact target", SmartWardensPaeanTargetSelfTests.FrozenIntentCannotRerankFallbackOrRetry),
    ("Smart Kardia IDs and eligibility are exact", SmartKardiaSelfTests.ExactIdsAndCandidateEligibilityArePinned),
    ("Smart Kardia party view rejects ambiguity", SmartKardiaSelfTests.CompletePartyViewRejectsIdentityAmbiguity),
    ("Smart Kardia partial live pressure fails closed", SmartKardiaSelfTests.PartialLivePressureViewFailsClosed),
    ("Smart Kardia ranking is deterministic", SmartKardiaSelfTests.RankingIsPressureThenExactHpThenStableSlot),
    ("Smart Kardia status never selects an alternate", SmartKardiaSelfTests.BestKardionStateNeverFallsThroughToAnAlternate),
    ("Smart Kardia defaults exactly to self", SmartKardiaSelfTests.DefaultSelfFallbackIsExactAndTerminal),
    ("Smart Kardia accepted trigger is bounded and exact", SmartKardiaSelfTests.AcceptedTriggerIsBoundedAndIdentityExact),
    ("Smart Kardia causal Eukrasia evidence is exact", SmartKardiaSelfTests.CausalEvidenceRequiresChargeOrOwnedStatusTransition),
    ("Smart Kardia requires every event and safety gate", SmartKardiaSelfTests.DispatchRequiresEveryEventAndSafetyGate),
    ("Smart Kardia freezes one exact intent", SmartKardiaSelfTests.FrozenIntentCannotRerankFallbackOrRetry),
    ("isolation warning requires continuous isolation", IsolationWarningSelfTests.ContinuousIsolationUsesEntryDelay),
    ("isolation warning clears only after stable connection", IsolationWarningSelfTests.StableConnectionUsesClearDelay),
    ("isolation warning ignores dead allies", IsolationWarningSelfTests.DeadAlliesDoNotProvideConnection),
    ("isolation warning accepts one reachable ally", IsolationWarningSelfTests.AnyReachableAllyPreventsIsolation),
    ("isolation warning unknown data fails closed", IsolationWarningSelfTests.UnknownAndIncompleteDataFailClosed),
    ("isolation warning jitter and clock resets are safe", IsolationWarningSelfTests.JitterAndClockResetAreSafe),
    ("Seiton popup is one shot and rearms", StablePopupIsOneShot),
    ("Seiton range jitter cannot rearm", SeitonRangeJitterCannotRearm),
    ("Seiton popup rearm must remain stable", SeitonPopupRearmMustRemainStable),
    ("persistent Seiton cue enters once and remains visible", PersistentSeitonCueEntersOnce),
    ("persistent Seiton cue ignores range jitter", PersistentSeitonCueIgnoresRangeJitter),
    ("persistent Seiton cue rearms only from semantic recovery", PersistentSeitonCueRearmsSemantically),
    ("Seiton preparation band is optional and exact", SeitonPreparationBandIsExact),
    ("NIN Seiton execute-blocking protection IDs are exact", NinjaSeitonDispatchSelfTests.ExecuteBlockingProtectionStatusSetIsExact),
    ("NIN Seiton dispatch candidate gates are exact", NinjaSeitonDispatchSelfTests.CandidateEligibilityIsExactAndStrict),
    ("NIN Seiton dispatch ranks by exact HP then S-slot", NinjaSeitonDispatchSelfTests.LowestExactHealthWinsThenStableSlot),
    ("NIN Seiton skips protected targets and cancels protected frozen intents", NinjaSeitonDispatchSelfTests.ProtectedTargetsAreSkippedAndFrozenProtectionDriftCancels),
    ("NIN Seiton dispatch rejects ambiguous canonical candidates", NinjaSeitonDispatchSelfTests.AmbiguousCanonicalCandidatesFailClosed),
    ("NIN Seiton dispatch requires every gate and held input", NinjaSeitonDispatchSelfTests.DispatchRequiresEveryGateAndHeldInput),
    ("NIN Seiton dispatch freezes one exact intent", NinjaSeitonDispatchSelfTests.DispatchFreezesOneExactIntent),
    ("NIN Seiton held level uses distinct accepted adjusted epochs", NinjaSeitonDispatchSelfTests.HeldLevelUsesOneAcceptedAdjustedActionEpochAtATime),
    ("NIN Guard-Shukuchi constants and strict threshold are exact", NinjaGuardShukuchiSelfTests.ConstantsAndStrictThresholdAreExact),
    ("NIN Guard-Shukuchi native range and position are exact", NinjaGuardShukuchiSelfTests.NativeRangeAndPositionAreExact),
    ("NIN Guard-Shukuchi candidate requires every gate", NinjaGuardShukuchiSelfTests.CandidateRequiresEveryGuardLowHpGate),
    ("NIN Guard-Shukuchi pressure is only a bonus", NinjaGuardShukuchiSelfTests.PositivePressureIsOnlyARankingBonus),
    ("NIN Guard-Shukuchi partial slots stay unambiguous", NinjaGuardShukuchiSelfTests.PartialSlotsWorkButAmbiguityFailsClosed),
    ("NIN Guard-Shukuchi dispatch requires every gate", NinjaGuardShukuchiSelfTests.DispatchRequiresEveryStaticAndInputGate),
    ("NIN Guard-Shukuchi freezes one exact actor", NinjaGuardShukuchiSelfTests.FrozenIntentCannotRerankOrDrift),
    ("NIN Guard-Shukuchi cast cancellation and retry stay exact", NinjaGuardShukuchiSelfTests.CastCancellationAndRetryKeepExactIntent),
    ("NIN Guard-Shukuchi continuous hold requires cooldown rearm", NinjaGuardShukuchiSelfTests.ContinuousHoldRequiresProvenCooldownRearm),
    ("Panic Shukuchi constants and forward axes are exact", PanicShukuchiSelfTests.ConstantsAndForwardAxesAreExact),
    ("Panic Shukuchi contexts keep Wolves' Den opt-in", PanicShukuchiSelfTests.SupportedContextsAreExact),
    ("Panic Shukuchi ground hit is exact forward and in range", PanicShukuchiSelfTests.GroundHitMustBeExactForwardFiniteAndInRange),
    ("Panic Shukuchi valid command produces one immediate intent", PanicShukuchiSelfTests.ValidCommandProducesOneImmediateIntent),
    ("Panic Shukuchi repeated commands are independent", PanicShukuchiSelfTests.RepeatedCommandsAreIndependent),
    ("Panic Shukuchi policy has no Guard or scheduler inputs", PanicShukuchiSelfTests.CommandPolicyHasNoGuardOrSchedulerInputs),
    ("Panic Shukuchi static command gates fail closed", PanicShukuchiSelfTests.StaticCommandGatesFailClosed),
    ("Panic Shukuchi invalid action or terrain exposes no fallback", PanicShukuchiSelfTests.InvalidActionOrTerrainExposesNoFallback),
    ("personal debuff alerts deduplicate and order by urgency", PersonalDebuffsDeduplicateAndOrder),
    ("personal debuff refresh does not repulse", PersonalDebuffRefreshDoesNotRepulse),
    ("personal debuff missing grace prevents flicker", PersonalDebuffMissingGracePreventsFlicker),
    ("personal debuff escalation pulses once", PersonalDebuffEscalationPulsesOnce),
    ("personal debuff lifecycle fails closed", PersonalDebuffLifecycleFailsClosed),
    ("physical key priming and release define generations", PhysicalGameplayKeySelfTests.PrimingAndReleaseDefineGenerations),
    ("physical key consumption survives until release", PhysicalGameplayKeySelfTests.ConsumptionSurvivesUntilRelease),
    ("text input cannot become a held gameplay trigger", PhysicalGameplayKeySelfTests.TextInputPoisonsOnlyTheCurrentHold),
    ("physical key hard reset requires release", PhysicalGameplayKeySelfTests.HardResetRequiresAnotherRelease),
    ("stable held key wins over coincident fresh tap", PhysicalGameplayKeySelfTests.StableHoldWinsOverCoincidentFreshTap),
    ("stable held selection survives a multi-frame action tap", PhysicalGameplayKeySelfTests.StableSelectionSurvivesMultiFrameActionTap),
    ("one physical hold can authorize distinct Purify status generations", PhysicalGameplayKeySelfTests.OneHoldCanCrossDistinctPurifyStatusGenerations),
    ("Guard suppression preserves the observed physical hold", PhysicalGameplayKeySelfTests.GuardSuppressionPreservesObservedHold),
    ("Purify accepts a same-frame fresh key", EmergencyPurifyBufferSelfTests.SameFrameFreshKeyCanDispatch),
    ("Purify held-key entry is explicit and one shot", EmergencyPurifyBufferSelfTests.HeldKeyAtStatusEntryIsExplicitAndOneShot),
    ("Purify held-key level only counts at status entry", EmergencyPurifyBufferSelfTests.HeldKeyOnlyCountsAtStatusEntry),
    ("Purify stable hold wins over coincident fresh input", EmergencyPurifyBufferSelfTests.StableHoldWinsWhenFreshAndHeldCoincide),
    ("Purify held intent claims each active framework frame", EmergencyPurifyBufferSelfTests.HeldKeyIsConsumedWhenItOnlyArms),
    ("Purify clean false retries at the shared cadence", EmergencyPurifyBufferSelfTests.DispatchConsumesBeforeAttempt),
    ("ready Purify dispatches once at the key edge", EmergencyPurifyBufferSelfTests.ReadyAtArmDispatchesExactlyOnce),
    ("Purify status and key lease survives a long structural wait", EmergencyPurifyBufferSelfTests.TimeoutWithoutAttemptCanRearm),
    ("Purify rearms only after status absence", EmergencyPurifyBufferSelfTests.StatusAbsenceIsTheOnlyRearmForSameInstance),
    ("Purify tracks the exact status instance", EmergencyPurifyBufferSelfTests.ExactStatusReplacementNeedsANewKey),
    ("Purify temporary gates do not spend an attempt", EmergencyPurifyBufferSelfTests.TemporarySafetyGatesDoNotSpendAnAttempt),
    ("Purify hard reset and invalid inputs fail closed", EmergencyPurifyBufferSelfTests.HardResetAndInvalidInputsFailClosed),
    ("Purify native outcomes use the shared retry policy", EmergencyPurifyBufferSelfTests.NativeOutcomesUseSharedRetryPolicy),
    ("target and focus on the same actor are combined", TargetHighlightRulesSelfTests.SameObjectIsCombined),
    ("different current and focus targets stay ordered", TargetHighlightRulesSelfTests.DifferentObjectsRemainOrdered),
    ("target PvP-only gating is per source", TargetHighlightRulesSelfTests.PvpGateIsPerSource),
    ("invalid target identities fail closed", TargetHighlightRulesSelfTests.InvalidIdentitiesFailClosed),
    ("target HP formatting is safe", TargetHighlightRulesSelfTests.HpFormattingIsSafe),
    ("target distance formatting is safe", TargetHighlightRulesSelfTests.DistanceFormattingIsSafe),
    ("target S-slot formatting is exact", TargetHighlightRulesSelfTests.EnemySlotFormattingIsExact),
    ("combined target info uses safe same-identity fallbacks", TargetHighlightRulesSelfTests.CombinedPlanUsesOnlySafeFallbacks),
    ("Near Assist rewrites one eligible macro action", NearAssistOneShotSelfTests.ValidAttemptRewritesExactlyOnce),
    ("Near Assist timeout fails closed at its boundary", NearAssistOneShotSelfTests.TimeoutFailsClosedAtBoundary),
    ("Near Assist rejects slot and identity drift", NearAssistOneShotSelfTests.EnemySlotAndIdentityDriftFailClosed),
    ("Near Assist range and line-of-sight failures consume", NearAssistOneShotSelfTests.RangeAndLineOfSightFailureConsumes),
    ("Near Assist rejects unsafe action shapes and modes", NearAssistOneShotSelfTests.ActionShapeAndModeFailuresConsume),
    ("Near Assist ignores unrelated non-macro calls", NearAssistOneShotSelfTests.NonMacroCallsDoNotStealTheToken),
    ("Near Assist works without a stable own target", NearAssistOneShotSelfTests.OwnTargetDriftPreservesTheActualCallTarget),
    ("Near Assist missing candidate arms a fallback guard", NearAssistOneShotSelfTests.MissingCandidateArmsOneFallbackGuard),
    ("Near Assist carrier identity ignores macro-line timing", NearAssistOneShotSelfTests.CarrierIdentityDoesNotDependOnMacroLineTiming),
    ("Near Assist replacement keeps only the newest token", NearAssistOneShotSelfTests.ReplacementUsesOnlyTheNewestToken),
    ("Near Assist invalid state and resets fail closed", NearAssistOneShotSelfTests.InvalidStateAndResetsPreserveOriginalBits),
    ("Near Assist nearest mode stays predictable", NearAssistSelectionSelfTests.NearestModeIsPredictable),
    ("Near Assist smart mode prefers nearby damage roles", NearAssistSelectionSelfTests.SmartModePrefersDamageInsideTheNearbyCluster),
    ("Near Assist smart mode cannot pull across the arena", NearAssistSelectionSelfTests.SmartModeCannotPullAcrossTheArena),
    ("Near Assist same-role tie breaks are stable", NearAssistSelectionSelfTests.SameRoleUsesDistanceThenStableEntityId),
    ("Near Assist invalid selection candidates fail closed", NearAssistSelectionSelfTests.InvalidCandidatesFailClosed),
    ("Near Assist current PvP damage jobs are classified", NearAssistSelectionSelfTests.CurrentPlayableDamageJobsAreClassifiedExactly),
    ("Smart Target reach tiers precede combat signals", SmartTargetSelectionSelfTests.ReachTierWinsBeforeEveryCombatSignal),
    ("Smart Target ranking order is exact", SmartTargetSelectionSelfTests.RankingOrderIsExactAndDeterministic),
    ("Smart Target eligibility and ambiguity fail closed", SmartTargetSelectionSelfTests.EligibilityAndAmbiguityFailClosed),
    ("Smart Target freezes one action and actor", SmartTargetSelectionSelfTests.FrozenIntentNeverReranksOrChangesAction),
    ("Smart Tab reach precedes combat signals", SmartTabSelectionSelfTests.ReachTierPrecedesEveryCombatSignal),
    ("Smart Tab ranking order is exact", SmartTabSelectionSelfTests.RankingOrderIsExactAndDeterministic),
    ("Smart Tab eligibility and ambiguity fail closed", SmartTabSelectionSelfTests.EligibilityAndAmbiguityFailClosed),
    ("Smart Tab freezes one exact actor", SmartTabSelectionSelfTests.FrozenIntentNeverReranksOrChangesActor),
    ("Smart Tab owns exact native forward target", SmartTabInterceptionSelfTests.ExactNativeForwardTargetIsConsumed),
    ("Smart Tab toggle and context gates preserve vanilla", SmartTabInterceptionSelfTests.ToggleOffAndUnsupportedContextsStayVanilla),
    ("Smart Tab other native paths preserve vanilla", SmartTabInterceptionSelfTests.OtherNativePathsStayVanilla),
    ("Smart Target reviewed melee gap caps are exact", SmartTargetReachSelfTests.ReviewedMeleeJobsAndGapCapsAreExact),
    ("Smart Target hitbox edge reach is exact", SmartTargetReachSelfTests.HitboxEdgeBoundariesProduceOnlyMeleeOrGapTiers),
    ("Smart Target invalid geometry fails closed", SmartTargetReachSelfTests.UnknownJobsAndInvalidGeometryFailClosed),
    ("Near Help lowest exact health ratio wins before distance", NearHelpSelectionSelfTests.LowestExactHealthRatioWinsBeforeDistance),
    ("Near Help health comparison is exact and overflow safe", NearHelpSelectionSelfTests.HealthRatioComparisonIsExactAndOverflowSafe),
    ("Near Help ties use distance and stable identity", NearHelpSelectionSelfTests.EqualHealthUsesDistanceThenStableIdentity),
    ("Near Help self requires action-specific targetability", NearHelpSelectionSelfTests.SelfRequiresActionSpecificTargetability),
    ("Near Help critical health remains HP-first", NearHelpSelectionSelfTests.CriticalHealthAnchorAlwaysWins),
    ("Near Help pressure window is exact and overflow safe", NearHelpSelectionSelfTests.PressureWindowBoundaryIsExactAndOverflowSafe),
    ("Near Help pressure ranking is deterministic", NearHelpSelectionSelfTests.PressureUsesCountThenExistingStableOrder),
    ("Near Help unknown pressure preserves HP-first", NearHelpSelectionSelfTests.UnknownOrUntrustedPressureFallsBackExactly),
    ("Near Help reachability and friendly identity fail closed", NearHelpSelectionSelfTests.ReachabilityAndFriendlyIdentityFailClosed),
    ("Near Help rewrites one friendly macro action", NearHelpOneShotSelfTests.ValidAttemptSelectsAtActionTimeAndRewritesOnce),
    ("Near Help pressure remains action-time and one-shot", NearHelpOneShotSelfTests.PressureSelectionRemainsActionTimeAndOneShot),
    ("Near Help missing candidate uses exact carrier fallback", NearHelpOneShotSelfTests.MissingCandidateUsesExactCarrierFallbackPolicy),
    ("Near Help carrier identity preserves compact target", NearHelpOneShotSelfTests.CarrierIdentityDistinguishesAuthoredSlotFromOwnTarget),
    ("Near Help rejects unsafe action shapes without drift", NearHelpOneShotSelfTests.ActionShapeFailuresConsumeWithoutDrift),
    ("Near Help ignores unrelated actions and expires safely", NearHelpOneShotSelfTests.NonMacroActionWaitsAndTimeoutFailsClosed),
    ("Near Help invalid state and resets fail closed", NearHelpOneShotSelfTests.InvalidArmsAndHardResetFailClosed),
    ("Far Help distance always wins before role", FarHelpSelectionSelfTests.DistanceAlwaysWinsBeforeRole),
    ("Far Help selects the farthest actor across all roles", FarHelpSelectionSelfTests.FarthestWinsAcrossAllRoles),
    ("Far Help exact-distance ties use healer then ranged priority", FarHelpSelectionSelfTests.EqualDistanceUsesExactRoleOrder),
    ("Far Help equal-distance ties use stable identity", FarHelpSelectionSelfTests.EqualDistanceUsesStablePartyAndActorIdentity),
    ("Far Help exact party reachability fails closed", FarHelpSelectionSelfTests.ExactPartyReachabilityAndLivenessFailClosed),
    ("Far Help prefers safe backline without requiring it", FarHelpSelectionSelfTests.BacklineSafetyIsPreferredButNeverRequired),
    ("Far Help current PvP jobs use exact role tiers", FarHelpSelectionSelfTests.CurrentPvpJobsUseExactRoleTiers),
    ("Far Help rewrites one friendly macro action", FarHelpOneShotSelfTests.ValidAttemptSelectsAtActionTimeAndRewritesOnce),
    ("Far Help missing candidate never falls back to own target", FarHelpOneShotSelfTests.MissingCandidateNeverFallsBackToOwnTarget),
    ("Far Help carrier identity preserves compact target", FarHelpOneShotSelfTests.CarrierIdentityDistinguishesAuthoredSlotFromOwnTarget),
    ("Far Help rejects non-movement and unsafe action shapes", FarHelpOneShotSelfTests.ActionShapeFailuresConsumeWithoutDrift),
    ("Far Help ignores unrelated actions and expires safely", FarHelpOneShotSelfTests.NonMacroActionWaitsAndTimeoutFailsClosed),
    ("Far Help invalid state and resets fail closed", FarHelpOneShotSelfTests.InvalidArmsAndHardResetFailClosed),
    ("Far Help obsolete fallback is suppressed through quarantine", FarHelpFallbackSuppressionSelfTests.ExactFollowingActionIsSuppressedThroughQuarantine),
    ("Far Help fallback suppression ignores unrelated actions", FarHelpFallbackSuppressionSelfTests.UnrelatedActionsCannotConsumeSuppression),
    ("Far Help fallback suppression expires and fails closed", FarHelpFallbackSuppressionSelfTests.InvalidClockAndExpiryClearWithoutSuppressing),
    ("Ally Rescue trigger status allowlist is exact", AllyRescueSelectionSelfTests.TriggerStatusAllowlistIsExact),
    ("Ally Rescue health ranking uses exact ratios", AllyRescueSelectionSelfTests.ExactHealthRatioWinsBeforeEveryOtherSignal),
    ("Ally Rescue pressure is unique and unknown-last", AllyRescueSelectionSelfTests.PressureIsUniqueDescendingAndUnknownLast),
    ("Ally Rescue trusted MP ranking is exact", AllyRescueSelectionSelfTests.TrustedMpRatioIsExactAndUnknownLast),
    ("Ally Rescue selection ties are deterministic", AllyRescueSelectionSelfTests.DistanceAndStableIdentityBreakFullTies),
    ("Ally Rescue selection eligibility fails closed", AllyRescueSelectionSelfTests.EligibilityFailsClosedAndSpentIntentIsExcluded),
    ("Ally Rescue native false retries only exact lease", AllyRescueBufferSelfTests.NativeFalseRetriesOnlyTheExactLeaseUntilAccepted),
    ("Ally Rescue retries are bounded and exceptions terminal", AllyRescueBufferSelfTests.NativeRetriesAreBoundedAndExceptionsAreTerminal),
    ("Ally Rescue lease is status and held-key bound", AllyRescueBufferSelfTests.StatusBoundLeaseSurvivesLongSoftWaits),
    ("Ally Rescue held input only counts at candidate entry", AllyRescueBufferSelfTests.HeldInputOnlyCountsAtCandidateAppearanceOrReplacement),
    ("Ally Rescue stable hold wins over coincident fresh tap", AllyRescueBufferSelfTests.StableHeldEntryWinsOverCoincidentFreshTap),
    ("Ally Rescue continuous hold accepts distinct intents", AllyRescueBufferSelfTests.ContinuousHoldCanAuthorizeLaterDistinctIntents),
    ("Ally Rescue buffered target cannot drift", AllyRescueBufferSelfTests.CandidateChangesCancelBufferedIntentWithoutTargetDrift),
    ("Ally Rescue resolves ranking before input ownership", AllyRescueBufferSelfTests.RankingIsResolvedBeforeTheInputIsOwned),
    ("Ally Rescue safety gates fail closed", AllyRescueBufferSelfTests.SafetyGatesAndHardResetFailClosed),
    ("Self Purify owns shared input before Ally Rescue", AllyRescueBufferSelfTests.SelfPurifyOwnsTheSharedInputBeforeAllyRescue),
    ("Ally Rescue local attempt never confirms alone", AllyRescueConfirmationSelfTests.AttemptRegistrationNeverConfirmsLocally),
    ("Ally Rescue exact recovered effect confirms once", AllyRescueConfirmationSelfTests.ExactRecoveredEffectConfirmsOnce),
    ("Ally Rescue confirmation identity is exact", AllyRescueConfirmationSelfTests.ExactIdentityAndEffectShapeAreRequired),
    ("Ally Rescue confirmation deduplicates sequences", AllyRescueConfirmationSelfTests.DuplicateSequenceCannotDoubleCount),
    ("Ally Rescue confirmation counts all six actual cleanses", AllyRescueConfirmationSelfTests.AllSixRemovedStatusesAreCountedButOnlyFourTrigger),
    ("Ally Rescue confirmation expiry preserves statistics", AllyRescueConfirmationSelfTests.PopupAndPendingExpireWithoutChangingSessionCounts),
    ("Ally Rescue confirmation reset and invalid state fail closed", AllyRescueConfirmationSelfTests.HardResetAndInvalidStateFailClosed),
    ("MCH LB exact early target marker is accepted", MachinistLimitBreakMarkerSelfTests.ExactMarkerIsAccepted),
    ("MCH LB damage and ambiguous packets fail closed", MachinistLimitBreakMarkerSelfTests.DamageAndAmbiguousPacketsFailClosed),
    ("PvP LB catalog is complete and current", CombatLimitBreakSelfTests.CatalogIsCompleteCurrentAndUnique),
    ("PvP LB damage decoding is exact", CombatLimitBreakSelfTests.DamageDecoderIsExactAndFailClosed),
    ("PvP LB duration evidence is exact", CombatLimitBreakSelfTests.DurationEvidenceRequiresExactCarrierAndSource),
    ("PvP LB nameplate display requires an exact fresh enemy", CombatLimitBreakNameplateSelfTests.DisplayRequiresFreshExactEnemyIdentity),
    ("PvP LB nameplate countdown and flash are exact", CombatLimitBreakNameplateSelfTests.CountdownRequiresConfirmedDurationAndFlashIsBounded),
    ("PvP LB and CC nameplate emblems stack deterministically", CombatLimitBreakNameplateSelfTests.VerticalStackIsDeterministicAndNeverOverlaps),
    ("PvP LB self notification requires exact fresh evidence", CombatLimitBreakNotificationSelfTests.SelfBannerRequiresExactFreshEvidence),
    ("PvP LB ally damage notifications require exact events", CombatLimitBreakNotificationSelfTests.AllyDamageCardsRequireExactBoundedEvents),
    ("PvP LB notifications stay inside safe screen lanes", CombatLimitBreakNotificationSelfTests.NotificationLayoutStaysInsideSafeScreenLanes),
    ("CC protection allowlist metadata is exact", CcProtectionRulesSelfTests.AllowlistMetadataIsExact),
    ("unknown protection statuses and Aquaveil fail closed", CcProtectionRulesSelfTests.UnknownStatusesAndAquaveilFailClosed),
    ("CC protection requires finite positive bounded time", CcProtectionRulesSelfTests.InvalidDurationsAreIgnored),
    ("duplicate CC protections keep the longest duration", CcProtectionRulesSelfTests.DuplicateStatusesKeepLongestDuration),
    ("CC protections use stable category order", CcProtectionRulesSelfTests.IndicatorsUseStableProtectionPriority),
    ("CC protection countdown is conservative", CcProtectionRulesSelfTests.CountdownFormattingIsConservative),
    ("CC immunity brake action catalog is exact", CcImmunityBrakeSelfTests.ActionCatalogIsExactAndConservative),
    ("CC immunity brake toggle and identity gates pass safely", CcImmunityBrakeSelfTests.ToggleAndIdentityGatesPassWithoutMutation),
    ("CC immunity brake target matching is exact", CcImmunityBrakeSelfTests.TargetMustBeExactValidAndMatchIncomingCall),
    ("CC immunity brake resolves only native default targets", CcImmunityBrakeSelfTests.DefaultTargetCarrierResolvesOnlyTheNativeHardTarget),
    ("CC immunity brake standard blocker matrix is exact", CcImmunityBrakeSelfTests.StandardBlockerMatrixIsExact),
    ("CC immunity brake Miracle blocker matrix is exact", CcImmunityBrakeSelfTests.MiracleBlockerMatrixIsExact),
    ("CC immunity brake keeps exact internal Miracle eligible", CcImmunityBrakeSelfTests.ExactMiracleUsesTheSharedFinalDecision),
    ("CC immunity brake is stable and stateless", CcImmunityBrakeSelfTests.StatusOrderingIsStableAndRulesAreStateless),
    ("target pressure merges every exact source", TargetPressureSnapshotSelfTests.AllSourcesAreMergedAndExposed),
    ("target pressure eligibility fails closed", TargetPressureSnapshotSelfTests.EnemyEligibilityFailsClosed),
    ("target pressure rejects ambiguous actor identities", TargetPressureSnapshotSelfTests.AmbiguousActorIdentitiesFailClosed),
    ("target pressure duplicate observations merge safely", TargetPressureSnapshotSelfTests.DuplicateObservationsMergeSafely),
    ("target pressure event signals are narrow and job checked", TargetPressureSnapshotSelfTests.EventSignalsAreNarrowAndJobChecked),
    ("team pressure is exact and deduplicated", TargetPressureSnapshotSelfTests.AllyTargetsAreExactAndDeduplicated),
    ("team pressure rejects conflicting ally identities", TargetPressureSnapshotSelfTests.ConflictingAllyIdentitiesFailClosed),
    ("incoming ally hard and cast intent counts one enemy once", TargetPressureSnapshotSelfTests.IncomingHardAndCastIntentFromOneEnemyCountsOnce),
    ("incoming ally intent counts unique live enemies", TargetPressureSnapshotSelfTests.IncomingIntentCountsUniqueLiveEnemies),
    ("incoming ally intent rejects ambiguous identities", TargetPressureSnapshotSelfTests.IncomingIntentRejectsAmbiguousAndPartialIdentities),
    ("incoming ally pressure distinguishes zero from unknown", TargetPressureSnapshotSelfTests.IncomingPressureDistinguishesKnownZeroFromUnknown),
    ("incoming ally pressure includes exact local player", TargetPressureSnapshotSelfTests.IncomingPressureIncludesExactLocalPlayer),
    ("incoming local pressure ignores targetless recent signals", TargetPressureSnapshotSelfTests.LocalRecentSignalDoesNotInventIncomingIntent),
    ("target pressure ordering is deterministic", TargetPressureSnapshotSelfTests.OrderingIsDeterministic),
    ("target pressure clears duplicate CC slots", TargetPressureSnapshotSelfTests.DuplicateCcSlotsAreCleared),
    ("target pressure invalid local state fails closed", TargetPressureSnapshotSelfTests.InvalidLocalAndNullInputAreHandled),
    ("Near Assist pressure toggle preserves existing selection", TargetPressureSnapshotSelfTests.NearAssistDisabledPreservesExistingSelection),
    ("Near Assist follows team pressure inside nearby window", TargetPressureSnapshotSelfTests.NearAssistPressureWinsInsideNearbyWindow),
    ("Near Assist pressure cannot pull across the arena", TargetPressureSnapshotSelfTests.NearAssistPressureCannotPullAcrossArena),
    ("Near Assist pressure ties use existing stable order", TargetPressureSnapshotSelfTests.NearAssistPressureTiesUseExistingOrder),
    ("Near Assist pressure invalid candidates fail closed", TargetPressureSnapshotSelfTests.NearAssistPressureInvalidCandidatesFailClosed),
    ("Miracle exact start signatures are narrow", MiracleInterceptSelfTests.ExactStartSignaturesAreNarrow),
    ("Miracle held input dispatches once", MiracleInterceptSelfTests.HeldInputDispatchesAndSignalCannotRearm),
    ("Miracle attempt alone never confirms landing", MiracleInterceptConfirmationSelfTests.AttemptNeverConfirmsLocally),
    ("Miracle exact status add confirms and labels threat", MiracleInterceptConfirmationSelfTests.ExactStatusAddConfirmsAndLabelsThreat),
    ("Reactive BRD CC confirms only from exact Silence", MiracleInterceptConfirmationSelfTests.SilentNocturneRequiresExactSilenceStatus),
    ("Reactive NIN CC confirms both Raiju variants from exact Stun", MiracleInterceptConfirmationSelfTests.NinjaRaijuVariantsRequireExactStunStatus),
    ("Miracle landing correlation requires exact evidence", MiracleInterceptConfirmationSelfTests.CorrelationRequiresExactIdentityShapeAndWindow),
    ("Miracle landing packet is counted once", MiracleInterceptConfirmationSelfTests.DuplicateCannotIncrementTwice),
    ("Miracle landing pending cannot be overwritten", MiracleInterceptConfirmationSelfTests.NewAttemptCannotOverwriteActivePending),
    ("Miracle landing popup and pending expire", MiracleInterceptConfirmationSelfTests.PopupAndPendingExpireWithoutReplay),
    ("Miracle Viper accepts first-frame protection absence", MiracleInterceptSelfTests.ViperMayAlreadyBeUnprotectedOnFirstFrame),
    ("Miracle Viper waits for actual protection absence", MiracleInterceptSelfTests.ViperWaitsForActualProtectionAbsence),
    ("Miracle protection and range waits are bounded", MiracleInterceptSelfTests.OtherProtectionAndRangeWaitOnlyInsideDeadline),
    ("Miracle priority and identity fail closed", MiracleInterceptSelfTests.HigherPriorityAndIdentityFailClosed),
    ("Miracle stable hold wins and typing is ignored", MiracleInterceptSelfTests.StableHoldWinsAndTypingNeverTriggers),
    ("Miracle jobs and action identities are exact", MiracleInterceptSelfTests.ThreatJobsAndActionsMustMatch),
    ("Protection-end held consent is exact and scoped", MiracleProtectionEndSelfTests.HeldConsentRequiresOneExactUnconsumedGeneration),
    ("Protection-end positive-pressure bonus and fallbacks are deterministic", MiracleProtectionEndSelfTests.RankingUsesPositivePressureBonusThenHealthMpAndIdentity),
    ("WHM, BRD, and NIN share protection-end semantics", MiracleProtectionEndSelfTests.WhiteMageBardAndNinjaShareProtectionEndSemantics),
    ("Protection-end held lease survives priority and retries", MiracleProtectionEndSelfTests.HeldLeaseSurvivesPriorityAndRetriesOnlyInsideItsBound),
    ("Reactive CC follow-up accepts exact self-Purify action evidence", MiracleCleanseFollowupSelfTests.ExactPurifySignalAcceptsActionLevelOrKnownRecovery),
    ("Reactive CC follow-up retries only exact canonical resolution", MiracleCleanseFollowupSelfTests.ValidatedSignalRetriesOnlyCanonicalResolutionInsideOriginalDeadline),
    ("Miracle follow-up observes release and promotes once", MiracleCleanseFollowupSelfTests.ExactLifecyclePromotesOnceAfterObservedRelease),
    ("Miracle follow-up rejects immunity flicker and ambiguity", MiracleCleanseFollowupSelfTests.MissingGraceRejectsFlickerAndAmbiguity),
    ("Miracle follow-up windows are bounded", MiracleCleanseFollowupSelfTests.AcquisitionReleaseAndOpportunityWindowsAreBounded),
    ("Miracle follow-up yields to immediate helper priority", MiracleCleanseFollowupSelfTests.HigherPriorityWaitsWithoutDestroyingOpportunity),
    ("Reactive CC follow-up pressure has no minimum gate", MiracleCleanseFollowupSelfTests.TeamPressureHasNoMinimumAndUnknownRemainsEligible),
    ("Miracle follow-up identity and concurrency fail closed", MiracleCleanseFollowupSelfTests.IdentityAmbiguityAndConcurrencyFailClosed),
    ("Miracle follow-up tracks distinct Purify slots independently", MiracleCleanseFollowupSelfTests.IndependentEnemySlotsKeepDistinctPurifyEpisodes),
    ("Miracle follow-up uses first authoritative absence at expected end", MiracleCleanseFollowupSelfTests.ExpectedEndUsesFirstAuthoritativeAbsentFrame),
    ("Miracle follow-up invalid duration keeps absence grace", MiracleCleanseFollowupSelfTests.InvalidExpectedEndKeepsAbsenceGrace),
    ("Miracle follow-up binds the current key at release", MiracleCleanseFollowupSelfTests.ReservationBindsAtReleaseAndThenRequiresExactKey),
    ("Miracle follow-up labels confirmation without broad start rules", MiracleCleanseFollowupSelfTests.PromotionKindLabelsConfirmationWithoutBroadeningStartRules),
    ("Guard follow-up requires exact presence before absence", MiracleGuardFollowupSelfTests.ExactGuardRowsAndAbsenceCannotSyntheticArm),
    ("Guard follow-up releases on first verified absent frame", MiracleGuardFollowupSelfTests.FirstVerifiedAbsentFramePromotesOnceAndRequiresPositiveRearm),
    ("Guard follow-up pressure has no minimum and priority is bounded", MiracleGuardFollowupSelfTests.PressureHasNoMinimumAndPriorityWaitsInsideOriginalWindow),
    ("Guard follow-up positive-pressure bonus and fallbacks are deterministic", MiracleGuardFollowupSelfTests.SimultaneousReleaseUsesPositivePressureBonusThenFallbacks),
    ("Guard follow-up identity and status ambiguity fail closed", MiracleGuardFollowupSelfTests.IdentityLifeAndStatusAmbiguityBreakTheEpisode),
    ("Guard follow-up reset gates clear every episode", MiracleGuardFollowupSelfTests.ConfigurationContextClockAndHardResetClearAllEpisodes),
    ("Guard follow-up binds the current key on release", MiracleGuardFollowupSelfTests.ReservationBindsOnGuardEndAndAllowsEarlyCancel),
    ("Guard follow-up release opportunity freezes one exact key", MiracleGuardFollowupSelfTests.ReleaseOpportunityAcquiresOnceAndThenRequiresExactKey),
    ("Monk Earth Reply identities are exact", MonkEarthReplySelfTests.ExactActionAndStatusIdentityIsFixed),
    ("Monk Earth Reply low-health threshold is exact", MonkEarthReplySelfTests.LowHealthThresholdIsInclusiveAndOverflowSafe),
    ("Monk Earth Reply expiry threshold is exact", MonkEarthReplySelfTests.ExpiryThresholdIsInclusiveAndLowHpWins),
    ("Monk Earth Reply defers to Purify without spending", MonkEarthReplySelfTests.PurifyPriorityDefersWithoutSpending),
    ("Monk Earth Reply requires the adjusted follow-up", MonkEarthReplySelfTests.ExactAdjustedFollowUpIsMandatory),
    ("Monk Earth Reply spends before its sole attempt", MonkEarthReplySelfTests.DispatchSpendsBeforeAnyAttemptResult),
    ("Monk Earth Reply status flicker cannot rearm", MonkEarthReplySelfTests.AbsenceGracePreventsFlickerRearm),
    ("Monk Earth Reply safety gates fail closed", MonkEarthReplySelfTests.SafetyGatesAndInvalidInputsFailClosed),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {test.Name}: {exception.Message}");
    }
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
if (failures.Count > 0)
{
    Environment.ExitCode = 1;
    return;
}

Console.WriteLine($"PASS all {tests.Length} core tests");

static void NinjaJobGateIsExact()
{
    True(ExecuteThreshold.IsNinja(30), "NIN row");
    False(ExecuteThreshold.IsNinja(29), "non-NIN row");
    False(ExecuteThreshold.IsNinja(0), "invalid row");
}

static void ExecuteThresholdIsStrict()
{
    True(ExecuteThreshold.IsBelowHalf(49, 100), "49 percent");
    True(ExecuteThreshold.IsBelowHalf(49_999, 100_000), "fraction below half");
    False(ExecuteThreshold.IsBelowHalf(50, 100), "exactly 50 percent");
    False(ExecuteThreshold.IsBelowHalf(51, 100), "above half");
}

static void InvalidHpFailsClosed()
{
    False(ExecuteThreshold.IsBelowHalf(0, 100), "dead target");
    False(ExecuteThreshold.IsBelowHalf(1, 0), "zero maximum");
    False(ExecuteThreshold.IsBelowHalf(101, 100), "impossible current HP");
    True(ExecuteThreshold.IsBelowHalf(uint.MaxValue / 4, uint.MaxValue), "wide arithmetic");
}

static void EnemySlotLabelsAreExact()
{
    for (var slot = 1; slot <= 5; slot++)
    {
        True(EnemySlotRules.IsValidSlot(slot), $"slot {slot}");
        Equal($"S{slot}", EnemySlotRules.Label(slot), $"label {slot}");
    }

    False(EnemySlotRules.IsValidSlot(0), "slot zero");
    False(EnemySlotRules.IsValidSlot(6), "slot six");
    Equal(string.Empty, EnemySlotRules.Label(0), "invalid label");
}

static void EnemyValidationFailsClosed()
{
    True(EnemySlotRules.CanUseResolvedEnemy(false, false, true, false, true, true, 10, 100), "hostile enemy");
    True(EnemySlotRules.CanUseResolvedEnemy(false, false, false, true, true, true, 10, 100), "complete CC party fallback");
    False(EnemySlotRules.CanUseResolvedEnemy(true, false, false, true, true, true, 10, 100), "fallback cannot admit self");
    False(EnemySlotRules.CanUseResolvedEnemy(false, true, false, true, true, true, 10, 100), "fallback cannot admit party or alliance");
    False(EnemySlotRules.CanUseResolvedEnemy(false, false, false, false, true, true, 10, 100), "unknown relation");
    False(EnemySlotRules.CanUseResolvedEnemy(false, false, false, true, false, true, 10, 100), "fallback cannot admit dead");
    False(EnemySlotRules.CanUseResolvedEnemy(false, false, false, true, true, false, 10, 100), "fallback cannot admit untargetable");
    False(EnemySlotRules.CanUseResolvedEnemy(false, false, false, true, true, true, 0, 100), "fallback requires positive HP");
    False(EnemySlotRules.CanUseResolvedEnemy(false, false, false, true, true, true, 10, 0), "fallback requires positive max HP");
    False(EnemySlotRules.CanUseResolvedEnemy(false, false, false, true, true, true, 101, 100), "fallback rejects impossible HP");
}

static void NativeRangeResultIsExact()
{
    True(SeitonRangeRules.HasNativeRangeAndLineOfSight(0), "native success");
    True(SeitonRangeRules.HasNativeRangeAndLineOfSight(565), "not-facing still has range and line of sight");
    False(SeitonRangeRules.HasNativeRangeAndLineOfSight(562), "line of sight failure");
    False(SeitonRangeRules.HasNativeRangeAndLineOfSight(566), "out of range");
    False(SeitonRangeRules.HasNativeRangeAndLineOfSight(uint.MaxValue), "unknown result");
}

static void KnownCcTerritoriesAreComplete()
{
    var publicTerritories = new uint[] { 1032, 1033, 1034, 1116, 1138, 1293, 1357 };
    var customTerritories = new uint[] { 1058, 1059, 1060, 1117, 1139, 1294, 1358 };
    True(publicTerritories.All(PvPMatchRules.IsPublicCrystallineConflictTerritory), "public territories");
    True(publicTerritories.Concat(customTerritories).All(PvPMatchRules.IsKnownCrystallineConflictTerritory), "all CC territories");
    False(customTerritories.Any(PvPMatchRules.IsPublicCrystallineConflictTerritory), "custom not public");
}

static void CcMatchingIsFailClosed()
{
    True(PvPMatchRules.IsCrystallineConflict(true, 1032, false, false, 0, false, false), "known territory");
    True(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 43, false, false), "category 43");
    True(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 44, false, false), "category 44");
    True(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 0, true, false), "casual roulette");
    True(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 0, false, true), "ranked roulette");
    False(PvPMatchRules.IsCrystallineConflict(false, 1032, true, true, 43, true, true), "not live PvP");
    False(PvPMatchRules.IsCrystallineConflict(true, 9999, false, true, 43, false, false), "invalid condition");
    False(PvPMatchRules.IsCrystallineConflict(true, 9999, true, false, 43, false, false), "non-PvP condition");
    False(PvPMatchRules.IsCrystallineConflict(true, 9999, true, true, 12, false, false), "unrelated mode");
}

static void SupportedPvpContextIsExact()
{
    Equal(
        SupportedPvPContext.CrystallineConflict,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: true,
            includeWolvesDenTesting: false,
            territoryId: 1032,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "known CC remains supported without Wolves' Den opt-in");

    Equal(
        SupportedPvPContext.WolvesDen,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: false,
            includeWolvesDenTesting: true,
            territoryId: 250,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "Wolves' Den flags plus opt-in");

    Equal(
        SupportedPvPContext.None,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: false,
            includeWolvesDenTesting: false,
            territoryId: 250,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "Wolves' Den is disabled without opt-in");

    Equal(
        SupportedPvPContext.None,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: false,
            isPvPExcludingWolvesDen: false,
            includeWolvesDenTesting: true,
            territoryId: 250,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "non-PvP cannot become Wolves' Den");

    Equal(
        SupportedPvPContext.None,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: false,
            includeWolvesDenTesting: true,
            territoryId: 9999,
            conditionValid: false,
            conditionPvP: false,
            contentUiCategoryId: 0,
            casualRoulette: false,
            rankedRoulette: false),
        "Wolves' Den flags outside territory 250 fail closed");

    Equal(
        SupportedPvPContext.None,
        PvPMatchRules.ResolveSupportedContext(
            isPvP: true,
            isPvPExcludingWolvesDen: true,
            includeWolvesDenTesting: true,
            territoryId: 9999,
            conditionValid: true,
            conditionPvP: true,
            contentUiCategoryId: 45,
            casualRoulette: false,
            rankedRoulette: false),
        "Frontline or Rival Wings remains excluded");
}

static void WolvesDenOpponentSelectionIsStrict()
{
    var enemy = new WolvesDenOpponentCandidate(
        EntityId: 100,
        GameObjectId: 1_000,
        MatchesNativeDuelEnemyId: true,
        HasValidAddress: true,
        IsPlayerCharacter: true,
        IsSelf: false,
        HasHostileFlag: true,
        IsTargetable: true);

    var resolved = WolvesDenOpponentRules.ResolveSingleSlot([enemy]);
    True(resolved.HasValue, "one strict hostile resolves");
    Equal(EnemySlotRules.FirstSlot, resolved!.Value.Slot, "duel opponent uses S1");
    Equal(100u, resolved.Value.EntityId, "resolved entity is preserved");

    False(WolvesDenOpponentRules.ResolveSingleSlot([]).HasValue, "zero candidates fail closed");
    False(
        WolvesDenOpponentRules.ResolveSingleSlot([enemy, enemy with { EntityId = 101 }]).HasValue,
        "multiple hostile candidates fail closed");

    var rejectedCandidates = new[]
    {
        enemy with { EntityId = 0 },
        enemy with { EntityId = 0xE0000000 },
        enemy with { GameObjectId = 0 },
        enemy with { GameObjectId = 0xE0000000 },
        enemy with { MatchesNativeDuelEnemyId = false },
        enemy with { HasValidAddress = false },
        enemy with { IsPlayerCharacter = false },
        enemy with { IsSelf = true },
        enemy with { HasHostileFlag = false },
        enemy with { IsTargetable = false },
    };

    foreach (var candidate in rejectedCandidates)
    {
        False(
            WolvesDenOpponentRules.ResolveSingleSlot([candidate]).HasValue,
            $"ineligible candidate fails closed: {candidate}");
    }

    resolved = WolvesDenOpponentRules.ResolveSingleSlot([
        enemy with { MatchesNativeDuelEnemyId = false },
        enemy,
        enemy with { HasHostileFlag = false },
    ]);
    True(resolved.HasValue, "ineligible bystanders do not block one strict hostile");
    Equal(100u, resolved!.Value.EntityId, "strict hostile remains S1");
}

static void VisibilityHasFalseGrace()
{
    var state = DebouncedVisibilityRules.Observe(DebouncedVisibilityState.Initial, true, 1_000);
    state = DebouncedVisibilityRules.Observe(state, false, 1_050);
    True(state.IsVisible, "first false sample remains visible");
    state = DebouncedVisibilityRules.Observe(state, true, 1_100);
    True(state.IsVisible, "true sample cancels pending hide");
    state = DebouncedVisibilityRules.Observe(state, false, 1_150);
    state = DebouncedVisibilityRules.Observe(state, false, 1_349);
    True(state.IsVisible, "inside grace");
    state = DebouncedVisibilityRules.Observe(state, false, 1_350);
    False(state.IsVisible, "grace elapsed");

    state = DebouncedVisibilityRules.Observe(DebouncedVisibilityState.Initial, true, 2_000);
    state = DebouncedVisibilityRules.Observe(
        state,
        false,
        2_050,
        falseGraceMilliseconds: PersonalDebuffAlertRules.MissingGraceMilliseconds);
    True(state.IsVisible, "Resilience-style status gap remains active inside 150 ms grace");
    state = DebouncedVisibilityRules.Observe(
        state,
        false,
        2_199,
        falseGraceMilliseconds: PersonalDebuffAlertRules.MissingGraceMilliseconds);
    True(state.IsVisible, "custom grace remains active before its exact boundary");
    state = DebouncedVisibilityRules.Observe(
        state,
        false,
        2_200,
        falseGraceMilliseconds: PersonalDebuffAlertRules.MissingGraceMilliseconds);
    False(state.IsVisible, "custom grace expires at its exact boundary");
}

static void VisibilityHardResetIsImmediate()
{
    var state = DebouncedVisibilityRules.Observe(DebouncedVisibilityState.Initial, true, 1_000);
    state = DebouncedVisibilityRules.Observe(state, true, 1_001, hardReset: true);
    False(state.IsVisible, "hard reset overrides a visible observation");
}

static void UnknownGuardFailsClosed()
{
    Equal(GuardAvailability.Unknown, GuardCooldownRules.GetAvailability(GuardCooldownState.Initial, 1_000), "unknown state");
    False(GuardCooldownRules.ShouldShowCrossedIcon(GuardCooldownState.Initial, 1_000), "unknown has no crossed icon");
}

static void GuardActionTracksCooldown()
{
    var state = GuardCooldownRules.ObserveAction(GuardCooldownState.Initial, 1_000);
    Equal(GuardAvailability.Unavailable, GuardCooldownRules.GetAvailability(state, 30_999), "before deadline");
    Equal(1L, GuardCooldownRules.RemainingMilliseconds(state, 30_999), "remaining time");
    Equal(GuardAvailability.Ready, GuardCooldownRules.GetAvailability(state, 31_000), "at deadline");
}

static void GuardStatusInfersDeadline()
{
    var state = GuardCooldownRules.ObserveStatus(GuardCooldownState.Initial, 10_000, 2_000);
    Equal(38_000L, state.ReadyAtMilliseconds, "now plus remaining plus 26 seconds");
    state = GuardCooldownRules.ObserveStatus(state, 10_100, 2_000);
    Equal(38_000L, state.ReadyAtMilliseconds, "stale remaining time cannot extend deadline");
    state = GuardCooldownRules.ObserveStatus(state, 10_200, 1_600);
    Equal(38_000L, state.ReadyAtMilliseconds, "later status cannot move the inferred deadline");
}

static void GuardStatusTracksLaterActivation()
{
    var state = GuardCooldownRules.ObserveStatus(GuardCooldownState.Initial, 10_000, 2_000);
    state = GuardCooldownRules.ObserveStatus(state, 38_000, 4_000);
    Equal(68_000L, state.ReadyAtMilliseconds, "new status after prior recast gets a new deadline");
}

static void GuardReviveResetsCooldown()
{
    var state = GuardCooldownRules.ObserveAction(GuardCooldownState.Initial, 1_000);
    state = GuardCooldownRules.ObserveRevive();
    Equal(GuardAvailability.Ready, GuardCooldownRules.GetAvailability(state, 1_001), "recast reset on revive");
}

static void LowMpRequiresStableTrustedSample()
{
    var state = LowMpRules.Observe(LowMpState.Initial, 1_500, trustedSample: false, 1_000);
    False(LowMpRules.ShouldShowCrossedIcon(state), "untrusted low sample");
    state = LowMpRules.Observe(state, 1_500, trustedSample: true, 1_050);
    False(LowMpRules.ShouldShowCrossedIcon(state), "debounce begins");
    state = LowMpRules.Observe(state, 1_500, trustedSample: true, 1_199);
    False(LowMpRules.ShouldShowCrossedIcon(state), "inside debounce");
    state = LowMpRules.Observe(state, 1_500, trustedSample: true, 1_200);
    True(LowMpRules.ShouldShowCrossedIcon(state), "stable low MP");
}

static void LowMpUsesExitHysteresis()
{
    var state = LowMpRules.Observe(LowMpState.Initial, 1_000, true, 1_000, debounceMilliseconds: 0);
    True(LowMpRules.ShouldShowCrossedIcon(state), "entered below 2000");
    state = LowMpRules.Observe(state, 2_100, true, 1_050, debounceMilliseconds: 0);
    True(LowMpRules.ShouldShowCrossedIcon(state), "2100 remains unavailable");
    state = LowMpRules.Observe(state, 2_300, true, 1_100);
    state = LowMpRules.Observe(state, 2_300, true, 1_250);
    False(LowMpRules.ShouldShowCrossedIcon(state), "2300 exits after debounce");
}

static void LowMpTrustLossIsStable()
{
    var pending = LowMpRules.Observe(LowMpState.Initial, 1_500, true, 1_000);
    pending = LowMpRules.Observe(pending, 0, false, 1_100);
    pending = LowMpRules.Observe(pending, 1_500, true, 1_200);
    False(LowMpRules.ShouldShowCrossedIcon(pending), "untrusted gap restarts pending entry");
    pending = LowMpRules.Observe(pending, 1_500, true, 1_350);
    True(LowMpRules.ShouldShowCrossedIcon(pending), "restarted entry completes after full debounce");

    var latched = LowMpRules.Observe(LowMpState.Initial, 1_000, true, 2_000, debounceMilliseconds: 0);
    latched = LowMpRules.Observe(latched, 0, false, 2_100);
    True(LowMpRules.ShouldShowCrossedIcon(latched), "missing sample preserves an established low-MP icon");
}

static void LowMpThresholdsAreExact()
{
    var ready = LowMpRules.Observe(LowMpState.Initial, 2_000, true, 1_000, debounceMilliseconds: 0);
    False(LowMpRules.ShouldShowCrossedIcon(ready), "exactly 2000 can afford Recuperate");

    var low = LowMpRules.Observe(LowMpState.Initial, 1_999, true, 1_000, debounceMilliseconds: 0);
    True(LowMpRules.ShouldShowCrossedIcon(low), "1999 cannot afford Recuperate");
    low = LowMpRules.Observe(low, 2_299, true, 1_050, debounceMilliseconds: 0);
    True(LowMpRules.ShouldShowCrossedIcon(low), "2299 remains inside recovery hysteresis");
    low = LowMpRules.Observe(low, 2_300, true, 1_100, debounceMilliseconds: 0);
    False(LowMpRules.ShouldShowCrossedIcon(low), "exactly 2300 clears hysteresis");
}

static void StablePopupIsOneShot()
{
    var decision = StablePopupRules.Observe(StablePopupState.Initial, true, false, 1_000);
    False(decision.TriggerPopup, "first true sample");
    decision = StablePopupRules.Observe(decision.NextState, true, false, 1_049);
    False(decision.TriggerPopup, "not stable long enough");
    decision = StablePopupRules.Observe(decision.NextState, true, false, 1_050);
    True(decision.TriggerPopup, "stable rising edge");
    decision = StablePopupRules.Observe(decision.NextState, true, false, 1_500);
    False(decision.TriggerPopup, "latched one shot");
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_600);
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_900);
    True(decision.NextState.Armed, "stable false rearms");
    decision = StablePopupRules.Observe(decision.NextState, true, false, 2_000, stableTrueMilliseconds: 0);
    True(decision.TriggerPopup, "new rising edge triggers");
}

static void SeitonRangeJitterCannotRearm()
{
    var decision = StablePopupRules.Observe(
        StablePopupState.Initial,
        candidate: true,
        rearmCondition: false,
        nowMilliseconds: 1_000,
        stableTrueMilliseconds: 0);
    True(decision.TriggerPopup, "initial popup");

    decision = StablePopupRules.Observe(decision.NextState, false, false, 1_100);
    decision = StablePopupRules.Observe(decision.NextState, false, false, 2_000);
    False(decision.NextState.Armed, "range loss remains latched");

    decision = StablePopupRules.Observe(decision.NextState, true, false, 2_050, stableTrueMilliseconds: 0);
    False(decision.TriggerPopup, "walking back into range cannot pop again");
}

static void SeitonPopupRearmMustRemainStable()
{
    var decision = StablePopupRules.Observe(
        StablePopupState.Initial,
        candidate: true,
        rearmCondition: false,
        nowMilliseconds: 1_000,
        stableTrueMilliseconds: 0);
    True(decision.TriggerPopup, "initial popup");

    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_100);
    decision = StablePopupRules.Observe(decision.NextState, false, false, 1_399);
    False(decision.NextState.Armed, "interrupted rearm is cancelled");
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_400);
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_699);
    False(decision.NextState.Armed, "new rearm has not reached boundary");
    decision = StablePopupRules.Observe(decision.NextState, false, true, 1_700);
    True(decision.NextState.Armed, "exact 300 ms rearms");

    decision = StablePopupRules.Observe(decision.NextState, true, false, 2_000, hardReset: true);
    True(decision.NextState.Armed, "hard reset restores initial armed state");
    False(decision.TriggerPopup, "hard reset never emits a popup");
}

static void PersistentSeitonCueEntersOnce()
{
    var decision = ObserveSeiton(PersistentSeitonCueState.Initial, hp: 49, now: 1_000);
    Equal(SeitonCueKind.Hidden, decision.Cue, "execute entry waits for stability");
    False(decision.TriggerEntryPulse, "first sample does not pulse");

    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_050);
    Equal(SeitonCueKind.Execute, decision.Cue, "execute cue appears at stable boundary");
    True(decision.TriggerEntryPulse, "execute entry pulses once");

    decision = ObserveSeiton(decision.NextState, hp: 48, now: 5_000);
    Equal(SeitonCueKind.Execute, decision.Cue, "cue remains visible while eligible");
    False(decision.TriggerEntryPulse, "persistent cue does not repeatedly pulse");
}

static void PersistentSeitonCueIgnoresRangeJitter()
{
    var decision = ObserveSeiton(
        PersistentSeitonCueState.Initial,
        hp: 49,
        now: 1_000,
        stableExecuteMilliseconds: 0);
    True(decision.TriggerEntryPulse, "initial entry pulse");

    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_100, inRange: false);
    Equal(SeitonCueKind.Execute, decision.Cue, "short range loss retains cue");
    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_300, inRange: false);
    Equal(SeitonCueKind.Hidden, decision.Cue, "sustained range loss hides cue");

    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_350, inRange: true);
    Equal(SeitonCueKind.Execute, decision.Cue, "return to range restores persistent cue");
    False(decision.TriggerEntryPulse, "range return never repulses");

    decision = ObserveSeiton(decision.NextState, hp: 52, now: 1_400, inRange: false);
    Equal(SeitonCueKind.Hidden, decision.Cue, "healing out of execute range clears a stale cue immediately");
}

static void PersistentSeitonCueRearmsSemantically()
{
    var decision = ObserveSeiton(
        PersistentSeitonCueState.Initial,
        hp: 49,
        now: 1_000,
        stableExecuteMilliseconds: 0);
    True(decision.TriggerEntryPulse, "first execute entry");

    decision = ObserveSeiton(decision.NextState, hp: 51, now: 1_100);
    Equal(SeitonCueKind.Preparation, decision.Cue, "51 percent is no longer falsely actionable");
    False(decision.TriggerEntryPulse, "threshold jitter does not repulse");

    decision = ObserveSeiton(
        decision.NextState,
        hp: 49,
        now: 1_150,
        stableExecuteMilliseconds: 0);
    Equal(SeitonCueKind.Execute, decision.Cue, "falling below half restores the execute cue");
    False(decision.TriggerEntryPulse, "sub-52 percent jitter does not rearm the entry pulse");

    decision = ObserveSeiton(decision.NextState, hp: 52, now: 1_200);
    Equal(SeitonCueKind.Preparation, decision.Cue, "52 percent rearms into preparation");
    decision = ObserveSeiton(
        decision.NextState,
        hp: 49,
        now: 1_300,
        stableExecuteMilliseconds: 0);
    True(decision.TriggerEntryPulse, "new execute entry after semantic recovery pulses");

    decision = ObserveSeiton(decision.NextState, hp: 49, now: 1_400, resourceReady: false);
    Equal(SeitonCueKind.Hidden, decision.Cue, "resource use clears cue");
    decision = ObserveSeiton(
        decision.NextState,
        hp: 49,
        now: 1_500,
        stableExecuteMilliseconds: 0);
    True(decision.TriggerEntryPulse, "new resource activation rearms pulse");
}

static void SeitonPreparationBandIsExact()
{
    True(PersistentSeitonCueRules.IsPreparationBand(50, 100), "exactly 50 percent");
    True(PersistentSeitonCueRules.IsPreparationBand(59_999, 100_000), "below 60 percent");
    False(PersistentSeitonCueRules.IsPreparationBand(49, 100), "execute band");
    False(PersistentSeitonCueRules.IsPreparationBand(60, 100), "exactly 60 percent");
    False(PersistentSeitonCueRules.IsPreparationBand(0, 100), "dead target");

    var shown = ObserveSeiton(PersistentSeitonCueState.Initial, hp: 55, now: 1_000);
    Equal(SeitonCueKind.Preparation, shown.Cue, "preparation enabled");
    var hidden = ObserveSeiton(
        PersistentSeitonCueState.Initial,
        hp: 55,
        now: 1_000,
        showPreparation: false);
    Equal(SeitonCueKind.Hidden, hidden.Cue, "preparation disabled");

    hidden = ObserveSeiton(
        shown.NextState,
        hp: 55,
        now: 1_050,
        inRange: false,
        showPreparation: false);
    Equal(SeitonCueKind.Hidden, hidden.Cue, "disabled preparation never survives range grace");

    hidden = ObserveSeiton(shown.NextState, hp: 60, now: 1_050, inRange: false);
    Equal(SeitonCueKind.Hidden, hidden.Cue, "leaving the preparation band clears stale grace");
}

static void PersonalDebuffsDeduplicateAndOrder()
{
    var observations = new[]
    {
        new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 7_000),
        new PersonalDebuffObservation(20, PersonalDebuffAlertKind.CleanseUrgent, 4_000),
        new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 8_000),
        new PersonalDebuffObservation(30, PersonalDebuffAlertKind.Warning, 3_000),
    };
    var decision = PersonalDebuffAlertRules.Observe([], observations, 1_000);

    Equal(3, decision.Alerts.Length, "duplicate status collapsed");
    Equal(20u, decision.Alerts[0].StatusId, "cleanse warning ordered first");
    Equal(30u, decision.Alerts[1].StatusId, "shorter regular warning ordered next");
    Equal(10u, decision.Alerts[2].StatusId, "longer warning ordered last");
    Equal(7_000L, decision.Alerts[2].RemainingMilliseconds, "newest duplicate expiry retained");
    True(decision.Alerts.All(alert => alert.TriggerEntryPulse), "each unique entry pulses once");
}

static void PersonalDebuffRefreshDoesNotRepulse()
{
    var first = PersonalDebuffAlertRules.Observe(
        [],
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 3_000)],
        1_000);
    True(first.Alerts[0].TriggerEntryPulse, "first observation pulses");

    var refreshed = PersonalDebuffAlertRules.Observe(
        first.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 5_000)],
        1_100);
    Equal(3_900L, refreshed.Alerts[0].RemainingMilliseconds, "countdown uses refreshed expiry");
    False(refreshed.Alerts[0].TriggerEntryPulse, "duration refresh is not a new application");
}

static void PersonalDebuffMissingGracePreventsFlicker()
{
    var first = PersonalDebuffAlertRules.Observe(
        [],
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 5_000)],
        1_000);
    var missing = PersonalDebuffAlertRules.Observe(first.NextStates, [], 1_050);
    Equal(1, missing.Alerts.Length, "first missing sample remains visible");
    False(missing.Alerts[0].TriggerEntryPulse, "missing grace never pulses");

    var returned = PersonalDebuffAlertRules.Observe(
        missing.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 5_000)],
        1_199);
    False(returned.Alerts[0].TriggerEntryPulse, "return inside grace is same application");

    missing = PersonalDebuffAlertRules.Observe(returned.NextStates, [], 1_300);
    var removed = PersonalDebuffAlertRules.Observe(missing.NextStates, [], 1_450);
    Equal(0, removed.Alerts.Length, "missing grace boundary removes warning");
    var reapplied = PersonalDebuffAlertRules.Observe(
        removed.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 6_000)],
        1_500);
    True(reapplied.Alerts[0].TriggerEntryPulse, "application after removal pulses again");
}

static void PersonalDebuffEscalationPulsesOnce()
{
    var first = PersonalDebuffAlertRules.Observe(
        [],
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.Warning, 5_000)],
        1_000);
    var escalated = PersonalDebuffAlertRules.Observe(
        first.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.CleanseUrgent, 5_000)],
        1_100);
    True(escalated.Alerts[0].TriggerEntryPulse, "urgency escalation gets attention once");
    var stable = PersonalDebuffAlertRules.Observe(
        escalated.NextStates,
        [new PersonalDebuffObservation(10, PersonalDebuffAlertKind.CleanseUrgent, 5_000)],
        1_200);
    False(stable.Alerts[0].TriggerEntryPulse, "stable urgent alert does not repulse");
}

static void PersonalDebuffLifecycleFailsClosed()
{
    var decision = PersonalDebuffAlertRules.Observe(
        [],
        [
            new PersonalDebuffObservation(0, PersonalDebuffAlertKind.Warning, 5_000),
            new PersonalDebuffObservation(10, (PersonalDebuffAlertKind)99, 5_000),
            new PersonalDebuffObservation(20, PersonalDebuffAlertKind.Warning, 1_000),
        ],
        1_000);
    Equal(0, decision.Alerts.Length, "invalid and expired observations are ignored");

    decision = PersonalDebuffAlertRules.Observe(
        [],
        [new PersonalDebuffObservation(30, PersonalDebuffAlertKind.CleanseUrgent, 5_000)],
        1_100,
        hardReset: true);
    Equal(0, decision.Alerts.Length, "hard reset clears alerts immediately");
    Equal(0, decision.NextStates.Length, "hard reset clears lifecycle state");
}

static PersistentSeitonCueDecision ObserveSeiton(
    PersistentSeitonCueState state,
    uint hp,
    long now,
    bool resourceReady = true,
    bool inRange = true,
    bool showPreparation = true,
    long stableExecuteMilliseconds = PersistentSeitonCueRules.StableExecuteMilliseconds) =>
    PersistentSeitonCueRules.Observe(
        state,
        resourceReady,
        targetPresent: true,
        trustedHealthSample: true,
        hp,
        maximumHp: 100,
        inRange,
        showPreparation,
        now,
        stableExecuteMilliseconds: stableExecuteMilliseconds);

static void True(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException($"Expected true: {label}");
}

static void False(bool condition, string label) => True(!condition, label);

static void Equal<T>(T expected, T actual, string label)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}
