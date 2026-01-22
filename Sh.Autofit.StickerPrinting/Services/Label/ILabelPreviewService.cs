using System.Windows.Media.Imaging;
using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Label;

public interface ILabelPreviewService
{
    BitmapSource? GeneratePreview(LabelData labelData, StickerSettings settings);
}
