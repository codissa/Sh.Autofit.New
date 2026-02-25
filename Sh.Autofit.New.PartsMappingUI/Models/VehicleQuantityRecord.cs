using System.Text.Json.Serialization;

namespace Sh.Autofit.New.PartsMappingUI.Models;

/// <summary>
/// Record from the government vehicle quantity dataset
/// (resource_id: 5e87a7a1-2f6f-41c1-8aec-7216d52a6cf6)
/// </summary>
public class VehicleQuantityRecord
{
    [JsonPropertyName("_id")]
    public int Id { get; set; }

    [JsonPropertyName("sug_degem")]
    public string? VehicleBodyType { get; set; }

    [JsonPropertyName("tozeret_cd")]
    public int ManufacturerCode { get; set; }

    [JsonPropertyName("tozeret_nm")]
    public string? ManufacturerName { get; set; }

    [JsonPropertyName("tozeret_eretz_nm")]
    public string? ManufacturerCountry { get; set; }

    [JsonPropertyName("tozar")]
    public string? ManufacturerShortName { get; set; }

    [JsonPropertyName("degem_cd")]
    public int ModelCode { get; set; }

    [JsonPropertyName("degem_nm")]
    public string? ModelName { get; set; }

    [JsonPropertyName("shnat_yitzur")]
    public int? ManufacturingYear { get; set; }

    [JsonPropertyName("mispar_rechavim_pailim")]
    public int ActiveVehicleCount { get; set; }

    [JsonPropertyName("mispar_rechavim_le_pailim")]
    public int InactiveVehicleCount { get; set; }

    [JsonPropertyName("kinuy_mishari")]
    public string? CommercialName { get; set; }
}

public class VehicleQuantityApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public VehicleQuantityApiResult? Result { get; set; }
}

public class VehicleQuantityApiResult
{
    [JsonPropertyName("records")]
    public List<VehicleQuantityRecord>? Records { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public record VehicleCountResult(int Active, int Inactive, int Total);
