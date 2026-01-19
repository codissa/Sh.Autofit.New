namespace Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

/// <summary>
/// Describes what features a printer type supports
/// </summary>
public class PrinterCapabilities
{
    public bool SupportsRtlText { get; set; }
    public bool SupportsCustomFonts { get; set; }
    public bool SupportsBarcodes { get; set; }
    public bool SupportsQrCodes { get; set; }
    public List<string> SupportedFontNames { get; set; } = new();
    public int MinDpi { get; set; }
    public int MaxDpi { get; set; }
    public int DefaultDpi { get; set; }
    public string CommandLanguage { get; set; } = string.Empty; // "TSPL", "ZPL", "ESC/POS"
}
