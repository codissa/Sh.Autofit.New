using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Sh.Autofit.StickerPrinting.Helpers;
using Sh.Autofit.StickerPrinting.Models;

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
    #region Caching Infrastructure

    /// <summary>
    /// Cache key for text measurements - uses value equality
    /// </summary>
    private readonly struct MeasurementCacheKey : IEquatable<MeasurementCacheKey>
    {
        public readonly string Text;
        public readonly string FontName;
        public readonly int FontSizePtX100; // Store as int (fontSizePt * 100) for reliable equality
        public readonly int Dpi;
        public readonly int MaxWidthDots;
        public readonly bool Bold;
        public readonly int WidthScaleX1000;
        public readonly int HeightScaleX1000;

        public MeasurementCacheKey(string text, string fontName, float fontSizePt, int dpi,
            int maxWidthDots, bool bold, float widthScale, float heightScale)
        {
            Text = text;
            FontName = fontName;
            FontSizePtX100 = (int)(fontSizePt * 100);
            Dpi = dpi;
            MaxWidthDots = maxWidthDots;
            Bold = bold;
            WidthScaleX1000 = (int)(widthScale * 1000);
            HeightScaleX1000 = (int)(heightScale * 1000);
        }

        public bool Equals(MeasurementCacheKey other) =>
            Text == other.Text &&
            FontName == other.FontName &&
            FontSizePtX100 == other.FontSizePtX100 &&
            Dpi == other.Dpi &&
            MaxWidthDots == other.MaxWidthDots &&
            Bold == other.Bold &&
            WidthScaleX1000 == other.WidthScaleX1000 &&
            HeightScaleX1000 == other.HeightScaleX1000;

        public override bool Equals(object? obj) => obj is MeasurementCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Text, FontName, FontSizePtX100, Dpi, MaxWidthDots, Bold, WidthScaleX1000, HeightScaleX1000);
    }

    /// <summary>
    /// Cache key for rendered GFA commands
    /// </summary>
    private readonly struct GfaCacheKey : IEquatable<GfaCacheKey>
    {
        public readonly string Text;
        public readonly string FontName;
        public readonly int FontSizePtX100;
        public readonly int MaxWidthDots;
        public readonly int Dpi;
        public readonly bool IsRtl;
        public readonly bool Bold;
        public readonly int WidthScaleX1000;
        public readonly int HeightScaleX1000;

        public GfaCacheKey(string text, string fontName, float fontSizePt, int maxWidthDots, int dpi,
            bool isRtl, bool bold, float widthScale, float heightScale)
        {
            Text = text;
            FontName = fontName;
            FontSizePtX100 = (int)(fontSizePt * 100);
            MaxWidthDots = maxWidthDots;
            Dpi = dpi;
            IsRtl = isRtl;
            Bold = bold;
            WidthScaleX1000 = (int)(widthScale * 1000);
            HeightScaleX1000 = (int)(heightScale * 1000);
        }

        public bool Equals(GfaCacheKey other) =>
            Text == other.Text &&
            FontName == other.FontName &&
            FontSizePtX100 == other.FontSizePtX100 &&
            MaxWidthDots == other.MaxWidthDots &&
            Dpi == other.Dpi &&
            IsRtl == other.IsRtl &&
            Bold == other.Bold &&
            WidthScaleX1000 == other.WidthScaleX1000 &&
            HeightScaleX1000 == other.HeightScaleX1000;

        public override bool Equals(object? obj) => obj is GfaCacheKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Text, FontName, FontSizePtX100, MaxWidthDots, Dpi, IsRtl, Bold, WidthScaleX1000);
    }

    /// <summary>
    /// Cached GFA result includes the command string and actual dimensions
    /// </summary>
    private readonly struct GfaCachedResult
    {
        public readonly string GfaCommand;
        public readonly int ActualWidth;
        public readonly int ActualHeight;

        public GfaCachedResult(string gfaCommand, int width, int height)
        {
            GfaCommand = gfaCommand;
            ActualWidth = width;
            ActualHeight = height;
        }
    }

    // Thread-safe caches with LRU-like eviction
    private static readonly ConcurrentDictionary<MeasurementCacheKey, (int width, int height)> _measurementCache = new();
    private static readonly ConcurrentDictionary<GfaCacheKey, GfaCachedResult> _gfaCache = new();

    private const int MaxMeasurementCacheSize = 2000;
    private const int MaxGfaCacheSize = 500;

    /// <summary>
    /// Clear all caches (useful for testing or when settings change)
    /// </summary>
    public static void ClearCaches()
    {
        _measurementCache.Clear();
        _gfaCache.Clear();
    }

    /// <summary>
    /// Get cache statistics for monitoring
    /// </summary>
    public static (int measurementCacheCount, int gfaCacheCount) GetCacheStats() =>
        (_measurementCache.Count, _gfaCache.Count);

    private static void TrimCacheIfNeeded<TKey, TValue>(ConcurrentDictionary<TKey, TValue> cache, int maxSize) where TKey : notnull
    {
        // Simple trim: if over 110% capacity, clear half
        // This is a simple approach; a true LRU would track access times
        if (cache.Count > maxSize * 1.1)
        {
            var keysToRemove = cache.Keys.Take(cache.Count / 2).ToList();
            foreach (var key in keysToRemove)
            {
                cache.TryRemove(key, out _);
            }
        }
    }

    #endregion

    #region Fast Pixel Access (LockBits)

    /// <summary>
    /// Extract pixel data from bitmap using LockBits for fast access
    /// Returns BGRA format (4 bytes per pixel)
    /// </summary>
    private static (byte[] pixels, int stride) GetBitmapBytes(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            int stride = bitmapData.Stride;
            int byteCount = Math.Abs(stride) * bitmap.Height;
            byte[] pixels = new byte[byteCount];

            Marshal.Copy(bitmapData.Scan0, pixels, 0, byteCount);

            return (pixels, stride);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
    }

    /// <summary>
    /// Get ink bounds using fast LockBits pixel access (10-100x faster than GetPixel)
    /// </summary>
    private static (int minX, int minY, int maxX, int maxY) GetInkBoundsFast(Bitmap source)
    {
        var (pixels, stride) = GetBitmapBytes(source);
        int width = source.Width;
        int height = source.Height;

        int minX = width, minY = height, maxX = -1, maxY = -1;

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * stride;
            for (int x = 0; x < width; x++)
            {
                int pixelOffset = rowOffset + (x * 4); // BGRA format
                byte b = pixels[pixelOffset];
                byte g = pixels[pixelOffset + 1];
                byte r = pixels[pixelOffset + 2];
                byte a = pixels[pixelOffset + 3];

                // Ink detection: alpha > 0 and RGB sum < 750 (near-white threshold)
                bool isInk = a > 0 && (r + g + b) < 750;
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
    /// Convert bitmap to ZPL ^GFA format using fast LockBits pixel access
    /// </summary>
    private static string ConvertToGfaFormatFast(Bitmap bitmap)
    {
        int width = bitmap.Width;
        int height = bitmap.Height;

        var (pixels, stride) = GetBitmapBytes(bitmap);

        int bytesPerRow = (width + 7) / 8;
        int totalBytes = bytesPerRow * height;

        // Pre-allocate StringBuilder with known capacity
        var sb = new StringBuilder(totalBytes * 2 + 50);
        sb.Append($"^GFA,{totalBytes},{totalBytes},{bytesPerRow},");

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * stride;
            byte currentByte = 0;
            int bitPosition = 0;

            for (int x = 0; x < width; x++)
            {
                int pixelOffset = rowOffset + (x * 4); // BGRA format
                byte b = pixels[pixelOffset];
                byte g = pixels[pixelOffset + 1];
                byte r = pixels[pixelOffset + 2];

                // Black if RGB sum < 384 (average < 128)
                bool isBlack = (r + g + b) < 384;

                if (isBlack)
                {
                    currentByte |= (byte)(1 << (7 - bitPosition));
                }

                bitPosition++;

                if (bitPosition == 8)
                {
                    sb.Append(currentByte.ToString("X2"));
                    currentByte = 0;
                    bitPosition = 0;
                }
            }

            if (bitPosition > 0)
            {
                sb.Append(currentByte.ToString("X2"));
            }
        }

        sb.Append("^FS");
        return sb.ToString();
    }

    /// <summary>
    /// Crop bitmap to ink bounds using fast LockBits access
    /// </summary>
    private static Bitmap CropToInkFast(Bitmap source, int padding = 3)
    {
        var (minX, minY, maxX, maxY) = GetInkBoundsFast(source);

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

    #endregion

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

        // Check GFA cache first (position-independent cache key)
        var gfaCacheKey = new GfaCacheKey(text, fontName, fontSizePt, maxWidthDots, dpi, isRtl, bold, widthScale, heightScale);
        if (_gfaCache.TryGetValue(gfaCacheKey, out var cachedGfa))
        {
            // Use cached GFA data, but calculate final position based on alignment
            int finalX = alignment switch
            {
                TextAlignment.Left => xPosition,
                TextAlignment.Center => xPosition - (cachedGfa.ActualWidth / 2),
                TextAlignment.Right => xPosition - cachedGfa.ActualWidth,
                _ => xPosition
            };
            return $"^FO{finalX},{yPosition}\n{cachedGfa.GfaCommand}\n";
        }

        try
        {
            // Create bitmap with sufficient size
            // Convert points to pixels: pixels = points * dpi / 72
            int fontSize = (int)Math.Ceiling(fontSizePt * dpi / 72.0);

            // Estimate height (1.5x font size for descenders/ascenders)
            int estimatedHeight = (int)(fontSize * 1.5);

            // Adjust bitmap size for scaling to ensure proper rendering space
            // For scale-down (< 1.0): need larger canvas to fit pre-transformed text
            // For scale-up (> 1.0): need larger canvas to fit post-transformed stretched text
            const int edgePadding = 20;
            float widthMultiplier = widthScale < 1.0f ? 1.0f / widthScale : widthScale;
            float heightMultiplier = heightScale < 1.0f ? 1.0f / heightScale : heightScale;
            int bitmapWidth = (int)Math.Ceiling(maxWidthDots * widthMultiplier) + edgePadding;
            int bitmapHeight = (int)Math.Ceiling(estimatedHeight * heightMultiplier);

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
            // For RTL text: use full bitmap width to prevent GDI+ from clipping at rectangle boundary
            // RTL with StringAlignment.Near starts at RIGHT edge and flows LEFT - can clip at rect.X
            using var brush = new SolidBrush(Color.Black);
            float drawHeight = heightScale != 1.0f ? estimatedHeight / heightScale : estimatedHeight;

            // Use full bitmap width for drawing to prevent any clipping
            // CropToInk will crop to actual ink bounds afterward
            float actualBitmapWidth = bitmapWidth / (widthScale != 1.0f ? widthScale : 1.0f);
            var rect = new RectangleF(0, 0, actualBitmapWidth, drawHeight);
            graphics.DrawString(text, font, brush, rect, format);

            // Measure actual text size to crop bitmap
            var textSize = graphics.MeasureString(text, font, (int)actualBitmapWidth, format);
            //int actualWidth = (int)Math.Ceiling(textSize.Width);
            //int actualHeight = (int)Math.Ceiling(textSize.Height);
            //todo remove once debugged
            //bitmap.Save(@"c:\temp\zpl_debug.png", ImageFormat.Png);
            // Crop bitmap to actual size
            // Use fast LockBits-based cropping (10-100x faster than GetPixel)
            using var croppedBitmap = CropToInkFast(bitmap);
            int actualWidth = croppedBitmap.Width;
            int actualHeight = croppedBitmap.Height;

            // Convert to ZPL GFA format using fast method
            string gfaData = ConvertToGfaFormatFast(croppedBitmap);

            // Cache the GFA result for future use (position-independent)
            _gfaCache[gfaCacheKey] = new GfaCachedResult(gfaData, actualWidth, actualHeight);
            TrimCacheIfNeeded(_gfaCache, MaxGfaCacheSize);

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

        // Check measurement cache first
        var cacheKey = new MeasurementCacheKey(text, fontName, fontSizePt, dpi, maxWidthDots, bold, widthScale, heightScale);
        if (_measurementCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            // Match RenderTextAsGfa exactly: render text and measure actual ink bounds
            // This ensures MeasureTextDots returns the same dimensions as the rendered bitmap

            // Convert points to pixels at printer DPI (same as RenderTextAsGfa line 62)
            int fontSize = (int)Math.Ceiling(fontSizePt * dpi / 72.0);
            int estimatedHeight = (int)(fontSize * 1.5);

            // Calculate bitmap size (same logic as RenderTextAsGfa)
            // For scale-down (< 1.0): need larger canvas to fit pre-transformed text
            // For scale-up (> 1.0): need larger canvas to fit post-transformed stretched text
            float widthMultiplier = widthScale < 1.0f ? 1.0f / widthScale : widthScale;
            float heightMultiplier = heightScale < 1.0f ? 1.0f / heightScale : heightScale;
            int bitmapWidth = maxWidthDots > 0 ? maxWidthDots : (int)Math.Ceiling(fontSize * text.Length * 0.8);
            bitmapWidth = (int)Math.Ceiling(bitmapWidth * widthMultiplier);
            int bitmapHeight = (int)Math.Ceiling(estimatedHeight * heightMultiplier);

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

            // Measure actual ink bounds using fast LockBits method
            var (minX, minY, maxX, maxY) = GetInkBoundsFast(bitmap);

            if (maxX < 0) // Nothing drawn
                return (0, 0);

            int actualWidth = maxX - minX + 1;
            int actualHeight = maxY - minY + 1;

            var result = (actualWidth, actualHeight);

            // Cache the result and trim if needed
            _measurementCache[cacheKey] = result;
            TrimCacheIfNeeded(_measurementCache, MaxMeasurementCacheSize);

            return result;
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

        // Use binary search instead of linear search (reduces iterations from ~40 to ~6)
        float low = minFontPt;
        float high = startFontPt;
        float bestFit = minFontPt;

        // Tolerance: 0.5pt precision (matching original step size)
        const float tolerance = 0.5f;

        while (high - low > tolerance)
        {
            float mid = (low + high) / 2f;
            // Round to 0.5pt increments for consistency
            mid = (float)Math.Round(mid * 2) / 2f;

            var (width, _) = MeasureTextDots(text, fontName, mid, dpi, 0, bold, widthScale, heightScale);

            if (width <= maxWidthDots)
            {
                bestFit = mid;
                low = mid + tolerance; // Try larger
            }
            else
            {
                high = mid - tolerance; // Try smaller
            }
        }

        return bestFit;
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
    /// <param name="baseWidthScale">Base width scale to apply (e.g., 0.8 for 80% condensed baseline)</param>
    /// <returns>Optimal width scale (includes baseWidthScale, ready for rendering)</returns>
    public static float FitWidthScaleToWidth(
        string text,
        string fontName,
        float fontSizePt,
        int maxWidthDots,
        int dpi,
        bool bold = false,
        float minWidthScale = 0.5f,
        float baseWidthScale = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return baseWidthScale;

        // Try base width scale first (no additional compression)
        var (normalWidth, _) = MeasureTextDots(text, fontName, fontSizePt, dpi, 0, bold, widthScale: baseWidthScale, heightScale: 1.0f);
        if (normalWidth <= maxWidthDots)
            return baseWidthScale;  // Fits with just base scale!

        // Use binary search to find optimal scale between minWidthScale and baseWidthScale
        float low = minWidthScale;
        float high = baseWidthScale;
        float bestFit = minWidthScale;

        // Tolerance: 0.05 precision (matching original step size)
        const float tolerance = 0.05f;

        while (high - low > tolerance)
        {
            float mid = (low + high) / 2f;
            // Round to 0.05 increments for consistency
            mid = (float)Math.Round(mid * 20) / 20f;

            var (width, _) = MeasureTextDots(text, fontName, fontSizePt, dpi, 0, bold, mid, heightScale: 1.0f);

            if (width <= maxWidthDots)
            {
                bestFit = mid;
                low = mid + tolerance; // Try larger scale (less compression)
            }
            else
            {
                high = mid - tolerance; // Try smaller scale (more compression)
            }
        }

        return bestFit;
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

    private static Bitmap CropToInk(Bitmap source, int padding = 3)
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

    #region Label Preview Rendering

    /// <summary>
    /// Render a single text element to a bitmap (extracted from RenderTextAsGfa for reuse)
    /// Returns the cropped bitmap containing the rendered text
    /// </summary>
    public static Bitmap? RenderTextToBitmap(
        string text,
        string fontName,
        float fontSizePt,
        int maxWidthDots,
        int dpi,
        bool isRtl,
        bool bold = false,
        float widthScale = 1.0f,
        float heightScale = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            // Convert points to pixels: pixels = points * dpi / 72
            int fontSize = (int)Math.Ceiling(fontSizePt * dpi / 72.0);
            int estimatedHeight = (int)(fontSize * 1.5);

            const int edgePadding = 20;
            int bitmapWidth = (int)Math.Ceiling(maxWidthDots / Math.Min(widthScale, 1.0f)) + edgePadding;
            int bitmapHeight = (int)Math.Ceiling(estimatedHeight / Math.Min(heightScale, 1.0f));

            using var bitmap = new Bitmap(bitmapWidth, bitmapHeight, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);

            // Match rendering settings from RenderTextAsGfa exactly
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
            graphics.Clear(Color.White);

            if (widthScale != 1.0f || heightScale != 1.0f)
            {
                graphics.ScaleTransform(widthScale, heightScale);
            }

            FontStyle fontStyle = bold ? FontStyle.Bold : FontStyle.Regular;
            using var font = new Font(fontName, fontSizePt, fontStyle, GraphicsUnit.Point);
            using var format = new StringFormat();

            if (isRtl)
            {
                format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            }

            format.Alignment = StringAlignment.Near;
            format.LineAlignment = StringAlignment.Near;
            format.Trimming = StringTrimming.None;
            format.FormatFlags |= StringFormatFlags.NoWrap;

            using var brush = new SolidBrush(Color.Black);
            float drawHeight = heightScale != 1.0f ? estimatedHeight / heightScale : estimatedHeight;
            float actualBitmapWidth = bitmapWidth / (widthScale != 1.0f ? widthScale : 1.0f);
            var rect = new RectangleF(0, 0, actualBitmapWidth, drawHeight);
            graphics.DrawString(text, font, brush, rect, format);

            // Return cropped bitmap
            return CropToInkFast(bitmap);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RenderTextToBitmap failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Calculate all layout positions and scales for a label.
    /// This is the single source of truth - both preview and print use this.
    /// </summary>
    public static LabelLayout CalculateLayout(LabelData labelData, StickerSettings settings, int labelWidthDots = 0)
    {
        var layout = new LabelLayout();
        int dpi = settings.DPI;

        // Use provided width or calculate from settings
        layout.Dpi = dpi;
        layout.LabelWidthDots = labelWidthDots > 0 ? labelWidthDots : MmToDots(settings.LabelWidthMm, dpi);
        layout.LabelHeightDots = MmToDots(settings.LabelHeightMm, dpi);

        // Calculate margins and usable width
        layout.LeftMarginDots = MmToDots(settings.LeftMargin, dpi);
        int rightMarginDots = MmToDots(settings.RightMargin, dpi);
        layout.UsableWidthDots = layout.LabelWidthDots - layout.LeftMarginDots - rightMarginDots;
        layout.DescriptionUsableWidth = layout.UsableWidthDots - MmToDots(0.6, dpi);
        layout.CenterX = layout.LabelWidthDots / 2;

        // ===== 1. IntroLine =====
        layout.IntroY = MmToDots(settings.TopMargin, dpi);
        layout.IntroFontPt = settings.IntroStartFontPt;
        layout.IntroHeightScale = settings.IntroFontHeightScale;
        layout.HasIntroLine = !string.IsNullOrWhiteSpace(labelData.IntroLine);

        if (layout.HasIntroLine)
        {
            layout.IntroWidthScale = FitWidthScaleToWidth(
                labelData.IntroLine,
                settings.IntroFontFamily,
                layout.IntroFontPt,
                layout.UsableWidthDots,
                dpi,
                bold: settings.IntroBold,
                minWidthScale: settings.IntroMinWidthScale,
                baseWidthScale: settings.IntroFontWidthScale);

            var (_, introHeight) = MeasureTextDots(
                labelData.IntroLine, settings.IntroFontFamily, layout.IntroFontPt, dpi, 0,
                bold: settings.IntroBold, layout.IntroWidthScale, layout.IntroHeightScale);
            layout.IntroHeightDots = introHeight;
        }
        else
        {
            layout.IntroWidthScale = settings.IntroFontWidthScale;
            layout.IntroHeightDots = 0;
        }

        // ===== 2. ItemKey =====
        layout.HasItemKey = !string.IsNullOrWhiteSpace(labelData.ItemKey);

        if (layout.HasIntroLine && layout.IntroHeightDots > 0)
        {
            layout.ItemKeyY = layout.IntroY + layout.IntroHeightDots + MmToDots(1, dpi);
        }
        else
        {
            layout.ItemKeyY = MmToDots(settings.LabelHeightMm * 0.25, dpi);
        }

        if (layout.HasItemKey)
        {
            bool hasExtraSpace = !labelData.ShouldShowDescription ||
                                 string.IsNullOrWhiteSpace(labelData.Description);

            int labelBottomDots = MmToDots(settings.LabelHeightMm - settings.BottomMargin, dpi);
            int availableHeightForItemKey = labelBottomDots - layout.ItemKeyY;

            float itemKeyStartFontPt = hasExtraSpace
                ? settings.ItemKeyStartFontPt * 1.5f
                : settings.ItemKeyStartFontPt;

            float itemKeyMinFontPt = hasExtraSpace
                ? settings.ItemKeyStartFontPt
                : settings.ItemKeyMinFontPt;

            layout.ItemKeyWidthScale = hasExtraSpace ? 1.3f : settings.ItemKeyFontWidthScale;
            layout.ItemKeyHeightScale = hasExtraSpace ? 1.5f : settings.ItemKeyFontHeightScale;

            layout.ItemKeyFontPt = FitFontPtToWidth(
                labelData.ItemKey,
                "Arial",
                layout.UsableWidthDots,
                dpi,
                startFontPt: itemKeyStartFontPt,
                minFontPt: itemKeyMinFontPt,
                bold: true,
                widthScale: layout.ItemKeyWidthScale,
                heightScale: layout.ItemKeyHeightScale);

            // Scale up ItemKey if no description
            if (hasExtraSpace)
            {
                var (currentWidth, currentHeightDots) = MeasureTextDots(
                    labelData.ItemKey, "Arial", layout.ItemKeyFontPt, dpi, 0, bold: true,
                    layout.ItemKeyWidthScale, layout.ItemKeyHeightScale);

                if (currentHeightDots > 0 && currentHeightDots < availableHeightForItemKey)
                {
                    float potentialHeightScale = (float)availableHeightForItemKey / currentHeightDots;
                    potentialHeightScale = Math.Min(potentialHeightScale * 0.85f, 2.0f);

                    var (scaledWidth, scaledHeight) = MeasureTextDots(
                        labelData.ItemKey, "Arial", layout.ItemKeyFontPt, dpi, 0, bold: true,
                        layout.ItemKeyWidthScale, layout.ItemKeyHeightScale * potentialHeightScale);

                    if (scaledWidth <= layout.UsableWidthDots && scaledHeight <= availableHeightForItemKey)
                    {
                        layout.ItemKeyHeightScale *= potentialHeightScale;
                    }
                }

                // Recenter ItemKey vertically
                var (_, finalHeightDots) = MeasureTextDots(
                    labelData.ItemKey, "Arial", layout.ItemKeyFontPt, dpi, 0, bold: true,
                    layout.ItemKeyWidthScale, layout.ItemKeyHeightScale);

                layout.ItemKeyY = layout.ItemKeyY + ((availableHeightForItemKey - finalHeightDots) / 2);
                layout.ItemKeyY = Math.Max(layout.ItemKeyY, layout.IntroY + layout.IntroHeightDots + MmToDots(0.5, dpi));
            }
        }

        // ===== 3. Description =====
        layout.HasDescription = labelData.ShouldShowDescription && !string.IsNullOrWhiteSpace(labelData.Description);

        if (layout.HasDescription)
        {
            layout.IsRtl = ContainsHebrewOrArabic(labelData.Description);
            layout.FontFamily = labelData.FontFamily ?? "Arial Narrow";

            // Calculate descriptionY
            if (layout.HasItemKey)
            {
                float itemKeyFontPt = FitFontPtToWidth(
                    labelData.ItemKey, "Arial", layout.UsableWidthDots, dpi,
                    settings.ItemKeyStartFontPt, settings.ItemKeyMinFontPt, true,
                    settings.ItemKeyFontWidthScale, settings.ItemKeyFontHeightScale);

                var (_, itemKeyHeightDots) = MeasureTextDots(
                    labelData.ItemKey, "Arial", itemKeyFontPt, dpi, 0, true,
                    settings.ItemKeyFontWidthScale, settings.ItemKeyFontHeightScale);

                layout.DescY = layout.ItemKeyY + itemKeyHeightDots + MmToDots(0.4, dpi);
            }
            else
            {
                layout.DescY = MmToDots(settings.LabelHeightMm * 0.50, dpi);
            }

            // Split text and find optimal font size
            layout.DescFontPt = settings.DescriptionStartFontPt;
            double descriptionWidthMm = settings.LabelWidthMm - settings.LeftMargin - settings.RightMargin - 0.6;

            do
            {
                layout.DescLines = FontSizeCalculator.SplitTextToFit(
                    labelData.Description,
                    descriptionWidthMm,
                    layout.DescFontPt,
                    layout.FontFamily,
                    settings.DescriptionFontWidthScale);

                if (layout.DescLines.Count <= settings.DescriptionMaxLines)
                    break;

                layout.DescFontPt -= 0.5f;
            } while (layout.DescFontPt >= settings.DescriptionMinFontPt);

            if (layout.DescLines.Count > settings.DescriptionMaxLines)
            {
                layout.DescLines = layout.DescLines.Take(settings.DescriptionMaxLines).ToList();
            }

            // Calculate scales
            const float lineHeightFactor = 0.8f;
            layout.DescHeightScale = settings.DescriptionFontHeightScale;
            layout.DescWidthScale = settings.DescriptionFontWidthScale;
            int lineHeightDots = (int)(layout.DescFontPt * layout.DescHeightScale * lineHeightFactor * dpi / 72.0);
            int totalDescriptionHeight = lineHeightDots * layout.DescLines.Count;
            int labelBottomDots = MmToDots(settings.LabelHeightMm - settings.BottomMargin, dpi);
            int availableHeight = labelBottomDots - layout.DescY;

            // Reduce if overflow
            if (totalDescriptionHeight > availableHeight && layout.DescLines.Count > 0)
            {
                float requiredScale = (float)availableHeight / (layout.DescLines.Count * layout.DescFontPt * lineHeightFactor * dpi / 72.0f);
                layout.DescHeightScale = Math.Max(0.6f, Math.Min(layout.DescHeightScale, requiredScale));
                lineHeightDots = (int)(layout.DescFontPt * layout.DescHeightScale * lineHeightFactor * dpi / 72.0);
            }

            // Scale up if room available
            int widestLineWidthDots = 0;
            int tallestLineHeightDots = 0;
            foreach (var line in layout.DescLines)
            {
                var (lineWidth, lineHeight) = MeasureTextDots(
                    line, layout.FontFamily, layout.DescFontPt, dpi, 0, bold: true,
                    layout.DescWidthScale, layout.DescHeightScale);
                if (lineWidth > widestLineWidthDots)
                    widestLineWidthDots = lineWidth;
                if (lineHeight > tallestLineHeightDots)
                    tallestLineHeightDots = lineHeight;
            }

            int actualTotalHeight = tallestLineHeightDots * layout.DescLines.Count;

            if (widestLineWidthDots > 0 && actualTotalHeight > 0)
            {
                float widthScaleUp = (float)layout.DescriptionUsableWidth / widestLineWidthDots;
                float heightScaleUp = (float)availableHeight / actualTotalHeight;

                // Scale width
                if (widthScaleUp > 1.0f)
                {
                    float attemptedWidthScale = Math.Min(widthScaleUp, 1.5f);
                    while (attemptedWidthScale > 1.0f)
                    {
                        bool allLinesFit = true;
                        foreach (var line in layout.DescLines)
                        {
                            var (scaledWidth, _) = MeasureTextDots(
                                line, layout.FontFamily, layout.DescFontPt, dpi, 0, bold: true,
                                layout.DescWidthScale * attemptedWidthScale, layout.DescHeightScale);
                            if (scaledWidth > layout.DescriptionUsableWidth)
                            {
                                allLinesFit = false;
                                break;
                            }
                        }
                        if (allLinesFit)
                        {
                            layout.DescWidthScale *= attemptedWidthScale;
                            break;
                        }
                        attemptedWidthScale -= 0.05f;
                    }
                }

                // Scale height
                if (heightScaleUp > 1.05f)
                {
                    float maxHeightScale = layout.DescLines.Count switch
                    {
                        1 => 2.0f,
                        2 => 1.7f,
                        _ => 1.4f
                    };

                    float potentialHeightScale = Math.Min(heightScaleUp * 0.85f, maxHeightScale);

                    int scaledTotalHeight = 0;
                    foreach (var line in layout.DescLines)
                    {
                        var (_, scaledHeight) = MeasureTextDots(
                            line, layout.FontFamily, layout.DescFontPt, dpi, 0, bold: true,
                            layout.DescWidthScale, layout.DescHeightScale * potentialHeightScale);
                        if (scaledHeight > scaledTotalHeight / layout.DescLines.Count || scaledTotalHeight == 0)
                            scaledTotalHeight = scaledHeight * layout.DescLines.Count;
                    }

                    if (scaledTotalHeight <= availableHeight)
                    {
                        layout.DescHeightScale *= potentialHeightScale;
                        lineHeightDots = scaledTotalHeight / layout.DescLines.Count;
                    }
                }
            }

            layout.LineSpacingDots = (int)(lineHeightDots * 0.85);
        }

        return layout;
    }

    /// <summary>
    /// Render a complete label to a bitmap using the shared layout calculation.
    /// This provides pixel-perfect preview that matches printed output.
    /// </summary>
    public static Bitmap RenderLabelToBitmap(LabelData labelData, StickerSettings settings)
    {
        // Use shared layout calculation - single source of truth
        var layout = CalculateLayout(labelData, settings);

        // Create label bitmap
        var labelBitmap = new Bitmap(layout.LabelWidthDots, layout.LabelHeightDots, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(labelBitmap);
        graphics.Clear(Color.White);

        // ===== 1. IntroLine =====
        if (layout.HasIntroLine)
        {
            using var introBitmap = RenderTextToBitmap(
                labelData.IntroLine,
                settings.IntroFontFamily,
                layout.IntroFontPt,
                layout.UsableWidthDots,
                layout.Dpi,
                isRtl: false,
                bold: settings.IntroBold,
                widthScale: layout.IntroWidthScale,
                heightScale: layout.IntroHeightScale);

            if (introBitmap != null)
            {
                graphics.DrawImage(introBitmap, layout.LeftMarginDots, layout.IntroY);
            }
        }

        // ===== 2. ItemKey =====
        if (layout.HasItemKey)
        {
            using var itemKeyBitmap = RenderTextToBitmap(
                labelData.ItemKey,
                "Arial",
                layout.ItemKeyFontPt,
                layout.UsableWidthDots,
                layout.Dpi,
                isRtl: false,
                bold: true,
                widthScale: layout.ItemKeyWidthScale,
                heightScale: layout.ItemKeyHeightScale);

            if (itemKeyBitmap != null)
            {
                int itemKeyX = layout.CenterX - (itemKeyBitmap.Width / 2);
                graphics.DrawImage(itemKeyBitmap, itemKeyX, layout.ItemKeyY);
            }
        }

        // ===== 3. Description =====
        if (layout.HasDescription && layout.DescLines.Count > 0)
        {
            int lineY = layout.DescY;
            foreach (var line in layout.DescLines)
            {
                using var lineBitmap = RenderTextToBitmap(
                    line,
                    layout.FontFamily,
                    layout.DescFontPt,
                    layout.DescriptionUsableWidth,
                    layout.Dpi,
                    isRtl: layout.IsRtl,
                    bold: true,
                    widthScale: layout.DescWidthScale,
                    heightScale: layout.DescHeightScale);

                if (lineBitmap != null)
                {
                    int lineX = layout.CenterX - (lineBitmap.Width / 2);
                    graphics.DrawImage(lineBitmap, lineX, lineY);
                }

                lineY += layout.LineSpacingDots;
            }
        }

        // Draw border for visual reference
        using var borderPen = new Pen(Color.LightGray, 1);
        graphics.DrawRectangle(borderPen, 0, 0, layout.LabelWidthDots - 1, layout.LabelHeightDots - 1);

        return labelBitmap;
    }

    private static int MmToDots(double mm, int dpi)
    {
        return (int)Math.Round(mm * dpi / 25.4);
    }

    #endregion
}
