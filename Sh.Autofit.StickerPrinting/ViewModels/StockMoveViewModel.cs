using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sh.Autofit.StickerPrinting.Services.Database;
using Sh.Autofit.StickerPrinting.Services.Label;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.ViewModels;

public class StockMoveViewModel : INotifyPropertyChanged
{
    private readonly IStockDataService _stockDataService;
    private readonly IPartDataService _partDataService;
    private readonly IPrinterService _printerService;
    private readonly ILabelRenderService _labelRenderService;

    private string _selectedPrinter = string.Empty;

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set { _selectedPrinter = value; OnPropertyChanged(); }
    }

    public StockMoveViewModel(
        IStockDataService stockDataService,
        IPartDataService partDataService,
        IPrinterService printerService,
        ILabelRenderService labelRenderService)
    {
        _stockDataService = stockDataService ?? throw new ArgumentNullException(nameof(stockDataService));
        _partDataService = partDataService ?? throw new ArgumentNullException(nameof(partDataService));
        _printerService = printerService ?? throw new ArgumentNullException(nameof(printerService));
        _labelRenderService = labelRenderService ?? throw new ArgumentNullException(nameof(labelRenderService));

        // TODO: Implement stock move functionality
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
