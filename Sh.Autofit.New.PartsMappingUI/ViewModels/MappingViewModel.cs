using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class MappingViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly ISettingsService _settingsService;
    private readonly IPartSuggestionService _suggestionService;
    private readonly IPartKitService _partKitService;
    private AppSettings _appSettings;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // Vehicle-related properties
    [ObservableProperty]
    private ObservableCollection<ManufacturerGroup> _manufacturerGroups = new();

    [ObservableProperty]
    private ObservableCollection<VehicleDisplayModel> _allVehicles = new();

    [ObservableProperty]
    private string _vehicleSearchText = string.Empty;

    [ObservableProperty]
    private bool _vehicleFilterUnmappedOnly;

    [ObservableProperty]
    private string? _selectedVehicleManufacturer;

    [ObservableProperty]
    private string? _selectedVehicleCategory;

    // Part-related properties
    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _allParts = new();

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _filteredParts = new();

    [ObservableProperty]
    private string _partSearchText = string.Empty;

    [ObservableProperty]
    private string? _selectedPartCategory;

    [ObservableProperty]
    private bool _partFilterUnmappedOnly;

    [ObservableProperty]
    private bool _partFilterUniversalOnly;

    // Statistics
    [ObservableProperty]
    private int _totalVehicles;

    [ObservableProperty]
    private int _totalParts;

    [ObservableProperty]
    private int _mappedVehicles;

    [ObservableProperty]
    private int _mappedParts;

    public ObservableCollection<string> AvailableManufacturers { get; } = new();
    public ObservableCollection<string> AvailableVehicleCategories { get; } = new();
    public ObservableCollection<string> AvailablePartCategories { get; } = new();

    [ObservableProperty]
    private bool _showSuggestedPartsFirst;

    [ObservableProperty]
    private bool _suggestionsEnabled;

    public int SelectedVehiclesCount => AllVehicles.Count(v => v.IsSelected);
    public int SelectedPartsCount => FilteredParts.Count(p => p.IsSelected);

    public MappingViewModel(IDataService dataService, ISettingsService settingsService, IPartSuggestionService suggestionService, IPartKitService partKitService)
    {
        _dataService = dataService;
        _settingsService = settingsService;
        _suggestionService = suggestionService;
        _partKitService = partKitService;
        _appSettings = _settingsService.LoadSettings();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading data...";

            // Load vehicle group summary (lightweight - only counts, no full vehicle data)
            StatusMessage = "Loading vehicle groups...";
            var groupSummary = await _dataService.LoadVehicleGroupSummaryAsync();

            // Build manufacturer and commercial name groups structure
            // Group ONLY by ManufacturerShortName to combine all vehicle types under the same manufacturer
            var manufacturerGroups = groupSummary
                .GroupBy(s => s.ManufacturerShortName)
                .Select(mg => new ManufacturerGroup
                {
                    ManufacturerShortName = mg.Key,
                    ManufacturerName = mg.First().ManufacturerName, // Use the first manufacturer name for display
                    CommercialNameGroups = new ObservableCollection<CommercialNameGroup>(
                        mg.Select(g =>
                        {
                            var cng = new CommercialNameGroup
                            {
                                ManufacturerShortName = g.ManufacturerShortName,
                                CommercialName = g.CommercialName,
                                VehicleCount = g.Count
                            };
                            // Add a placeholder to show expander arrow
                            if (cng.HasChildren)
                            {
                                cng.ModelGroups.Add(new ModelGroup { ModelName = "Loading..." });
                            }
                            return cng;
                        })
                        .OrderBy(c => c.CommercialName)
                    )
                })
                .OrderBy(m => m.ManufacturerShortName)
                .ToList();

            ManufacturerGroups = new ObservableCollection<ManufacturerGroup>(manufacturerGroups);

            // Subscribe to expansion events for all commercial name groups
            foreach (var mfg in ManufacturerGroups)
            {
                foreach (var cng in mfg.CommercialNameGroups)
                {
                    cng.PropertyChanged += CommercialNameGroup_PropertyChanged;
                }
            }

            // Clear AllVehicles since we're using lazy loading now
            AllVehicles = new ObservableCollection<VehicleDisplayModel>();

            // Load parts
            StatusMessage = "Loading parts...";
            var parts = await _dataService.LoadPartsAsync();
            AllParts = new ObservableCollection<PartDisplayModel>(parts);

            // Load mapping counts
            StatusMessage = "Loading mapping statistics...";
            var partMappingCounts = await _dataService.LoadPartMappingCountsAsync();

            // Update part mapping status
            foreach (var part in AllParts)
            {
                if (partMappingCounts.TryGetValue(part.PartNumber, out var count))
                {
                    part.MappedVehiclesCount = count;
                    part.MappingStatus = count > 0
                        ? (count >= 10 ? MappingStatus.Mapped : MappingStatus.PartiallyMapped)
                        : MappingStatus.Unmapped;
                }
            }

            // Populate filters
            PopulateFilters();

            // Initialize filtered parts
            FilteredParts = new ObservableCollection<PartDisplayModel>(AllParts);

            // Load statistics
            await LoadStatisticsAsync();

            StatusMessage = $"Loaded {ManufacturerGroups.Sum(m => m.TotalVehicleCount)} vehicle groups and {AllParts.Count} parts";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to load data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void CommercialNameGroup_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommercialNameGroup.IsExpanded) && sender is CommercialNameGroup group)
        {
            if (group.IsExpanded && !group.IsLoaded && !group.IsLoading)
            {
                await LoadCommercialNameGroupModelsAsync(group);
            }
        }
    }

    private async void ModelGroup_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModelGroup.IsExpanded) && sender is ModelGroup group)
        {
            if (group.IsExpanded && !group.IsLoaded && !group.IsLoading)
            {
                await LoadModelGroupVehiclesAsync(group);
            }
        }
    }

    private async Task LoadCommercialNameGroupModelsAsync(CommercialNameGroup group)
    {
        try
        {
            group.IsLoading = true;
            StatusMessage = $"Loading models for {group.CommercialName ?? "(No Commercial Name)"}...";

            // Load model summary for this commercial name
            var modelSummary = await _dataService.LoadModelGroupSummaryAsync(
                group.ManufacturerShortName,
                group.CommercialName ?? string.Empty);

            // Clear placeholder
            group.ModelGroups.Clear();

            // Build model groups
            var modelGroups = modelSummary.Select(m =>
            {
                var modelGroup = new ModelGroup
                {
                    ManufacturerShortName = group.ManufacturerShortName,
                    CommercialName = group.CommercialName ?? string.Empty,
                    ModelName = m.ModelName,
                    VehicleCount = m.Count,
                    YearFrom = m.YearFrom,
                    YearTo = m.YearTo,
                    EngineVolume = m.EngineVolume,
                    FuelType = m.FuelType,
                    TransmissionType = m.TransmissionType,
                    TrimLevel = m.TrimLevel
                };
                // Add a placeholder to show expander arrow
                if (modelGroup.HasChildren)
                {
                    modelGroup.Vehicles.Add(new VehicleDisplayModel { ModelName = "Loading..." });
                }
                return modelGroup;
            }).OrderBy(m => m.ModelName).ToList();

            foreach (var modelGroup in modelGroups)
            {
                group.ModelGroups.Add(modelGroup);
            }

            // Subscribe to expansion events for all model groups
            foreach (var modelGroup in group.ModelGroups)
            {
                modelGroup.PropertyChanged += ModelGroup_PropertyChanged;
            }

            group.IsLoaded = true;

            StatusMessage = $"Loaded {modelGroups.Count} model(s) for {group.CommercialName ?? "(No Commercial Name)"}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading models: {ex.Message}";
            MessageBox.Show($"Failed to load models: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            group.IsLoading = false;
        }
    }

    private async Task LoadModelGroupVehiclesAsync(ModelGroup group)
    {
        try
        {
            group.IsLoading = true;
            StatusMessage = $"Loading vehicles for {group.ModelName}...";

            // Load vehicles for this model (with optional engine volume filter)
            var vehicles = await _dataService.LoadVehiclesByModelAsync(
                group.ManufacturerShortName,
                group.CommercialName,
                group.ModelName,
                group.EngineVolume);

            // Load mapping counts
            var vehicleMappingCounts = await _dataService.LoadMappingCountsAsync();

            // Update vehicle mapping status
            foreach (var vehicle in vehicles)
            {
                if (vehicleMappingCounts.TryGetValue(vehicle.VehicleTypeId, out var count))
                {
                    vehicle.MappedPartsCount = count;
                    vehicle.MappingStatus = count > 0
                        ? (count >= 10 ? MappingStatus.Mapped : MappingStatus.PartiallyMapped)
                        : MappingStatus.Unmapped;
                }

                // Subscribe to selection changes
                vehicle.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(VehicleDisplayModel.IsSelected))
                        OnPropertyChanged(nameof(SelectedVehiclesCount));
                };
            }

            // Clear placeholder
            group.Vehicles.Clear();

            // Add vehicles
            foreach (var vehicle in vehicles.OrderBy(v => v.YearFrom))
            {
                group.Vehicles.Add(vehicle);
            }
            group.IsLoaded = true;

            StatusMessage = $"Loaded {vehicles.Count} vehicle(s) for model {group.ModelName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading vehicles: {ex.Message}";
            MessageBox.Show($"Failed to load vehicles: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            group.IsLoading = false;
        }
    }

    private void PopulateFilters()
    {
        // Manufacturers
        AvailableManufacturers.Clear();
        var manufacturers = ManufacturerGroups
            .Select(m => m.ManufacturerShortName)
            .Distinct()
            .OrderBy(m => m);
        foreach (var mfr in manufacturers)
            AvailableManufacturers.Add(mfr);

        // Vehicle categories - will be populated as vehicles are loaded
        AvailableVehicleCategories.Clear();

        // Part categories
        AvailablePartCategories.Clear();
        var partCategories = AllParts
            .Where(p => !string.IsNullOrEmpty(p.Category))
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c);
        foreach (var cat in partCategories)
            AvailablePartCategories.Add(cat);
    }

    // No longer needed - groups are built from summary data in LoadDataAsync

    partial void OnVehicleSearchTextChanged(string value)
    {
        ApplyVehicleFilters();
    }

    partial void OnVehicleFilterUnmappedOnlyChanged(bool value)
    {
        ApplyVehicleFilters();
    }

    partial void OnSelectedVehicleManufacturerChanged(string? value)
    {
        ApplyVehicleFilters();
    }

    partial void OnSelectedVehicleCategoryChanged(string? value)
    {
        ApplyVehicleFilters();
    }

    private void ApplyVehicleFilters()
    {
        var searchLower = VehicleSearchText?.ToLower() ?? string.Empty;
        var hasSearch = !string.IsNullOrWhiteSpace(searchLower);

        foreach (var mfgGroup in ManufacturerGroups)
        {
            // Check manufacturer filter
            if (!string.IsNullOrEmpty(SelectedVehicleManufacturer) &&
                mfgGroup.ManufacturerShortName != SelectedVehicleManufacturer)
            {
                continue; // Skip this manufacturer entirely
            }

            // Check if manufacturer name matches search
            bool mfgMatches = !hasSearch ||
                             mfgGroup.ManufacturerName?.ToLower().Contains(searchLower) == true ||
                             mfgGroup.ManufacturerShortName?.ToLower().Contains(searchLower) == true;

            bool anyCommercialNameHasMatches = false;

            foreach (var cng in mfgGroup.CommercialNameGroups)
            {
                // Check if commercial name matches search
                bool cngMatches = mfgMatches ||
                                 (!hasSearch || cng.CommercialName?.ToLower().Contains(searchLower) == true);

                bool anyModelHasMatches = false;

                foreach (var modelGroup in cng.ModelGroups)
                {
                    // Check if model name matches search
                    bool modelMatches = cngMatches ||
                                       (!hasSearch || modelGroup.ModelName?.ToLower().Contains(searchLower) == true);

                    bool anyVehicleVisible = false;

                    foreach (var vehicle in modelGroup.Vehicles)
                    {
                        // Skip placeholders
                        if (vehicle.VehicleTypeId == 0)
                        {
                            vehicle.IsVisible = false;
                            continue;
                        }

                        bool visible = modelMatches;

                        // Check vehicle-level search if not already matched
                        if (hasSearch && !visible)
                        {
                            visible = vehicle.ManufacturerName?.ToLower().Contains(searchLower) == true ||
                                     vehicle.ManufacturerShortName?.ToLower().Contains(searchLower) == true ||
                                     vehicle.ModelName?.ToLower().Contains(searchLower) == true ||
                                     vehicle.CommercialName?.ToLower().Contains(searchLower) == true ||
                                     vehicle.VehicleCategory?.ToLower().Contains(searchLower) == true ||
                                     vehicle.FuelTypeName?.ToLower().Contains(searchLower) == true ||
                                     vehicle.EngineModel?.ToLower().Contains(searchLower) == true ||
                                     vehicle.YearFrom.ToString().Contains(searchLower) ||
                                     (vehicle.YearTo.HasValue && vehicle.YearTo.ToString()!.Contains(searchLower)) ||
                                     (vehicle.EngineVolume.HasValue && vehicle.EngineVolume.ToString()!.Contains(searchLower));
                        }

                        // Apply unmapped filter
                        if (visible && VehicleFilterUnmappedOnly)
                        {
                            visible = vehicle.MappingStatus == MappingStatus.Unmapped;
                        }

                        // Apply category filter
                        if (visible && !string.IsNullOrEmpty(SelectedVehicleCategory))
                        {
                            visible = vehicle.VehicleCategory == SelectedVehicleCategory;
                        }

                        vehicle.IsVisible = visible;

                        if (visible)
                        {
                            anyVehicleVisible = true;
                        }
                    }

                    // Auto-expand/collapse model group based on matches
                    if (anyVehicleVisible)
                    {
                        anyModelHasMatches = true;

                        // If there are matching vehicles and search is active, expand to show them
                        if (hasSearch && modelGroup.IsLoaded)
                        {
                            modelGroup.IsExpanded = true;
                        }
                    }
                }

                // Auto-expand/collapse commercial name group based on matches
                if (anyModelHasMatches)
                {
                    anyCommercialNameHasMatches = true;

                    // If there are matching models and search is active, expand to show them
                    if (hasSearch && cng.IsLoaded)
                    {
                        cng.IsExpanded = true;
                    }
                }
            }

            // Auto-expand manufacturer group if it has matches
            if (anyCommercialNameHasMatches && hasSearch)
            {
                mfgGroup.IsExpanded = true;
            }
        }

        OnPropertyChanged(nameof(ManufacturerGroups));
    }

    partial void OnPartSearchTextChanged(string value)
    {
        ApplyPartFilters();
    }

    partial void OnSelectedPartCategoryChanged(string? value)
    {
        ApplyPartFilters();
    }

    partial void OnPartFilterUnmappedOnlyChanged(bool value)
    {
        ApplyPartFilters();
    }

    partial void OnPartFilterUniversalOnlyChanged(bool value)
    {
        ApplyPartFilters();
    }

    private void ApplyPartFilters()
    {
        var filtered = AllParts.AsEnumerable();

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

        if (!string.IsNullOrEmpty(SelectedPartCategory))
        {
            filtered = filtered.Where(p => p.Category == SelectedPartCategory);
        }

        if (PartFilterUnmappedOnly)
        {
            filtered = filtered.Where(p => p.MappingStatus == MappingStatus.Unmapped);
        }

        if (PartFilterUniversalOnly)
        {
            filtered = filtered.Where(p => p.UniversalPart);
        }

        // Sort by relevance if suggestions are enabled and showing suggested first
        if (SuggestionsEnabled && ShowSuggestedPartsFirst)
        {
            filtered = filtered.OrderByDescending(p => p.RelevanceScore)
                              .ThenBy(p => p.PartNumber);
        }

        FilteredParts = new ObservableCollection<PartDisplayModel>(filtered);
    }

    [RelayCommand]
    private async Task CalculateSuggestionsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Preparing vehicles...";

            // Collect all commercial name groups and model groups that need to be loaded
            var commercialGroupsToLoad = new List<CommercialNameGroup>();
            var modelGroupsToLoad = new List<ModelGroup>();

            foreach (var mfg in ManufacturerGroups)
            {
                if (mfg.IsSelected)
                {
                    // Load all commercial name groups and their models
                    foreach (var cng in mfg.CommercialNameGroups)
                    {
                        if (!cng.IsLoaded && !cng.IsLoading)
                            commercialGroupsToLoad.Add(cng);

                        // Also load all model groups if the commercial name group is loaded
                        if (cng.IsLoaded)
                        {
                            modelGroupsToLoad.AddRange(cng.ModelGroups.Where(m => !m.IsLoaded && !m.IsLoading));
                        }
                    }
                }
                else
                {
                    // Check individual commercial name groups and model groups
                    foreach (var cng in mfg.CommercialNameGroups)
                    {
                        if (cng.IsSelected)
                        {
                            if (!cng.IsLoaded && !cng.IsLoading)
                                commercialGroupsToLoad.Add(cng);

                            if (cng.IsLoaded)
                            {
                                modelGroupsToLoad.AddRange(cng.ModelGroups.Where(m => !m.IsLoaded && !m.IsLoading));
                            }
                        }
                        else if (cng.IsLoaded)
                        {
                            // Check individual model groups
                            modelGroupsToLoad.AddRange(cng.ModelGroups.Where(m => m.IsSelected && !m.IsLoaded && !m.IsLoading));
                        }
                    }
                }
            }

            // Load all required commercial name groups first
            foreach (var group in commercialGroupsToLoad)
            {
                StatusMessage = $"Loading models for {group.CommercialName ?? "(No Commercial Name)"}...";
                await LoadCommercialNameGroupModelsAsync(group);

                // After loading, add model groups to the list if they need loading
                modelGroupsToLoad.AddRange(group.ModelGroups.Where(m => !m.IsLoaded && !m.IsLoading));
            }

            // Then load all required model groups
            foreach (var group in modelGroupsToLoad)
            {
                StatusMessage = $"Loading vehicles for model {group.ModelName}...";
                await LoadModelGroupVehiclesAsync(group);
            }

            var selectedVehicles = GetAllSelectedVehicles();

            if (!selectedVehicles.Any())
            {
                MessageBox.Show("Please select at least one vehicle or vehicle group to get part suggestions.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            StatusMessage = "Calculating part suggestions...";

            // Get suggestions from the service
            var suggestions = await _suggestionService.GetSuggestionsForVehiclesAsync(selectedVehicles, AllParts.ToList());

            // Clear previous suggestions
            foreach (var part in AllParts)
            {
                part.RelevanceScore = 0;
                part.RelevanceReason = string.Empty;
                part.HasSuggestion = false;
            }

            // Apply new suggestions
            foreach (var suggestion in suggestions)
            {
                var part = AllParts.FirstOrDefault(p => p.PartNumber == suggestion.PartNumber);
                if (part != null)
                {
                    part.RelevanceScore = suggestion.RelevanceScore;
                    part.RelevanceReason = suggestion.ReasonsSummary;
                    part.HasSuggestion = true;
                }
            }

            SuggestionsEnabled = true;
            ShowSuggestedPartsFirst = true;

            // Reapply filters to sort by relevance
            ApplyPartFilters();

            StatusMessage = $"Found {suggestions.Count} suggested parts for {selectedVehicles.Count} vehicle(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to calculate suggestions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearSuggestions()
    {
        foreach (var part in AllParts)
        {
            part.RelevanceScore = 0;
            part.RelevanceReason = string.Empty;
            part.HasSuggestion = false;
        }

        SuggestionsEnabled = false;
        ShowSuggestedPartsFirst = false;

        ApplyPartFilters();
        StatusMessage = "Suggestions cleared";
    }

    partial void OnShowSuggestedPartsFirstChanged(bool value)
    {
        if (SuggestionsEnabled)
        {
            ApplyPartFilters();
        }
    }

    [RelayCommand]
    private async Task MapSelectedAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading selected vehicles...";

            // Ensure all selected groups are loaded
            await EnsureSelectedVehiclesLoadedAsync();

            var selectedVehicles = GetAllSelectedVehicles();
            var selectedParts = FilteredParts.Where(p => p.IsSelected).ToList();

            if (!selectedVehicles.Any())
            {
                MessageBox.Show("Please select at least one vehicle.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!selectedParts.Any())
            {
                MessageBox.Show("Please select at least one part.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Map {selectedParts.Count} part(s) to {selectedVehicles.Count} vehicle(s)?",
                "Confirm Mapping",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            StatusMessage = "Creating mappings...";

            var vehicleIds = selectedVehicles.Select(v => v.VehicleTypeId).ToList();
            var partNumbers = selectedParts.Select(p => p.PartNumber).ToList();

            await _dataService.MapPartsToVehiclesAsync(vehicleIds, partNumbers, "UIUser");

            // Reload mapping counts
            await RefreshMappingCountsAsync();

            StatusMessage = $"Successfully mapped {selectedParts.Count} part(s) to {selectedVehicles.Count} vehicle(s)";
            MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to create mappings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UnmapSelectedAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading selected vehicles...";

            // Ensure all selected groups are loaded
            await EnsureSelectedVehiclesLoadedAsync();

            var selectedVehicles = GetAllSelectedVehicles();
            var selectedParts = FilteredParts.Where(p => p.IsSelected).ToList();

            if (!selectedVehicles.Any() || !selectedParts.Any())
            {
                MessageBox.Show("Please select vehicles and parts to unmap.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Unmap {selectedParts.Count} part(s) from {selectedVehicles.Count} vehicle(s)?",
                "Confirm Unmapping",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            StatusMessage = "Removing mappings...";

            var vehicleIds = selectedVehicles.Select(v => v.VehicleTypeId).ToList();
            var partNumbers = selectedParts.Select(p => p.PartNumber).ToList();

            await _dataService.UnmapPartsFromVehiclesAsync(vehicleIds, partNumbers, "UIUser");

            // Reload mapping counts
            await RefreshMappingCountsAsync();

            StatusMessage = $"Successfully unmapped {selectedParts.Count} part(s) from {selectedVehicles.Count} vehicle(s)";
            MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to remove mappings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task MapKitAsync()
    {
        var selectedVehicles = AllVehicles.Where(v => v.IsSelected).ToList();

        if (!selectedVehicles.Any())
        {
            MessageBox.Show("Please select at least one vehicle to map the kit to.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Load available kits
            var kits = await _partKitService.LoadAllKitsAsync();

            if (!kits.Any())
            {
                MessageBox.Show("אין ערכות זמינות. צור ערכה בטאב 'ניהול ערכות' תחילה.", "אין ערכות", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Show kit selection dialog
            var dialog = new Views.MapKitDialog(kits, selectedVehicles.Count);
            if (dialog.ShowDialog() == true && dialog.SelectedKit != null)
            {
                IsLoading = true;
                StatusMessage = $"Mapping kit '{dialog.SelectedKit.KitName}' to {selectedVehicles.Count} vehicle(s)...";

                var vehicleIds = selectedVehicles.Select(v => v.VehicleTypeId).ToList();

                await _partKitService.MapKitToVehiclesAsync(dialog.SelectedKit.PartKitId, vehicleIds, "UIUser");

                // Reload mapping counts
                await RefreshMappingCountsAsync();

                StatusMessage = $"Successfully mapped kit '{dialog.SelectedKit.KitName}' ({dialog.SelectedKit.PartCount} parts) to {selectedVehicles.Count} vehicle(s)";
                MessageBox.Show(StatusMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to map kit: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CopyMappingAsync()
    {
        try
        {
            // Collect all loaded vehicles
            var allLoadedVehicles = new List<VehicleDisplayModel>();
            foreach (var mfgGroup in ManufacturerGroups)
            {
                allLoadedVehicles.AddRange(mfgGroup.GetAllLoadedVehicles());
            }

            // Filter out placeholders
            allLoadedVehicles = allLoadedVehicles.Where(v => v.VehicleTypeId > 0).Distinct().ToList();

            if (!allLoadedVehicles.Any())
            {
                MessageBox.Show("אנא טען רכבים תחילה על ידי פתיחת קבוצות רכבים.", "מידע", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Views.CopyMappingDialog(allLoadedVehicles);
            dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                var sourceVehicle = dialog.SourceVehicle;
                var targetVehicles = dialog.TargetVehicles;

                if (sourceVehicle == null || !targetVehicles.Any())
                    return;

                var result = MessageBox.Show(
                    $"להעתיק מיפוי מ-\n{sourceVehicle.DisplayName}\n\nל-{targetVehicles.Count} רכבים?",
                    "אישור העתקה",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                IsLoading = true;
                StatusMessage = "מעתיק מיפוי...";

                var targetVehicleIds = targetVehicles.Select(v => v.VehicleTypeId).ToList();

                await _dataService.CopyMappingsAsync(sourceVehicle.VehicleTypeId, targetVehicleIds, "UIUser");

                // Reload mapping counts
                await RefreshMappingCountsAsync();

                StatusMessage = $"מיפוי הועתק בהצלחה מ-{sourceVehicle.DisplayName} ל-{targetVehicles.Count} רכבים";
                MessageBox.Show(StatusMessage, "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"נכשל בהעתקת מיפוי: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task EnsureSelectedVehiclesLoadedAsync()
    {
        foreach (var mfgGroup in ManufacturerGroups)
        {
            if (mfgGroup.IsSelected)
            {
                // Load all commercial name groups for this manufacturer
                foreach (var cng in mfgGroup.CommercialNameGroups)
                {
                    if (!cng.IsLoaded && !cng.IsLoading)
                    {
                        await LoadCommercialNameGroupModelsAsync(cng);
                    }

                    // Load all model groups
                    foreach (var modelGroup in cng.ModelGroups)
                    {
                        if (!modelGroup.IsLoaded && !modelGroup.IsLoading)
                        {
                            await LoadModelGroupVehiclesAsync(modelGroup);
                        }
                    }
                }
            }
            else
            {
                // Check individual commercial name groups
                foreach (var cng in mfgGroup.CommercialNameGroups)
                {
                    if (cng.IsSelected)
                    {
                        // Load this commercial name group if not loaded
                        if (!cng.IsLoaded && !cng.IsLoading)
                        {
                            await LoadCommercialNameGroupModelsAsync(cng);
                        }

                        // Load all model groups
                        foreach (var modelGroup in cng.ModelGroups)
                        {
                            if (!modelGroup.IsLoaded && !modelGroup.IsLoading)
                            {
                                await LoadModelGroupVehiclesAsync(modelGroup);
                            }
                        }
                    }
                    else
                    {
                        // Check individual model groups
                        foreach (var modelGroup in cng.ModelGroups)
                        {
                            if (modelGroup.IsSelected && !modelGroup.IsLoaded && !modelGroup.IsLoading)
                            {
                                await LoadModelGroupVehiclesAsync(modelGroup);
                            }
                        }
                    }
                }
            }
        }
    }

    private List<VehicleDisplayModel> GetAllSelectedVehicles()
    {
        var selectedVehicles = new List<VehicleDisplayModel>();

        foreach (var mfgGroup in ManufacturerGroups)
        {
            if (mfgGroup.IsSelected)
            {
                // Select all loaded vehicles from all commercial name groups
                selectedVehicles.AddRange(mfgGroup.GetAllLoadedVehicles());
            }
            else
            {
                // Check each commercial name group
                foreach (var cng in mfgGroup.CommercialNameGroups)
                {
                    if (cng.IsSelected)
                    {
                        // Select all vehicles in this commercial name group
                        selectedVehicles.AddRange(cng.GetAllLoadedVehicles());
                    }
                    else
                    {
                        // Check each model group
                        foreach (var modelGroup in cng.ModelGroups)
                        {
                            if (modelGroup.IsSelected)
                            {
                                // Select all vehicles in this model group
                                selectedVehicles.AddRange(modelGroup.Vehicles);
                            }
                            else
                            {
                                // Select only individually selected vehicles
                                selectedVehicles.AddRange(modelGroup.Vehicles.Where(v => v.IsSelected));
                            }
                        }
                    }
                }
            }
        }

        // Filter out placeholder items (they have VehicleTypeId = 0)
        return selectedVehicles.Where(v => v.VehicleTypeId > 0).Distinct().ToList();
    }

    [RelayCommand]
    private void SelectAllVehicles()
    {
        foreach (var mfgGroup in ManufacturerGroups)
        {
            foreach (var cng in mfgGroup.CommercialNameGroups)
            {
                foreach (var modelGroup in cng.ModelGroups)
                {
                    foreach (var vehicle in modelGroup.Vehicles)
                    {
                        vehicle.IsSelected = true;
                    }
                }
            }
        }
        OnPropertyChanged(nameof(SelectedVehiclesCount));
    }

    [RelayCommand]
    private void DeselectAllVehicles()
    {
        foreach (var mfgGroup in ManufacturerGroups)
        {
            mfgGroup.IsSelected = false;
            foreach (var cng in mfgGroup.CommercialNameGroups)
            {
                cng.IsSelected = false;
                foreach (var modelGroup in cng.ModelGroups)
                {
                    modelGroup.IsSelected = false;
                    foreach (var vehicle in modelGroup.Vehicles)
                    {
                        vehicle.IsSelected = false;
                    }
                }
            }
        }
        OnPropertyChanged(nameof(SelectedVehiclesCount));
    }

    [RelayCommand]
    private void SelectAllParts()
    {
        foreach (var part in FilteredParts)
        {
            part.IsSelected = true;
        }
        OnPropertyChanged(nameof(SelectedPartsCount));
    }

    [RelayCommand]
    private void DeselectAllParts()
    {
        foreach (var part in FilteredParts)
        {
            part.IsSelected = false;
        }
        OnPropertyChanged(nameof(SelectedPartsCount));
    }

    private async Task RefreshMappingCountsAsync()
    {
        var vehicleMappingCounts = await _dataService.LoadMappingCountsAsync();
        var partMappingCounts = await _dataService.LoadPartMappingCountsAsync();

        // Update only loaded vehicles
        foreach (var mfgGroup in ManufacturerGroups)
        {
            foreach (var cng in mfgGroup.CommercialNameGroups)
            {
                foreach (var modelGroup in cng.ModelGroups)
                {
                    foreach (var vehicle in modelGroup.Vehicles)
                    {
                        if (vehicleMappingCounts.TryGetValue(vehicle.VehicleTypeId, out var count))
                        {
                            vehicle.MappedPartsCount = count;
                            vehicle.MappingStatus = count > 0
                                ? (count >= 10 ? MappingStatus.Mapped : MappingStatus.PartiallyMapped)
                                : MappingStatus.Unmapped;
                        }
                        else
                        {
                            vehicle.MappedPartsCount = 0;
                            vehicle.MappingStatus = MappingStatus.Unmapped;
                        }
                    }
                }
            }
        }

        foreach (var part in AllParts)
        {
            if (partMappingCounts.TryGetValue(part.PartNumber, out var count))
            {
                part.MappedVehiclesCount = count;
                part.MappingStatus = count > 0
                    ? (count >= 10 ? MappingStatus.Mapped : MappingStatus.PartiallyMapped)
                    : MappingStatus.Unmapped;
            }
            else
            {
                part.MappedVehiclesCount = 0;
                part.MappingStatus = MappingStatus.Unmapped;
            }
        }

        await LoadStatisticsAsync();
        ApplyVehicleFilters();
        ApplyPartFilters();
    }

    private async Task LoadStatisticsAsync()
    {
        TotalVehicles = await _dataService.GetTotalVehiclesAsync();
        TotalParts = await _dataService.GetTotalPartsAsync();
        MappedVehicles = await _dataService.GetMappedVehiclesAsync();
        MappedParts = await _dataService.GetMappedPartsAsync();
    }

    [RelayCommand]
    private void ManageExcludedManufacturers()
    {
        // Gather all manufacturers with vehicle counts
        var manufacturerData = ManufacturerGroups
            .Select(g => (
                ShortName: g.ManufacturerShortName,
                FullName: g.ManufacturerName,
                VehicleCount: g.TotalVehicleCount
            ))
            .ToList();

        var dialog = new Views.ManufacturerExclusionDialog(
            manufacturerData,
            _appSettings.ExcludedManufacturers);

        dialog.Owner = Application.Current.MainWindow;

        if (dialog.ShowDialog() == true)
        {
            _appSettings.ExcludedManufacturers = dialog.ExcludedManufacturers;
            _settingsService.SaveSettings(_appSettings);

            // Refresh the filters
            ApplyVehicleFilters();

            StatusMessage = $"Excluded {_appSettings.ExcludedManufacturers.Count} manufacturer(s)";
        }
    }
}
