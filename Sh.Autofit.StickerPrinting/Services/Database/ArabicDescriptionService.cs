using System.Data.SqlClient;
using Dapper;

namespace Sh.Autofit.StickerPrinting.Services.Database;

public class ArabicDescriptionService : IArabicDescriptionService
{
    private readonly string _connectionString;

    public ArabicDescriptionService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<string?> GetArabicDescriptionAsync(string itemKey)
    {
        const string sql = @"
            SELECT ArabicDescription
            FROM dbo.ArabicPartDescriptions
            WHERE ItemKey = @ItemKey AND IsActive = 1";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        return await connection.QuerySingleOrDefaultAsync<string>(
            sql,
            new { ItemKey = itemKey },
            commandTimeout: 30
        );
    }

    public async Task SaveArabicDescriptionAsync(string itemKey, string description, string userName)
    {
        const string sql = @"
            MERGE dbo.ArabicPartDescriptions AS target
            USING (SELECT @ItemKey AS ItemKey) AS source
            ON target.ItemKey = source.ItemKey
            WHEN MATCHED THEN
                UPDATE SET
                    ArabicDescription = @Description,
                    UpdatedAt = GETDATE(),
                    UpdatedBy = @UserName,
                    IsActive = 1
            WHEN NOT MATCHED THEN
                INSERT (ItemKey, ArabicDescription, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                VALUES (@ItemKey, @Description, 1, GETDATE(), GETDATE(), @UserName, @UserName);";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            sql,
            new { ItemKey = itemKey, Description = description, UserName = userName },
            commandTimeout: 30
        );
    }

    public async Task DeleteArabicDescriptionAsync(string itemKey)
    {
        const string sql = @"
            UPDATE dbo.ArabicPartDescriptions
            SET IsActive = 0, UpdatedAt = GETDATE()
            WHERE ItemKey = @ItemKey";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            sql,
            new { ItemKey = itemKey },
            commandTimeout: 30
        );
    }
}
