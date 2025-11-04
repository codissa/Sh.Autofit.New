using Sh.Autofit.New.PartsMappingUI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class SelectPartsDialog : Window
{
    private List<PartDisplayModel> _allParts;
    private List<PartDisplayModel> _filteredParts;

    public List<PartDisplayModel> SelectedParts { get; private set; }

    public SelectPartsDialog(List<PartDisplayModel> parts)
    {
        InitializeComponent();

        _allParts = parts;
        _filteredParts = new List<PartDisplayModel>(_allParts);

        PartsGrid.ItemsSource = _filteredParts;
        SelectedParts = new List<PartDisplayModel>();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredParts = new List<PartDisplayModel>(_allParts);
        }
        else
        {
            _filteredParts = _allParts.Where(p =>
                p.PartNumber?.ToLower().Contains(searchText) == true ||
                p.PartName?.ToLower().Contains(searchText) == true ||
                p.Category?.ToLower().Contains(searchText) == true ||
                p.Manufacturer?.ToLower().Contains(searchText) == true
            ).ToList();
        }

        PartsGrid.ItemsSource = _filteredParts;
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedParts = _allParts.Where(p => p.IsSelected).ToList();

        if (!SelectedParts.Any())
        {
            MessageBox.Show("אנא בחר לפחות חלק אחד", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
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
