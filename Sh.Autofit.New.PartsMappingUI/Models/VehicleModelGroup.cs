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
}
