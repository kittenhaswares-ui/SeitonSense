namespace SeitonSense.Core;

public static class SeitonRangeRules
{
    public const uint Ready = 0;
    public const uint LineOfSightBlocked = 562;
    public const uint NotFacingTarget = 565;
    public const uint OutOfRange = 566;

    public static bool HasNativeRangeAndLineOfSight(uint resultCode) =>
        resultCode is Ready or NotFacingTarget;
}
