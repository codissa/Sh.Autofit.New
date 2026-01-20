using System;

namespace Sh.Autofit.StickerPrinting.Models;

public class StickerSettings
{
    // ===== 2-UP LAYOUT DIMENSIONS =====
    // Web width: Full paper roll width (106mm)
    public double WebWidthMm { get; set; } = 106.0;

    // Single label dimensions
    public double LabelWidthMm { get; set; } = 51.0;  // (106 - 2*2mm margin - 2mm gap) / 2
    public double LabelHeightMm { get; set; } = 25.0;  // 2.5cm

    // Layout spacing
    public double HorizontalGapMm { get; set; } = 2.0;  // Gap between left and right labels
    public double VerticalSpacingMm { get; set; } = 3.0; // Spacing between label rows (increased for better tear-off)

    // Backward compatibility properties
    [Obsolete("Use WebWidthMm instead for 2-up layouts")]
    public double WidthMm
    {
        get => WebWidthMm;
        set => WebWidthMm = value;
    }

    public double HeightMm
    {
        get => LabelHeightMm;
        set => LabelHeightMm = value;
    }

    // Printer settings
    public string PrinterName { get; set; } = string.Empty;
    public int DPI { get; set; } = 203; // Zebra S4M DPI

    // Layout (margins in mm)
    public double TopMargin { get; set; } = 2.0;
    public double BottomMargin { get; set; } = 2.0;
    public double LeftMargin { get; set; } = 2.0;
    public double RightMargin { get; set; } = 2.0;

    // Convenience property for LeftMargin (used in ZPL generator)
    public double LeftMarginMm
    {
        get => LeftMargin;
        set => LeftMargin = value;
    }

    // Default font sizes (in points)
    public int IntroFontSize { get; set; } = 12;
    public int ItemKeyFontSize { get; set; } = 13;
    public int DescriptionFontSize { get; set; } = 12;

    // ===== INTRO LINE CONFIGURATION =====
    /// <summary>
    /// Font family for intro line (e.g., "Arial", "Arial Narrow")
    /// Use "Arial Narrow" for condensed text to fit more at larger sizes
    /// </summary>
    public string IntroFontFamily { get; set; } = "Arial Narrow";

    /// <summary>
    /// Starting/maximum font size for intro line in points (will shrink if needed)
    /// </summary>
    public float IntroStartFontPt { get; set; } = 14.0f;

    /// <summary>
    /// Minimum font size for intro line in points
    /// </summary>
    public float IntroMinFontPt { get; set; } = 8.0f;

    /// <summary>
    /// Use bold font for intro line
    /// </summary>
    public bool IntroBold { get; set; } = true;

    // ===== ITEMKEY FONT CONFIGURATION =====
    /// <summary>
    /// Starting/maximum font size for item key in points (will shrink if needed)
    /// </summary>
    public float ItemKeyStartFontPt { get; set; } = 18.0f;

    /// <summary>
    /// Minimum font size for item key in points
    /// </summary>
    public float ItemKeyMinFontPt { get; set; } = 14.0f;

    // ===== DESCRIPTION FONT CONFIGURATION =====
    /// <summary>
    /// Starting/maximum font size for description in points (will shrink if needed)
    /// Defaults to match ItemKey for consistent sizing
    /// </summary>
    public float DescriptionStartFontPt { get; set; } = 18.0f;

    /// <summary>
    /// Minimum font size for description in points
    /// </summary>
    public float DescriptionMinFontPt { get; set; } = 14.0f;

    /// <summary>
    /// Maximum number of lines for description before shrinking font
    /// </summary>
    public int DescriptionMaxLines { get; set; } = 3;

    // ===== WIDTH/HEIGHT SCALING (ASPECT RATIO CONTROL) =====
    /// <summary>
    /// Width scaling factor for intro text (1.0 = normal, &lt;1.0 = compressed, &gt;1.0 = expanded)
    /// Example: 0.8 = 20% narrower (condensed), 1.2 = 20% wider
    /// </summary>
    public float IntroFontWidthScale { get; set; } = 1.0f;

    /// <summary>
    /// Height scaling factor for intro text (1.0 = normal, &lt;1.0 = shorter, &gt;1.0 = taller)
    /// </summary>
    public float IntroFontHeightScale { get; set; } = 1.0f;

    /// <summary>
    /// Width scaling factor for item key text (1.0 = normal, &lt;1.0 = compressed, &gt;1.0 = expanded)
    /// </summary>
    public float ItemKeyFontWidthScale { get; set; } = 1.0f;

    /// <summary>
    /// Height scaling factor for item key text (1.0 = normal, &lt;1.0 = shorter, &gt;1.0 = taller)
    /// </summary>
    public float ItemKeyFontHeightScale { get; set; } = 1.0f;

    /// <summary>
    /// Width scaling factor for description text (1.0 = normal, &lt;1.0 = compressed, &gt;1.0 = expanded)
    /// </summary>
    public float DescriptionFontWidthScale { get; set; } = 1.0f;

    /// <summary>
    /// Height scaling factor for description text (1.0 = normal, &lt;1.0 = shorter, &gt;1.0 = taller)
    /// </summary>
    public float DescriptionFontHeightScale { get; set; } = 1.0f;

    // Global defaults
    public string DefaultIntroLine { get; set; } = "S.H. CAR PARTS IMPORT AND DISTRIBUTION";

    // Font names (Zebra built-in fonts)
    public string DefaultFontName { get; set; } = "0";  // Zebra internal font
}
