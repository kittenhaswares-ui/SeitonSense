namespace SeitonSense.Core;

/// <summary>
/// One read-only snapshot of FFXIV's normal gameplay camera. The numeric mode
/// values intentionally mirror the reviewed client structure without taking a
/// dependency on native camera types in the pure Core project.
/// </summary>
public readonly record struct BackwardPanicShukuchiCameraObservation(
    bool CameraManagerAvailable,
    bool NormalCameraAvailable,
    bool ActiveCameraMatchesNormal,
    int ActiveCameraIndex,
    int ControlMode,
    int ZoomMode,
    bool EventCameraAutoControl,
    float DirectionRadians);

/// <summary>
/// Pure camera admission and geometry for /seitonbw. It accepts only FFXIV's
/// normal first-/third-person gameplay camera and returns one exact point 19.5
/// yalms in the camera-relative screen-back direction. FFXIV's third-person
/// DirH is already the camera azimuth from the actor, while first-person DirH
/// is the view direction and therefore needs a half turn. It has no native
/// calls, clock, scheduler, target, cursor, retry, or fallback state.
/// </summary>
public static class BackwardPanicShukuchiRules
{
    public const int NormalGameplayCameraIndex = 0;
    public const int FirstPersonControlMode = 0;
    public const int ThirdPersonLegacyControlMode = 1;
    public const int ThirdPersonFixedControlMode = 2;
    public const int FirstPersonZoomMode = 0;
    public const int ThirdPersonZoomMode = 1;

    public static bool IsSupportedStandardCamera(
        BackwardPanicShukuchiCameraObservation observation)
    {
        if (!observation.CameraManagerAvailable ||
            !observation.NormalCameraAvailable ||
            !observation.ActiveCameraMatchesNormal ||
            observation.ActiveCameraIndex != NormalGameplayCameraIndex ||
            observation.EventCameraAutoControl ||
            !float.IsFinite(observation.DirectionRadians))
        {
            return false;
        }

        return observation.ControlMode switch
        {
            FirstPersonControlMode => observation.ZoomMode == FirstPersonZoomMode,
            ThirdPersonLegacyControlMode or ThirdPersonFixedControlMode =>
                observation.ZoomMode == ThirdPersonZoomMode,
            _ => false,
        };
    }

    public static bool TryCreateBackwardCameraProbe(
        PanicShukuchiPoint origin,
        BackwardPanicShukuchiCameraObservation observation,
        out float backwardRotationRadians,
        out PanicShukuchiPoint probe)
    {
        backwardRotationRadians = default;
        probe = default;
        if (!origin.IsFinite || !IsSupportedStandardCamera(observation)) return false;

        var rawBackward = observation.ControlMode == FirstPersonControlMode
            ? (double)observation.DirectionRadians + Math.PI
            : observation.DirectionRadians;
        var normalizedBackward = Math.IEEERemainder(rawBackward, Math.Tau);
        if (!double.IsFinite(normalizedBackward) ||
            normalizedBackward < float.MinValue ||
            normalizedBackward > float.MaxValue)
        {
            return false;
        }

        backwardRotationRadians = (float)normalizedBackward;
        return PanicShukuchiRules.TryCreateForwardProbe(
            origin,
            backwardRotationRadians,
            out probe);
    }
}
