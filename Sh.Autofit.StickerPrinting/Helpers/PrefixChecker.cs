namespace Sh.Autofit.StickerPrinting.Helpers;

public static class PrefixChecker
{
    private static readonly string[] ExcludedPrefixes =
    {
        "3pk", "4pk", "5pk", "6pk", "7pk", "8pk",
        "9.5X", "12.5X", "Ax", "bx"
    };

    // Prefixes/keys that should be completely ignored when loading stock moves
    private static readonly string[] IgnoredPrefixes = { "cv" };
    private static readonly string[] IgnoredExactKeys = { "100", "*" };

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

    /// <summary>
    /// Checks if the given item key should be completely ignored when loading stock moves.
    /// Items starting with "cv", or with key "100" or "*" are ignored.
    /// </summary>
    public static bool ShouldIgnoreItem(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return true;

        var trimmedKey = itemKey.Trim();

        // Check exact matches (case-insensitive)
        if (IgnoredExactKeys.Any(k => k.Equals(trimmedKey, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Check prefix matches (case-insensitive)
        var upperKey = trimmedKey.ToUpperInvariant();
        return IgnoredPrefixes.Any(prefix =>
            upperKey.StartsWith(prefix.ToUpperInvariant()));
    }
}
