using Sh.Autofit.StickerPrinting.Helpers;
using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Label;

public class LabelRenderService : ILabelRenderService
{
    public LabelData CreateLabelData(string itemKey, PartInfo partInfo, string language)
    {
        // Check if this item has an excluded prefix (no description)
        bool hasExcludedPrefix = PrefixChecker.HasExcludedPrefix(itemKey);

        var labelData = new LabelData
        {
            // Uppercase the ItemKey when it has an excluded prefix (will be displayed alone, larger)
            ItemKey = hasExcludedPrefix ? itemKey.ToUpperInvariant() : itemKey,
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

        // Clear description for excluded prefix items
        if (hasExcludedPrefix)
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
