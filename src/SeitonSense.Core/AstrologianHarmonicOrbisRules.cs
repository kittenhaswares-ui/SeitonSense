namespace SeitonSense.Core;

public readonly record struct AstrologianHarmonicOrbisIntent(
    TargetPressureActorIdentity Target,
    int PartySlot,
    bool DoubleCastWasReady,
    ulong BaseChargeEpochToken,
    ulong OrbisFrameworkFrame)
{
    public bool IsValid =>
        Target.IsValid &&
        NearHelpSelectionRules.IsValidPartySlot(PartySlot) &&
        BaseChargeEpochToken != 0;
}

/// <summary>
/// Monotone ownership for one observed available Harmonic Orbis charge. A
/// client-accepted use spends the exact token. The same unchanged charge count
/// cannot open another token; a later observed charge-count transition must
/// prove a distinct remaining or recovered charge first.
/// </summary>
public readonly record struct AstrologianHarmonicOrbisBaseChargeEpochState(
    bool ChargeCountKnown,
    uint LastObservedCharges,
    ulong CurrentEpochToken,
    ulong SpentEpochToken)
{
    public static AstrologianHarmonicOrbisBaseChargeEpochState Initial => default;

    public bool IsValid =>
        LastObservedCharges <=
            AstrologianHarmonicOrbisRules.MaximumHarmonicOrbisCharges &&
        (CurrentEpochToken != 0 || SpentEpochToken == 0);

    public bool HasAvailableEpoch =>
        IsValid &&
        ChargeCountKnown &&
        LastObservedCharges > 0 &&
        CurrentEpochToken != 0 &&
        CurrentEpochToken != SpentEpochToken;
}

public enum AstrologianHarmonicOrbisFollowUpKind : byte
{
    None = 0,
    Waiting = 1,
    Dispatch = 2,
    Complete = 3,
    Cancelled = 4,
}

public enum AstrologianHarmonicOrbisFollowUpReason : byte
{
    None = 0,
    IntentInvalid,
    OrbisNotAccepted,
    DoubleCastWasUnavailable,
    LaterFrameworkFrameRequired,
    TargetChanged,
    TargetUnavailable,
    CarrierNotAdjusted,
    WrongAdjustedAction,
}

public readonly record struct AstrologianHarmonicOrbisFollowUpDecision(
    AstrologianHarmonicOrbisFollowUpKind Kind,
    AstrologianHarmonicOrbisFollowUpReason Reason,
    uint ActionId = 0,
    TargetPressureActorIdentity Target = default)
{
    public bool ShouldDispatch =>
        Kind == AstrologianHarmonicOrbisFollowUpKind.Dispatch &&
        ActionId ==
            AstrologianHarmonicOrbisRules.DoubleCastHarmonicOrbisActionId &&
        Target.IsValid;
}

/// <summary>
/// Pure, runtime-independent rules for the opt-in held AST healing pair.
/// Initial target selection is exactly Near Help restricted to allies at or
/// below 60% HP. Once Harmonic Orbis is accepted, that HP threshold is no
/// longer consulted: an available Double Cast belongs to the same frozen
/// target and may run only on a later framework frame.
/// </summary>
public static class AstrologianHarmonicOrbisRules
{
    public const uint AstrologianJobId = 33;
    public const uint HarmonicOrbisActionId = 29_243;
    public const uint DoubleCastCarrierActionId = 29_245;
    public const uint DoubleCastHarmonicOrbisActionId = 29_247;
    public const uint MaximumHarmonicOrbisCharges = 2;
    public const int MaximumTargetHealthPercent = 60;

    /// <summary>
    /// Final fail-closed policy for the helper-owned native hook boundary. The
    /// scope is valid only for one authored AST action, one unchanged target,
    /// and the exact local actor which froze the intent. An active or merely
    /// propagating own Guard always vetoes the native action.
    /// </summary>
    public static bool ShouldVetoNativeBoundaryForOwnGuard(
        uint actionId,
        TargetPressureActorIdentity frozenLocalPlayer,
        TargetPressureActorIdentity currentLocalPlayer,
        ulong frozenTargetGameObjectId,
        ulong forwardedTargetGameObjectId,
        bool ownGuardActiveOrPropagating)
    {
        if (actionId is not (HarmonicOrbisActionId or
                DoubleCastHarmonicOrbisActionId) ||
            !frozenLocalPlayer.IsValid ||
            currentLocalPlayer != frozenLocalPlayer ||
            !IsNetworkGameObjectId(frozenTargetGameObjectId) ||
            forwardedTargetGameObjectId != frozenTargetGameObjectId)
        {
            return true;
        }

        return ownGuardActiveOrPropagating;
    }

    public static AstrologianHarmonicOrbisBaseChargeEpochState ObserveBaseChargeEpoch(
        AstrologianHarmonicOrbisBaseChargeEpochState previous,
        bool chargeCountKnown,
        uint currentCharges,
        bool hardReset = false)
    {
        if (hardReset) return AstrologianHarmonicOrbisBaseChargeEpochState.Initial;
        if (!previous.IsValid)
            previous = AstrologianHarmonicOrbisBaseChargeEpochState.Initial;

        if (!chargeCountKnown || currentCharges > MaximumHarmonicOrbisCharges)
            return previous with { ChargeCountKnown = false };

        if (!previous.ChargeCountKnown &&
            previous.LastObservedCharges == currentCharges &&
            (previous.CurrentEpochToken != 0 || currentCharges == 0))
        {
            // Restoring visibility of the same last-known count is not proof
            // that another charge appeared during the telemetry gap.
            return previous with { ChargeCountKnown = true };
        }

        if (previous.ChargeCountKnown &&
            previous.LastObservedCharges == currentCharges)
        {
            return previous;
        }

        var nextEpochToken = previous.CurrentEpochToken;
        if (currentCharges > 0 &&
            (!previous.ChargeCountKnown ||
             previous.LastObservedCharges != currentCharges))
        {
            nextEpochToken = NextEpochToken(previous.CurrentEpochToken);
        }

        return previous with
        {
            ChargeCountKnown = true,
            LastObservedCharges = currentCharges,
            CurrentEpochToken = nextEpochToken,
        };
    }

