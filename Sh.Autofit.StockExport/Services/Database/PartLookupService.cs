using System.Data.SqlClient;
using Dapper;
using Sh.Autofit.StockExport.Helpers;
using Sh.Autofit.StockExport.Models;

namespace Sh.Autofit.StockExport.Services.Database;

/// <summary>
/// Service for validating and resolving part codes (SH codes and OEM codes) against the database
/// Uses vw_Parts view for lookups with OEM normalization
/// READ-ONLY access - no write operations are performed
/// </summary>
public class PartLookupService
{
    private readonly string _connectionString;

    public PartLookupService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Validates if an SH code / ItemKey exists in the database
    /// </summary>
    /// <param name="itemKey">The SH code to validate</param>
    /// <returns>True if the code exists, false otherwise</returns>
    public async Task<bool> ValidateShCodeAsync(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        const string sql = @"
            SELECT TOP 1 PartNumber
            FROM dbo.vw_Parts
            WHERE PartNumber = @ItemKey";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var result = await connection.QuerySingleOrDefaultAsync<string>(
                sql,
                new { ItemKey = itemKey },
                commandTimeout: 30
            );

            return result != null;
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                $"Failed to validate SH code in database: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Searches for parts matching the given OEM code (normalized search across all 5 OEM fields)
    /// Handles OEM fields with multiple codes separated by "/" by using LIKE pattern matching
    /// </summary>
    /// <param name="oemCode">The OEM code to search for</param>
    /// <returns>List of matching parts (may be empty, one, or multiple)</returns>
    public async Task<List<PartLookupResult>> SearchByOemCodeAsync(string oemCode)
    {
        if (string.IsNullOrWhiteSpace(oemCode))
            return new List<PartLookupResult>();

        // Normalize the OEM code for comparison
        var normalizedOem = OemNormalizer.Normalize(oemCode);

        // Use LIKE to match OEM codes that might be in "/" separated lists
        // The pattern will match the normalized code anywhere in the field
        const string sql = @"
            SELECT
                PartNumber,
                PartName,
                Manufacturer,
                Category,
                OEMNumber1,
                OEMNumber2,
                OEMNumber3,
                OEMNumber4,
                OEMNumber5
            FROM dbo.vw_Parts
            WHERE
                REPLACE(REPLACE(REPLACE(REPLACE(LOWER(ISNULL(OEMNumber1, '')), '.', ''), '-', ''), '/', ''), ' ', '') LIKE '%' + @NormalizedOem + '%'
                OR REPLACE(REPLACE(REPLACE(REPLACE(LOWER(ISNULL(OEMNumber2, '')), '.', ''), '-', ''), '/', ''), ' ', '') LIKE '%' + @NormalizedOem + '%'
                OR REPLACE(REPLACE(REPLACE(REPLACE(LOWER(ISNULL(OEMNumber3, '')), '.', ''), '-', ''), '/', ''), ' ', '') LIKE '%' + @NormalizedOem + '%'
                OR REPLACE(REPLACE(REPLACE(REPLACE(LOWER(ISNULL(OEMNumber4, '')), '.', ''), '-', ''), '/', ''), ' ', '') LIKE '%' + @NormalizedOem + '%'
                OR REPLACE(REPLACE(REPLACE(REPLACE(LOWER(ISNULL(OEMNumber5, '')), '.', ''), '-', ''), '/', ''), ' ', '') LIKE '%' + @NormalizedOem + '%'";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var dbResults = await connection.QueryAsync(
                sql,
                new { NormalizedOem = normalizedOem },
                commandTimeout: 60
            );

            // Filter results in memory to ensure exact match (not just substring)
            var results = new List<PartLookupResult>();

