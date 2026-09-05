using Dalamud.Game;
using SeitonSense.Core;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

internal static class GuardianCommunicationSelfTests
{
    internal static void GuardianQuickChatKeepsReviewedLocalizedCommands()
    {
        // These are command-construction tests, not live parser or delivery
        // proof. All four languages use one action-first command shape.
        var localizedNames = new[]
        {
            (Language: ClientLanguage.English, Name: "Covering Target"),
            (Language: ClientLanguage.German, Name: "Ziel decken"),
            (Language: ClientLanguage.French, Name: "Soutien : cible"),
            (Language: ClientLanguage.Japanese, Name: "援護：ターゲット"),
        };
        var uniqueCommands = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (language, name) in localizedNames)
        {
            for (var slot = 1; slot <= 8; slot++)
            {
                var command = GuardianQuickChatCommand.Build(language, slot)
                    ?? throw new InvalidOperationException("A reviewed language/slot tuple must construct one command.");
                Require(command == $"/quickchat \"{name}\" <{slot}>",
                    "The complete localized name precedes the exact frozen party slot.");
                Require(ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
                    new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianCoveringTarget, slot, language)) == command,
                    "The dispatcher must use the isolated Guardian command builder.");
                Require(command.StartsWith("/quickchat ", StringComparison.Ordinal) &&
                        command.Count(character => character == '/') == 1 &&
                        command.Count(character => character == '"') == 2 &&
                        command.Count(character => character == '<') == 1 &&
                        command.Count(character => character == '>') == 1 &&
                        !command.Contains('\r') && !command.Contains('\n'),
                    "Exactly one slash command, quoted action and bounded target may cross the chat-entry boundary.");
                Require(!command.Contains("<t>", StringComparison.Ordinal) &&
                        !command.Contains("/schnellchat", StringComparison.Ordinal),
                    "No visible-target placeholder or German-only command branch may remain.");
                Require(uniqueCommands.Add(command),
                    "Each reviewed language/slot tuple produces its own single command, not a duplicate tuple.");
            }
            foreach (var invalidSlot in new[] { int.MinValue, -1, 0, 9, int.MaxValue })
            {
                Require(GuardianQuickChatCommand.Build(language, invalidSlot) is null &&
                        ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
                            new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianCoveringTarget, invalidSlot, language)) is null,
                    "Unreviewed party slots cannot cross the command boundary in any language.");
            }
        }
        Require(uniqueCommands.Count == 32, "All four languages and eight party slots must be covered.");
        Require(GuardianQuickChatCommand.FormatLabel == "action-first-v2",
            "Diagnostics must identify the rewritten command format.");
        foreach (var invalidLanguage in new[] { unchecked((ClientLanguage)(-1)), (ClientLanguage)4, (ClientLanguage)99 })
        {
            for (var slot = 1; slot <= 8; slot++)
            {
                Require(GuardianQuickChatCommand.Build(invalidLanguage, slot) is null &&
                        ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
                            new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianCoveringTarget, slot, invalidLanguage)) is null,
                    "An unknown language must not fall back to a possibly wrong shoutout.");
            }
        }
        Require(ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
            new ReviewedPvpCommand((ReviewedPvpCommandKind)99, 2, ClientLanguage.German)) is null,
            "An unknown command kind cannot enter the Guardian builder.");

        for (var slot = 1; slot <= 8; slot++)
        {
            Require(ReviewedPvpCommandDispatcher.ResolveExactHardcodedCommand(
                new ReviewedPvpCommand(ReviewedPvpCommandKind.GuardianBind2Party, slot)) == $"/mk bind2 <{slot}>",
                "Guardian marker commands remain independent of Quick Chat formatting.");
        }
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

        ExplicitMacroCommunicationRequiresItsExactAcceptedEpisode();
    }

    private static void ExplicitMacroCommunicationRequiresItsExactAcceptedEpisode()
    {
        var configuration = new PluginConfiguration
        {
            Enabled = true,
            PaladinGuardianLowAlly = false,
            PaladinGuardianAnnounceAndMark = true,
        };
        var accepted = new AcceptedAutoGuardianEpisode(
            3, 1_000, new(100, 10), new(200, 20), 2)
        {
            IsExplicitMacro = true,
        };
        var episode = new GuardianTeamCommunicationEpisode(
            accepted.Token,
            accepted.AcceptedAtMilliseconds,
            accepted.LocalPlayer,
            accepted.Target,
            accepted.PartySlot);

        bool Authorized(
            GuardianTeamCommunicationEpisode? current,
            AcceptedAutoGuardianEpisode? source,
            bool metadataMatches = true) =>
            GuardianCommunicationService.IsCommunicationConfiguredForEpisode(
                configuration, metadataMatches, current, source);

        Require(Authorized(episode, accepted),
            "One exact accepted explicit macro may communicate with automatic Guardian disabled.");
        Require(!Authorized(episode, accepted with { IsExplicitMacro = false }),
            "An automatic episode cannot inherit explicit macro consent.");
        Require(!Authorized(episode, null) && !Authorized(null, accepted),
            "Missing or reset episode state cannot retain explicit consent.");
        Require(!Authorized(episode, accepted with { Token = 0 }),
            "Invalid accepted events cannot authorize communication.");
        Require(!Authorized(episode, accepted with { Token = 4 }) &&
                !Authorized(episode with { Token = 4 }, accepted),
            "A newer or older episode must not inherit another token's consent.");
        Require(!Authorized(episode, accepted with { AcceptedAtMilliseconds = 1_001 }),
            "The acceptance timestamp is part of the exact source event.");
        Require(!Authorized(episode, accepted with { PartySlot = 3 }),
            "A reused party slot cannot redirect the explicit communication.");
        foreach (var changed in new[]
        {
            accepted with { LocalPlayer = new(101, 10) },
            accepted with { LocalPlayer = new(100, 11) },
            accepted with { Target = new(201, 20) },
            accepted with { Target = new(200, 21) },
        })
        {
            Require(!Authorized(episode, changed),
                "Both exact local and protected-ally identity fields must match.");
        }
        Require(!Authorized(episode, accepted, metadataMatches: false),
            "Explicit consent cannot bypass metadata or localization validation.");
        configuration.Enabled = false;
        Require(!Authorized(episode, accepted),
            "The global plugin switch still disables explicit communication.");
        configuration.Enabled = true;
        configuration.PaladinGuardianAnnounceAndMark = false;
        Require(!Authorized(episode, accepted),
            "The shoutout/marker preference still disables explicit communication.");
        configuration.PaladinGuardianAnnounceAndMark = true;
        configuration.PaladinGuardianLowAlly = true;
        Require(Authorized(episode, accepted with { IsExplicitMacro = false }),
            "Configured automatic episodes keep their existing communication behavior.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
