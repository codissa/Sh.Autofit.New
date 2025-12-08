using System.IO;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using Sh.Autofit.StockExport.Models;

namespace Sh.Autofit.StockExport.Services.Excel;

/// <summary>
/// Service for exporting stock moves to Excel (.xls) with WizCount/H-ERP format
/// Uses NPOI library for Excel 97-2003 (.xls) format support
/// </summary>
public class ExcelExportService : IExcelExportService
{
    private const string WorksheetName = "Data";
    private const string NamedRangeName = "StockOpening";

    /// <summary>
    /// Exports stock move items to an Excel .xls file formatted for WizCount/H-ERP import
    /// </summary>
    /// <param name="items">The stock move items to export</param>
    /// <param name="settings">Export settings including file path and column values</param>
    /// <returns>True if export was successful</returns>
    /// <exception cref="ArgumentNullException">Thrown when items or settings are null</exception>
    /// <exception cref="InvalidOperationException">Thrown when export fails</exception>
    public async Task<bool> ExportToExcelAsync(List<StockMoveItem> items, ExportSettings settings)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.SavePath))
            throw new ArgumentException("Save path cannot be empty", nameof(settings));

        return await Task.Run(() =>
        {
            try
            {
                // Create workbook (HSSFWorkbook for .xls format)
                var workbook = new HSSFWorkbook();
                var sheet = workbook.CreateSheet(WorksheetName);

                // Create cell styles
                var headerStyle = CreateHeaderStyle(workbook);
                var textCellStyle = CreateTextCellStyle(workbook);

                // Create header row
                CreateHeaderRow(sheet, headerStyle);

                // Create data rows
                CreateDataRows(sheet, items, settings, textCellStyle);

                // Create named range for the entire table
                CreateNamedRange(workbook, items.Count);

                // Auto-size columns
                for (int i = 0; i < 5; i++)
                {
                    sheet.AutoSizeColumn(i);
                }

                // Save the workbook
                using (var fileStream = new FileStream(settings.SavePath, FileMode.Create, FileAccess.Write))
                {
                    workbook.Write(fileStream);
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to export Excel file: {ex.Message}", ex);
            }
        });
    }

    /// <summary>
    /// Creates header cell style
    /// </summary>
    private ICellStyle CreateHeaderStyle(HSSFWorkbook workbook)
    {
        var style = workbook.CreateCellStyle();
        var font = workbook.CreateFont();
        font.IsBold = true;
        style.SetFont(font);

        // Light gray background
        style.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.Grey25Percent.Index;
        style.FillPattern = FillPattern.SolidForeground;

        // Borders
        style.BorderTop = BorderStyle.Thin;
        style.BorderBottom = BorderStyle.Thin;
        style.BorderLeft = BorderStyle.Thin;
        style.BorderRight = BorderStyle.Thin;

        return style;
    }

    /// <summary>
    /// Creates text cell style (format as text with @)
    /// </summary>
    private ICellStyle CreateTextCellStyle(HSSFWorkbook workbook)
    {
        var style = workbook.CreateCellStyle();

        // Format as text - CRITICAL for H-ERP import
        var format = workbook.CreateDataFormat();
        style.DataFormat = format.GetFormat("@");

        // Borders
        style.BorderTop = BorderStyle.Thin;
        style.BorderBottom = BorderStyle.Thin;
        style.BorderLeft = BorderStyle.Thin;
        style.BorderRight = BorderStyle.Thin;

        return style;
    }

    /// <summary>
    /// Creates the header row with Hebrew column names
    /// </summary>
    private void CreateHeaderRow(ISheet sheet, ICellStyle headerStyle)
    {
        var headerRow = sheet.CreateRow(0);

        // Hebrew column headers as required by H-ERP
        CreateCell(headerRow, 0, "סוג מסמך", headerStyle);      // DocType
        CreateCell(headerRow, 1, "מפתח חשבון", headerStyle);    // Description (Account Key)
        CreateCell(headerRow, 2, "מספר אסמכתא", headerStyle);   // DocNumber (Reference Number)
        CreateCell(headerRow, 3, "מפתח פריט", headerStyle);     // ItemKey
        CreateCell(headerRow, 4, "כמות", headerStyle);          // Quantity
    }

    /// <summary>
    /// Creates data rows for each stock move item
    /// ALL COLUMNS MUST BE TEXT FORMAT for H-ERP import
    /// </summary>
    private void CreateDataRows(ISheet sheet, List<StockMoveItem> items, ExportSettings settings, ICellStyle textCellStyle)
    {
        int currentRow = 1; // Start after header

        foreach (var item in items)
        {
            var row = sheet.CreateRow(currentRow);

            // Column A: DocType - TEXT format (even though it's a number)
            CreateCell(row, 0, settings.DocType.ToString(), textCellStyle);

            // Column B: Description - TEXT format
            CreateCell(row, 1, settings.Description, textCellStyle);

            // Column C: DocNumber - TEXT format (even though it's a number)
            CreateCell(row, 2, settings.DocNumber.ToString(), textCellStyle);

            // Column D: ItemKey - TEXT format
            CreateCell(row, 3, item.ItemKey, textCellStyle);

            // Column E: Quantity - TEXT format
            // If ItemKey is "*", keep quantity as-is; otherwise make it negative
            var quantity = item.ItemKey == "*" ? item.TotalQuantity : item.TotalQuantity * -1;
            CreateCell(row, 4, quantity.ToString(), textCellStyle);

            currentRow++;
        }
    }

    /// <summary>
    /// Helper method to create a cell with value and style
    /// </summary>
    private void CreateCell(IRow row, int columnIndex, string value, ICellStyle style)
    {
        var cell = row.CreateCell(columnIndex);
        cell.SetCellValue(value);
        cell.CellStyle = style;
    }

    /// <summary>
    /// Creates a named range covering the entire table (header + data)
    /// </summary>
    private void CreateNamedRange(HSSFWorkbook workbook, int itemCount)
    {
        var lastRow = itemCount; // Header is row 0, data starts at row 1

        // Define the range A1:E{lastRow+1} (include header)
        var rangeAddress = $"Data!$A$1:$E${lastRow + 1}";

        // Create named range
        var name = workbook.CreateName();
        name.NameName = NamedRangeName;
        name.RefersToFormula = rangeAddress;
    }
}
