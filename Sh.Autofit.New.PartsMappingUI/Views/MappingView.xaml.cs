using Sh.Autofit.New.PartsMappingUI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class MappingView : UserControl
{
    public MappingView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MappingViewModel viewModel)
        {
            await viewModel.LoadDataCommand.ExecuteAsync(null);
        }
    }

    private void OemNumber_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is string oemNumber)
        {
            try
            {
                Clipboard.SetText(oemNumber);

                // Visual feedback - briefly change the tooltip
                var originalTooltip = border.ToolTip;
                border.ToolTip = "Copied!";

                // Reset tooltip after 1 second
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                timer.Tick += (s, args) =>
                {
                    border.ToolTip = originalTooltip;
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
