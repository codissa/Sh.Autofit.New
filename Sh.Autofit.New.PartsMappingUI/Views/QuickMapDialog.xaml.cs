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
    private readonly IVirtualPartService _virtualPartService;
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

    public QuickMapDialog(
        IDataService dataService,
        IVirtualPartService virtualPartService,
        int vehicleTypeId,
        VehicleDisplayModel vehicle)
    {
        InitializeComponent();
        DataContext = this;

        _dataService = dataService;
        _virtualPartService = virtualPartService;
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


            // Try to get consolidated model first (like mapped parts flow)
            _consolidatedModel = await _dataService
                .GetConsolidatedModelForVehicleTypeAsync(_vehicleTypeId);

            List<PartDisplayModel> parts;

            if (_consolidatedModel != null)
            {


                parts = await _dataService.LoadUnmappedPartsForConsolidatedModelAsync(
                    _consolidatedModel.ConsolidatedModelId,
                    includeCouplings: true);
            }
            else
            {
                // Legacy fallback by model name


                parts = await _dataService.LoadUnmappedPartsByModelAsync(
                    _vehicle.ManufacturerName,
                    _vehicle.ModelName);
            }

            // Build SelectablePartModel list
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

        if (string.IsNullOrWhiteSpace(searchText))
        {
            foreach (var part in _allParts)
            {
                _filteredParts.Add(part);
            }
            return;
        }

        // Split search text into individual words for non-continuous matching
        var searchWords = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var filtered = _allParts.Where(p =>
        {
            // For non-continuous search, check if ALL search words appear anywhere in the searchable fields
            return searchWords.All(word =>
                (p.PartNumber?.ToLower().Contains(word) == true) ||
                (p.PartName?.ToLower().Contains(word) == true) ||
                (p.Category?.ToLower().Contains(word) == true) ||
                (p.Manufacturer?.ToLower().Contains(word) == true) ||
                (p.Model?.ToLower().Contains(word) == true) ||
                // Search in OEM numbers with normalization (ignores ., -, /, spaces)
                Helpers.OemSearchHelper.OemContains(p.OemNumber1 ?? "", word) ||
                Helpers.OemSearchHelper.OemContains(p.OemNumber2 ?? "", word) ||
                Helpers.OemSearchHelper.OemContains(p.OemNumber3 ?? "", word) ||
                Helpers.OemSearchHelper.OemContains(p.OemNumber4 ?? "", word) ||
                Helpers.OemSearchHelper.OemContains(p.OemNumber5 ?? "", word)
            );
        });

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

    private async void CreateVirtualPartButton_Click(object sender, RoutedEventArgs e)
    {
        // Get available categories for dropdown
        var categories = _allParts
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var dialog = new CreateVirtualPartDialog(
            _virtualPartService,
            _consolidatedModel?.ConsolidatedModelId,
            _vehicleTypeId,
            categories);

        if (dialog.ShowDialog() == true && dialog.CreatedVirtualPart != null)
        {
            // Reload parts to include new virtual part
            await LoadPartsAsync();

            // Auto-select the new virtual part
            var newVirtualPart = _allParts.FirstOrDefault(p =>
                p.PartNumber == dialog.CreatedVirtualPart.PartNumber);

            if (newVirtualPart != null)
            {
                newVirtualPart.IsSelected = true;
                UpdateSelectionInfo();

                // Auto-map the virtual part to the vehicle/consolidated model
                try
                {
                    IsLoading = true;

                    if (_consolidatedModel != null)
                    {
                        // Map to consolidated model
                        await _dataService.MapPartsToConsolidatedModelAsync(
                            _consolidatedModel.ConsolidatedModelId,
                            new List<string> { newVirtualPart.PartNumber },
                            "QuickMap-VirtualPart");

                        MessageBox.Show(
                            $"החלק הוירטואלי '{newVirtualPart.PartName}' נוצר ומופה בהצלחה לדגם מאוחד!",
                            "✓ הצלחה",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        // Fallback: Map to individual vehicle type
                        await _dataService.MapPartsToVehiclesAsync(
                            new List<int> { _vehicleTypeId },
                            new List<string> { newVirtualPart.PartNumber },
                            "QuickMap-VirtualPart");

                        MessageBox.Show(
                            $"החלק הוירטואלי '{newVirtualPart.PartName}' נוצר ומופה בהצלחה לרכב!",
                            "✓ הצלחה",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }

                    // Close the dialog after successful mapping
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"החלק נוצר אך נכשל במיפוי: {ex.Message}",
                        "שגיאה חלקית",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }
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
    public string? Model { get; set; }

    // OEM Numbers for search
    public string? OemNumber1 { get; set; }
    public string? OemNumber2 { get; set; }
    public string? OemNumber3 { get; set; }
    public string? OemNumber4 { get; set; }
    public string? OemNumber5 { get; set; }

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
        Model = part.Model;
        OemNumber1 = part.OemNumber1;
        OemNumber2 = part.OemNumber2;
        OemNumber3 = part.OemNumber3;
        OemNumber4 = part.OemNumber4;
        OemNumber5 = part.OemNumber5;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
