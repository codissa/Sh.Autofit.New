using System.Globalization;
using System.Linq;
using System.Text;
using Sh.Autofit.StickerPrinting.Helpers;
using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Zebra;

/// <summary>
/// Generates ZPL commands for Zebra printers
/// Handles text, RTL support using BiDi algorithm, multi-line text, and font sizing
/// Now supports 2-up label layout with Hebrew/Arabic bitmap rendering
/// </summary>
public class ZplCommandGenerator : IZplCommandGenerator
{
    /// <summary>
    /// Generate ZPL commands with 2-up layout support
    /// </summary>
    /// <param name="labelData">Label content</param>
    /// <param name="settings">Printer settings with 2-up dimensions</param>
    /// <param name="copies">Number of copies to print (controlled by ^PQ)</param>
    /// <param name="printTwoUp">If true, print 2 labels side-by-side; if false, print single label on left</param>
    /// <returns>ZPL command string</returns>
    public string GenerateLabelCommands(
        LabelData labelData,
        StickerSettings settings,
        int copies,
        bool printTwoUp)
    {
        var sb = new StringBuilder();

        // Calculate dimensions in dots
        int webWidthDots = MmToDots(settings.WebWidthMm, settings.DPI);
        int labelHeightDots = MmToDots(settings.LabelHeightMm, settings.DPI);
        int labelWidthDots = MmToDots(settings.LabelWidthMm, settings.DPI);
        int gapDots = MmToDots(settings.HorizontalGapMm, settings.DPI);
        int leftMarginDots = MmToDots(settings.LeftMarginMm, settings.DPI);
        int verticalGapDots = MmToDots(settings.VerticalSpacingMm, settings.DPI);

        // Calculate total label length including vertical gap
        // This is the "pitch" - the distance from the start of one label to the start of the next
        int totalLabelPitch = labelHeightDots + verticalGapDots;

        // Start label format
        sb.AppendLine("^XA"); // Start of label

        // CRITICAL: Set print width to WEB width, not label width
        sb.AppendLine($"^PW{webWidthDots}"); // Print width in dots

        // CRITICAL: Set label length to LABEL height (printable area)
        sb.AppendLine($"^LL{labelHeightDots}"); // Label length in dots

        // Label home position
        sb.AppendLine("^LH0,0");

        // Print mode - Tear-off with gap detection
        sb.AppendLine("^MMT");  // Media mode: Tear-off
        sb.AppendLine("^MNM");  // Media tracking: Non-continuous (gap/mark sensing)
        sb.AppendLine($"^ML{labelHeightDots}");  // Max label length (assists gap detection)

        // Note: ^MNM tells printer to use gap sensor between labels
        // ^ML sets expected label length to help calibration
        // If issues persist, printer may need manual calibration via front panel

        // Calculate base X positions for left and right labels
        int leftBaseX = leftMarginDots;
        int rightBaseX = leftMarginDots + labelWidthDots + gapDots;

        // ALWAYS draw left label
        DrawSingleLabel(sb, labelData, settings, leftBaseX, labelWidthDots);

        // Draw right label ONLY if printTwoUp is true (for pairs)
        // When printTwoUp=false (for single/odd labels), only left label is drawn
        if (printTwoUp)
        {
            DrawSingleLabel(sb, labelData, settings, rightBaseX, labelWidthDots);
        }

        // Set print quantity
        sb.AppendLine($"^PQ{copies},0,1,Y");

        // End label format
        sb.AppendLine("^XZ"); // End of label

        // Debug output - show complete ZPL for testing in Labelary
        var zplOutput = sb.ToString();
        System.Diagnostics.Debug.WriteLine("=== ZPL OUTPUT FOR LABELARY ===");
        System.Diagnostics.Debug.WriteLine($"Label info: printTwoUp={printTwoUp}, copies={copies}, webWidth={webWidthDots}dots, labelHeight={labelHeightDots}dots");
        System.Diagnostics.Debug.WriteLine("Copy the ZPL below and paste into: http://labelary.com/viewer.html");
        System.Diagnostics.Debug.WriteLine("Set DPI to 8dpmm (203dpi) and label size to 4x1 inches (106x25mm)");
        System.Diagnostics.Debug.WriteLine("--- START ZPL ---");
        System.Diagnostics.Debug.WriteLine(zplOutput);
        System.Diagnostics.Debug.WriteLine("--- END ZPL ---");
        System.Diagnostics.Debug.WriteLine("");

        return zplOutput;
    }

    /// <summary>
    /// Existing method signature for backward compatibility
    /// </summary>
    public string GenerateLabelCommands(LabelData labelData, StickerSettings settings)
    {
        // Call new method with single-label default
        return GenerateLabelCommands(labelData, settings, 1, false);
    }

