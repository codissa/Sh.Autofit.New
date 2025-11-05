using System.Text.RegularExpressions;

namespace Sh.Autofit.New.PartsMappingUI.Services;

/// <summary>
/// Utility class for matching OEM numbers based on manufacturer-specific patterns
/// </summary>
public static class OemPatternMatcher
{
    /// <summary>
    /// Calculate similarity score between two OEM numbers based on manufacturer patterns
    /// </summary>
    /// <param name="oem1">First OEM number</param>
    /// <param name="oem2">Second OEM number</param>
    /// <param name="manufacturerName">Manufacturer name to apply specific patterns</param>
    /// <returns>Similarity score (0-25 points)</returns>
    public static double CalculateOemSimilarity(string oem1, string oem2, string manufacturerName)
    {
        if (string.IsNullOrWhiteSpace(oem1) || string.IsNullOrWhiteSpace(oem2))
            return 0;

        // Normalize both OEM numbers
        var norm1 = NormalizeOem(oem1);
        var norm2 = NormalizeOem(oem2);

        // Exact match
        if (norm1 == norm2)
            return 25;

        // Apply manufacturer-specific pattern matching
        var mfg = manufacturerName?.ToLower() ?? "";

        if (mfg.Contains("honda") || mfg.Contains("acura"))
            return MatchHonda(norm1, norm2);

        if (mfg.Contains("toyota") || mfg.Contains("lexus"))
            return MatchToyota(norm1, norm2);

        if (mfg.Contains("nissan") || mfg.Contains("infiniti"))
            return MatchNissan(norm1, norm2);

        if (mfg.Contains("volkswagen") || mfg.Contains("vw") || mfg.Contains("audi") ||
            mfg.Contains("skoda") || mfg.Contains("seat"))
            return MatchVwGroup(norm1, norm2);

        if (mfg.Contains("bmw") || mfg.Contains("mini"))
            return MatchBmw(norm1, norm2);

        if (mfg.Contains("mercedes") || mfg.Contains("benz"))
            return MatchMercedes(norm1, norm2);

        if (mfg.Contains("ford") || mfg.Contains("lincoln"))
            return MatchFord(norm1, norm2);

        if (mfg.Contains("chevrolet") || mfg.Contains("gmc") || mfg.Contains("cadillac") ||
            mfg.Contains("buick") || mfg.Contains("general motors") || mfg.Contains("gm"))
            return MatchGm(norm1, norm2);

        if (mfg.Contains("hyundai") || mfg.Contains("kia"))
            return MatchHyundaiKia(norm1, norm2);

        if (mfg.Contains("renault") || mfg.Contains("dacia"))
            return MatchRenault(norm1, norm2);

        if (mfg.Contains("mazda"))
            return MatchMazda(norm1, norm2);

        if (mfg.Contains("peugeot") || mfg.Contains("citroen") || mfg.Contains("ds"))
            return MatchPsaGroup(norm1, norm2);

        if (mfg.Contains("fiat") || mfg.Contains("alfa") || mfg.Contains("lancia"))
            return MatchFiat(norm1, norm2);

        // Generic fuzzy matching for unknown manufacturers
        return CalculateGenericSimilarity(norm1, norm2);
    }

    /// <summary>
    /// Normalize OEM number: uppercase, remove special chars
    /// </summary>
    public static string NormalizeOem(string oem)
    {
        if (string.IsNullOrWhiteSpace(oem))
            return string.Empty;

        return Regex.Replace(oem.ToUpperInvariant(), @"[^A-Z0-9]", "");
    }

    /// <summary>
    /// Parse multiple OEM numbers from a single field (separated by /)
    /// </summary>
    public static List<string> ParseOemField(string oemField)
    {
        if (string.IsNullOrWhiteSpace(oemField))
            return new List<string>();

        return oemField
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(o => o.Trim())
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToList();
    }

    #region Manufacturer-Specific Matchers

