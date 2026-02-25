using Microsoft.AspNetCore.Mvc;
using Sh.Autofit.OrderBoard.Web.Services;

namespace Sh.Autofit.OrderBoard.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArchiveController : ControllerBase
{
    private readonly IArchiveService _archiveService;

    public ArchiveController(IArchiveService archiveService)
    {
        _archiveService = archiveService;
    }

    [HttpGet]
    public async Task<IActionResult> GetArchiveBoard([FromQuery] DateTime date)
    {
        if (date.Date > DateTime.Today)
            return BadRequest(new { error = "Cannot view archive for future dates" });
        if (date.Date == DateTime.Today)
            return BadRequest(new { error = "Use the live board for today's data" });

        var board = await _archiveService.BuildArchiveBoardAsync(date);
        return Ok(board);
    }

    [HttpGet("order/{id:int}/timeline")]
    public async Task<IActionResult> GetOrderTimeline(int id)
    {
        var timeline = await _archiveService.GetOrderTimelineAsync(id);
        return Ok(timeline);
    }
}
