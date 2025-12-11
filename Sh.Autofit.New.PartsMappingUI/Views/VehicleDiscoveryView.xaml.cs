using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class VehicleDiscoveryView : UserControl
{
    public VehicleDiscoveryView(VehicleDiscoveryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
