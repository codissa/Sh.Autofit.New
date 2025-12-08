using System.Data.SqlClient;
using Dapper;
using Sh.Autofit.StockExport.Models;

namespace Sh.Autofit.StockExport.Services.Database;

/// <summary>
/// Service for querying stock moves from SH2013.dbo.StockMoves
/// READ-ONLY access - no write operations are performed
/// </summary>
public class StockMovesService : IStockMovesService
{
    private readonly string _connectionString;

    public StockMovesService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Gets aggregated stock move items for the specified stock ID
    /// READ-ONLY query - no modifications to database
    /// </summary>
    /// <param name="stockId">The stock ID to query</param>
    /// <returns>List of aggregated stock move items</returns>
    /// <exception cref="InvalidOperationException">Thrown when database connection fails</exception>
    public async Task<List<StockMoveItem>> GetStockMovesAsync(int stockId)
    {
        if (stockId <= 0)
        {
            throw new ArgumentException("Stock ID must be greater than zero", nameof(stockId));
        }

        const string sql = @"
            SELECT
                ItemKey,
                SUM(Quantity) AS TotalQuantity
            FROM SH2013.dbo.StockMoves
            WHERE StockID = @StockID
            GROUP BY ItemKey
            ORDER BY ItemKey";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var results = await connection.QueryAsync<StockMoveItem>(
                sql,
                new { StockID = stockId },
                commandTimeout: 60
            );

            return results.ToList();
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                $"Failed to retrieve stock moves from database: {ex.Message}",
                ex
            );
        }
    }

    /// <summary>
    /// Gets the current document number (CurrenFNumber) for the specified document ID
    /// READ-ONLY query - no modifications to database
    /// </summary>
    /// <param name="documentId">The document ID (e.g., 24 for יתרת פתיחה)</param>
    /// <returns>Current document number from CurrenFNumber field</returns>
    /// <exception cref="InvalidOperationException">Thrown when database connection fails or document not found</exception>
    public async Task<int> GetCurrentDocumentNumberAsync(int documentId)
    {
        const string sql = @"
            SELECT CurrenFNumber
            FROM SH2013.dbo.DocumentsDef
            WHERE DocumentID = @DocumentID";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var result = await connection.QuerySingleOrDefaultAsync<int?>(
                sql,
                new { DocumentID = documentId },
                commandTimeout: 30
            );

            if (result == null)
            {
                throw new InvalidOperationException($"Document ID {documentId} not found in DocumentsDef table");
            }

            return result.Value;
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                $"Failed to retrieve current document number from database: {ex.Message}",
                ex
            );
        }
    }
}
