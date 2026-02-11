using Microsoft.AspNetCore.SignalR;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Database;
using Sh.Autofit.StickerPrinting.Services.Label;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.Web.Hubs;

public class PrintHub : Hub
{
    private readonly IPartDataService _partDataService;
    private readonly IArabicDescriptionService _arabicService;
    private readonly ILabelRenderService _labelRenderService;
    private readonly IPrinterService _printerService;

    public PrintHub(
        IPartDataService partDataService,
        IArabicDescriptionService arabicService,
        ILabelRenderService labelRenderService,
        IPrinterService printerService)
    {
        _partDataService = partDataService;
        _arabicService = arabicService;
        _labelRenderService = labelRenderService;
        _printerService = printerService;
    }

    public async Task PrintBatch(BatchPrintRequest request)
    {
        var total = request.Items.Count;
        var printed = 0;
        var errors = new List<string>();
        var settings = new StickerSettings();

        for (int i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            try
            {
                await Clients.Caller.SendAsync("PrintProgress", new
                {
                    current = i + 1,
                    total,
                    currentItemKey = item.ItemKey,
                    status = "printing"
                });

                var partInfo = await _partDataService.GetPartByItemKeyAsync(item.ItemKey);
                if (partInfo == null)
                {
                    errors.Add($"{item.ItemKey}: part not found");
                    continue;
                }

                var language = item.Language ?? request.DefaultLanguage ?? "he";

                if (language == "ar")
                {
                    var arabicDesc = await _arabicService.GetArabicDescriptionAsync(item.ItemKey);
                    if (!string.IsNullOrEmpty(arabicDesc))
                        partInfo.ArabicDescription = arabicDesc;
                }

                var labelData = _labelRenderService.CreateLabelData(item.ItemKey, partInfo, language);
                var quantity = item.Quantity > 0 ? item.Quantity : 1;
                labelData.Quantity = quantity;

                await _printerService.PrintLabelAsync(labelData, settings, request.PrinterName, quantity);
                printed += quantity;
            }
            catch (Exception ex)
            {
                errors.Add($"{item.ItemKey}: {ex.Message}");
                await Clients.Caller.SendAsync("PrintError", new
                {
                    itemKey = item.ItemKey,
                    error = ex.Message
                });
            }
        }

        await Clients.Caller.SendAsync("PrintComplete", new
        {
            totalPrinted = printed,
            errors
        });
    }
}

public record BatchPrintRequest(
    List<BatchPrintItem> Items,
    string PrinterName,
    string? DefaultLanguage);

public record BatchPrintItem(
    string ItemKey,
    string? Language,
    int Quantity);
