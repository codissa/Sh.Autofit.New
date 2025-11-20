using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Helpers;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class ModelMappingsManagementViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

    [ObservableProperty]
    private ObservableCollection<VehicleModelGroup> _modelGroups = new();

    [ObservableProperty]
    private ObservableCollection<VehicleModelGroup> _filteredModelGroups = new();

    [ObservableProperty]
    private VehicleModelGroup? _selectedModelGroup;

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _mappedParts = new();

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _suggestedParts = new();

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

    public ModelMappingsManagementViewModel(IDataService dataService, IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _dataService = dataService;
        _contextFactory = contextFactory;
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

            // Group vehicles by manufacturer, model name, AND all distinguishing characteristics
            // This splits models with different engine volumes, fuel types, transmissions, and trim levels
            var groups = _allVehicles
                .GroupBy(v => new
                {
                    ManufacturerNameNormalized = v.ManufacturerName.NormalizeForGrouping(),
                    ModelNameNormalized = v.ModelName.NormalizeForGrouping(),
                    v.ManufacturerName,
                    v.ManufacturerShortName,
                    v.ModelName,
                    // SPLIT BY THESE CRITERIA (NOT FinishLevel):
                    v.EngineVolume,
                    FuelTypeNormalized = v.FuelTypeName?.NormalizeForGrouping(),
                    TransmissionTypeNormalized = v.TransmissionType?.NormalizeForGrouping(),
                    TrimLevelNormalized = v.TrimLevel?.NormalizeForGrouping()
                })
                .Select(g => new VehicleModelGroup
                {
                    ManufacturerName = g.Key.ManufacturerName,
                    ManufacturerShortName = g.Key.ManufacturerShortName,
                    ModelName = g.Key.ModelName,
                    VehicleCount = g.Count(),
                    YearFrom = g.Min(v => v.YearFrom),
                    YearTo = g.Max(v => v.YearFrom),
                    MappedPartsCount = 0, // Will be calculated when needed
                    // Since we're grouping by these, each group has uniform values:
                    EngineVolumes = g.Key.EngineVolume.HasValue
                                    ? new List<int> { g.Key.EngineVolume.Value }
                                    : new List<int>(),
                    FuelTypes = !string.IsNullOrEmpty(g.First().FuelTypeName)
                                ? new List<string> { g.First().FuelTypeName }
                                : new List<string>(),
                    CommercialNames = g.Where(v => !string.IsNullOrEmpty(v.CommercialName))
                                      .Select(v => v.CommercialName!)
                                      .Distinct()
                                      .ToList(),
                    TransmissionTypes = !string.IsNullOrEmpty(g.First().TransmissionType)
                                        ? new List<string> { g.First().TransmissionType }
                                        : new List<string>(),
                    FinishLevels = g.Where(v => !string.IsNullOrEmpty(v.FinishLevel))
                                   .Select(v => v.FinishLevel!)
                                   .Distinct()
                                   .ToList(),
                    TrimLevels = !string.IsNullOrEmpty(g.First().TrimLevel)
                                 ? new List<string> { g.First().TrimLevel }
                                 : new List<string>()
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
                m.ModelName.ToLower().Contains(searchLower) ||
                (m.CommercialNames != null && m.CommercialNames.Any(c => c.ToLower().Contains(searchLower))));
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
            _ = LoadSuggestedPartsAsync(value);
        }
        else
        {
            MappedParts.Clear();
            SuggestedParts.Clear();
        }
    }

    private async Task LoadMappedPartsAsync(VehicleModelGroup modelGroup)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "טוען חלקים ממופים...";

            // Get all vehicle IDs for this VARIANT (matching all criteria: model, engine, fuel, transmission, trim)
            var vehicleIds = _allVehicles
                .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(modelGroup.ManufacturerName) &&
                           v.ModelName.EqualsIgnoringWhitespace(modelGroup.ModelName) &&
                           MatchesVariantCriteria(v, modelGroup))
                .Select(v => v.VehicleTypeId)
                .ToList();

            StatusMessage = $"נמצאו {vehicleIds.Count} רכבים בווריאנט, טוען חלקים...";

            // Load parts for each vehicle and find common parts
            var partsByVehicle = new Dictionary<int, HashSet<string>>();
            foreach (var vehicleId in vehicleIds)
            {
                var parts = await _dataService.LoadMappedPartsAsync(vehicleId);
                partsByVehicle[vehicleId] = parts.Select(p => p.PartNumber).ToHashSet();
            }

            // Find parts that are mapped to at least one vehicle in this model
            var allPartNumbers = partsByVehicle.Values.SelectMany(p => p).Distinct().ToList();

            StatusMessage = $"נמצאו {allPartNumbers.Count} חלקים ייחודיים, טוען פרטים...";

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

            if (MappedParts.Count == 0)
            {
                StatusMessage = $"אין חלקים ממופים לדגם {modelGroup.ModelName}. לחץ על 'הוסף חלקים' כדי להתחיל.";
            }
            else
            {
                StatusMessage = $"{MappedParts.Count} חלקים ממופים לדגם זה";
            }
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

    private async Task LoadSuggestedPartsAsync(VehicleModelGroup modelGroup)
    {
        try
        {
            var suggestions = await _dataService.GetSuggestedPartsForModelAsync(
                modelGroup.ManufacturerName,
                modelGroup.ModelName);

            SuggestedParts.Clear();
            foreach (var part in suggestions)
            {
                SuggestedParts.Add(part);
            }
        }
        catch (Exception)
        {
            // Silently fail for suggestions - they're optional
            SuggestedParts.Clear();
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

        // Use the old simple SelectPartsDialog
        var allParts = await _dataService.LoadPartsAsync();
        var unmappedParts = allParts.Where(p => !MappedParts.Any(mp => mp.PartNumber == p.PartNumber)).ToList();

        var dialog = new Views.SelectPartsDialog(unmappedParts);
        if (dialog.ShowDialog() == true && dialog.SelectedParts.Any())
        {
            try
            {
                IsLoading = true;
                StatusMessage = "ממפה חלקים לכל הרכבים בווריאנט...";

                // IMPORTANT: Get vehicle IDs for this VARIANT (matching all criteria)
                var vehicleIds = _allVehicles
                    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(SelectedModelGroup.ManufacturerName) &&
                               v.ModelName.EqualsIgnoringWhitespace(SelectedModelGroup.ModelName) &&
                               MatchesVariantCriteria(v, SelectedModelGroup))
                    .Select(v => v.VehicleTypeId)
                    .ToList();

                var partNumbers = dialog.SelectedParts.Select(p => p.PartNumber).ToList();
                await _dataService.MapPartsToVehiclesAsync(vehicleIds, partNumbers, "current_user");

                StatusMessage = $"✓ מופו {partNumbers.Count} חלקים ל-{vehicleIds.Count} רכבים בווריאנט";
                await LoadMappedPartsAsync(SelectedModelGroup);
                await LoadSuggestedPartsAsync(SelectedModelGroup);
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
                StatusMessage = "מסיר חלק מכל הרכבים בווריאנט...";

                // Get vehicle IDs for this VARIANT (matching all criteria)
                var vehicleIds = _allVehicles
                    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(SelectedModelGroup.ManufacturerName) &&
                               v.ModelName.EqualsIgnoringWhitespace(SelectedModelGroup.ModelName) &&
                               MatchesVariantCriteria(v, SelectedModelGroup))
                    .Select(v => v.VehicleTypeId)
                    .ToList();

                await _dataService.UnmapPartsFromVehiclesAsync(
                    vehicleIds,
                    new List<string> { part.PartNumber },
                    "current_user"
                );

                StatusMessage = "החלק הוסר בהצלחה";
                await LoadMappedPartsAsync(SelectedModelGroup);
                await LoadSuggestedPartsAsync(SelectedModelGroup);
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

    [RelayCommand]
    private async Task CopyMappingFromOtherModelAsync()
    {
        if (SelectedModelGroup == null)
        {
            MessageBox.Show("אנא בחר דגם תחילה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Show dialog to select source model
            var sourceModels = FilteredModelGroups
                .Where(m => m != SelectedModelGroup) // Exclude current model
                .ToList();

            if (!sourceModels.Any())
            {
                MessageBox.Show("אין דגמים זמינים להעתקה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Create a simple selection dialog
            var dialog = new Window
            {
                Title = "בחר דגם מקור להעתקת מיפויים",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                FlowDirection = FlowDirection.RightToLeft
            };

            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var listBox = new System.Windows.Controls.ListBox
            {
                ItemsSource = sourceModels,
                DisplayMemberPath = "DisplayName",
                Margin = new Thickness(10)
            };
            System.Windows.Controls.Grid.SetRow(listBox, 0);

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "העתק",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };
            okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "ביטול",
                Width = 100,
                Height = 30,
                Margin = new Thickness(5)
            };
            cancelButton.Click += (s, e) => { dialog.Close(); };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 1);

            grid.Children.Add(listBox);
            grid.Children.Add(buttonPanel);
            dialog.Content = grid;

            if (dialog.ShowDialog() == true && listBox.SelectedItem is VehicleModelGroup selectedSource)
            {
                IsLoading = true;
                StatusMessage = "מעתיק מיפויים...";

                // Get all vehicle IDs for source model
                var sourceVehicleIds = _allVehicles
                    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(selectedSource.ManufacturerName) &&
                               v.ModelName.EqualsIgnoringWhitespace(selectedSource.ModelName))
                    .Select(v => v.VehicleTypeId)
                    .ToList();

                // Get all vehicle IDs for target model
                var targetVehicleIds = _allVehicles
                    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(SelectedModelGroup.ManufacturerName) &&
                               v.ModelName.EqualsIgnoringWhitespace(SelectedModelGroup.ModelName))
                    .Select(v => v.VehicleTypeId)
                    .ToList();

                // Get all unique part numbers mapped to source vehicles
                var sourcePartNumbers = new HashSet<string>();
                foreach (var vehicleId in sourceVehicleIds)
                {
                    var parts = await _dataService.LoadMappedPartsAsync(vehicleId);
                    foreach (var part in parts)
                    {
                        sourcePartNumbers.Add(part.PartNumber);
                    }
                }

                // Copy mappings
                await _dataService.MapPartsToVehiclesAsync(
                    targetVehicleIds,
                    sourcePartNumbers.ToList(),
                    "current_user");

                StatusMessage = $"הועתקו {sourcePartNumbers.Count} חלקים מ-{selectedSource.ModelName}";
                MessageBox.Show(
                    $"הועתקו בהצלחה {sourcePartNumbers.Count} חלקים מ-{selectedSource.ModelName} ל-{SelectedModelGroup.ModelName}",
                    "הצלחה",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Reload parts
                await LoadMappedPartsAsync(SelectedModelGroup);
                await LoadSuggestedPartsAsync(SelectedModelGroup);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה בהעתקת מיפויים: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AcceptPartSuggestionAsync(PartDisplayModel? part)
    {
        if (part == null || SelectedModelGroup == null)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "ממפה חלק...";

            // Map directly to ALL vehicles in the selected model (model-level mapping)
            var vehicleTypeIds = _allVehicles
                .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(SelectedModelGroup.ManufacturerName) &&
                           v.ModelName.EqualsIgnoringWhitespace(SelectedModelGroup.ModelName))
                .Select(v => v.VehicleTypeId)
                .ToList();

            if (!vehicleTypeIds.Any())
            {
                MessageBox.Show("לא נמצאו רכבים במודל זה", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Map the suggested part to ALL vehicles in the model
            await _dataService.MapPartsToVehiclesAsync(vehicleTypeIds, new List<string> { part.PartNumber }, "current_user");

            StatusMessage = $"✓ מופה החלק {part.PartNumber} ל-{vehicleTypeIds.Count} רכבים במודל {SelectedModelGroup.ModelName}";

            // Reload both lists
            await LoadMappedPartsAsync(SelectedModelGroup);
            await LoadSuggestedPartsAsync(SelectedModelGroup);
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

    /// <summary>
    /// Checks if a vehicle matches the variant criteria of a model group
    /// (engine volume, fuel type, transmission type, trim level)
    /// </summary>
    private bool MatchesVariantCriteria(VehicleDisplayModel vehicle, VehicleModelGroup modelGroup)
    {
        // Check engine volume
        if (modelGroup.EngineVolumes.Any())
        {
            if (!vehicle.EngineVolume.HasValue || !modelGroup.EngineVolumes.Contains(vehicle.EngineVolume.Value))
                return false;
        }

        // Check fuel type
        if (modelGroup.FuelTypes.Any())
        {
            if (string.IsNullOrEmpty(vehicle.FuelTypeName) ||
                !modelGroup.FuelTypes.Any(f => f.EqualsIgnoringWhitespace(vehicle.FuelTypeName)))
                return false;
        }

        // Check transmission type
        if (modelGroup.TransmissionTypes.Any())
        {
            if (string.IsNullOrEmpty(vehicle.TransmissionType) ||
                !modelGroup.TransmissionTypes.Any(t => t.EqualsIgnoringWhitespace(vehicle.TransmissionType)))
                return false;
        }

        // Check trim level (NOT finish level - we're not grouping by that)
        if (modelGroup.TrimLevels.Any())
        {
            if (string.IsNullOrEmpty(vehicle.TrimLevel) ||
                !modelGroup.TrimLevels.Any(t => t.EqualsIgnoringWhitespace(vehicle.TrimLevel)))
                return false;
        }

        return true;
    }
}
