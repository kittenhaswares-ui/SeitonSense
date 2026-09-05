using Dalamud.Game;
using SeitonSense.Plugin.Services;

internal static class GuardianCommunicationSelfTests
{
    internal static void GuardianQuickChatKeepsReviewedLocalizedCommands()
    {
        // German intentionally places its target first, as documented at
        // https://de.finalfantasyxiv.com/lodestone/playguide/db/text_command/bf935708127/
        // Its complete multiword name must remain a single quoted argument.
        for (var slot = 1; slot <= 8; slot++)
        {
            var german = ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
                new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianCoveringTarget, slot, ClientLanguage.German));
            var english = ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
                new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianCoveringTarget, slot, ClientLanguage.English));
            var french = ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
                new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianCoveringTarget, slot, ClientLanguage.French));
            var japanese = ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
                new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianCoveringTarget, slot, ClientLanguage.Japanese));
            Require(german == $"/schnellchat <{slot}> \"Ziel decken\"", "German command keeps target first and groups the full localized name.");
            Require(english == $"/quickchat \"Covering Target\" <{slot}>", "English command keeps its own syntax.");
            Require(french == $"/quickchat \"Soutien : cible\" <{slot}>", "French command uses the same frozen party slot.");
            Require(japanese == $"/quickchat \"援護：ターゲット\" <{slot}>", "Japanese command uses the same frozen party slot.");
        }
        foreach (var invalidSlot in new[] { -1, 0, 9, int.MaxValue })
        {
            Require(ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
                new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianCoveringTarget, invalidSlot, ClientLanguage.German)) is null,
                "Unreviewed party slots cannot cross the command boundary.");
        }
        Require(ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
            new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianCoveringTarget, 2, (ClientLanguage)99)) is null,
            "An unknown language must not fall back to a possibly wrong shoutout.");
    }

    internal static void GuardianQuickChatRetriesOnlyBeforeNativeInvocation()
    {
        Require(ReviewedPvpCommandDispatcher.ClassifyUnavailableShell(true, false) ==
                ReviewedPvpCommandDispatchResult.TextCommandUnavailableBeforeInvocation,
            "A missing shell before invocation is safe for the bounded same-episode retry.");
        Require(ReviewedPvpCommandDispatcher.ClassifyUnavailableShell(true, true) ==
                ReviewedPvpCommandDispatchResult.NativeUnavailable,
            "Unknown delivery after entering native code never permits a duplicate shoutout.");
        Require(ReviewedPvpCommandDispatcher.ClassifyUnavailableShell(false, false) ==
                ReviewedPvpCommandDispatchResult.NativeUnavailable,
            "Guardian-only retry handling must not change marker command behavior.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
