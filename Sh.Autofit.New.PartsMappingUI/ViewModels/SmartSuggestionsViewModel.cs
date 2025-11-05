using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sh.Autofit.New.PartsMappingUI.Models;
using Sh.Autofit.New.PartsMappingUI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace Sh.Autofit.New.PartsMappingUI.ViewModels;

public partial class SmartSuggestionsViewModel : ObservableObject
{
    private readonly ISmartSuggestionsService _smartSuggestionsService;

    [ObservableProperty]
    private ObservableCollection<SmartSuggestion> _suggestions = new();

    [ObservableProperty]
    private ObservableCollection<SmartSuggestion> _filteredSuggestions = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _selectedManufacturer;

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private double _minScore = 70;

    [ObservableProperty]
    private bool _showOnlyHighConfidence;

    [ObservableProperty]
    private ObservableCollection<string> _availableManufacturers = new();

    [ObservableProperty]
    private ObservableCollection<string> _availableCategories = new();

    // Statistics
    [ObservableProperty]
    private int _totalSuggestions;

    [ObservableProperty]
    private int _highConfidenceSuggestions;

    [ObservableProperty]
    private int _mediumConfidenceSuggestions;

    [ObservableProperty]
    private int _potentialVehicles;

    private List<SmartSuggestion> _allSuggestions = new();

    public SmartSuggestionsViewModel(
        ISmartSuggestionsService smartSuggestionsService)
    {
        _smartSuggestionsService = smartSuggestionsService;
    }

    public async Task InitializeAsync()
    {
        await LoadSuggestionsAsync();
    }

    [RelayCommand]
    private async Task LoadSuggestionsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "מייצר הצעות חכמות...";

            // Generate suggestions
            var suggestions = await _smartSuggestionsService.GenerateSuggestionsAsync(
                minScore: ShowOnlyHighConfidence ? 90 : MinScore,
                maxSuggestions: 200);

            _allSuggestions = suggestions;

