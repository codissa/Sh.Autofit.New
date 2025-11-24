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

        // Wire up PartMappingsManagementView with its ViewModel
        PartMappingsManagementView.SetViewModel(viewModel.PartMappingsManagementViewModel);

        // Wire up ModelMappingsManagementView with its ViewModel
        ModelMappingsManagementView.SetViewModel(viewModel.ModelMappingsManagementViewModel);

        // Wire up CouplingManagementView with its ViewModel
        CouplingManagementView.SetViewModel(viewModel.CouplingManagementViewModel);
    }
}