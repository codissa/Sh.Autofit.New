using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Printing.Zebra;

namespace Sh.Autofit.StickerPrinting.Services.Label;

public class LabelPreviewService : ILabelPreviewService
{
    /// <summary>
    /// Generate a pixel-perfect preview using the same rendering as the printer
    /// </summary>
    public BitmapSource? GeneratePreview(LabelData labelData, StickerSettings settings)
    {
        if (labelData == null || string.IsNullOrEmpty(labelData.ItemKey))
            return null;

        try
        {
            // Use the EXACT same rendering as the printer (pixel-perfect match)
            using var bitmap = ZplGfaRenderer.RenderLabelToBitmap(labelData, settings);
            return ConvertToBitmapSource(bitmap);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Preview generation failed: {ex.Message}");
            return null;
        }
    }

    private BitmapSource ConvertToBitmapSource(System.Drawing.Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, ImageFormat.Png);
        memory.Position = 0;

        var bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.StreamSource = memory;
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.EndInit();
        bitmapImage.Freeze();

        return bitmapImage;
    }
}
