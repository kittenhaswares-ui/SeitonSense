namespace SeitonSense.Core;

public enum AutomaticWolvesDenProtectionPolicy : byte
{
    None,
    CrowdControlUtility,
    GuardBreakDamage,
}

public static class AutomaticSmartActionWolvesDenRules
{
    public static AutomaticWolvesDenProtectionPolicy Get(uint actionId) => actionId switch
    {
        BardRepellingShotRules.RepellingShotActionId => AutomaticWolvesDenProtectionPolicy.CrowdControlUtility,
        PaladinShieldSmiteRules.ActionId => AutomaticWolvesDenProtectionPolicy.GuardBreakDamage,
        _ => AutomaticWolvesDenProtectionPolicy.None,
    };
}
