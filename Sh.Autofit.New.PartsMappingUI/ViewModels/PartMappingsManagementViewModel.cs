using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.PartsMappingUI.Helpers;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using static Sh.Autofit.New.PartsMappingUI.Helpers.VehicleMatchingHelper;

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

    // Grouped view of mapped vehicles
    public ICollectionView? MappedVehiclesView { get; private set; }

    [ObservableProperty]
    private ObservableCollection<VehicleDisplayModel> _suggestedVehicles = new();

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
            _ = LoadSuggestedVehiclesAsync(value.PartNumber);
        }
        else
        {
            MappedVehicles.Clear();
            SuggestedVehicles.Clear();
        }
    }

    private async Task LoadMappedVehiclesAsync(string partNumber)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען רכבים ממופים...";

            // Use efficient method to load only mapped vehicles
            var vehicles = await _dataService.LoadVehiclesForPartAsync(partNumber);

            MappedVehicles.Clear();
            foreach (var vehicle in vehicles)
            {
                MappedVehicles.Add(vehicle);
            }

            // Create grouped view
            MappedVehiclesView = CollectionViewSource.GetDefaultView(MappedVehicles);
            MappedVehiclesView.GroupDescriptions.Clear();

            // Group by variant key (manufacturer + model + engine + fuel + transmission + finish + trim)
            MappedVehiclesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(VehicleDisplayModel.ManufacturerName)));
            MappedVehiclesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(VehicleDisplayModel.ModelName)));
            MappedVehiclesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(VehicleDisplayModel.EngineVolume)));
            MappedVehiclesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(VehicleDisplayModel.FuelTypeName)));
            MappedVehiclesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(VehicleDisplayModel.TransmissionType)));
            MappedVehiclesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(VehicleDisplayModel.FinishLevel)));
            MappedVehiclesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(VehicleDisplayModel.TrimLevel)));

            OnPropertyChanged(nameof(MappedVehiclesView));

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

    private async Task LoadSuggestedVehiclesAsync(string partNumber)
    {
        try
        {
            var suggestions = await _dataService.GetSuggestedVehiclesForPartAsync(partNumber);

            SuggestedVehicles.Clear();
            foreach (var vehicle in suggestions)
            {
                SuggestedVehicles.Add(vehicle);
            }
        }
        catch (Exception ex)
        {
            // Silently fail for suggestions - they're optional
            SuggestedVehicles.Clear();
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

        var variantDescription = GetVariantDescription(vehicle);

        // Ask user if they want to unmap from just this vehicle variant or all variants of the model
        var result = MessageBox.Show(
            $"האם להסיר את '{SelectedPart.PartName}' מכל הווריאנטים של '{vehicle.ModelName}'?\n\n" +
            $"הווריאנט הנוכחי: {variantDescription}\n\n" +
            "בחר 'כן' להסרה מכל הווריאנטים של הדגם (כל נפחי מנוע, תיבות הילוכים ורמות גימור),\n" +
            "'לא' להסרה רק מהווריאנט המדויק הזה (כולל שנות ייצור שונות),\n" +
            "או 'ביטול'.",
            "אישור הסרת מיפוי",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Cancel)
            return;

        try
        {
            IsLoading = true;

            List<int> vehicleIds;

            if (result == MessageBoxResult.Yes)
            {
                // Unmap from ALL variants of this model
                StatusMessage = "מסיר מיפוי מכל הווריאנטים של הדגם...";

                var allVehicles = await _dataService.LoadVehiclesAsync();
                vehicleIds = allVehicles
                    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(vehicle.ManufacturerName) &&
                               v.ModelName.EqualsIgnoringWhitespace(vehicle.ModelName))
                    .Select(v => v.VehicleTypeId)
                    .ToList();
            }
            else
            {
                // Unmap from only this specific variant
                StatusMessage = "מסיר מיפוי מהווריאנט המדויק...";

                var allVehicles = await _dataService.LoadVehiclesAsync();
                var sameVariantVehicles = GetSameVariantVehicles(allVehicles, vehicle);
                vehicleIds = sameVariantVehicles.Select(v => v.VehicleTypeId).ToList();
            }

            await _dataService.UnmapPartsFromVehiclesAsync(
                vehicleIds,
                new List<string> { SelectedPart.PartNumber },
                "current_user"
            );

            var scope = result == MessageBoxResult.Yes ? "כל הווריאנטים" : $"הווריאנט {variantDescription}";
            StatusMessage = $"המיפוי הוסר מ-{scope} ({vehicleIds.Count} רכבים)";

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

    [RelayCommand]
    private async Task AcceptSuggestionAsync(VehicleDisplayModel? vehicle)
    {
        if (vehicle == null || SelectedPart == null)
            return;

        var variantDescription = GetVariantDescription(vehicle);

        // Ask user if they want to map to all variants or just this variant
        var result = MessageBox.Show(
            $"האם למפות '{SelectedPart.PartName}' לכל הווריאנטים של '{vehicle.ModelName}'?\n\n" +
            $"הווריאנט המוצע: {variantDescription}\n\n" +
            "בחר 'כן' למיפוי לכל הווריאנטים (כל נפחי מנוע, תיבות הילוכים ורמות גימור),\n" +
            "'לא' למיפוי רק לווריאנט המדויק הזה,\n" +
            "או 'ביטול'.",
            "בחירת היקף מיפוי",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Cancel)
            return;

        try
        {
            IsLoading = true;

            List<int> vehicleIds;

            if (result == MessageBoxResult.Yes)
            {
                // Map to ALL variants of this model
                StatusMessage = "ממפה לכל הווריאנטים...";

                var allVehicles = await _dataService.LoadVehiclesAsync();
                vehicleIds = allVehicles
                    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(vehicle.ManufacturerName) &&
                               v.ModelName.EqualsIgnoringWhitespace(vehicle.ModelName))
                    .Select(v => v.VehicleTypeId)
                    .ToList();
            }
            else
            {
                // Map only to this specific variant
                StatusMessage = "ממפה לווריאנט המדויק...";

                var allVehicles = await _dataService.LoadVehiclesAsync();
                var sameVariantVehicles = GetSameVariantVehicles(allVehicles, vehicle);
                vehicleIds = sameVariantVehicles.Select(v => v.VehicleTypeId).ToList();
            }

            // Add the mapping
            await _dataService.MapPartsToVehiclesAsync(
                vehicleIds,
                new List<string> { SelectedPart.PartNumber },
                "current_user"
            );

            var scope = result == MessageBoxResult.Yes ? "כל הווריאנטים" : $"הווריאנט {variantDescription}";
            StatusMessage = $"ההצעה אושרה - מופה ל-{scope} ({vehicleIds.Count} רכבים)";

            // Reload both lists
            await LoadMappedVehiclesAsync(SelectedPart.PartNumber);
            await LoadSuggestedVehiclesAsync(SelectedPart.PartNumber);
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה באישור הצעה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
