using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sh.Autofit.StickerPrinting.Commands;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Database;
using Sh.Autofit.StickerPrinting.Services.Label;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.ViewModels;

public class PrintOnDemandViewModel : INotifyPropertyChanged
{
    private readonly IPartDataService _partDataService;
    private readonly IArabicDescriptionService _arabicDescService;
    private readonly IPrinterService _printerService;
    private readonly ILabelRenderService _labelRenderService;
    private readonly ILabelPreviewService? _previewService;

    private string _itemKey = string.Empty;
    private string _selectedLanguage = "he";
    private LabelData? _currentLabel;
    private bool _isLoading = false;
    private string _statusMessage = string.Empty;
    private string _selectedPrinter = string.Empty;
    private string? _currentLocalization;

    // AutoSuggest fields
    private ObservableCollection<PartInfo> _searchResults = new();
    private bool _isDropdownOpen = false;
    private PartInfo? _selectedSearchResult;
    private CancellationTokenSource? _searchCts;
    private DispatcherTimer? _debounceTimer;
    private bool _suppressSearch = false;

    // Print feedback fields
    private bool _isPrinting = false;
    private string _lastPrintedInfo = string.Empty;

    // Print debounce
    private DateTime _lastPrintTime = DateTime.MinValue;
    private const int PrintCooldownMs = 200;

    // Preview field
    private BitmapSource? _previewImage;

    public string ItemKey
    {
        get => _itemKey;
        set
        {
            var upperValue = value?.ToUpperInvariant() ?? string.Empty;
            if (_itemKey != upperValue)
            {
                _itemKey = upperValue;
                OnPropertyChanged();
                LoadItemCommand.RaiseCanExecuteChanged();

                // Trigger debounced search for autosuggest
                if (!_suppressSearch)
                {
                    DebouncedSearch(upperValue);
                }
            }
        }
    }

    // AutoSuggest properties
    public ObservableCollection<PartInfo> SearchResults
    {
        get => _searchResults;
        set { _searchResults = value; OnPropertyChanged(); }
    }

    public bool IsDropdownOpen
    {
        get => _isDropdownOpen;
        set { _isDropdownOpen = value; OnPropertyChanged(); }
    }

