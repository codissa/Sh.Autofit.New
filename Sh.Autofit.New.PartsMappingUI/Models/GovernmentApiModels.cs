using System.Text.Json.Serialization;

namespace Sh.Autofit.New.PartsMappingUI.Models;

/// <summary>
/// Response from data.gov.il vehicle API
/// </summary>
public class GovernmentApiResponse
{
    [JsonPropertyName("help")]
    public string? Help { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public GovernmentApiResult? Result { get; set; }
}

public class GovernmentApiResult
{
    [JsonPropertyName("include_total")]
    public bool IncludeTotal { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("q")]
    public string? Query { get; set; }

    [JsonPropertyName("resource_id")]
    public string? ResourceId { get; set; }

    [JsonPropertyName("records")]
    public List<GovernmentVehicleRecord>? Records { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class GovernmentVehicleRecord
{
    [JsonPropertyName("_id")]
    public int Id { get; set; }

    [JsonPropertyName("mispar_rechev")]
    public long LicensePlate { get; set; }

    [JsonPropertyName("tozeret_cd")]
    public int? ManufacturerCode { get; set; }

    [JsonPropertyName("sug_degem")]
    public string? ModelType { get; set; }

    [JsonPropertyName("tozeret_nm")]
    public string? ManufacturerName { get; set; }

    [JsonPropertyName("degem_cd")]
    public int? ModelCode { get; set; }

    [JsonPropertyName("degem_nm")]
    public string? ModelName { get; set; }

    [JsonPropertyName("ramat_gimur")]
    public string? TrimLevel { get; set; }

    [JsonPropertyName("kvutzat_zihum")]
    public int? PollutionGroup { get; set; }

    [JsonPropertyName("shnat_yitzur")]
    public int? ManufacturingYear { get; set; }

    [JsonPropertyName("degem_manoa")]
    public string? EngineModel { get; set; }

    [JsonPropertyName("mivchan_acharon_dt")]
    public string? LastTestDate { get; set; }

    [JsonPropertyName("tokef_dt")]
    public string? ValidityDate { get; set; }

    [JsonPropertyName("baalut")]
    public string? OwnershipType { get; set; }

    [JsonPropertyName("misgeret")]
    public string? VinChassis { get; set; }

    [JsonPropertyName("mispar_shilda")]
    public string? VinNumber { get; set; }

    [JsonPropertyName("nefach_manoa")]
    public int? EngineVolume { get; set; }

    [JsonPropertyName("mispar_manoa")]
    public string? EngineNumber { get; set; }

    [JsonPropertyName("tzeva_cd")]
    public int? ColorCode { get; set; }

    [JsonPropertyName("tzeva_rechev")]
    public string? ColorName { get; set; }

    [JsonPropertyName("zmig_kidmi")]
    public string? FrontTire { get; set; }

    [JsonPropertyName("zmig_ahori")]
    public string? RearTire { get; set; }

    [JsonPropertyName("sug_delek_nm")]
    public string? FuelType { get; set; }

    [JsonPropertyName("horaat_rishum")]
    public int? RegistrationInstruction { get; set; }

    [JsonPropertyName("moed_aliya_lakvish")]
    public string? RoadDate { get; set; }

    [JsonPropertyName("kinuy_mishari")]
    public string? CommercialName { get; set; }

    [JsonPropertyName("rank")]
    public double? Rank { get; set; }

    // Import-specific fields (populated by GovernmentVehicleRecordConverter)
    // These fields only exist in the Personal Import API resource
    /// <summary>
    /// Vehicle type name from import API (sug_rechev_nm) - e.g., "פרטי נוסעים"
    /// </summary>
    public string? VehicleTypeName { get; set; }

    /// <summary>
    /// Vehicle type code from import API (sug_rechev_cd) - e.g., 112
    /// </summary>
    public int? VehicleTypeCode { get; set; }

    /// <summary>
    /// Country of origin from import API (tozeret_eretz_nm) - e.g., "גרמניה"
    /// </summary>
    public string? CountryOfOrigin { get; set; }

    /// <summary>
    /// Import type from import API (sug_yevu) - e.g., "יבוא אישי-משומש", "יבוא אישי-חדש"
    /// </summary>
    public string? ImportType { get; set; }

    // Special vehicle flags (set by LookupVehicleByPlateAsync based on which resource found the vehicle)
    /// <summary>
    /// Indicates if this vehicle is an off-road vehicle (found in OFF_ROAD_VEHICLES_RESOURCE_ID)
    /// </summary>
    public bool IsOffRoad { get; set; }

    /// <summary>
    /// Indicates if this vehicle is a personal import (found in PERSONAL_IMPORT_RESOURCE_ID)
    /// </summary>
    public bool IsPersonalImport { get; set; }

    /// <summary>
    /// Which government API resource ID found this vehicle (for debugging and tracking)
    /// Examples: "Primary", "Fallback", "Fallback2", "PersonalImport", "OffRoad"
    /// </summary>
    public string? SourceResourceId { get; set; }
}
