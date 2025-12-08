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

    private string _stockId = string.Empty;
    private DocumentType? _selectedDocumentType;
    private string _description = "ללא";
    private string _docNumber = string.Empty;
    private string _savePath = string.Empty;
    private bool _isExporting;
    private string _statusMessage = "מוכן";

    /// <summary>
    /// Initializes the main view model with required services
    /// </summary>
    public MainViewModel(IStockMovesService stockMovesService, IExcelExportService excelExportService)
    {
        _stockMovesService = stockMovesService ?? throw new ArgumentNullException(nameof(stockMovesService));
        _excelExportService = excelExportService ?? throw new ArgumentNullException(nameof(excelExportService));

        // Initialize document types collection
        DocumentTypes = new ObservableCollection<DocumentType>
        {
            new DocumentType { Value = 24, DisplayName = "יתרת פתיחה" }
        };

        // Set default selection
        SelectedDocumentType = DocumentTypes.First();

        // Initialize commands
        BrowseCommand = new RelayCommand(_ => BrowseForSavePath());
        ExportCommand = new AsyncRelayCommand(_ => ExportToExcelAsync(), _ => CanExport());
        RefreshDocNumberCommand = new AsyncRelayCommand(_ => RefreshDocumentNumberAsync(), _ => CanRefreshDocNumber());
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
        return !IsExporting &&
               !string.IsNullOrWhiteSpace(StockId) &&
               int.TryParse(StockId, out var stockId) && stockId > 0 &&
               SelectedDocumentType != null &&
               !string.IsNullOrWhiteSpace(Description) &&
               !string.IsNullOrWhiteSpace(DocNumber) &&
               int.TryParse(DocNumber, out var docNum) && docNum > 0 &&
               !string.IsNullOrWhiteSpace(SavePath);
    }

    /// <summary>
    /// Exports stock moves to Excel asynchronously
    /// </summary>
    private async Task ExportToExcelAsync()
    {
        if (!ValidateInputs())
            return;

        IsExporting = true;
        StatusMessage = "מבצע שאילתה במסד הנתונים...";

        try
        {
            // Parse inputs
            var stockId = int.Parse(StockId);
            var docNumber = int.Parse(DocNumber);

            // Query database for stock moves
            var items = await _stockMovesService.GetStockMovesAsync(stockId);

            // Check if any records were found
            if (items.Count == 0)
            {
                ShowMessage(
                    "לא נמצאו רשומות",
                    $"לא נמצאו תנועות מלאי עבור מזהה מלאי: {stockId}",
                    MessageBoxImage.Warning
                );
                StatusMessage = "לא נמצאו רשומות";
                return;
            }

            StatusMessage = $"נמצאו {items.Count} פריטים. מייצר קובץ אקסל...";

            // Create export settings
            var settings = new ExportSettings
            {
                StockId = stockId,
                DocType = SelectedDocumentType!.Value,
                Description = Description,
                DocNumber = docNumber,
                SavePath = SavePath
            };

            // Export to Excel
            await _excelExportService.ExportToExcelAsync(items, settings);

            ShowMessage(
                "הייצוא הצליח",
                $"יוצאו בהצלחה {items.Count} פריטים אל:\n{SavePath}",
                MessageBoxImage.Information
            );

            StatusMessage = $"הייצוא הושלם בהצלחה ({items.Count} פריטים)";
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(
                "שגיאת מסד נתונים",
                $"כשל בקבלת נתונים ממסד הנתונים:\n{ex.Message}",
                MessageBoxImage.Error
            );
            StatusMessage = "אירעה שגיאת מסד נתונים";
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
        // Validate Stock ID
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

        // Validate Document Type
        if (SelectedDocumentType == null)
        {
            ShowMessage("קלט לא תקין", "נא לבחור סוג מסמך", MessageBoxImage.Warning);
            return false;
        }

        // Validate Description
        if (string.IsNullOrWhiteSpace(Description))
        {
            ShowMessage("קלט לא תקין", "נא להזין תיאור", MessageBoxImage.Warning);
            return false;
        }

        // Validate Doc Number
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

        // Validate Save Path
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

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
