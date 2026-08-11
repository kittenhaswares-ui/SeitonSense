using System.Numerics;

namespace SeitonSense.Plugin.Services;

internal sealed record NamePlateAnchorSnapshot(
    ulong GameObjectId,
    Vector2 JobIconTopLeft,
    Vector2 JobIconBottomRight,
    long CapturedAtMilliseconds)
{
    public float Width => JobIconBottomRight.X - JobIconTopLeft.X;
    public float Height => JobIconBottomRight.Y - JobIconTopLeft.Y;
}
