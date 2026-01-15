using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using Sh.Autofit.StockExport.Commands;
using Sh.Autofit.StockExport.Models;
using Sh.Autofit.StockExport.Services.Database;
using Sh.Autofit.StockExport.Services.Excel;

namespace Sh.Autofit.StockExport.ViewModels;

/// <summary>
/// Main view model for the stock export application
/// Implements MVVM pattern with INotifyPropertyChanged
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly IStockMovesService _stockMovesService;
    private readonly IExcelExportService _excelExportService;
    private readonly ExcelImportService _excelImportService;
    private readonly PartLookupService _partLookupService;

    private string _stockId = string.Empty;
    private DocumentType? _selectedDocumentType;
    private string _description = "ללא";
    private string _docNumber = string.Empty;
    private string _savePath = string.Empty;
    private bool _isExporting;
    private string _statusMessage = "מוכן";

    // Excel import mode properties
    private bool _isExcelMode;
    private string _excelFilePath = string.Empty;
    private ExcelImportSettings? _excelImportSettings;
    private string _columnMappingSummary = string.Empty;

    /// <summary>
    /// Initializes the main view model with required services
    /// </summary>
    public MainViewModel(IStockMovesService stockMovesService, IExcelExportService excelExportService)
    {
        _stockMovesService = stockMovesService ?? throw new ArgumentNullException(nameof(stockMovesService));
        _excelExportService = excelExportService ?? throw new ArgumentNullException(nameof(excelExportService));

        // Initialize Excel import services
        var connectionString = "Data Source=server-pc\\wizsoft2;Initial Catalog=Sh.Autofit;User ID=issa;Password=5060977Ih;";
        _excelImportService = new ExcelImportService();
        _partLookupService = new PartLookupService(connectionString);

        // Initialize document types collection with both types
        DocumentTypes = new ObservableCollection<DocumentType>
        {
            new DocumentType { Value = 24, DisplayName = "יתרת פתיחה", ShouldNegateQuantities = true },
            new DocumentType { Value = 14, DisplayName = "תעודת משלוח רכש", ShouldNegateQuantities = false }
        };

        // Set default selection
        SelectedDocumentType = DocumentTypes.First();

        // Initialize commands
        BrowseCommand = new RelayCommand(_ => BrowseForSavePath());
        ExportCommand = new AsyncRelayCommand(_ => ExportToExcelAsync(), _ => CanExport());
        RefreshDocNumberCommand = new AsyncRelayCommand(_ => RefreshDocumentNumberAsync(), _ => CanRefreshDocNumber());
        BrowseExcelCommand = new RelayCommand(_ => BrowseForExcelFile());
        ConfigureColumnsCommand = new RelayCommand(_ => ConfigureColumns(), _ => HasExcelFile);
    }

    #region Properties

    /// <summary>
    /// Stock ID input (as string for binding, validated before use)
    /// </summary>
    public string StockId
    {
        get => _stockId;
        set
        {
            if (_stockId != value)
            {
                _stockId = value;
                OnPropertyChanged();
                (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Available document types for the dropdown
    /// </summary>
    public ObservableCollection<DocumentType> DocumentTypes { get; }

    /// <summary>
    /// Currently selected document type
    /// </summary>
    public DocumentType? SelectedDocumentType
    {
        get => _selectedDocumentType;
        set
        {
            if (_selectedDocumentType != value)
            {
                _selectedDocumentType = value;
                OnPropertyChanged();
                (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

                // Auto-populate document number from database when document type changes
                if (value != null)
                {
                    _ = LoadDocumentNumberAsync(value.Value);
                }
            }
        }
    }

    /// <summary>
    /// Description text for column 2
    /// </summary>
    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value;
                OnPropertyChanged();
                (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Document number for column 3 (as string for binding, validated before use)
    /// </summary>
    public string DocNumber
    {
        get => _docNumber;
        set
        {
            if (_docNumber != value)
            {
                _docNumber = value;
                OnPropertyChanged();
                (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// File path where Excel will be saved
    /// </summary>
    public string SavePath
    {
        get => _savePath;
        set
        {
            if (_savePath != value)
            {
                _savePath = value;
                OnPropertyChanged();
                (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Indicates if an export operation is in progress
    /// </summary>
    public bool IsExporting
    {
        get => _isExporting;
        set
        {
            if (_isExporting != value)
            {
                _isExporting = value;
                OnPropertyChanged();
                (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Status message displayed to the user
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indicates if Excel import mode is active
    /// </summary>
    public bool IsExcelMode
    {
        get => _isExcelMode;
        set
        {
            if (_isExcelMode != value)
            {
                _isExcelMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDbMode));
                (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

                // Clear Excel-specific data when switching modes
                if (!value)
                {
                    ExcelFilePath = string.Empty;
                    ExcelImportSettings = null;
                    ColumnMappingSummary = string.Empty;
                }
            }
        }
    }

    /// <summary>
    /// Indicates if database mode is active (inverse of IsExcelMode)
    /// </summary>
    public bool IsDbMode => !IsExcelMode;

    /// <summary>
    /// Path to the selected Excel file for import
    /// </summary>
    public string ExcelFilePath
    {
        get => _excelFilePath;
        set
        {
            if (_excelFilePath != value)
            {
                _excelFilePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasExcelFile));
                (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Indicates if an Excel file has been selected
    /// </summary>
    public bool HasExcelFile => !string.IsNullOrWhiteSpace(ExcelFilePath);

    /// <summary>
    /// Current Excel import settings (column mappings)
    /// </summary>
    public ExcelImportSettings? ExcelImportSettings
    {
        get => _excelImportSettings;
        set
        {
            _excelImportSettings = value;
            OnPropertyChanged();
            (ExportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Summary text showing current column mappings
    /// </summary>
    public string ColumnMappingSummary
    {
        get => _columnMappingSummary;
        set
        {
            _columnMappingSummary = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Command to browse for save file path
    /// </summary>
    public RelayCommand BrowseCommand { get; }

    /// <summary>
    /// Command to export to Excel
    /// </summary>
    public AsyncRelayCommand ExportCommand { get; }

    /// <summary>
    /// Command to refresh document number from database
    /// </summary>
    public AsyncRelayCommand RefreshDocNumberCommand { get; }

    /// <summary>
    /// Command to browse for Excel file to import
    /// </summary>
    public RelayCommand BrowseExcelCommand { get; }

    /// <summary>
    /// Command to configure Excel column mappings
    /// </summary>
    public RelayCommand ConfigureColumnsCommand { get; }

    #endregion

    #region Private Methods

    /// <summary>
    /// Opens a save file dialog for the user to choose where to save the Excel file
    /// </summary>
    private void BrowseForSavePath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "שמירת קובץ אקסל",
            Filter = "קבצי אקסל 97-2003 (*.xls)|*.xls",
            DefaultExt = ".xls",
            FileName = $"StockExport_{DateTime.Now:yyyyMMdd_HHmmss}.xls"
        };

        if (dialog.ShowDialog() == true)
        {
            SavePath = dialog.FileName;
            StatusMessage = $"נבחר נתיב: {System.IO.Path.GetFileName(SavePath)}";
        }
    }

    /// <summary>
    /// Determines if the export command can execute
    /// </summary>
    private bool CanExport()
    {
        if (IsExporting)
            return false;

        // Common requirements
        if (SelectedDocumentType == null ||
            string.IsNullOrWhiteSpace(Description) ||
            string.IsNullOrWhiteSpace(DocNumber) ||
            !int.TryParse(DocNumber, out var docNum) || docNum <= 0 ||
            string.IsNullOrWhiteSpace(SavePath))
        {
            return false;
        }

        // Mode-specific requirements
        if (IsDbMode)
        {
            // DB mode requires StockId
            return !string.IsNullOrWhiteSpace(StockId) &&
                   int.TryParse(StockId, out var stockId) && stockId > 0;
        }
        else
        {
            // Excel mode requires Excel file and column mappings
            return !string.IsNullOrWhiteSpace(ExcelFilePath) &&
                   ExcelImportSettings != null;
        }
    }

    /// <summary>
    /// Exports stock moves to Excel asynchronously
    /// </summary>
    private async Task ExportToExcelAsync()
    {
        if (!ValidateInputs())
            return;

        IsExporting = true;

        try
        {
            List<StockMoveItem> itemsToExport;

            if (IsDbMode)
            {
                // DB Mode: Query database for stock moves
                StatusMessage = "מבצע שאילתה במסד הנתונים...";

                var stockId = int.Parse(StockId);
                itemsToExport = await _stockMovesService.GetStockMovesAsync(stockId);

                if (itemsToExport.Count == 0)
                {
                    ShowMessage(
                        "לא נמצאו רשומות",
                        $"לא נמצאו תנועות מלאי עבור מזהה מלאי: {stockId}",
                        MessageBoxImage.Warning
                    );
                    StatusMessage = "לא נמצאו רשומות";
                    return;
                }
            }
            else
            {
                // Excel Import Mode
                StatusMessage = "קורא קובץ Excel...";

                var rawItems = await _excelImportService.ImportStockItemsAsync(ExcelImportSettings!);

                StatusMessage = "מאמת נתונים מול מאגר...";

                var validatedItems = await _partLookupService.ValidateAndResolveItemsAsync(rawItems);

                // Show validation results dialog with part lookup service for retry capability
                var validationDialog = new Views.ValidationResultsDialog(validatedItems, _partLookupService);
                if (validationDialog.ShowDialog() != true)
                {
                    StatusMessage = "הייצוא בוטל";
                    return;
                }

                // Convert validated items to StockMoveItem format
                itemsToExport = validatedItems
                    .Where(i => i.ValidationStatus == ValidationStatus.Valid)
                    .Select(i => new StockMoveItem
                    {
                        ItemKey = i.ResolvedItemKey ?? string.Empty,
                        TotalQuantity = i.Quantity
                    })
                    .ToList();
            }

            // Check if there's data to export
            if (itemsToExport.Count == 0)
            {
                ShowMessage(
                    "אין נתונים לייצוא",
                    "לא נמצאו פריטים תקינים לייצוא",
                    MessageBoxImage.Warning
                );
                StatusMessage = "אין נתונים לייצוא";
                return;
            }

            StatusMessage = $"מייצא {itemsToExport.Count} פריטים...";

            // Create export settings
            var docNumber = int.Parse(DocNumber);
            var settings = new ExportSettings
            {
                StockId = IsDbMode ? int.Parse(StockId) : 0,
                DocType = SelectedDocumentType!.Value,
                Description = Description,
                DocNumber = docNumber,
                SavePath = SavePath
            };

            // Export to Excel with appropriate quantity handling
            bool negateQuantities = SelectedDocumentType.ShouldNegateQuantities;
            await _excelExportService.ExportToExcelAsync(itemsToExport, settings, negateQuantities);

            ShowMessage(
                "הייצוא הצליח",
                $"יוצאו בהצלחה {itemsToExport.Count} פריטים אל:\n{SavePath}",
                MessageBoxImage.Information
            );

            StatusMessage = $"הייצוא הושלם בהצלחה ({itemsToExport.Count} פריטים)";
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(
                "שגיאה",
                $"כשל בעיבוד:\n{ex.Message}",
                MessageBoxImage.Error
            );
            StatusMessage = "אירעה שגיאה";
        }
        catch (Exception ex)
        {
            ShowMessage(
                "הייצוא נכשל",
                $"אירעה שגיאה במהלך הייצוא:\n{ex.Message}",
                MessageBoxImage.Error
            );
            StatusMessage = "הייצוא נכשל";
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>
    /// Validates all user inputs before export
    /// </summary>
    private bool ValidateInputs()
    {
        // Mode-specific validations
        if (IsDbMode)
        {
            // Validate Stock ID for DB mode
            if (string.IsNullOrWhiteSpace(StockId))
            {
                ShowMessage("קלט לא תקין", "נא להזין מזהה מלאי", MessageBoxImage.Warning);
                return false;
            }

            if (!int.TryParse(StockId, out var stockId) || stockId <= 0)
            {
                ShowMessage("קלט לא תקין", "מזהה מלאי חייב להיות מספר חיובי", MessageBoxImage.Warning);
                return false;
            }
        }
        else
        {
            // Validate Excel import requirements
            if (string.IsNullOrWhiteSpace(ExcelFilePath))
            {
                ShowMessage("קלט לא תקין", "נא לבחור קובץ Excel", MessageBoxImage.Warning);
                return false;
            }

            if (ExcelImportSettings == null)
            {
                ShowMessage("קלט לא תקין", "נא להגדיר מיפוי עמודות", MessageBoxImage.Warning);
                return false;
            }
        }

        // Common validations
        if (SelectedDocumentType == null)
        {
            ShowMessage("קלט לא תקין", "נא לבחור סוג מסמך", MessageBoxImage.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            ShowMessage("קלט לא תקין", "נא להזין תיאור", MessageBoxImage.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(DocNumber))
        {
            ShowMessage("קלט לא תקין", "נא להזין מספר מסמך", MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(DocNumber, out var docNum) || docNum <= 0)
        {
            ShowMessage("קלט לא תקין", "מספר מסמך חייב להיות מספר חיובי", MessageBoxImage.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(SavePath))
        {
            ShowMessage("קלט לא תקין", "נא לבחור נתיב שמירה", MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Shows a message box to the user
    /// </summary>
    private void ShowMessage(string title, string message, MessageBoxImage icon)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, icon);
    }

    /// <summary>
    /// Loads the current document number from the database based on the document type
    /// </summary>
    private async Task LoadDocumentNumberAsync(int documentId)
    {
        try
        {
            StatusMessage = "טוען מספר אסמכתא מהמסד...";

            var docNumber = await _stockMovesService.GetCurrentDocumentNumberAsync(documentId);
            DocNumber = docNumber.ToString();

            StatusMessage = $"מספר אסמכתא עודכן: {docNumber}";
        }
        catch (Exception ex)
        {
            StatusMessage = "שגיאה בטעינת מספר אסמכתא";
            ShowMessage(
                "שגיאה",
                $"כשל בטעינת מספר אסמכתא ממסד הנתונים:\n{ex.Message}",
                MessageBoxImage.Warning
            );
        }
    }

    /// <summary>
    /// Refreshes the document number from the database (invoked by the refresh button)
    /// </summary>
    private async Task RefreshDocumentNumberAsync()
    {
        if (SelectedDocumentType != null)
        {
            await LoadDocumentNumberAsync(SelectedDocumentType.Value);
        }
    }

    /// <summary>
    /// Determines if the refresh document number command can execute
    /// </summary>
    private bool CanRefreshDocNumber()
    {
        return SelectedDocumentType != null;
    }

    /// <summary>
    /// Opens a file picker to select an Excel file for import
    /// </summary>
    private void BrowseForExcelFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "בחר קובץ Excel לייבוא",
            Filter = "קבצי Excel|*.xls;*.xlsx|כל הקבצים|*.*",
            DefaultExt = ".xlsx"
        };

        if (dialog.ShowDialog() == true)
        {
            ExcelFilePath = dialog.FileName;
            ExcelImportSettings = null; // Reset mappings when new file is selected
            ColumnMappingSummary = string.Empty;
            StatusMessage = $"נבחר קובץ: {System.IO.Path.GetFileName(ExcelFilePath)}";
        }
    }

    /// <summary>
    /// Opens the column mapping dialog to configure Excel import settings
    /// </summary>
    private void ConfigureColumns()
    {
        if (string.IsNullOrWhiteSpace(ExcelFilePath))
        {
            ShowMessage("שגיאה", "נא לבחור קובץ Excel תחילה", MessageBoxImage.Warning);
            return;
        }

        try
        {
            var dialog = new Views.ColumnMappingDialog(ExcelFilePath);

            if (dialog.ShowDialog() == true && dialog.ViewModel.Result != null)
            {
                ExcelImportSettings = dialog.ViewModel.Result;

                // Build summary text
                var shCol = ExcelImportSettings.HasShCode ? GetColumnLetter(ExcelImportSettings.ShCodeColumnIndex!.Value) : "--";
                var oemCol = ExcelImportSettings.HasOemCode ? GetColumnLetter(ExcelImportSettings.OemCodeColumnIndex!.Value) : "--";
                var qtyCol = GetColumnLetter(ExcelImportSettings.QuantityColumnIndex);

                ColumnMappingSummary = $"עמודות: SH={shCol}, OEM={oemCol}, כמות={qtyCol}";
                StatusMessage = "מיפוי עמודות הוגדר בהצלחה";
            }
        }
        catch (Exception ex)
        {
            ShowMessage(
                "שגיאה",
                $"כשל בפתיחת דיאלוג מיפוי עמודות:\n{ex.Message}",
                MessageBoxImage.Error
            );
        }
    }

    /// <summary>
    /// Converts column index to Excel column letter (0=A, 1=B, 25=Z, 26=AA)
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

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
