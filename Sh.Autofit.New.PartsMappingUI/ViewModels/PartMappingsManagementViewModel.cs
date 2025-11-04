using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class PartMappingsManagementViewModel : ObservableObject
{
    private readonly IDataService _dataService;

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _allParts = new();

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _filteredParts = new();

    [ObservableProperty]
    private PartDisplayModel? _selectedPart;

    [ObservableProperty]
    private ObservableCollection<VehicleDisplayModel> _mappedVehicles = new();

    [ObservableProperty]
    private string _partSearchText = string.Empty;

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<string> _availableCategories = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public PartMappingsManagementViewModel(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task InitializeAsync()
    {
        await LoadPartsAsync();
    }

    [RelayCommand]
    private async Task LoadPartsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען חלקים...";

            var parts = await _dataService.LoadPartsAsync();
            AllParts.Clear();
            foreach (var part in parts)
            {
                AllParts.Add(part);
            }

            // Extract unique categories
            var categories = parts.Where(p => !string.IsNullOrEmpty(p.Category))
                                 .Select(p => p.Category!)
                                 .Distinct()
                                 .OrderBy(c => c)
                                 .ToList();

            AvailableCategories.Clear();
            AvailableCategories.Add("הכל"); // All
            foreach (var category in categories)
            {
                AvailableCategories.Add(category);
            }

            StatusMessage = $"נטענו {AllParts.Count} חלקים";
            ApplyFilters();
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה בטעינת חלקים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnPartSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = AllParts.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(PartSearchText))
        {
            var searchLower = PartSearchText.ToLower();
            filtered = filtered.Where(p =>
                p.PartNumber.ToLower().Contains(searchLower) ||
                p.PartName.ToLower().Contains(searchLower) ||
                (p.Category?.ToLower().Contains(searchLower) ?? false) ||
                (p.Manufacturer?.ToLower().Contains(searchLower) ?? false) ||
                (p.OemNumbers != null && p.OemNumbers.Any(oem => oem.ToLower().Contains(searchLower))));
        }

        // Apply category filter
        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "הכל")
        {
            filtered = filtered.Where(p => p.Category == SelectedCategory);
        }

        FilteredParts = new ObservableCollection<PartDisplayModel>(filtered);
    }

    partial void OnSelectedPartChanged(PartDisplayModel? value)
    {
        if (value != null)
        {
            _ = LoadMappedVehiclesAsync(value.PartNumber);
        }
        else
        {
            MappedVehicles.Clear();
        }
    }

    private async Task LoadMappedVehiclesAsync(string partNumber)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען רכבים ממופים...";

            // This needs a new method in DataService
            var vehicles = await _dataService.LoadVehiclesAsync();
            var mappedParts = await Task.WhenAll(
                vehicles.Select(async v =>
                {
                    var parts = await _dataService.LoadMappedPartsAsync(v.VehicleTypeId);
                    return new { Vehicle = v, HasPart = parts.Any(p => p.PartNumber == partNumber) };
                })
            );

            MappedVehicles.Clear();
            foreach (var item in mappedParts.Where(x => x.HasPart))
            {
                MappedVehicles.Add(item.Vehicle);
            }

            StatusMessage = $"{MappedVehicles.Count} רכבים ממופים";
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
    private async Task AddVehicleMappingsAsync()
    {
        if (SelectedPart == null)
        {
            MessageBox.Show("אנא בחר חלק", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show dialog to select vehicles
        var allVehicles = await _dataService.LoadVehiclesAsync();
        var unmappedVehicles = allVehicles.Where(v => !MappedVehicles.Any(mv => mv.VehicleTypeId == v.VehicleTypeId)).ToList();

        var dialog = new Views.SelectVehiclesDialog(unmappedVehicles);
        if (dialog.ShowDialog() == true && dialog.SelectedVehicles.Any())
        {
            try
            {
                IsLoading = true;
                StatusMessage = "ממפה חלק לרכבים...";

                var vehicleIds = dialog.SelectedVehicles.Select(v => v.VehicleTypeId).ToList();
                await _dataService.MapPartsToVehiclesAsync(vehicleIds, new List<string> { SelectedPart.PartNumber }, "current_user");

                StatusMessage = "המיפוי בוצע בהצלחה";
                await LoadMappedVehiclesAsync(SelectedPart.PartNumber);
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
    private async Task RemoveVehicleMappingAsync(VehicleDisplayModel? vehicle)
    {
        if (vehicle == null || SelectedPart == null)
            return;

        var result = MessageBox.Show(
            $"האם להסיר את המיפוי של '{SelectedPart.PartName}' מ-'{vehicle.DisplayName}'?",
            "אישור הסרה",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                IsLoading = true;
                StatusMessage = "מסיר מיפוי...";

                await _dataService.UnmapPartsFromVehiclesAsync(
                    new List<int> { vehicle.VehicleTypeId },
                    new List<string> { SelectedPart.PartNumber },
                    "current_user"
                );

                StatusMessage = "המיפוי הוסר בהצלחה";
                await LoadMappedVehiclesAsync(SelectedPart.PartNumber);
            }
            catch (Exception ex)
            {
                StatusMessage = $"שגיאה: {ex.Message}";
                MessageBox.Show($"שגיאה בהסרת מיפוי: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
