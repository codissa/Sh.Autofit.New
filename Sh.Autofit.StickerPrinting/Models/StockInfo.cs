namespace Sh.Autofit.StickerPrinting.Models;

public class StockInfo
{
    public int StockId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountKey { get; set; } = string.Empty;
    public DateTime? ValueDate { get; set; }
    public string Remarks { get; set; } = string.Empty;
}
