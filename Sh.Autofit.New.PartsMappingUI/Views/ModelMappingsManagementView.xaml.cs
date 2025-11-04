using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class ModelMappingsManagementView : UserControl
{
    public ModelMappingsManagementView()
    {
        InitializeComponent();
    }

    public void SetViewModel(ModelMappingsManagementViewModel viewModel)
    {
        DataContext = viewModel;
        _ = viewModel.InitializeAsync();
    }
}
