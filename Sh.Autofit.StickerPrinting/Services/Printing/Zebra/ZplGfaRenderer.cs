using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Zebra;

/// <summary>
/// Text alignment options for GFA rendering
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Renders text as bitmap images and converts to ZPL ^GFA commands
/// Used for Hebrew/Arabic text that Zebra built-in fonts cannot render properly
/// </summary>
public static class ZplGfaRenderer
{
    /// <summary>
    /// Render text as a bitmap and return ZPL ^GFA command
    /// </summary>
    /// <param name="text">Text to render (Hebrew/Arabic/mixed)</param>
    /// <param name="fontName">Font family (e.g., "Arial", "Arial Narrow")</param>
    /// <param name="fontSizePt">Font size in points</param>
    /// <param name="maxWidthDots">Maximum width in dots (for text wrapping)</param>
    /// <param name="dpi">Printer DPI (203 for Zebra S4M)</param>
    /// <param name="isRtl">True for RTL languages (Hebrew/Arabic)</param>
    /// <param name="xPosition">X position in dots (meaning depends on alignment)</param>
    /// <param name="yPosition">Y position in dots</param>
    /// <param name="alignment">Text alignment (Left: xPosition is left edge, Center: xPosition is center, Right: xPosition is right edge)</param>
    /// <param name="bold">Use bold font style</param>
    /// <param name="widthScale">Width scaling factor (1.0 = normal, &lt;1.0 = compressed, &gt;1.0 = expanded)</param>
    /// <param name="heightScale">Height scaling factor (1.0 = normal, &lt;1.0 = shorter, &gt;1.0 = taller)</param>
    /// <returns>ZPL command string with ^FO and ^GFA</returns>
    public static string RenderTextAsGfa(
        string text,
        string fontName,
        float fontSizePt,
        int maxWidthDots,
        int dpi,
        bool isRtl,
        int xPosition,
        int yPosition,
        TextAlignment alignment = TextAlignment.Left,
        bool bold = false,
        float widthScale = 1.0f,
        float heightScale = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        try
        {
            // Create bitmap with sufficient size
            // Convert points to pixels: pixels = points * dpi / 72
            int fontSize = (int)Math.Ceiling(fontSizePt * dpi / 72.0);

            // Estimate height (1.5x font size for descenders/ascenders)
            int estimatedHeight = (int)(fontSize * 1.5);

            // Adjust bitmap size for scaling to ensure proper rendering space
            int bitmapWidth = (int)Math.Ceiling(maxWidthDots / Math.Min(widthScale, 1.0f));
            int bitmapHeight = (int)Math.Ceiling(estimatedHeight / Math.Min(heightScale, 1.0f));

            using var bitmap = new Bitmap(bitmapWidth, bitmapHeight, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);


            // High-quality rendering settings for monochrome output
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;

            // White background (printer prints black)
            graphics.Clear(Color.White);

            // Apply width/height scaling transform before drawing
            if (widthScale != 1.0f || heightScale != 1.0f)
            {
                graphics.ScaleTransform(widthScale, heightScale);
            }

            // Create font and string format
            FontStyle fontStyle = bold ? FontStyle.Bold : FontStyle.Regular;
            using var font = new Font(fontName, fontSizePt, fontStyle, GraphicsUnit.Point);
            using var format = new StringFormat();

            // Set RTL direction if needed
            if (isRtl)
            {
                format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            }

            // Always draw left-aligned; centering/right-alignment is handled by final X position calculation
            // This ensures the cropped bitmap's content starts at x=0, making position calculations accurate
            format.Alignment = StringAlignment.Near;

            format.LineAlignment = StringAlignment.Near;
            format.Trimming = StringTrimming.None;
            format.FormatFlags |= StringFormatFlags.NoWrap;  // Prevent DrawString from re-wrapping pre-split lines

            // Draw text (use unscaled dimensions since we applied ScaleTransform)
            using var brush = new SolidBrush(Color.Black);
            float drawWidth = widthScale != 1.0f ? maxWidthDots / widthScale : maxWidthDots;
            float drawHeight = heightScale != 1.0f ? estimatedHeight / heightScale : estimatedHeight;
            var rect = new RectangleF(0, 0, drawWidth, drawHeight);
            graphics.DrawString(text, font, brush, rect, format);

            // Measure actual text size to crop bitmap
            var textSize = graphics.MeasureString(text, font, (int)drawWidth, format);
            //int actualWidth = (int)Math.Ceiling(textSize.Width);
            //int actualHeight = (int)Math.Ceiling(textSize.Height);
            //todo remove once debugged
            //bitmap.Save(@"c:\temp\zpl_debug.png", ImageFormat.Png);
            // Crop bitmap to actual size
            // using var croppedBitmap = CropBitmap(bitmap, actualWidth, actualHeight);
            //todo remove once debugged
            using var croppedBitmap = CropToInk(bitmap);
            int actualWidth = croppedBitmap.Width;
            int actualHeight = croppedBitmap.Height;
            //string gfaData = ConvertToGfaFormat(croppedBitmap);
            //croppedBitmap.Save(@"C:\temp\zpl_debug_cropped.png", ImageFormat.Png);
            // Convert to ZPL GFA format
            string gfaData = ConvertToGfaFormat(croppedBitmap);

            // Calculate final X position based on alignment
            int finalX = alignment switch
            {
                TextAlignment.Left => xPosition,
                TextAlignment.Center => xPosition - (actualWidth / 2),
                TextAlignment.Right => xPosition - actualWidth,
                _ => xPosition
            };

            // Return complete ZPL command (^FT for baseline positioning, matches legacy)
            return $"^FO{finalX},{yPosition}\n{gfaData}\n";
        }
        catch (Exception ex)
        {
            // Fallback: return empty (caller should handle with native ZPL font)
            System.Diagnostics.Debug.WriteLine($"GFA rendering failed: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Check if text contains Hebrew or Arabic characters
    /// </summary>
    public static bool ContainsHebrewOrArabic(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        return text.Any(c =>
            (c >= 0x0590 && c <= 0x05FF) ||  // Hebrew
            (c >= 0x0600 && c <= 0x06FF) ||  // Arabic
            (c >= 0x0750 && c <= 0x077F) ||  // Arabic Supplement
            (c >= 0x08A0 && c <= 0x08FF));   // Arabic Extended-A
    }

    /// <summary>
    /// Measure text dimensions in printer dots using Windows Forms measurement
    /// </summary>
    /// <param name="text">Text to measure</param>
    /// <param name="fontName">Font family name (e.g., "Arial", "Arial Narrow")</param>
    /// <param name="fontSizePt">Font size in points</param>
    /// <param name="dpi">Printer DPI (203 for Zebra S4M)</param>
    /// <param name="maxWidthDots">Maximum width for text wrapping (0 = no wrap)</param>
    /// <param name="bold">Use bold font style</param>
    /// <param name="widthScale">Width scaling factor (1.0 = normal)</param>
    /// <param name="heightScale">Height scaling factor (1.0 = normal)</param>
    /// <returns>Size in dots (width, height)</returns>
    public static (int widthDots, int heightDots) MeasureTextDots(
        string text,
        string fontName,
        float fontSizePt,
        int dpi,
        int maxWidthDots = 0,
        bool bold = false,
        float widthScale = 1.0f,
        float heightScale = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (0, 0);

        try
        {
            // Match RenderTextAsGfa exactly: render text and measure actual ink bounds
            // This ensures MeasureTextDots returns the same dimensions as the rendered bitmap

            // Convert points to pixels at printer DPI (same as RenderTextAsGfa line 62)
            int fontSize = (int)Math.Ceiling(fontSizePt * dpi / 72.0);
            int estimatedHeight = (int)(fontSize * 1.5);

            // Calculate bitmap size (same logic as RenderTextAsGfa)
            int bitmapWidth = maxWidthDots > 0 ? maxWidthDots : (int)Math.Ceiling(fontSize * text.Length * 0.8);
            bitmapWidth = (int)Math.Ceiling(bitmapWidth / Math.Min(widthScale, 1.0f));
            int bitmapHeight = (int)Math.Ceiling(estimatedHeight / Math.Min(heightScale, 1.0f));

            using var bitmap = new Bitmap(bitmapWidth, bitmapHeight, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);

            // Match rendering settings from RenderTextAsGfa exactly
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
            graphics.Clear(Color.White);

            // Apply scaling transform (same as RenderTextAsGfa)
            if (widthScale != 1.0f || heightScale != 1.0f)
            {
                graphics.ScaleTransform(widthScale, heightScale);
            }

            FontStyle fontStyle = bold ? FontStyle.Bold : FontStyle.Regular;
            using var font = new Font(fontName, fontSizePt, fontStyle, GraphicsUnit.Point);
            using var format = new StringFormat();
            format.Alignment = StringAlignment.Near;
            format.LineAlignment = StringAlignment.Near;
            format.Trimming = StringTrimming.None;

            if (maxWidthDots > 0)
            {
                format.FormatFlags = StringFormatFlags.LineLimit;
            }
            else
            {
                format.FormatFlags = StringFormatFlags.NoWrap;
            }

            // Draw text (same as RenderTextAsGfa)
            using var brush = new SolidBrush(Color.Black);
            float drawWidth = widthScale != 1.0f ? bitmapWidth / widthScale : bitmapWidth;
            float drawHeight = heightScale != 1.0f ? estimatedHeight / heightScale : estimatedHeight;
            var rect = new RectangleF(0, 0, drawWidth, drawHeight);
            graphics.DrawString(text, font, brush, rect, format);

            // Measure actual ink bounds (same as RenderTextAsGfa uses CropToInk)
            var (minX, minY, maxX, maxY) = GetInkBounds(bitmap);

            if (maxX < 0) // Nothing drawn
                return (0, 0);

            int actualWidth = maxX - minX + 1;
            int actualHeight = maxY - minY + 1;

            return (actualWidth, actualHeight);
        }
        catch (Exception ex)
        {
            // Fallback: crude estimation
            System.Diagnostics.Debug.WriteLine($"MeasureTextDots failed: {ex.Message}");
            int fallbackWidth = (int)(text.Length * fontSizePt * dpi / 72.0 * 0.6);
            int fallbackHeight = (int)(fontSizePt * dpi / 72.0 * 1.2);
            return (fallbackWidth, fallbackHeight);
        }
    }

    /// <summary>
    /// Get ink bounds without creating a new bitmap (for measurement only)
    /// </summary>
    private static (int minX, int minY, int maxX, int maxY) GetInkBounds(Bitmap source)
    {
        int minX = source.Width, minY = source.Height, maxX = -1, maxY = -1;

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                var p = source.GetPixel(x, y);
                bool isInk = p.A > 0 && (p.R + p.G + p.B) < (250 * 3);
                if (!isInk) continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Find largest font size (in points) that fits text within specified width
    /// Returns startFontPt if text fits, otherwise shrinks down to minFontPt
    /// </summary>
    /// <param name="text">Text to fit</param>
    /// <param name="fontName">Font family name (e.g., "Arial", "Arial Narrow")</param>
    /// <param name="maxWidthDots">Maximum width in dots</param>
    /// <param name="dpi">Printer DPI (203)</param>
    /// <param name="startFontPt">Starting/desired font size in points (e.g., 18.0)</param>
    /// <param name="minFontPt">Minimum font size in points (e.g., 12.0)</param>
    /// <param name="bold">Use bold font style</param>
    /// <param name="widthScale">Width scaling factor (1.0 = normal)</param>
    /// <param name="heightScale">Height scaling factor (1.0 = normal)</param>
    /// <returns>Optimal font size in points (largest that fits)</returns>
    public static float FitFontPtToWidth(
        string text,
        string fontName,
        int maxWidthDots,
        int dpi,
        float startFontPt,
        float minFontPt,
        bool bold = false,
        float widthScale = 1.0f,
        float heightScale = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return startFontPt;

        // First check if start size fits
        var (startWidth, _) = MeasureTextDots(text, fontName, startFontPt, dpi, 0, bold, widthScale, heightScale);
        if (startWidth <= maxWidthDots)
        {
            // Perfect! Use the desired size
            return startFontPt;
        }

        // Start size too big, use linear search downward (simpler and more predictable)
        float fontSize = startFontPt - 0.5f;
        while (fontSize >= minFontPt)
        {
            var (width, _) = MeasureTextDots(text, fontName, fontSize, dpi, 0, bold, widthScale, heightScale);
            if (width <= maxWidthDots)
            {
                return fontSize;
            }
            fontSize -= 0.5f;
        }

        // Text too long even at minimum
        return minFontPt;
    }

    /// <summary>
    /// Find smallest width scale that fits text at a fixed font size
    /// Used for intro line which must stay at constant font size but compress horizontally
    /// </summary>
    /// <param name="text">Text to fit</param>
    /// <param name="fontName">Font family name (e.g., "Arial Narrow")</param>
    /// <param name="fontSizePt">Fixed font size in points (e.g., 28.0)</param>
    /// <param name="maxWidthDots">Maximum width in dots</param>
    /// <param name="dpi">Printer DPI (203)</param>
    /// <param name="bold">Use bold font style</param>
    /// <param name="minWidthScale">Minimum width scale (0.5 = 50% compression limit)</param>
    /// <returns>Optimal width scale (1.0 = normal, lower = more compressed)</returns>
    public static float FitWidthScaleToWidth(
        string text,
        string fontName,
        float fontSizePt,
        int maxWidthDots,
        int dpi,
        bool bold = false,
        float minWidthScale = 0.5f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 1.0f;

        // Try normal width first (no compression)
        var (normalWidth, _) = MeasureTextDots(text, fontName, fontSizePt, dpi, 0, bold, widthScale: 1.0f, heightScale: 1.0f);
        if (normalWidth <= maxWidthDots)
            return 1.0f;  // Fits without compression!

        // Need compression - search from 0.95 down to minWidthScale in 5% increments
        float widthScale = 0.95f;
        while (widthScale >= minWidthScale)
        {
            var (width, _) = MeasureTextDots(text, fontName, fontSizePt, dpi, 0, bold, widthScale, heightScale: 1.0f);
            if (width <= maxWidthDots)
                return widthScale;

            widthScale -= 0.05f;  // Reduce by 5% increments
        }

        // Maximum compression - text still might not fit but this is the best we can do
        return minWidthScale;
    }

    /// <summary>
    /// Crop bitmap to actual content size
    /// </summary>
    private static Bitmap CropBitmap(Bitmap source, int width, int height)
    {
        width = Math.Min(width, source.Width);
        height = Math.Min(height, source.Height);

        // Ensure minimum size
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var cropped = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(cropped);
        g.DrawImage(source, 0, 0, new Rectangle(0, 0, width, height), GraphicsUnit.Pixel);
        return cropped;
    }

    /// <summary>
    /// Crop bitmap to actual content size with ink bounds 
    /// </summary>

    private static Bitmap CropToInk(Bitmap source, int padding = 1)
    {
        int minX = source.Width, minY = source.Height, maxX = -1, maxY = -1;

        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                var p = source.GetPixel(x, y);

                // treat near-white as background; keep alpha in mind
                bool isInk = p.A > 0 && (p.R + p.G + p.B) < (250 * 3);
                if (!isInk) continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

        if (maxX < 0) // nothing drawn
            return new Bitmap(1, 1, PixelFormat.Format32bppArgb);

        minX = Math.Max(0, minX - padding);
        minY = Math.Max(0, minY - padding);
        maxX = Math.Min(source.Width - 1, maxX + padding);
        maxY = Math.Min(source.Height - 1, maxY + padding);

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;

        var cropped = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(cropped);
        g.Clear(Color.White);
        g.DrawImage(source,
            new Rectangle(0, 0, w, h),
            new Rectangle(minX, minY, w, h),
            GraphicsUnit.Pixel);
        return cropped;
    }


    /// <summary>
    /// Convert bitmap to ZPL ^GFA format (ASCII hex)
    /// </summary>
    private static string ConvertToGfaFormat(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        // Calculate bytes per row (8 pixels per byte, rounded up)
        int bytesPerRow = (width + 7) / 8;
        int totalBytes = bytesPerRow * height;

        var sb = new StringBuilder();

        // ^GFA command format: ^GFA,totalBytes,totalBytes,bytesPerRow,data
        sb.Append($"^GFA,{totalBytes},{totalBytes},{bytesPerRow},");

        // Convert pixels to hex bytes
        for (int y = 0; y < height; y++)
        {
            byte currentByte = 0;
            int bitPosition = 0;

            for (int x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);

                // Black pixel = 1, White pixel = 0 (for printing)
                // Threshold: if average RGB < 128, consider it black
                bool isBlack = (pixel.R + pixel.G + pixel.B) < 384; // 128 * 3

                if (isBlack)
                {
                    currentByte |= (byte)(1 << (7 - bitPosition));
                }

                bitPosition++;

                // Write byte every 8 pixels
                if (bitPosition == 8)
                {
                    sb.Append(currentByte.ToString("X2"));
                    currentByte = 0;
                    bitPosition = 0;
                }
            }

            // Write remaining bits if row width not multiple of 8
            if (bitPosition > 0)
            {
                sb.Append(currentByte.ToString("X2"));
            }
        }

        sb.Append("^FS");
        return sb.ToString();
    }
}
