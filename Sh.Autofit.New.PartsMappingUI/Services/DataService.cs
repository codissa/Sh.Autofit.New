using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class DataService : IDataService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

    public DataService(IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<VehicleDisplayModel>> LoadVehiclesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var vehicles = await context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive)
            .Select(v => new VehicleDisplayModel
            {
                VehicleTypeId = v.VehicleTypeId,
                ManufacturerId = v.ManufacturerId,
                ManufacturerName = v.Manufacturer.ManufacturerName,
                ManufacturerShortName = v.Manufacturer.ManufacturerShortName ?? v.Manufacturer.ManufacturerName,
                ModelCode = v.ModelCode ?? string.Empty,
                ModelName = v.ModelName,
                YearFrom = v.YearFrom,
                YearTo = v.YearTo,
                VehicleCategory = v.VehicleCategory,
                CommercialName = v.CommercialName,
                FuelTypeName = v.FuelTypeName,
                EngineModel = v.EngineModel
            })
            .ToListAsync();

        return vehicles;
    }

    public async Task<List<(string ManufacturerShortName, string ManufacturerName, string CommercialName, int Count)>> LoadVehicleGroupSummaryAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var summary = await context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive)
            .GroupBy(v => new
            {
                ManufacturerShortName = v.Manufacturer.ManufacturerShortName ?? v.Manufacturer.ManufacturerName,
                ManufacturerName = v.Manufacturer.ManufacturerName,
                CommercialName = v.CommercialName ?? string.Empty
            })
            .Select(g => new
            {
                g.Key.ManufacturerShortName,
                g.Key.ManufacturerName,
                g.Key.CommercialName,
                Count = g.Count()
            })
            .ToListAsync();

        return summary.Select(s => (s.ManufacturerShortName, s.ManufacturerName, s.CommercialName, s.Count)).ToList();
    }

    public async Task<List<(string ModelName, int Count, int? YearFrom, int? YearTo)>> LoadModelGroupSummaryAsync(string manufacturerShortName, string commercialName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var summary = await context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive &&
                       (v.Manufacturer.ManufacturerShortName == manufacturerShortName ||
                        (v.Manufacturer.ManufacturerShortName == null && v.Manufacturer.ManufacturerName == manufacturerShortName)) &&
                       (string.IsNullOrEmpty(commercialName) ? (v.CommercialName == null || v.CommercialName == string.Empty) : v.CommercialName == commercialName))
            .GroupBy(v => v.ModelName)
            .Select(g => new
            {
                ModelName = g.Key,
                Count = g.Count(),
                YearFrom = (int?)g.Min(v => v.YearFrom),
                YearTo = (int?)g.Max(v => v.YearTo)
            })
            .ToListAsync();

        return summary.Select(s => (s.ModelName, s.Count, s.YearFrom, s.YearTo)).ToList();
    }

    public async Task<List<VehicleDisplayModel>> LoadVehiclesByModelAsync(string manufacturerShortName, string commercialName, string modelName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Handle empty commercial name case
        var vehicles = await context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive &&
                       (v.Manufacturer.ManufacturerShortName == manufacturerShortName ||
                        (v.Manufacturer.ManufacturerShortName == null && v.Manufacturer.ManufacturerName == manufacturerShortName)) &&
                       (string.IsNullOrEmpty(commercialName) ? (v.CommercialName == null || v.CommercialName == string.Empty) : v.CommercialName == commercialName) &&
                       v.ModelName == modelName)
            .Select(v => new VehicleDisplayModel
            {
                VehicleTypeId = v.VehicleTypeId,
                ManufacturerId = v.ManufacturerId,
                ManufacturerName = v.Manufacturer.ManufacturerName,
                ManufacturerShortName = v.Manufacturer.ManufacturerShortName ?? v.Manufacturer.ManufacturerName,
                ModelCode = v.ModelCode ?? string.Empty,
                ModelName = v.ModelName,
                YearFrom = v.YearFrom,
                YearTo = v.YearTo,
                VehicleCategory = v.VehicleCategory,
                CommercialName = v.CommercialName,
                FuelTypeName = v.FuelTypeName,
                EngineModel = v.EngineModel
            })
            .ToListAsync();

        return vehicles;
    }

    public async Task<List<PartDisplayModel>> LoadPartsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var parts = await context.VwParts
            .AsNoTracking()
            .Where(p => p.IsActive == 1)
            .Select(p => new PartDisplayModel
            {
                PartNumber = p.PartNumber ?? string.Empty,
                PartName = p.PartName ?? string.Empty,
                Category = p.Category,
                Manufacturer = p.Manufacturer,
                Model = p.Model,
                RetailPrice = p.RetailPrice,
                CostPrice = p.CostPrice,
                StockQuantity = p.StockQuantity,
                IsInStock = p.IsInStock == 1,
                IsActive = p.IsActive == 1,
                UniversalPart = p.UniversalPart,
                ImageUrl = p.ImageUrl,
                CompatibilityNotes = p.CompatibilityNotes,
                OemNumber1 = p.Oemnumber1,
                OemNumber2 = p.Oemnumber2,
                OemNumber3 = p.Oemnumber3,
                OemNumber4 = p.Oemnumber4,
                OemNumber5 = p.Oemnumber5
            })
            .ToListAsync();

        return parts;
    }

    public async Task<List<PartDisplayModel>> LoadMappedPartsAsync(int vehicleTypeId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get all part numbers mapped to this vehicle
        var mappedPartNumbers = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.VehicleTypeId == vehicleTypeId && m.IsActive && m.IsCurrentVersion)
            .Select(m => m.PartItemKey)
            .Distinct()
            .ToListAsync();

        if (!mappedPartNumbers.Any())
            return new List<PartDisplayModel>();

        // Load the part details
        var parts = await context.VwParts
            .AsNoTracking()
            .Where(p => mappedPartNumbers.Contains(p.PartNumber))
            .Select(p => new PartDisplayModel
            {
                PartNumber = p.PartNumber ?? string.Empty,
                PartName = p.PartName ?? string.Empty,
                Category = p.Category,
                Manufacturer = p.Manufacturer,
                Model = p.Model,
                RetailPrice = p.RetailPrice,
                CostPrice = p.CostPrice,
                StockQuantity = p.StockQuantity,
                IsInStock = p.IsInStock == 1,
                IsActive = p.IsActive == 1,
                UniversalPart = p.UniversalPart,
                ImageUrl = p.ImageUrl,
                CompatibilityNotes = p.CompatibilityNotes,
                OemNumber1 = p.Oemnumber1,
                OemNumber2 = p.Oemnumber2,
                OemNumber3 = p.Oemnumber3,
                OemNumber4 = p.Oemnumber4,
                OemNumber5 = p.Oemnumber5
            })
            .ToListAsync();

        return parts;
    }

    public async Task<List<PartDisplayModel>> LoadUnmappedPartsAsync(int vehicleTypeId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get all part numbers mapped to this vehicle
        var mappedPartNumbers = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.VehicleTypeId == vehicleTypeId && m.IsActive && m.IsCurrentVersion)
            .Select(m => m.PartItemKey)
            .Distinct()
            .ToListAsync();

        // Load all active parts that are NOT in the mapped list
        var parts = await context.VwParts
            .AsNoTracking()
            .Where(p => p.IsActive == 1 && !mappedPartNumbers.Contains(p.PartNumber))
            .Select(p => new PartDisplayModel
            {
                PartNumber = p.PartNumber ?? string.Empty,
                PartName = p.PartName ?? string.Empty,
                Category = p.Category,
                Manufacturer = p.Manufacturer,
                Model = p.Model,
                RetailPrice = p.RetailPrice,
                CostPrice = p.CostPrice,
                StockQuantity = p.StockQuantity,
                IsInStock = p.IsInStock == 1,
                IsActive = p.IsActive == 1,
                UniversalPart = p.UniversalPart,
                ImageUrl = p.ImageUrl,
                CompatibilityNotes = p.CompatibilityNotes,
                OemNumber1 = p.Oemnumber1,
                OemNumber2 = p.Oemnumber2,
                OemNumber3 = p.Oemnumber3,
                OemNumber4 = p.Oemnumber4,
                OemNumber5 = p.Oemnumber5
            })
            .ToListAsync();

        return parts;
    }

    public async Task<Dictionary<int, int>> LoadMappingCountsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var counts = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.IsActive && m.IsCurrentVersion)
            .GroupBy(m => m.VehicleTypeId)
            .Select(g => new { VehicleTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VehicleTypeId, x => x.Count);

        return counts;
    }

    public async Task<Dictionary<string, int>> LoadPartMappingCountsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var counts = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.IsActive && m.IsCurrentVersion)
            .GroupBy(m => m.PartItemKey)
            .Select(g => new { PartItemKey = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PartItemKey, x => x.Count);

        return counts;
    }

    public async Task MapPartsToVehiclesAsync(List<int> vehicleTypeIds, List<string> partNumbers, string createdBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Filter out invalid vehicle type IDs (placeholders will have ID = 0)
        var validVehicleTypeIds = vehicleTypeIds.Where(id => id > 0).Distinct().ToList();

        if (!validVehicleTypeIds.Any())
        {
            throw new InvalidOperationException("No valid vehicle type IDs provided.");
        }

        // Validate that all vehicle type IDs exist in the database
        var existingVehicleIds = await context.VehicleTypes
            .Where(v => validVehicleTypeIds.Contains(v.VehicleTypeId))
            .Select(v => v.VehicleTypeId)
            .ToListAsync();

        var invalidIds = validVehicleTypeIds.Except(existingVehicleIds).ToList();
        if (invalidIds.Any())
        {
            throw new InvalidOperationException($"The following vehicle type IDs do not exist: {string.Join(", ", invalidIds)}");
        }

        var existingMappings = await context.VehiclePartsMappings
            .Where(m => validVehicleTypeIds.Contains(m.VehicleTypeId) &&
                       partNumbers.Contains(m.PartItemKey) &&
                       m.IsActive &&
                       m.IsCurrentVersion)
            .Select(m => new { m.VehicleTypeId, m.PartItemKey })
            .ToListAsync();

        var existingKeys = existingMappings
            .Select(m => $"{m.VehicleTypeId}_{m.PartItemKey}")
            .ToHashSet();

        var newMappings = new List<VehiclePartsMapping>();

        foreach (var vehicleTypeId in validVehicleTypeIds)
        {
            foreach (var partNumber in partNumbers)
            {
                var key = $"{vehicleTypeId}_{partNumber}";
                if (!existingKeys.Contains(key))
                {
                    newMappings.Add(new VehiclePartsMapping
                    {
                        VehicleTypeId = vehicleTypeId,
                        PartItemKey = partNumber,
                        MappingSource = "Manual",
                        IsActive = true,
                        IsCurrentVersion = true,
                        VersionNumber = 1,
                        CreatedBy = createdBy,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        if (newMappings.Any())
        {
            await context.VehiclePartsMappings.AddRangeAsync(newMappings);
            await context.SaveChangesAsync();
        }
    }

    public async Task UnmapPartsFromVehiclesAsync(List<int> vehicleTypeIds, List<string> partNumbers, string updatedBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Filter out invalid vehicle type IDs (placeholders will have ID = 0)
        var validVehicleTypeIds = vehicleTypeIds.Where(id => id > 0).Distinct().ToList();

        if (!validVehicleTypeIds.Any())
        {
            throw new InvalidOperationException("No valid vehicle type IDs provided.");
        }

        var mappingsToDeactivate = await context.VehiclePartsMappings
            .Where(m => validVehicleTypeIds.Contains(m.VehicleTypeId) &&
                       partNumbers.Contains(m.PartItemKey) &&
                       m.IsActive &&
                       m.IsCurrentVersion)
            .ToListAsync();

        foreach (var mapping in mappingsToDeactivate)
        {
            mapping.IsActive = false;
            mapping.DeactivatedAt = DateTime.UtcNow;
            mapping.DeactivatedBy = updatedBy;
            mapping.DeactivationReason = "Manually unmapped via UI";
            mapping.UpdatedAt = DateTime.UtcNow;
        }

        if (mappingsToDeactivate.Any())
        {
            await context.SaveChangesAsync();
        }
    }

    public async Task CopyMappingsAsync(int sourceVehicleTypeId, List<int> targetVehicleTypeIds, string createdBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Validate source vehicle exists
        var sourceExists = await context.VehicleTypes
            .AnyAsync(v => v.VehicleTypeId == sourceVehicleTypeId && v.IsActive);

        if (!sourceExists)
        {
            throw new InvalidOperationException($"Source vehicle type ID {sourceVehicleTypeId} does not exist.");
        }

        // Filter out invalid target vehicle type IDs
        var validTargetVehicleTypeIds = targetVehicleTypeIds.Where(id => id > 0).Distinct().ToList();

        if (!validTargetVehicleTypeIds.Any())
        {
            throw new InvalidOperationException("No valid target vehicle type IDs provided.");
        }

        // Validate that all target vehicle type IDs exist
        var existingTargetIds = await context.VehicleTypes
            .Where(v => validTargetVehicleTypeIds.Contains(v.VehicleTypeId) && v.IsActive)
            .Select(v => v.VehicleTypeId)
            .ToListAsync();

        var invalidIds = validTargetVehicleTypeIds.Except(existingTargetIds).ToList();
        if (invalidIds.Any())
        {
            throw new InvalidOperationException($"The following target vehicle type IDs do not exist: {string.Join(", ", invalidIds)}");
        }

        // Get all active mappings from source vehicle
        var sourceMappings = await context.VehiclePartsMappings
            .Where(m => m.VehicleTypeId == sourceVehicleTypeId && m.IsActive && m.IsCurrentVersion)
            .Select(m => m.PartItemKey)
            .ToListAsync();

        if (!sourceMappings.Any())
        {
            throw new InvalidOperationException($"Source vehicle has no active part mappings to copy.");
        }

        // Get existing mappings for target vehicles to avoid duplicates
        var existingTargetMappings = await context.VehiclePartsMappings
            .Where(m => validTargetVehicleTypeIds.Contains(m.VehicleTypeId) &&
                       sourceMappings.Contains(m.PartItemKey) &&
                       m.IsActive &&
                       m.IsCurrentVersion)
            .Select(m => new { m.VehicleTypeId, m.PartItemKey })
            .ToListAsync();

        var existingKeys = existingTargetMappings
            .Select(m => $"{m.VehicleTypeId}_{m.PartItemKey}")
            .ToHashSet();

        // Create new mappings for target vehicles
        var newMappings = new List<VehiclePartsMapping>();

        foreach (var targetVehicleTypeId in validTargetVehicleTypeIds)
        {
            foreach (var partNumber in sourceMappings)
            {
                var key = $"{targetVehicleTypeId}_{partNumber}";
                if (!existingKeys.Contains(key))
                {
                    newMappings.Add(new VehiclePartsMapping
                    {
                        VehicleTypeId = targetVehicleTypeId,
                        PartItemKey = partNumber,
                        MappingSource = "Copied",
                        IsActive = true,
                        IsCurrentVersion = true,
                        VersionNumber = 1,
                        CreatedBy = createdBy,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        if (newMappings.Any())
        {
            await context.VehiclePartsMappings.AddRangeAsync(newMappings);
            await context.SaveChangesAsync();
        }
    }

    public async Task<int> GetTotalVehiclesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.VehicleTypes.CountAsync(v => v.IsActive);
    }

    public async Task<int> GetTotalPartsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.VwParts.CountAsync(p => p.IsActive == 1);
    }

    public async Task<int> GetMappedVehiclesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.VehiclePartsMappings
            .Where(m => m.IsActive && m.IsCurrentVersion)
            .Select(m => m.VehicleTypeId)
            .Distinct()
            .CountAsync();
    }

    public async Task<int> GetMappedPartsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.VehiclePartsMappings
            .Where(m => m.IsActive && m.IsCurrentVersion)
            .Select(m => m.PartItemKey)
            .Distinct()
            .CountAsync();
    }
}
