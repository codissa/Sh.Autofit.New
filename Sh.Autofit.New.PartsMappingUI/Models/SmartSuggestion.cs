namespace Sh.Autofit.New.PartsMappingUI.Models;

/// <summary>
/// Represents a smart suggestion to map a part from a source model to similar target models
/// </summary>
public class SmartSuggestion
{
    // Part Information
    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    // Source Model (where part is currently mapped)
    public string SourceManufacturer { get; set; } = string.Empty;
    public string SourceModelName { get; set; } = string.Empty;
    public string SourceCommercialName { get; set; } = string.Empty;
    public int SourceYearFrom { get; set; }
    public int SourceYearTo { get; set; }
    public int SourceEngineVolume { get; set; }
    public int SourceVehicleCount { get; set; }
    public int OtherModelsWithPart { get; set; } // How many other models use this part

    // Target Models (where we suggest mapping)
    public List<TargetModel> TargetModels { get; set; } = new();
    public int TotalTargetVehicles { get; set; }

    // Scoring
    public double Score { get; set; }
    public string ScoreReason { get; set; } = string.Empty;
    public List<string> ScoreBreakdown { get; set; } = new(); // Detailed score explanation
    public ConfidenceLevel Confidence => Score switch
    {
        >= 90 => ConfidenceLevel.High,
        >= 70 => ConfidenceLevel.Medium,
        >= 50 => ConfidenceLevel.Low,
        _ => ConfidenceLevel.VeryLow
    };

    // UI State
    public bool IsSelected { get; set; }
    public bool IsAccepted { get; set; }
    public bool IsRejected { get; set; }

    // UI Helpers
    public string ConfidenceIcon => Confidence switch
    {
        ConfidenceLevel.High => "🟢",
        ConfidenceLevel.Medium => "🟡",
        ConfidenceLevel.Low => "🟠",
        _ => "🔴"
    };

    public string ConfidenceText => Confidence.ToString();

    public string SourceModelDisplay =>
        $"{SourceManufacturer} {SourceModelName} {SourceCommercialName} ({SourceYearFrom}-{SourceYearTo}, {SourceEngineVolume}cc)";

    public string TargetModelsDisplay =>
        $"{TargetModels.Count} similar model{(TargetModels.Count != 1 ? "s" : "")} ({TotalTargetVehicles} vehicle{(TotalTargetVehicles != 1 ? "s" : "")})";
}

/// <summary>
/// Represents a target model that could receive the suggested mapping
/// </summary>
public class TargetModel
{
    public string ManufacturerName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string CommercialName { get; set; } = string.Empty;
    public int YearFrom { get; set; }
    public int YearTo { get; set; }
    public int EngineVolume { get; set; }
    public int VehicleCount { get; set; }
    public bool IsSelected { get; set; } = true; // Default to selected

    // Score components for this specific target
    public double TargetScore { get; set; }
    public bool HasCommercialNameMatch { get; set; }
    public bool HasYearOverlap { get; set; }
    public double OemSimilarityScore { get; set; }

    // UI Helper
    public string DisplayName =>
        $"{ModelName} {CommercialName} ({YearFrom}-{YearTo}, {EngineVolume}cc) - {VehicleCount} vehicle{(VehicleCount != 1 ? "s" : "")}";
}

/// <summary>
/// Confidence level for suggestions
/// </summary>
public enum ConfidenceLevel
{
    VeryLow = 0,  // < 50 score
    Low = 50,     // 50-69 score - Only engine match
    Medium = 70,  // 70-89 score - Same engine + years
    High = 90     // 90+ score - Same engine + years + commercial name + OEM patterns
}
