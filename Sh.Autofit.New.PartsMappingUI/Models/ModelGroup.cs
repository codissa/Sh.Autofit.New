using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class ModelGroup : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoaded;

    public string ModelName { get; set; } = string.Empty;
    public string ManufacturerShortName { get; set; } = string.Empty;
    public string CommercialName { get; set; } = string.Empty;
    public int VehicleCount { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }

    public ObservableCollection<VehicleDisplayModel> Vehicles { get; set; } = new();

    // Property to indicate this group has children that can be loaded
    public bool HasChildren => VehicleCount > 0;

    public string DisplayName
    {
        get
        {
            var yearRange = GetYearRangeDisplay();
            return string.IsNullOrEmpty(yearRange)
                ? $"{ModelName} ({VehicleCount})"
                : $"{ModelName} {yearRange} ({VehicleCount})";
        }
    }

    private string GetYearRangeDisplay()
    {
        if (YearFrom.HasValue && YearTo.HasValue)
        {
            if (YearFrom == YearTo)
                return $"({YearFrom})";
            return $"({YearFrom}-{YearTo})";
        }
        else if (YearFrom.HasValue)
        {
            return $"({YearFrom}+)";
        }
        else if (YearTo.HasValue)
        {
            return $"(-{YearTo})";
        }
        return string.Empty;
    }

    partial void OnIsExpandedChanged(bool value)
    {
        // This will be used to trigger loading/unloading
        if (value && !IsLoaded && !IsLoading)
        {
            // Signal that loading should happen
            // The ViewModel will handle this
        }
        else if (!value && IsLoaded)
        {
            // Release vehicles from memory when collapsed
            Vehicles.Clear();
            IsLoaded = false;
        }
    }
}
