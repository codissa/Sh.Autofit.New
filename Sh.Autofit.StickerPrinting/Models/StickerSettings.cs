namespace Sh.Autofit.StickerPrinting.Models;

public class StickerSettings
{
    // Label dimensions (TSC printer)
    public double WidthMm { get; set; } = 106.0;  // 10.6cm
    public double HeightMm { get; set; } = 25.0;  // 2.5cm

    // Printer settings
    public string PrinterName { get; set; } = string.Empty;
    public int DPI { get; set; } = 203; // TSC standard DPI

    // Layout (margins in mm)
    public double TopMargin { get; set; } = 2.0;
    public double BottomMargin { get; set; } = 2.0;
    public double LeftMargin { get; set; } = 2.0;
    public double RightMargin { get; set; } = 2.0;

    // Default font sizes (in points)
    public int IntroFontSize { get; set; } = 10;
    public int ItemKeyFontSize { get; set; } = 14;
    public int DescriptionFontSize { get; set; } = 12;

    // Global defaults
    public string DefaultIntroLine { get; set; } = "S.H. Car Rubber Import and Distribution";

    // Font names (configurable for different TSC models)
    public string DefaultFontName { get; set; } = "0";  // TSC internal font
    public string HebrewFontName { get; set; } = "HEBREW.TTF";
    public string ArabicFontName { get; set; } = "ARABIC.TTF";
}
