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

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _mappedParts = new();

    [ObservableProperty]
    private ObservableCollection<PartDisplayModel> _suggestedParts = new();

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _showCopiedPopup;

    public string CleanVinNumber => CleanVin(GovernmentVehicle?.VinNumber ?? GovernmentVehicle?.VinChassis);

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
            MappedParts.Clear();

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
                        cachedRegistration.VehicleType?.Manufacturer?.ManufacturerShortName ?? "",
                        cachedRegistration.VehicleType?.CommercialName ?? "",
                        cachedRegistration.VehicleType?.ModelName ?? "");

                    MatchedVehicle = vehicles.FirstOrDefault(v => v.VehicleTypeId == cachedRegistration.VehicleTypeId.Value);

                    if (MatchedVehicle != null)
                    {
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

                        // Load mapped parts
                        StatusMessage = "טוען חלקים ממופים...";
                        var cachedParts = await _dataService.LoadMappedPartsByModelNameAsync(
                            cachedRegistration.GovManufacturerName ?? "",
                            cachedRegistration.GovModelName ?? "");

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
                    // This plate was previously not found in Gov API
                    StatusMessage = $"רכב לא נמצא במאגר (מטמון - חיפוש #{cachedRegistration.LookupCount})";

                    // Update lookup count
                    await _dataService.UpsertVehicleRegistrationAsync(
                        PlateNumber,
                        null,
                        null,
                        null,
                        "NotFoundInGovAPI",
                        "Previously not found in government API",
                        "Cache");
                    return;
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
            StatusMessage = "רכב נמצא, מחפש התאמה במערכת...";
            apiResource = "Primary"; // You can enhance GovernmentApiService to return which resource was used

            // Step 2: Find matching vehicle in our database
            var matchedVehicle = await _vehicleMatchingService.FindMatchingVehicleTypeAsync(govVehicle);

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

            // Step 2.9: Cache the lookup result (Task 5)
            await _dataService.UpsertVehicleRegistrationAsync(
                PlateNumber,
                govVehicle,
                matchedVehicleTypeId,
                matchedManufacturerId,
                matchStatus,
                matchReason,
                apiResource);

            // Step 3: Load mapped parts by model name (not just this specific vehicle type)
            StatusMessage = "טוען חלקים ממופים לדגם...";
            var parts = await _dataService.LoadMappedPartsByModelNameAsync(
                govVehicle.ManufacturerName ?? "",
                govVehicle.ModelName ?? "");

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
            // Use the old simple QuickMapDialog
            var dialog = new Views.QuickMapDialog(_dataService, MatchedVehicle.VehicleTypeId);
            var result = dialog.ShowDialog();

            if (result == true)
            {
                // Refresh mapped parts list
                StatusMessage = "מעדכן רשימת חלקים...";
                var parts = await _dataService.LoadMappedPartsAsync(MatchedVehicle.VehicleTypeId);
                MappedParts.Clear();
                foreach (var part in parts)
                {
                    MappedParts.Add(part);
                }

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
        MappedParts.Clear();
        StatusMessage = string.Empty;
        HasResults = false;
        ShowCopiedPopup = false;
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
                StatusMessage = "טוען חלקים ממופים...";

                var parts = await _dataService.LoadMappedPartsAsync(MatchedVehicle.VehicleTypeId);
                MappedParts.Clear();
                foreach (var part in parts)
                {
                    MappedParts.Add(part);
                }

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

        // Ask user if they want to unmap from just this vehicle or all vehicles with same model name
        var result = MessageBox.Show(
            $"האם להסיר את '{part.PartName}' מכל הרכבים בדגם '{GovernmentVehicle?.ModelName}'?\n\n" +
            "בחר 'כן' להסרה מכל הדגם, 'לא' להסרה מרכב זה בלבד, או 'ביטול'.",
            "אישור הסרת מיפוי",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Cancel)
            return;

        try
        {
            IsLoading = true;

            if (result == MessageBoxResult.Yes)
            {
                // Unmap from all vehicles with same model name
                StatusMessage = "מסיר מיפוי מכל הרכבים בדגם...";

                // Get all vehicles for this model (using normalized comparison to ignore whitespace)
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

                StatusMessage = $"המיפוי של '{part.PartName}' הוסר מכל רכבי {GovernmentVehicle?.ModelName}";
            }
            else
            {
                // Unmap from only this specific vehicle
                StatusMessage = "מסיר מיפוי...";

                await _dataService.UnmapPartsFromVehiclesAsync(
                    new List<int> { MatchedVehicle.VehicleTypeId },
                    new List<string> { part.PartNumber },
                    "current_user");

                StatusMessage = $"המיפוי של '{part.PartName}' הוסר מרכב זה";
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

            // Map directly to ALL vehicles in the current model (model-level mapping)
            var allVehicles = await _dataService.LoadVehiclesAsync();

            // Get ALL vehicles from the current vehicle's model
            var vehicleTypeIds = allVehicles
                .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(MatchedVehicle.ManufacturerName) &&
                           v.ModelName.EqualsIgnoringWhitespace(MatchedVehicle.ModelName))
                .Select(v => v.VehicleTypeId)
                .ToList();

            if (!vehicleTypeIds.Any())
            {
                StatusMessage = "לא נמצאו רכבים במודל זה";
                return;
            }

            // Map the suggested part to ALL vehicles in the model
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

            StatusMessage = $"✓ מופה החלק {part.PartNumber} ל-{vehicleTypeIds.Count} רכבים במודל {MatchedVehicle.ModelName}";
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
