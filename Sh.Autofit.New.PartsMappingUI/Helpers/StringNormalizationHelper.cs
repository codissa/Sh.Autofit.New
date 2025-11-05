using System;
using System.Text.RegularExpressions;

namespace Sh.Autofit.New.PartsMappingUI.Helpers;

/// <summary>
/// Helper class for normalizing strings for comparison, especially for commercial names and model names.
/// Removes all whitespace to ensure consistent comparisons regardless of spacing variations.
/// </summary>
public static class StringNormalizationHelper
{
    /// <summary>
    /// Removes all whitespace characters from a string for comparison purposes.
    /// </summary>
    /// <param name="value">The string to normalize. Can be null.</param>
    /// <returns>String with all whitespace removed, or empty string if input is null/empty.</returns>
    public static string RemoveWhitespace(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return Regex.Replace(value, @"\s+", "");
    }

    /// <summary>
    /// Compares two strings ignoring all whitespace.
    /// </summary>
    /// <param name="value1">First string to compare</param>
    /// <param name="value2">Second string to compare</param>
    /// <param name="comparisonType">The string comparison type (default: OrdinalIgnoreCase)</param>
    /// <returns>True if the strings are equal after removing whitespace</returns>
    public static bool EqualsIgnoringWhitespace(this string? value1, string? value2,
        StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
    {
        var normalized1 = RemoveWhitespace(value1);
        var normalized2 = RemoveWhitespace(value2);

        return string.Equals(normalized1, normalized2, comparisonType);
    }

    /// <summary>
    /// Gets a normalized version of the string suitable for grouping operations.
    /// Removes all whitespace and converts to uppercase for consistent grouping.
    /// </summary>
    /// <param name="value">The string to normalize</param>
    /// <returns>Normalized string for grouping</returns>
    public static string NormalizeForGrouping(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return RemoveWhitespace(value).ToUpperInvariant();
    }
}
