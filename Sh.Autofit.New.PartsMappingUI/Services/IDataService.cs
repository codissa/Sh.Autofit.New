using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IDataService
{
    Task<List<VehicleDisplayModel>> LoadVehiclesAsync();
    Task<List<(string ManufacturerShortName, string ManufacturerName, string CommercialName, int Count)>> LoadVehicleGroupSummaryAsync();
    Task<List<(string ModelName, int Count, int? YearFrom, int? YearTo)>> LoadModelGroupSummaryAsync(string manufacturerShortName, string commercialName);
    Task<List<VehicleDisplayModel>> LoadVehiclesByModelAsync(string manufacturerShortName, string commercialName, string modelName);
    Task<List<PartDisplayModel>> LoadPartsAsync();
    Task<List<PartDisplayModel>> LoadMappedPartsAsync(int vehicleTypeId);
    Task<List<PartDisplayModel>> LoadUnmappedPartsAsync(int vehicleTypeId);
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
}
