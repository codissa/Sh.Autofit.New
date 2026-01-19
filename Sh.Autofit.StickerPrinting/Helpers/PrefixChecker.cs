namespace Sh.Autofit.StickerPrinting.Helpers;

public static class PrefixChecker
{
    private static readonly string[] ExcludedPrefixes =
    {
        "3pk", "4pk", "5pk", "6pk", "7pk", "8pk",
        "9.5X", "12.5X", "Ax", "bx"
    };

    /// <summary>
    /// Checks if the given item key starts with an excluded prefix.
    /// Items with excluded prefixes should show ONLY the ItemKey on labels (no description).
    /// </summary>
    public static bool HasExcludedPrefix(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        var upperKey = itemKey.ToUpperInvariant();
        return ExcludedPrefixes.Any(prefix =>
            upperKey.StartsWith(prefix.ToUpperInvariant()));
    }
}
