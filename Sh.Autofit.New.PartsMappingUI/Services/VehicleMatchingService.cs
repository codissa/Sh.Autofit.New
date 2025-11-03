using Sh.Autofit.New.PartsMappingUI.Models;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class VehicleMatchingService : IVehicleMatchingService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

    public VehicleMatchingService(IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<VehicleDisplayModel?> FindMatchingVehicleTypeAsync(GovernmentVehicleRecord govRecord)
    {
        var possibleMatches = await FindPossibleMatchesAsync(govRecord);

        // Return the first match (best match)
        return possibleMatches.FirstOrDefault();
    }

    public async Task<List<VehicleDisplayModel>> FindPossibleMatchesAsync(GovernmentVehicleRecord govRecord)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Build query to find matching vehicles
        var query = context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive);

        // Step 1: Filter by manufacturer name (exact match)
        if (!string.IsNullOrWhiteSpace(govRecord.ManufacturerName))
        {
            var govManufacturerName = govRecord.ManufacturerName.Trim();
            query = query.Where(v =>
                v.Manufacturer.ManufacturerName == govManufacturerName ||
                v.Manufacturer.ManufacturerShortName == govManufacturerName);
        }
        else
        {
            // No manufacturer name provided, return empty
            return new List<VehicleDisplayModel>();
        }

        // Step 2: Filter by model name or model code
        if (!string.IsNullOrWhiteSpace(govRecord.ModelName))
        {
            var govModelName = govRecord.ModelName.Trim();
            query = query.Where(v =>
                (v.ModelName != null && v.ModelName.Contains(govModelName)) ||
                (v.ModelCode != null && v.ModelCode.Contains(govModelName)) ||
                (v.ModelName != null && govModelName.Contains(v.ModelName)) ||
                (v.ModelCode != null && govModelName.Contains(v.ModelCode)));
        }

        // Step 3: Filter by year (year must fall within vehicle's year range)
        if (govRecord.ManufacturingYear.HasValue)
        {
            var year = govRecord.ManufacturingYear.Value;
            query = query.Where(v =>
                (v.YearFrom == null || v.YearFrom <= year) &&
                (v.YearTo == null || v.YearTo >= year));
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
                EngineModel = v.EngineModel
            })
            .ToListAsync();

        return vehicles;
    }
}
