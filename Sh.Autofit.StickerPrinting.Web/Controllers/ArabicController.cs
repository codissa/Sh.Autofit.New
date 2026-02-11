using Microsoft.AspNetCore.Mvc;
using Sh.Autofit.StickerPrinting.Services.Database;

namespace Sh.Autofit.StickerPrinting.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArabicController : ControllerBase
{
    private readonly IArabicDescriptionService _arabicService;

    public ArabicController(IArabicDescriptionService arabicService)
    {
        _arabicService = arabicService;
    }

    [HttpGet("{itemKey}")]
    public async Task<IActionResult> Get(string itemKey)
    {
        var description = await _arabicService.GetArabicDescriptionAsync(itemKey);
        return Ok(new { itemKey, description });
    }

    [HttpPut("{itemKey}")]
    public async Task<IActionResult> Save(string itemKey, [FromBody] SaveArabicRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { message = "Description is required" });

        var userName = request.UserName ?? "WebUser";
        await _arabicService.SaveArabicDescriptionAsync(itemKey, request.Description, userName);
        return Ok(new { message = "Arabic description saved", itemKey });
    }

    [HttpDelete("{itemKey}")]
    public async Task<IActionResult> Delete(string itemKey)
    {
        await _arabicService.DeleteArabicDescriptionAsync(itemKey);
        return Ok(new { message = "Arabic description deleted", itemKey });
    }
}

public record SaveArabicRequest(string Description, string? UserName);
