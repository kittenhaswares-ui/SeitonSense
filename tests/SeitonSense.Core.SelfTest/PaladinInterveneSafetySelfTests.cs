using SeitonSense.Core;

internal static class PaladinInterveneSafetySelfTests
{
    internal static void GuardianLinkBelongsToLocalProtector()
    {
        const uint local = 100;
        const uint ally = 200;
        const uint otherPaladin = 300;
        Check(Link(local, local, isCover: true), "local Cover means we are protecting an ally");
        Check(Link(local, ally, isCover: true), "local Cover holder is authoritative even if source is target");
        Check(Link(ally, local, isCovered: true), "Covered on ally from us blocks Intervene");
        Check(!Link(ally, otherPaladin, isCovered: true), "another Paladin's Covered does not block us");
        Check(!Link(local, otherPaladin, isCovered: true), "receiving somebody else's Guardian is not our link");
        Check(!Link(otherPaladin, otherPaladin, isCover: true), "another Paladin's Cover is unrelated");
        Check(!Link(ally, local), "unrelated status from us is not Guardian");
        Check(!PaladinInterveneSafetyRules.IsOwnGuardianLink(0, ally, 0, false, true),
            "invalid identities cannot manufacture a source match");

        static bool Link(uint holder, uint source, bool isCover = false, bool isCovered = false) =>
            PaladinInterveneSafetyRules.IsOwnGuardianLink(local, holder, source, isCover, isCovered);
    }

    internal static void InterveneRequiresGuardDownAnd3000Mp()
    {
        const uint action = MiracleInterceptConfirmationRules.InterveneActionId;
        Check(Read(mp: 2_999) == PaladinInterveneBlockReason.LowMp, "2999 MP cannot trigger Intervene");
        Check(Read(mp: 3_000) == PaladinInterveneBlockReason.None, "exactly 3000 MP is allowed");
        Check(Read(guard: true) == PaladinInterveneBlockReason.OwnGuard,
            "active or accepted-propagating own Guard blocks Intervene");
        Check(Read(protecting: true) == PaladinInterveneBlockReason.ProtectingAlly,
            "Guardian link blocks Intervene despite sufficient resources");
        Check(PaladinInterveneSafetyRules.Evaluate(action, false, true, 10000, false, false) ==
              PaladinInterveneBlockReason.LocalPlayerUnavailable, "invalid local actor cannot jump");
        Check(PaladinInterveneSafetyRules.Evaluate(action, true, false, 10000, false, false) ==
              PaladinInterveneBlockReason.GuardianMetadataUnavailable, "unverified status data cannot jump");
        foreach (var other in new[]
                 {
                     MiracleInterceptConfirmationRules.MiracleOfNatureActionId,
                     MiracleInterceptConfirmationRules.SilentNocturneActionId,
                     MiracleInterceptConfirmationRules.ForkedRaijuActionId,
                 })
            Check(PaladinInterveneSafetyRules.Evaluate(other, false, false, 0, true, true) ==
                  PaladinInterveneBlockReason.None, "PLD-only limits do not change other helpers");

        static PaladinInterveneBlockReason Read(uint mp = 3000, bool guard = false, bool protecting = false) =>
            PaladinInterveneSafetyRules.Evaluate(action, true, true, mp, guard, protecting);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
