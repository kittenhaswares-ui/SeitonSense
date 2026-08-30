namespace SeitonSense.Core;

/// <summary>
/// Keeps optional release-note presentation data bounded and non-fatal. A bad
/// changelog must never prevent the gameplay plugin from loading.
/// </summary>
public static class ReleaseNotesContentRules
{
    public const int MaximumBulletCount = 5;

    public static string[] NormalizeBullets(IEnumerable<string?>? bullets)
    {
        if (bullets is null) return [];

        return bullets
            .Where(static bullet => !string.IsNullOrWhiteSpace(bullet))
            .Select(static bullet => bullet!.Trim())
            .Take(MaximumBulletCount)
            .ToArray();
    }
}
