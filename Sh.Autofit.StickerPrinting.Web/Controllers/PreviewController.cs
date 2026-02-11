using System.Drawing.Imaging;
using Microsoft.AspNetCore.Mvc;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Database;
using Sh.Autofit.StickerPrinting.Services.Label;
using Sh.Autofit.StickerPrinting.Services.Printing.Zebra;

namespace Sh.Autofit.StickerPrinting.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PreviewController : ControllerBase
{
    private readonly IPartDataService _partDataService;
    private readonly IArabicDescriptionService _arabicService;
    private readonly ILabelRenderService _labelRenderService;

    public PreviewController(
        IPartDataService partDataService,
        IArabicDescriptionService arabicService,
        ILabelRenderService labelRenderService)
    {
        _partDataService = partDataService;
        _arabicService = arabicService;
        _labelRenderService = labelRenderService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPreview([FromQuery] string itemKey, [FromQuery] string? language)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return BadRequest(new { message = "itemKey is required" });

        var partInfo = await _partDataService.GetPartByItemKeyAsync(itemKey);
        if (partInfo == null)
            return NotFound(new { message = $"Part '{itemKey}' not found" });

        var lang = language ?? "he";

        // If Arabic, check for custom Arabic description
        if (lang == "ar")
        {
            var arabicDesc = await _arabicService.GetArabicDescriptionAsync(itemKey);
            if (!string.IsNullOrEmpty(arabicDesc))
                partInfo.ArabicDescription = arabicDesc;
        }

        var settings = new StickerSettings();
        var labelData = _labelRenderService.CreateLabelData(itemKey, partInfo, lang);

        // Use ZplGfaRenderer directly to get a System.Drawing.Bitmap (no WPF dependency)
        using var bitmap = ZplGfaRenderer.RenderLabelToBitmap(labelData, settings);
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        return File(ms.ToArray(), "image/png");
    }
}
