namespace Sh.Autofit.New.PartsMappingUI.Models;

public class PartSuggestion
{
    public string PartNumber { get; set; } = string.Empty;
    public double RelevanceScore { get; set; } // 0-100
    public List<SuggestionReason> Reasons { get; set; } = new();

    public string ConfidenceLevel => RelevanceScore switch
    {
        >= 70 => "High",
        >= 40 => "Medium",
        >= 20 => "Low",
        _ => "Very Low"
    };

    public string ConfidenceIcon => RelevanceScore switch
    {
        >= 70 => "🟢",
        >= 40 => "🟡",
        >= 20 => "🟠",
        _ => "⚪"
    };

    public string ReasonsSummary => string.Join(", ", Reasons.Select(r => r.Description));
}

public class SuggestionReason
{
    public SuggestionStrategy Strategy { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Score { get; set; }
}

public enum SuggestionStrategy
{
    NameMatch,          // Vehicle name in part description
    CodeSimilarity,     // Model code similar to part number
    OemAssociation,     // OEM numbers used by similar vehicles
    CategoryMatch,      // Part category matches vehicle category
    ManufacturerMatch   // Part manufacturer matches vehicle manufacturer
}