    // Honda/Acura: 12345-ABC-000 → 5 digit prefix or 8 char prefix
    private static double MatchHonda(string norm1, string norm2)
    {
        // Prefix match (8 chars): Same part family + application
        if (norm1.Length >= 8 && norm2.Length >= 8 &&
            norm1.Substring(0, 8) == norm2.Substring(0, 8))
            return 20;

        // Prefix match (5 chars): Same part family
        if (norm1.Length >= 5 && norm2.Length >= 5 &&
            norm1.Substring(0, 5) == norm2.Substring(0, 5))
            return 15;

        return 0;
    }

    // Toyota/Lexus: 90210-12345 → 5 digit prefix
    private static double MatchToyota(string norm1, string norm2)
    {
        // Prefix match (5 chars): Same part group
        if (norm1.Length >= 5 && norm2.Length >= 5 &&
            norm1.Substring(0, 5) == norm2.Substring(0, 5))
            return 15;

        return 0;
    }

    // Nissan/Infiniti: 41060-1AA0A → 5 char prefix
    private static double MatchNissan(string norm1, string norm2)
    {
        // Prefix match (5 chars): Same part group
        if (norm1.Length >= 5 && norm2.Length >= 5 &&
            norm1.Substring(0, 5) == norm2.Substring(0, 5))
            return 15;

        return 0;
    }

    // VW Group: 1K0615301AA → 3 char platform or 6 char platform+group
    private static double MatchVwGroup(string norm1, string norm2)
    {
        // Prefix match (6 chars): Same platform + part group
        if (norm1.Length >= 6 && norm2.Length >= 6 &&
            norm1.Substring(0, 6) == norm2.Substring(0, 6))
            return 20;

        // Platform match (3 chars): Same platform family
        if (norm1.Length >= 3 && norm2.Length >= 3 &&
            norm1.Substring(0, 3) == norm2.Substring(0, 3))
            return 10;

        return 0;
    }

    // BMW/Mini: 34116777772 → 2 or 4 digit prefix
    private static double MatchBmw(string norm1, string norm2)
    {
        // Prefix match (4 chars): Same part group + subgroup
        if (norm1.Length >= 4 && norm2.Length >= 4 &&
            norm1.Substring(0, 4) == norm2.Substring(0, 4))
            return 20;

        // Prefix match (2 chars): Same part group
        if (norm1.Length >= 2 && norm2.Length >= 2 &&
            norm1.Substring(0, 2) == norm2.Substring(0, 2))
            return 15;

        return 0;
    }

    // Mercedes: A0004201220 → 7 char prefix
    private static double MatchMercedes(string norm1, string norm2)
    {
        // Prefix match (7 chars): Same part group + number
        if (norm1.Length >= 7 && norm2.Length >= 7 &&
            norm1.Substring(0, 7) == norm2.Substring(0, 7))
            return 20;

        return 0;
    }

    // Ford/Lincoln: F1DZ2001A → 5 char suffix match
    private static double MatchFord(string norm1, string norm2)
    {
        // Suffix match (5 chars): Same part number + revision
        if (norm1.Length >= 5 && norm2.Length >= 5 &&
            norm1.Substring(norm1.Length - 5) == norm2.Substring(norm2.Length - 5))
            return 20;

        return 0;
    }

    // GM: 84406322 → 4 digit prefix
    private static double MatchGm(string norm1, string norm2)
    {
        // Prefix match (4 chars): Possible part family
        if (norm1.Length >= 4 && norm2.Length >= 4 &&
            norm1.Substring(0, 4) == norm2.Substring(0, 4))
            return 10;

        return 0;
    }

    // Hyundai/Kia: 583021GA00 → 5 digit prefix
    private static double MatchHyundaiKia(string norm1, string norm2)
    {
        // Prefix match (5 chars): Same part group
        if (norm1.Length >= 5 && norm2.Length >= 5 &&
            norm1.Substring(0, 5) == norm2.Substring(0, 5))
            return 15;

        return 0;
    }

    // Renault: 296H50128R → 10 char format, check first 7-8 chars
    private static double MatchRenault(string norm1, string norm2)
    {
        // Prefix match (7 chars): Similar part group
        if (norm1.Length >= 7 && norm2.Length >= 7 &&
            norm1.Substring(0, 7) == norm2.Substring(0, 7))
            return 15;

        // Prefix match (4 chars): Same series (7700, 8200, etc.)
        if (norm1.Length >= 4 && norm2.Length >= 4 &&
            norm1.Substring(0, 4) == norm2.Substring(0, 4))
            return 10;

        return 0;
    }

