using SeitonSense.Core;

internal static class ReleaseNotesContentSelfTests
{
    internal static void MalformedContentIsBoundedAndNeverThrows()
    {
        Equal(0, ReleaseNotesContentRules.NormalizeBullets(null).Length, "null content hides safely");
        Equal(
            0,
            ReleaseNotesContentRules.NormalizeBullets(["", "   ", "\t"]).Length,
            "blank content hides safely");

        var normalized = ReleaseNotesContentRules.NormalizeBullets(
        [
            " one ",
            "two",
            "three",
            "four",
            "five",
            "six",
        ]);

        Equal(ReleaseNotesContentRules.MaximumBulletCount, normalized.Length, "oversized content is capped");
        Equal("one", normalized[0], "content is trimmed");
        Equal("five", normalized[^1], "the first five release notes remain ordered");
    }

    private static void Equal<T>(T expected, T actual, string message)
        where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
