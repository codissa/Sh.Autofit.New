using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sh.Autofit.New.PartsMappingUI.Models;

/// <summary>
/// Custom JSON converter for GovernmentVehicleRecord that handles field name variations
/// between different government API resources (main API vs Personal Import API)
/// </summary>
public class GovernmentVehicleRecordConverter : JsonConverter<GovernmentVehicleRecord>
{
    public override GovernmentVehicleRecord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var jsonDoc = JsonDocument.ParseValue(ref reader);
        var root = jsonDoc.RootElement;

        var record = new GovernmentVehicleRecord
        {
            // Standard fields (same across all APIs)
            Id = GetIntValue(root, "_id") ?? 0,
            LicensePlate = GetLongValue(root, "mispar_rechev") ?? 0,
            ManufacturerCode = GetIntValue(root, "tozeret_cd"),
            ManufacturerName = GetStringValue(root, "tozeret_nm"),
            ModelCode = GetIntValue(root, "degem_cd"),
            ModelName = GetStringValue(root, "degem_nm"),
            ModelType = GetStringValue(root, "sug_degem"),
            TrimLevel = GetStringValue(root, "ramat_gimur"),
            PollutionGroup = GetIntValue(root, "kvutzat_zihum"),
            ManufacturingYear = GetIntValue(root, "shnat_yitzur"),
            EngineModel = GetStringValue(root, "degem_manoa"),
            EngineVolume = GetIntValue(root, "nefach_manoa"),
            EngineNumber = GetStringValue(root, "mispar_manoa"),
            LastTestDate = GetStringValue(root, "mivchan_acharon_dt"),
            ValidityDate = GetStringValue(root, "tokef_dt"),
            OwnershipType = GetStringValue(root, "baalut"),
            VinChassis = GetStringValue(root, "misgeret"),
            ColorCode = GetIntValue(root, "tzeva_cd"),
            ColorName = GetStringValue(root, "tzeva_rechev"),
            FrontTire = GetStringValue(root, "zmig_kidmi"),
            RearTire = GetStringValue(root, "zmig_ahori"),
            FuelType = GetStringValue(root, "sug_delek_nm"),
            RegistrationInstruction = GetIntValue(root, "horaat_rishum"),
            RoadDate = GetStringValue(root, "moed_aliya_lakvish"),
            CommercialName = GetStringValue(root, "kinuy_mishari"),
            Rank = GetDoubleValue(root, "rank"),

            // VIN field - try alternate field names
            // Import API uses "shilda", main API uses "mispar_shilda"
            VinNumber = GetStringValue(root, "shilda") ?? GetStringValue(root, "mispar_shilda"),

            // Import-specific fields (only in Personal Import API)
            VehicleTypeName = GetStringValue(root, "sug_rechev_nm"),
            VehicleTypeCode = GetIntValue(root, "sug_rechev_cd"),
            CountryOfOrigin = GetStringValue(root, "tozeret_eretz_nm"),
            ImportType = GetStringValue(root, "sug_yevu")
        };

        return record;
    }

    public override void Write(Utf8JsonWriter writer, GovernmentVehicleRecord value, JsonSerializerOptions options)
    {
        // We don't need to write this model back to JSON, only read
        throw new NotImplementedException("Writing GovernmentVehicleRecord to JSON is not supported");
    }

    // Helper methods to safely get values from JSON
    private static string? GetStringValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
        {
            return prop.GetString();
        }
        return null;
    }

    private static int? GetIntValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
        {
            // Handle both numeric and string representations
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }
            else if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var intValue))
            {
                return intValue;
            }
        }
        return null;
    }

    private static long? GetLongValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
        {
            // Handle both numeric and string representations
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt64();
            }
            else if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out var longValue))
            {
                return longValue;
            }
        }
        return null;
    }

    private static double? GetDoubleValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
        {
            // Handle both numeric and string representations
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetDouble();
            }
            else if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var doubleValue))
            {
                return doubleValue;
            }
        }
        return null;
    }
}
