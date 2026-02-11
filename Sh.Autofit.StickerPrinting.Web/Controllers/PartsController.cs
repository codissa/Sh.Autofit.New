using Microsoft.AspNetCore.Mvc;
using Sh.Autofit.StickerPrinting.Services.Database;

namespace Sh.Autofit.StickerPrinting.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PartsController : ControllerBase
{
    private readonly IPartDataService _partDataService;

    public PartsController(IPartDataService partDataService)
    {
        _partDataService = partDataService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<object>());

        var results = await _partDataService.SearchPartsAsync(q);
        return Ok(results);
    }

    [HttpGet("{itemKey}")]
    public async Task<IActionResult> GetByItemKey(string itemKey)
    {
        var part = await _partDataService.GetPartByItemKeyAsync(itemKey);
        if (part == null)
            return NotFound(new { message = $"Part '{itemKey}' not found" });

        return Ok(part);
    }
}
