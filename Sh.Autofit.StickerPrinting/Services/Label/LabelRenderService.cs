using Sh.Autofit.StickerPrinting.Helpers;
using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Label;

public class LabelRenderService : ILabelRenderService
{
    public LabelData CreateLabelData(string itemKey, PartInfo partInfo, string language)
    {
        var labelData = new LabelData
        {
            ItemKey = itemKey,
            Language = language
        };

        // Determine description based on language
        if (language == "ar")
        {
            labelData.Description = partInfo.ArabicDescription ?? partInfo.PartName;
        }
        else // Hebrew default
        {
            labelData.Description = partInfo.HebrewDescription ?? partInfo.PartName;
        }

        // Check if description should be hidden
        if (PrefixChecker.HasExcludedPrefix(itemKey))
        {
            labelData.Description = string.Empty;
        }

        return labelData;
    }

    public void OptimizeFontSize(LabelData labelData, StickerSettings settings)
    {
        if (!labelData.ShouldShowDescription || string.IsNullOrWhiteSpace(labelData.Description))
            return;

        // Calculate available space
        double availableWidth = settings.WidthMm - settings.LeftMargin - settings.RightMargin;
        double availableHeight = settings.HeightMm - settings.TopMargin - settings.BottomMargin - 20; // Reserve space for intro + key

        // Calculate optimal font size
        double optimalSize = FontSizeCalculator.CalculateOptimalFontSize(
            labelData.Description,
            availableWidth,
            availableHeight,
            settings.DescriptionFontSize,
            labelData.FontFamily
        );

        labelData.FontSize = optimalSize;
    }
}
