using CommunityToolkit.Mvvm.ComponentModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class PendingVehicleDisplayModel : ObservableObject
{
    public int PendingVehicleId { get; set; }

    public int ManufacturerCode { get; set; }
    public string ManufacturerName { get; set; } = string.Empty;
    public int ModelCode { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string? CommercialName { get; set; }
    public int ManufacturingYear { get; set; }
    public int? EngineVolume { get; set; }
    public string? FuelType { get; set; }
    public string? TransmissionType { get; set; }
    public string? TrimLevel { get; set; }
    public string? FinishLevel { get; set; }
    public int? Horsepower { get; set; }
    public string? DriveType { get; set; }
    public int? NumberOfDoors { get; set; }
    public int? NumberOfSeats { get; set; }
    public int? TotalWeight { get; set; }
    public decimal? SafetyRating { get; set; }
    public int? GreenIndex { get; set; }

    public string ReviewStatus { get; set; } = "Pending";
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }

    public DateTime DiscoveredAt { get; set; }
    public string DiscoverySource { get; set; } = "AutoDiscovery";
    public Guid? BatchId { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayName => $"{ManufacturerName} {ModelName} ({ManufacturingYear})";

    public string Specs
    {
        get
        {
            var parts = new List<string>();

            if (EngineVolume.HasValue)
                parts.Add($"{EngineVolume}cc");

            if (!string.IsNullOrEmpty(FuelType))
                parts.Add(FuelType);

            if (!string.IsNullOrEmpty(TransmissionType))
                parts.Add(TransmissionType);

            if (Horsepower.HasValue)
                parts.Add($"{Horsepower}HP");

            return string.Join(" | ", parts);
        }
    }
}
