using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class AnalyticsDashboardViewModel : ObservableObject
{
    private readonly IDataService _dataService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _totalLookups;

    [ObservableProperty]
    private int _matchedCount;

    [ObservableProperty]
    private int _unmatchedCount;

    [ObservableProperty]
    private double _matchRate;

    [ObservableProperty]
    private ObservableCollection<MostSearchedModel> _mostSearchedModels = new();

    [ObservableProperty]
    private ObservableCollection<MostSearchedPlate> _mostSearchedPlates = new();

    [ObservableProperty]
    private ObservableCollection<VehicleRegistration> _unmatchedRegistrations = new();

    [ObservableProperty]
    private string _statusMessage = "";

    public AnalyticsDashboardViewModel(IDataService dataService)
    {
        _dataService = dataService;
    }

    [RelayCommand]
    private async Task LoadAnalyticsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading analytics...";

            // Load all analytics data in parallel for performance
            var totalLookupsTask = _dataService.GetTotalRegistrationLookupsAsync();
            var matchedCountTask = _dataService.GetMatchedRegistrationsCountAsync();
            var unmatchedCountTask = _dataService.GetUnmatchedRegistrationsCountAsync();
            var mostSearchedModelsTask = _dataService.GetMostSearchedModelsAsync(10);
            var mostSearchedPlatesTask = _dataService.GetMostSearchedPlatesAsync(10);
            var unmatchedRegistrationsTask = _dataService.GetUnmatchedRegistrationsAsync();

            await Task.WhenAll(
                totalLookupsTask,
                matchedCountTask,
                unmatchedCountTask,
                mostSearchedModelsTask,
                mostSearchedPlatesTask,
                unmatchedRegistrationsTask);

            // Update properties
            TotalLookups = await totalLookupsTask;
            MatchedCount = await matchedCountTask;
            UnmatchedCount = await unmatchedCountTask;

            // Calculate match rate
            var totalRegistrations = MatchedCount + UnmatchedCount;
            MatchRate = totalRegistrations > 0 ? (double)MatchedCount / totalRegistrations * 100 : 0;

            // Update most searched models
            MostSearchedModels.Clear();
            var models = await mostSearchedModelsTask;
            foreach (var (modelName, count) in models)
            {
                MostSearchedModels.Add(new MostSearchedModel
                {
                    ModelName = modelName,
                    SearchCount = count
                });
            }

            // Update most searched plates
            MostSearchedPlates.Clear();
            var plates = await mostSearchedPlatesTask;
            foreach (var (licensePlate, count, lastLookup) in plates)
            {
                MostSearchedPlates.Add(new MostSearchedPlate
                {
                    LicensePlate = licensePlate,
                    SearchCount = count,
                    LastLookup = lastLookup
                });
            }

            // Update unmatched registrations
            UnmatchedRegistrations.Clear();
            var unmatched = await unmatchedRegistrationsTask;
            foreach (var registration in unmatched)
            {
                UnmatchedRegistrations.Add(registration);
            }

            StatusMessage = $"Analytics loaded successfully. {TotalLookups} total lookups, {MatchRate:F1}% match rate.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading analytics: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAnalyticsAsync();
    }
}

// Helper classes for data binding
public class MostSearchedModel
{
    public string ModelName { get; set; } = "";
    public int SearchCount { get; set; }
}

public class MostSearchedPlate
{
    public string LicensePlate { get; set; } = "";
    public int SearchCount { get; set; }
    public DateTime LastLookup { get; set; }
}
