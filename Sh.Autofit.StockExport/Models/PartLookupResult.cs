namespace Sh.Autofit.StockExport.Models;

/// <summary>
/// Represents a part found during OEM code database lookup
/// </summary>
public class PartLookupResult
{
    /// <summary>
    /// Part number / ItemKey / SH code
    /// </summary>
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>
    /// Part description/name
    /// </summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>
    /// The OEM number that matched (from OEMNumber1-5 fields)
    /// </summary>
    public string OemNumber { get; set; } = string.Empty;

    /// <summary>
    /// Manufacturer name
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Category name
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Display text for UI selection dialogs
    /// </summary>
    public string DisplayText => $"{PartNumber} - {PartName}";

    /// <summary>
    /// Additional details for UI (manufacturer and category)
    /// </summary>
    public string DetailsText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Manufacturer))
                parts.Add($"יצרן: {Manufacturer}");
            if (!string.IsNullOrWhiteSpace(Category))
                parts.Add($"קטגוריה: {Category}");
            return string.Join(" | ", parts);
        }
    }
}
