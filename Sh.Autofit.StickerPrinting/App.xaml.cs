using System.Windows;
using Sh.Autofit.StickerPrinting.Services.Database;
using Sh.Autofit.StickerPrinting.Services.Label;
using Sh.Autofit.StickerPrinting.Services.Printing.Infrastructure;
using Sh.Autofit.StickerPrinting.Services.Printing.Zebra;
using Sh.Autofit.StickerPrinting.ViewModels;
using Sh.Autofit.StickerPrinting.Views;

namespace Sh.Autofit.StickerPrinting;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        const string connectionString =
            "Data Source=server-pc\\wizsoft2;Initial Catalog=Sh.Autofit;" +
            "User ID=issa;Password=5060977Ih;TrustServerCertificate=True;";

        // Database services
        var partDataService = new PartDataService(connectionString);
        var arabicDescService = new ArabicDescriptionService(connectionString);
        var stockDataService = new StockDataService(connectionString);

        // Printer infrastructure (Zebra)
        var rawCommunicator = new RawPrinterCommunicator();
        var zplGenerator = new ZplCommandGenerator();
        var zebraPrinterService = new ZebraPrinterService(zplGenerator, rawCommunicator);

        // Label rendering
        var labelRenderService = new LabelRenderService();

        // ViewModels
        var printOnDemandVM = new PrintOnDemandViewModel(
            partDataService,
            arabicDescService,
            zebraPrinterService,
            labelRenderService);

        var stockMoveVM = new StockMoveViewModel(
            stockDataService,
            partDataService,
            zebraPrinterService,
            labelRenderService);

        var mainViewModel = new MainViewModel(
            printOnDemandVM,
            stockMoveVM,
            zebraPrinterService);

        var mainWindow = new MainWindow { DataContext = mainViewModel };
        mainWindow.Show();
    }
}
