using Sh.Autofit.StockExport.Models;

namespace Sh.Autofit.StockExport.Services.Database;

/// <summary>
/// Interface for querying stock moves from the database
/// </summary>
public interface IStockMovesService
{
    /// <summary>
    /// Gets aggregated stock move items for the specified stock ID
    /// </summary>
    /// <param name="stockId">The stock ID to query</param>
    /// <returns>List of aggregated stock move items</returns>
    Task<List<StockMoveItem>> GetStockMovesAsync(int stockId);

    /// <summary>
    /// Gets the current document number (CurrenFNumber) for the specified document ID
    /// </summary>
    /// <param name="documentId">The document ID (e.g., 24)</param>
    /// <returns>Current document number</returns>
    Task<int> GetCurrentDocumentNumberAsync(int documentId);
}