            // Extract unique manufacturers and categories
            var manufacturers = suggestions
                .Select(s => s.SourceManufacturer)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            var categories = suggestions
                .Select(s => s.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            AvailableManufacturers.Clear();
            AvailableManufacturers.Add("הכל"); // All
            foreach (var mfg in manufacturers)
            {
                AvailableManufacturers.Add(mfg);
            }

            AvailableCategories.Clear();
            AvailableCategories.Add("הכל"); // All
            foreach (var cat in categories)
            {
                AvailableCategories.Add(cat);
            }

            // Calculate statistics
            UpdateStatistics();

            // Apply filters
            ApplyFilters();

            StatusMessage = $"נמצאו {TotalSuggestions} הצעות חכמות";
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה ביצירת הצעות: {ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AcceptSuggestionAsync(SmartSuggestion suggestion)
    {
        if (suggestion == null) return;

        try
        {
            var selectedTargets = suggestion.TargetModels.Count(t => t.IsSelected);
            var selectedVehicles = suggestion.TargetModels.Where(t => t.IsSelected).Sum(t => t.VehicleCount);

            var result = MessageBox.Show(
                $"האם למפות את החלק:\n{suggestion.PartNumber} - {suggestion.PartName}\n\n" +
                $"ל-{selectedTargets} דגמים ({selectedVehicles} רכבים)?",
                "אישור מיפוי",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            IsLoading = true;
            StatusMessage = "ממפה...";

            var vehiclesMapped = await _smartSuggestionsService.AcceptSuggestionAsync(suggestion, "current_user");

            // Mark as accepted and remove from list
            suggestion.IsAccepted = true;
            Suggestions.Remove(suggestion);
            FilteredSuggestions.Remove(suggestion);
            _allSuggestions.Remove(suggestion);

            UpdateStatistics();

            StatusMessage = $"מופה בהצלחה ל-{vehiclesMapped} רכבים!";
            MessageBox.Show($"החלק מופה בהצלחה ל-{vehiclesMapped} רכבים!",
                "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה במיפוי: {ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AcceptTopNAsync(int count)
    {
        var topSuggestions = FilteredSuggestions.Take(count).ToList();

        if (!topSuggestions.Any())
        {
            MessageBox.Show("אין הצעות למיפוי", "שים לב", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var totalVehicles = topSuggestions.Sum(s =>
            s.TargetModels.Where(t => t.IsSelected).Sum(t => t.VehicleCount));
        var totalModels = topSuggestions.Sum(s => s.TargetModels.Count(t => t.IsSelected));

        var result = MessageBox.Show(
            $"האם למפות את {count} ההצעות הראשונות?\n\n" +
            $"סה\"כ: {count} חלקים → {totalModels} דגמים ({totalVehicles} רכבים)",
            "אישור מיפוי קבוצתי",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"ממפה {count} הצעות...";

            var vehiclesMapped = await _smartSuggestionsService.AcceptSuggestionsAsync(topSuggestions, "current_user");

            // Remove accepted suggestions
            foreach (var suggestion in topSuggestions)
            {
                suggestion.IsAccepted = true;
                Suggestions.Remove(suggestion);
                FilteredSuggestions.Remove(suggestion);
                _allSuggestions.Remove(suggestion);
            }

            UpdateStatistics();

            StatusMessage = $"מופו בהצלחה {count} הצעות ל-{vehiclesMapped} רכבים!";
            MessageBox.Show($"מופו בהצלחה {count} הצעות ל-{vehiclesMapped} רכבים!",
                "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה במיפוי קבוצתי: {ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AcceptAllHighConfidenceAsync()
    {
        var highConfidenceSuggestions = FilteredSuggestions
            .Where(s => s.Confidence == ConfidenceLevel.High)
            .ToList();

        if (!highConfidenceSuggestions.Any())
        {
            MessageBox.Show("אין הצעות ברמת ביטחון גבוהה", "שים לב",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await AcceptMultipleSuggestionsAsync(highConfidenceSuggestions, "כל ההצעות ברמת ביטחון גבוהה");
    }

    [RelayCommand]
    private void RejectSuggestion(SmartSuggestion suggestion)
    {
        if (suggestion == null) return;

        suggestion.IsRejected = true;
        Suggestions.Remove(suggestion);
        FilteredSuggestions.Remove(suggestion);
        _allSuggestions.Remove(suggestion);

        UpdateStatistics();
        StatusMessage = "ההצעה נדחתה";
    }

    [RelayCommand]
    private void EditTargetModels(SmartSuggestion suggestion)
    {
        // This would open a dialog to select/deselect target models
        // For now, just show a message
        MessageBox.Show($"עריכת דגמים עבור:\n{suggestion.PartNumber} - {suggestion.PartName}\n\n" +
            $"ניתן לסמן/לבטל סימון דגמי יעד בטבלה למטה.",
            "עריכת דגמי יעד",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedManufacturerChanged(string? value)
    {
        ApplyFilters();
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        ApplyFilters();
    }

    partial void OnMinScoreChanged(double value)
    {
        ApplyFilters();
    }

    partial void OnShowOnlyHighConfidenceChanged(bool value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = _allSuggestions.AsEnumerable();

        // Search text filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLower();
            filtered = filtered.Where(s =>
                s.PartNumber.ToLower().Contains(search) ||
                s.PartName.ToLower().Contains(search) ||
                s.SourceModelName.ToLower().Contains(search));
        }

        // Manufacturer filter
        if (!string.IsNullOrEmpty(SelectedManufacturer) && SelectedManufacturer != "הכל")
        {
            filtered = filtered.Where(s => s.SourceManufacturer == SelectedManufacturer);
        }

        // Category filter
        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "הכל")
        {
            filtered = filtered.Where(s => s.Category == SelectedCategory);
        }

        // Score filter
        filtered = filtered.Where(s => s.Score >= MinScore);

        // High confidence only filter
        if (ShowOnlyHighConfidence)
        {
            filtered = filtered.Where(s => s.Confidence == ConfidenceLevel.High);
        }

        // Update filtered collection
        FilteredSuggestions.Clear();
        foreach (var suggestion in filtered)
        {
            FilteredSuggestions.Add(suggestion);
        }

        StatusMessage = $"מוצגות {FilteredSuggestions.Count} מתוך {_allSuggestions.Count} הצעות";
    }

    private void UpdateStatistics()
    {
        TotalSuggestions = _allSuggestions.Count;
        HighConfidenceSuggestions = _allSuggestions.Count(s => s.Confidence == ConfidenceLevel.High);
        MediumConfidenceSuggestions = _allSuggestions.Count(s => s.Confidence == ConfidenceLevel.Medium);
        PotentialVehicles = _allSuggestions.Sum(s => s.TotalTargetVehicles);
    }

    private async Task AcceptMultipleSuggestionsAsync(List<SmartSuggestion> suggestions, string description)
    {
        var totalVehicles = suggestions.Sum(s =>
            s.TargetModels.Where(t => t.IsSelected).Sum(t => t.VehicleCount));
        var totalModels = suggestions.Sum(s => s.TargetModels.Count(t => t.IsSelected));

        var result = MessageBox.Show(
            $"האם למפות את {description}?\n\n" +
            $"סה\"כ: {suggestions.Count} חלקים → {totalModels} דגמים ({totalVehicles} רכבים)",
            "אישור מיפוי קבוצתי",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"ממפה {suggestions.Count} הצעות...";

            var vehiclesMapped = await _smartSuggestionsService.AcceptSuggestionsAsync(suggestions, "current_user");

            // Remove accepted suggestions
            foreach (var suggestion in suggestions)
            {
                suggestion.IsAccepted = true;
                Suggestions.Remove(suggestion);
                FilteredSuggestions.Remove(suggestion);
                _allSuggestions.Remove(suggestion);
            }

            UpdateStatistics();

            StatusMessage = $"מופו בהצלחה {suggestions.Count} הצעות ל-{vehiclesMapped} רכבים!";
            MessageBox.Show($"מופו בהצלחה {suggestions.Count} הצעות ל-{vehiclesMapped} רכבים!",
                "הצלחה", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"שגיאה: {ex.Message}";
            MessageBox.Show($"שגיאה במיפוי קבוצתי: {ex.Message}", "שגיאה",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
