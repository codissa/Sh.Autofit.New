using CommunityToolkit.Mvvm.ComponentModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class VehicleDisplayModel : ObservableObject
{
    public int VehicleTypeId { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isVisible = true;

    public int ManufacturerId { get; set; }
    public string ManufacturerName { get; set; } = string.Empty;
    public string ManufacturerShortName { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
    public string? VehicleCategory { get; set; }
    public string? CommercialName { get; set; }
    public string? FuelTypeName { get; set; }
    public string? EngineModel { get; set; }
    public int? EngineVolume { get; set; }

    [ObservableProperty]
    private int _mappedPartsCount;

    [ObservableProperty]
    private MappingStatus _mappingStatus = MappingStatus.Unmapped;

    // Suggestion properties (Task 2)
    [ObservableProperty]
    private bool _isSuggestion;

    [ObservableProperty]
    private string? _suggestionReason;

    public string DisplayName
    {
        get
        {
            var name = $"{ManufacturerShortName ?? ManufacturerName} {ModelName}";

            if (!string.IsNullOrEmpty(CommercialName))
                name += $" ({CommercialName})";

            name += $" [{YearFrom}{(YearTo.HasValue ? $"-{YearTo}" : "")}]";

            return name;
        }
    }

    // Labeled display properties for detailed view
    public string YearDisplay => YearTo.HasValue
        ? $"שנים: {YearFrom}-{YearTo}"
        : $"שנה: {YearFrom}";

    public string ModelDisplay => $"דגם: {ModelName}";

    public string CommercialNameDisplay => !string.IsNullOrEmpty(CommercialName)
        ? $"שם מסחרי: {CommercialName}"
        : null;

    public string ManufacturerDisplay => $"יצרן: {ManufacturerShortName ?? ManufacturerName}";

    public string CategoryDisplay => !string.IsNullOrEmpty(VehicleCategory)
        ? $"קטגוריה: {VehicleCategory}"
        : null;

    public string FuelDisplay => !string.IsNullOrEmpty(FuelTypeName)
        ? $"סוג דלק: {FuelTypeName}"
        : null;

    public string EngineModelDisplay => !string.IsNullOrEmpty(EngineModel)
        ? $"דגם מנוע: {EngineModel}"
        : null;

    public string EngineVolumeDisplay => EngineVolume.HasValue
        ? $"נפח מנוע: {EngineVolume} סמ\"ק"
        : null;

    public string PartsCountDisplay => $"חלקים ממופים: {MappedPartsCount}";

    public string StatusIcon => MappingStatus switch
    {
        MappingStatus.Unmapped => "🔴",
        MappingStatus.PartiallyMapped => "🟡",
        MappingStatus.Mapped => "🟢",
        _ => "⚪"
    };
}

public enum MappingStatus
{
    Unmapped,
    PartiallyMapped,
    Mapped
}
