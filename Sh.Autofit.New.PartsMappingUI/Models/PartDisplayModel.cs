using CommunityToolkit.Mvvm.ComponentModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class PartDisplayModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string PartNumber { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public double? RetailPrice { get; set; }
    public double? CostPrice { get; set; }
    public double? StockQuantity { get; set; }
    public bool IsInStock { get; set; }
    public bool IsActive { get; set; }
    public bool UniversalPart { get; set; }
    public string? ImageUrl { get; set; }
    public string? CompatibilityNotes { get; set; }

    // OEM Numbers
    public string? OemNumber1 { get; set; }
    public string? OemNumber2 { get; set; }
    public string? OemNumber3 { get; set; }
    public string? OemNumber4 { get; set; }
    public string? OemNumber5 { get; set; }

    [ObservableProperty]
    private int _mappedVehiclesCount;

    // Alias for backwards compatibility with binding
    public int MappedVehicleCount => MappedVehiclesCount;

    [ObservableProperty]
    private MappingStatus _mappingStatus = MappingStatus.Unmapped;

    // Consolidated mapping type (Direct, CoupledModel, CoupledPart)
    [ObservableProperty]
    private string _mappingType = "Direct";

    // Display icon for mapping type
    public string MappingTypeIcon => MappingType switch
    {
        "Direct" => "🔵",      // Direct mapping
        "CoupledModel" => "🟢", // Via coupled model
        "CoupledPart" => "🟡",  // Via coupled part
        _ => "⚪"
    };

    public string MappingTypeDisplay => MappingType switch
    {
        "Direct" => "מיפוי ישיר",
        "CoupledModel" => "דרך דגם מצומד",
        "CoupledPart" => "דרך חלק מצומד",
        _ => ""
    };

    // Suggestion properties
    [ObservableProperty]
    private double _relevanceScore;

    [ObservableProperty]
    private string _relevanceReason = string.Empty;

    [ObservableProperty]
    private bool _hasSuggestion;

    // Virtual Part properties
    [ObservableProperty]
    private bool _isVirtual = false;

    [ObservableProperty]
    private bool _hasMigrationAvailable = false;

    [ObservableProperty]
    private string? _migrationCandidateRealPartNumber;

    public string DisplayName => $"{PartNumber} - {PartName}";

    public string StatusIcon => MappingStatus switch
    {
        MappingStatus.Unmapped => "🔴",
        MappingStatus.PartiallyMapped => "🟡",
        MappingStatus.Mapped => "🟢",
        _ => "⚪"
    };

    public string StockStatusIcon => IsInStock ? "✅" : "❌";

    public string UniversalIcon => UniversalPart ? "🌐" : "";

    // Get all OEM numbers as a list (non-null and non-empty), splitting by "/" if multiple in one field
    public List<string> OemNumbers
    {
        get
        {
            var numbers = new List<string>();
            var allFields = new[] { OemNumber1, OemNumber2, OemNumber3, OemNumber4, OemNumber5 };

            foreach (var field in allFields)
            {
                if (!string.IsNullOrWhiteSpace(field))
                {
                    // Split by "/" and trim whitespace from each part
                    var parts = field.Split('/', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(p => p.Trim())
                                     .Where(p => !string.IsNullOrWhiteSpace(p));
                    numbers.AddRange(parts);
                }
            }

            return numbers;
        }
    }

    // Get all OEM numbers as a formatted string for display
    public string OemNumbersDisplay => OemNumbers.Any()
        ? string.Join(" | ", OemNumbers)
        : "";

    // Check if part has any OEM numbers
    public bool HasOemNumbers => OemNumbers.Any();

    // Suggestion display properties
    public string SuggestionConfidenceLevel => RelevanceScore switch
    {
        >= 70 => "High",
        >= 40 => "Medium",
        >= 20 => "Low",
        _ => "Very Low"
    };

    public string SuggestionIcon => RelevanceScore switch
    {
        >= 70 => "⭐⭐⭐",
        >= 40 => "⭐⭐",
        >= 20 => "⭐",
        _ => ""
    };

    public string RelevanceScoreDisplay => HasSuggestion ? $"{RelevanceScore:F0}%" : "";

    // Virtual Part visual indicators
    public string VirtualIndicator => IsVirtual ? "🔶" : "";

    public string PartTypeDisplay => IsVirtual ? "Virtual Part" : "Real Part";

    public string BackgroundColorHex => IsVirtual ? "#FFF9E6" : "#FFFFFF"; // Light yellow for virtual parts

    public System.Windows.Media.Brush BackgroundColor => IsVirtual
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 248, 220))
        : System.Windows.Media.Brushes.White;

    public System.Windows.Media.Brush ForegroundColor => IsVirtual
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 0)) // Dark orange
        : System.Windows.Media.Brushes.Black;

    public System.Windows.FontStyle FontStyle => IsVirtual
        ? System.Windows.FontStyles.Italic
        : System.Windows.FontStyles.Normal;
}
