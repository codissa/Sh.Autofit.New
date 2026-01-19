using System.Drawing.Printing;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Tsc;

/// <summary>
/// TSC printer implementation using TSPL commands
/// Sends commands directly to printer via Windows print queue
/// </summary>
public class TscPrinterService : IPrinterService
{
    private readonly ITsplCommandGenerator _commandGenerator;
    private readonly IRawPrinterCommunicator _communicator;

    public string PrinterType => "TSC";

    public TscPrinterService(
        ITsplCommandGenerator commandGenerator,
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
            SupportsCustomFonts = true,
            SupportsBarcodes = true,
            SupportsQrCodes = true,
            SupportedFontNames = new List<string> { "0", "1", "2", "3", "4", "HEBREW.TTF", "ARABIC.TTF" },
            MinDpi = 200,
            MaxDpi = 600,
            DefaultDpi = 203,
            CommandLanguage = "TSPL"
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
                // Filter for TSC printers (or allow all if TSC driver handles it)
                if (IsTscPrinter(printerName))
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

            // For TSC printers, could query status via TSPL commands if needed
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
        // Generate TSPL commands
        var tsplCommands = _commandGenerator.GenerateLabelCommands(labelData, settings);

        // Print specified number of copies
        for (int i = 0; i < quantity; i++)
        {
            var success = await _communicator.SendStringToPrinterAsync(printerName, tsplCommands);

            if (!success)
            {
                throw new InvalidOperationException(
                    $"Failed to send print job to printer '{printerName}' (copy {i + 1} of {quantity})");
            }

            // Small delay between copies to avoid overwhelming printer buffer
            if (i < quantity - 1)
            {
                await Task.Delay(100);
            }
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

    private bool IsTscPrinter(string printerName)
    {
        // Check if printer name contains TSC or known TSC model names
        var tscIdentifiers = new[] { "TSC", "TTP", "TDP", "MH", "ME", "Alpha" };
        return tscIdentifiers.Any(id =>
            printerName.Contains(id, StringComparison.OrdinalIgnoreCase));
    }
}
