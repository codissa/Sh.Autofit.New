using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Sh.Autofit.StockExport.Commands;
using Sh.Autofit.StockExport.Models;
using Sh.Autofit.StockExport.Services.Database;

namespace Sh.Autofit.StockExport.ViewModels;

/// <summary>
/// ViewModel for the Validation Results Dialog
/// Shows summary statistics and allows manual resolution of validation issues
/// </summary>
public class ValidationResultsDialogViewModel : INotifyPropertyChanged
{
    private readonly List<ImportedStockItem> _allItems;
    private readonly PartLookupService? _partLookupService;
    private ObservableCollection<ImportedStockItem> _problematicItems = new();
    private int _validItemsCount;
    private int _itemsWithMultipleMatches;
    private int _itemsNotFound;
    private double _totalQuantity;
    private bool _canContinue;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ValidationResultsDialogViewModel(List<ImportedStockItem> items, PartLookupService? partLookupService = null)
    {
        _allItems = items ?? throw new ArgumentNullException(nameof(items));
        _partLookupService = partLookupService;

        // Initialize commands
        ContinueCommand = new RelayCommand(_ => OnContinue(), _ => CanContinue);
        CancelCommand = new RelayCommand(_ => OnCancel());
        SelectOemMatchCommand = new RelayCommand(item => SelectOemMatch((ImportedStockItem)item!));
        DeleteItemCommand = new RelayCommand(item => DeleteItem((ImportedStockItem)item!));
        RetryValidationCommand = new AsyncRelayCommand(async item => await RetryValidation((ImportedStockItem)item!));

        // Calculate initial statistics and populate problematic items
        CalculateStatistics();
        PopulateProblematicItems();
        CheckCanContinue();
    }

    #region Properties

    public ObservableCollection<ImportedStockItem> ProblematicItems
    {
        get => _problematicItems;
        set { _problematicItems = value; OnPropertyChanged(); }
    }

    public int ValidItemsCount
    {
        get => _validItemsCount;
        private set { _validItemsCount = value; OnPropertyChanged(); }
    }

    public int ItemsWithMultipleMatches
    {
        get => _itemsWithMultipleMatches;
        private set { _itemsWithMultipleMatches = value; OnPropertyChanged(); }
    }

    public int ItemsNotFound
    {
        get => _itemsNotFound;
        private set { _itemsNotFound = value; OnPropertyChanged(); }
    }

    public double TotalQuantity
    {
        get => _totalQuantity;
        private set { _totalQuantity = value; OnPropertyChanged(); }
    }

    public bool CanContinue
    {
        get => _canContinue;
        private set { _canContinue = value; OnPropertyChanged(); }
    }

    public string SummaryText =>
        $"✓ {ValidItemsCount} שורות תקינות | " +
        $"⚠ {ItemsWithMultipleMatches} התאמות מרובות | " +
        $"✗ {ItemsNotFound} לא נמצאו";

    public string TotalQuantityText => $"סה\"כ כמות: {TotalQuantity:N0} יחידות";

    #endregion

    #region Commands

    public ICommand ContinueCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SelectOemMatchCommand { get; }
    public ICommand DeleteItemCommand { get; }
    public ICommand RetryValidationCommand { get; }

    #endregion

    #region Public Properties for Dialog Result

    //public bool DialogResult { get; private set; }

    #endregion

    #region Private Methods

    private void CalculateStatistics()
    {
        ValidItemsCount = _allItems.Count(i => i.ValidationStatus == ValidationStatus.Valid);

        ItemsWithMultipleMatches = _allItems.Count(i => i.ValidationStatus == ValidationStatus.MultipleOemMatches);

        ItemsNotFound = _allItems.Count(i =>
            i.ValidationStatus == ValidationStatus.ShCodeNotFound ||
            i.ValidationStatus == ValidationStatus.OemCodeNotFound);

        TotalQuantity = _allItems
            .Where(i => i.ValidationStatus == ValidationStatus.Valid)
            .Sum(i => i.Quantity);

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(TotalQuantityText));
    }

    private void PopulateProblematicItems()
    {
        ProblematicItems.Clear();
        foreach (var item in _allItems.Where(i => i.IsProblematic))
        {
            ProblematicItems.Add(item);
        }
    }

    private void CheckCanContinue()
    {
        // Can only continue if all items are valid (no problematic items)
        CanContinue = !_allItems.Any(i => i.IsProblematic);
    }

    private void SelectOemMatch(ImportedStockItem item)
    {
        if (item == null || item.ValidationStatus != ValidationStatus.MultipleOemMatches)
            return;

        // Open OemMatchSelectionDialog
        var dialog = new Views.OemMatchSelectionDialog(item.MatchedParts, item.RawOemCode ?? "");

        if (dialog.ShowDialog() == true && dialog.ViewModel.SelectedPart != null)
        {
            // User selected a part - update the item
            item.ResolvedItemKey = dialog.ViewModel.SelectedPart.PartNumber;
            item.ValidationStatus = ValidationStatus.Valid;
            item.ValidationMessage = $"נבחר: {dialog.ViewModel.SelectedPart.DisplayText}";

            // Refresh statistics
            CalculateStatistics();
            PopulateProblematicItems();
            CheckCanContinue();
        }
    }

    private void DeleteItem(ImportedStockItem item)
    {
        if (item == null)
            return;

        // Remove from both collections
        _allItems.Remove(item);
        ProblematicItems.Remove(item);

        // Refresh statistics
        CalculateStatistics();
        CheckCanContinue();
    }

    private async Task RetryValidation(ImportedStockItem item)
    {
        if (item == null || _partLookupService == null)
            return;

        // Check if user entered a manual SH code
        if (string.IsNullOrWhiteSpace(item.ManualShCode))
        {
            item.ValidationMessage = "אנא הזן קוד SH ידני";
            return;
        }

        try
        {
            // Validate the manually entered SH code
            bool isValid = await _partLookupService.ValidateShCodeAsync(item.ManualShCode);

            if (isValid)
            {
                // Update item with valid manual code
                item.ResolvedItemKey = item.ManualShCode;
                item.ValidationStatus = ValidationStatus.Valid;
                item.ValidationMessage = $"אומת ידנית: {item.ManualShCode}";

                // Refresh statistics
                CalculateStatistics();
                PopulateProblematicItems();
                CheckCanContinue();
            }
            else
            {
                // Manual code not found
                item.ValidationMessage = $"קוד SH '{item.ManualShCode}' לא נמצא במאגר";
            }
        }
        catch (Exception ex)
        {
            item.ValidationMessage = $"שגיאה באימות: {ex.Message}";
        }
    }

    private void OnContinue()
    {
      //  DialogResult = true;
    }

    private void OnCancel()
    {
       // DialogResult = false;
    }

    #endregion

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
