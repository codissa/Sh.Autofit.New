using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IDataService
{
    Task<List<VehicleDisplayModel>> LoadVehiclesAsync();
    Task<List<(string ManufacturerShortName, string ManufacturerName, string CommercialName, int Count)>> LoadVehicleGroupSummaryAsync();
    Task<List<(string ModelName, int Count, int? YearFrom, int? YearTo, int? EngineVolume, string? FuelType, string? TransmissionType, string? TrimLevel)>> LoadModelGroupSummaryAsync(string manufacturerShortName, string commercialName);
    Task<List<VehicleDisplayModel>> LoadVehiclesByModelAsync(string manufacturerShortName, string commercialName, string modelName, int? engineVolume = null);
    Task<List<PartDisplayModel>> LoadPartsAsync();
    Task<List<PartDisplayModel>> LoadMappedPartsAsync(int vehicleTypeId);
    Task<List<PartDisplayModel>> LoadUnmappedPartsAsync(int vehicleTypeId);
    Task<List<PartDisplayModel>> LoadUnmappedPartsByModelAsync(string manufacturerName, string modelName);
    Task<Dictionary<int, int>> LoadMappingCountsAsync();
    Task<Dictionary<string, int>> LoadPartMappingCountsAsync();
    Task MapPartsToVehiclesAsync(List<int> vehicleTypeIds, List<string> partNumbers, string createdBy);
    Task UnmapPartsFromVehiclesAsync(List<int> vehicleTypeIds, List<string> partNumbers, string updatedBy);
    Task CopyMappingsAsync(int sourceVehicleTypeId, List<int> targetVehicleTypeIds, string createdBy);
    Task CopyPartMappingsAsync(string sourcePartNumber, List<string> targetPartNumbers, string createdBy);
    Task<int> GetTotalVehiclesAsync();
    Task<int> GetTotalPartsAsync();
    Task<int> GetMappedVehiclesAsync();
    Task<int> GetMappedPartsAsync();
    Task<VehicleDisplayModel> CreateVehicleTypeFromGovernmentRecordAsync(GovernmentVehicleRecord govRecord);
    Task<List<PartDisplayModel>> LoadMappedPartsByModelNameAsync(string manufacturerName, string modelName);

    // Vehicle Registration caching (Task 5)
    Task<VehicleRegistration?> GetCachedRegistrationAsync(string licensePlate);
    Task<VehicleRegistration> UpsertVehicleRegistrationAsync(
        string licensePlate,
        GovernmentVehicleRecord? govRecord,
        int? matchedVehicleTypeId,
        int? matchedManufacturerId,
        string matchStatus,
        string matchReason,
        string apiResourceUsed);

    // Analytics (Task 5)
    Task<int> GetTotalRegistrationLookupsAsync();
    Task<int> GetMatchedRegistrationsCountAsync();
    Task<int> GetUnmatchedRegistrationsCountAsync();
    Task<List<VehicleRegistration>> GetUnmatchedRegistrationsAsync();
    Task<List<(string ModelName, int Count)>> GetMostSearchedModelsAsync(int topCount = 10);
    Task<List<(string LicensePlate, int Count, DateTime LastLookup)>> GetMostSearchedPlatesAsync(int topCount = 10);

    // Item Management (Task 2)
    Task<List<VehicleDisplayModel>> LoadVehiclesForPartAsync(string partNumber);
    Task<List<VehicleDisplayModel>> GetSuggestedVehiclesForPartAsync(string partNumber);

    // Model Management (Task 3)
    Task<List<PartDisplayModel>> GetSuggestedPartsForModelAsync(string manufacturerName, string modelName);

    // Plate Lookup Suggestions
    Task<List<PartDisplayModel>> GetSuggestedPartsForVehicleAsync(int vehicleTypeId);
}
