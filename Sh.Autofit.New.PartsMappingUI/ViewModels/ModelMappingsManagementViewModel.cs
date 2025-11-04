using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class ModelMappingsManagementViewModel : ObservableObject
{
    private readonly IDataService _dataService;

    [ObservableProperty]
    private ObservableCollection<VehicleModelGroup> _modelGroups = new();

    [ObservableProperty]
    private ObservableCollection<VehicleModelGroup> _filteredModelGroups = new();

    [ObservableProperty]
    private VehicleModelGroup? _selectedModelGroup;

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _mappedParts = new();

    [ObservableProperty]
    private string _modelSearchText = string.Empty;

    [ObservableProperty]
    private string? _selectedManufacturer;

    [ObservableProperty]
    private ObservableCollection<string> _availableManufacturers = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private List<VehicleDisplayModel> _allVehicles = new();

    public ModelMappingsManagementViewModel(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        await LoadModelGroupsAsync();
    }

    [RelayCommand]
    private async Task LoadModelGroupsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען קבוצות דגמים...";

            // Load all vehicles
            _allVehicles = await _dataService.LoadVehiclesAsync();

            // Group vehicles by manufacturer and model name
            var groups = _allVehicles
                .GroupBy(v => new { v.ManufacturerName, v.ManufacturerShortName, v.ModelName })
                .Select(g => new VehicleModelGroup
                {
                    ManufacturerName = g.Key.ManufacturerName,
                    ManufacturerShortName = g.Key.ManufacturerShortName,
                    ModelName = g.Key.ModelName,
                    VehicleCount = g.Count(),
                    YearFrom = g.Min(v => v.YearFrom),
                    YearTo = g.Max(v => v.YearFrom),
                    MappedPartsCount = 0 // Will be calculated when needed
                })
                .OrderBy(g => g.ManufacturerShortName ?? g.ManufacturerName)
                .ThenBy(g => g.ModelName)
                .ToList();

            ModelGroups.Clear();
            foreach (var group in groups)
            {
                ModelGroups.Add(group);
            }

            // Extract unique manufacturers
            var manufacturers = groups
                .Select(g => g.ManufacturerShortName ?? g.ManufacturerName)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            AvailableManufacturers.Clear();
            AvailableManufacturers.Add("הכל"); // All
            foreach (var mfg in manufacturers)
            {
                AvailableManufacturers.Add(mfg);
            }

            StatusMessage = $"נטענו {ModelGroups.Count} קבוצות דגמים";
            ApplyFilters();
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה בטעינת דגמים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnModelSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedManufacturerChanged(string? value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = ModelGroups.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(ModelSearchText))
        {
            var searchLower = ModelSearchText.ToLower();
            filtered = filtered.Where(m =>
                m.ManufacturerName.ToLower().Contains(searchLower) ||
                (m.ManufacturerShortName?.ToLower().Contains(searchLower) ?? false) ||
                m.ModelName.ToLower().Contains(searchLower));
        }

        // Apply manufacturer filter
        if (!string.IsNullOrEmpty(SelectedManufacturer) && SelectedManufacturer != "הכל")
        {
            filtered = filtered.Where(m =>
                (m.ManufacturerShortName ?? m.ManufacturerName) == SelectedManufacturer);
        }

        FilteredModelGroups = new ObservableCollection<VehicleModelGroup>(filtered);
    }

    partial void OnSelectedModelGroupChanged(VehicleModelGroup? value)
    {
        if (value != null)
        {
            _ = LoadMappedPartsAsync(value);
        }
        else
        {
            MappedParts.Clear();
        }
    }

    private async Task LoadMappedPartsAsync(VehicleModelGroup modelGroup)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען חלקים ממופים...";

            // Get all vehicle IDs for this model
            var vehicleIds = _allVehicles
                .Where(v => v.ManufacturerName == modelGroup.ManufacturerName &&
                           v.ModelName == modelGroup.ModelName)
                .Select(v => v.VehicleTypeId)
                .ToList();

            // Load parts for each vehicle and find common parts
            var partsByVehicle = new Dictionary<int, HashSet<string>>();
            foreach (var vehicleId in vehicleIds)
            {
                var parts = await _dataService.LoadMappedPartsAsync(vehicleId);
                partsByVehicle[vehicleId] = parts.Select(p => p.PartNumber).ToHashSet();
            }

            // Find parts that are mapped to at least one vehicle in this model
            var allPartNumbers = partsByVehicle.Values.SelectMany(p => p).Distinct().ToList();

            // Load full part information
            var allParts = await _dataService.LoadPartsAsync();
            var mappedPartsList = allParts.Where(p => allPartNumbers.Contains(p.PartNumber)).ToList();

            // Calculate how many vehicles each part is mapped to
            foreach (var part in mappedPartsList)
            {
                part.MappedVehiclesCount = partsByVehicle.Values.Count(v => v.Contains(part.PartNumber));
            }

            MappedParts.Clear();
            foreach (var part in mappedPartsList.OrderBy(p => p.PartNumber))
            {
                MappedParts.Add(part);
            }

            StatusMessage = $"{MappedParts.Count} חלקים ממופים לדגם זה";
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddPartsToModelAsync()
    {
        if (SelectedModelGroup == null)
        {
            MessageBox.Show("אנא בחר דגם", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show dialog to select parts
        var allParts = await _dataService.LoadPartsAsync();
        var unmappedParts = allParts.Where(p => !MappedParts.Any(mp => mp.PartNumber == p.PartNumber)).ToList();

        var dialog = new Views.SelectPartsDialog(unmappedParts);
        if (dialog.ShowDialog() == true && dialog.SelectedParts.Any())
        {
            try
            {
                IsLoading = true;
                StatusMessage = "ממפה חלקים לכל הרכבים בדגם...";

                // Get all vehicle IDs for this model
                var vehicleIds = _allVehicles
                    .Where(v => v.ManufacturerName == SelectedModelGroup.ManufacturerName &&
                               v.ModelName == SelectedModelGroup.ModelName)
                    .Select(v => v.VehicleTypeId)
                    .ToList();

                var partNumbers = dialog.SelectedParts.Select(p => p.PartNumber).ToList();
                await _dataService.MapPartsToVehiclesAsync(vehicleIds, partNumbers, "current_user");

                StatusMessage = "המיפוי בוצע בהצלחה";
                await LoadMappedPartsAsync(SelectedModelGroup);
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה: {ex.Message}";
                MessageBox.Show($"שגיאה במיפוי: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    [RelayCommand]
    private async Task RemovePartFromModelAsync(PartDisplayModel? part)
    {
        if (part == null || SelectedModelGroup == null)
            return;

        var result = MessageBox.Show(
            $"האם להסיר את '{part.PartName}' מכל הרכבים בדגם '{SelectedModelGroup.ModelName}'?",
            "אישור הסרה",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "מסיר חלק מכל הרכבים בדגם...";

                // Get all vehicle IDs for this model
                var vehicleIds = _allVehicles
                    .Where(v => v.ManufacturerName == SelectedModelGroup.ManufacturerName &&
                               v.ModelName == SelectedModelGroup.ModelName)
                    .Select(v => v.VehicleTypeId)
                    .ToList();

                await _dataService.UnmapPartsFromVehiclesAsync(
                    vehicleIds,
                    new List<string> { part.PartNumber },
                    "current_user"
                );

                StatusMessage = "החלק הוסר בהצלחה";
                await LoadMappedPartsAsync(SelectedModelGroup);
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה: {ex.Message}";
                MessageBox.Show($"שגיאה בהסרת חלק: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
