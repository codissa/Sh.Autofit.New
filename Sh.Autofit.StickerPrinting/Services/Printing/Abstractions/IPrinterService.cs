using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

/// <summary>
/// Generic printer service interface - extensible for any printer type
/// </summary>
public interface IPrinterService
{
    /// <summary>
    /// Unique identifier for this printer type (e.g., "TSC", "Zebra", "Dymo")
    /// </summary>
    string PrinterType { get; }

    /// <summary>
    /// Get printer capabilities (supported features)
    /// </summary>
    PrinterCapabilities GetCapabilities();

    /// <summary>
    /// Discover available printers of this type
    /// </summary>
    Task<List<PrinterInfo>> GetAvailablePrintersAsync();

    /// <summary>
    /// Get current status of a specific printer
    /// </summary>
    Task<PrinterInfo> GetPrinterStatusAsync(string printerName);

    /// <summary>
    /// Print a label with the given data
    /// </summary>
    /// <param name="labelData">Label content and formatting</param>
    /// <param name="settings">Label dimensions and printer settings</param>
    /// <param name="printerName">Target printer name</param>
    /// <param name="quantity">Number of copies</param>
    Task PrintLabelAsync(LabelData labelData, StickerSettings settings, string printerName, int quantity = 1);

    /// <summary>
    /// Print multiple different labels (batch operation)
    /// </summary>
    Task PrintBatchAsync(List<PrintJob> jobs, string printerName);
}
