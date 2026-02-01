namespace Sh.Autofit.StickerPrinting.Models;

public class StockMoveItem
{
    public string ItemKey { get; set; } = string.Empty;
    public double TotalQuantity { get; set; }
    public string? Localization { get; set; }
    public int OriginalOrder { get; set; }
}
