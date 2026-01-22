using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Sh.Autofit.StickerPrinting.Commands;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Database;
using Sh.Autofit.StickerPrinting.Services.Label;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.ViewModels;

public class StockMoveViewModel : INotifyPropertyChanged
{
    // Services
    private readonly IStockDataService _stockDataService;
    private readonly IPartDataService _partDataService;
    private readonly IPrinterService _printerService;
    private readonly ILabelRenderService _labelRenderService;
    private readonly ILabelPreviewService? _previewService;
    private readonly IArabicDescriptionService? _arabicDescService;

    // State fields
    private string _stockIdInput = string.Empty;
    private StockInfo? _currentStockInfo;
    private ObservableCollection<StockMoveLabelItem> _items = new();
    private string _globalLanguage = "ar";
    private string _selectedPrinter = string.Empty;

    // Loading states
    private bool _isLoadingStock = false;
    private bool _isPrinting = false;
    private string _statusMessage = string.Empty;

    // Progress tracking
    private int _printProgress = 0;
    private int _printTotal = 0;
    private string _printProgressText = string.Empty;

    // Preview cache to avoid regenerating
    private readonly Dictionary<string, BitmapSource> _previewCache = new();
    private readonly StickerSettings _settings = new();

    #region Properties

    public string StockIdInput
    {
        get => _stockIdInput;
        set { _stockIdInput = value; OnPropertyChanged(); LoadStockCommand.RaiseCanExecuteChanged(); }
    }

