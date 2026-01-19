using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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

    private string _itemKey = string.Empty;
    private string _selectedLanguage = "he";
    private LabelData? _currentLabel;
    private bool _isLoading = false;
    private string _statusMessage = string.Empty;
    private string _selectedPrinter = string.Empty;

    public string ItemKey
    {
        get => _itemKey;
        set
        {
            if (_itemKey != value)
            {
                _itemKey = value;
                OnPropertyChanged();
                LoadItemCommand.RaiseCanExecuteChanged();
            }
        }
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
        set { _currentLabel = value; OnPropertyChanged(); }
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

    public AsyncRelayCommand LoadItemCommand { get; }
    public AsyncRelayCommand PrintCommand { get; }
    public AsyncRelayCommand EditArabicCommand { get; }

    public PrintOnDemandViewModel(
        IPartDataService partDataService,
        IArabicDescriptionService arabicDescService,
        IPrinterService printerService,
        ILabelRenderService labelRenderService)
    {
        _partDataService = partDataService ?? throw new ArgumentNullException(nameof(partDataService));
        _arabicDescService = arabicDescService ?? throw new ArgumentNullException(nameof(arabicDescService));
        _printerService = printerService ?? throw new ArgumentNullException(nameof(printerService));
        _labelRenderService = labelRenderService ?? throw new ArgumentNullException(nameof(labelRenderService));

        LoadItemCommand = new AsyncRelayCommand(_ => LoadItemAsync(), _ => CanLoadItem());
        PrintCommand = new AsyncRelayCommand(_ => PrintAsync(), _ => CanPrint());
        EditArabicCommand = new AsyncRelayCommand(_ => EditArabicDescriptionAsync(), _ => CanEditArabicDescription);
    }

    private bool CanLoadItem() => !string.IsNullOrWhiteSpace(ItemKey) && !IsLoading;
    private bool CanPrint() => CurrentLabel != null && !IsLoading && !string.IsNullOrEmpty(SelectedPrinter);

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
                return;
            }

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

        IsLoading = true;
        StatusMessage = "Printing...";

        try
        {
            var settings = new StickerSettings { PrinterName = SelectedPrinter };

            await _printerService.PrintLabelAsync(
                CurrentLabel,
                settings,
                SelectedPrinter,
                CurrentLabel.Quantity);

            MessageBox.Show($"Printed {CurrentLabel.Quantity} label(s) successfully", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
            StatusMessage = "Print complete";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Print failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Print failed";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task EditArabicDescriptionAsync()
    {
        if (CurrentLabel == null || !IsArabicMode)
            return;

        // TODO: Show dialog to edit Arabic description
        MessageBox.Show("Edit Arabic Description dialog not yet implemented", "TODO",
            MessageBoxButton.OK, MessageBoxImage.Information);
        await Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
