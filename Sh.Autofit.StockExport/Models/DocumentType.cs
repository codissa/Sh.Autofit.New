namespace Sh.Autofit.StockExport.Models;

/// <summary>
/// Represents a document type option for the dropdown
/// </summary>
public class DocumentType
{
    /// <summary>
    /// The numeric value (e.g., 24)
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// The display name (e.g., "יתרת פתיחה")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Controls whether quantities should be negated in export
    /// True = negate quantities (except for ItemKey "*")
    /// False = keep quantities positive
    /// </summary>
    public bool ShouldNegateQuantities { get; set; } = true;

    /// <summary>
    /// Returns the display name for UI binding
    /// </summary>
    public override string ToString() => DisplayName;
}
