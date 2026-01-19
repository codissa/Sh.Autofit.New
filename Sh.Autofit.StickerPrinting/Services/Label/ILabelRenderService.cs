using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Label;

public interface ILabelRenderService
{
    LabelData CreateLabelData(string itemKey, PartInfo partInfo, string language);
    void OptimizeFontSize(LabelData labelData, StickerSettings settings);
}
