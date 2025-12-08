namespace Sh.Autofit.StockExport.Models;

/// <summary>
/// Contains all settings required for exporting stock moves to Excel
/// </summary>
public class ExportSettings
{
    /// <summary>
    /// The stock ID to query
    /// </summary>
    public int StockId { get; set; }

    /// <summary>
    /// Document type value (e.g., 24 for יתרת פתיחה)
    /// </summary>
    public int DocType { get; set; }

    /// <summary>
    /// Description text for column 2
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Document number for column 3
    /// </summary>
    public int DocNumber { get; set; }

    /// <summary>
    /// File path where the Excel file should be saved
    /// </summary>
    public string SavePath { get; set; } = string.Empty;
}
