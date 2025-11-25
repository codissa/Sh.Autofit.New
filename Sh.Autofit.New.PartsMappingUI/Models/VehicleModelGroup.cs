using CommunityToolkit.Mvvm.ComponentModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class VehicleModelGroup : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _hasCouplings;

    [ObservableProperty]
    private string _couplingInfo = string.Empty;

    // If this group corresponds to a consolidated model, this will contain its id
    public int? ConsolidatedModelId { get; set; }

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
    public List<string> CommercialNames { get; set; } = new List<string>();
    public List<string> TransmissionTypes { get; set; } = new List<string>();
    public List<string> FinishLevels { get; set; } = new List<string>();  // מרכב
    public List<string> TrimLevels { get; set; } = new List<string>();    // רמת גימור

    public string DisplayName
    {
        get
        {
            var mfg = !string.IsNullOrEmpty(ManufacturerShortName) ? ManufacturerShortName : ManufacturerName;
            var parts = new List<string> { mfg, "-", ModelName };

            // Add distinguishing characteristics
            if (EngineVolumes.Any())
                parts.Add($"{EngineVolumesDisplay}");

            if (FuelTypes.Any())
                parts.Add(FuelTypesDisplay);

            if (TransmissionTypes.Any())
                parts.Add(TransmissionTypesDisplay);

            if (FinishLevels.Any())
                parts.Add($"({FinishLevelsDisplay})");

            if (TrimLevels.Any())
                parts.Add(TrimLevelsDisplay);

            var yearRange = GetYearRangeDisplay();
            if (!string.IsNullOrEmpty(yearRange))
                parts.Add(yearRange);

            parts.Add($"({VehicleCount} רכבים)");

            return string.Join(" ", parts);
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

    public string TransmissionTypesDisplay
    {
        get
        {
            if (TransmissionTypes == null || !TransmissionTypes.Any())
                return string.Empty;

            var uniqueTransmissionTypes = TransmissionTypes
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();

            if (uniqueTransmissionTypes.Count == 0)
                return string.Empty;

            if (uniqueTransmissionTypes.Count == 1)
                return uniqueTransmissionTypes[0];

            return string.Join(", ", uniqueTransmissionTypes);
        }
    }

    public string FinishLevelsDisplay
    {
        get
        {
            if (FinishLevels == null || !FinishLevels.Any())
                return string.Empty;

            var uniqueFinishLevels = FinishLevels
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .ToList();

            if (uniqueFinishLevels.Count == 0)
                return string.Empty;

            if (uniqueFinishLevels.Count == 1)
                return uniqueFinishLevels[0];

            return string.Join(", ", uniqueFinishLevels);
        }
    }

    public string TrimLevelsDisplay
    {
        get
        {
            if (TrimLevels == null || !TrimLevels.Any())
                return string.Empty;

            var uniqueTrimLevels = TrimLevels
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();

            if (uniqueTrimLevels.Count == 0)
                return string.Empty;

            if (uniqueTrimLevels.Count == 1)
                return uniqueTrimLevels[0];

            return string.Join(", ", uniqueTrimLevels);
        }
    }
}
