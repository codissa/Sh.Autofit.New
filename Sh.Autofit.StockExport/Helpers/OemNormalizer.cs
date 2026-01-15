using System.Text.RegularExpressions;

namespace Sh.Autofit.StockExport.Helpers;

/// <summary>
/// Utility class for normalizing OEM codes for case-insensitive and special character-insensitive searches
/// Matches the normalization logic used in the main application's OemSearchHelper
/// </summary>
public static class OemNormalizer
{
    /// <summary>
    /// Normalizes an OEM code by removing special characters and converting to lowercase
    /// This enables consistent matching regardless of formatting variations
    /// </summary>
    /// <param name="input">The OEM code to normalize</param>
    /// <returns>Normalized OEM code (lowercase, no dots, dashes, slashes, or spaces)</returns>
    /// <example>
    /// "1844.51" → "184451"
    /// "1844-51" → "184451"
    /// "1844/51" → "184451"
    /// "1844 51" → "184451"
    /// "ABC.123-XYZ" → "abc123xyz"
    /// </example>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Remove: dots (.), dashes (-), slashes (/), spaces
        // Then convert to lowercase
        return Regex.Replace(input, @"[.\-/\s]", "").ToLower();
    }
}
