using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class PartKitsView : UserControl
{
    public PartKitsView()
    {
        InitializeComponent();
    }

    public async void SetViewModel(PartKitsViewModel viewModel)
    {
        DataContext = viewModel;
        await viewModel.InitializeAsync();
    }
}
