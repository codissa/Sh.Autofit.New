namespace Sh.Autofit.StickerPrinting.Models;

public enum PrinterStatus
{
    Unknown,
    Ready,
    Offline,
    OutOfPaper,
    Error
}

public class PrinterInfo
{
    public string Name { get; set; } = string.Empty;
    public PrinterStatus Status { get; set; } = PrinterStatus.Unknown;
    public string StatusMessage { get; set; } = "Unknown";
    public string PrinterType { get; set; } = string.Empty;
    public bool IsConnected => Status != PrinterStatus.Offline && Status != PrinterStatus.Unknown;
}
