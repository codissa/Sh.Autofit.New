namespace Sh.Autofit.StockExport.Models;

/// <summary>
/// Configuration settings for importing stock data from Excel files
/// </summary>
public class ExcelImportSettings
{
    /// <summary>
    /// Full path to the Excel file to import
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Name of the worksheet to read from
    /// </summary>
    public string SheetName { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based column index containing SH/ItemKey codes (optional)
    /// </summary>
    public int? ShCodeColumnIndex { get; set; }

    /// <summary>
    /// Zero-based column index containing OEM codes (optional)
    /// </summary>
    public int? OemCodeColumnIndex { get; set; }

    /// <summary>
    /// Zero-based column index containing quantities (required)
    /// </summary>
    public int QuantityColumnIndex { get; set; }

    /// <summary>
    /// First row containing data (1-based). Row 1 is typically headers, so default is 2.
    /// </summary>
    public int StartRow { get; set; } = 2;

    /// <summary>
    /// Indicates whether an SH code column is mapped
    /// </summary>
    public bool HasShCode => ShCodeColumnIndex.HasValue;

    /// <summary>
    /// Indicates whether an OEM code column is mapped
    /// </summary>
    public bool HasOemCode => OemCodeColumnIndex.HasValue;
}
