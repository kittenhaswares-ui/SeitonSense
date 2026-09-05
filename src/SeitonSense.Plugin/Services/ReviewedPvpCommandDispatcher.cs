using Dalamud.Game;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace SeitonSense.Plugin.Services;

/// <summary>
/// Closed set of reviewed, party-visible PvP shell commands. Callers can only
/// select a reviewed command kind plus a bounded native party/enemy slot; no
/// arbitrary command text crosses the native shell boundary.
/// </summary>
internal enum ReviewedPvpCommandKind
{
    Attack1Enemy,
    ClearAttack1Enemy,
    GuardianCoveringTarget,
    GuardianBind1Self,
    GuardianBind2Party,
    GuardianClearBind1,
    GuardianClearBind2,
}

internal enum ReviewedPvpCommandDispatchResult
{
    Invoked,
    MarkerRateLimited,
    TextCommandUnavailableBeforeInvocation,
    InvalidCommand,
    NativeUnavailable,
}

internal readonly record struct ReviewedPvpCommand(
    ReviewedPvpCommandKind Kind,
    int Slot = 0,
    ClientLanguage Language = ClientLanguage.English);

internal sealed class ReviewedPvpCommandDispatcher
{
    internal const long MinimumMarkerCommandIntervalMilliseconds = 100;

    private readonly object markerReservationGate = new();
    private long lastMarkerReservationAt = -MinimumMarkerCommandIntervalMilliseconds;

    internal ReviewedPvpCommandDispatchResult TryMarkAttack1(int enemySlot, long nowMilliseconds) =>
        TryExecuteMarker(
            new ReviewedPvpCommand(ReviewedPvpCommandKind.Attack1Enemy, enemySlot),
            nowMilliseconds);

    internal ReviewedPvpCommandDispatchResult TryClearAttack1(int enemySlot, long nowMilliseconds) =>
        TryExecuteMarker(
            new ReviewedPvpCommand(ReviewedPvpCommandKind.ClearAttack1Enemy, enemySlot),
            nowMilliseconds);

    internal ReviewedPvpCommandDispatchResult TryQuickChatCoveringTarget(
        ClientLanguage language,
        int partySlot) =>
        TryExecuteUnreserved(new ReviewedPvpCommand(
            ReviewedPvpCommandKind.GuardianCoveringTarget,
            partySlot,
            language));

