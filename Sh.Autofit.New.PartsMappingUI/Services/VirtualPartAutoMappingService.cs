using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;
using System.Text.Json;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class VirtualPartAutoMappingService : IVirtualPartAutoMappingService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

    public VirtualPartAutoMappingService(IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<VirtualPartMigrationCandidate>> DetectMigrationCandidatesForRealPartAsync(string realPartNumber)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get the real part's OEM numbers
        var realPart = await context.VwParts
            .FirstOrDefaultAsync(p => p.PartNumber == realPartNumber && p.IsActive == 1);

        if (realPart == null)
            return new List<VirtualPartMigrationCandidate>();

        var realPartOems = GetAllOemNumbers(
            realPart.Oemnumber1,
            realPart.Oemnumber2,
            realPart.Oemnumber3,
            realPart.Oemnumber4,
            realPart.Oemnumber5);

        // Find virtual parts with matching OEMs
        return await FindMatchingVirtualPartsAsync(realPart.PartNumber, realPart.PartName, realPartOems);
    }

    public async Task<List<VirtualPartMigrationCandidate>> DetectMigrationCandidatesAsync(List<string> realPartNumbers)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var allCandidates = new List<VirtualPartMigrationCandidate>();

        // Get all real parts
        var realParts = await context.VwParts
            .Where(p => realPartNumbers.Contains(p.PartNumber) && p.IsActive == 1)
            .ToListAsync();

        foreach (var realPart in realParts)
        {
            var realPartOems = GetAllOemNumbers(
                realPart.Oemnumber1,
                realPart.Oemnumber2,
                realPart.Oemnumber3,
                realPart.Oemnumber4,
                realPart.Oemnumber5);

            var candidates = await FindMatchingVirtualPartsAsync(
                realPart.PartNumber,
                realPart.PartName,
                realPartOems);

            allCandidates.AddRange(candidates);
        }

        return allCandidates;
    }

    public async Task<VirtualPartMigrationResult> MigrateVirtualPartAsync(
        int virtualPartId,
        string realPartNumber,
        string migratedBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var virtualPart = await context.VirtualParts.FindAsync(virtualPartId);
        if (virtualPart == null || !virtualPart.IsActive)
        {
            return new VirtualPartMigrationResult
            {
                Success = false,
                ErrorMessage = "Virtual part not found or already inactive"
            };
        }

        try
        {
            // Get all mappings for this virtual part
            var mappings = await context.VehiclePartsMappings
                .Where(m => m.PartItemKey == virtualPart.PartNumber && m.IsActive)
                .ToListAsync();

            // Update all mappings to point to real part
            foreach (var mapping in mappings)
            {
                mapping.PartItemKey = realPartNumber;
                mapping.UpdatedAt = DateTime.UtcNow;
                mapping.UpdatedBy = migratedBy;
            }

            // Create snapshot of virtual part for logging
            var virtualPartSnapshot = JsonSerializer.Serialize(new
            {
                virtualPart.VirtualPartId,
                virtualPart.PartNumber,
                virtualPart.PartName,
                virtualPart.OemNumber1,
                virtualPart.OemNumber2,
                virtualPart.OemNumber3,
                virtualPart.OemNumber4,
                virtualPart.OemNumber5,
                virtualPart.Category,
                virtualPart.Notes,
                virtualPart.CreatedAt,
                virtualPart.CreatedBy
            });

            // Collect matched OEM numbers for logging
            var virtualOems = GetAllOemNumbers(
                virtualPart.OemNumber1,
                virtualPart.OemNumber2,
                virtualPart.OemNumber3,
                virtualPart.OemNumber4,
                virtualPart.OemNumber5);

            var realPart = await context.VwParts
                .FirstOrDefaultAsync(p => p.PartNumber == realPartNumber && p.IsActive == 1);

            var realOems = realPart != null
                ? GetAllOemNumbers(
                    realPart.Oemnumber1,
                    realPart.Oemnumber2,
                    realPart.Oemnumber3,
                    realPart.Oemnumber4,
                    realPart.Oemnumber5)
                : new HashSet<string>();

            var matchedOems = virtualOems.Intersect(realOems, StringComparer.OrdinalIgnoreCase).ToList();

            // Log the migration
            var migrationLog = new VirtualPartMigrationLog
            {
                VirtualPartNumber = virtualPart.PartNumber,
                RealPartNumber = realPartNumber,
                MatchedOemNumbers = string.Join(", ", matchedOems),
                MappingsTransferred = mappings.Count,
                MigratedBy = migratedBy,
                MigratedAt = DateTime.UtcNow,
                VirtualPartData = virtualPartSnapshot
            };
            context.VirtualPartMigrationLogs.Add(migrationLog);

            // Soft delete virtual part
            virtualPart.IsActive = false;
            virtualPart.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();

            return new VirtualPartMigrationResult
            {
                Success = true,
                MappingsTransferred = mappings.Count,
                VirtualPartDeleted = true
            };
        }
        catch (Exception ex)
        {
            return new VirtualPartMigrationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<List<VirtualPartMigrationCandidate>> FindMatchingVirtualPartsAsync(
        string realPartNumber,
        string realPartName,
        HashSet<string> realPartOems)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var virtualParts = await context.VirtualParts
            .Where(vp => vp.IsActive)
            .ToListAsync();

        var candidates = new List<VirtualPartMigrationCandidate>();

        foreach (var virtualPart in virtualParts)
        {
            var virtualOems = GetAllOemNumbers(
                virtualPart.OemNumber1,
                virtualPart.OemNumber2,
                virtualPart.OemNumber3,
                virtualPart.OemNumber4,
                virtualPart.OemNumber5);

            // Check for ANY matching OEM
            var matchedOems = virtualOems
                .Where(voem => realPartOems.Contains(voem, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (matchedOems.Any())
            {
                // Count mappings that would be transferred
                var mappingCount = await context.VehiclePartsMappings
                    .CountAsync(m => m.PartItemKey == virtualPart.PartNumber && m.IsActive);

                candidates.Add(new VirtualPartMigrationCandidate
                {
                    VirtualPartId = virtualPart.VirtualPartId,
                    VirtualPartNumber = virtualPart.PartNumber,
                    VirtualPartName = virtualPart.PartName,
                    RealPartNumber = realPartNumber,
                    RealPartName = realPartName ?? string.Empty,
                    MatchedOemNumbers = matchedOems,
                    MappingsToTransfer = mappingCount
                });
            }
        }

        return candidates;
    }

    /// <summary>
    /// Extracts all OEM numbers from the 5 OEM fields
    /// OEM5 can contain multiple values separated by "/"
    /// </summary>
    private HashSet<string> GetAllOemNumbers(
        string? oem1,
        string? oem2,
        string? oem3,
        string? oem4,
        string? oem5)
    {
        var oems = new List<string>();

        // Add OEM 1-4 (single values)
        if (!string.IsNullOrWhiteSpace(oem1)) oems.Add(oem1.Trim());
        if (!string.IsNullOrWhiteSpace(oem2)) oems.Add(oem2.Trim());
        if (!string.IsNullOrWhiteSpace(oem3)) oems.Add(oem3.Trim());
        if (!string.IsNullOrWhiteSpace(oem4)) oems.Add(oem4.Trim());

        // OEM5 can contain multiple OEMs separated by "/"
        if (!string.IsNullOrWhiteSpace(oem5))
        {
            var splitOems = oem5.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            oems.AddRange(splitOems.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()));
        }

        return new HashSet<string>(oems, StringComparer.OrdinalIgnoreCase);
    }
}
