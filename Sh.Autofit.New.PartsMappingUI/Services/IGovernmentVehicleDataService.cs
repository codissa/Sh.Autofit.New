using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IGovernmentVehicleDataService
{
    /// <summary>
    /// Fetches all vehicle data from the government API
    /// </summary>
    /// <param name="batchSize">Number of records to fetch per request (default 1000)</param>
    /// <param name="progressCallback">Callback to report progress (current, total)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of government vehicle records</returns>
    Task<List<GovernmentVehicleDataRecord>> FetchAllVehicleDataAsync(
        int batchSize = 1000,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches vehicle data for specific manufacturer and model codes
    /// </summary>
    /// <param name="manufacturerCode">Government manufacturer code (tozeret_cd)</param>
    /// <param name="modelCode">Government model code (degem_cd)</param>
    /// <returns>List of matching government vehicle records</returns>
    Task<List<GovernmentVehicleDataRecord>> FetchVehicleByCodesAsync(
        int manufacturerCode,
        string modelCode);

    /// <summary>
    /// Parses drive type from government API field
    /// </summary>
    /// <param name="hanaa_nm">Drive type name from API (e.g., "הנעה רגילה", "4X4")</param>
    /// <returns>Standardized drive type (2WD, 4WD, AWD)</returns>
    string ParseDriveType(string? hanaa_nm);
}
