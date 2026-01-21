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

        // Initialization block (matches legacy PRN that works)
        sb.AppendLine("CT~~CD,~CC^~CT~");  // Set command chars if modified
        sb.AppendLine("^XA");
        sb.AppendLine("~TA000");     // Tear adjust position: 0
        sb.AppendLine("~JSN");       // Backfeed sequence: None
        sb.AppendLine("^LT0");       // Label top position: 0
        sb.AppendLine("^MNW");       // Media tracking: Web sensing
        sb.AppendLine("^MTD");       // Media type: Direct thermal
        sb.AppendLine("^PON");       // Print orientation: Normal
        sb.AppendLine("^PMN");       // Print mirror: No
        sb.AppendLine("^LH0,0");     // Label home: (0,0)
        sb.AppendLine("^JMA");       // Graphics mode: Alternative
        sb.AppendLine("^PR4,4");     // Print rate: 4 ips print, 4 ips slew
        sb.AppendLine("~SD15");      // Darkness: 15
        sb.AppendLine("^JUS");       // Save current settings
        sb.AppendLine("^LRN");       // Label reverse: No
        sb.AppendLine("^CI0");       // Character set: USA1
        sb.AppendLine("^XZ");

        // Start actual label format
        sb.AppendLine("^XA");
        sb.AppendLine("^MMT");       // Media mode: Tear-off
        //sb.AppendLine("^PW831");     // Print width: 831 dots (matches legacy)
        sb.AppendLine($"^PW{webWidthDots}"); // Print width in dots
        sb.AppendLine($"^LL0{labelHeightDots}"); // Label length with leading zero
        sb.AppendLine("^LS0");       // Label shift: 0
        sb.AppendLine("^LH0,0");     // Label home

        // Calculate base X positions for left and right labels (with optional horizontal offset)
        int horizontalOffsetDots = MmToDots(settings.HorizontalOffsetMm, settings.DPI);
        int leftBaseX = leftMarginDots + horizontalOffsetDots;
        if(leftBaseX < 0)
            leftBaseX = 0; // Prevent negative X position
        int rightBaseX = leftMarginDots + labelWidthDots + gapDots + horizontalOffsetDots;

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

        // Calculate initial Y position (itemKeyY will be calculated dynamically after IntroLine)
        int introY = MmToDots(settings.TopMargin, dpi);

        // Calculate usable width (label width minus margins)
        int leftMarginDots = MmToDots(settings.LeftMargin, dpi);
        int rightMarginDots = MmToDots(settings.RightMargin, dpi);
        int usableWidthDots = labelWidthDots - leftMarginDots - rightMarginDots;

        // Description needs extra margins to prevent edge clipping (0.3mm on each side)
        int descriptionUsableWidth = usableWidthDots - MmToDots(0.6, dpi);

        // Center X position for centered elements
        int centerX = baseX + (labelWidthDots / 2);

        // ===== 1. IntroLine (fixed 28pt font, compress width to fit single line) =====
        float introFontPt = settings.IntroStartFontPt;  // Fixed at 28pt
        float introWidthScale = 1.0f;

        if (!string.IsNullOrWhiteSpace(labelData.IntroLine))
        {
            // Intro ALWAYS uses the start font size (28pt) - never shrinks font
            // Instead, we compress the WIDTH if text is too long

            // Calculate optimal width compression to fit text in single line
            introWidthScale = ZplGfaRenderer.FitWidthScaleToWidth(
                labelData.IntroLine,
                settings.IntroFontFamily,
                introFontPt,  // Use fixed 28pt
                usableWidthDots,
                dpi,
                bold: settings.IntroBold,
                minWidthScale: settings.IntroMinWidthScale);  // Down to 50% minimum

            // Render as bitmap (left-aligned, single line)
            string gfaCommand = ZplGfaRenderer.RenderTextAsGfa(
                labelData.IntroLine,
                settings.IntroFontFamily,
                introFontPt,  // Always 28pt
                usableWidthDots,
                dpi,
                isRtl: false, // IntroLine is always LTR
                xPosition: baseX + leftMarginDots, // Left edge
                yPosition: introY,
                alignment: TextAlignment.Left,
                bold: settings.IntroBold,
                widthScale: introWidthScale,  // Dynamically compressed!
                heightScale: settings.IntroFontHeightScale);

            if (!string.IsNullOrEmpty(gfaCommand))
            {
                sb.Append(gfaCommand);
            }
        }

        // Calculate itemKeyY dynamically based on where IntroLine ends
        int itemKeyY;
        if (!string.IsNullOrWhiteSpace(labelData.IntroLine))
        {
            // Measure IntroLine height to know where it ends
            var (_, introHeightDots) = ZplGfaRenderer.MeasureTextDots(
                labelData.IntroLine, settings.IntroFontFamily, introFontPt, dpi, 0,
                bold: settings.IntroBold, introWidthScale, settings.IntroFontHeightScale);

            // ItemKey starts after IntroLine with small gap (1mm)
            itemKeyY = introY + introHeightDots + MmToDots(1, dpi);
        }
        else
        {
            // Fallback if no IntroLine
            itemKeyY = MmToDots(settings.LabelHeightMm * 0.25, dpi);
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

        // Calculate descriptionY dynamically based on where ItemKey ends
        int descriptionY;
        if (!string.IsNullOrWhiteSpace(labelData.ItemKey))
        {
            // Measure ItemKey height to know where it ends
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

            var (_, itemKeyHeightDots) = ZplGfaRenderer.MeasureTextDots(
                labelData.ItemKey, "Arial", itemKeyFontPt, dpi, 0, bold: true,
                settings.ItemKeyFontWidthScale, settings.ItemKeyFontHeightScale);

            // Description starts after ItemKey with gap (0.4mm)
            descriptionY = itemKeyY + itemKeyHeightDots + MmToDots(0.4, dpi);
        }
        else
        {
            // Fallback if no ItemKey
            descriptionY = MmToDots(settings.LabelHeightMm * 0.50, dpi);
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
            // Use description-specific width (with extra margins for edge protection)
            double descriptionWidthMm = settings.LabelWidthMm - settings.LeftMargin - settings.RightMargin - 0.6;
            do
            {
                descLines = FontSizeCalculator.SplitTextToFit(
                    labelData.Description,
                    descriptionWidthMm,
                    descFontPt,
                    fontFamily,
                    settings.DescriptionFontWidthScale);

                if (descLines.Count <= settings.DescriptionMaxLines)
                    break;

                descFontPt -= 0.5f; // Reduce by 0.5pt increments

            } while (descFontPt >= settings.DescriptionMinFontPt);

            // Limit to max lines (safety check)
            if (descLines.Count > settings.DescriptionMaxLines)
            {
                descLines = descLines.Take(settings.DescriptionMaxLines).ToList();
            }

            // Calculate line height and check if description fits
            // Line height factor: font points to dots conversion with spacing
            const float lineHeightFactor = 0.8f; // Standard line height (1.2x font size)
            float descHeightScale = settings.DescriptionFontHeightScale;
            float descWidthScale = settings.DescriptionFontWidthScale;
            int lineHeightDots = (int)(descFontPt * descHeightScale * lineHeightFactor * dpi / 72.0);
            int totalDescriptionHeight = lineHeightDots * descLines.Count;
            int labelBottomDots = MmToDots(settings.LabelHeightMm - settings.BottomMargin, dpi);
            int availableHeight = labelBottomDots - descriptionY;

            // If description would overflow, reduce height scale to fit
            if (totalDescriptionHeight > availableHeight && descLines.Count > 0)
            {
                // Calculate required height scale to fit
                float requiredScale = (float)availableHeight / (descLines.Count * descFontPt * lineHeightFactor * dpi / 72.0f);
                descHeightScale = Math.Max(0.6f, Math.Min(descHeightScale, requiredScale)); // Minimum 60% height
                lineHeightDots = (int)(descFontPt * descHeightScale * lineHeightFactor * dpi / 72.0);
                totalDescriptionHeight = lineHeightDots * descLines.Count;
            }

            // === SCALE UP DESCRIPTION TO FILL AVAILABLE SPACE (WIDTH AND HEIGHT INDEPENDENTLY) ===
            // Measure the widest line to determine width scale potential
            int widestLineWidthDots = 0;
            foreach (var line in descLines)
            {
                var (lineWidth, _) = ZplGfaRenderer.MeasureTextDots(
                    line, fontFamily, descFontPt, dpi, 0, bold: true,
                    descWidthScale, descHeightScale);
                if (lineWidth > widestLineWidthDots)
                    widestLineWidthDots = lineWidth;
            }

            if (widestLineWidthDots > 0 && totalDescriptionHeight > 0)
            {
                // Calculate potential scale-up factors independently (use description-specific width)
                float widthScaleUp = (float)descriptionUsableWidth / widestLineWidthDots;
                float heightScaleUp = (float)availableHeight / totalDescriptionHeight;

                // Scale width independently - try progressively smaller scales until one fits
                if (widthScaleUp > 1.0f)
                {
                    // Cap initial attempt at 1.5x max
                    float attemptedWidthScale = Math.Min(widthScaleUp, 1.5f);

                    // Try progressively smaller scales until one fits
                    while (attemptedWidthScale > 1.0f)
                    {
                        bool allLinesFit = true;
                        foreach (var line in descLines)
                        {
                            var (scaledWidth, _) = ZplGfaRenderer.MeasureTextDots(
                                line, fontFamily, descFontPt, dpi, 0, bold: true,
                                descWidthScale * attemptedWidthScale, descHeightScale);
                            if (scaledWidth > descriptionUsableWidth)
                            {
                                allLinesFit = false;
                                break;
                            }
                        }

                        if (allLinesFit)
                        {
                            descWidthScale *= attemptedWidthScale;
                            break;  // Found a working scale
                        }

                        attemptedWidthScale -= 0.05f;  // Try 5% smaller
                    }
                }

                // Scale height independently if there's room to grow
                if (heightScaleUp > 1.05f)  // At least 5% room to grow
                {
                    // Apply scale-up directly (capped at 1.4x to avoid overly tall text)
                    float appliedHeightScale = Math.Min(heightScaleUp * 0.95f, 1.4f);  // 95% of available to leave margin

                    // Verify it actually fits
                    float testHeightScale = descHeightScale * appliedHeightScale;
                    int testLineHeight = (int)(descFontPt * testHeightScale * lineHeightFactor * dpi / 72.0);
                    int testTotalHeight = testLineHeight * descLines.Count;

                    if (testTotalHeight <= availableHeight)
                    {
                        descHeightScale = testHeightScale;
                        lineHeightDots = testLineHeight;
                        System.Diagnostics.Debug.WriteLine($"[ZPL] Height scaled up by {appliedHeightScale:F2}x, new descHeightScale={descHeightScale:F2}");
                    }
                }
            }

            // Line spacing for rendering (slightly tighter than full line height)
            int lineSpacingDots = (int)(lineHeightDots * 0.85);

            int lineY = descriptionY;

            // Render each line centered (use description-specific width for edge protection)
            foreach (var line in descLines)
            {
                string gfaCommand = ZplGfaRenderer.RenderTextAsGfa(
                    line,
                    fontFamily,
                    descFontPt,
                    descriptionUsableWidth,
                    dpi,
                    isRtl: isRtl,
                    xPosition: centerX,
                    yPosition: lineY,
                    alignment: TextAlignment.Center,
                    bold: true,
                    widthScale: descWidthScale,
                    heightScale: descHeightScale);

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
