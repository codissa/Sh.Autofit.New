using Microsoft.AspNetCore.Mvc;
using Sh.Autofit.StickerPrinting.Helpers;
using Sh.Autofit.StickerPrinting.Services.Database;

namespace Sh.Autofit.StickerPrinting.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly IStockDataService _stockDataService;

    public StockController(IStockDataService stockDataService)
    {
        _stockDataService = stockDataService;
    }

    [HttpGet("{stockId:int}")]
    public async Task<IActionResult> GetStockInfo(int stockId)
    {
        var stockInfo = await _stockDataService.GetStockInfoAsync(stockId);
        if (stockInfo == null)
            return NotFound(new { message = $"Stock '{stockId}' not found" });

        return Ok(stockInfo);
    }

    [HttpGet("{stockId:int}/moves")]
    public async Task<IActionResult> GetStockMoves(int stockId)
    {
        var moves = await _stockDataService.GetStockMovesAsync(stockId);
        var filtered = moves.Where(m => !PrefixChecker.ShouldIgnoreItem(m.ItemKey)).ToList();
        return Ok(filtered);
    }
}
