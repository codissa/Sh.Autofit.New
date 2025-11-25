using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views
{
    public partial class VehicleDataSyncView : UserControl
    {
        public VehicleDataSyncView()
        {
            InitializeComponent();
        }

        public void SetViewModel(VehicleDataSyncViewModel viewModel)
        {
            DataContext = viewModel;
        }
    }
}