    internal ReviewedPvpCommandDispatchResult TryMarkGuardianSelf(long nowMilliseconds) =>
        TryExecuteMarker(
            new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianBind1Self),
            nowMilliseconds);

    internal ReviewedPvpCommandDispatchResult TryMarkGuardianAlly(
        int partySlot,
        long nowMilliseconds) =>
        TryExecuteMarker(
            new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianBind2Party, partySlot),
            nowMilliseconds);

    internal ReviewedPvpCommandDispatchResult TryClearGuardianSelf(long nowMilliseconds) =>
        TryExecuteMarker(
            new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianClearBind1),
            nowMilliseconds);

    internal ReviewedPvpCommandDispatchResult TryClearGuardianAlly(long nowMilliseconds) =>
        TryExecuteMarker(
            new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianClearBind2),
            nowMilliseconds);

    internal bool CanIssueMarker(long nowMilliseconds)
    {
        if (nowMilliseconds < 0) return false;

        lock (markerReservationGate)
        {
            NormalizeMarkerClock(nowMilliseconds);
            return MarkerIntervalElapsed(nowMilliseconds);
        }
    }

    private ReviewedPvpCommandDispatchResult TryExecuteMarker(
        ReviewedPvpCommand command,
        long nowMilliseconds)
    {
        var exactHardcodedCommand = ResolveExactHardcodedCommand(command);
        if (exactHardcodedCommand is null || !IsMarkerCommand(command.Kind) || nowMilliseconds < 0)
            return ReviewedPvpCommandDispatchResult.InvalidCommand;

        lock (markerReservationGate)
        {
            NormalizeMarkerClock(nowMilliseconds);
            if (!MarkerIntervalElapsed(nowMilliseconds))
            {
                return ReviewedPvpCommandDispatchResult.MarkerRateLimited;
            }

            // Reserve before crossing the native boundary. Even an unavailable
            // boundary cannot allow a second marker command in the same frame.
            lastMarkerReservationAt = nowMilliseconds;
        }

        return TryExecuteShellCommand(exactHardcodedCommand);
    }

    private static ReviewedPvpCommandDispatchResult TryExecuteUnreserved(
        ReviewedPvpCommand command)
    {
        var exactHardcodedCommand = ResolveExactHardcodedCommand(command);
        if (exactHardcodedCommand is null || IsMarkerCommand(command.Kind))
            return ReviewedPvpCommandDispatchResult.InvalidCommand;

        return TryExecuteShellCommand(exactHardcodedCommand,
            guardianQuickChat: command.Kind == ReviewedPvpCommandKind.GuardianCoveringTarget);
    }

    private void NormalizeMarkerClock(long nowMilliseconds)
    {
        if (nowMilliseconds < lastMarkerReservationAt)
            lastMarkerReservationAt = nowMilliseconds - MinimumMarkerCommandIntervalMilliseconds;
    }

    private bool MarkerIntervalElapsed(long nowMilliseconds) =>
        lastMarkerReservationAt < 0 ||
        (nowMilliseconds >= lastMarkerReservationAt &&
         nowMilliseconds - lastMarkerReservationAt >=
         MinimumMarkerCommandIntervalMilliseconds);

    private static bool IsMarkerCommand(ReviewedPvpCommandKind kind) => kind is
        ReviewedPvpCommandKind.Attack1Enemy or
        ReviewedPvpCommandKind.ClearAttack1Enemy or
        ReviewedPvpCommandKind.GuardianBind1Self or
        ReviewedPvpCommandKind.GuardianBind2Party or
        ReviewedPvpCommandKind.GuardianClearBind1 or
        ReviewedPvpCommandKind.GuardianClearBind2;

    internal static string? ResolveExactHardcodedCommand(ReviewedPvpCommand command) => command switch
    {
        { Kind: ReviewedPvpCommandKind.Attack1Enemy, Slot: 1 } => "/mk attack1 <e1>",
        { Kind: ReviewedPvpCommandKind.Attack1Enemy, Slot: 2 } => "/mk attack1 <e2>",
        { Kind: ReviewedPvpCommandKind.Attack1Enemy, Slot: 3 } => "/mk attack1 <e3>",
        { Kind: ReviewedPvpCommandKind.Attack1Enemy, Slot: 4 } => "/mk attack1 <e4>",
        { Kind: ReviewedPvpCommandKind.Attack1Enemy, Slot: 5 } => "/mk attack1 <e5>",
        { Kind: ReviewedPvpCommandKind.ClearAttack1Enemy, Slot: 1 } => "/mk off <e1>",
        { Kind: ReviewedPvpCommandKind.ClearAttack1Enemy, Slot: 2 } => "/mk off <e2>",
        { Kind: ReviewedPvpCommandKind.ClearAttack1Enemy, Slot: 3 } => "/mk off <e3>",
        { Kind: ReviewedPvpCommandKind.ClearAttack1Enemy, Slot: 4 } => "/mk off <e4>",
        { Kind: ReviewedPvpCommandKind.ClearAttack1Enemy, Slot: 5 } => "/mk off <e5>",

        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.English, Slot: 1 } => "/quickchat \"Covering Target\" <1>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.English, Slot: 2 } => "/quickchat \"Covering Target\" <2>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.English, Slot: 3 } => "/quickchat \"Covering Target\" <3>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.English, Slot: 4 } => "/quickchat \"Covering Target\" <4>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.English, Slot: 5 } => "/quickchat \"Covering Target\" <5>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.English, Slot: 6 } => "/quickchat \"Covering Target\" <6>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.English, Slot: 7 } => "/quickchat \"Covering Target\" <7>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.English, Slot: 8 } => "/quickchat \"Covering Target\" <8>",
        // Keep the documented German target-first order, but group the full
        // localized message name into one argument just like the other locales.
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.German, Slot: 1 } => "/schnellchat <1> \"Ziel decken\"",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.German, Slot: 2 } => "/schnellchat <2> \"Ziel decken\"",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.German, Slot: 3 } => "/schnellchat <3> \"Ziel decken\"",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.German, Slot: 4 } => "/schnellchat <4> \"Ziel decken\"",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.German, Slot: 5 } => "/schnellchat <5> \"Ziel decken\"",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.German, Slot: 6 } => "/schnellchat <6> \"Ziel decken\"",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.German, Slot: 7 } => "/schnellchat <7> \"Ziel decken\"",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.German, Slot: 8 } => "/schnellchat <8> \"Ziel decken\"",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.French, Slot: 1 } => "/quickchat \"Soutien : cible\" <1>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.French, Slot: 2 } => "/quickchat \"Soutien : cible\" <2>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.French, Slot: 3 } => "/quickchat \"Soutien : cible\" <3>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.French, Slot: 4 } => "/quickchat \"Soutien : cible\" <4>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.French, Slot: 5 } => "/quickchat \"Soutien : cible\" <5>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.French, Slot: 6 } => "/quickchat \"Soutien : cible\" <6>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.French, Slot: 7 } => "/quickchat \"Soutien : cible\" <7>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.French, Slot: 8 } => "/quickchat \"Soutien : cible\" <8>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.Japanese, Slot: 1 } => "/quickchat \"援護：ターゲット\" <1>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.Japanese, Slot: 2 } => "/quickchat \"援護：ターゲット\" <2>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.Japanese, Slot: 3 } => "/quickchat \"援護：ターゲット\" <3>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.Japanese, Slot: 4 } => "/quickchat \"援護：ターゲット\" <4>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.Japanese, Slot: 5 } => "/quickchat \"援護：ターゲット\" <5>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.Japanese, Slot: 6 } => "/quickchat \"援護：ターゲット\" <6>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.Japanese, Slot: 7 } => "/quickchat \"援護：ターゲット\" <7>",
        { Kind: ReviewedPvpCommandKind.GuardianCoveringTarget, Language: ClientLanguage.Japanese, Slot: 8 } => "/quickchat \"援護：ターゲット\" <8>",

        { Kind: ReviewedPvpCommandKind.GuardianBind1Self } => "/mk bind1 <me>",
        { Kind: ReviewedPvpCommandKind.GuardianBind2Party, Slot: 1 } => "/mk bind2 <1>",
        { Kind: ReviewedPvpCommandKind.GuardianBind2Party, Slot: 2 } => "/mk bind2 <2>",
        { Kind: ReviewedPvpCommandKind.GuardianBind2Party, Slot: 3 } => "/mk bind2 <3>",
        { Kind: ReviewedPvpCommandKind.GuardianBind2Party, Slot: 4 } => "/mk bind2 <4>",
        { Kind: ReviewedPvpCommandKind.GuardianBind2Party, Slot: 5 } => "/mk bind2 <5>",
        { Kind: ReviewedPvpCommandKind.GuardianBind2Party, Slot: 6 } => "/mk bind2 <6>",
        { Kind: ReviewedPvpCommandKind.GuardianBind2Party, Slot: 7 } => "/mk bind2 <7>",
        { Kind: ReviewedPvpCommandKind.GuardianBind2Party, Slot: 8 } => "/mk bind2 <8>",
        { Kind: ReviewedPvpCommandKind.GuardianClearBind1 } => "/mk off <bind1>",
        { Kind: ReviewedPvpCommandKind.GuardianClearBind2 } => "/mk off <bind2>",
        _ => null,
    };

    internal static ReviewedPvpCommandDispatchResult ClassifyUnavailableShell(
        bool guardianQuickChat, bool invocationStarted) =>
        guardianQuickChat && !invocationStarted
            ? ReviewedPvpCommandDispatchResult.TextCommandUnavailableBeforeInvocation
            : ReviewedPvpCommandDispatchResult.NativeUnavailable;

    private static unsafe ReviewedPvpCommandDispatchResult TryExecuteShellCommand(
        string exactHardcodedCommand, bool guardianQuickChat = false)
    {
        Utf8String* command = null;
        var invocationStarted = false;
        try
        {
            var uiModule = UIModule.Instance();
            if (uiModule == null) return ClassifyUnavailableShell(guardianQuickChat, invocationStarted);
            var shell = uiModule->GetRaptureShellModule();
            if (shell == null) return ClassifyUnavailableShell(guardianQuickChat, invocationStarted);

            // ExecuteCommandInner has no result value. This native flag is the
            // only exact pre-invocation proof that the shell cannot accept a
            // text command right now; do not misreport that case as Invoked.
            if (shell->IsTextCommandUnavailable)
            {
                return ReviewedPvpCommandDispatchResult.TextCommandUnavailableBeforeInvocation;
            }

            command = Utf8String.FromString(exactHardcodedCommand);
            if (command == null) return ClassifyUnavailableShell(guardianQuickChat, invocationStarted);
            // Exceptions after this point have unknown delivery. Never retry
            // those; only positively pre-invocation failures may be re-offered.
            invocationStarted = true;
            if (guardianQuickChat)
            {
                // Quick Chat is a chat entry, not a marker shell command. Let
                // the normal chat-entry path parse the localized action name
                // and party placeholder. The inner shell path can return
                // without producing a team message. Never try both paths.
                uiModule->ProcessChatBoxEntry(command, 0, false);
            }
            else
            {
                shell->ExecuteCommandInner(command, uiModule);
            }
            return ReviewedPvpCommandDispatchResult.Invoked;
        }
        catch
        {
            return ClassifyUnavailableShell(guardianQuickChat, invocationStarted);
        }
        finally
        {
            if (command != null) command->Dtor(true);
        }
    }
}
