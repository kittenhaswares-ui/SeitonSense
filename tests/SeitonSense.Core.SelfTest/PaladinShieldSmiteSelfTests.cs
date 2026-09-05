using System.Numerics;
using SeitonSense.Core;

internal static class PaladinShieldSmiteSelfTests
{
    internal static void AutomaticDenBoundarySelectsCorrectProtectionPolicy()
    {
        Check(AutomaticSmartActionWolvesDenRules.Get(41430) ==
              AutomaticWolvesDenProtectionPolicy.GuardBreakDamage, "Shield Smite reaches Den damage protection proof");
        Check(AutomaticSmartActionWolvesDenRules.Get(BardRepellingShotRules.RepellingShotActionId) ==
              AutomaticWolvesDenProtectionPolicy.CrowdControlUtility, "BRD alone retains CC immunity proof");
        foreach (var unreviewed in new uint[] { 0, 16, 29065, 29716, uint.MaxValue })
            Check(AutomaticSmartActionWolvesDenRules.Get(unreviewed) == AutomaticWolvesDenProtectionPolicy.None,
                "unreviewed automatic actions cannot inherit Den permission");
    }

    internal static void WeakenedGuardAllowsDamageButRetainsCrowdControlImmunity()
    {
        var catalog = SmartActionProtectionStatusCatalog.Create(
        [
            new(3054, SmartActionProtectionStatusSemantic.Guard),
            new(3673, SmartActionProtectionStatusSemantic.Guard),
            new(2413, SmartActionProtectionStatusSemantic.Covered),
            new(1302, SmartActionProtectionStatusSemantic.HallowedGround),
            new(3039, SmartActionProtectionStatusSemantic.UndeadRedemption),
        ]);
        Check(catalog.Classify(3673) == SmartActionProtectionKind.Guard,
            "without exact weakened-status metadata the old safe behavior is retained");
        Check(catalog.WithVerifiedWeakenedGuard(3054).Classify(3054) == SmartActionProtectionKind.Guard,
            "full Guard cannot be accidentally marked weakened");
        catalog = catalog.WithVerifiedWeakenedGuard(3673);
        Check(catalog.IsWeakenedGuardStatus(3673) && catalog.Classify(3673) == SmartActionProtectionKind.None,
            "verified weakened Guard no longer blocks damage selection");
        Check(catalog.Classify(3054) == SmartActionProtectionKind.Guard,
            "normal Guard remains a damage blocker");
        Check(CcImmunityBrakeActionCatalog.IsBlockerStatus(
                CcImmunityBrakeBlockerFamily.StandardPurifyCc, 3673, 19),
            "weakened Guard still prevents stun/silence");
        var target = new SmartActionActorGeometry(1, new(100, 200), true, Vector3.Zero, 0.5f);
        foreach (var shape in Enum.GetValues<SmartActionAttackShape>())
        {
            var radius = shape == SmartActionAttackShape.DirectSingleTarget ? 0 : 8;
            Check(SmartActionProtectionRules.IsActionProtectionSafe(shape, target, radius, []),
                "weak Guard alone permits direct and area damage");
            foreach (var other in new[] { SmartActionProtectionKind.Guard, SmartActionProtectionKind.Chiten,
                         SmartActionProtectionKind.Covered, SmartActionProtectionKind.Invulnerability })
                Check(!SmartActionProtectionRules.IsActionProtectionSafe(shape, target, radius, [new(target, other)]),
                    $"weak Guard cannot remove a separate {other} protection");
        }
    }

    internal static void KeylessShieldSmiteRequiresFullGuardAndPreservesOwnGuard()
    {
        Check(PaladinShieldSmiteRules.ActionId == 41430, "exact PvP Shield Smite, not PvE Shield Bash");
        Check(PaladinShieldSmiteRules.CanSelectTarget(true, true, true, true), "full Guard target allowed");
        Check(!PaladinShieldSmiteRules.CanSelectTarget(true, false, true, true), "unguarded or already-weakened target skipped");
        Check(!PaladinShieldSmiteRules.CanSelectTarget(true, true, false, true), "SmartAction protection is retained");
        Check(!PaladinShieldSmiteRules.CanSelectTarget(true, true, true, false), "native range and LoS required");
        Check(Dispatch(), "automatic helper needs no held-key observation");
        Check(Dispatch(context: SupportedPvPContext.WolvesDen), "exact duel target can use same automatic path");
        Check(!Dispatch(guard: true), "own Guard prevents automated Shield Smite");
        Check(!Dispatch(higher: true), "survival helper keeps priority");
        Check(!Dispatch(nativeReady: false), "casts and native locks wait without cast cancellation");
        Check(!Dispatch(context: SupportedPvPContext.None), "no automatic action outside supported PvP");

        static bool Dispatch(bool guard = false, bool higher = false, bool nativeReady = true,
            SupportedPvPContext context = SupportedPvPContext.CrystallineConflict) =>
            PaladinShieldSmiteRules.CanDispatch(true, context, 19, true, true, guard, false,
                higher, true, nativeReady, true);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
