using System.Text.RegularExpressions;

namespace Sh.Autofit.New.PartsMappingUI.Helpers;

/// <summary>
/// Helper class for OEM number search operations with normalization
/// </summary>
public static class OemSearchHelper
{
    /// <summary>
    /// Normalizes OEM number by removing special characters (dots, dashes, slashes, spaces)
    /// </summary>
    /// <param name="oem">The OEM number to normalize</param>
    /// <returns>Normalized OEM number (lowercase, no special characters)</returns>
    public static string NormalizeOem(string oem)
    {
        if (string.IsNullOrWhiteSpace(oem))
            return string.Empty;

        // Remove dots, dashes, slashes, and spaces, then convert to lowercase
        return Regex.Replace(oem, @"[.\-/\s]", "").ToLower();
    }

    /// <summary>
    /// Checks if search term matches OEM using normalized, contains logic
    /// </summary>
    /// <param name="oem">The OEM number to search in</param>
    /// <param name="searchTerm">The search term to look for</param>
    /// <returns>True if the normalized search term is contained in the normalized OEM</returns>
    /// <example>
    /// OemContains("1844.51", "844") → true
    /// OemContains("1844-51", "84451") → true
    /// OemContains("1844/01", "844") → true
    /// </example>
    public static bool OemContains(string oem, string searchTerm)
    {
        var normalizedOem = NormalizeOem(oem);
        var normalizedSearch = NormalizeOem(searchTerm);

        return normalizedOem.Contains(normalizedSearch);
    }
}
