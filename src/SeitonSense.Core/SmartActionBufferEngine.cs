namespace SeitonSense.Core;

/// <summary>
/// Thread-safe, one-shot state machine for an exact action rejected by a
/// transient local timing gate. It never selects a target, retries a server
/// rejection, or dispatches the same intent twice.
/// </summary>
public sealed class SmartActionBufferEngine
{
    private readonly object gate = new();
    private ArmedIntent? pending;

    public SmartActionBufferIntent? Pending
    {
        get
        {
            lock (gate)
            {
                return pending?.Intent;
            }
        }
    }

    public SmartActionBufferCancelReason LastCancelReason { get; private set; }

    public bool Arm(
        SmartActionBufferIntent intent,
        long originalAttemptAtMilliseconds,
        int holdMilliseconds = SmartActionBufferWindowRules.DefaultMilliseconds)
    {
        ValidateIntent(intent);

        lock (gate)
        {
            if (pending is not null)
            {
                pending = null;
                LastCancelReason = SmartActionBufferCancelReason.Replaced;
            }

            if (!intent.IsEligibleForBuffering)
            {
                LastCancelReason = SmartActionBufferCancelReason.Ineligible;
                return false;
            }

            if (intent.OriginalFailure == SmartActionBufferFailure.ServerRejected)
            {
                LastCancelReason = SmartActionBufferCancelReason.ServerRejected;
                return false;
            }

            if (intent.OriginalFailure is not SmartActionBufferFailure.GlobalCooldown
                and not SmartActionBufferFailure.AnimationLock
                and not SmartActionBufferFailure.Cooldown)
            {
                LastCancelReason = SmartActionBufferCancelReason.NonTransientFailure;
                return false;
            }

            var normalizedHold = SmartActionBufferWindowRules.Normalize(holdMilliseconds);
            pending = new ArmedIntent(
                intent,
                SaturatingAdd(originalAttemptAtMilliseconds, normalizedHold));
            return true;
        }
    }

    public void Cancel(SmartActionBufferCancelReason reason)
    {
        if (reason == SmartActionBufferCancelReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        lock (gate)
        {
            pending = null;
            LastCancelReason = reason;
        }
    }

    public SmartActionBufferDecision Evaluate(
        SmartActionBufferContext context,
        long nowMilliseconds)
    {
        lock (gate)
        {
            if (pending is not { } current)
            {
                return SmartActionBufferDecision.None;
            }

            var cancellation = GetCancellationReason(current.Intent.Action, context.Safety);
            if (cancellation != SmartActionBufferCancelReason.None)
            {
                pending = null;
                LastCancelReason = cancellation;
                return new SmartActionBufferDecision(
                    SmartActionBufferDecisionKind.Cancelled,
                    null,
                    cancellation);
            }

            // Expiry wins at the exact deadline, including while dispatch is paused.
            if (nowMilliseconds >= current.ExpiresAtMilliseconds)
            {
                pending = null;
                LastCancelReason = SmartActionBufferCancelReason.Expired;
                return new SmartActionBufferDecision(
                    SmartActionBufferDecisionKind.Expired,
                    null,
                    SmartActionBufferCancelReason.Expired);
            }

            if (context.IsFinalDispatchPaused || !context.ActionIsExecutable)
            {
                return SmartActionBufferDecision.None;
            }

            // Clear before returning so a re-entrant caller cannot dispatch twice.
            var intent = current.Intent;
            pending = null;
            LastCancelReason = SmartActionBufferCancelReason.Dispatched;
            return new SmartActionBufferDecision(
                SmartActionBufferDecisionKind.Dispatch,
                intent,
                SmartActionBufferCancelReason.Dispatched);
        }
    }

    private static SmartActionBufferCancelReason GetCancellationReason(
        SmartActionBufferAction action,
        SmartActionBufferSafety safety)
    {
        if (!safety.Enabled)
        {
            return SmartActionBufferCancelReason.Disabled;
        }

        if (safety.ConflictDetected)
        {
            return SmartActionBufferCancelReason.Conflict;
        }

        if (!safety.LoggedIn)
        {
            return SmartActionBufferCancelReason.Logout;
        }

        if (!safety.IsAlive)
        {
            return SmartActionBufferCancelReason.Death;
        }

        if (safety.IsMounted)
        {
            return SmartActionBufferCancelReason.Mounted;
        }

        if (safety.IsStunned)
        {
            return SmartActionBufferCancelReason.Stun;
        }

        if (safety.IsKnockbackActive)
        {
            return SmartActionBufferCancelReason.Knockback;
        }

        if (safety.TerritoryId != action.TerritoryId)
        {
            return SmartActionBufferCancelReason.TerritoryChange;
        }

        if (safety.InstanceId != action.InstanceId)
        {
            return SmartActionBufferCancelReason.InstanceChange;
        }

        if (safety.TargetId != action.TargetId)
        {
            return SmartActionBufferCancelReason.TargetChange;
        }

        if (safety.RequestedActionId != action.RequestedActionId)
        {
            return SmartActionBufferCancelReason.RequestedActionChange;
        }

        if (safety.ResolvedActionId != action.ResolvedActionId)
        {
            return SmartActionBufferCancelReason.ResolvedActionChange;
        }

        return SmartActionBufferCancelReason.None;
    }

    private static void ValidateIntent(SmartActionBufferIntent intent)
    {
        if (intent.Action.RequestedActionId == 0 || intent.Action.ResolvedActionId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(intent));
        }
    }

    private static long SaturatingAdd(long value, int delta)
    {
        if (value > long.MaxValue - delta)
        {
            return long.MaxValue;
        }

        return value + delta;
    }

    private sealed record ArmedIntent(
        SmartActionBufferIntent Intent,
        long ExpiresAtMilliseconds);
}
