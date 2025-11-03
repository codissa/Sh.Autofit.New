using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Wire up PartKitsView with its ViewModel
        PartKitsView.SetViewModel(viewModel.PartKitsViewModel);
    }
}