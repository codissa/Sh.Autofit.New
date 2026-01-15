using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sh.Autofit.StockExport.Commands;
using Sh.Autofit.StockExport.Models;
using Sh.Autofit.StockExport.Services.Excel;

namespace Sh.Autofit.StockExport.ViewModels;

/// <summary>
/// ViewModel for the Column Mapping Dialog
/// Allows user to map Excel columns to required fields
/// </summary>
public class ColumnMappingDialogViewModel : INotifyPropertyChanged
{
    private readonly ExcelImportService _excelImportService;
    private readonly string _excelFilePath;

    private ObservableCollection<string> _worksheetNames = new();
    private string? _selectedWorksheet;
    private DataTable? _previewData;
    private ObservableCollection<string> _availableColumns = new();
    private string? _selectedShCodeColumn;
    private string? _selectedOemCodeColumn;
    private string? _selectedQuantityColumn;
    private int _startRow = 2;
    private bool _canConfirm;
    private string _statusMessage = string.Empty;
    private bool _isLoading;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ColumnMappingDialogViewModel(string excelFilePath)
    {
        _excelFilePath = excelFilePath ?? throw new ArgumentNullException(nameof(excelFilePath));
        _excelImportService = new ExcelImportService();

        // Initialize commands
        ConfirmCommand = new RelayCommand(_ => OnConfirm(), _ => CanConfirm);
        CancelCommand = new RelayCommand(_ => OnCancel());
        WorksheetChangedCommand = new AsyncRelayCommand(_ => LoadPreviewAsync());

        // Load worksheets
        _ = LoadWorksheetsAsync();
    }

    #region Properties

    public ObservableCollection<string> WorksheetNames
    {
        get => _worksheetNames;
        set { _worksheetNames = value; OnPropertyChanged(); }
    }

    public string? SelectedWorksheet
    {
        get => _selectedWorksheet;
        set
        {
            _selectedWorksheet = value;
            OnPropertyChanged();
        }
    }

    public DataTable? PreviewData
    {
        get => _previewData;
        set { _previewData = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> AvailableColumns
    {
        get => _availableColumns;
        set { _availableColumns = value; OnPropertyChanged(); }
    }

    public string? SelectedShCodeColumn
    {
        get => _selectedShCodeColumn;
        set
        {
            _selectedShCodeColumn = value;
            OnPropertyChanged();
            ValidateMappings();
        }
    }

    public string? SelectedOemCodeColumn
    {
        get => _selectedOemCodeColumn;
        set
        {
            _selectedOemCodeColumn = value;
            OnPropertyChanged();
            ValidateMappings();
        }
    }

    public string? SelectedQuantityColumn
    {
        get => _selectedQuantityColumn;
        set
        {
            _selectedQuantityColumn = value;
            OnPropertyChanged();
            ValidateMappings();
        }
    }

    public int StartRow
    {
        get => _startRow;
        set
        {
            if (value < 1) value = 1;
            _startRow = value;
            OnPropertyChanged();
        }
    }

    public bool CanConfirm
    {
        get => _canConfirm;
        private set { _canConfirm = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    #endregion

    #region Commands

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand WorksheetChangedCommand { get; }

    #endregion

    #region Public Properties for Dialog Result

    public bool DialogResult { get; private set; }
    public ExcelImportSettings? Result { get; private set; }

    #endregion

    #region Private Methods

    private async Task LoadWorksheetsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען רשימת גיליונות...";

            var sheets = await _excelImportService.GetWorksheetNamesAsync(_excelFilePath);

            WorksheetNames.Clear();
            foreach (var sheet in sheets)
            {
                WorksheetNames.Add(sheet);
            }

            // Select first sheet by default
            if (WorksheetNames.Count > 0)
            {
                SelectedWorksheet = WorksheetNames[0];
                await LoadPreviewAsync();
            }

            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה בטעינת הקובץ: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedWorksheet))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "טוען תצוגה מקדימה...";

            PreviewData = await _excelImportService.GetColumnPreviewAsync(_excelFilePath, SelectedWorksheet, 10);

            // Update available columns based on preview
            AvailableColumns.Clear();
            AvailableColumns.Add("-- לא נבחר --");

            if (PreviewData != null)
            {
                foreach (DataColumn column in PreviewData.Columns)
                {
                    AvailableColumns.Add($"עמודה {column.ColumnName}");
                }
            }

            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה בטעינת תצוגה מקדימה: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ValidateMappings()
    {
        // Quantity is required
        bool hasQuantity = !string.IsNullOrWhiteSpace(SelectedQuantityColumn) &&
                          SelectedQuantityColumn != "-- לא נבחר --";

        // At least one of SH Code or OEM Code must be selected
        bool hasShCode = !string.IsNullOrWhiteSpace(SelectedShCodeColumn) &&
                        SelectedShCodeColumn != "-- לא נבחר --";

        bool hasOemCode = !string.IsNullOrWhiteSpace(SelectedOemCodeColumn) &&
                         SelectedOemCodeColumn != "-- לא נבחר --";

        CanConfirm = hasQuantity && (hasShCode || hasOemCode);
    }

    private void OnConfirm()
    {
        // Create ExcelImportSettings from selections
        Result = new ExcelImportSettings
        {
            FilePath = _excelFilePath,
            SheetName = SelectedWorksheet ?? string.Empty,
            ShCodeColumnIndex = GetColumnIndex(SelectedShCodeColumn),
            OemCodeColumnIndex = GetColumnIndex(SelectedOemCodeColumn),
            QuantityColumnIndex = GetColumnIndex(SelectedQuantityColumn) ?? 0,
            StartRow = StartRow
        };

        DialogResult = true;
    }

    private void OnCancel()
    {
        DialogResult = false;
        Result = null;
    }

    private int? GetColumnIndex(string? columnSelection)
    {
        if (string.IsNullOrWhiteSpace(columnSelection) || columnSelection == "-- לא נבחר --")
            return null;

        // Extract column letter from "עמודה A" format
        var parts = columnSelection.Split(' ');
        if (parts.Length < 2)
            return null;

        string columnLetter = parts[1];

        // Convert column letter to index (A=0, B=1, etc.)
        return ConvertColumnLetterToIndex(columnLetter);
    }

    private int ConvertColumnLetterToIndex(string columnLetter)
    {
        int index = 0;
        for (int i = 0; i < columnLetter.Length; i++)
        {
            index = index * 26 + (columnLetter[i] - 'A' + 1);
        }
        return index - 1; // Convert to 0-based
    }

    #endregion

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
