using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IPendingVehicleReviewService
{
    /// <summary>
    /// Loads pending vehicles for review
    /// </summary>
    /// <param name="batchId">Optional batch ID to filter by</param>
    /// <returns>List of pending vehicles</returns>
    Task<List<PendingVehicleDisplayModel>> LoadPendingVehiclesAsync(Guid? batchId = null);

    /// <summary>
    /// Approves a single vehicle for processing
    /// </summary>
    Task ApproveVehicleAsync(int pendingVehicleId, string reviewedBy, string? notes = null);

    /// <summary>
    /// Rejects a single vehicle
    /// </summary>
    Task RejectVehicleAsync(int pendingVehicleId, string reviewedBy, string? notes = null);

    /// <summary>
    /// Bulk approves multiple vehicles
    /// </summary>
    Task ApproveBatchAsync(List<int> pendingVehicleIds, string reviewedBy);

    /// <summary>
    /// Bulk rejects multiple vehicles
    /// </summary>
    Task RejectBatchAsync(List<int> pendingVehicleIds, string reviewedBy);

    /// <summary>
    /// Processes approved vehicles - creates VehicleTypes, consolidated models, and couplings
    /// </summary>
    Task<VehicleProcessingResult> ProcessApprovedVehiclesAsync(
        Guid? batchId = null,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of pending vehicles
    /// </summary>
    Task<int> GetPendingCountAsync();
}
