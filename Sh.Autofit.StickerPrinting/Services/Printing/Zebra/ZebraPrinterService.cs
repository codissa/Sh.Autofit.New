using System.Drawing.Printing;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Zebra;

/// <summary>
/// Zebra printer implementation using ZPL commands
/// Sends commands directly to printer via Windows print queue
/// </summary>
public class ZebraPrinterService : IPrinterService
{
    private readonly IZplCommandGenerator _commandGenerator;
    private readonly IRawPrinterCommunicator _communicator;

    public string PrinterType => "Zebra";

    public ZebraPrinterService(
        IZplCommandGenerator commandGenerator,
        IRawPrinterCommunicator communicator)
    {
        _commandGenerator = commandGenerator ?? throw new ArgumentNullException(nameof(commandGenerator));
        _communicator = communicator ?? throw new ArgumentNullException(nameof(communicator));
    }

    public PrinterCapabilities GetCapabilities()
    {
        return new PrinterCapabilities
        {
            SupportsRtlText = true,
            SupportsCustomFonts = false, // Using Zebra built-in fonts
            SupportsBarcodes = true,
            SupportsQrCodes = true,
            SupportedFontNames = new List<string> { "0" }, // Zebra Font 0 (built-in)
            MinDpi = 203,
            MaxDpi = 203, // ZDesigner S4M is 203 DPI
            DefaultDpi = 203,
            CommandLanguage = "ZPL"
        };
    }

    public async Task<List<PrinterInfo>> GetAvailablePrintersAsync()
    {
        return await Task.Run(() =>
        {
            var printers = PrinterSettings.InstalledPrinters;
            var result = new List<PrinterInfo>();

            foreach (string printerName in printers)
            {
                // Filter for Zebra printers
                if (IsZebraPrinter(printerName))
                {
                    result.Add(new PrinterInfo
                    {
                        Name = printerName,
                        Status = PrinterStatus.Unknown,
                        StatusMessage = "Not checked",
                        PrinterType = PrinterType
                    });
                }
            }

            return result;
        });
    }

    public async Task<PrinterInfo> GetPrinterStatusAsync(string printerName)
    {
        try
        {
            var isAvailable = await _communicator.IsPrinterAvailableAsync(printerName);

            if (!isAvailable)
            {
                return new PrinterInfo
                {
                    Name = printerName,
                    Status = PrinterStatus.Offline,
                    StatusMessage = "Printer not found or offline",
                    PrinterType = PrinterType
                };
            }

            // For Zebra printers, could query status via ZPL commands if needed
            // For now, assume ready if printer is available
            return new PrinterInfo
            {
                Name = printerName,
                Status = PrinterStatus.Ready,
                StatusMessage = "Ready",
                PrinterType = PrinterType
            };
        }
        catch (Exception ex)
        {
            return new PrinterInfo
            {
                Name = printerName,
                Status = PrinterStatus.Error,
                StatusMessage = ex.Message,
                PrinterType = PrinterType
            };
        }
    }

    public async Task PrintLabelAsync(
        LabelData labelData,
        StickerSettings settings,
        string printerName,
        int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));

        // Calculate pairs and remainder for 2-up printing
        // For N labels: print N/2 rows (2-up) + N%2 single labels
        int pairs = quantity / 2;      // e.g., 7 → 3 pairs
        int remainder = quantity % 2;  // e.g., 7 → 1 leftover

        // Debug output
        System.Diagnostics.Debug.WriteLine($"[PRINT] Quantity={quantity}, Pairs={pairs}, Remainder={remainder}");

        try
        {
            // Print pairs (2-up mode)
            if (pairs > 0)
            {
                var twoUpCommands = _commandGenerator.GenerateLabelCommands(
                    labelData,
                    settings,
                    pairs,          // Print 'pairs' rows
                    printTwoUp: true);

                var success = await _communicator.SendStringToPrinterAsync(printerName, twoUpCommands);

                if (!success)
                {
                    throw new InvalidOperationException(
                        $"Failed to send 2-up print job to printer '{printerName}' ({pairs} pairs)");
                }

                // Delay after sending batch
                await Task.Delay(200);
            }

            // Print remainder (1-up mode, left label only)
            if (remainder == 1)
            {
                var singleCommands = _commandGenerator.GenerateLabelCommands(
                    labelData,
                    settings,
                    1,              // Print 1 row
                    printTwoUp: false);

                var success = await _communicator.SendStringToPrinterAsync(printerName, singleCommands);

                if (!success)
                {
                    throw new InvalidOperationException(
                        $"Failed to send single label print job to printer '{printerName}'");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Print job failed for printer '{printerName}': {ex.Message}", ex);
        }
    }

    public async Task PrintBatchAsync(List<PrintJob> jobs, string printerName)
    {
        foreach (var job in jobs)
        {
            await PrintLabelAsync(
                job.LabelData,
                job.Settings,
                printerName,
                job.LabelData.Quantity);

            // Delay between different jobs to prevent buffer overflow
            await Task.Delay(100);
        }
    }

    private bool IsZebraPrinter(string printerName)
    {
        // Check if printer name contains Zebra identifiers or known Zebra model names
        var zebraIdentifiers = new[] { "Zebra", "ZD", "ZT", "GK", "GX", "S4M", "ZDesigner" };
        return zebraIdentifiers.Any(id =>
            printerName.Contains(id, StringComparison.OrdinalIgnoreCase));
    }
}
