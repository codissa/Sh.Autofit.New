using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IPartKitService
{
    Task<List<PartKitDisplayModel>> LoadAllKitsAsync();
    Task<PartKitDisplayModel?> GetKitByIdAsync(int kitId);
    Task<int> CreateKitAsync(string kitName, string? description, string createdBy);
    Task UpdateKitAsync(int kitId, string kitName, string? description, string updatedBy);
    Task DeleteKitAsync(int kitId);
    Task<int> DuplicateKitAsync(int sourceKitId, string newKitName, string createdBy);
    Task AddPartToKitAsync(int kitId, string partNumber, int? displayOrder, string? notes, string createdBy);
    Task RemovePartFromKitAsync(int kitItemId);
    Task<List<PartKitItemDisplayModel>> GetKitPartsAsync(int kitId);
    Task MapKitToVehiclesAsync(int kitId, List<int> vehicleTypeIds, string createdBy);

    /// <summary>
    /// Syncs all parts in a kit to share the same vehicle mappings.
    /// For all vehicles that any part in the kit is mapped to, maps all other parts to those vehicles.
    /// </summary>
    Task<(int VehiclesMapped, int MappingsCreated)> SyncKitMappingsAsync(int kitId, string createdBy);

    /// <summary>
    /// Syncs all kits by running SyncKitMappingsAsync for each kit.
    /// </summary>
    Task<(int KitsSynced, int TotalMappingsCreated)> SyncAllKitsMappingsAsync(string createdBy);
}
