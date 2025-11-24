using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using Sh.Autofit.New.PartsMappingUI.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using static Sh.Autofit.New.PartsMappingUI.Helpers.VehicleMatchingHelper;

namespace Sh.Autofit.New.PartsMappingUI.Views;

public partial class QuickMapDialog : Window, INotifyPropertyChanged
{
    private readonly IDataService _dataService;
    private readonly int _vehicleTypeId;
    private readonly VehicleDisplayModel _vehicle;
    private ConsolidatedVehicleModel? _consolidatedModel;
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

    public QuickMapDialog(IDataService dataService, int vehicleTypeId, VehicleDisplayModel vehicle)
    {
        InitializeComponent();
        DataContext = this;

        _dataService = dataService;
        _vehicleTypeId = vehicleTypeId;
        _vehicle = vehicle;

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

            // Try to get consolidated model first (NEW WAY)
            _consolidatedModel = await _dataService.GetConsolidatedModelForVehicleTypeAsync(_vehicleTypeId);

            // Load all unmapped parts for this model (not just this vehicle type)
            // This ensures we don't show parts already mapped to other variants of the same model
            var parts = await _dataService.LoadUnmappedPartsByModelAsync(
                _vehicle.ManufacturerName,
                _vehicle.ModelName);

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

            // NEW WAY: Use consolidated model if available
            if (_consolidatedModel != null)
            {
                await _dataService.MapPartsToConsolidatedModelAsync(
                    _consolidatedModel.ConsolidatedModelId,
                    partNumbers,
                    "QuickMap");

                var yearRange = _consolidatedModel.YearTo.HasValue
                    ? $"{_consolidatedModel.YearFrom}-{_consolidatedModel.YearTo}"
                    : $"{_consolidatedModel.YearFrom}+";

                MessageBox.Show($"מופו {selectedParts.Count} חלקים לדגם מאוחד ({yearRange})", "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
                return;
            }

            // FALLBACK: Legacy approach
            var variantDescription = GetVariantDescription(_vehicle);

            // Ask user: map to all model variants or just this variant?
            var result = MessageBox.Show(
                $"האם למפות {selectedParts.Count} חלקים לכל הווריאנטים של '{_vehicle.ModelName}'?\n\n" +
                $"הווריאנט הנוכחי: {variantDescription}\n\n" +
                "בחר 'כן' למיפוי לכל הווריאנטים (כל נפחי מנוע, תיבות הילוכים ורמות גימור),\n" +
                "'לא' למיפוי רק לווריאנט המדויק הזה,\n" +
                "או 'ביטול'.",
                "בחירת היקף מיפוי",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Cancel)
                return;

            List<int> vehicleTypeIds;

            if (result == MessageBoxResult.Yes)
            {
                // Map to ALL variants of this model
                var allVehicles = await _dataService.LoadVehiclesAsync();
                vehicleTypeIds = allVehicles
                    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(_vehicle.ManufacturerName) &&
                               v.ModelName.EqualsIgnoringWhitespace(_vehicle.ModelName))
                    .Select(v => v.VehicleTypeId)
                    .ToList();
            }
            else
            {
                // Map only to this specific variant
                var allVehicles = await _dataService.LoadVehiclesAsync();
                var sameVariantVehicles = GetSameVariantVehicles(allVehicles, _vehicle);
                vehicleTypeIds = sameVariantVehicles.Select(v => v.VehicleTypeId).ToList();
            }

            await _dataService.MapPartsToVehiclesAsync(vehicleTypeIds, partNumbers, "QuickMap");

            var scope = result == MessageBoxResult.Yes ? "כל הווריאנטים" : $"הווריאנט {variantDescription}";
            MessageBox.Show($"מופו {selectedParts.Count} חלקים ל-{scope} ({vehicleTypeIds.Count} רכבים)", "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);

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
