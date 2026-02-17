using System.Data.SqlClient;
using Dapper;
using Sh.Autofit.OrderBoard.Web.Models;

namespace Sh.Autofit.OrderBoard.Web.Services;

public interface IAppOrderService
{
    Task<List<AppOrder>> GetAllOrdersAsync(bool includeHidden = false);
    Task<AppOrder?> GetOrderByIdAsync(int appOrderId);
    Task<int> CreateOrderAsync(AppOrder order);
    Task UpdateOrderAsync(AppOrder order);

    Task<List<AppOrderLink>> GetAllPresentLinksAsync();
    Task<List<AppOrderLink>> GetLinksForOrderAsync(int appOrderId);
    Task<AppOrderLink?> GetLinkByStockIdAsync(int stockId);
    Task<AppOrder?> FindOrderByDocNumberAsync(int docNumber);
    Task<int> CreateLinkAsync(AppOrderLink link);
    Task UpdateLinkAsync(AppOrderLink link);

    Task ReassignLinksAsync(int fromOrderId, int toOrderId);
    Task<int> BulkHideByStageAsync(string stage);
    Task InsertStageEventAsync(StageEvent evt);

    Task<string> GetSettingAsync(string key, string defaultValue = "");
    Task SetSettingAsync(string key, string value);
}

public class AppOrderService : IAppOrderService
{
    private readonly string _connectionString;

    public AppOrderService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private SqlConnection CreateConnection() => new(_connectionString);

    // ---- AppOrders ----

