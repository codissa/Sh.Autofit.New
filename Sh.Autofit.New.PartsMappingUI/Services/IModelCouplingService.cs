using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IModelCouplingService
{
    /// <summary>
    /// Automatically couples consolidated models based on overlap logic
    /// Uses TWO scenarios only:
    /// 1. Small overlaps (≤3 years overlap)
    /// 2. Nested ranges (one range completely contains another)
    /// </summary>
    /// <param name="consolidatedModelIds">IDs of consolidated models to analyze for coupling</param>
    /// <param name="createdBy">User creating the couplings</param>
    /// <returns>Coupling result with statistics</returns>
    Task<CouplingResult> AutoCoupleModelsAsync(
        List<int> consolidatedModelIds,
        string createdBy);
}
