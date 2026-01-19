namespace Sh.Autofit.StickerPrinting.Models;

public class PrintJob
{
    public LabelData LabelData { get; set; } = null!;
    public StickerSettings Settings { get; set; } = null!;

    // TSPL coordinates (calculated based on DPI and margins)
    public int IntroX => CalculateX(Settings.LeftMargin);
    public int IntroY => CalculateY(Settings.TopMargin);

    public int ItemKeyX => CalculateX(Settings.WidthMm / 2); // Center
    public int ItemKeyY => CalculateY(Settings.HeightMm * 0.35);

    public int DescriptionX => CalculateX(Settings.WidthMm / 2); // Center
    public int DescriptionY => CalculateY(Settings.HeightMm * 0.6);

    private int CalculateX(double mm) => (int)(mm * Settings.DPI / 25.4);
    private int CalculateY(double mm) => (int)(mm * Settings.DPI / 25.4);
}