            foreach (var row in dbResults)
            {
                var partNumber = row.PartNumber?.ToString() ?? string.Empty;
                var partName = row.PartName?.ToString() ?? string.Empty;
                var manufacturer = row.Manufacturer?.ToString();
                var category = row.Category?.ToString();

                // Access OEM fields directly
                var oemFields = new[]
                {
                    row.OEMNumber1?.ToString(),
                    row.OEMNumber2?.ToString(),
                    row.OEMNumber3?.ToString(),
                    row.OEMNumber4?.ToString(),
                    row.OEMNumber5?.ToString()
                };

                // Check each OEM field for an exact match (handle "/" separated codes)
                string matchedOemNumber = string.Empty;
                bool foundMatch = false;

                foreach (var oemFieldValue in oemFields)
                {
                    if (string.IsNullOrWhiteSpace(oemFieldValue))
                        continue;

                    // Split by "/" to handle multiple codes in one field
                    var oemParts = oemFieldValue.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    foreach (var oemPart in oemParts)
                    {
                        if (OemNormalizer.Normalize(oemPart) == normalizedOem)
                        {
                            matchedOemNumber = oemPart.Trim();
                            foundMatch = true;
                            break;
                        }
                    }

                    if (foundMatch)
                        break;
                }

                // Only add to results if we found an exact match
                if (foundMatch)
                {
                    results.Add(new PartLookupResult
                    {
                        PartNumber = partNumber,
                        PartName = partName,
                        OemNumber = matchedOemNumber,
                        Manufacturer = manufacturer,
                        Category = category
                    });
                }
            }

            return results;
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                $"Failed to search for OEM code in database: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Validates and resolves all imported items by checking SH codes and/or searching OEM codes
    /// </summary>
    /// <param name="items">List of imported items to validate</param>
    /// <returns>The same list with updated validation status and resolved ItemKeys</returns>
    public async Task<List<ImportedStockItem>> ValidateAndResolveItemsAsync(List<ImportedStockItem> items)
    {
        if (items == null || items.Count == 0)
            return items ?? new List<ImportedStockItem>();

        foreach (var item in items)
        {
            try
            {
                // Priority 1: Validate SH code if present
                if (!string.IsNullOrWhiteSpace(item.RawShCode))
                {
                    bool shCodeExists = await ValidateShCodeAsync(item.RawShCode);

                    if (shCodeExists)
                    {
                        // SH code is valid - use it directly
                        item.ResolvedItemKey = item.RawShCode;
                        item.ValidationStatus = ValidationStatus.Valid;
                        item.ValidationMessage = string.Empty;
                        continue; // Skip OEM lookup
                    }
                    else
                    {
                        // SH code not found - will try OEM if available
                        if (string.IsNullOrWhiteSpace(item.RawOemCode))
                        {
                            // No OEM code to fallback to
                            item.ValidationStatus = ValidationStatus.ShCodeNotFound;
                            item.ValidationMessage = $"קוד SH '{item.RawShCode}' לא נמצא במאגר";
                            continue;
                        }
                        // Otherwise fall through to OEM search
                    }
                }

                // Priority 2: Search by OEM code (either as primary or fallback)
                if (!string.IsNullOrWhiteSpace(item.RawOemCode))
                {
                    var matches = await SearchByOemCodeAsync(item.RawOemCode);

                    if (matches.Count == 0)
                    {
                        item.ValidationStatus = ValidationStatus.OemCodeNotFound;
                        item.ValidationMessage = $"קוד OEM '{item.RawOemCode}' לא נמצא במאגר";
                    }
                    else if (matches.Count == 1)
                    {
                        // Single match - auto-resolve
                        item.ResolvedItemKey = matches[0].PartNumber;
                        item.ValidationStatus = ValidationStatus.Valid;
                        item.ValidationMessage = $"זוהה: {matches[0].PartNumber} - {matches[0].PartName}";
                        item.MatchedParts = matches;
                    }
                    else
                    {
                        // Multiple matches - requires manual selection
                        item.ValidationStatus = ValidationStatus.MultipleOemMatches;
                        item.ValidationMessage = $"נמצאו {matches.Count} התאמות לקוד OEM '{item.RawOemCode}' - נדרשת בחירה ידנית";
                        item.MatchedParts = matches;
                    }
                }
                else
                {
                    // Neither SH nor OEM code provided
                    item.ValidationStatus = ValidationStatus.OemCodeNotFound;
                    item.ValidationMessage = "לא סופק קוד SH או קוד OEM";
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with other items
                item.ValidationStatus = ValidationStatus.OemCodeNotFound;
                item.ValidationMessage = $"שגיאה באימות: {ex.Message}";
            }
        }

        return items;
    }
}
