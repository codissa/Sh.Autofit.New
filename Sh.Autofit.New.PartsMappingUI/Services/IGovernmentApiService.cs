using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IGovernmentApiService
{
    Task<GovernmentVehicleRecord?> LookupVehicleByPlateAsync(string plateNumber);
    Task<bool> IsVehicleOffRoadAsync(string plateNumber);
    Task<bool> IsPersonalImportAsync(string plateNumber);
}
