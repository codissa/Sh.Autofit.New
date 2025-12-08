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

        // Build initial query
        var query = context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive);

        // Step 1: Filter by manufacturer name (broad filter in DB)
        if (!string.IsNullOrWhiteSpace(govRecord.ManufacturerName))
        {
            var govManufacturerName = govRecord.ManufacturerName.Trim();
            query = query.Where(v =>
                v.Manufacturer.ManufacturerName == govManufacturerName ||
                v.Manufacturer.ManufacturerShortName == govManufacturerName);
        }
        else
        {
            return new List<VehicleDisplayModel>();
        }

        // Step 2: Filter by model name or model code
        if (!string.IsNullOrWhiteSpace(govRecord.ModelName))
        {
            string modelCodeString = "";
                if (govRecord.ModelCode != null) modelCodeString = govRecord.ModelCode.Value.ToString("D4");
            var govModelName = govRecord.ModelName.Trim();
            query = query.Where(v =>
                (v.ModelName != null && v.ModelName.Contains(govModelName)) &&
                (v.ModelCode != null && v.ModelCode.Contains(modelCodeString)) );
        }

        // Step 3: Load vehicles first (we'll filter by year in memory after calculating ranges)
        // Don't filter by year in DB since we need to calculate year ranges per model

        // Load vehicles from database
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
                TrimLevel = v.TrimLevel,
                FinishLevel = v.FinishLevel,
                TransmissionType = v.TransmissionType,
                VehicleCategory = v.VehicleCategory,
                CommercialName = v.CommercialName,
                FuelTypeName = v.FuelTypeName,
                EngineModel = v.EngineModel,
                EngineVolume = v.EngineVolume,
                Horsepower = v.Horsepower,
                DriveType = v.DriveType
            })
            .ToListAsync();

        // Step 4: Filter by year (calculate year range per model, then filter in memory)
        if (govRecord.ManufacturingYear.HasValue)
        {
            var year = govRecord.ManufacturingYear.Value;

            // Group vehicles by Manufacturer+ModelName and calculate year ranges
            var modelGroups = vehicles
                .GroupBy(v => new { v.ManufacturerName, v.ModelName })
                .Select(g => new
                {
                    g.Key.ManufacturerName,
                    g.Key.ModelName,
                    MinYear = g.Min(v => v.YearFrom) ?? int.MinValue,
                    MaxYear = g.Max(v => v.YearFrom) ?? int.MaxValue,  // YearTo is not populated, use max YearFrom
                    Vehicles = g.ToList()
                })
                .ToList();

            // Filter to only models where the year falls within the range
            vehicles = modelGroups
                .Where(mg => year >= mg.MinYear && year <= mg.MaxYear)
                .SelectMany(mg => mg.Vehicles)
                .ToList();
        }

        // Step 5: Filter by commercial name in memory (removing all whitespaces)
        if (!string.IsNullOrWhiteSpace(govRecord.CommercialName))
        {
            var govCommercialNameNoSpaces = RemoveAllWhitespace(govRecord.CommercialName);
            vehicles = vehicles.Where(v =>
            {
                var vehicleCommercialNameNoSpaces = RemoveAllWhitespace(v.CommercialName);
                return vehicleCommercialNameNoSpaces == govCommercialNameNoSpaces ||
                       (vehicleCommercialNameNoSpaces.Contains(govCommercialNameNoSpaces) ||
                        govCommercialNameNoSpaces.Contains(vehicleCommercialNameNoSpaces));
            }).ToList();
        }

        return vehicles;
    }

    private static string RemoveAllWhitespace(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
