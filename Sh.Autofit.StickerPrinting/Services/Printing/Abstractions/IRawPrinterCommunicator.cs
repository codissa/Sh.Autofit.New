namespace Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

/// <summary>
/// Low-level communication with Windows print queue
/// Uses Win32 API to send raw data directly to printer
/// </summary>
public interface IRawPrinterCommunicator
{
    /// <summary>
    /// Send raw string data to printer
    /// </summary>
    Task<bool> SendStringToPrinterAsync(string printerName, string data);

    /// <summary>
    /// Send raw byte data to printer
    /// </summary>
    Task<bool> SendBytesToPrinterAsync(string printerName, byte[] data);

    /// <summary>
    /// Check if printer is accessible
    /// </summary>
    Task<bool> IsPrinterAvailableAsync(string printerName);
}
