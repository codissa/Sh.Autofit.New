using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IVehicleDiscoveryService
{
    /// <summary>
    /// Discovers new vehicles from government API and saves them to PendingVehicleReviews
    /// </summary>
    /// <param name="progressCallback">Callback to report progress (current, total, message)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Discovery result with statistics</returns>
    Task<VehicleDiscoveryResult> DiscoverNewVehiclesAsync(
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default);
}

public class VehicleDiscoveryResult
{
    public int TotalRecordsChecked { get; set; }
    public int NewVehiclesFound { get; set; }
    public Guid BatchId { get; set; }
    public DateTime DiscoveryStartedAt { get; set; }
    public DateTime DiscoveryCompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
