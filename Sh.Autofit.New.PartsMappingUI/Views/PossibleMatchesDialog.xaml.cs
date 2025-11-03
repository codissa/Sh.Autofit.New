using Sh.Autofit.New.PartsMappingUI.Models;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class PossibleMatchesDialog : Window
{
    public VehicleDisplayModel? SelectedVehicle { get; private set; }

    public PossibleMatchesDialog(List<VehicleDisplayModel> possibleMatches)
    {
        InitializeComponent();

        MatchesDataGrid.ItemsSource = possibleMatches;
        MatchesDataGrid.SelectionChanged += MatchesDataGrid_SelectionChanged;
    }

    private void MatchesDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SelectButton.IsEnabled = MatchesDataGrid.SelectedItem != null;
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedVehicle = MatchesDataGrid.SelectedItem as VehicleDisplayModel;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void MatchesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (MatchesDataGrid.SelectedItem != null)
        {
            SelectedVehicle = MatchesDataGrid.SelectedItem as VehicleDisplayModel;
            DialogResult = true;
            Close();
        }
    }
}
