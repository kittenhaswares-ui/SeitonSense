namespace SeitonSense.Core;

public readonly record struct SmartActionSafetyLeaseToken(
    uint TerritoryId,
    TargetPressureActorIdentity LocalPlayer,
    uint RawActionType,
    uint RawActionId,
    uint ResolvedActionId,
    long ArmedAtMilliseconds,
    long ExpiresAtMilliseconds)
{
    public bool IsValid =>
        TerritoryId != 0 &&
        LocalPlayer.IsValid &&
        RawActionType != 0 &&
        RawActionId != 0 &&
        ArmedAtMilliseconds >= 0 &&
        ExpiresAtMilliseconds > ArmedAtMilliseconds;
}

public readonly record struct SmartActionSafetyLeaseState(
    SmartActionSafetyLeaseToken? Token)
{
    public static SmartActionSafetyLeaseState Initial => new(null);

    public bool IsArmed => Token is { IsValid: true };
}

public enum SmartActionSafetyLeaseDecisionKind : byte
{
    PassThrough = 0,
    Waiting = 1,
    InspectExactAction = 2,
    RejectExactActionDrift = 3,
    Cleared = 4,
}

public readonly record struct SmartActionSafetyLeaseDecision(
    SmartActionSafetyLeaseState NextState,
    SmartActionSafetyLeaseDecisionKind Kind)
{
    public bool ShouldInspect => Kind == SmartActionSafetyLeaseDecisionKind.InspectExactAction;
    public bool ShouldRejectDrift =>
        Kind == SmartActionSafetyLeaseDecisionKind.RejectExactActionDrift;
}

/// <summary>
/// Keeps the claimed harmful action from one consumed Smart Action macro under
/// protection inspection until the client accepts it or the bounded post-claim
/// lease ends. If semantic action resolution is unavailable, the same short
/// lease blocks supported action carriers because aliases cannot be disproved.
/// </summary>
public static class SmartActionSafetyLeaseRules
{
    public const long DefaultLifetimeMilliseconds = 750;

    public static bool IsCurrent(
        SmartActionSafetyLeaseState state,
        uint territoryId,
        TargetPressureActorIdentity localPlayer,
        long nowMilliseconds) =>
        state.Token is { } token &&
        token.IsValid &&
        nowMilliseconds >= token.ArmedAtMilliseconds &&
        nowMilliseconds < token.ExpiresAtMilliseconds &&
        territoryId == token.TerritoryId &&
        localPlayer == token.LocalPlayer;

    public static SmartActionSafetyLeaseState Arm(
        uint territoryId,
        TargetPressureActorIdentity localPlayer,
        uint rawActionType,
        uint rawActionId,
        uint resolvedActionId,
        long nowMilliseconds,
        long expiresAtMilliseconds)
    {
        var token = new SmartActionSafetyLeaseToken(
            territoryId,
            localPlayer,
            rawActionType,
            rawActionId,
            resolvedActionId,
            nowMilliseconds,
            expiresAtMilliseconds);
        return token.IsValid
            ? new SmartActionSafetyLeaseState(token)
            : SmartActionSafetyLeaseState.Initial;
    }

    public static SmartActionSafetyLeaseDecision Observe(
        SmartActionSafetyLeaseState previous,
        uint territoryId,
        TargetPressureActorIdentity localPlayer,
        uint rawActionType,
        uint rawActionId,
        uint resolvedActionId,
        long nowMilliseconds)
    {
        if (previous.Token is not { } token)
            return PassThrough();
        if (!IsCurrent(previous, territoryId, localPlayer, nowMilliseconds))
        {
            return Cleared();
        }

        var rawActionMatches =
            rawActionType == token.RawActionType &&
            rawActionId == token.RawActionId;
        if (token.ResolvedActionId == 0 || resolvedActionId == 0)
        {
            // Resolution loss means an Action/PvPAction alias cannot be proven
            // unrelated. Keep the short lease closed instead of opening an
            // unresolvable carrier hole.
            return new SmartActionSafetyLeaseDecision(
                previous,
                SmartActionSafetyLeaseDecisionKind.RejectExactActionDrift);
        }

        if (token.ResolvedActionId != 0 &&
            resolvedActionId == token.ResolvedActionId)
        {
            // Action and PvPAction carriers may represent the same exact
            // resolved PvP skill. The protection lease follows that semantic
            // action instead of opening a raw-carrier alias hole.
            return new SmartActionSafetyLeaseDecision(
                previous,
                SmartActionSafetyLeaseDecisionKind.InspectExactAction);
        }

        if (!rawActionMatches)
        {
            return new SmartActionSafetyLeaseDecision(
                previous,
                SmartActionSafetyLeaseDecisionKind.Waiting);
        }

        return new SmartActionSafetyLeaseDecision(
            previous,
            SmartActionSafetyLeaseDecisionKind.RejectExactActionDrift);
    }

    private static SmartActionSafetyLeaseDecision PassThrough() =>
        new(
            SmartActionSafetyLeaseState.Initial,
            SmartActionSafetyLeaseDecisionKind.PassThrough);

    private static SmartActionSafetyLeaseDecision Cleared() =>
        new(
            SmartActionSafetyLeaseState.Initial,
            SmartActionSafetyLeaseDecisionKind.Cleared);
}
