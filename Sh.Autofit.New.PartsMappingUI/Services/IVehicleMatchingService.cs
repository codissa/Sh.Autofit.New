using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IVehicleMatchingService
{
    Task<VehicleDisplayModel?> FindMatchingVehicleTypeAsync(GovernmentVehicleRecord govRecord);
    Task<List<VehicleDisplayModel>> FindPossibleMatchesAsync(GovernmentVehicleRecord govRecord);
}
