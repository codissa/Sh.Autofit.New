using System.Windows;
using Sh.Autofit.StockExport.Models;
using Sh.Autofit.StockExport.Services.Database;
using Sh.Autofit.StockExport.ViewModels;

namespace Sh.Autofit.StockExport.Views;

/// <summary>
/// Interaction logic for ValidationResultsDialog.xaml
/// </summary>
public partial class ValidationResultsDialog : Window
{
    public ValidationResultsDialogViewModel ViewModel { get; }

    public ValidationResultsDialog(List<ImportedStockItem> items, PartLookupService? partLookupService = null)
    {
        InitializeComponent();
        ViewModel = new ValidationResultsDialogViewModel(items, partLookupService);
        DataContext = ViewModel;
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;   // <-- THIS is what ShowDialog() checks
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
