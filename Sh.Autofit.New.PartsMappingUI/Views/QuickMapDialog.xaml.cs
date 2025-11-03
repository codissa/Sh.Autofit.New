using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class QuickMapDialog : Window, INotifyPropertyChanged
{
    private readonly IDataService _dataService;
    private readonly int _vehicleTypeId;
    private List<SelectablePartModel> _allParts = new();
    private ObservableCollection<SelectablePartModel> _filteredParts = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public QuickMapDialog(IDataService dataService, int vehicleTypeId)
    {
        InitializeComponent();
        DataContext = this;

        _dataService = dataService;
        _vehicleTypeId = vehicleTypeId;

        PartsDataGrid.ItemsSource = _filteredParts;

        Loaded += QuickMapDialog_Loaded;
    }

    private async void QuickMapDialog_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadPartsAsync();
    }

    private async Task LoadPartsAsync()
    {
        try
        {
            IsLoading = true;

            // Load all unmapped parts for this vehicle
            var parts = await _dataService.LoadUnmappedPartsAsync(_vehicleTypeId);

            _allParts = parts.Select(p => new SelectablePartModel(p)).ToList();
            _filteredParts.Clear();

            foreach (var part in _allParts)
            {
                _filteredParts.Add(part);
                part.PropertyChanged += Part_PropertyChanged;
            }

            UpdateSelectionInfo();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בטעינת חלקים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Part_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectablePartModel.IsSelected))
        {
            UpdateSelectionInfo();
        }
    }

    private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var searchText = SearchTextBox.Text?.ToLower() ?? string.Empty;

        _filteredParts.Clear();

        var filtered = string.IsNullOrWhiteSpace(searchText)
            ? _allParts
            : _allParts.Where(p =>
                p.PartNumber?.ToLower().Contains(searchText) == true ||
                p.PartName?.ToLower().Contains(searchText) == true ||
                p.Category?.ToLower().Contains(searchText) == true ||
                p.Manufacturer?.ToLower().Contains(searchText) == true);

        foreach (var part in filtered)
        {
            _filteredParts.Add(part);
        }
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var part in _filteredParts)
        {
            part.IsSelected = true;
        }
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var part in _filteredParts)
        {
            part.IsSelected = false;
        }
    }

    private void UpdateSelectionInfo()
    {
        var selectedCount = _allParts.Count(p => p.IsSelected);
        SelectionInfoText.Text = selectedCount == 0
            ? "לא נבחרו חלקים"
            : $"נבחרו {selectedCount} חלקים";

        MapButton.IsEnabled = selectedCount > 0;
    }

    private async void MapButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedParts = _allParts.Where(p => p.IsSelected).ToList();

        if (!selectedParts.Any())
        {
            MessageBox.Show("נא לבחור לפחות חלק אחד", "אזהרה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsLoading = true;

            var partNumbers = selectedParts.Select(p => p.PartNumber).ToList();
            var vehicleTypeIds = new List<int> { _vehicleTypeId };

            await _dataService.MapPartsToVehiclesAsync(vehicleTypeIds, partNumbers, "QuickMap");

            MessageBox.Show($"מופו {selectedParts.Count} חלקים בהצלחה", "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה במיפוי חלקים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class SelectablePartModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public string PartNumber { get; set; } = string.Empty;
    public string? PartName { get; set; }
    public string? Category { get; set; }
    public string? Manufacturer { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public SelectablePartModel(PartDisplayModel part)
    {
        PartNumber = part.PartNumber;
        PartName = part.PartName;
        Category = part.Category;
        Manufacturer = part.Manufacturer;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
