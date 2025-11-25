using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IVehicleDataSyncService
{
    /// <summary>
    /// Syncs all vehicles from government API to database
    /// </summary>
    /// <param name="progressCallback">Progress callback (currentRecord, totalRecords, message)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sync statistics</returns>
    Task<VehicleDataSyncResult> SyncAllVehiclesAsync(
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs a single vehicle by manufacturer and model codes
    /// </summary>
    Task<VehicleDataSyncResult> SyncVehicleByCodesAsync(
        int manufacturerCode,
        string modelCode);

    /// <summary>
    /// Updates consolidated models with aggregated data from their variants
    /// </summary>
    Task UpdateConsolidatedModelsAsync();
}

/// <summary>
/// Result of vehicle data sync operation
/// </summary>
public class VehicleDataSyncResult
{
    public int TotalRecordsProcessed { get; set; }
    public int VehiclesMatched { get; set; }
    public int VehiclesUpdated { get; set; }
    public int VehiclesNotMatched { get; set; }
    public int NewVehiclesFound { get; set; }
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }

    public string GetSummary()
    {
        return $"סונכרנו {TotalRecordsProcessed:N0} רשומות | " +
               $"{VehiclesMatched:N0} הותאמו | " +
               $"{VehiclesUpdated:N0} עודכנו | " +
               $"{VehiclesNotMatched:N0} לא נמצאו | " +
               $"{NewVehiclesFound:N0} חדשים | " +
               $"זמן: {Duration.TotalMinutes:F1} דקות";
    }
}
