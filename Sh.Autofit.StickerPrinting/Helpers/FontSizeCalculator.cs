using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Sh.Autofit.StickerPrinting.Helpers;

public static class FontSizeCalculator
{
    /// <summary>
    /// Calculate optimal font size to fit text within bounds
    /// </summary>
    public static double CalculateOptimalFontSize(
        string text,
        double maxWidthMm,
        double maxHeightMm,
        double initialFontSize,
        string fontFamily = "Arial")
    {
        if (string.IsNullOrWhiteSpace(text))
            return initialFontSize;

        // Convert mm to pixels (approximate at 96 DPI)
        double maxWidthPx = maxWidthMm * 3.7795; // ~96 DPI conversion
        double maxHeightPx = maxHeightMm * 3.7795;

        double fontSize = initialFontSize;

        while (fontSize > 6) // Minimum readable size
        {
            var size = MeasureText(text, fontSize, fontFamily);

            if (size.Width <= maxWidthPx && size.Height <= maxHeightPx)
                return fontSize;

            fontSize -= 0.5;
        }

        return 6; // Minimum
    }

    /// <summary>
    /// Split text into multiple lines if too long
    /// </summary>
    public static List<string> SplitTextToFit(
        string text,
        double maxWidthMm,
        double fontSize,
        string fontFamily = "Arial")
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return lines;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = string.Empty;
        double maxWidthPx = maxWidthMm * 3.7795;

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine)
                ? word
                : $"{currentLine} {word}";

            var size = MeasureText(testLine, fontSize, fontFamily);

            if (size.Width > maxWidthPx && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        return lines;
    }

    private static Size MeasureText(string text, double fontSize, string fontFamily)
    {
        try
        {
            var typeface = new Typeface(new FontFamily(fontFamily), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

            // Use a reasonable DPI value for measurement
            double dpiScale = 96.0 / 96.0;  // Standard DPI scale

            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                dpiScale);

            return new Size(formattedText.Width, formattedText.Height);
        }
        catch
        {
            // Fallback: approximate size
            return new Size(text.Length * fontSize * 0.6, fontSize * 1.2);
        }
    }
}
