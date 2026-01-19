using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Sh.Autofit.StickerPrinting.Commands;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IPrinterService _printerService;
    private string _selectedPrinter = string.Empty;
    private PrinterInfo? _printerStatus;
    private int _selectedTabIndex = 0;

    public PrintOnDemandViewModel PrintOnDemandVM { get; }
    public StockMoveViewModel StockMoveVM { get; }

    public ObservableCollection<string> AvailablePrinters { get; } = new();

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set
        {
            if (_selectedPrinter != value)
            {
                _selectedPrinter = value;
                OnPropertyChanged();
                _ = UpdatePrinterStatusAsync();

                // Update printer in child ViewModels
                PrintOnDemandVM.SelectedPrinter = value;
                StockMoveVM.SelectedPrinter = value;
            }
        }
    }

    public PrinterInfo? PrinterStatus
    {
        get => _printerStatus;
        set { _printerStatus = value; OnPropertyChanged(); }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    public AsyncRelayCommand RefreshPrintersCommand { get; }
    public AsyncRelayCommand CheckPrinterStatusCommand { get; }

    public MainViewModel(
        PrintOnDemandViewModel printOnDemandVM,
        StockMoveViewModel stockMoveVM,
        IPrinterService printerService)
    {
        PrintOnDemandVM = printOnDemandVM ?? throw new ArgumentNullException(nameof(printOnDemandVM));
        StockMoveVM = stockMoveVM ?? throw new ArgumentNullException(nameof(stockMoveVM));
        _printerService = printerService ?? throw new ArgumentNullException(nameof(printerService));

        RefreshPrintersCommand = new AsyncRelayCommand(_ => LoadPrintersAsync());
        CheckPrinterStatusCommand = new AsyncRelayCommand(_ => UpdatePrinterStatusAsync());

        // Load printers on startup
        _ = LoadPrintersAsync();
    }

    private async Task LoadPrintersAsync()
    {
        try
        {
            var printers = await _printerService.GetAvailablePrintersAsync();

            AvailablePrinters.Clear();
            foreach (var printer in printers)
            {
                AvailablePrinters.Add(printer.Name);
            }

            if (AvailablePrinters.Any() && string.IsNullOrEmpty(SelectedPrinter))
            {
                SelectedPrinter = AvailablePrinters.First();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load printers: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task UpdatePrinterStatusAsync()
    {
        if (string.IsNullOrEmpty(SelectedPrinter))
            return;

        try
        {
            PrinterStatus = await _printerService.GetPrinterStatusAsync(SelectedPrinter);
        }
        catch (Exception ex)
        {
            PrinterStatus = new PrinterInfo
            {
                Name = SelectedPrinter,
                Status = Models.PrinterStatus.Error,
                StatusMessage = ex.Message
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
