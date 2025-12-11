using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IVirtualPartAutoMappingService
{
    /// <summary>
    /// Detects if any virtual parts match a real part's OEM numbers
    /// </summary>
    /// <param name="realPartNumber">The real part number to check</param>
    /// <returns>List of migration candidates</returns>
    Task<List<VirtualPartMigrationCandidate>> DetectMigrationCandidatesForRealPartAsync(string realPartNumber);

    /// <summary>
    /// Checks all virtual parts against a list of real parts
    /// </summary>
    /// <param name="realPartNumbers">List of real part numbers</param>
    /// <returns>List of migration candidates</returns>
    Task<List<VirtualPartMigrationCandidate>> DetectMigrationCandidatesAsync(List<string> realPartNumbers);

    /// <summary>
    /// Migrates a virtual part to a real part
    /// - Transfers all mappings from virtual part to real part
    /// - Soft deletes the virtual part
    /// - Logs the migration
    /// </summary>
    Task<VirtualPartMigrationResult> MigrateVirtualPartAsync(
        int virtualPartId,
        string realPartNumber,
        string migratedBy);
}