    // Mazda: FD0152211D → Format is xxxx-xx-xxx, check first 4 or first 6
    private static double MatchMazda(string norm1, string norm2)
    {
        // Prefix match (6 chars): Same application + section
        if (norm1.Length >= 6 && norm2.Length >= 6 &&
            norm1.Substring(0, 6) == norm2.Substring(0, 6))
            return 15;

        // Prefix match (4 chars): Same application
        if (norm1.Length >= 4 && norm2.Length >= 4 &&
            norm1.Substring(0, 4) == norm2.Substring(0, 4))
            return 10;

        return 0;
    }

    // PSA Group (Peugeot/Citroen): 5960A2 → 4-5 digits + letters
    private static double MatchPsaGroup(string norm1, string norm2)
    {
        // Extract numeric prefix
        var match1 = Regex.Match(norm1, @"^\d{4,5}");
        var match2 = Regex.Match(norm2, @"^\d{4,5}");

        if (match1.Success && match2.Success && match1.Value == match2.Value)
            return 15;

        // Check first 4 chars
        if (norm1.Length >= 4 && norm2.Length >= 4 &&
            norm1.Substring(0, 4) == norm2.Substring(0, 4))
            return 10;

        return 0;
    }

    // Fiat: 46480362 → 8 digit numeric, check first 4-6 digits
    private static double MatchFiat(string norm1, string norm2)
    {
        // Remove leading zeros for comparison
        norm1 = norm1.TrimStart('0');
        norm2 = norm2.TrimStart('0');

        // Prefix match (6 chars)
        if (norm1.Length >= 6 && norm2.Length >= 6 &&
            norm1.Substring(0, 6) == norm2.Substring(0, 6))
            return 15;

        // Prefix match (4 chars)
        if (norm1.Length >= 4 && norm2.Length >= 4 &&
            norm1.Substring(0, 4) == norm2.Substring(0, 4))
            return 10;

        return 0;
    }

    #endregion

    #region Generic Fuzzy Matching

    /// <summary>
    /// Generic similarity calculation using Levenshtein distance
    /// </summary>
    private static double CalculateGenericSimilarity(string norm1, string norm2)
    {
        // Check for common prefixes
        int commonPrefixLength = 0;
        int minLength = Math.Min(norm1.Length, norm2.Length);

        for (int i = 0; i < minLength; i++)
        {
            if (norm1[i] == norm2[i])
                commonPrefixLength++;
            else
                break;
        }

        // Long common prefix
        if (commonPrefixLength >= 6)
            return 15;
        if (commonPrefixLength >= 4)
            return 10;
        if (commonPrefixLength >= 3)
            return 5;

        // Levenshtein distance for typo tolerance
        int distance = LevenshteinDistance(norm1, norm2);
        if (distance <= 2 && Math.Min(norm1.Length, norm2.Length) >= 5)
            return 10; // Very similar, likely typo

        return 0;
    }

    /// <summary>
    /// Calculate Levenshtein distance between two strings
    /// </summary>
    private static int LevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
        if (string.IsNullOrEmpty(s2)) return s1.Length;

        int[,] d = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            d[i, 0] = i;
        for (int j = 0; j <= s2.Length; j++)
            d[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[s1.Length, s2.Length];
    }

    #endregion

    /// <summary>
    /// Find the best matching score between any pair of OEM numbers from two lists
    /// </summary>
    public static double FindBestOemMatch(List<string> oemList1, List<string> oemList2, string manufacturerName)
    {
        double bestScore = 0;

        foreach (var oem1 in oemList1)
        {
            foreach (var oem2 in oemList2)
            {
                var score = CalculateOemSimilarity(oem1, oem2, manufacturerName);
                if (score > bestScore)
                    bestScore = score;

                // Early exit if we found an exact match
                if (bestScore >= 25)
                    return bestScore;
            }
        }

        return bestScore;
    }
}
