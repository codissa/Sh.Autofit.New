using System.Text.Json;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IGovernmentVehicleDataService
{
    /// <summary>
    /// Fetches all vehicle data from the government API
    /// </summary>
    Task<List<GovernmentVehicleDataRecord>> FetchAllVehicleDataAsync(
        int batchSize = 1000,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches vehicle data for specific manufacturer and model codes
    /// </summary>
    Task<List<GovernmentVehicleDataRecord>> FetchVehicleByCodesAsync(
        int manufacturerCode,
        string modelCode);

    /// <summary>
    /// Parses drive type from government API field
    /// </summary>
    string ParseDriveType(string? hanaa_nm);

    /// <summary>
    /// Fetches all vehicle quantity records from the government API
    /// </summary>
    Task<List<VehicleQuantityRecord>> FetchAllVehicleQuantitiesAsync(
        int batchSize = 5000,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams registration batches from a specific resource via callback.
    /// Uses raw JsonElement because each resource has a different schema.
    /// </summary>
    Task FetchRegistrationBatchesAsync(
        string resourceId,
        int batchSize,
        int startOffset,
        Action<int, int>? progressCallback,
        Func<List<JsonElement>, Task> batchProcessor,
        CancellationToken cancellationToken);
}
