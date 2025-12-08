using Sh.Autofit.StockExport.Models;

namespace Sh.Autofit.StockExport.Services.Excel;

/// <summary>
/// Interface for exporting stock moves to Excel
/// </summary>
public interface IExcelExportService
{
    /// <summary>
    /// Exports stock move items to an Excel file formatted for WizCount/H-ERP import
    /// </summary>
    /// <param name="items">The stock move items to export</param>
    /// <param name="settings">Export settings including file path and column values</param>
    /// <returns>True if export was successful</returns>
    Task<bool> ExportToExcelAsync(List<StockMoveItem> items, ExportSettings settings);
}
