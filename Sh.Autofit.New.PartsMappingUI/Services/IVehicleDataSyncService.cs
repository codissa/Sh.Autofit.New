using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IVehicleDataSyncService
{
    /// <summary>
    /// Syncs all vehicles from government API to database
    /// </summary>
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

    /// <summary>
    /// Syncs vehicle quantities from government API to local database
    /// </summary>
    Task<VehicleDataSyncResult> SyncVehicleQuantitiesAsync(
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs vehicle registrations from all 6 government resources
    /// </summary>
    Task<VehicleDataSyncResult> SyncVehicleRegistrationsAsync(
        bool fullRefresh,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs all sync operations: WLTP specs, quantities, registrations, consolidation
    /// </summary>
    Task<VehicleDataSyncResult> SyncAllDataAsync(
        bool fullRefresh,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current sync statistics (record counts, last sync dates)
    /// </summary>
    Task<DataSyncStats> GetSyncStatsAsync();
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

/// <summary>
/// Statistics about synced data
/// </summary>
public class DataSyncStats
{
    public int QuantityCount { get; set; }
    public int TotalRegistrationCount { get; set; }
    public Dictionary<string, int> RegistrationCountByResource { get; set; } = new();
    public Dictionary<string, DateTime> LastSyncByDataset { get; set; } = new();
    public int VehicleTypeCount { get; set; }
    public int ConsolidatedModelCount { get; set; }
}
