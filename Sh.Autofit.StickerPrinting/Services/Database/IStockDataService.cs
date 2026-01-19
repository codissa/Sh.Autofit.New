using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Database;

public interface IStockDataService
{
    Task<StockInfo?> GetStockInfoAsync(int stockId);
    Task<List<StockMoveItem>> GetStockMovesAsync(int stockId);
}
