using System.Data.SqlClient;
using Dapper;
using Sh.Autofit.OrderBoard.Web.Models.Dtos;

namespace Sh.Autofit.OrderBoard.Web.Services;

public interface IAccountsService
{
    Task<List<AccountSearchResult>> SearchAccountsAsync(string query, int top = 20);
}

public class AccountsService : IAccountsService
{
    private readonly string _connectionString;

    public AccountsService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<AccountSearchResult>> SearchAccountsAsync(string query, int top = 20)
    {
        const string sql = @"
            SELECT TOP(@Top) AccountKey, FullName, City, Phone
            FROM SH2013.dbo.Accounts
            WHERE AccountKey LIKE @Query OR FullName LIKE @Query
            ORDER BY FullName";

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return (await conn.QueryAsync<AccountSearchResult>(sql,
            new { Query = $"%{query}%", Top = top }, commandTimeout: 15)).ToList();
    }
}
