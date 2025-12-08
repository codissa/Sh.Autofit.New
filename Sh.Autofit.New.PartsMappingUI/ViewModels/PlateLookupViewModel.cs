using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Helpers;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using static Sh.Autofit.New.PartsMappingUI.Helpers.VehicleMatchingHelper;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class PlateLookupViewModel : ObservableObject
{
    private readonly IGovernmentApiService _governmentApiService;
    private readonly IVehicleMatchingService _vehicleMatchingService;
    private readonly IDataService _dataService;
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

    [ObservableProperty]
    private string _plateNumber = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickMapCommand), nameof(ViewAllMatchesCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ViewAllMatchesCommand))]
    private GovernmentVehicleRecord? _governmentVehicle;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickMapCommand))]
    private VehicleDisplayModel? _matchedVehicle;

    // Consolidated model for the matched vehicle (NEW WAY)
    [ObservableProperty]
    private ConsolidatedVehicleModel? _consolidatedModel;

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _mappedParts = new();

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _suggestedParts = new();

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _showCopiedPopup;

    [ObservableProperty]
    private bool _isOffRoad;

    [ObservableProperty]
    private bool _isPersonalImport;

    public string CleanVinNumber => CleanVin(GovernmentVehicle?.VinNumber ?? GovernmentVehicle?.VinChassis);

    // Display engine volume - use government API if available, otherwise use matched vehicle
    public int? DisplayEngineVolume
    {
        get
        {
            // Priority 1: Government API data
            if (GovernmentVehicle?.EngineVolume.HasValue == true && GovernmentVehicle.EngineVolume.Value > 0)
                return GovernmentVehicle.EngineVolume.Value;

            // Priority 2: Matched vehicle data
            if (MatchedVehicle?.EngineVolume.HasValue == true && MatchedVehicle.EngineVolume.Value > 0)
                return MatchedVehicle.EngineVolume.Value;

            // Priority 3: Consolidated model data
            if (ConsolidatedModel?.EngineVolume.HasValue == true && ConsolidatedModel.EngineVolume.Value > 0)
                return ConsolidatedModel.EngineVolume.Value;

            return null;
        }
    }

    // Display year range from consolidated model (or manufacturing year as fallback)
    public string DisplayYearRange
    {
        get
        {
            // Priority 1: Consolidated model year range
            if (ConsolidatedModel != null)
            {
                var yearFrom = ConsolidatedModel.YearFrom;
                var yearTo = ConsolidatedModel.YearTo;

                if (yearTo.HasValue)
                {
                    if (yearFrom == yearTo.Value)
                        return $"{yearFrom}"; // Single year
                    else
                        return $"{yearFrom}-{yearTo}"; // Range
                }
                else
                {
                    return $"{yearFrom}+"; // Open-ended range
                }
            }

            // Priority 2: Government vehicle manufacturing year
            if (GovernmentVehicle?.ManufacturingYear.HasValue == true)
                return GovernmentVehicle.ManufacturingYear.Value.ToString();

            // Priority 3: Matched vehicle year
            if (MatchedVehicle?.YearFrom > 0)
            {
                if (MatchedVehicle.YearTo.HasValue && MatchedVehicle.YearTo.Value > 0)
                {
                    if (MatchedVehicle.YearFrom == MatchedVehicle.YearTo.Value)
                        return $"{MatchedVehicle.YearFrom}";
                    else
                        return $"{MatchedVehicle.YearFrom}-{MatchedVehicle.YearTo}";
                }
                else
                {
                    return $"{MatchedVehicle.YearFrom}+";
                }
            }

            return "לא ידוע";
        }
    }

    private string CleanVin(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
            return string.Empty;

        // Remove all non-alphanumeric characters
        return new string(vin.Where(char.IsLetterOrDigit).ToArray());
    }

    public PlateLookupViewModel(
        IGovernmentApiService governmentApiService,
        IVehicleMatchingService vehicleMatchingService,
        IDataService dataService,
        IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _governmentApiService = governmentApiService;
        _vehicleMatchingService = vehicleMatchingService;
        _dataService = dataService;
        _contextFactory = contextFactory;
    }

    [RelayCommand]
    private async Task SearchPlateAsync()
    {
        if (string.IsNullOrWhiteSpace(PlateNumber))
        {
            StatusMessage = "נא להזין מספר רישוי";
            return;
        }

        try
        {
            IsLoading = true;
            HasResults = false;
            GovernmentVehicle = null;
            MatchedVehicle = null;
            ConsolidatedModel = null;
            MappedParts.Clear();
            IsOffRoad = false;
            IsPersonalImport = false;

            // Step 0: Check cache first! (Task 5 Performance Enhancement)
            StatusMessage = "בודק מטמון...";
            var cachedRegistration = await _dataService.GetCachedRegistrationAsync(PlateNumber);

            if (cachedRegistration != null)
            {
                StatusMessage = $"✓ נמצא במטמון! (חיפוש #{cachedRegistration.LookupCount + 1})";

                // Use cached data - much faster than Gov API!
                if (cachedRegistration.VehicleTypeId.HasValue)
                {
                    // Load the matched vehicle from our database
                    var vehicles = await _dataService.LoadVehiclesByModelAsync(
                        cachedRegistration.VehicleType?.Manufacturer?.ManufacturerCode ?? 0,
                        cachedRegistration.VehicleType?.CommercialName ?? "",
                        cachedRegistration.VehicleType?.ModelName ?? "");

                    MatchedVehicle = vehicles.FirstOrDefault(v => v.VehicleTypeId == cachedRegistration.VehicleTypeId.Value);

                    if (MatchedVehicle != null)
                    {
                        // Get consolidated model for this vehicle
                        ConsolidatedModel = await _dataService.GetConsolidatedModelForVehicleTypeAsync(cachedRegistration.VehicleTypeId.Value);

                        // Reconstruct GovernmentVehicle from cached data for display
                        GovernmentVehicle = new GovernmentVehicleRecord
                        {
                            ManufacturerName = cachedRegistration.GovManufacturerName,
                            ModelName = cachedRegistration.GovModelName,
                            EngineVolume = cachedRegistration.GovEngineVolume,
                            FuelType = cachedRegistration.GovFuelType,
                            ManufacturingYear = cachedRegistration.GovYear,
                            ColorName = cachedRegistration.Color,
                            OwnershipType = cachedRegistration.CurrentOwner,
                            VinNumber = cachedRegistration.Vin
                        };
                        OnPropertyChanged(nameof(CleanVinNumber));
                        OnPropertyChanged(nameof(DisplayEngineVolume));
                        OnPropertyChanged(nameof(DisplayYearRange));

                        // Load mapped parts using consolidated model approach
                        StatusMessage = "טוען חלקים ממופים...";
                        List<PartDisplayModel> cachedParts;

                        if (ConsolidatedModel != null)
                        {
                            // Load from consolidated model (includes couplings)
                            cachedParts = await _dataService.LoadMappedPartsForConsolidatedModelAsync(
                                ConsolidatedModel.ConsolidatedModelId,
                                includeCouplings: true);
                        }
                        else
                        {
                            // Fallback to legacy approach by model name
                            cachedParts = await _dataService.LoadMappedPartsByModelNameAsync(
                                cachedRegistration.GovManufacturerName ?? "",
                                cachedRegistration.GovModelName ?? "");
                        }

                        MappedParts.Clear();
                        foreach (var part in cachedParts)
                        {
                            MappedParts.Add(part);
                        }

                        // Load suggestions
                        if (cachedRegistration.VehicleTypeId.HasValue)
                        {
                            try
                            {
                                var suggestions = await _dataService.GetSuggestedPartsForVehicleAsync(cachedRegistration.VehicleTypeId.Value);
                                SuggestedParts.Clear();
                                foreach (var suggestion in suggestions)
                                {
                                    SuggestedParts.Add(suggestion);
                                }
                            }
                            catch
                            {
                                SuggestedParts.Clear();
                            }
                        }

                        // Update cache stats (increment lookup count)
                        await _dataService.UpsertVehicleRegistrationAsync(
                            PlateNumber,
                            GovernmentVehicle,
                            cachedRegistration.VehicleTypeId,
                            cachedRegistration.ManufacturerId,
                            cachedRegistration.MatchStatus ?? "Matched",
                            cachedRegistration.MatchReason ?? "Loaded from cache",
                            cachedRegistration.ApiResourceUsed ?? "Cache");

                        StatusMessage = $"✓ נמצאו {MappedParts.Count} חלקים (מהמטמון)";
                        HasResults = true;
                        return; // Skip Gov API call entirely!
                    }
                }
                else if (cachedRegistration.MatchStatus == "NotFoundInGovAPI")
                {
                    // This plate was previously not found, but let's try again
                    // Government API might have been updated since last search
                    StatusMessage = $"רכב לא נמצא בעבר (חיפוש #{cachedRegistration.LookupCount + 1}) - מנסה שוב...";

                    // Don't return - continue to Gov API search below
                }
            }

            // Cache miss or outdated - proceed with Gov API call
            string matchStatus = "Pending";
            string matchReason = "";
            string apiResource = "Unknown";

            StatusMessage = "מחפש ברשומות משרד התחבורה...";

            // Step 1: Lookup vehicle from government API (tries all fallback sources automatically)
            var govVehicle = await _governmentApiService.LookupVehicleByPlateAsync(PlateNumber);

            if (govVehicle == null)
            {
                StatusMessage = "רכב לא נמצא במאגר משרד התחבורה";
                matchStatus = "NotFoundInGovAPI";
                matchReason = "Vehicle not found in any government API resource";

                // Cache the failed lookup (Task 5)
                await _dataService.UpsertVehicleRegistrationAsync(
                    PlateNumber,
                    null,
                    null,
                    null,
                    matchStatus,
                    matchReason,
                    apiResource);

                return;
            }

            GovernmentVehicle = govVehicle;
            OnPropertyChanged(nameof(CleanVinNumber));
            OnPropertyChanged(nameof(DisplayEngineVolume));
            OnPropertyChanged(nameof(DisplayYearRange));

            // Check special vehicle statuses (in parallel)
            var offRoadTask = _governmentApiService.IsVehicleOffRoadAsync(PlateNumber);
            var personalImportTask = _governmentApiService.IsPersonalImportAsync(PlateNumber);

            await Task.WhenAll(offRoadTask, personalImportTask);

            IsOffRoad = offRoadTask.Result;
            IsPersonalImport = personalImportTask.Result;

            StatusMessage = "רכב נמצא, מחפש התאמה מדויקת במערכת...";
            apiResource = "Primary"; // You can enhance GovernmentApiService to return which resource was used

            // Step 2: Find matching vehicle in our database - use EXACT matching
            // First, load all potential matches by manufacturer code and model name
            var potentialMatches = await _dataService.LoadVehiclesByModelAsync(
                govVehicle.ManufacturerCode ?? 0,
                govVehicle.CommercialName ?? "",
                govVehicle.ModelName ?? "",
                govVehicle.EngineVolume,
                govVehicle.ModelCode);

            // Then filter to exact matches based on engine volume, fuel type, trim level, etc.
            var exactMatches = VehicleMatchingHelper.GetExactMatches(potentialMatches, govVehicle);

            VehicleDisplayModel? matchedVehicle = null;
            if (exactMatches.Any())
            {
                // Prefer exact match
                matchedVehicle = exactMatches.First();
                StatusMessage = "נמצאה התאמה מדויקת!";
            }
            else
            {
                // Fallback to service matching (more lenient)
                matchedVehicle = await _vehicleMatchingService.FindMatchingVehicleTypeAsync(govVehicle);
            }

            int? matchedVehicleTypeId = matchedVehicle?.VehicleTypeId;
            int? matchedManufacturerId = matchedVehicle?.ManufacturerId;

            // Step 2.5: If not found, auto-create a new vehicle type
            if (matchedVehicle == null)
            {
                StatusMessage = "לא נמצאה התאמה במערכת, יוצר רכב חדש...";
                matchedVehicle = await _dataService.CreateVehicleTypeFromGovernmentRecordAsync(govVehicle);
                matchedVehicleTypeId = matchedVehicle?.VehicleTypeId;
                matchedManufacturerId = matchedVehicle?.ManufacturerId;
                matchStatus = "AutoCreated";
                matchReason = "Vehicle auto-created from government record";
                StatusMessage = "רכב חדש נוצר במערכת!";
            }
            else
            {
                matchStatus = "Matched";
                matchReason = $"Matched to existing vehicle: {matchedVehicle.ManufacturerName} {matchedVehicle.ModelName}";
                StatusMessage = "נמצאה התאמה במערכת!";
            }

            MatchedVehicle = matchedVehicle;
            OnPropertyChanged(nameof(DisplayEngineVolume));
            OnPropertyChanged(nameof(DisplayYearRange));

            // Step 2.8: Get consolidated model for this vehicle (NEW WAY)
            if (matchedVehicleTypeId.HasValue)
            {
                ConsolidatedModel = await _dataService.GetConsolidatedModelForVehicleTypeAsync(matchedVehicleTypeId.Value);
                OnPropertyChanged(nameof(DisplayEngineVolume));
                OnPropertyChanged(nameof(DisplayYearRange));
            }

            // Step 2.9: Cache the lookup result (Task 5)
            await _dataService.UpsertVehicleRegistrationAsync(
                PlateNumber,
                govVehicle,
                matchedVehicleTypeId,
                matchedManufacturerId,
                matchStatus,
                matchReason,
                apiResource);

            // Step 3: Load mapped parts - prioritize consolidated model if available
            StatusMessage = "טוען חלקים ממופים לדגם...";
            List<PartDisplayModel> parts;

            if (ConsolidatedModel != null)
            {
                // NEW WAY: Load from consolidated model (includes couplings)
                parts = await _dataService.LoadMappedPartsForConsolidatedModelAsync(ConsolidatedModel.ConsolidatedModelId, includeCouplings: true);
            }
            else
            {
                // FALLBACK: Legacy approach by model name
                parts = await _dataService.LoadMappedPartsByModelNameAsync(
                    govVehicle.ManufacturerName ?? "",
                    govVehicle.ModelName ?? "");
            }

            MappedParts.Clear();
            foreach (var part in parts)
            {
                MappedParts.Add(part);
            }

            // Step 4: Load suggested parts
            if (matchedVehicleTypeId.HasValue)
            {
                try
                {
                    var suggestions = await _dataService.GetSuggestedPartsForVehicleAsync(matchedVehicleTypeId.Value);
                    SuggestedParts.Clear();
                    foreach (var suggestion in suggestions)
                    {
                        SuggestedParts.Add(suggestion);
                    }
                }
                catch
                {
                    // Silently fail for suggestions
                    SuggestedParts.Clear();
                }
            }

            StatusMessage = $"נמצאו {MappedParts.Count} חלקים ממופים" +
                          (SuggestedParts.Count > 0 ? $" + {SuggestedParts.Count} הצעות" : "");
            HasResults = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"אירעה שגיאה בחיפוש: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanQuickMap))]
    private async Task QuickMapAsync()
    {
        if (MatchedVehicle == null)
            return;

        try
        {
            // Use QuickMapDialog with variant-aware mapping
            var dialog = new Views.QuickMapDialog(_dataService, MatchedVehicle.VehicleTypeId, MatchedVehicle);
            var result = dialog.ShowDialog();

            if (result == true)
            {
                // Refresh mapped parts list using consolidated model approach
                StatusMessage = "מעדכן רשימת חלקים...";
                await ReloadMappedPartsAsync();

                // Reload suggestions
                await ReloadSuggestionsAsync();

                StatusMessage = $"נמצאו {MappedParts.Count} חלקים ממופים לרכב זה";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"אירעה שגיאה במיפוי מהיר: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanQuickMap() => MatchedVehicle != null && !IsLoading;

    [RelayCommand]
    private void Clear()
    {
        PlateNumber = string.Empty;
        GovernmentVehicle = null;
        MatchedVehicle = null;
        ConsolidatedModel = null;
        MappedParts.Clear();
        SuggestedParts.Clear();
        StatusMessage = string.Empty;
        HasResults = false;
        ShowCopiedPopup = false;
        IsOffRoad = false;
        IsPersonalImport = false;
        OnPropertyChanged(nameof(CleanVinNumber));
    }

    [RelayCommand]
    private async Task CopyVinToClipboardAsync()
    {
        if (string.IsNullOrWhiteSpace(CleanVinNumber))
            return;

        try
        {
            Clipboard.SetText(CleanVinNumber);
            ShowCopiedPopup = true;

            // Hide popup after 2 seconds
            await Task.Delay(2000);
            ShowCopiedPopup = false;
        }
        catch (Exception)
        {
            // Ignore clipboard errors
        }
    }

    [RelayCommand(CanExecute = nameof(CanViewAllMatches))]
    private async Task ViewAllMatchesAsync()
    {
        if (GovernmentVehicle == null)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "מחפש כל ההתאמות האפשריות...";

            var possibleMatches = await _vehicleMatchingService.FindPossibleMatchesAsync(GovernmentVehicle);

            if (!possibleMatches.Any())
            {
                MessageBox.Show("לא נמצאו התאמות במערכת", "אין תוצאות", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Show dialog with all possible matches
            var dialog = new Views.PossibleMatchesDialog(possibleMatches);
            var result = dialog.ShowDialog();

            if (result == true && dialog.SelectedVehicle != null)
            {
                // User selected a different match
                MatchedVehicle = dialog.SelectedVehicle;

                // Reset ConsolidatedModel so it gets reloaded for the new vehicle
                ConsolidatedModel = null;

                StatusMessage = "טוען חלקים ממופים...";

                // Load parts using consolidated model approach
                await ReloadMappedPartsAsync();

                StatusMessage = $"נמצאו {MappedParts.Count} חלקים ממופים לרכב זה";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"אירעה שגיאה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanViewAllMatches() => GovernmentVehicle != null && !IsLoading;

    [RelayCommand]
    private async Task UnmapPartAsync(PartDisplayModel? part)
    {
        if (part == null || MatchedVehicle == null)
            return;

        // Check if this part comes from a coupled model/part
        if (part.MappingType == "CoupledModel")
        {
            MessageBox.Show(
                $"החלק '{part.PartName}' ממופה דרך דגם מצומד.\n\n" +
                "לא ניתן להסיר את המיפוי מכאן.\n" +
                "יש להיכנס לניהול צימודים ולהסיר את הצימוד בין הדגמים, או להסיר את המיפוי מהדגם המצומד עצמו.",
                "לא ניתן להסיר מיפוי",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (part.MappingType == "CoupledPart")
        {
            MessageBox.Show(
                $"החלק '{part.PartName}' ממופה דרך צימוד חלקים.\n\n" +
                "לא ניתן להסיר את המיפוי מכאן.\n" +
                "יש להיכנס לניהול צימודים ולהסיר את הצימוד בין החלקים.",
                "לא ניתן להסיר מיפוי",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            IsLoading = true;

            // NEW WAY: Use consolidated model if available (only for Direct mappings)
            if (ConsolidatedModel != null)
            {
                // Check if this model has active couplings
                var couplings = await _dataService.GetModelCouplingsAsync(ConsolidatedModel.ConsolidatedModelId);
                var activeCouplings = couplings.Where(c => c.IsActive).ToList();

                if (activeCouplings.Any())
                {
                    // Get coupled model names for display
                    var coupledModelNames = new List<string>();
                    foreach (var coupling in activeCouplings)
                    {
                        var otherModelId = coupling.ConsolidatedModelIdA == ConsolidatedModel.ConsolidatedModelId
                            ? coupling.ConsolidatedModelIdB
                            : coupling.ConsolidatedModelIdA;

                        var otherModel = await _dataService.GetConsolidatedModelByIdAsync(otherModelId);
                        if (otherModel != null)
                        {
                            coupledModelNames.Add($"{otherModel.Manufacturer?.ManufacturerShortName} {otherModel.ModelName}");
                        }
                    }

                    var coupledModelsText = string.Join(", ", coupledModelNames);

                    var confirmResult = MessageBox.Show(
                        $"הדגם '{ConsolidatedModel.Manufacturer?.ManufacturerShortName} {ConsolidatedModel.ModelName}' מצומד עם:\n{coupledModelsText}\n\n" +
                        $"האם להסיר את החלק '{part.PartName}' מדגם זה ומכל הדגמים המצומדים אליו?\n\n" +
                        "לחץ 'כן' להסרה מכולם, 'לא' לביטול.\n\n" +
                        "💡 טיפ: אם ברצונך להסיר את הצימוד ולנהל כל דגם בנפרד, השתמש בכפתור 'שבור צימוד' בניהול צימודים.",
                        "אישור הסרת מיפוי מדגמים מצומדים",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (confirmResult != MessageBoxResult.Yes)
                    {
                        IsLoading = false;
                        return;
                    }

                    // Unmap from this model AND all coupled models
                    StatusMessage = "מסיר מיפוי מהדגם ומכל הדגמים המצומדים...";

                    var modelsToUnmapFrom = new List<int> { ConsolidatedModel.ConsolidatedModelId };
                    foreach (var coupling in activeCouplings)
                    {
                        var otherModelId = coupling.ConsolidatedModelIdA == ConsolidatedModel.ConsolidatedModelId
                            ? coupling.ConsolidatedModelIdB
                            : coupling.ConsolidatedModelIdA;
                        modelsToUnmapFrom.Add(otherModelId);
                    }

                    foreach (var modelId in modelsToUnmapFrom)
                    {
                        await _dataService.UnmapPartsFromConsolidatedModelAsync(
                            modelId,
                            new List<string> { part.PartNumber },
                            "current_user");
                    }

                    StatusMessage = $"המיפוי של '{part.PartName}' הוסר מ-{modelsToUnmapFrom.Count} דגמים מצומדים";
                }
                else
                {
                    // No couplings - simple unmap
                    StatusMessage = "מסיר מיפוי מהדגם המאוחד...";

                    await _dataService.UnmapPartsFromConsolidatedModelAsync(
                        ConsolidatedModel.ConsolidatedModelId,
                        new List<string> { part.PartNumber },
                        "current_user");

                    var yearRange = ConsolidatedModel.YearTo.HasValue
                        ? $"{ConsolidatedModel.YearFrom}-{ConsolidatedModel.YearTo}"
                        : $"{ConsolidatedModel.YearFrom}+";
                    StatusMessage = $"המיפוי של '{part.PartName}' הוסר מהדגם המאוחד ({yearRange})";
                }
            }
            else
            {
                // FALLBACK: Legacy approach
                var variantDescription = GetVariantDescription(MatchedVehicle);

                // Ask user if they want to unmap from just this vehicle variant or all variants of the model
                var result = MessageBox.Show(
                    $"האם להסיר את '{part.PartName}' מכל הווריאנטים של '{GovernmentVehicle?.ModelName}'?\n\n" +
                    $"הווריאנט הנוכחי: {variantDescription}\n\n" +
                    "בחר 'כן' להסרה מכל הווריאנטים של הדגם (כל נפחי מנוע, תיבות הילוכים ורמות גימור),\n" +
                    "'לא' להסרה רק מהווריאנט המדויק הזה (כולל שנות ייצור שונות),\n" +
                    "או 'ביטול'.",
                    "אישור הסרת מיפוי",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Cancel)
                {
                    IsLoading = false;
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    // Unmap from ALL variants of this model (all engine volumes, transmissions, trims)
                    StatusMessage = "מסיר מיפוי מכל הווריאנטים של הדגם...";

                    // Get all vehicles for this model (only matching manufacturer and model name)
                    var allVehicles = await _dataService.LoadVehiclesAsync();
                    var vehicleIds = allVehicles
                        .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(GovernmentVehicle?.ManufacturerName) &&
                                   v.ModelName.EqualsIgnoringWhitespace(GovernmentVehicle?.ModelName))
                        .Select(v => v.VehicleTypeId)
                        .ToList();

                    await _dataService.UnmapPartsFromVehiclesAsync(
                        vehicleIds,
                        new List<string> { part.PartNumber },
                        "current_user");

                    StatusMessage = $"המיפוי של '{part.PartName}' הוסר מכל ווריאנטים של {GovernmentVehicle?.ModelName} ({vehicleIds.Count} רכבים)";
                }
                else
                {
                    // Unmap from only this specific variant (same engine, transmission, trim, etc.)
                    StatusMessage = "מסיר מיפוי מהווריאנט המדויק...";

                    // Get all vehicles with the same variant characteristics
                    var allVehicles = await _dataService.LoadVehiclesAsync();
                    var sameVariantVehicles = GetSameVariantVehicles(allVehicles, MatchedVehicle);
                    var vehicleIds = sameVariantVehicles.Select(v => v.VehicleTypeId).ToList();

                    await _dataService.UnmapPartsFromVehiclesAsync(
                        vehicleIds,
                        new List<string> { part.PartNumber },
                        "current_user");

                    StatusMessage = $"המיפוי של '{part.PartName}' הוסר מהווריאנט {variantDescription} ({vehicleIds.Count} רכבים)";
                }
            }

            // Reload parts
            MappedParts.Remove(part);

            // Reload suggestions to see if the part should be suggested again
            await ReloadSuggestionsAsync();
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

    /// <summary>
    /// Reloads mapped parts using the consolidated model approach with coupling support
    /// </summary>
    private async Task ReloadMappedPartsAsync()
    {
        if (MatchedVehicle == null) return;

        try
        {
            List<PartDisplayModel> parts;

            if (ConsolidatedModel != null)
            {
                // Load from consolidated model (includes couplings)
                parts = await _dataService.LoadMappedPartsForConsolidatedModelAsync(
                    ConsolidatedModel.ConsolidatedModelId,
                    includeCouplings: true);
            }
            else
            {
                // Need to find the consolidated model for this vehicle
                await using var context = await _contextFactory.CreateDbContextAsync();
                var vehicleType = await context.VehicleTypes
                    .Include(vt => vt.ConsolidatedModel)
                    .FirstOrDefaultAsync(vt => vt.VehicleTypeId == MatchedVehicle.VehicleTypeId);

                if (vehicleType?.ConsolidatedModel != null)
                {
                    ConsolidatedModel = vehicleType.ConsolidatedModel;
                    parts = await _dataService.LoadMappedPartsForConsolidatedModelAsync(
                        ConsolidatedModel.ConsolidatedModelId,
                        includeCouplings: true);
                }
                else
                {
                    // Fallback to legacy approach
                    parts = await _dataService.LoadMappedPartsAsync(MatchedVehicle.VehicleTypeId);
                }
            }

            MappedParts.Clear();
            foreach (var part in parts)
            {
                MappedParts.Add(part);
            }
        }
        catch
        {
            // Fallback to legacy approach if consolidated model fails
            var parts = await _dataService.LoadMappedPartsAsync(MatchedVehicle.VehicleTypeId);
            MappedParts.Clear();
            foreach (var part in parts)
            {
                MappedParts.Add(part);
            }
        }
    }

    private async Task ReloadSuggestionsAsync()
    {
        if (MatchedVehicle == null) return;

        try
        {
            var suggestions = await _dataService.GetSuggestedPartsForVehicleAsync(MatchedVehicle.VehicleTypeId);
            SuggestedParts.Clear();
            foreach (var suggestion in suggestions)
            {
                SuggestedParts.Add(suggestion);
            }
        }
        catch
        {
            // Silently fail for suggestions
            SuggestedParts.Clear();
        }
    }

    [RelayCommand]
    private async Task QuickMapSuggestionAsync(PartDisplayModel? part)
    {
        if (part == null || MatchedVehicle == null)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "ממפה חלק...";

            // NEW WAY: Map to consolidated model if available
            if (ConsolidatedModel != null)
            {
                // Use consolidated model mapping - covers all years automatically
                await _dataService.MapPartsToConsolidatedModelAsync(
                    ConsolidatedModel.ConsolidatedModelId,
                    new List<string> { part.PartNumber },
                    "current_user");

                // Move from suggestions to mapped
                SuggestedParts.Remove(part);
                part.HasSuggestion = false;
                part.MappingType = "Direct";
                if (!MappedParts.Any(p => p.PartNumber == part.PartNumber))
                {
                    MappedParts.Add(part);
                }

                // Reload suggestions
                await ReloadSuggestionsAsync();

                var yearRange = ConsolidatedModel.YearTo.HasValue
                    ? $"{ConsolidatedModel.YearFrom}-{ConsolidatedModel.YearTo}"
                    : $"{ConsolidatedModel.YearFrom}+";
                StatusMessage = $"✓ מופה החלק {part.PartNumber} לדגם מאוחד ({yearRange})";
            }
            else
            {
                // FALLBACK: Legacy mapping approach
                var variantDescription = GetVariantDescription(MatchedVehicle);

                // Ask user: map to all model variants or just this variant?
                var result = MessageBox.Show(
                    $"האם למפות '{part.PartName}' לכל הווריאנטים של '{MatchedVehicle.ModelName}'?\n\n" +
                    $"הווריאנט הנוכחי: {variantDescription}\n\n" +
                    "בחר 'כן' למיפוי לכל הווריאנטים (כל נפחי מנוע, תיבות הילוכים ורמות גימור),\n" +
                    "'לא' למיפוי רק לווריאנט המדויק הזה,\n" +
                    "או 'ביטול'.",
                    "בחירת היקף מיפוי",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Cancel)
                {
                    IsLoading = false;
                    return;
                }

                var allVehicles = await _dataService.LoadVehiclesAsync();
                List<int> vehicleTypeIds;

                if (result == MessageBoxResult.Yes)
                {
                    // Map to ALL variants of this model
                    vehicleTypeIds = allVehicles
                        .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(MatchedVehicle.ManufacturerName) &&
                                   v.ModelName.EqualsIgnoringWhitespace(MatchedVehicle.ModelName))
                        .Select(v => v.VehicleTypeId)
                        .ToList();
                }
                else
                {
                    // Map only to this specific variant
                    var sameVariantVehicles = GetSameVariantVehicles(allVehicles, MatchedVehicle);
                    vehicleTypeIds = sameVariantVehicles.Select(v => v.VehicleTypeId).ToList();
                }

                if (!vehicleTypeIds.Any())
                {
                    StatusMessage = "לא נמצאו רכבים תואמים";
                    return;
                }

                // Map the suggested part to the selected vehicles (legacy)
                await _dataService.MapPartsToVehiclesAsync(vehicleTypeIds, new List<string> { part.PartNumber }, "current_user");

                // Move from suggestions to mapped
                SuggestedParts.Remove(part);
                part.HasSuggestion = false;
                if (!MappedParts.Any(p => p.PartNumber == part.PartNumber))
                {
                    MappedParts.Add(part);
                }

                // Reload suggestions to reflect the new mapping
                await ReloadSuggestionsAsync();

                var scope = result == MessageBoxResult.Yes ? "כל הווריאנטים" : $"הווריאנט {variantDescription}";
                StatusMessage = $"✓ מופה החלק {part.PartNumber} ל-{vehicleTypeIds.Count} רכבים ({scope})";
            }
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

    [RelayCommand]
    private async Task ShowCouplingInfoAsync(ConsolidatedVehicleModel? model)
    {
        if (model == null) return;

        try
        {
            // Get couplings for this model
            var couplings = await _dataService.GetModelCouplingsAsync(model.ConsolidatedModelId);
            var activeCouplings = couplings.Where(c => c.IsActive).ToList();

            if (!activeCouplings.Any())
            {
                MessageBox.Show(
                    $"הדגם '{model.Manufacturer?.ManufacturerShortName} {model.ModelName}' לא מצומד לדגמים אחרים.",
                    "מידע על צימוד",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

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
                    var yearRange = otherModel.YearTo.HasValue
                        ? $"{otherModel.YearFrom}-{otherModel.YearTo}"
                        : $"{otherModel.YearFrom}+";
                    coupledModelNames.Add($"• {otherModel.Manufacturer?.ManufacturerShortName} {otherModel.ModelName} ({yearRange})");
                }
            }

            var yearRangeDisplay = model.YearTo.HasValue
                ? $"{model.YearFrom}-{model.YearTo}"
                : $"{model.YearFrom}+";

            var coupledModelsText = string.Join("\n", coupledModelNames);

            MessageBox.Show(
                $"🔗 הדגם '{model.Manufacturer?.ManufacturerShortName} {model.ModelName}' ({yearRangeDisplay})\n" +
                $"מצומד עם {activeCouplings.Count} דגמים:\n\n" +
                $"{coupledModelsText}\n\n" +
                "החלקים הממופים לדגמים אלו משותפים לכולם.\n" +
                "לניהול הצימודים, עבור לכרטיסייה 'ניהול צימודים'.",
                "מידע על צימוד",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בטעינת מידע צימוד: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

}
