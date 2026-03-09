using System.Data.SqlClient;
using Dapper;
using Sh.Autofit.OrderBoard.Web.Models;

namespace Sh.Autofit.OrderBoard.Web.Services;

public interface ISh2013PollingService
{
    Task<List<StockRow>> DiscoverNewRowsAsync(int lastMaxId);
    Task<List<StockRow>> RecheckTrackedIdsAsync(IEnumerable<int> stockIds);
}

public class Sh2013PollingService : ISh2013PollingService
{
    private readonly string _connectionString;

    public Sh2013PollingService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<StockRow>> DiscoverNewRowsAsync(int lastMaxId)
    {
        const string sql = @"
            SELECT ID, DocumentID, DocNumber, Status, Reference,
                   AccountKey, AccountName, City, Address, Phone,
                   IssueDate, ValueDate, Remarks
            FROM SH2013.dbo.Stock
            WHERE DocumentID IN (11, 1, 4, 7)
              AND IssueDate >= DATEADD(DAY, -2, CAST(GETDATE() AS DATE))
              AND ID > @LastMaxId
            ORDER BY ID";

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var rows = await conn.QueryAsync<StockRow>(sql, new { LastMaxId = lastMaxId }, commandTimeout: 60);
        return rows.ToList();
    }

    public async Task<List<StockRow>> RecheckTrackedIdsAsync(IEnumerable<int> stockIds)
    {
        var ids = stockIds.ToList();
        if (ids.Count == 0) return [];

        const string sql = @"
            SELECT ID, DocumentID, DocNumber, Status, Reference,
                   AccountKey, AccountName, City, Address, Phone,
                   IssueDate, ValueDate, Remarks
            FROM SH2013.dbo.Stock
            WHERE ID IN @Ids";

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var result = new List<StockRow>(ids.Count);
        foreach (var batch in ids.Chunk(2000))
        {
            var rows = await conn.QueryAsync<StockRow>(sql, new { Ids = batch }, commandTimeout: 30);
            result.AddRange(rows);
        }
        return result;
    }
}