    public StockInfo? CurrentStockInfo
    {
        get => _currentStockInfo;
        set { _currentStockInfo = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStockLoaded)); }
    }

    public bool HasStockLoaded => CurrentStockInfo != null;

    public ObservableCollection<StockMoveLabelItem> Items
    {
        get => _items;
        set { _items = value; OnPropertyChanged(); }
    }

    public string GlobalLanguage
    {
        get => _globalLanguage;
        set
        {
            if (_globalLanguage != value)
            {
                _globalLanguage = value;
                OnPropertyChanged();
                ApplyGlobalLanguage();
            }
        }
    }

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set { _selectedPrinter = value; OnPropertyChanged(); PrintAllCommand.RaiseCanExecuteChanged(); }
    }

    public bool IsLoadingStock
    {
        get => _isLoadingStock;
        set
        {
            _isLoadingStock = value;
            OnPropertyChanged();
            LoadStockCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsPrinting
    {
        get => _isPrinting;
        set
        {
            _isPrinting = value;
            OnPropertyChanged();
            PrintAllCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public int PrintProgress
    {
        get => _printProgress;
        set { _printProgress = value; OnPropertyChanged(); }
    }

    public int PrintTotal
    {
        get => _printTotal;
        set { _printTotal = value; OnPropertyChanged(); }
    }

    public string PrintProgressText
    {
        get => _printProgressText;
        set { _printProgressText = value; OnPropertyChanged(); }
    }

    #endregion

    #region Commands

    public AsyncRelayCommand LoadStockCommand { get; }
    public AsyncRelayCommand PrintAllCommand { get; }
    public RelayCommand AddItemCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    public AsyncRelayCommand EditArabicCommand { get; }

    #endregion

    public StockMoveViewModel(
        IStockDataService stockDataService,
        IPartDataService partDataService,
        IPrinterService printerService,
        ILabelRenderService labelRenderService,
        ILabelPreviewService? previewService = null,
        IArabicDescriptionService? arabicDescService = null)
    {
        _stockDataService = stockDataService ?? throw new ArgumentNullException(nameof(stockDataService));
        _partDataService = partDataService ?? throw new ArgumentNullException(nameof(partDataService));
        _printerService = printerService ?? throw new ArgumentNullException(nameof(printerService));
        _labelRenderService = labelRenderService ?? throw new ArgumentNullException(nameof(labelRenderService));
        _previewService = previewService;
        _arabicDescService = arabicDescService;

        // Initialize commands
        LoadStockCommand = new AsyncRelayCommand(_ => LoadStockAsync(), _ => CanLoadStock());
        PrintAllCommand = new AsyncRelayCommand(_ => PrintAllAsync(), _ => CanPrintAll());
        AddItemCommand = new RelayCommand(_ => ShowAddItemDialog());
        RemoveItemCommand = new RelayCommand(RemoveItem);
        EditArabicCommand = new AsyncRelayCommand(EditArabicDescriptionAsync);
    }

    #region Command Methods

    private bool CanLoadStock() =>
        !string.IsNullOrWhiteSpace(StockIdInput) &&
        int.TryParse(StockIdInput, out _) &&
        !IsLoadingStock;

    private bool CanPrintAll() =>
        Items.Count > 0 &&
        !string.IsNullOrEmpty(SelectedPrinter) &&
        !IsPrinting;

    private async Task LoadStockAsync()
    {
        if (!int.TryParse(StockIdInput, out int stockId))
        {
            MessageBox.Show("מספר תנועת מלאי לא חוקי", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsLoadingStock = true;
        StatusMessage = "טוען תנועת מלאי...";

        // Cleanup existing items
        foreach (var item in Items)
        {
            item.PreviewUpdateRequested -= OnItemPreviewUpdateRequested;
            item.Cleanup();
        }
        Items.Clear();
        _previewCache.Clear();

        try
        {
            // Load stock info
            var stockInfo = await _stockDataService.GetStockInfoAsync(stockId);
            if (stockInfo == null)
            {
                MessageBox.Show($"תנועת מלאי {stockId} לא נמצאה", "לא נמצא",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusMessage = "לא נמצא";
                CurrentStockInfo = null;
                return;
            }

            CurrentStockInfo = stockInfo;

            // Load stock move items
            var stockMoves = await _stockDataService.GetStockMovesAsync(stockId);
            StatusMessage = $"טוען {stockMoves.Count} פריטים...";

            // Load part info for each item and create label items
            foreach (var moveItem in stockMoves)
            {
                var partInfo = await _partDataService.GetPartByItemKeyAsync(moveItem.ItemKey);
                if (partInfo == null) continue;

                var labelData = _labelRenderService.CreateLabelData(
                    moveItem.ItemKey,
                    partInfo,
                    _globalLanguage);

                labelData.Quantity = (int)Math.Max(1, moveItem.TotalQuantity);

                var item = new StockMoveLabelItem(labelData)
                {
                    OriginalArabicDescription = partInfo.ArabicDescription ?? string.Empty
                };

                // Subscribe to preview update requests
                item.PreviewUpdateRequested += OnItemPreviewUpdateRequested;

                Items.Add(item);
            }

            // Generate previews asynchronously
            _ = GenerateAllPreviewsAsync();

            StatusMessage = $"נטענו {Items.Count} פריטים";
            PrintAllCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בטעינה: {ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "שגיאה בטעינה";
        }
        finally
        {
            IsLoadingStock = false;
        }
    }

    private async Task PrintAllAsync()
    {
        if (Items.Count == 0)
        {
            MessageBox.Show("אין פריטים להדפסה", "שים לב",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsPrinting = true;
        PrintProgress = 0;
        PrintTotal = Items.Sum(x => x.Quantity);
        PrintProgressText = $"מדפיס 0/{PrintTotal}";
        StatusMessage = "מתחיל הדפסה...";

        try
        {
            int printed = 0;

            // Print each item
            foreach (var item in Items)
            {
                if (item.Quantity <= 0) continue;

                await _printerService.PrintLabelAsync(
                    item.LabelData,
                    _settings,
                    SelectedPrinter,
                    item.Quantity);

                printed += item.Quantity;
                PrintProgress = printed;
                PrintProgressText = $"מדפיס {printed}/{PrintTotal}";
            }

            StatusMessage = $"הודפסו {PrintTotal} תוויות בהצלחה";
            PrintProgressText = $"הושלם {PrintTotal}/{PrintTotal}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בהדפסה: {ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "ההדפסה נכשלה";
        }
        finally
        {
            IsPrinting = false;
        }
    }

    private void ShowAddItemDialog()
    {
        // Show dialog to manually add an item by ItemKey
        var dialog = new Views.AddItemDialog { Owner = Application.Current.MainWindow };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ItemKey))
        {
            _ = AddItemAsync(dialog.ItemKey, dialog.Quantity);
        }
    }

    private async Task AddItemAsync(string itemKey, int quantity)
    {
        StatusMessage = "מוסיף פריט...";

        try
        {
            // Check if item already exists
            if (Items.Any(x => x.ItemKey.Equals(itemKey, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"פריט {itemKey} כבר קיים ברשימה", "שים לב",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var partInfo = await _partDataService.GetPartByItemKeyAsync(itemKey);
            if (partInfo == null)
            {
                MessageBox.Show($"פריט {itemKey} לא נמצא", "לא נמצא",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusMessage = "פריט לא נמצא";
                return;
            }

            var labelData = _labelRenderService.CreateLabelData(itemKey, partInfo, _globalLanguage);
            labelData.Quantity = quantity;

            var item = new StockMoveLabelItem(labelData)
            {
                OriginalArabicDescription = partInfo.ArabicDescription ?? string.Empty
            };

            item.PreviewUpdateRequested += OnItemPreviewUpdateRequested;
            Items.Add(item);

            // Generate preview
            await GeneratePreviewForItemAsync(item);

            StatusMessage = $"נוסף פריט {itemKey}";
            PrintAllCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בהוספת פריט: {ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoveItem(object? param)
    {
        if (param is StockMoveLabelItem item)
        {
            item.PreviewUpdateRequested -= OnItemPreviewUpdateRequested;
            item.Cleanup();
            Items.Remove(item);

            // Remove from cache
            var cacheKey = GetCacheKey(item);
            _previewCache.Remove(cacheKey);

            StatusMessage = $"הוסר פריט {item.ItemKey}";
            PrintAllCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task EditArabicDescriptionAsync(object? param)
    {
        if (param is not StockMoveLabelItem item || !item.IsArabic)
            return;

        var dialog = new Views.EditArabicDialog
        {
            ArabicDescription = item.Description,
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            StatusMessage = "שומר תיאור ערבית...";

            try
            {
                // Save to database
                if (_arabicDescService != null)
                {
                    await _arabicDescService.SaveArabicDescriptionAsync(
                        item.ItemKey,
                        dialog.ArabicDescription,
                        Environment.UserName);
                }

                // Update item
                item.Description = dialog.ArabicDescription;
                item.OriginalArabicDescription = dialog.ArabicDescription;

                // Invalidate cache for this item
                var cacheKey = GetCacheKey(item);
                _previewCache.Remove(cacheKey);

                // Refresh preview
                await GeneratePreviewForItemAsync(item);

                StatusMessage = "תיאור ערבית נשמר";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"שגיאה בשמירה: {ex.Message}", "שגיאה",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "השמירה נכשלה";
            }
        }
    }

    #endregion

    #region Helper Methods

    private void ApplyGlobalLanguage()
    {
        // Clear cache when language changes
        _previewCache.Clear();

        // Update all items and refresh descriptions
        _ = RefreshAllDescriptionsAsync();
    }

    private async Task RefreshAllDescriptionsAsync()
    {
        StatusMessage = "מעדכן שפה...";

        foreach (var item in Items)
        {
            await RefreshItemDescriptionAsync(item);
        }

        StatusMessage = $"{Items.Count} פריטים עודכנו";
    }

    private async Task RefreshItemDescriptionAsync(StockMoveLabelItem item)
    {
        try
        {
            var partInfo = await _partDataService.GetPartByItemKeyAsync(item.ItemKey);
            if (partInfo != null)
            {
                // Update language first
                item.LabelData.Language = _globalLanguage;

                // Update description based on language
                if (item.IsArabic)
                {
                    item.LabelData.Description = partInfo.ArabicDescription ?? string.Empty;
                }
                else
                {
                    item.LabelData.Description = partInfo.HebrewDescription ?? partInfo.PartName;
                }

                await GeneratePreviewForItemAsync(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to refresh description: {ex.Message}");
        }
    }

    private void OnItemPreviewUpdateRequested(object? sender, EventArgs e)
    {
        if (sender is StockMoveLabelItem item)
        {
            // Invalidate cache and regenerate preview
            var cacheKey = GetCacheKey(item);
            _previewCache.Remove(cacheKey);
            _ = GeneratePreviewForItemAsync(item);
        }
    }

    private async Task GenerateAllPreviewsAsync()
    {
        foreach (var item in Items)
        {
            await GeneratePreviewForItemAsync(item);
        }
    }

    private async Task GeneratePreviewForItemAsync(StockMoveLabelItem item)
    {
        if (_previewService == null) return;

        item.IsLoadingPreview = true;

        try
        {
            // Check cache first
            var cacheKey = GetCacheKey(item);
            if (_previewCache.TryGetValue(cacheKey, out var cached))
            {
                item.PreviewImage = cached;
                item.IsLoadingPreview = false;
                return;
            }

            // Generate preview on background thread
            var preview = await Task.Run(() =>
                _previewService.GeneratePreview(item.LabelData, _settings));

            if (preview != null)
            {
                _previewCache[cacheKey] = preview;
                item.PreviewImage = preview;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Preview failed: {ex.Message}");
        }
        finally
        {
            item.IsLoadingPreview = false;
        }
    }

    private string GetCacheKey(StockMoveLabelItem item)
    {
        return $"{item.ItemKey}_{item.Language}_{item.Description?.GetHashCode() ?? 0}";
    }

    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