    public static bool TrySpendBaseChargeEpoch(
        AstrologianHarmonicOrbisBaseChargeEpochState state,
        ulong expectedEpochToken,
        out AstrologianHarmonicOrbisBaseChargeEpochState spentState)
    {
        spentState = state;
        if (!state.HasAvailableEpoch ||
            expectedEpochToken == 0 ||
            expectedEpochToken != state.CurrentEpochToken)
        {
            return false;
        }

        spentState = state with { SpentEpochToken = expectedEpochToken };
        return true;
    }

    public static NearHelpSelectionDecision SelectBestTarget(
        IReadOnlyList<NearHelpSelectionCandidate>? candidates,
        bool preferIncomingPressure = false,
        bool hasTrustedPressureView = false) =>
        NearHelpSelectionRules.SelectBestAtOrBelowHealthPercent(
            candidates,
            MaximumTargetHealthPercent,
            preferIncomingPressure,
            hasTrustedPressureView);

    public static int SelectBestTargetIndex(
        IReadOnlyList<NearHelpSelectionCandidate>? candidates,
        bool preferIncomingPressure = false,
        bool hasTrustedPressureView = false) =>
        SelectBestTarget(
            candidates,
            preferIncomingPressure,
            hasTrustedPressureView).SelectedIndex;

    public static bool TryCreateIntent(
        NearHelpSelectionCandidate selectedTarget,
        bool doubleCastWasReady,
        ulong baseChargeEpochToken,
        ulong orbisFrameworkFrame,
        out AstrologianHarmonicOrbisIntent intent)
    {
        intent = default;
        if (baseChargeEpochToken == 0 ||
            !NearHelpSelectionRules.IsAtOrBelowHealthPercent(
                selectedTarget,
                MaximumTargetHealthPercent))
        {
            return false;
        }

        intent = new AstrologianHarmonicOrbisIntent(
            new TargetPressureActorIdentity(
                selectedTarget.GameObjectId,
                selectedTarget.EntityId),
            selectedTarget.PartySlot,
            doubleCastWasReady,
            baseChargeEpochToken,
            orbisFrameworkFrame);
        return intent.IsValid;
    }

    public static AstrologianHarmonicOrbisFollowUpDecision EvaluateFollowUp(
        AstrologianHarmonicOrbisIntent intent,
        ClientActionAttemptOutcome orbisOutcome,
        ulong currentFrameworkFrame,
        TargetPressureActorIdentity currentTarget,
        bool targetStillEligible,
        uint resolvedDoubleCastActionId)
    {
        if (!intent.IsValid)
            return Cancelled(AstrologianHarmonicOrbisFollowUpReason.IntentInvalid);
        if (orbisOutcome != ClientActionAttemptOutcome.ClientAccepted)
            return Cancelled(AstrologianHarmonicOrbisFollowUpReason.OrbisNotAccepted);
        if (!intent.DoubleCastWasReady)
        {
            return new AstrologianHarmonicOrbisFollowUpDecision(
                AstrologianHarmonicOrbisFollowUpKind.Complete,
                AstrologianHarmonicOrbisFollowUpReason.DoubleCastWasUnavailable);
        }

        if (currentFrameworkFrame <= intent.OrbisFrameworkFrame)
        {
            return new AstrologianHarmonicOrbisFollowUpDecision(
                AstrologianHarmonicOrbisFollowUpKind.Waiting,
                AstrologianHarmonicOrbisFollowUpReason.LaterFrameworkFrameRequired);
        }

        if (currentTarget != intent.Target)
            return Cancelled(AstrologianHarmonicOrbisFollowUpReason.TargetChanged);
        if (!targetStillEligible)
            return Cancelled(AstrologianHarmonicOrbisFollowUpReason.TargetUnavailable);
        if (resolvedDoubleCastActionId == DoubleCastCarrierActionId)
        {
            return new AstrologianHarmonicOrbisFollowUpDecision(
                AstrologianHarmonicOrbisFollowUpKind.Waiting,
                AstrologianHarmonicOrbisFollowUpReason.CarrierNotAdjusted);
        }

        if (resolvedDoubleCastActionId != DoubleCastHarmonicOrbisActionId)
            return Cancelled(AstrologianHarmonicOrbisFollowUpReason.WrongAdjustedAction);

        return new AstrologianHarmonicOrbisFollowUpDecision(
            AstrologianHarmonicOrbisFollowUpKind.Dispatch,
            AstrologianHarmonicOrbisFollowUpReason.None,
            DoubleCastHarmonicOrbisActionId,
            intent.Target);
    }

    private static AstrologianHarmonicOrbisFollowUpDecision Cancelled(
        AstrologianHarmonicOrbisFollowUpReason reason) =>
        new(AstrologianHarmonicOrbisFollowUpKind.Cancelled, reason);

    private static ulong NextEpochToken(ulong current) =>
        current == ulong.MaxValue ? 1UL : current + 1UL;

    private static bool IsNetworkGameObjectId(ulong gameObjectId) =>
        gameObjectId is not 0 and not 0xE0000000 and not ulong.MaxValue;
}