    public PartInfo? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set { _selectedSearchResult = value; OnPropertyChanged(); }
    }

    // Print feedback properties
    public bool IsPrinting
    {
        get => _isPrinting;
        set { _isPrinting = value; OnPropertyChanged(); }
    }

    public string LastPrintedInfo
    {
        get => _lastPrintedInfo;
        set { _lastPrintedInfo = value; OnPropertyChanged(); }
    }

    // Preview property
    public BitmapSource? PreviewImage
    {
        get => _previewImage;
        set { _previewImage = value; OnPropertyChanged(); }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value)
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsArabicMode));
                OnPropertyChanged(nameof(CanEditArabicDescription));
                _ = LoadItemAsync();
            }
        }
    }

    public LabelData? CurrentLabel
    {
        get => _currentLabel;
        set
        {
            // Unsubscribe from old label
            if (_currentLabel != null)
                _currentLabel.PropertyChanged -= OnLabelDataChanged;

            _currentLabel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEditArabicDescription));

            // Subscribe to new label
            if (_currentLabel != null)
                _currentLabel.PropertyChanged += OnLabelDataChanged;

            UpdatePreview();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
            LoadItemCommand.RaiseCanExecuteChanged();
            PrintCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set { _selectedPrinter = value; OnPropertyChanged(); }
    }

    public bool IsArabicMode => SelectedLanguage == "ar";
    public bool CanEditArabicDescription => IsArabicMode && CurrentLabel != null;

    public string? CurrentLocalization
    {
        get => _currentLocalization;
        set { _currentLocalization = value; OnPropertyChanged(); }
    }

    public AsyncRelayCommand LoadItemCommand { get; }
    public AsyncRelayCommand PrintCommand { get; }
    public AsyncRelayCommand EditArabicCommand { get; }
    public RelayCommand SelectSuggestionCommand { get; }
    public RelayCommand CloseDropdownCommand { get; }

    public PrintOnDemandViewModel(
        IPartDataService partDataService,
        IArabicDescriptionService arabicDescService,
        IPrinterService printerService,
        ILabelRenderService labelRenderService,
        ILabelPreviewService? previewService = null)
    {
        _partDataService = partDataService ?? throw new ArgumentNullException(nameof(partDataService));
        _arabicDescService = arabicDescService ?? throw new ArgumentNullException(nameof(arabicDescService));
        _printerService = printerService ?? throw new ArgumentNullException(nameof(printerService));
        _labelRenderService = labelRenderService ?? throw new ArgumentNullException(nameof(labelRenderService));
        _previewService = previewService;

        LoadItemCommand = new AsyncRelayCommand(_ => LoadItemAsync(), _ => CanLoadItem());
        PrintCommand = new AsyncRelayCommand(_ => PrintAsync(), _ => CanPrint());
        EditArabicCommand = new AsyncRelayCommand(_ => EditArabicDescriptionAsync(), _ => CanEditArabicDescription);
        SelectSuggestionCommand = new RelayCommand(SelectSuggestion);
        CloseDropdownCommand = new RelayCommand(_ => CloseDropdown());

        // Initialize debounce timer
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += async (s, e) =>
        {
            _debounceTimer.Stop();
            await PerformSearchAsync(_itemKey);
        };
    }

    private bool CanLoadItem() => !string.IsNullOrWhiteSpace(ItemKey) && !IsLoading;
    private bool CanPrint() =>
        CurrentLabel != null &&
        !IsLoading &&
        !string.IsNullOrEmpty(SelectedPrinter) &&
        (DateTime.Now - _lastPrintTime).TotalMilliseconds > PrintCooldownMs;

    private async Task LoadItemAsync()
    {
        if (string.IsNullOrWhiteSpace(ItemKey))
            return;

        IsLoading = true;
        StatusMessage = "Loading part...";

        try
        {
            var partInfo = await _partDataService.GetPartByItemKeyAsync(ItemKey);

            if (partInfo == null)
            {
                MessageBox.Show($"Part not found: {ItemKey}", "Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusMessage = "Part not found";
                CurrentLocalization = null;
                return;
            }

            // Set localization for display
            CurrentLocalization = partInfo.Localization;

            // Create label data
            CurrentLabel = _labelRenderService.CreateLabelData(ItemKey, partInfo, SelectedLanguage);

            // Optimize font size
            var settings = new StickerSettings(); // TODO: Load from config
            _labelRenderService.OptimizeFontSize(CurrentLabel, settings);

            StatusMessage = "Ready to print";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading part: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Error occurred";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PrintAsync()
    {
        if (CurrentLabel == null || string.IsNullOrEmpty(SelectedPrinter))
            return;

        IsPrinting = true;
        IsLoading = true;
        StatusMessage = "Printing...";
        LastPrintedInfo = string.Empty;

        try
        {
            var settings = new StickerSettings { PrinterName = SelectedPrinter };

            await _printerService.PrintLabelAsync(
                CurrentLabel,
                settings,
                SelectedPrinter,
                CurrentLabel.Quantity);

            LastPrintedInfo = $"Printed {CurrentLabel.ItemKey} x {CurrentLabel.Quantity}";
            StatusMessage = "Print complete";

            // Auto-clear success message after 5 seconds
            var itemKeyPrinted = CurrentLabel.ItemKey;
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (LastPrintedInfo.Contains(itemKeyPrinted))
                        LastPrintedInfo = string.Empty;
                });
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Print failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Print failed";
        }
        finally
        {
            IsPrinting = false;
            IsLoading = false;
            _lastPrintTime = DateTime.Now;
            PrintCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task EditArabicDescriptionAsync()
    {
        if (CurrentLabel == null || !IsArabicMode)
            return;

        var dialog = new Views.EditArabicDialog
        {
            ArabicDescription = CurrentLabel.Description,
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            IsLoading = true;
            StatusMessage = "Saving Arabic description...";

            try
            {
                // Save to database
                await _arabicDescService.SaveArabicDescriptionAsync(
                    CurrentLabel.ItemKey,
                    dialog.ArabicDescription,
                    Environment.UserName);

                // Update current label
                CurrentLabel.Description = dialog.ArabicDescription;

                // Refresh preview
                UpdatePreview();

                StatusMessage = "Arabic description saved";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Save failed";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    #region AutoSuggest Methods

    private void DebouncedSearch(string searchTerm)
    {
        _debounceTimer?.Stop();

        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
        {
            SearchResults.Clear();
            IsDropdownOpen = false;
            return;
        }

        _debounceTimer?.Start();
    }

    private async Task PerformSearchAsync(string searchTerm)
    {
        // Cancel previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        try
        {
            var results = await _partDataService.SearchPartsAsync(searchTerm);

            Application.Current.Dispatcher.Invoke(() =>
            {
                SearchResults.Clear();
                foreach (var part in results.Take(10)) // Limit to 10 results
                {
                    SearchResults.Add(part);
                }
                IsDropdownOpen = SearchResults.Count > 0;
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search failed: {ex.Message}");
        }
    }

    private void SelectSuggestion(object? param)
    {
        if (param is PartInfo selectedPart)
        {
            _suppressSearch = true;
            _itemKey = selectedPart.ItemKey;
            OnPropertyChanged(nameof(ItemKey));
            _suppressSearch = false;

            IsDropdownOpen = false;
            SearchResults.Clear();

            // Auto-load the selected item
            _ = LoadItemAsync();
        }
    }

    private void CloseDropdown()
    {
        IsDropdownOpen = false;
    }

    #endregion

    #region Preview Methods

    private void OnLabelDataChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Update preview when any label property changes
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (CurrentLabel == null || _previewService == null)
        {
            PreviewImage = null;
            return;
        }

        try
        {
            var settings = new StickerSettings();
            PreviewImage = _previewService.GeneratePreview(CurrentLabel, settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Preview generation failed: {ex.Message}");
            PreviewImage = null;
        }
    }

    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
