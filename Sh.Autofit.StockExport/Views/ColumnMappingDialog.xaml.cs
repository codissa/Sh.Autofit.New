using System.Windows;
using Sh.Autofit.StockExport.ViewModels;

namespace Sh.Autofit.StockExport.Views;

/// <summary>
/// Interaction logic for ColumnMappingDialog.xaml
/// </summary>
public partial class ColumnMappingDialog : Window
{
    public ColumnMappingDialogViewModel ViewModel { get; }

    public ColumnMappingDialog(string excelFilePath)
    {
        InitializeComponent();
        ViewModel = new ColumnMappingDialogViewModel(excelFilePath);
        DataContext = ViewModel;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        // Execute the command first to populate the Result
        if (ViewModel.ConfirmCommand.CanExecute(null))
        {
            ViewModel.ConfirmCommand.Execute(null);
        }

        // Now set DialogResult from ViewModel
        DialogResult = ViewModel.DialogResult;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