    /// <summary>
    /// Draw a single label at the specified X position using bitmap rendering for all text
    /// </summary>
    private void DrawSingleLabel(
        StringBuilder sb,
        LabelData labelData,
        StickerSettings settings,
        int baseX,
        int labelWidthDots)
    {
        int dpi = settings.DPI;

        // Calculate Y positions
        int introY = MmToDots(settings.TopMargin, dpi);
        int itemKeyY = MmToDots(settings.LabelHeightMm * 0.25, dpi);
        int descriptionY = MmToDots(settings.LabelHeightMm * 0.55, dpi);

        // Calculate usable width (label width minus margins)
        int leftMarginDots = MmToDots(settings.LeftMargin, dpi);
        int rightMarginDots = MmToDots(settings.RightMargin, dpi);
        int usableWidthDots = labelWidthDots - leftMarginDots - rightMarginDots;

        // Center X position for centered elements
        int centerX = baseX + (labelWidthDots / 2);

        // ===== 1. IntroLine (configurable font, left-aligned, shrink to fit) =====
        if (!string.IsNullOrWhiteSpace(labelData.IntroLine))
        {
            // Use configurable font settings from StickerSettings
            float introFontPt = ZplGfaRenderer.FitFontPtToWidth(
                labelData.IntroLine,
                settings.IntroFontFamily,
                usableWidthDots,
                dpi,
                startFontPt: settings.IntroStartFontPt,
                minFontPt: settings.IntroMinFontPt,
                bold: settings.IntroBold,
                widthScale: settings.IntroFontWidthScale,
                heightScale: settings.IntroFontHeightScale);

            // Render as bitmap (left-aligned)
            string gfaCommand = ZplGfaRenderer.RenderTextAsGfa(
                labelData.IntroLine,
                settings.IntroFontFamily,
                introFontPt,
                usableWidthDots,
                dpi,
                isRtl: false, // IntroLine is always LTR
                xPosition: baseX + leftMarginDots, // Left edge
                yPosition: introY,
                alignment: TextAlignment.Left,
                bold: settings.IntroBold,
                widthScale: settings.IntroFontWidthScale,
                heightScale: settings.IntroFontHeightScale);

            if (!string.IsNullOrEmpty(gfaCommand))
            {
                sb.Append(gfaCommand);
            }
        }

        // ===== 2. ItemKey (centered, configurable font sizes) =====
        if (!string.IsNullOrWhiteSpace(labelData.ItemKey))
        {
            // Use configurable font settings from StickerSettings
            float itemKeyFontPt = ZplGfaRenderer.FitFontPtToWidth(
                labelData.ItemKey,
                "Arial",
                usableWidthDots,
                dpi,
                startFontPt: settings.ItemKeyStartFontPt,
                minFontPt: settings.ItemKeyMinFontPt,
                bold: true,
                widthScale: settings.ItemKeyFontWidthScale,
                heightScale: settings.ItemKeyFontHeightScale);

            // Render as bitmap (centered, bold for emphasis)
            string gfaCommand = ZplGfaRenderer.RenderTextAsGfa(
                labelData.ItemKey,
                "Arial",
                itemKeyFontPt,
                usableWidthDots,
                dpi,
                isRtl: false, // Item keys are always LTR
                xPosition: centerX,
                yPosition: itemKeyY,
                alignment: TextAlignment.Center,
                bold: true,  // Make ItemKey bold for better visibility
                widthScale: settings.ItemKeyFontWidthScale,
                heightScale: settings.ItemKeyFontHeightScale);

            if (!string.IsNullOrEmpty(gfaCommand))
            {
                sb.Append(gfaCommand);
            }
            else
            {
                // Debug warning if GFA rendering failed
                System.Diagnostics.Debug.WriteLine($"[WARNING] GFA rendering failed for ItemKey: '{labelData.ItemKey}'");
            }
        }
        else
        {
            // Debug warning if ItemKey is empty
            System.Diagnostics.Debug.WriteLine("[WARNING] ItemKey is empty - label will not show item number!");
        }

        // ===== 3. Description (multi-line, centered, intelligent 3-line shrinking, RTL aware) =====
        if (labelData.ShouldShowDescription && !string.IsNullOrWhiteSpace(labelData.Description))
        {
            // Detect RTL using ContainsHebrewOrArabic
            bool isRtl = ZplGfaRenderer.ContainsHebrewOrArabic(labelData.Description);
            string fontFamily = labelData.FontFamily ?? "Arial Narrow";

            // Start with same font size as ItemKey for consistency
            float descFontPt = settings.DescriptionStartFontPt;
            var descLines = new System.Collections.Generic.List<string>();

            // Shrink font until text fits within max lines
            do
            {
                descLines = FontSizeCalculator.SplitTextToFit(
                    labelData.Description,
                    settings.LabelWidthMm - settings.LeftMargin - settings.RightMargin,
                    descFontPt,
                    fontFamily);

                if (descLines.Count <= settings.DescriptionMaxLines)
                    break;

                descFontPt -= 0.5f; // Reduce by 0.5pt increments

            } while (descFontPt >= settings.DescriptionMinFontPt);

            // Limit to max lines (safety check)
            if (descLines.Count > settings.DescriptionMaxLines)
            {
                descLines = descLines.Take(settings.DescriptionMaxLines).ToList();
            }

            // Calculate line spacing (1.1x font height)
            int lineSpacingDots = (int)(descFontPt * 1.1 * dpi / 72.0);
            int lineY = descriptionY;

            // Render each line centered
            foreach (var line in descLines)
            {
                string gfaCommand = ZplGfaRenderer.RenderTextAsGfa(
                    line,
                    fontFamily,
                    descFontPt,
                    usableWidthDots,
                    dpi,
                    isRtl: isRtl,
                    xPosition: centerX,
                    yPosition: lineY,
                    alignment: TextAlignment.Center,
                    bold: false,
                    widthScale: settings.DescriptionFontWidthScale,
                    heightScale: settings.DescriptionFontHeightScale);

                if (!string.IsNullOrEmpty(gfaCommand))
                {
                    sb.Append(gfaCommand);
                }

                lineY += lineSpacingDots;
            }
        }
    }

