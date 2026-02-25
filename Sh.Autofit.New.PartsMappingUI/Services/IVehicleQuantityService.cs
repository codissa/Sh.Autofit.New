using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IVehicleQuantityService
{
    Task<VehicleCountResult?> GetVehicleCountAsync(int manufacturerCode, int modelCode, string modelName);

    Task<Dictionary<(int ManufacturerCode, int ModelCode, string ModelName), VehicleCountResult>> GetVehicleCountBatchAsync(
        IEnumerable<(int ManufacturerCode, int ModelCode, string ModelName)> modelKeys);
}
