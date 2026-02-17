using Microsoft.AspNetCore.Mvc;
using Sh.Autofit.OrderBoard.Web.Services;

namespace Sh.Autofit.OrderBoard.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BoardController : ControllerBase
{
    private readonly IBoardBuilder _boardBuilder;

    public BoardController(IBoardBuilder boardBuilder)
    {
        _boardBuilder = boardBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> GetBoard([FromQuery] bool includeArchived = false)
    {
        var board = await _boardBuilder.BuildBoardAsync(includeArchived);
        return Ok(board);
    }
}
