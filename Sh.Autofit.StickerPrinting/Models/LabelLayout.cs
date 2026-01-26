using System.Collections.Generic;

namespace Sh.Autofit.StickerPrinting.Models;

/// <summary>
/// Contains all calculated layout positions, scales, and dimensions for a label.
/// This ensures both preview and print rendering use identical calculations.
/// </summary>
public class LabelLayout
{
    // ===== Label Dimensions =====
    public int Dpi { get; set; }
    public int LabelWidthDots { get; set; }
    public int LabelHeightDots { get; set; }
    public int UsableWidthDots { get; set; }
    public int DescriptionUsableWidth { get; set; }
    public int LeftMarginDots { get; set; }
    public int CenterX { get; set; }

    // ===== IntroLine =====
    public bool HasIntroLine { get; set; }
    public float IntroFontPt { get; set; }
    public float IntroWidthScale { get; set; }
    public float IntroHeightScale { get; set; }
    public int IntroY { get; set; }
    public int IntroHeightDots { get; set; }

    // ===== ItemKey =====
    public bool HasItemKey { get; set; }
    public float ItemKeyFontPt { get; set; }
    public float ItemKeyWidthScale { get; set; }
    public float ItemKeyHeightScale { get; set; }
    public int ItemKeyY { get; set; }

    // ===== Description =====
    public bool HasDescription { get; set; }
    public bool IsRtl { get; set; }
    public string FontFamily { get; set; } = "Arial Narrow";
    public float DescFontPt { get; set; }
    public float DescWidthScale { get; set; }
    public float DescHeightScale { get; set; }
    public int DescY { get; set; }
    public int LineSpacingDots { get; set; }
    public List<string> DescLines { get; set; } = new();
}
