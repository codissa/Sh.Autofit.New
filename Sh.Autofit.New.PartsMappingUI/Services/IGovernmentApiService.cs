using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IGovernmentApiService
{
    Task<GovernmentVehicleRecord?> LookupVehicleByPlateAsync(string plateNumber, CancellationToken ct = default);
    Task<bool> IsVehicleOffRoadAsync(string plateNumber, CancellationToken ct = default);
    Task<bool> IsPersonalImportAsync(string plateNumber, CancellationToken ct = default);
}
