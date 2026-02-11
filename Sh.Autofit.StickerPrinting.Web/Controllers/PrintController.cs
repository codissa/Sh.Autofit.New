using Microsoft.AspNetCore.Mvc;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Database;
using Sh.Autofit.StickerPrinting.Services.Label;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrintController : ControllerBase
{
    private readonly IPartDataService _partDataService;
    private readonly IArabicDescriptionService _arabicService;
    private readonly ILabelRenderService _labelRenderService;
    private readonly IPrinterService _printerService;

    public PrintController(
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

    [HttpPost("label")]
    public async Task<IActionResult> PrintLabel([FromBody] PrintLabelRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ItemKey))
            return BadRequest(new { message = "ItemKey is required" });
        if (string.IsNullOrWhiteSpace(request.PrinterName))
            return BadRequest(new { message = "PrinterName is required" });

        var partInfo = await _partDataService.GetPartByItemKeyAsync(request.ItemKey);
        if (partInfo == null)
            return NotFound(new { message = $"Part '{request.ItemKey}' not found" });

        // If Arabic, check for custom Arabic description
        if (request.Language == "ar")
        {
            var arabicDesc = await _arabicService.GetArabicDescriptionAsync(request.ItemKey);
            if (!string.IsNullOrEmpty(arabicDesc))
                partInfo.ArabicDescription = arabicDesc;
        }

        var language = request.Language ?? "he";
        var quantity = request.Quantity > 0 ? request.Quantity : 1;
        var settings = new StickerSettings();

        var labelData = _labelRenderService.CreateLabelData(request.ItemKey, partInfo, language);
        labelData.Quantity = quantity;

        await _printerService.PrintLabelAsync(labelData, settings, request.PrinterName, quantity);

        return Ok(new { message = $"Printed {quantity} labels for {request.ItemKey}", itemKey = request.ItemKey, quantity });
    }
}

public record PrintLabelRequest(string ItemKey, string? Language, int Quantity, string PrinterName);
