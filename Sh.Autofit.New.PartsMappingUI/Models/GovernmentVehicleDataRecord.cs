using System.Text.Json.Serialization;

namespace Sh.Autofit.New.PartsMappingUI.Models;

/// <summary>
/// Represents a vehicle record from the Israeli Government vehicle database API
/// </summary>
public class GovernmentVehicleDataRecord
{
    [JsonPropertyName("_id")]
    public int Id { get; set; }

    [JsonPropertyName("tozeret_cd")]
    public int ManufacturerCode { get; set; }

    [JsonPropertyName("tozeret_nm")]
    public string? ManufacturerName { get; set; }

    [JsonPropertyName("tozar")]
    public string? ManufacturerShortName { get; set; }

    [JsonPropertyName("degem_cd")]
    public string? ModelCode { get; set; }

    [JsonPropertyName("degem_nm")]
    public string? ModelName { get; set; }

    [JsonPropertyName("kinuy_mishari")]
    public string? CommercialName { get; set; }

    [JsonPropertyName("shnat_yitzur")]
    public int? Year { get; set; }

    [JsonPropertyName("nefah_manoa")]
    public int? EngineVolume { get; set; }

    [JsonPropertyName("koah_sus")]
    public int? Horsepower { get; set; }

    [JsonPropertyName("delek_nm")]
    public string? FuelType { get; set; }

    [JsonPropertyName("technologiat_hanaa_nm")]
    public string? DriveTypeTech { get; set; }

    [JsonPropertyName("hanaa_nm")]
    public string? DriveTypeName { get; set; }

    [JsonPropertyName("merkav")]
    public string? TrimLevel { get; set; }  // Body type (SUV, Sedan, etc.)

    [JsonPropertyName("ramat_gimur")]
    public string? FinishLevel { get; set; }  // Trim finish level

    [JsonPropertyName("mispar_dlatot")]
    public int? Doors { get; set; }

    [JsonPropertyName("mispar_moshavim")]
    public int? Seats { get; set; }

    [JsonPropertyName("mishkal_kolel")]
    public int? TotalWeight { get; set; }

    [JsonPropertyName("nikud_betihut")]
    public decimal? SafetyRating { get; set; }

    [JsonPropertyName("madad_yarok")]
    public int? GreenIndex { get; set; }  // Green/environmental index

    [JsonPropertyName("automatic_ind")]
    public int? IsAutomatic { get; set; }

    [JsonPropertyName("mazgan_ind")]
    public int? HasAirConditioning { get; set; }

    [JsonPropertyName("abs_ind")]
    public int? HasABS { get; set; }

    // Additional useful fields
    [JsonPropertyName("CO2_WLTP")]
    public decimal? CO2Emissions { get; set; }

    [JsonPropertyName("mispar_kariot_avir")]
    public int? NumberOfAirbags { get; set; }

    /// <summary>
    /// Gets the drive type in a standardized format
    /// </summary>
    public string GetStandardizedDriveType()
    {
        var driveInfo = DriveTypeTech ?? DriveTypeName ?? "";

        if (driveInfo.Contains("4X4", StringComparison.OrdinalIgnoreCase) ||
            driveInfo.Contains("4WD", StringComparison.OrdinalIgnoreCase) ||
            driveInfo.Contains("ארבעה גלגלים", StringComparison.OrdinalIgnoreCase))
        {
            return "4WD";
        }

        if (driveInfo.Contains("AWD", StringComparison.OrdinalIgnoreCase) ||
            driveInfo.Contains("כפולה", StringComparison.OrdinalIgnoreCase))
        {
            return "AWD";
        }

        if (driveInfo.Contains("FWD", StringComparison.OrdinalIgnoreCase) ||
            driveInfo.Contains("קדמית", StringComparison.OrdinalIgnoreCase))
        {
            return "FWD";
        }

        if (driveInfo.Contains("RWD", StringComparison.OrdinalIgnoreCase) ||
            driveInfo.Contains("אחורית", StringComparison.OrdinalIgnoreCase))
        {
            return "RWD";
        }

        // Default to 2WD if "הנעה רגילה" or unknown
        return "2WD";
    }

    /// <summary>
    /// Gets transmission type as string
    /// </summary>
    public string GetTransmissionType()
    {
        return IsAutomatic == 1 ? "Automatic" : "Manual";
    }
}

/// <summary>
/// Response from CKAN datastore_search API
/// </summary>
public class GovernmentVehicleApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public GovernmentVehicleApiResult? Result { get; set; }
}

/// <summary>
/// Result object from CKAN API
/// </summary>
public class GovernmentVehicleApiResult
{
    [JsonPropertyName("records")]
    public List<GovernmentVehicleDataRecord>? Records { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("_links")]
    public GovernmentVehicleApiLinks? Links { get; set; }
}

/// <summary>
/// Pagination links from CKAN API
/// </summary>
public class GovernmentVehicleApiLinks
{
    [JsonPropertyName("start")]
    public string? Start { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }
}
