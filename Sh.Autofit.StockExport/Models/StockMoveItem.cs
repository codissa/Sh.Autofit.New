namespace Sh.Autofit.StockExport.Models;

/// <summary>
/// Represents an aggregated stock move item from the database
/// </summary>
public class StockMoveItem
{
    /// <summary>
    /// Item key from the database - must be preserved as text
    /// </summary>
    public string ItemKey { get; set; } = string.Empty;

    /// <summary>
    /// Total quantity for this item (aggregated)
    /// </summary>
    public double TotalQuantity { get; set; }
}
