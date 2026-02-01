using System.Data.SqlClient;
using Dapper;
using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Database;

public class PartDataService : IPartDataService
{
    private readonly string _connectionString;

    public PartDataService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<PartInfo?> GetPartByItemKeyAsync(string itemKey)
    {
        const string sql = @"
            SELECT TOP 1
                PartNumber AS ItemKey,
                PartName,
                CustomDescription AS HebrewDescription,
                ArabicDescription,
                Localization
            FROM dbo.vw_Parts
            WHERE PartNumber = @ItemKey AND IsActive = 1";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var result = await connection.QuerySingleOrDefaultAsync<PartInfo>(
            sql,
            new { ItemKey = itemKey },
            commandTimeout: 30
        );

        return result;
    }

    public async Task<List<PartInfo>> SearchPartsAsync(string searchTerm)
    {
        const string sql = @"
            SELECT TOP 100
                PartNumber AS ItemKey,
                PartName,
                CustomDescription AS HebrewDescription,
                ArabicDescription,
                Category,
                Localization
            FROM dbo.vw_Parts
            WHERE IsActive = 1
                AND (PartNumber LIKE @Search OR PartName LIKE @Search OR CustomDescription LIKE @Search)
            ORDER BY PartNumber";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync<PartInfo>(
            sql,
            new { Search = $"%{searchTerm}%" },
            commandTimeout: 30
        );

        return results.ToList();
    }
}
