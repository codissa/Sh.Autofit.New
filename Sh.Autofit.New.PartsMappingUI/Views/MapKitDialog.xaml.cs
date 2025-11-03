using Sh.Autofit.New.PartsMappingUI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class MapKitDialog : Window
{
    private List<PartKitDisplayModel> _allKits;
    private List<PartKitDisplayModel> _filteredKits;

    public PartKitDisplayModel? SelectedKit { get; private set; }
    public int VehicleCount { get; }

    public MapKitDialog(List<PartKitDisplayModel> kits, int vehicleCount)
    {
        InitializeComponent();

        VehicleCount = vehicleCount;
        DataContext = this;

        _allKits = kits.Where(k => k.IsActive).ToList();
        _filteredKits = new List<PartKitDisplayModel>(_allKits);

        KitsList.ItemsSource = _filteredKits;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredKits = new List<PartKitDisplayModel>(_allKits);
        }
        else
        {
            _filteredKits = _allKits.Where(k =>
                k.KitName?.ToLower().Contains(searchText) == true ||
                k.Description?.ToLower().Contains(searchText) == true
            ).ToList();
        }

        KitsList.ItemsSource = _filteredKits;
    }

    private void KitsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedKit = KitsList.SelectedItem as PartKitDisplayModel;
    }

    private void MapButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedKit == null)
        {
            MessageBox.Show("אנא בחר ערכה למיפוי", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
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
