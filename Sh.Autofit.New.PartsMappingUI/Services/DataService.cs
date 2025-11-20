using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Helpers;
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
                EngineModel = v.EngineModel,
                EngineVolume = v.EngineVolume,
                TransmissionType = v.TransmissionType,
                FinishLevel = v.FinishLevel,
                TrimLevel = v.TrimLevel
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

    public async Task<List<(string ModelName, int Count, int? YearFrom, int? YearTo, int? EngineVolume, string? FuelType, string? TransmissionType, string? TrimLevel)>> LoadModelGroupSummaryAsync(string manufacturerShortName, string commercialName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var vehicles = await context.VehicleTypes
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
                YearTo = (int?)g.Max(v => v.YearFrom),
                Vehicles = g.Select(v => new { v.EngineVolume, v.FuelTypeName, v.TransmissionType, v.TrimLevel }).ToList()
            })
            .ToListAsync();

        var result = new List<(string ModelName, int Count, int? YearFrom, int? YearTo, int? EngineVolume, string? FuelType, string? TransmissionType, string? TrimLevel)>();

        foreach (var v in vehicles)
        {
            // Group by engine volume, transmission, and trim level to create separate entries
            var groups = v.Vehicles
                .GroupBy(x => new { x.EngineVolume, x.TransmissionType, x.TrimLevel })
                .ToList();

            if (groups.Count > 1)
            {
                // SPLIT: Multiple combinations - create separate entry for each unique combination
                foreach (var group in groups.OrderBy(g => g.Key.EngineVolume).ThenBy(g => g.Key.TransmissionType).ThenBy(g => g.Key.TrimLevel))
                {
                    var fuelTypes = group.Where(x => !string.IsNullOrEmpty(x.FuelTypeName)).Select(x => x.FuelTypeName).Distinct().ToList();
                    string? uniformFuelType = fuelTypes.Count == 1 ? fuelTypes.First() : null;

                    result.Add((v.ModelName, group.Count(), v.YearFrom, v.YearTo, group.Key.EngineVolume, uniformFuelType, group.Key.TransmissionType, group.Key.TrimLevel));
                }
            }
            else
            {
                // Single combination - return as one entry
                var singleGroup = groups.First();
                var fuelTypes = singleGroup.Where(x => !string.IsNullOrEmpty(x.FuelTypeName)).Select(x => x.FuelTypeName).Distinct().ToList();
                string? uniformFuelType = fuelTypes.Count == 1 ? fuelTypes.First() : null;

                result.Add((v.ModelName, v.Count, v.YearFrom, v.YearTo, singleGroup.Key.EngineVolume, uniformFuelType, singleGroup.Key.TransmissionType, singleGroup.Key.TrimLevel));
            }
        }

        return result;
    }

    public async Task<List<VehicleDisplayModel>> LoadVehiclesByModelAsync(string manufacturerShortName, string commercialName, string modelName, int? engineVolume = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Build query with optional engine volume filter
        var query = context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive &&
                       (v.Manufacturer.ManufacturerShortName == manufacturerShortName ||
                        (v.Manufacturer.ManufacturerShortName == null && v.Manufacturer.ManufacturerName == manufacturerShortName)) &&
                       (string.IsNullOrEmpty(commercialName) ? (v.CommercialName == null || v.CommercialName == string.Empty) : v.CommercialName == commercialName) &&
                       v.ModelName == modelName);

        // Add engine volume filter if specified
        if (engineVolume.HasValue)
        {
            query = query.Where(v => v.EngineVolume == engineVolume.Value);
        }

        var vehicles = await query
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
                EngineModel = v.EngineModel,
                EngineVolume = v.EngineVolume,
                TransmissionType = v.TransmissionType,
                FinishLevel = v.FinishLevel,
                TrimLevel = v.TrimLevel
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

    public async Task<List<PartDisplayModel>> LoadUnmappedPartsByModelAsync(string manufacturerName, string modelName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Find all vehicle types with this manufacturer and model name
        var vehicleTypeIds = await context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive &&
                       (v.Manufacturer.ManufacturerName == manufacturerName ||
                        v.Manufacturer.ManufacturerShortName == manufacturerName) &&
                       v.ModelName == modelName)
            .Select(v => v.VehicleTypeId)
            .ToListAsync();

        if (!vehicleTypeIds.Any())
            return await context.VwParts
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

        // Get all part numbers mapped to ANY variant of this model
        var mappedPartNumbers = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => vehicleTypeIds.Contains(m.VehicleTypeId) && m.IsActive && m.IsCurrentVersion)
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

        // Get manufacturer, model names, engine volumes, transmission types, and trim levels for the selected vehicles
        var selectedVehicles = await context.VehicleTypes
            .Include(v => v.Manufacturer)
            .Where(v => validVehicleTypeIds.Contains(v.VehicleTypeId))
            .Select(v => new
            {
                v.VehicleTypeId,
                ManufacturerName = v.Manufacturer.ManufacturerName,
                v.ModelName,
                v.EngineVolume,
                v.TransmissionType,
                v.TrimLevel
            })
            .ToListAsync();

        if (!selectedVehicles.Any())
        {
            throw new InvalidOperationException("No valid vehicle type IDs provided.");
        }

        // Get unique combinations of model name, engine volume, transmission type, and trim level from selected vehicles
        var modelEngineGroups = selectedVehicles
            .Select(v => new { v.ModelName, v.EngineVolume, v.TransmissionType, v.TrimLevel })
            .Distinct()
            .ToList();

        // Find ALL vehicle IDs with the same ModelName, EngineVolume, TransmissionType, and TrimLevel from ANY manufacturer
        // This ensures parts are mapped to all years and all manufacturers with the same specifications
        // Fetch all active vehicles first, then filter in memory to avoid LINQ translation issues
        var allActiveVehicles = await context.VehicleTypes
            .Where(v => v.IsActive)
            .Select(v => new
            {
                v.VehicleTypeId,
                v.ModelName,
                v.EngineVolume,
                v.TransmissionType,
                v.TrimLevel
            })
            .ToListAsync();

        // Filter in memory to match the model/engine/transmission/trim combinations
        var allVehicleIdsToMap = allActiveVehicles
            .Where(v => modelEngineGroups.Any(g =>
                g.ModelName == v.ModelName &&
                (g.EngineVolume == null && v.EngineVolume == null || g.EngineVolume == v.EngineVolume) &&
                (g.TransmissionType == null && v.TransmissionType == null || g.TransmissionType == v.TransmissionType) &&
                (g.TrimLevel == null && v.TrimLevel == null || g.TrimLevel == v.TrimLevel)))
            .Select(v => v.VehicleTypeId)
            .ToList();

        validVehicleTypeIds = allVehicleIdsToMap.Distinct().ToList();

        // Clean up old inactive mappings first to avoid constraint issues on re-mapping
        var oldInactiveMappings = await context.VehiclePartsMappings
            .Where(m => validVehicleTypeIds.Contains(m.VehicleTypeId) &&
                       partNumbers.Contains(m.PartItemKey) &&
                       !m.IsActive &&
                       !m.IsCurrentVersion)
            .ToListAsync();

        if (oldInactiveMappings.Any())
        {
            context.VehiclePartsMappings.RemoveRange(oldInactiveMappings);
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

        // Get manufacturer, model names, engine volumes, transmission types, and trim levels for the selected vehicles
        var selectedVehicles = await context.VehicleTypes
            .Include(v => v.Manufacturer)
            .Where(v => validVehicleTypeIds.Contains(v.VehicleTypeId))
            .Select(v => new
            {
                v.VehicleTypeId,
                ManufacturerName = v.Manufacturer.ManufacturerName,
                v.ModelName,
                v.EngineVolume,
                v.TransmissionType,
                v.TrimLevel
            })
            .ToListAsync();

        if (!selectedVehicles.Any())
        {
            throw new InvalidOperationException("No valid vehicle type IDs provided.");
        }

        // Get unique combinations of model name, engine volume, transmission type, and trim level from selected vehicles
        var modelEngineGroups = selectedVehicles
            .Select(v => new { v.ModelName, v.EngineVolume, v.TransmissionType, v.TrimLevel })
            .Distinct()
            .ToList();

        // Find ALL vehicle IDs with the same ModelName, EngineVolume, TransmissionType, and TrimLevel from ANY manufacturer
        // This ensures parts are unmapped from all years and all manufacturers with the same specifications
        // Fetch all active vehicles first, then filter in memory to avoid LINQ translation issues
        var allActiveVehicles = await context.VehicleTypes
            .Where(v => v.IsActive)
            .Select(v => new
            {
                v.VehicleTypeId,
                v.ModelName,
                v.EngineVolume,
                v.TransmissionType,
                v.TrimLevel
            })
            .ToListAsync();

        // Filter in memory to match the model/engine/transmission/trim combinations
        var allVehicleIdsToUnmap = allActiveVehicles
            .Where(v => modelEngineGroups.Any(g =>
                g.ModelName == v.ModelName &&
                (g.EngineVolume == null && v.EngineVolume == null || g.EngineVolume == v.EngineVolume) &&
                (g.TransmissionType == null && v.TransmissionType == null || g.TransmissionType == v.TransmissionType) &&
                (g.TrimLevel == null && v.TrimLevel == null || g.TrimLevel == v.TrimLevel)))
            .Select(v => v.VehicleTypeId)
            .ToList();

        validVehicleTypeIds = allVehicleIdsToUnmap.Distinct().ToList();

        // First, delete any old inactive mappings to avoid unique constraint violations
        var oldInactiveMappings = await context.VehiclePartsMappings
            .Where(m => validVehicleTypeIds.Contains(m.VehicleTypeId) &&
                       partNumbers.Contains(m.PartItemKey) &&
                       !m.IsActive &&
                       !m.IsCurrentVersion)
            .ToListAsync();

        if (oldInactiveMappings.Any())
        {
            context.VehiclePartsMappings.RemoveRange(oldInactiveMappings);
        }

        // Now get the active mappings to deactivate
        var mappingsToDeactivate = await context.VehiclePartsMappings
            .Where(m => validVehicleTypeIds.Contains(m.VehicleTypeId) &&
                       partNumbers.Contains(m.PartItemKey) &&
                       m.IsActive &&
                       m.IsCurrentVersion)
            .ToListAsync();

        foreach (var mapping in mappingsToDeactivate)
        {
            mapping.IsActive = false;
            mapping.IsCurrentVersion = false;
            mapping.DeactivatedAt = DateTime.UtcNow;
            mapping.DeactivatedBy = updatedBy;
            mapping.DeactivationReason = "Manually unmapped via UI";
            mapping.UpdatedAt = DateTime.UtcNow;
        }

        if (oldInactiveMappings.Any() || mappingsToDeactivate.Any())
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

        // Get manufacturer and model names for the target vehicles
        var targetVehicles = await context.VehicleTypes
            .Include(v => v.Manufacturer)
            .Where(v => validTargetVehicleTypeIds.Contains(v.VehicleTypeId))
            .Select(v => new
            {
                v.VehicleTypeId,
                ManufacturerName = v.Manufacturer.ManufacturerName,
                v.ModelName
            })
            .ToListAsync();

        // Get unique model names from target vehicles
        var modelNames = targetVehicles
            .Select(v => v.ModelName)
            .Distinct()
            .ToList();

        // Find ALL vehicle IDs with the same ModelName from ANY manufacturer
        // This ensures mappings are copied to all years and all manufacturers with the same model name
        var allTargetVehicleIds = await context.VehicleTypes
            .Where(v => v.IsActive && modelNames.Contains(v.ModelName))
            .Select(v => v.VehicleTypeId)
            .ToListAsync();

        validTargetVehicleTypeIds = allTargetVehicleIds.Distinct().ToList();

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

    public async Task CopyPartMappingsAsync(string sourcePartNumber, List<string> targetPartNumbers, string createdBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        if (string.IsNullOrWhiteSpace(sourcePartNumber))
        {
            throw new InvalidOperationException("Source part number is required.");
        }

        if (!targetPartNumbers.Any())
        {
            throw new InvalidOperationException("No target part numbers provided.");
        }

        // Get all active vehicle mappings from source part
        var sourceVehicleMappings = await context.VehiclePartsMappings
            .Where(m => m.PartItemKey == sourcePartNumber && m.IsActive && m.IsCurrentVersion)
            .Select(m => m.VehicleTypeId)
            .ToListAsync();

        if (!sourceVehicleMappings.Any())
        {
            throw new InvalidOperationException($"Source part '{sourcePartNumber}' has no active vehicle mappings to copy.");
        }

        // Get existing mappings for target parts to avoid duplicates
        var existingTargetMappings = await context.VehiclePartsMappings
            .Where(m => targetPartNumbers.Contains(m.PartItemKey) &&
                       sourceVehicleMappings.Contains(m.VehicleTypeId) &&
                       m.IsActive &&
                       m.IsCurrentVersion)
            .Select(m => new { m.VehicleTypeId, m.PartItemKey })
            .ToListAsync();

        var existingKeys = existingTargetMappings
            .Select(m => $"{m.VehicleTypeId}_{m.PartItemKey}")
            .ToHashSet();

        // Create new mappings for target parts
        var newMappings = new List<VehiclePartsMapping>();

        foreach (var targetPartNumber in targetPartNumbers)
        {
            foreach (var vehicleTypeId in sourceVehicleMappings)
            {
                var key = $"{vehicleTypeId}_{targetPartNumber}";
                if (!existingKeys.Contains(key))
                {
                    newMappings.Add(new VehiclePartsMapping
                    {
                        VehicleTypeId = vehicleTypeId,
                        PartItemKey = targetPartNumber,
                        MappingSource = "Copied from Part",
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

    public async Task<VehicleDisplayModel> CreateVehicleTypeFromGovernmentRecordAsync(GovernmentVehicleRecord govRecord)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // First, find or create the manufacturer (case-insensitive search with trim)
        var manufacturerName = (govRecord.ManufacturerName ?? "Unknown").Trim();
        var manufacturer = await context.Manufacturers
            .FirstOrDefaultAsync(m => m.ManufacturerName.ToLower().Trim() == manufacturerName.ToLower());

        if (manufacturer == null)
        {
            try
            {
                // Get the next available manufacturer code
                var maxCode = await context.Manufacturers
                    .AsNoTracking()
                    .MaxAsync(m => (int?)m.ManufacturerCode) ?? 0;

                // Create new manufacturer
                manufacturer = new Manufacturer
                {
                    ManufacturerCode = maxCode + 1,
                    ManufacturerName = manufacturerName,
                    ManufacturerShortName = manufacturerName,
                    CountryOfOrigin = "Unknown",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Manufacturers.Add(manufacturer);
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Race condition: another request created the same manufacturer
                // Reload the manufacturer from the database
                manufacturer = await context.Manufacturers
                    .FirstOrDefaultAsync(m => m.ManufacturerName.ToLower().Trim() == manufacturerName.ToLower());

                if (manufacturer == null)
                {
                    throw; // Re-throw if still not found (unexpected error)
                }
            }
        }

        // Create the vehicle type
        var vehicleType = new VehicleType
        {
            ManufacturerId = manufacturer.ManufacturerId,
            ModelCode = govRecord.ModelCode?.ToString() ?? "",
            ModelName = govRecord.ModelName ?? "Unknown",
            CommercialName = govRecord.CommercialName ?? "",
            FinishLevel = govRecord.TrimLevel ?? "",
            YearFrom = govRecord.ManufacturingYear ?? DateTime.Now.Year,
            YearTo = null,
            EngineVolume = govRecord.EngineVolume,
            TotalWeight = null,
            EngineModel = govRecord.EngineModel ?? "",
            FuelTypeCode = null,
            FuelTypeName = govRecord.FuelType ?? "",
            TransmissionType = "",
            NumberOfDoors = null,
            NumberOfSeats = null,
            Horsepower = null,
            TrimLevel = govRecord.TrimLevel ?? "",
            VehicleCategory = "",
            EmissionGroup = govRecord.PollutionGroup,
            GreenIndex = null,
            SafetyRating = null,
            SafetyLevel = null,
            FrontTireSize = govRecord.FrontTire ?? "",
            RearTireSize = govRecord.RearTire ?? "",
            AdditionalSpecs = "Auto-created from government API",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow
        };

        context.VehicleTypes.Add(vehicleType);
        await context.SaveChangesAsync();

        // Return as VehicleDisplayModel
        return new VehicleDisplayModel
        {
            VehicleTypeId = vehicleType.VehicleTypeId,
            ManufacturerId = vehicleType.ManufacturerId,
            ManufacturerName = manufacturer.ManufacturerName,
            ManufacturerShortName = manufacturer.ManufacturerShortName ?? manufacturer.ManufacturerName,
            ModelCode = vehicleType.ModelCode,
            ModelName = vehicleType.ModelName,
            YearFrom = vehicleType.YearFrom,
            YearTo = vehicleType.YearTo,
            VehicleCategory = vehicleType.VehicleCategory,
            CommercialName = vehicleType.CommercialName,
            FuelTypeName = vehicleType.FuelTypeName,
            EngineModel = vehicleType.EngineModel,
            EngineVolume = vehicleType.EngineVolume
        };
    }

    public async Task<List<PartDisplayModel>> LoadMappedPartsByModelNameAsync(string manufacturerName, string modelName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Find all vehicle types with this manufacturer and model name
        var vehicleTypeIds = await context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive &&
                       (v.Manufacturer.ManufacturerName == manufacturerName ||
                        v.Manufacturer.ManufacturerShortName == manufacturerName) &&
                       v.ModelName == modelName)
            .Select(v => v.VehicleTypeId)
            .ToListAsync();

        if (!vehicleTypeIds.Any())
            return new List<PartDisplayModel>();

        // Get all mapped parts for these vehicle types
        var parts = await context.VehiclePartsMappings
            .AsNoTracking()
            .Include(m => m.PartItemKeyNavigation)
            .Where(m => vehicleTypeIds.Contains(m.VehicleTypeId) &&
                       m.IsActive &&
                       m.PartItemKeyNavigation != null &&
                       m.PartItemKeyNavigation.IsActive == 1)
            .Select(m => m.PartItemKeyNavigation)
            .Distinct()
            .Select(p => new PartDisplayModel
            {
                PartNumber = p.PartNumber,
                PartName = p.PartName ?? "",
                Category = p.Category != null ? p.Category : "",
                Manufacturer = p.Manufacturer ?? "",
                IsInStock = p.StockQuantity > 0,
                UniversalPart = p.UniversalPart ,
                MappedVehiclesCount = context.VehiclePartsMappings
                    .Count(m => m.PartItemKey == p.PartNumber && m.IsActive)
            })
            .ToListAsync();

        return parts;
    }

    // ===== Vehicle Registration Caching (Task 5) =====

    public async Task<VehicleRegistration?> GetCachedRegistrationAsync(string licensePlate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.VehicleRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.LicensePlate == licensePlate);
    }

    public async Task<VehicleRegistration> UpsertVehicleRegistrationAsync(
        string licensePlate,
        GovernmentVehicleRecord? govRecord,
        int? matchedVehicleTypeId,
        int? matchedManufacturerId,
        string matchStatus,
        string matchReason,
        string apiResourceUsed)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var existing = await context.VehicleRegistrations
            .FirstOrDefaultAsync(r => r.LicensePlate == licensePlate);

        if (existing != null)
        {
            // Update existing record
            existing.LastLookupDate = DateTime.Now;
            existing.LookupCount++;
            existing.VehicleTypeId = matchedVehicleTypeId;
            existing.ManufacturerId = matchedManufacturerId;
            existing.MatchStatus = matchStatus;
            existing.MatchReason = matchReason;
            existing.ApiResourceUsed = apiResourceUsed;

            if (govRecord != null)
            {
                existing.GovManufacturerName = govRecord.ManufacturerName;
                existing.GovModelName = govRecord.ModelName;
                existing.GovEngineVolume = govRecord.EngineVolume;
                existing.GovFuelType = govRecord.FuelType;
                existing.GovYear = govRecord.ManufacturingYear;
                existing.RegistrationYear = govRecord.ManufacturingYear;
                existing.Color = govRecord.ColorName;
                existing.CurrentOwner = govRecord.OwnershipType;
                existing.Vin = govRecord.VinNumber ?? govRecord.VinChassis;
                existing.ApiResponseJson = System.Text.Json.JsonSerializer.Serialize(govRecord);
            }

            await context.SaveChangesAsync();
            return existing;
        }
        else
        {
            // Create new record
            var newRegistration = new VehicleRegistration
            {
                LicensePlate = licensePlate,
                VehicleTypeId = matchedVehicleTypeId,
                ManufacturerId = matchedManufacturerId,
                MatchStatus = matchStatus,
                MatchReason = matchReason,
                ApiResourceUsed = apiResourceUsed,
                FirstLookupDate = DateTime.Now,
                LastLookupDate = DateTime.Now,
                LookupCount = 1,
                IsActive = true
            };

            if (govRecord != null)
            {
                newRegistration.GovManufacturerName = govRecord.ManufacturerName;
                newRegistration.GovModelName = govRecord.ModelName;
                newRegistration.GovEngineVolume = govRecord.EngineVolume;
                newRegistration.GovFuelType = govRecord.FuelType;
                newRegistration.GovYear = govRecord.ManufacturingYear;
                newRegistration.RegistrationYear = govRecord.ManufacturingYear;
                newRegistration.Color = govRecord.ColorName;
                newRegistration.CurrentOwner = govRecord.OwnershipType;
                newRegistration.Vin = govRecord.VinNumber ?? govRecord.VinChassis;
                newRegistration.ApiResponseJson = System.Text.Json.JsonSerializer.Serialize(govRecord);
            }

            context.VehicleRegistrations.Add(newRegistration);
            await context.SaveChangesAsync();
            return newRegistration;
        }
    }

    // ===== Analytics (Task 5) =====

    public async Task<int> GetTotalRegistrationLookupsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.VehicleRegistrations
            .AsNoTracking()
            .SumAsync(r => r.LookupCount);
    }

    public async Task<int> GetMatchedRegistrationsCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.VehicleRegistrations
            .AsNoTracking()
            .CountAsync(r => r.MatchStatus == "Matched" || r.MatchStatus == "AutoCreated");
    }

    public async Task<int> GetUnmatchedRegistrationsCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.VehicleRegistrations
            .AsNoTracking()
            .CountAsync(r => r.MatchStatus == "NotInOurDB" || r.MatchStatus == "NotFoundInGovAPI");
    }

    public async Task<List<VehicleRegistration>> GetUnmatchedRegistrationsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.VehicleRegistrations
            .AsNoTracking()
            .Include(r => r.Manufacturer)
            .Include(r => r.VehicleType)
            .Where(r => r.MatchStatus == "NotInOurDB" || r.MatchStatus == "NotFoundInGovAPI")
            .OrderByDescending(r => r.LastLookupDate)
            .ToListAsync();
    }

    public async Task<List<(string ModelName, int Count)>> GetMostSearchedModelsAsync(int topCount = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var results = await context.VehicleRegistrations
            .AsNoTracking()
            .Where(r => r.GovModelName != null)
            .GroupBy(r => r.GovModelName)
            .Select(g => new
            {
                ModelName = g.Key,
                Count = g.Sum(r => r.LookupCount)
            })
            .OrderByDescending(x => x.Count)
            .Take(topCount)
            .ToListAsync();

        return results.Select(r => (r.ModelName ?? "Unknown", r.Count)).ToList();
    }

    public async Task<List<(string LicensePlate, int Count, DateTime LastLookup)>> GetMostSearchedPlatesAsync(int topCount = 10)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var results = await context.VehicleRegistrations
            .AsNoTracking()
            .OrderByDescending(r => r.LookupCount)
            .ThenByDescending(r => r.LastLookupDate)
            .Take(topCount)
            .Select(r => new
            {
                r.LicensePlate,
                r.LookupCount,
                r.LastLookupDate
            })
            .ToListAsync();

        return results.Select(r => (r.LicensePlate, r.LookupCount, r.LastLookupDate)).ToList();
    }

    // ===== Item Management (Task 2) =====

    public async Task<List<VehicleDisplayModel>> LoadVehiclesForPartAsync(string partNumber)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Efficient query: get only vehicles mapped to this part
        var vehicles = await context.VehiclePartsMappings
            .AsNoTracking()
            .Include(m => m.VehicleType)
                .ThenInclude(v => v.Manufacturer)
            .Where(m => m.PartItemKey == partNumber && m.IsActive)
            .Select(m => m.VehicleType)
            .Distinct()
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
                EngineModel = v.EngineModel,
                EngineVolume = v.EngineVolume,
                TransmissionType = v.TransmissionType,
                FinishLevel = v.FinishLevel,
                TrimLevel = v.TrimLevel
            })
            .ToListAsync();

        return vehicles;
    }

    public async Task<List<VehicleDisplayModel>> GetSuggestedVehiclesForPartAsync(string partNumber)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Step 1: Get vehicles already mapped to this part
        var mappedVehicleIds = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.PartItemKey == partNumber && m.IsActive)
            .Select(m => m.VehicleTypeId)
            .Distinct()
            .ToListAsync();

        if (mappedVehicleIds.Count == 0)
        {
            return new List<VehicleDisplayModel>(); // No suggestions if part has no mappings yet
        }

        // Step 2: Get characteristics of mapped vehicles
        var mappedVehicles = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => mappedVehicleIds.Contains(v.VehicleTypeId))
            .ToListAsync();

        // Step 3: Find similar vehicles that are NOT already mapped
        var commercialNames = mappedVehicles.Select(v => v.CommercialName).Distinct().ToList();
        var engineVolumes = mappedVehicles.Select(v => v.EngineVolume).Distinct().ToList();
        var fuelTypes = mappedVehicles.Select(v => v.FuelTypeName).Distinct().ToList();

        // Find similar vehicles
        var suggestions = await context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive &&
                       !mappedVehicleIds.Contains(v.VehicleTypeId) && // Not already mapped
                       (commercialNames.Contains(v.CommercialName) || // Same commercial name
                        engineVolumes.Contains(v.EngineVolume) ||    // Same engine volume
                        fuelTypes.Contains(v.FuelTypeName)))          // Same fuel type
            .Select(v => new
            {
                Vehicle = v,
                // Calculate relevance score
                Score = (commercialNames.Contains(v.CommercialName) ? 3 : 0) +
                       (engineVolumes.Contains(v.EngineVolume) ? 2 : 0) +
                       (fuelTypes.Contains(v.FuelTypeName) ? 1 : 0)
            })
            .Where(x => x.Score >= 2) // Require at least 2 matching criteria
            .OrderByDescending(x => x.Score)
            .Take(50) // Limit to top 50 suggestions
            .Select(x => new VehicleDisplayModel
            {
                VehicleTypeId = x.Vehicle.VehicleTypeId,
                ManufacturerId = x.Vehicle.ManufacturerId,
                ManufacturerName = x.Vehicle.Manufacturer.ManufacturerName,
                ManufacturerShortName = x.Vehicle.Manufacturer.ManufacturerShortName ?? x.Vehicle.Manufacturer.ManufacturerName,
                ModelCode = x.Vehicle.ModelCode ?? string.Empty,
                ModelName = x.Vehicle.ModelName,
                YearFrom = x.Vehicle.YearFrom,
                YearTo = x.Vehicle.YearTo,
                VehicleCategory = x.Vehicle.VehicleCategory,
                CommercialName = x.Vehicle.CommercialName,
                FuelTypeName = x.Vehicle.FuelTypeName,
                EngineModel = x.Vehicle.EngineModel,
                EngineVolume = x.Vehicle.EngineVolume,
                IsSuggestion = true,
                SuggestionReason = x.Score == 6 ? "התאמה מלאה" :
                                  x.Score == 5 ? "התאמה גבוהה" :
                                  x.Score >= 3 ? "התאמה בינונית" : "התאמה אפשרית"
            })
            .ToListAsync();

        return suggestions;
    }

    public async Task<List<PartDisplayModel>> GetSuggestedPartsForModelAsync(string manufacturerName, string modelName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Step 1: Get all vehicles for this model
        var modelVehicleIds = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => v.Manufacturer.ManufacturerName == manufacturerName &&
                       v.ModelName == modelName &&
                       v.IsActive)
            .Select(v => v.VehicleTypeId)
            .ToListAsync();

        if (modelVehicleIds.Count == 0)
        {
            return new List<PartDisplayModel>();
        }

        // Step 2: Get parts already mapped to this model
        var mappedPartNumbers = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => modelVehicleIds.Contains(m.VehicleTypeId) && m.IsActive)
            .Select(m => m.PartItemKey)
            .Distinct()
            .ToListAsync();

        // Step 3: Get characteristics of this model's vehicles
        var modelVehicles = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => modelVehicleIds.Contains(v.VehicleTypeId))
            .ToListAsync();

        // Normalize commercial names for comparison (ignore whitespace)
        var commercialNamesNormalized = modelVehicles
            .Select(v => v.CommercialName.NormalizeForGrouping())
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .ToHashSet();

        var modelNameNormalized = modelName.NormalizeForGrouping();
        var engineVolumes = modelVehicles.Select(v => v.EngineVolume).Where(e => e.HasValue).Distinct().ToList();
        var fuelTypes = modelVehicles.Select(v => v.FuelTypeName).Where(f => !string.IsNullOrEmpty(f)).Distinct().ToList();
        var yearFrom = modelVehicles.Min(v => v.YearFrom);
        var yearTo = modelVehicles.Max(v => v.YearFrom);
        var manufacturerId = modelVehicles.First().ManufacturerId;

        // Step 4: Find similar vehicles with weighted scoring
        // Fetch candidate vehicles with engine volume and year overlap, then filter by normalized names in-memory
        var candidateVehicles = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => v.IsActive &&
                       !modelVehicleIds.Contains(v.VehicleTypeId) &&
                       engineVolumes.Contains(v.EngineVolume) &&
                       ((v.YearFrom <= yearTo && v.YearFrom >= yearFrom) ||
                        (v.YearFrom >= yearFrom && v.YearFrom <= yearTo)))
            .Select(v => new { v.VehicleTypeId, v.ModelName, v.CommercialName })
            .ToListAsync();

        // Strategy 1: Same commercial name + overlapping years + same engine volume (HIGHEST SCORE - 10 points)
        var strategy1Vehicles = candidateVehicles
            .Where(v => !v.ModelName.EqualsIgnoringWhitespace(modelName) && // Different model name
                       commercialNamesNormalized.Contains(v.CommercialName.NormalizeForGrouping()))
            .ToList();

        // Strategy 2: Same engine volume + overlapping years + different commercial name (HIGH SCORE - 6 points)
        var strategy2Vehicles = candidateVehicles
            .Where(v => !v.ModelName.EqualsIgnoringWhitespace(modelName) &&
                       !commercialNamesNormalized.Contains(v.CommercialName.NormalizeForGrouping()))
            .ToList();

        // Strategy 3: Same manufacturer + same engine volume (MEDIUM SCORE - 3 points)
        var strategy3Candidates = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => v.IsActive &&
                       !modelVehicleIds.Contains(v.VehicleTypeId) &&
                       v.ManufacturerId == manufacturerId &&
                       engineVolumes.Contains(v.EngineVolume))
            .Select(v => new { v.VehicleTypeId, v.ModelName, v.CommercialName })
            .ToListAsync();

        var strategy3Vehicles = strategy3Candidates
            .Where(v => !v.ModelName.EqualsIgnoringWhitespace(modelName))
            .ToList();

        // Strategy 4: Same commercial name (BASE SCORE - 2 points)
        var strategy4Candidates = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => v.IsActive &&
                       !modelVehicleIds.Contains(v.VehicleTypeId))
            .Select(v => new { v.VehicleTypeId, v.ModelName, v.CommercialName })
            .ToListAsync();

        var strategy4Vehicles = strategy4Candidates
            .Where(v => !v.ModelName.EqualsIgnoringWhitespace(modelName) &&
                       commercialNamesNormalized.Contains(v.CommercialName.NormalizeForGrouping()))
            .ToList();

        // Combine all strategies and calculate weighted scores per vehicle
        var vehicleScores = new Dictionary<int, (int Score, string Reason, string SourceModel)>();

        foreach (var vehicle in strategy1Vehicles)
        {
            if (!vehicleScores.ContainsKey(vehicle.VehicleTypeId))
                vehicleScores[vehicle.VehicleTypeId] = (10, "שם מסחרי + נפח מנוע + שנים תואמות", vehicle.ModelName ?? "");
        }

        foreach (var vehicle in strategy2Vehicles)
        {
            if (!vehicleScores.ContainsKey(vehicle.VehicleTypeId))
                vehicleScores[vehicle.VehicleTypeId] = (6, "נפח מנוע + שנים תואמות", vehicle.ModelName ?? "");
        }

        foreach (var vehicle in strategy3Vehicles)
        {
            if (!vehicleScores.ContainsKey(vehicle.VehicleTypeId))
                vehicleScores[vehicle.VehicleTypeId] = (3, "אותו יצרן + נפח מנוע תואם", vehicle.ModelName ?? "");
        }

        foreach (var vehicle in strategy4Vehicles)
        {
            if (!vehicleScores.ContainsKey(vehicle.VehicleTypeId))
                vehicleScores[vehicle.VehicleTypeId] = (2, "שם מסחרי תואם", vehicle.ModelName ?? "");
        }

        if (vehicleScores.Count == 0)
        {
            return new List<PartDisplayModel>();
        }

        var similarVehicleIds = vehicleScores.Keys.ToList();

        // Step 5: Get parts mapped to similar vehicles with weighted scoring
        var partScores = new Dictionary<string, (int TotalScore, List<string> SourceModels, string BestReason)>();

        var vehicleMappings = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => similarVehicleIds.Contains(m.VehicleTypeId) &&
                       m.IsActive &&
                       !mappedPartNumbers.Contains(m.PartItemKey))
            .ToListAsync();

        foreach (var mapping in vehicleMappings)
        {
            var vehicleScore = vehicleScores[mapping.VehicleTypeId];

            if (!partScores.ContainsKey(mapping.PartItemKey))
            {
                partScores[mapping.PartItemKey] = (vehicleScore.Score, new List<string> { vehicleScore.SourceModel }, vehicleScore.Reason);
            }
            else
            {
                var current = partScores[mapping.PartItemKey];
                current.SourceModels.Add(vehicleScore.SourceModel);
                partScores[mapping.PartItemKey] = (
                    current.TotalScore + vehicleScore.Score,
                    current.SourceModels,
                    vehicleScore.Score > GetScoreFromReason(current.BestReason) ? vehicleScore.Reason : current.BestReason
                );
            }
        }

        if (partScores.Count == 0)
        {
            return new List<PartDisplayModel>();
        }

        // Get top suggestions
        var topSuggestions = partScores
            .OrderByDescending(x => x.Value.TotalScore)
            .Take(50)
            .ToList();

        var suggestedPartNumbers = topSuggestions.Select(s => s.Key).ToList();

        // Step 6: Get full part information
        var suggestions = await context.VwParts
            .AsNoTracking()
            .Where(p => suggestedPartNumbers.Contains(p.PartNumber) && p.IsActive == 1)
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
                OemNumber5 = p.Oemnumber5,
                HasSuggestion = true,
                RelevanceScore = 0 // Will be calculated
            })
            .ToListAsync();

        // Calculate relevance scores and reasons
        foreach (var part in suggestions)
        {
            var partScore = partScores[part.PartNumber];
            part.RelevanceScore = partScore.TotalScore;

            var uniqueModels = partScore.SourceModels.Distinct().ToList();
            var modelCount = uniqueModels.Count;
            var modelsList = modelCount <= 3
                ? string.Join(", ", uniqueModels)
                : $"{uniqueModels.Count} דגמים";

            part.RelevanceReason = $"{partScore.BestReason} ({modelsList})";
        }

        return suggestions.OrderByDescending(p => p.RelevanceScore).ToList();
    }

    private int GetScoreFromReason(string reason)
    {
        if (reason.Contains("שם מסחרי + נפח מנוע + שנים")) return 10;
        if (reason.Contains("נפח מנוע + שנים")) return 6;
        if (reason.Contains("אותו יצרן + נפח מנוע")) return 3;
        if (reason.Contains("שם מסחרי תואם")) return 2;
        return 1;
    }

    public async Task<List<PartDisplayModel>> GetSuggestedPartsForVehicleAsync(int vehicleTypeId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Step 1: Get the specific vehicle details
        var targetVehicle = await context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .FirstOrDefaultAsync(v => v.VehicleTypeId == vehicleTypeId);

        if (targetVehicle == null)
        {
            return new List<PartDisplayModel>();
        }

        // Step 2: Get parts already mapped to this vehicle
        var mappedPartNumbers = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.VehicleTypeId == vehicleTypeId && m.IsActive)
            .Select(m => m.PartItemKey)
            .Distinct()
            .ToListAsync();

        // Normalize target vehicle attributes for comparison
        var commercialNameNormalized = targetVehicle.CommercialName.NormalizeForGrouping();
        var modelNameNormalized = targetVehicle.ModelName.NormalizeForGrouping();
        var trimLevelNormalized = targetVehicle.TrimLevel?.NormalizeForGrouping();
        var fuelTypeNormalized = targetVehicle.FuelTypeName?.NormalizeForGrouping();

        // Step 3: Find similar vehicles with 4 different strategies (using normalized comparison)
        // Fetch candidate vehicles with engine volume and year overlap, then filter by normalized names in-memory
        var candidateVehicles = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => v.IsActive &&
                       v.VehicleTypeId != vehicleTypeId &&
                       v.EngineVolume == targetVehicle.EngineVolume &&
                       ((v.YearFrom <= targetVehicle.YearFrom && v.YearFrom >= targetVehicle.YearFrom) ||
                        (v.YearFrom >= targetVehicle.YearFrom && v.YearFrom <= targetVehicle.YearFrom)))
            .Select(v => new
            {
                v.VehicleTypeId,
                v.ModelName,
                v.CommercialName,
                v.ManufacturerId,
                v.TrimLevel,
                v.FuelTypeName,
                v.TransmissionType
            })
            .ToListAsync();

        // Strategy 1: Same commercial name + overlapping years + same engine volume + same trim + same fuel (HIGHEST - 10 points)
        var strategy1Vehicles = candidateVehicles
            .Where(v => v.CommercialName.EqualsIgnoringWhitespace(targetVehicle.CommercialName) &&
                       (v.TrimLevel?.EqualsIgnoringWhitespace(targetVehicle.TrimLevel) ?? string.IsNullOrEmpty(targetVehicle.TrimLevel)) &&
                       (v.FuelTypeName?.EqualsIgnoringWhitespace(targetVehicle.FuelTypeName) ?? string.IsNullOrEmpty(targetVehicle.FuelTypeName)))
            .Select(v => new { v.VehicleTypeId, v.ModelName, v.CommercialName })
            .ToList();

        // Strategy 2: Same engine volume + overlapping years + same trim (HIGH - 6 points)
        var strategy2Vehicles = candidateVehicles
            .Where(v => !v.CommercialName.EqualsIgnoringWhitespace(targetVehicle.CommercialName) &&
                       (v.TrimLevel?.EqualsIgnoringWhitespace(targetVehicle.TrimLevel) ?? string.IsNullOrEmpty(targetVehicle.TrimLevel)))
            .Select(v => new { v.VehicleTypeId, v.ModelName, v.CommercialName })
            .ToList();

        // Strategy 3: Same manufacturer + same engine volume (MEDIUM - 3 points)
        var allManufacturerVehicles = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => v.IsActive &&
                       v.VehicleTypeId != vehicleTypeId &&
                       v.ManufacturerId == targetVehicle.ManufacturerId &&
                       v.EngineVolume == targetVehicle.EngineVolume)
            .Select(v => new { v.VehicleTypeId, v.ModelName, v.CommercialName })
            .ToListAsync();

        var strategy3Vehicles = allManufacturerVehicles
            .Where(v => !v.ModelName.EqualsIgnoringWhitespace(targetVehicle.ModelName))
            .ToList();

        // Strategy 4: Same commercial name (BASE - 2 points)
        var allCommercialVehicles = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => v.IsActive &&
                       v.VehicleTypeId != vehicleTypeId)
            .Select(v => new { v.VehicleTypeId, v.ModelName, v.CommercialName })
            .ToListAsync();

        var strategy4Vehicles = allCommercialVehicles
            .Where(v => !v.ModelName.EqualsIgnoringWhitespace(targetVehicle.ModelName) &&
                       v.CommercialName.EqualsIgnoringWhitespace(targetVehicle.CommercialName))
            .ToList();

        // Combine all strategies and calculate weighted scores per vehicle
        var vehicleScores = new Dictionary<int, (int Score, string Reason, string SourceModel)>();

        foreach (var vehicle in strategy1Vehicles)
        {
            if (!vehicleScores.ContainsKey(vehicle.VehicleTypeId))
                vehicleScores[vehicle.VehicleTypeId] = (10, "שם מסחרי + נפח מנוע + שנים + רמת גימור + דלק", vehicle.ModelName ?? "");
        }

        foreach (var vehicle in strategy2Vehicles)
        {
            if (!vehicleScores.ContainsKey(vehicle.VehicleTypeId))
                vehicleScores[vehicle.VehicleTypeId] = (6, "נפח מנוע + שנים + רמת גימור", vehicle.ModelName ?? "");
        }

        foreach (var vehicle in strategy3Vehicles)
        {
            if (!vehicleScores.ContainsKey(vehicle.VehicleTypeId))
                vehicleScores[vehicle.VehicleTypeId] = (3, "אותו יצרן + נפח מנוע תואם", vehicle.ModelName ?? "");
        }

        foreach (var vehicle in strategy4Vehicles)
        {
            if (!vehicleScores.ContainsKey(vehicle.VehicleTypeId))
                vehicleScores[vehicle.VehicleTypeId] = (2, "שם מסחרי תואם", vehicle.ModelName ?? "");
        }

        if (vehicleScores.Count == 0)
        {
            return new List<PartDisplayModel>();
        }

        var similarVehicleIds = vehicleScores.Keys.ToList();

        // Step 4: Get parts mapped to similar vehicles with cumulative scoring
        var partScores = new Dictionary<string, (int TotalScore, List<string> SourceModels, string BestReason)>();

        var vehicleMappings = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => similarVehicleIds.Contains(m.VehicleTypeId) &&
                       m.IsActive &&
                       !mappedPartNumbers.Contains(m.PartItemKey))
            .ToListAsync();

        foreach (var mapping in vehicleMappings)
        {
            var vehicleScore = vehicleScores[mapping.VehicleTypeId];

            if (!partScores.ContainsKey(mapping.PartItemKey))
            {
                partScores[mapping.PartItemKey] = (vehicleScore.Score, new List<string> { vehicleScore.SourceModel }, vehicleScore.Reason);
            }
            else
            {
                var current = partScores[mapping.PartItemKey];
                current.SourceModels.Add(vehicleScore.SourceModel);
                partScores[mapping.PartItemKey] = (
                    current.TotalScore + vehicleScore.Score,
                    current.SourceModels,
                    vehicleScore.Score > GetScoreFromReason(current.BestReason) ? vehicleScore.Reason : current.BestReason
                );
            }
        }

        if (partScores.Count == 0)
        {
            return new List<PartDisplayModel>();
        }

        // Get top suggestions
        var topSuggestions = partScores
            .OrderByDescending(x => x.Value.TotalScore)
            .Take(50)
            .ToList();

        var suggestedPartNumbers = topSuggestions.Select(s => s.Key).ToList();

        // Step 5: Get full part information
        var suggestions = await context.VwParts
            .AsNoTracking()
            .Where(p => suggestedPartNumbers.Contains(p.PartNumber) && p.IsActive == 1)
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
                OemNumber5 = p.Oemnumber5,
                HasSuggestion = true,
                RelevanceScore = 0 // Will be calculated
            })
            .ToListAsync();

        // Calculate relevance scores and reasons
        foreach (var part in suggestions)
        {
            var partScore = partScores[part.PartNumber];
            part.RelevanceScore = partScore.TotalScore;

            var uniqueModels = partScore.SourceModels.Distinct().ToList();
            var modelCount = uniqueModels.Count;
            var modelsList = modelCount <= 3
                ? string.Join(", ", uniqueModels)
                : $"{uniqueModels.Count} דגמים";

            part.RelevanceReason = $"{partScore.BestReason} ({modelsList})";
        }

        return suggestions.OrderByDescending(p => p.RelevanceScore).ToList();
    }
}
