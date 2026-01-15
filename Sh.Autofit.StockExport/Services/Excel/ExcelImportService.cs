using System.Data;
using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Sh.Autofit.StockExport.Models;

namespace Sh.Autofit.StockExport.Services.Excel;

/// <summary>
/// Service for importing stock data from Excel files (.xls and .xlsx)
/// Uses NPOI library for reading Excel files
/// </summary>
public class ExcelImportService
{
    /// <summary>
    /// Gets the list of worksheet names from an Excel file
    /// </summary>
    /// <param name="filePath">Path to the Excel file</param>
    /// <returns>List of worksheet names</returns>
    public async Task<List<string>> GetWorksheetNamesAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Excel file not found: {filePath}");

        return await Task.Run(() =>
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var workbook = WorkbookFactory.Create(fileStream);

                var sheetNames = new List<string>();
                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    sheetNames.Add(workbook.GetSheetName(i));
                }

                return sheetNames;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read Excel file: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Gets a preview of the Excel data (column headers and sample rows)
    /// </summary>
    /// <param name="filePath">Path to the Excel file</param>
    /// <param name="sheetName">Name of the worksheet</param>
    /// <param name="previewRows">Number of rows to preview (default: 10)</param>
    /// <returns>DataTable containing preview data</returns>
    public async Task<DataTable> GetColumnPreviewAsync(string filePath, string sheetName, int previewRows = 10)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        if (string.IsNullOrWhiteSpace(sheetName))
            throw new ArgumentException("Sheet name cannot be empty", nameof(sheetName));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Excel file not found: {filePath}");

        return await Task.Run(() =>
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var workbook = WorkbookFactory.Create(fileStream);
                var sheet = workbook.GetSheet(sheetName);

                if (sheet == null)
                    throw new InvalidOperationException($"Worksheet '{sheetName}' not found in file");

                var dataTable = new DataTable();

                // Determine number of columns from first row
                var firstRow = sheet.GetRow(sheet.FirstRowNum);
                if (firstRow == null)
                    return dataTable;

                int columnCount = firstRow.LastCellNum;

                // Add columns with letter names (A, B, C, etc.)
                for (int i = 0; i < columnCount; i++)
                {
                    dataTable.Columns.Add(GetColumnLetter(i));
                }

                // Read preview rows
                int rowsRead = 0;
                for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum && rowsRead < previewRows; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (row == null)
                        continue;

                    var dataRow = dataTable.NewRow();
                    for (int colIndex = 0; colIndex < columnCount; colIndex++)
                    {
                        var cell = row.GetCell(colIndex);
                        dataRow[colIndex] = GetCellValueAsString(cell);
                    }

                    dataTable.Rows.Add(dataRow);
                    rowsRead++;
                }