    public string? GenerateInitializationCommands(StickerSettings settings)
    {
        // Optional: Could send calibration or reset commands
        return null;
    }

    public string? GenerateFinalizationCommands()
    {
        // Optional: Could send cleanup commands
        return null;
    }

    private int MmToDots(double mm, int dpi)
    {
        return (int)Math.Round(mm * dpi / 25.4);
    }

    private string EscapeZpl(string text)
    {
        // ZPL special characters that need escaping
        // ^ and ~ are command prefixes, _ is hex delimiter
        return text.Replace("^", "\\^")
                   .Replace("~", "\\~")
                   .Replace("_", "\\_");
    }

    /// <summary>
    /// [DEPRECATED] Replaced by GFA bitmap rendering for RTL text
    /// Applies Unicode BiDi algorithm to properly order RTL text
    /// Handles Hebrew and Arabic text with proper character ordering
    /// NOTE: This method is bypassed when using GFA bitmap rendering
    /// </summary>
    private string ApplyBiDiAlgorithm(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Use .NET's BiDi support through StringInfo and Unicode categories
        var result = new List<char>();
        var segments = SplitIntoDirectionalRuns(text);

        // Process each segment based on its directionality
        foreach (var segment in segments)
        {
            if (segment.IsRtl)
            {
                // Reverse the RTL segment
                var reversed = segment.Text.Reverse().ToArray();
                result.AddRange(reversed);
            }
            else
            {
                // Keep LTR segments as-is
                result.AddRange(segment.Text);
            }
        }

        return new string(result.ToArray());
    }

    private List<TextSegment> SplitIntoDirectionalRuns(string text)
    {
        var segments = new List<TextSegment>();
        if (string.IsNullOrEmpty(text))
            return segments;

        var currentSegment = new StringBuilder();
        bool? currentIsRtl = null;

        foreach (char c in text)
        {
            bool isRtl = IsRtlCharacter(c);

            if (currentIsRtl == null)
            {
                // First character
                currentIsRtl = isRtl;
                currentSegment.Append(c);
            }
            else if (currentIsRtl == isRtl)
            {
                // Same direction, continue current segment
                currentSegment.Append(c);
            }
            else
            {
                // Direction changed, save current segment and start new one
                segments.Add(new TextSegment
                {
                    Text = currentSegment.ToString(),
                    IsRtl = currentIsRtl.Value
                });

                currentSegment.Clear();
                currentSegment.Append(c);
                currentIsRtl = isRtl;
            }
        }

        // Add the last segment
        if (currentSegment.Length > 0 && currentIsRtl.HasValue)
        {
            segments.Add(new TextSegment
            {
                Text = currentSegment.ToString(),
                IsRtl = currentIsRtl.Value
            });
        }

        return segments;
    }

    private bool IsRtlCharacter(char c)
    {
        // Check if character is from RTL Unicode blocks
        var category = CharUnicodeInfo.GetUnicodeCategory(c);

        // Hebrew: U+0590 - U+05FF
        // Arabic: U+0600 - U+06FF, U+0750 - U+077F, U+08A0 - U+08FF
        if ((c >= 0x0590 && c <= 0x05FF) || // Hebrew
            (c >= 0x0600 && c <= 0x06FF) || // Arabic
            (c >= 0x0750 && c <= 0x077F) || // Arabic Supplement
            (c >= 0x08A0 && c <= 0x08FF))   // Arabic Extended-A
        {
            return true;
        }

        // Whitespace and common characters are neutral
        if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsDigit(c))
        {
            return false; // Treat as LTR for simplicity
        }

        return false;
    }

    private class TextSegment
    {
        public string Text { get; set; } = string.Empty;
        public bool IsRtl { get; set; }
    }
}
