namespace SeitonSense.Plugin.Services;

internal sealed record FlashSnapshot(
    long StartedAtMilliseconds,
    long EndsAtMilliseconds,
    string SlotText)
{
    public static FlashSnapshot None { get; } = new(0, 0, string.Empty);
}
