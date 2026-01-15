using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Helpers;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using Sh.Autofit.New.PartsMappingUI.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using static Sh.Autofit.New.PartsMappingUI.Helpers.VehicleMatchingHelper;
using SelectableConsolidatedModel = Sh.Autofit.New.PartsMappingUI.Views.SelectableConsolidatedModel;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class PartMappingsManagementViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly IVirtualPartService _virtualPartService;

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

    // Consolidated models for the selected part (NEW WAY) - wrapped in SelectableConsolidatedModel for checkbox support
    [ObservableProperty]
    private ObservableCollection<SelectableConsolidatedModel> _consolidatedModels = new();

    [ObservableProperty]
    private bool _selectAllConsolidatedModels;

    [ObservableProperty]
    private string _partSearchText = string.Empty;

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<string> _availableCategories = new();

    [ObservableProperty]
    private bool _showOnlyVirtual = false;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public PartMappingsManagementViewModel(IDataService dataService, IVirtualPartService virtualPartService)
    {
        _dataService = dataService;
        _virtualPartService = virtualPartService;
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

    partial void OnShowOnlyVirtualChanged(bool value)
    {
        ApplyFilters();
    }

    partial void OnSelectAllConsolidatedModelsChanged(bool value)
    {
        foreach (var model in ConsolidatedModels)
        {
            model.IsSelected = value;
        }
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
                // OEM search with normalization (ignores special characters like ., -, /, spaces)
                (p.OemNumbers != null && p.OemNumbers.Any(oem =>
                    Helpers.OemSearchHelper.OemContains(oem, PartSearchText))));
        }

        // Apply category filter
        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "הכל")
        {
            filtered = filtered.Where(p => p.Category == SelectedCategory);
        }

        // Apply virtual parts filter
        if (ShowOnlyVirtual)
        {
            filtered = filtered.Where(p => p.IsVirtual);
        }

        FilteredParts = new ObservableCollection<PartDisplayModel>(filtered);
    }

    partial void OnSelectedPartChanged(PartDisplayModel? value)
    {
        if (value != null)
        {
            // Load data sequentially to prevent UI hang
            _ = LoadDataSequentiallyAsync(value.PartNumber);
        }
        else
        {
            MappedVehicles.Clear();
            SuggestedVehicles.Clear();
            ConsolidatedModels.Clear();
        }
    }

    private async Task LoadDataSequentiallyAsync(string partNumber)
    {
        try
        {
            IsLoading = true;

            // Load critical data first (mapped vehicles)
            await LoadMappedVehiclesAsync(partNumber);

            // Load less critical data in parallel (consolidated models and suggestions)
            var consolidatedTask = LoadConsolidatedModelsAsync(partNumber);
            var suggestionsTask = LoadSuggestedVehiclesAsync(partNumber);

            await Task.WhenAll(consolidatedTask, suggestionsTask);
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה בטעינת נתונים: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadConsolidatedModelsAsync(string partNumber)
    {
        try
        {
            var models = await _dataService.LoadConsolidatedModelsForPartAsync(partNumber, includeCouplings: true);
            ConsolidatedModels.Clear();
            SelectAllConsolidatedModels = false; // Reset select all

            foreach (var model in models)
            {
                ConsolidatedModels.Add(new SelectableConsolidatedModel(model));
            }
        }
        catch
        {
            ConsolidatedModels.Clear();
        }
    }

    private async Task LoadMappedVehiclesAsync(string partNumber)
    {
        try
        {
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
                StatusMessage = "ממפה חלק למודלים מאוחדים...";

                // NEW WAY: Get unique consolidated models for selected vehicles
                var consolidatedModelIds = new HashSet<int>();
                foreach (var vehicle in dialog.SelectedVehicles)
                {
                    var consolidatedModel = await _dataService.GetConsolidatedModelForVehicleTypeAsync(vehicle.VehicleTypeId);
                    if (consolidatedModel != null)
                    {
                        consolidatedModelIds.Add(consolidatedModel.ConsolidatedModelId);
                    }
                }

                if (consolidatedModelIds.Any())
                {
                    // Map to consolidated models (NEW WAY)
                    foreach (var modelId in consolidatedModelIds)
                    {
                        await _dataService.MapPartsToConsolidatedModelAsync(
                            modelId,
                            new List<string> { SelectedPart.PartNumber },
                            "current_user");
                    }
                    StatusMessage = $"✓ המיפוי בוצע ל-{consolidatedModelIds.Count} מודלים מאוחדים";
                }
                else
                {
                    // FALLBACK: Legacy approach for vehicles without consolidated models
                    var vehicleIds = dialog.SelectedVehicles.Select(v => v.VehicleTypeId).ToList();
                    await _dataService.MapPartsToVehiclesAsync(vehicleIds, new List<string> { SelectedPart.PartNumber }, "current_user");
                    StatusMessage = $"✓ המיפוי בוצע ל-{vehicleIds.Count} רכבים (legacy)";
                }

                await LoadMappedVehiclesAsync(SelectedPart.PartNumber);
                await LoadConsolidatedModelsAsync(SelectedPart.PartNumber);
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
    private async Task AddConsolidatedModelMappingsAsync()
    {
        if (SelectedPart == null)
        {
            MessageBox.Show("אנא בחר חלק", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "טוען דגמים מאוחדים זמינים...";

            // Load unmapped consolidated models
            var unmappedModels = await _dataService.LoadUnmappedConsolidatedModelsForPartAsync(SelectedPart.PartNumber);

            if (!unmappedModels.Any())
            {
                MessageBox.Show("כל הדגמים המאוחדים כבר ממופים לחלק זה", "מידע", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsLoading = false;

            // Show dialog to select consolidated models
            var dialog = new Views.SelectConsolidatedModelsDialog(unmappedModels);
            if (dialog.ShowDialog() == true && dialog.SelectedModels.Any())
            {
                try
                {
                    IsLoading = true;
                    StatusMessage = "ממפה חלק למודלים מאוחדים...";

                    var partNumbers = new List<string> { SelectedPart.PartNumber };

                    foreach (var model in dialog.SelectedModels)
                    {
                        await _dataService.MapPartsToConsolidatedModelAsync(
                            model.ConsolidatedModelId,
                            partNumbers,
                            "current_user");
                    }

                    StatusMessage = $"✓ מופה ל-{dialog.SelectedModels.Count} דגמים מאוחדים";

                    await LoadMappedVehiclesAsync(SelectedPart.PartNumber);
                    await LoadConsolidatedModelsAsync(SelectedPart.PartNumber);
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

    [RelayCommand]
    private async Task RemoveVehicleMappingAsync(VehicleDisplayModel? vehicle)
    {
        if (vehicle == null || SelectedPart == null)
            return;

        var variantDescription = GetVariantDescription(vehicle);

        // NEW WAY: Try to get consolidated model for this vehicle
        var consolidatedModel = await _dataService.GetConsolidatedModelForVehicleTypeAsync(vehicle.VehicleTypeId);

        string message;
        if (consolidatedModel != null)
        {
            // Check if this model has active couplings
            var couplings = await _dataService.GetModelCouplingsAsync(consolidatedModel.ConsolidatedModelId);
            var activeCouplings = couplings.Where(c => c.IsActive).ToList();

            if (activeCouplings.Any())
            {
                // Get coupled model names for display
                var coupledModelNames = new List<string>();
                foreach (var coupling in activeCouplings)
                {
                    var otherModelId = coupling.ConsolidatedModelIdA == consolidatedModel.ConsolidatedModelId
                        ? coupling.ConsolidatedModelIdB
                        : coupling.ConsolidatedModelIdA;

                    var otherModel = await _dataService.GetConsolidatedModelByIdAsync(otherModelId);
                    if (otherModel != null)
                    {
                        coupledModelNames.Add($"{otherModel.Manufacturer?.ManufacturerShortName} {otherModel.ModelName}");
                    }
                }

                var coupledModelsText = string.Join(", ", coupledModelNames);

                message = $"הדגם '{consolidatedModel.Manufacturer?.ManufacturerShortName} {consolidatedModel.ModelName}' מצומד עם:\n{coupledModelsText}\n\n" +
                         $"האם להסיר את החלק '{SelectedPart.PartName}' מדגם זה ומכל הדגמים המצומדים אליו?\n\n" +
                         "לחץ 'כן' להסרה מכולם, 'לא' לביטול.\n\n" +
                         "💡 טיפ: אם ברצונך להסיר את הצימוד ולנהל כל דגם בנפרד, השתמש בכפתור 'שבור צימוד' בניהול צימודים.";
            }
            else
            {
                message = $"האם להסיר את '{SelectedPart.PartName}' מהמודל המאוחד '{consolidatedModel.ModelName}'?\n\n" +
                         $"שנים: {consolidatedModel.YearFrom}-{consolidatedModel.YearTo}\n\n" +
                         "פעולה זו תסיר את החלק מכל השנים במודל המאוחד.";
            }
        }
        else
        {
            message = $"האם להסיר את '{SelectedPart.PartName}' מהרכב '{vehicle.ModelName}'?\n\n" +
                     $"הווריאנט: {variantDescription}";
        }

        var result = MessageBox.Show(
            message,
            "אישור הסרת מיפוי",
            MessageBoxButton.YesNo,
            consolidatedModel != null && (await _dataService.GetModelCouplingsAsync(consolidatedModel.ConsolidatedModelId)).Any(c => c.IsActive)
                ? MessageBoxImage.Warning
                : MessageBoxImage.Question
        );

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;

            if (consolidatedModel != null)
            {
                // Check for couplings again
                var couplings = await _dataService.GetModelCouplingsAsync(consolidatedModel.ConsolidatedModelId);
                var activeCouplings = couplings.Where(c => c.IsActive).ToList();

                if (activeCouplings.Any())
                {
                    // Unmap from this model AND all coupled models
                    StatusMessage = "מסיר מיפוי מהדגם ומכל הדגמים המצומדים...";

                    var modelsToUnmapFrom = new List<int> { consolidatedModel.ConsolidatedModelId };
                    foreach (var coupling in activeCouplings)
                    {
                        var otherModelId = coupling.ConsolidatedModelIdA == consolidatedModel.ConsolidatedModelId
                            ? coupling.ConsolidatedModelIdB
                            : coupling.ConsolidatedModelIdA;
                        modelsToUnmapFrom.Add(otherModelId);
                    }

                    foreach (var modelId in modelsToUnmapFrom)
                    {
                        await _dataService.UnmapPartsFromConsolidatedModelAsync(
                            modelId,
                            new List<string> { SelectedPart.PartNumber },
                            "current_user");
                    }

                    StatusMessage = $"המיפוי הוסר מ-{modelsToUnmapFrom.Count} דגמים מצומדים";
                }
                else
                {
                    // NEW WAY: Unmap from consolidated model (no couplings)
                    StatusMessage = "מסיר מיפוי ממודל מאוחד...";
                    await _dataService.UnmapPartsFromConsolidatedModelAsync(
                        consolidatedModel.ConsolidatedModelId,
                        new List<string> { SelectedPart.PartNumber },
                        "current_user"
                    );
                    StatusMessage = $"המיפוי הוסר ממודל מאוחד {consolidatedModel.ModelName}";
                }
            }
            else
            {
                // FALLBACK: Legacy approach - unmap from specific vehicles
                StatusMessage = "מסיר מיפוי (legacy)...";

                var allVehicles = await _dataService.LoadVehiclesAsync();
                var sameVariantVehicles = GetSameVariantVehicles(allVehicles, vehicle);
                var vehicleIds = sameVariantVehicles.Select(v => v.VehicleTypeId).ToList();

                await _dataService.UnmapPartsFromVehiclesAsync(
                    vehicleIds,
                    new List<string> { SelectedPart.PartNumber },
                    "current_user"
                );
                StatusMessage = $"המיפוי הוסר מ-{vehicleIds.Count} רכבים (legacy)";
            }

            await LoadMappedVehiclesAsync(SelectedPart.PartNumber);
            await LoadConsolidatedModelsAsync(SelectedPart.PartNumber);
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
    private async Task RemoveConsolidatedModelMappingAsync(SelectableConsolidatedModel? selectableModel)
    {
        if (selectableModel == null || SelectedPart == null)
            return;

        var model = selectableModel.Model;

        try
        {
            IsLoading = true;

            // Check for active couplings
            var couplings = await _dataService.GetModelCouplingsAsync(model.ConsolidatedModelId);
            var activeCouplings = couplings.Where(c => c.IsActive).ToList();

            // Build confirmation message
            string message;
            if (activeCouplings.Any())
            {
                // Get coupled model names
                var coupledModelNames = new List<string>();
                foreach (var coupling in activeCouplings)
                {
                    var otherModelId = coupling.ConsolidatedModelIdA == model.ConsolidatedModelId
                        ? coupling.ConsolidatedModelIdB
                        : coupling.ConsolidatedModelIdA;

                    var otherModel = await _dataService.GetConsolidatedModelByIdAsync(otherModelId);
                    if (otherModel != null)
                    {
                        coupledModelNames.Add($"{otherModel.Manufacturer?.ManufacturerShortName} {otherModel.ModelName}");
                    }
                }

                var coupledModelsText = string.Join(", ", coupledModelNames);
                message = $"הדגם '{model.ModelName}' מצומד עם: {coupledModelsText}\n\n" +
                         $"האם להסיר '{SelectedPart.PartName}' מדגם זה ומכל הדגמים המצומדים?\n\n" +
                         "לחץ 'כן' להסרה מכולם, 'לא' לביטול.\n\n" +
                         "💡 טיפ: השתמש בכפתור 'נתק צימוד' בניהול צימודים לניתוק דגמים.";
            }
            else
            {
                var yearRange = model.YearTo.HasValue
                    ? $"{model.YearFrom}-{model.YearTo}"
                    : $"{model.YearFrom}+";
                message = $"האם להסיר '{SelectedPart.PartName}' מהדגם המאוחד '{model.ModelName}'?\n\n" +
                         $"שנים: {yearRange}\n\n" +
                         "פעולה זו מסירה את החלק מכל השנים בדגם המאוחד.";
            }

            var result = MessageBox.Show(message, "אישור הסרה",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            StatusMessage = "מסיר מיפוי מדגם מאוחד...";
            var partNumbers = new List<string> { SelectedPart.PartNumber };

            if (activeCouplings.Any())
            {
                // Unmap from all coupled models
                var allModelIds = new HashSet<int> { model.ConsolidatedModelId };
                foreach (var coupling in activeCouplings)
                {
                    allModelIds.Add(coupling.ConsolidatedModelIdA);
                    allModelIds.Add(coupling.ConsolidatedModelIdB);
                }

                foreach (var modelId in allModelIds)
                {
                    await _dataService.UnmapPartsFromConsolidatedModelAsync(modelId, partNumbers, "current_user");
                }

                StatusMessage = $"✓ הוסר מ-{allModelIds.Count} דגמים מצומדים";
            }
            else
            {
                // Unmap from single model
                await _dataService.UnmapPartsFromConsolidatedModelAsync(
                    model.ConsolidatedModelId, partNumbers, "current_user");

                StatusMessage = $"✓ הוסר מהדגם המאוחד {model.ModelName}";
            }

            await LoadMappedVehiclesAsync(SelectedPart.PartNumber);
            await LoadConsolidatedModelsAsync(SelectedPart.PartNumber);
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה בהסרת מיפוי: {ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UnmapSelectedConsolidatedModelsAsync()
    {
        if (SelectedPart == null)
            return;

        var selectedModels = ConsolidatedModels.Where(m => m.IsSelected).ToList();

        if (!selectedModels.Any())
        {
            MessageBox.Show("אנא בחר לפחות דגם אחד להסרה", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Build confirmation message with truncation for large selections
        string modelsList;
        const int maxDisplayModels = 10;

        if (selectedModels.Count <= maxDisplayModels)
        {
            var modelNames = selectedModels.Select(m =>
                $"{m.Manufacturer?.ManufacturerShortName} {m.ModelName}").ToList();
            modelsList = string.Join("\n• ", modelNames);
        }
        else
        {
            var firstModels = selectedModels.Take(maxDisplayModels).Select(m =>
                $"{m.Manufacturer?.ManufacturerShortName} {m.ModelName}").ToList();
            modelsList = string.Join("\n• ", firstModels) + $"\n• ... ועוד {selectedModels.Count - maxDisplayModels} דגמים נוספים";
        }

        var message = $"האם להסיר את '{SelectedPart.PartName}' מהדגמים המאוחדים הבאים?\n\n• {modelsList}\n\n" +
                     $"סה\"כ {selectedModels.Count} דגמים ייהסרו.\n\n" +
                     "פעולה זו תסיר את החלק מכל השנים והדגמים המצומדים.";

        var result = MessageBox.Show(message, "אישור הסרה המונית",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"מסיר מיפוי מ-{selectedModels.Count} דגמים מאוחדים...";

            var partNumbers = new List<string> { SelectedPart.PartNumber };
            var allModelsToUnmapFrom = new HashSet<int>();

            // For each selected model, check for couplings and add all coupled models
            foreach (var selectableModel in selectedModels)
            {
                var model = selectableModel.Model;
                allModelsToUnmapFrom.Add(model.ConsolidatedModelId);

                // Check for couplings
                var couplings = await _dataService.GetModelCouplingsAsync(model.ConsolidatedModelId);
                var activeCouplings = couplings.Where(c => c.IsActive).ToList();

                foreach (var coupling in activeCouplings)
                {
                    allModelsToUnmapFrom.Add(coupling.ConsolidatedModelIdA);
                    allModelsToUnmapFrom.Add(coupling.ConsolidatedModelIdB);
                }
            }

            // Unmap from all models (including coupled ones)
            foreach (var modelId in allModelsToUnmapFrom)
            {
                await _dataService.UnmapPartsFromConsolidatedModelAsync(modelId, partNumbers, "current_user");
            }

            StatusMessage = $"✓ הוסר מ-{allModelsToUnmapFrom.Count} דגמים (כולל דגמים מצומדים)";

            MessageBox.Show($"החלק הוסר בהצלחה מ-{allModelsToUnmapFrom.Count} דגמים מאוחדים",
                "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);

            await LoadMappedVehiclesAsync(SelectedPart.PartNumber);
            await LoadConsolidatedModelsAsync(SelectedPart.PartNumber);
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה בהסרת מיפוי המוני: {ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
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

        try
        {
            IsLoading = true;

            // NEW WAY: Get consolidated model for the suggested vehicle
            var consolidatedModel = await _dataService.GetConsolidatedModelForVehicleTypeAsync(vehicle.VehicleTypeId);

            if (consolidatedModel != null)
            {
                // Map to consolidated model (covers all years)
                StatusMessage = $"ממפה למודל מאוחד {consolidatedModel.ModelName}...";
                await _dataService.MapPartsToConsolidatedModelAsync(
                    consolidatedModel.ConsolidatedModelId,
                    new List<string> { SelectedPart.PartNumber },
                    "current_user"
                );
                StatusMessage = $"✓ ההצעה אושרה - מופה למודל מאוחד {consolidatedModel.ModelName} (שנים {consolidatedModel.YearFrom}-{consolidatedModel.YearTo})";
            }
            else
            {
                // FALLBACK: Legacy approach - map to all variants of this model
                StatusMessage = "ממפה לכל הווריאנטים (legacy)...";

                var allVehicles = await _dataService.LoadVehiclesAsync();
                var vehicleIds = allVehicles
                    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(vehicle.ManufacturerName) &&
                               v.ModelName.EqualsIgnoringWhitespace(vehicle.ModelName))
                    .Select(v => v.VehicleTypeId)
                    .ToList();

                await _dataService.MapPartsToVehiclesAsync(
                    vehicleIds,
                    new List<string> { SelectedPart.PartNumber },
                    "current_user"
                );
                StatusMessage = $"✓ ההצעה אושרה - מופה ל-{vehicleIds.Count} רכבים (legacy)";
            }

            // Reload all lists
            await LoadMappedVehiclesAsync(SelectedPart.PartNumber);
            await LoadSuggestedVehiclesAsync(SelectedPart.PartNumber);
            await LoadConsolidatedModelsAsync(SelectedPart.PartNumber);
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

    [RelayCommand]
    private async Task EditVirtualPartAsync()
    {
        if (SelectedPart == null || !SelectedPart.IsVirtual)
            return;

        // Get available categories
        var categories = AllParts
            .Where(p => !string.IsNullOrWhiteSpace(p.Category))
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var virtualPart = await _virtualPartService.GetVirtualPartByPartNumberAsync(
            SelectedPart.PartNumber);

        if (virtualPart == null)
        {
            MessageBox.Show("החלק הוירטואלי לא נמצא", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new EditVirtualPartDialog(
            _virtualPartService,
            virtualPart,
            categories);

        if (dialog.ShowDialog() == true && dialog.WasUpdated)
        {
            // Reload parts
            await LoadPartsAsync();

            // Reselect the part
            SelectedPart = FilteredParts.FirstOrDefault(p =>
                p.PartNumber == virtualPart.PartNumber);

            StatusMessage = "✓ החלק הוירטואלי עודכן בהצלחה";
        }
    }

    [RelayCommand]
    private async Task DeleteVirtualPartAsync()
    {
        if (SelectedPart == null || !SelectedPart.IsVirtual)
            return;

        var result = MessageBox.Show(
            $"האם אתה בטוח שברצונך למחוק את החלק הוירטואלי '{SelectedPart.PartNumber}'?\n\n" +
            $"שם: {SelectedPart.PartName}\n\n" +
            "⚠️ פעולה זו תמחק את החלק הוירטואלי וכל המיפויים שלו לצמיתות!\n\n" +
            "פעולה זו בלתי הפיכה.",
            "אישור מחיקת חלק וירטואלי",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "מוחק חלק וירטואלי...";

            // Get virtual part ID from part number
            var virtualPart = await _virtualPartService.GetVirtualPartByPartNumberAsync(SelectedPart.PartNumber);

            if (virtualPart != null)
            {
                await _virtualPartService.DeleteVirtualPartAsync(virtualPart.VirtualPartId);

                StatusMessage = $"✓ החלק הוירטואלי '{SelectedPart.PartNumber}' נמחק בהצלחה";

                MessageBox.Show(
                    $"החלק הוירטואלי '{SelectedPart.PartNumber}' נמחק בהצלחה.",
                    "הצלחה",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                // Reload parts list
                await LoadPartsAsync();

                // Clear selection
                SelectedPart = null;
            }
            else
            {
                MessageBox.Show(
                    "החלק הוירטואלי לא נמצא במערכת.",
                    "שגיאה",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show(
                $"שגיאה במחיקת החלק הוירטואלי:\n\n{ex.Message}",
                "שגיאה",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        finally
        {
            IsLoading = false;
        }
    }
}
