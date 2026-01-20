using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;

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

    private static SizeF MeasureText(string text, double fontSize, string fontFamily)
    {
        try
        {
            // Use printer DPI for accurate measurement
            const int printerDpi = 203;
            int estimatedSize = (int)(fontSize * printerDpi / 72.0 * 2);

            using var bitmap = new Bitmap(estimatedSize, estimatedSize, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);

            // Match ZplGfaRenderer settings for consistency
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            graphics.PageUnit = GraphicsUnit.Pixel;

            using var font = new Font(fontFamily, (float)fontSize, FontStyle.Regular, GraphicsUnit.Point);
            var size = graphics.MeasureString(text, font);

            // Scale back to mm-like units for existing callers (96 DPI compatibility)
            float scaleFactor = 96.0f / printerDpi;
            return new SizeF(size.Width * scaleFactor, size.Height * scaleFactor);
        }
        catch
        {
            // Fallback: approximate size
            return new SizeF((float)(text.Length * fontSize * 0.6), (float)(fontSize * 1.2));
        }
    }
}
