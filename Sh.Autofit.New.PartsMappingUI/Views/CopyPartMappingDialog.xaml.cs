using Sh.Autofit.New.PartsMappingUI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class CopyPartMappingDialog : Window
{
    private List<PartDisplayModel> _allParts;
    private List<PartDisplayModel> _filteredSourceParts;
    private List<PartDisplayModel> _filteredTargetParts;

    public PartDisplayModel? SourcePart { get; private set; }
    public List<PartDisplayModel> TargetParts { get; private set; }

    public CopyPartMappingDialog(List<PartDisplayModel> parts)
    {
        InitializeComponent();

        _allParts = parts;
        _filteredSourceParts = new List<PartDisplayModel>(_allParts);
        _filteredTargetParts = new List<PartDisplayModel>(_allParts);

        SourcePartList.ItemsSource = _filteredSourceParts;
        TargetPartList.ItemsSource = _filteredTargetParts;

        TargetParts = new List<PartDisplayModel>();
    }

    private void SourceSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SourceSearchBox.Text?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredSourceParts = new List<PartDisplayModel>(_allParts);
        }
        else
        {
            _filteredSourceParts = _allParts.Where(p =>
                p.PartNumber?.ToLower().Contains(searchText) == true ||
                p.PartName?.ToLower().Contains(searchText) == true ||
                p.Category?.ToLower().Contains(searchText) == true ||
                p.Manufacturer?.ToLower().Contains(searchText) == true ||
                p.Model?.ToLower().Contains(searchText) == true
            ).ToList();
        }

        SourcePartList.ItemsSource = _filteredSourceParts;
    }

    private void TargetSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = TargetSearchBox.Text?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredTargetParts = new List<PartDisplayModel>(_allParts);
        }
        else
        {
            _filteredTargetParts = _allParts.Where(p =>
                p.PartNumber?.ToLower().Contains(searchText) == true ||
                p.PartName?.ToLower().Contains(searchText) == true ||
                p.Category?.ToLower().Contains(searchText) == true ||
                p.Manufacturer?.ToLower().Contains(searchText) == true ||
                p.Model?.ToLower().Contains(searchText) == true
            ).ToList();
        }

        TargetPartList.ItemsSource = _filteredTargetParts;
    }

    private void SourcePartList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SourcePart = SourcePartList.SelectedItem as PartDisplayModel;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourcePart == null)
        {
            MessageBox.Show("אנא בחר חלק מקור.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        TargetParts = TargetPartList.SelectedItems.Cast<PartDisplayModel>().ToList();

        if (!TargetParts.Any())
        {
            MessageBox.Show("אנא בחר לפחות חלק יעד אחד.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if source is in target list
        if (TargetParts.Any(p => p.PartNumber == SourcePart.PartNumber))
        {
            MessageBox.Show("לא ניתן להעתיק חלק לעצמו. אנא הסר את חלק המקור מרשימת היעד.", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
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
