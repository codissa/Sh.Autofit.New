using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class ManufacturerExclusionDialog : Window
{
    private readonly ObservableCollection<ManufacturerExclusionItem> _allManufacturers;
    private readonly ObservableCollection<ManufacturerExclusionItem> _filteredManufacturers;

    public List<string> ExcludedManufacturers { get; private set; }

    public ManufacturerExclusionDialog(
        List<(string ShortName, string FullName, int VehicleCount)> manufacturers,
        List<string> currentlyExcluded)
    {
        InitializeComponent();

        _allManufacturers = new ObservableCollection<ManufacturerExclusionItem>(
            manufacturers
                .OrderBy(m => m.ShortName)
                .Select(m => new ManufacturerExclusionItem
                {
                    ShortName = m.ShortName,
                    ManufacturerName = $"{m.ShortName} ({m.FullName})",
                    VehicleCount = m.VehicleCount,
                    IsExcluded = currentlyExcluded.Contains(m.ShortName)
                })
        );

        _filteredManufacturers = new ObservableCollection<ManufacturerExclusionItem>(_allManufacturers);
        ManufacturersItemsControl.ItemsSource = _filteredManufacturers;

        ExcludedManufacturers = new List<string>(currentlyExcluded);
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchTextBox.Text.ToLower();
        _filteredManufacturers.Clear();

        var filtered = string.IsNullOrWhiteSpace(searchText)
            ? _allManufacturers
            : _allManufacturers.Where(m => m.ManufacturerName.ToLower().Contains(searchText));

        foreach (var item in filtered)
        {
            _filteredManufacturers.Add(item);
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _filteredManufacturers)
        {
            item.IsExcluded = true;
        }
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _filteredManufacturers)
        {
            item.IsExcluded = false;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ExcludedManufacturers = _allManufacturers
            .Where(m => m.IsExcluded)
            .Select(m => m.ShortName)
            .ToList();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class ManufacturerExclusionItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    private bool _isExcluded;

    public string ShortName { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;
    public int VehicleCount { get; set; }

    public bool IsExcluded
    {
        get => _isExcluded;
        set => SetProperty(ref _isExcluded, value);
    }
}