    public async Task<List<AppOrder>> GetAllOrdersAsync(bool includeHidden = false)
    {
        var sql = @"SELECT * FROM dbo.AppOrders WHERE MergedIntoAppOrderId IS NULL";
        if (!includeHidden) sql += " AND Hidden = 0";
        sql += " ORDER BY DisplayTime DESC, AppOrderId DESC";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<AppOrder>(sql, commandTimeout: 30)).ToList();
    }

    public async Task<AppOrder?> GetOrderByIdAsync(int appOrderId)
    {
        const string sql = "SELECT * FROM dbo.AppOrders WHERE AppOrderId = @AppOrderId";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<AppOrder>(sql, new { AppOrderId = appOrderId });
    }

    public async Task<int> CreateOrderAsync(AppOrder order)
    {
        const string sql = @"
            INSERT INTO dbo.AppOrders
                (CreatedAt, UpdatedAt, AccountKey, AccountName, City, Address, Phone,
                 DisplayTime, CurrentStage, StageUpdatedAt, IsManual, ManualNote,
                 Hidden, HiddenReason, HiddenAt, Pinned, DeliveryMethodId, DeliveryRunId,
                 MergedIntoAppOrderId, NeedsResolve)
            OUTPUT INSERTED.AppOrderId
            VALUES
                (SYSUTCDATETIME(), SYSUTCDATETIME(), @AccountKey, @AccountName, @City, @Address, @Phone,
                 @DisplayTime, @CurrentStage, SYSUTCDATETIME(), @IsManual, @ManualNote,
                 @Hidden, @HiddenReason, @HiddenAt, @Pinned, @DeliveryMethodId, @DeliveryRunId,
                 @MergedIntoAppOrderId, @NeedsResolve)";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, order);
    }

    public async Task UpdateOrderAsync(AppOrder order)
    {
        const string sql = @"
            UPDATE dbo.AppOrders SET
                UpdatedAt = SYSUTCDATETIME(),
                AccountKey = @AccountKey, AccountName = @AccountName,
                City = @City, Address = @Address, Phone = @Phone,
                DisplayTime = @DisplayTime, CurrentStage = @CurrentStage,
                StageUpdatedAt = @StageUpdatedAt, IsManual = @IsManual, ManualNote = @ManualNote,
                Hidden = @Hidden, HiddenReason = @HiddenReason, HiddenAt = @HiddenAt,
                Pinned = @Pinned, DeliveryMethodId = @DeliveryMethodId, DeliveryRunId = @DeliveryRunId,
                MergedIntoAppOrderId = @MergedIntoAppOrderId, NeedsResolve = @NeedsResolve
            WHERE AppOrderId = @AppOrderId";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, order);
    }

    // ---- AppOrderLinks ----

    public async Task<List<AppOrderLink>> GetAllPresentLinksAsync()
    {
        const string sql = "SELECT * FROM dbo.AppOrderLinks WHERE IsPresent = 1";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<AppOrderLink>(sql, commandTimeout: 30)).ToList();
    }

    public async Task<List<AppOrderLink>> GetLinksForOrderAsync(int appOrderId)
    {
        const string sql = "SELECT * FROM dbo.AppOrderLinks WHERE AppOrderId = @AppOrderId";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return (await conn.QueryAsync<AppOrderLink>(sql, new { AppOrderId = appOrderId })).ToList();
    }

    public async Task<AppOrderLink?> GetLinkByStockIdAsync(int stockId)
    {
        const string sql = "SELECT * FROM dbo.AppOrderLinks WHERE StockId = @StockId";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<AppOrderLink>(sql, new { StockId = stockId });
    }

    public async Task<AppOrder?> FindOrderByDocNumberAsync(int docNumber)
    {
        // Find an AppOrder that has a link with DocID=11 and DocNumber=docNumber
        const string sql = @"
            SELECT o.* FROM dbo.AppOrders o
            INNER JOIN dbo.AppOrderLinks l ON l.AppOrderId = o.AppOrderId
            WHERE l.DocumentId = 11 AND l.DocNumber = @DocNumber
              AND o.MergedIntoAppOrderId IS NULL";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QueryFirstOrDefaultAsync<AppOrder>(sql, new { DocNumber = docNumber });
    }

    public async Task<int> CreateLinkAsync(AppOrderLink link)
    {
        const string sql = @"
            INSERT INTO dbo.AppOrderLinks
                (AppOrderId, SourceDb, StockId, DocumentId, DocNumber, Status, Reference,
                 FirstSeenAt, LastSeenAt, IsPresent, DisappearedAt, MissCount)
            OUTPUT INSERTED.LinkId
            VALUES
                (@AppOrderId, @SourceDb, @StockId, @DocumentId, @DocNumber, @Status, @Reference,
                 SYSUTCDATETIME(), SYSUTCDATETIME(), @IsPresent, @DisappearedAt, 0)";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(sql, link);
    }

    public async Task UpdateLinkAsync(AppOrderLink link)
    {
        const string sql = @"
            UPDATE dbo.AppOrderLinks SET
                Status = @Status, LastSeenAt = @LastSeenAt,
                IsPresent = @IsPresent, DisappearedAt = @DisappearedAt, MissCount = @MissCount
            WHERE LinkId = @LinkId";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, link);
    }

    public async Task ReassignLinksAsync(int fromOrderId, int toOrderId)
    {
        const string sql = "UPDATE dbo.AppOrderLinks SET AppOrderId = @ToOrderId WHERE AppOrderId = @FromOrderId";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, new { FromOrderId = fromOrderId, ToOrderId = toOrderId });
    }

    public async Task<int> BulkHideByStageAsync(string stage)
    {
        const string sql = @"
            UPDATE dbo.AppOrders
            SET Hidden = 1, HiddenReason = 'BULK_CLEAN', HiddenAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
            WHERE CurrentStage = @Stage AND Hidden = 0 AND MergedIntoAppOrderId IS NULL";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.ExecuteAsync(sql, new { Stage = stage });
    }

    // ---- StageEvents ----

    public async Task InsertStageEventAsync(StageEvent evt)
    {
        const string sql = @"
            INSERT INTO dbo.StageEvents (AppOrderId, At, Actor, Action, FromStage, ToStage, Payload)
            VALUES (@AppOrderId, SYSUTCDATETIME(), @Actor, @Action, @FromStage, @ToStage, @Payload)";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, evt);
    }

    // ---- Settings ----

    public async Task<string> GetSettingAsync(string key, string defaultValue = "")
    {
        const string sql = "SELECT [Value] FROM dbo.OrderBoardSettings WHERE [Key] = @Key";
        using var conn = CreateConnection();
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<string>(sql, new { Key = key }) ?? defaultValue;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        const string sql = @"
            IF EXISTS (SELECT 1 FROM dbo.OrderBoardSettings WHERE [Key] = @Key)
                UPDATE dbo.OrderBoardSettings SET [Value] = @Value WHERE [Key] = @Key
            ELSE
                INSERT INTO dbo.OrderBoardSettings ([Key], [Value]) VALUES (@Key, @Value)";

        using var conn = CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(sql, new { Key = key, Value = value });
    }
}
