using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class CouplingManagementView : UserControl
{
    public CouplingManagementView()
    {
        InitializeComponent();
    }

    public async void SetViewModel(CouplingManagementViewModel viewModel)
    {
        DataContext = viewModel;
        await viewModel.InitializeAsync();
    }
}
