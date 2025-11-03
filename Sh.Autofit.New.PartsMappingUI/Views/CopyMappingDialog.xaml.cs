using Sh.Autofit.New.PartsMappingUI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class CopyMappingDialog : Window
{
    private List<VehicleDisplayModel> _allVehicles;
    private List<VehicleDisplayModel> _filteredSourceVehicles;
    private List<VehicleDisplayModel> _filteredTargetVehicles;

    public VehicleDisplayModel? SourceVehicle { get; private set; }
    public List<VehicleDisplayModel> TargetVehicles { get; private set; }

    public CopyMappingDialog(List<VehicleDisplayModel> vehicles)
    {
        InitializeComponent();

        _allVehicles = vehicles.Where(v => v.VehicleTypeId > 0).ToList();
        _filteredSourceVehicles = new List<VehicleDisplayModel>(_allVehicles);
        _filteredTargetVehicles = new List<VehicleDisplayModel>(_allVehicles);

        SourceVehicleList.ItemsSource = _filteredSourceVehicles;
        TargetVehicleList.ItemsSource = _filteredTargetVehicles;

        TargetVehicles = new List<VehicleDisplayModel>();
    }

    private void SourceSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SourceSearchBox.Text?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredSourceVehicles = new List<VehicleDisplayModel>(_allVehicles);
        }
        else
        {
            _filteredSourceVehicles = _allVehicles.Where(v =>
                v.ManufacturerName?.ToLower().Contains(searchText) == true ||
                v.ManufacturerShortName?.ToLower().Contains(searchText) == true ||
                v.ModelName?.ToLower().Contains(searchText) == true ||
                v.CommercialName?.ToLower().Contains(searchText) == true ||
                v.VehicleCategory?.ToLower().Contains(searchText) == true ||
                v.FuelTypeName?.ToLower().Contains(searchText) == true ||
                v.EngineModel?.ToLower().Contains(searchText) == true
            ).ToList();
        }

        SourceVehicleList.ItemsSource = _filteredSourceVehicles;
    }

    private void TargetSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = TargetSearchBox.Text?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredTargetVehicles = new List<VehicleDisplayModel>(_allVehicles);
        }
        else
        {
            _filteredTargetVehicles = _allVehicles.Where(v =>
                v.ManufacturerName?.ToLower().Contains(searchText) == true ||
                v.ManufacturerShortName?.ToLower().Contains(searchText) == true ||
                v.ModelName?.ToLower().Contains(searchText) == true ||
                v.CommercialName?.ToLower().Contains(searchText) == true ||
                v.VehicleCategory?.ToLower().Contains(searchText) == true ||
                v.FuelTypeName?.ToLower().Contains(searchText) == true ||
                v.EngineModel?.ToLower().Contains(searchText) == true
            ).ToList();
        }

        TargetVehicleList.ItemsSource = _filteredTargetVehicles;
    }

    private void SourceVehicleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SourceVehicle = SourceVehicleList.SelectedItem as VehicleDisplayModel;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceVehicle == null)
        {
            MessageBox.Show("אנא בחר רכב מקור.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TargetVehicles = TargetVehicleList.SelectedItems.Cast<VehicleDisplayModel>().ToList();

        if (!TargetVehicles.Any())
        {
            MessageBox.Show("אנא בחר לפחות רכב יעד אחד.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if source is in target list
        if (TargetVehicles.Any(v => v.VehicleTypeId == SourceVehicle.VehicleTypeId))
        {
            MessageBox.Show("לא ניתן להעתיק רכב לעצמו. אנא הסר את רכב המקור מרשימת היעד.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
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
