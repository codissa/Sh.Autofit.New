using System.Text;
using Sh.Autofit.StickerPrinting.Helpers;
using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Tsc;

/// <summary>
/// Generates TSPL commands for TSC printers
/// Handles text, RTL support, multi-line text, and font sizing
/// </summary>
public class TsplCommandGenerator : ITsplCommandGenerator
{
    public string GenerateLabelCommands(LabelData labelData, StickerSettings settings)
    {
        var sb = new StringBuilder();

        // Calculate label dimensions in dots
        int widthDots = MmToDots(settings.WidthMm, settings.DPI);
        int heightDots = MmToDots(settings.HeightMm, settings.DPI);

        // Label initialization
        sb.AppendLine($"SIZE {settings.WidthMm:F1} mm, {settings.HeightMm:F1} mm");
        sb.AppendLine("GAP 2 mm, 0 mm");
        sb.AppendLine("DIRECTION 1");
        sb.AppendLine("REFERENCE 0,0");
        sb.AppendLine("OFFSET 0 mm");
        sb.AppendLine("SET PEEL OFF");
        sb.AppendLine("SET CUTTER OFF");
        sb.AppendLine("SET TEAR ON");
        sb.AppendLine("CLS"); // Clear label buffer

        // Calculate positions (in dots)
        int introX = MmToDots(settings.LeftMargin, settings.DPI);
        int introY = MmToDots(settings.TopMargin, settings.DPI);

        int itemKeyX = widthDots / 2; // Center
        int itemKeyY = MmToDots(settings.HeightMm * 0.35, settings.DPI);

        int descriptionX = widthDots / 2; // Center
        int descriptionY = MmToDots(settings.HeightMm * 0.6, settings.DPI);

        // Line 1: Intro text (small, left-aligned)
        if (!string.IsNullOrWhiteSpace(labelData.IntroLine))
        {
            sb.AppendLine($"TEXT {introX},{introY},\"{settings.DefaultFontName}\",0,1,1,\"{EscapeTspl(labelData.IntroLine)}\"");
        }

        // Line 2: Item Key (center, larger, bold)
        sb.AppendLine($"TEXT {itemKeyX},{itemKeyY},\"{settings.DefaultFontName}\",0,2,2,\"{EscapeTspl(labelData.ItemKey)}\"");

        // Line 3: Description (if applicable)
        if (labelData.ShouldShowDescription && !string.IsNullOrWhiteSpace(labelData.Description))
        {
            bool isRtl = labelData.IsArabic || labelData.IsHebrew;
            double availableWidthMm = settings.WidthMm - settings.LeftMargin - settings.RightMargin;

            // Split into lines if needed
            var lines = FontSizeCalculator.SplitTextToFit(
                labelData.Description,
                availableWidthMm,
                labelData.FontSize,
                labelData.FontFamily);

            int lineY = descriptionY;
            int lineSpacing = (int)(labelData.FontSize * 1.2 * settings.DPI / 72.0);

            foreach (var line in lines)
            {
                var textToRender = isRtl ? ReverseForRtl(line) : line;
                var fontName = GetFontName(isRtl, labelData.Language, settings);

                // TEXT command: TEXT x, y, font, rotation, x-mul, y-mul, "content"
                sb.AppendLine($"TEXT {descriptionX},{lineY},\"{fontName}\",0,1,1,\"{EscapeTspl(textToRender)}\"");

                lineY += lineSpacing;
            }
        }

        // Print command
        sb.AppendLine("PRINT 1,1"); // Print 1 copy, 1 label per set

        return sb.ToString();
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

    private string EscapeTspl(string text)
    {
        // Escape quotes and backslashes for TSPL
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private string ReverseForRtl(string text)
    {
        // Simple character reversal for RTL text
        // NOTE: This is a simplified approach. For complex Arabic/Hebrew with diacritics,
        // you may need a BiDi algorithm library
        return new string(text.Reverse().ToArray());
    }

    private string GetFontName(bool isRtl, string language, StickerSettings settings)
    {
        if (isRtl)
        {
            return language == "ar" ? settings.ArabicFontName : settings.HebrewFontName;
        }
        return settings.DefaultFontName;
    }
}
