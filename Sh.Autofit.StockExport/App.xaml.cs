using System.Windows;
using Sh.Autofit.StockExport.Services.Database;
using Sh.Autofit.StockExport.Services.Excel;
using Sh.Autofit.StockExport.ViewModels;
using Sh.Autofit.StockExport.Views;

namespace Sh.Autofit.StockExport;

/// <summary>
/// Application entry point with dependency injection setup
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Connection string to SH2013 database (READ-ONLY access)
    /// Using the same connection string as PartsMappingUI
    /// Database: SH2013 (accessed via Sh.Autofit catalog)
    /// </summary>
    private const string ConnectionString =
        "Data Source=server-pc\\wizsoft2;Initial Catalog=Sh.Autofit;Persist Security Info=True;User ID=issa;Password=5060977Ih;Encrypt=False;TrustServerCertificate=True";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize services
        var stockMovesService = new StockMovesService(ConnectionString);
        var excelExportService = new ExcelExportService();

        // Initialize ViewModel
        var viewModel = new MainViewModel(stockMovesService, excelExportService);

        // Initialize and show MainWindow
        var mainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        mainWindow.Show();
    }
}
