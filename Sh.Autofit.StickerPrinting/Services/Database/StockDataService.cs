using System.Data.SqlClient;
using Dapper;
using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Database;

public class StockDataService : IStockDataService
{
    private readonly string _connectionString;

    public StockDataService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<StockInfo?> GetStockInfoAsync(int stockId)
    {
        const string sql = @"
            SELECT TOP 1
                ID AS StockId,
                AccountName,
                AccountKey,
                ValueDate,
                Remarks
            FROM SH2013.dbo.Stock
            WHERE ID = @StockId";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        return await connection.QuerySingleOrDefaultAsync<StockInfo>(
            sql,
            new { StockId = stockId },
            commandTimeout: 30
        );
    }

    public async Task<List<StockMoveItem>> GetStockMovesAsync(int stockId)
    {
        //const string sql = @"
        //    SELECT
        //        ItemKey,
        //        SUM(Quantity) AS TotalQuantity
        //    FROM SH2013.dbo.StockMoves
        //    WHERE StockID = @StockID
        //    GROUP BY ItemKey
        //    ORDER BY ItemKey";
        const string sql = @"SELECT
                sm.ItemKey,
                SUM(sm.Quantity) AS TotalQuantity,
                i.Localization,
                ROW_NUMBER() OVER (ORDER BY i.Localization) AS OriginalOrder
            FROM SH2013.dbo.StockMoves sm
            LEFT JOIN SH2013.dbo.Items i ON i.ItemKey = sm.ItemKey
            WHERE StockID = @StockID
            GROUP BY sm.ItemKey, i.Localization";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync<StockMoveItem>(
            sql,
            new { StockID = stockId },
            commandTimeout: 30
        );

        return results.ToList();
    }
}
