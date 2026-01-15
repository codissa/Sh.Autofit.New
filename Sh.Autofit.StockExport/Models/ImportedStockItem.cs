namespace Sh.Autofit.StockExport.Models;

/// <summary>
/// Validation status for an imported stock item
/// </summary>
public enum ValidationStatus
{
    /// <summary>
    /// Item is valid and ready for export
    /// </summary>
    Valid,

    /// <summary>
    /// SH code not found in database
    /// </summary>
    ShCodeNotFound,

    /// <summary>
    /// OEM code not found in database
    /// </summary>
    OemCodeNotFound,

    /// <summary>
    /// OEM code matched multiple parts - manual selection required
    /// </summary>
    MultipleOemMatches,

    /// <summary>
    /// Quantity value is invalid or cannot be parsed
    /// </summary>
    InvalidQuantity
}

/// <summary>
/// Represents a stock item imported from Excel with validation status
/// </summary>
public class ImportedStockItem
{
    /// <summary>
    /// Excel row number (1-based) for error reporting
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Original SH code from Excel (if present)
    /// </summary>
    public string? RawShCode { get; set; }

    /// <summary>
    /// Original OEM code from Excel (if present)
    /// </summary>
    public string? RawOemCode { get; set; }

    /// <summary>
    /// Final ItemKey to use after validation/resolution
    /// </summary>
    public string? ResolvedItemKey { get; set; }

    /// <summary>
    /// Manually entered SH code for retry validation
    /// </summary>
    public string? ManualShCode { get; set; }

    /// <summary>
    /// Parsed quantity value
    /// </summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Current validation status
    /// </summary>
    public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Valid;

    /// <summary>
    /// User-friendly validation message (errors/warnings)
    /// </summary>
    public string ValidationMessage { get; set; } = string.Empty;

    /// <summary>
    /// List of matched parts (for OEM codes with multiple matches)
    /// </summary>
    public List<PartLookupResult> MatchedParts { get; set; } = new();

    /// <summary>
    /// Indicates if this item has unresolved issues
    /// </summary>
    public bool IsProblematic => ValidationStatus != ValidationStatus.Valid;
}
