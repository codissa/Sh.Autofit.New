using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class PlateLookupViewModel : ObservableObject
{
    private readonly IGovernmentApiService _governmentApiService;
    private readonly IVehicleMatchingService _vehicleMatchingService;
    private readonly IDataService _dataService;

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
        IDataService dataService)
    {
        _governmentApiService = governmentApiService;
        _vehicleMatchingService = vehicleMatchingService;
        _dataService = dataService;
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
            StatusMessage = "מחפש ברשומות משרד התחבורה...";
            GovernmentVehicle = null;
            MatchedVehicle = null;
            MappedParts.Clear();

            // Step 1: Lookup vehicle from government API (tries all fallback sources automatically)
            var govVehicle = await _governmentApiService.LookupVehicleByPlateAsync(PlateNumber);

            if (govVehicle == null)
            {
                StatusMessage = "רכב לא נמצא במאגר משרד התחבורה";
                return;
            }

            GovernmentVehicle = govVehicle;
            OnPropertyChanged(nameof(CleanVinNumber));
            StatusMessage = "רכב נמצא, מחפש התאמה במערכת...";

            // Step 2: Find matching vehicle in our database
            var matchedVehicle = await _vehicleMatchingService.FindMatchingVehicleTypeAsync(govVehicle);

            // Step 2.5: If not found, auto-create a new vehicle type
            if (matchedVehicle == null)
            {
                StatusMessage = "לא נמצאה התאמה במערכת, יוצר רכב חדש...";
                matchedVehicle = await _dataService.CreateVehicleTypeFromGovernmentRecordAsync(govVehicle);
                StatusMessage = "רכב חדש נוצר במערכת!";
            }
            else
            {
                StatusMessage = "נמצאה התאמה במערכת!";
            }

            MatchedVehicle = matchedVehicle;

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

            StatusMessage = $"נמצאו {MappedParts.Count} חלקים ממופים לדגם {govVehicle.ModelName}";
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
}