                return dataTable;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to read Excel preview: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Imports stock items from Excel file based on provided settings
    /// </summary>
    /// <param name="settings">Import settings including column mappings</param>
    /// <returns>List of imported items with raw data</returns>
    public async Task<List<ImportedStockItem>> ImportStockItemsAsync(ExcelImportSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.FilePath))
            throw new ArgumentException("File path cannot be empty", nameof(settings));

        if (!File.Exists(settings.FilePath))
            throw new FileNotFoundException($"Excel file not found: {settings.FilePath}");

        return await Task.Run(() =>
        {
            var items = new List<ImportedStockItem>();

            try
            {
                using var fileStream = new FileStream(settings.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var workbook = WorkbookFactory.Create(fileStream);
                var sheet = workbook.GetSheet(settings.SheetName);

                if (sheet == null)
                    throw new InvalidOperationException($"Worksheet '{settings.SheetName}' not found in file");

                // Read data rows (StartRow is 1-based, but row indices are 0-based)
                int startRowIndex = settings.StartRow - 1;

                for (int rowIndex = startRowIndex; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (row == null)
                        continue;

                    // Check if row is empty (all cells blank)
                    bool isEmptyRow = true;
                    for (int i = row.FirstCellNum; i < row.LastCellNum; i++)
                    {
                        var cell = row.GetCell(i);
                        if (cell != null && cell.CellType != CellType.Blank)
                        {
                            isEmptyRow = false;
                            break;
                        }
                    }

                    if (isEmptyRow)
                        continue;

                    var item = new ImportedStockItem
                    {
                        RowNumber = rowIndex + 1 // Convert to 1-based for user display
                    };

                    // Read SH code if column is mapped
                    if (settings.ShCodeColumnIndex.HasValue)
                    {
                        var cell = row.GetCell(settings.ShCodeColumnIndex.Value);
                        item.RawShCode = GetCellValueAsString(cell)?.Trim();
                    }

                    // Read OEM code if column is mapped
                    if (settings.OemCodeColumnIndex.HasValue)
                    {
                        var cell = row.GetCell(settings.OemCodeColumnIndex.Value);
                        item.RawOemCode = GetCellValueAsString(cell)?.Trim();
                    }

                    // Read quantity (required field)
                    var quantityCell = row.GetCell(settings.QuantityColumnIndex);
                    string quantityString = GetCellValueAsString(quantityCell);

                    if (string.IsNullOrWhiteSpace(quantityString))
                    {
                        item.ValidationStatus = ValidationStatus.InvalidQuantity;
                        item.ValidationMessage = "כמות חסרה";
                        items.Add(item);
                        continue;
                    }

                    if (!double.TryParse(quantityString, out double quantity))
                    {
                        item.ValidationStatus = ValidationStatus.InvalidQuantity;
                        item.ValidationMessage = $"כמות לא חוקית: '{quantityString}'";
                        items.Add(item);
                        continue;
                    }

                    item.Quantity = quantity;

                    // Validate that at least one code (SH or OEM) is present
                    if (string.IsNullOrWhiteSpace(item.RawShCode) && string.IsNullOrWhiteSpace(item.RawOemCode))
                    {
                        item.ValidationStatus = ValidationStatus.OemCodeNotFound;
                        item.ValidationMessage = "חסר קוד SH או קוד OEM";
                        items.Add(item);
                        continue;
                    }

                    items.Add(item);
                }

                return items;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to import Excel data: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Converts a cell to its string representation, handling all cell types
    /// </summary>
    private string? GetCellValueAsString(ICell? cell)
    {
        if (cell == null)
            return null;

        switch (cell.CellType)
        {
            case CellType.String:
                return cell.StringCellValue;

            case CellType.Numeric:
                if (DateUtil.IsCellDateFormatted(cell))
                {
                    return cell.DateCellValue.ToString();
                }
                else
                {
                    // Return numeric value as string
                    // Remove unnecessary decimal points for whole numbers
                    double numValue = cell.NumericCellValue;
                    if (numValue == Math.Floor(numValue))
                        return ((long)numValue).ToString();
                    else
                        return numValue.ToString();
                }

            case CellType.Boolean:
                return cell.BooleanCellValue.ToString();

            case CellType.Formula:
                // Try to get the cached formula result
                try
                {
                    switch (cell.CachedFormulaResultType)
                    {
                        case CellType.String:
                            return cell.StringCellValue;
                        case CellType.Numeric:
                            double numValue = cell.NumericCellValue;
                            if (numValue == Math.Floor(numValue))
                                return ((long)numValue).ToString();
                            else
                                return numValue.ToString();
                        case CellType.Boolean:
                            return cell.BooleanCellValue.ToString();
                        default:
                            return cell.ToString();
                    }
                }
                catch
                {
                    return cell.ToString();
                }

            case CellType.Blank:
                return null;

            case CellType.Error:
                return $"#ERROR";

            default:
                return cell.ToString();
        }
    }

    /// <summary>
    /// Converts a column index to Excel column letter (0=A, 1=B, 25=Z, 26=AA, etc.)
    /// </summary>
    private string GetColumnLetter(int columnIndex)
    {
        string columnLetter = "";

        while (columnIndex >= 0)
        {
            columnLetter = (char)('A' + (columnIndex % 26)) + columnLetter;
            columnIndex = (columnIndex / 26) - 1;
        }

        return columnLetter;
    }
}
