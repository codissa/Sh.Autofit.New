using CommunityToolkit.Mvvm.ComponentModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class VehicleModelGroup : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string ManufacturerName { get; set; } = string.Empty;
    public string ManufacturerShortName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int VehicleCount { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public int MappedPartsCount { get; set; }

    // Additional details
    public List<int> EngineVolumes { get; set; } = new List<int>();
    public List<string> FuelTypes { get; set; } = new List<string>();

    public string DisplayName
    {
        get
        {
            var mfg = !string.IsNullOrEmpty(ManufacturerShortName) ? ManufacturerShortName : ManufacturerName;
            var yearRange = GetYearRangeDisplay();

            return string.IsNullOrEmpty(yearRange)
                ? $"{mfg} - {ModelName} ({VehicleCount} רכבים)"
                : $"{mfg} - {ModelName} {yearRange} ({VehicleCount} רכבים)";
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

    public string EngineVolumesDisplay
    {
        get
        {
            if (EngineVolumes == null || !EngineVolumes.Any())
                return string.Empty;

            var uniqueVolumes = EngineVolumes.Distinct().OrderBy(v => v).ToList();
            if (uniqueVolumes.Count == 1)
                return $"{uniqueVolumes[0]} סמ\"ק";

            return string.Join(", ", uniqueVolumes.Select(v => $"{v}")) + " סמ\"ק";
        }
    }

    public string FuelTypesDisplay
    {
        get
        {
            if (FuelTypes == null || !FuelTypes.Any())
                return string.Empty;

            var uniqueFuelTypes = FuelTypes
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .ToList();

            if (uniqueFuelTypes.Count == 0)
                return string.Empty;

            if (uniqueFuelTypes.Count == 1)
                return uniqueFuelTypes[0];

            return string.Join(", ", uniqueFuelTypes);
        }
    }
}
