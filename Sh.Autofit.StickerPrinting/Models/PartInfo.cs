namespace Sh.Autofit.StickerPrinting.Models;

public class PartInfo
{
    public string ItemKey { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string? HebrewDescription { get; set; }
    public string? ArabicDescription { get; set; }
    public string? Category { get; set; }
}
