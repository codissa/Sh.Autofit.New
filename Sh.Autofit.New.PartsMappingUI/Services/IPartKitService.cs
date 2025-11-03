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
}
