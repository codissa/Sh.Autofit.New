using Sh.Autofit.New.PartsMappingUI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class SelectVehiclesDialog : Window
{
    private List<VehicleDisplayModel> _allVehicles;
    private List<VehicleDisplayModel> _filteredVehicles;

    public List<VehicleDisplayModel> SelectedVehicles { get; private set; }

    public SelectVehiclesDialog(List<VehicleDisplayModel> vehicles)
    {
        InitializeComponent();

        _allVehicles = vehicles;
        _filteredVehicles = new List<VehicleDisplayModel>(_allVehicles);

        VehiclesGrid.ItemsSource = _filteredVehicles;
        SelectedVehicles = new List<VehicleDisplayModel>();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredVehicles = new List<VehicleDisplayModel>(_allVehicles);
        }
        else
        {
            _filteredVehicles = _allVehicles.Where(v =>
                v.ManufacturerName?.ToLower().Contains(searchText) == true ||
                v.ManufacturerShortName?.ToLower().Contains(searchText) == true ||
                v.ModelName?.ToLower().Contains(searchText) == true ||
                v.CommercialName?.ToLower().Contains(searchText) == true ||
                v.FuelTypeName?.ToLower().Contains(searchText) == true
            ).ToList();
        }

        VehiclesGrid.ItemsSource = _filteredVehicles;
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedVehicles = _allVehicles.Where(v => v.IsSelected).ToList();

        if (!SelectedVehicles.Any())
        {
            MessageBox.Show("אנא בחר לפחות רכב אחד", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
