using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class AnalyticsView : UserControl
{
    public AnalyticsView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // Auto-load analytics when view is shown
        if (DataContext is AnalyticsDashboardViewModel viewModel)
        {
            await viewModel.LoadAnalyticsCommand.ExecuteAsync(null);
        }
    }
}
