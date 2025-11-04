using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class PartMappingsManagementView : UserControl
{
    public PartMappingsManagementView()
    {
        InitializeComponent();
    }

    public void SetViewModel(PartMappingsManagementViewModel viewModel)
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }
}
