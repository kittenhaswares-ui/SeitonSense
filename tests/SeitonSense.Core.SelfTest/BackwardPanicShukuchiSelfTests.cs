using SeitonSense.Core;

internal static class BackwardPanicShukuchiSelfTests
{
    public static void StandardFirstAndThirdPersonModesAreExact()
    {
        True(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with
                {
                    ControlMode = BackwardPanicShukuchiRules.FirstPersonControlMode,
                    ZoomMode = BackwardPanicShukuchiRules.FirstPersonZoomMode,
                }),
            "normal first-person camera is supported");
        True(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with
                {
                    ControlMode = BackwardPanicShukuchiRules.ThirdPersonLegacyControlMode,
                }),
            "normal legacy third-person camera is supported");
        True(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(ValidCamera()),
            "normal fixed third-person camera is supported");

        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with
                {
                    ControlMode = BackwardPanicShukuchiRules.FirstPersonControlMode,
                    ZoomMode = BackwardPanicShukuchiRules.ThirdPersonZoomMode,
                }),
            "inconsistent first-person telemetry fails closed");
        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with { ControlMode = 3 }),
            "lock-on first-person mode is not a standard camera");
        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with { ControlMode = 5 }),
            "lock-on third-person mode is not a standard camera");
        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with { ControlMode = 99 }),
            "unknown camera mode fails closed");
    }

    public static void MissingInvalidAndEventCameraDataFailClosed()
    {
        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with { CameraManagerAvailable = false }),
            "missing camera manager fails closed");
        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with { NormalCameraAvailable = false }),
            "missing normal camera fails closed");
        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with { ActiveCameraMatchesNormal = false }),
            "different active camera fails closed");
        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with { ActiveCameraIndex = 1 }),
            "event/cutscene camera index fails closed");
        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with { EventCameraAutoControl = true }),
            "event-camera auto-control fails closed");
        False(
            BackwardPanicShukuchiRules.IsSupportedStandardCamera(
                ValidCamera() with { DirectionRadians = float.NaN }),
            "non-finite direction fails closed");
        False(
            BackwardPanicShukuchiRules.TryCreateBackwardCameraProbe(
                new PanicShukuchiPoint(float.PositiveInfinity, 0f, 0f),
                ValidCamera(),
                out _,
                out _),
            "non-finite origin fails closed");
    }

    public static void BackwardCameraAxesAndDistanceAreExact()
    {
        var origin = new PanicShukuchiPoint(10f, 3f, -4f);
        True(
            BackwardPanicShukuchiRules.TryCreateBackwardCameraProbe(
                origin,
                ValidCamera() with
                {
                    ControlMode = BackwardPanicShukuchiRules.FirstPersonControlMode,
                    ZoomMode = BackwardPanicShukuchiRules.FirstPersonZoomMode,
                    DirectionRadians = 0f,
                },
                out var southRotation,
                out var south),
            "north-facing first-person view produces one southward point");
        Near(MathF.PI, MathF.Abs(southRotation), 0.0001f, "backward rotation is half a turn");
        Near(10f, south.X, 0.0001f, "first-person backward keeps X");
        Near(3f, south.Y, 0.0001f, "backward probe keeps origin Y");
        Near(-23.5f, south.Z, 0.0001f, "first-person backward goes south");
        Near(
            PanicShukuchiRules.SafeForwardDistanceYalms,
            HorizontalDistance(origin, south),
            0.0001f,
            "backward point is exactly 19.5 yalms horizontally");

        True(
            BackwardPanicShukuchiRules.TryCreateBackwardCameraProbe(
                origin,
                ValidCamera() with { DirectionRadians = 0f },
                out var northRotation,
                out var north),
            "third-person north camera azimuth produces one northward screen-back point");
        Near(0f, northRotation, 0.0001f, "third-person uses raw camera azimuth");
        Near(10f, north.X, 0.0001f, "third-person north keeps X");
        Near(15.5f, north.Z, 0.0001f, "third-person north goes toward the camera");

        True(
            BackwardPanicShukuchiRules.TryCreateBackwardCameraProbe(
                origin,
                ValidCamera() with { DirectionRadians = MathF.PI / 2f },
                out _,
                out var east),
            "third-person east camera azimuth produces one eastward screen-back point");
        Near(29.5f, east.X, 0.0001f, "third-person east goes toward the camera");
        Near(-4f, east.Z, 0.0001f, "third-person east keeps Z");
    }

    public static void BackwardGroundHitKeepsOneExactImmediateIntent()
    {
        var origin = new PanicShukuchiPoint(0f, 0f, 0f);
        True(
            BackwardPanicShukuchiRules.TryCreateBackwardCameraProbe(
                origin,
                ValidCamera(),
                out var backwardRotation,
                out var probe),
            "valid camera creates one frozen backward probe");
        var candidate = new PanicShukuchiCandidate(
            origin,
            backwardRotation,
            new PanicShukuchiGroundHit(
                true,
                probe with { Y = 4f }));
        var decision = PanicShukuchiRules.Evaluate(
            new PanicShukuchiCommandObservation(
                PluginEnabled: true,
                MetadataVerified: true,
                Context: SupportedPvPContext.CrystallineConflict,
                WolvesDenTestingEnabled: false,
                LocalJobId: PanicShukuchiRules.NinjaJobId,
                LocalPlayerAliveAndTargetable: true,
                ResolvedActionId: PanicShukuchiRules.ActionId,
                Candidate: candidate));

        True(decision.ShouldAttempt, "exact backward terrain keeps one immediate intent");
        Equal(probe with { Y = 4f }, decision.Intent!.Value.Destination, "intent keeps exact hit");
        False(
            PanicShukuchiRules.IsValidGroundHit(
                candidate with
                {
                    GroundHit = new PanicShukuchiGroundHit(
                        true,
                        probe with { X = probe.X + 0.051f }),
                }),
            "off-axis terrain cannot redirect the backward command");
        False(
            PanicShukuchiRules.IsValidGroundHit(
                candidate with
                {
                    GroundHit = new PanicShukuchiGroundHit(
                        true,
                        new PanicShukuchiPoint(
                            probe.X * (18f / PanicShukuchiRules.SafeForwardDistanceYalms),
                            0f,
                            probe.Z * (18f / PanicShukuchiRules.SafeForwardDistanceYalms))),
                }),
            "shorter terrain cannot become a fallback");
    }

    private static BackwardPanicShukuchiCameraObservation ValidCamera() => new(
        CameraManagerAvailable: true,
        NormalCameraAvailable: true,
        ActiveCameraMatchesNormal: true,
        ActiveCameraIndex: BackwardPanicShukuchiRules.NormalGameplayCameraIndex,
        ControlMode: BackwardPanicShukuchiRules.ThirdPersonFixedControlMode,
        ZoomMode: BackwardPanicShukuchiRules.ThirdPersonZoomMode,
        EventCameraAutoControl: false,
        DirectionRadians: 0f);

    private static float HorizontalDistance(
        PanicShukuchiPoint first,
        PanicShukuchiPoint second)
    {
        var deltaX = (double)second.X - first.X;
        var deltaZ = (double)second.Z - first.Z;
        return (float)Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
    }

    private static void True(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void False(bool value, string message) => True(!value, message);

    private static void Equal<T>(T expected, T actual, string message) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void Near(float expected, float actual, float tolerance, string message)
    {
        if (!float.IsFinite(actual) || MathF.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"{message}: expected {expected} +/- {tolerance}, got {actual}");
        }
    }
}
