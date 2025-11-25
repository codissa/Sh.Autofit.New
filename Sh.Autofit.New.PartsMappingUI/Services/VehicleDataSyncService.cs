using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class VehicleDataSyncService : IVehicleDataSyncService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;
    private readonly IGovernmentVehicleDataService _govDataService;

    public VehicleDataSyncService(
        IDbContextFactory<ShAutofitContext> contextFactory,
        IGovernmentVehicleDataService govDataService)
    {
        _contextFactory = contextFactory;
        _govDataService = govDataService;
    }

    public async Task<VehicleDataSyncResult> SyncAllVehiclesAsync(
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new VehicleDataSyncResult();

        try
        {
            progressCallback?.Invoke(0, 0, "מוריד נתונים מ-API ממשלתי...");

            // Fetch all data from government API
            var govRecords = await _govDataService.FetchAllVehicleDataAsync(
                batchSize: 1000,
                progressCallback: (current, total) =>
                {
                    progressCallback?.Invoke(current, total, $"הורדת נתונים: {current:N0} מתוך {total:N0}");
                },
                cancellationToken);

            result.TotalRecordsProcessed = govRecords.Count;

            progressCallback?.Invoke(0, govRecords.Count, "מתאים רכבים לרשומות במסד הנתונים...");

            // Load all vehicles from database with government codes for matching
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var dbVehicles = await context.VehicleTypes
                .Include(vt => vt.Manufacturer)
                .ToListAsync(cancellationToken);

            // Create lookup dictionary for faster matching
            var vehicleLookup = dbVehicles
                .Where(v => v.GovernmentManufacturerCode != null && !string.IsNullOrEmpty(v.GovernmentModelCode))
                .GroupBy(v => $"{v.GovernmentManufacturerCode}_{v.GovernmentModelCode}")
                .ToDictionary(g => g.Key, g => g.ToList());

            int processed = 0;

            // Process each government record
            foreach (var govRecord in govRecords)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                processed++;

                if (processed % 100 == 0)
                {
                    progressCallback?.Invoke(processed, govRecords.Count,
                        $"מעבד רשומה {processed:N0} מתוך {govRecords.Count:N0}...");
                }

                try
                {
                    // Try to match by manufacturer code + model code
                    var key = $"{govRecord.ManufacturerCode}_{govRecord.ModelCode}";

                    if (vehicleLookup.TryGetValue(key, out var matchedVehicles))
                    {
                        // Found matching vehicles - check if year falls in the range
                        var vehiclesToUpdate = matchedVehicles
                            .Where(v => govRecord.Year >= v.YearFrom &&
                                       govRecord.Year <= (v.YearTo ?? v.YearFrom))
                            .ToList();

                        if (!vehiclesToUpdate.Any())
                        {
                            // No exact range match, try ±1 year tolerance
                            vehiclesToUpdate = matchedVehicles
                                .Where(v => (govRecord.Year >= v.YearFrom - 1 &&
                                            govRecord.Year <= (v.YearTo ?? v.YearFrom) + 1))
                                .ToList();
                        }

                        foreach (var vehicle in vehiclesToUpdate)
                        {
                            UpdateVehicleFromGovData(vehicle, govRecord);
                            result.VehiclesUpdated++;
                        }

                        result.VehiclesMatched++;
                    }
                    else
                    {
                        // No match found - this is a new vehicle in government data
                        result.VehiclesNotMatched++;

                        // Could potentially create new vehicles here, but safer to just log
                        // result.NewVehiclesFound++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error processing record {govRecord.Id}: {ex.Message}");
                }
            }

            // Save all changes
            progressCallback?.Invoke(govRecords.Count, govRecords.Count, "שומר שינויים...");
            await context.SaveChangesAsync(cancellationToken);

            // Update consolidated models
            progressCallback?.Invoke(govRecords.Count, govRecords.Count, "מעדכן דגמים מאוחדים...");
            await UpdateConsolidatedModelsAsync();

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            progressCallback?.Invoke(govRecords.Count, govRecords.Count, "הסתיים בהצלחה!");
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Fatal error: {ex.Message}");
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<VehicleDataSyncResult> SyncVehicleByCodesAsync(
        int manufacturerCode,
        string modelCode)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new VehicleDataSyncResult();

        try
        {
            // Fetch specific vehicle data
            var govRecords = await _govDataService.FetchVehicleByCodesAsync(manufacturerCode, modelCode);
            result.TotalRecordsProcessed = govRecords.Count;

            await using var context = await _contextFactory.CreateDbContextAsync();

            // Find matching vehicles in database
            var dbVehicles = await context.VehicleTypes
                .Where(v => v.GovernmentManufacturerCode == manufacturerCode &&
                           v.GovernmentModelCode == modelCode)
                .ToListAsync();

            foreach (var govRecord in govRecords)
            {
                var matchingVehicles = dbVehicles
                    .Where(v => govRecord.Year >= v.YearFrom &&
                               govRecord.Year <= (v.YearTo ?? v.YearFrom))
                    .ToList();

                if (matchingVehicles.Any())
                {
                    foreach (var vehicle in matchingVehicles)
                    {
                        UpdateVehicleFromGovData(vehicle, govRecord);
                        result.VehiclesUpdated++;
                    }
                    result.VehiclesMatched++;
                }
                else
                {
                    result.VehiclesNotMatched++;
                }
            }

            await context.SaveChangesAsync();

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error: {ex.Message}");
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task UpdateConsolidatedModelsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var consolidatedModels = await context.ConsolidatedVehicleModels
            .Include(cm => cm.VehicleTypes)
            .ToListAsync();

        foreach (var model in consolidatedModels)
        {
            if (!model.VehicleTypes.Any())
                continue;

            // Aggregate data from all variants
            var variants = model.VehicleTypes.ToList();

            // Horsepower: take most common or average
            var horsepowerValues = variants
                .Where(v => v.Horsepower.HasValue)
                .Select(v => v.Horsepower!.Value)
                .ToList();

            if (horsepowerValues.Any())
            {
                model.Horsepower = (int)horsepowerValues.Average();
            }

            // DriveType: take most common
            var driveTypes = variants
                .Where(v => !string.IsNullOrEmpty(v.DriveType))
                .GroupBy(v => v.DriveType)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (driveTypes != null)
            {
                model.DriveType = driveTypes.Key;
            }

            // TrimLevel: take most common (body type like SUV, Sedan)
            var trimLevels = variants
                .Where(v => !string.IsNullOrEmpty(v.TrimLevel))
                .GroupBy(v => v.TrimLevel)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (trimLevels != null)
            {
                model.TrimLevel = trimLevels.Key;
            }

            // FinishLevel: take most common (trim finish level)
            var finishLevels = variants
                .Where(v => !string.IsNullOrEmpty(v.FinishLevel))
                .GroupBy(v => v.FinishLevel)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (finishLevels != null)
            {
                model.FinishLevel = finishLevels.Key;
            }

            // NumberOfDoors: take most common
            var doors = variants
                .Where(v => v.NumberOfDoors.HasValue)
                .GroupBy(v => v.NumberOfDoors)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (doors != null)
            {
                model.NumberOfDoors = doors.Key;
            }

            // NumberOfSeats: take most common
            var seats = variants
                .Where(v => v.NumberOfSeats.HasValue)
                .GroupBy(v => v.NumberOfSeats)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (seats != null)
            {
                model.NumberOfSeats = seats.Key;
            }

            // WeightRange: calculate from min/max
            var weights = variants
                .Where(v => v.TotalWeight.HasValue)
                .Select(v => v.TotalWeight!.Value)
                .ToList();

            if (weights.Any())
            {
                var minWeight = weights.Min();
                var maxWeight = weights.Max();
                if (minWeight == maxWeight)
                {
                    model.WeightRange = $"{minWeight} kg";
                }
                else
                {
                    model.WeightRange = $"{minWeight}-{maxWeight} kg";
                }
            }

            // SafetyRating: take average
            var safetyRatings = variants
                .Where(v => v.SafetyRating.HasValue)
                .Select(v => v.SafetyRating!.Value)
                .ToList();

            if (safetyRatings.Any())
            {
                model.SafetyRating = safetyRatings.Average();
            }

            // GreenIndex: take average
            var greenIndices = variants
                .Where(v => v.GreenIndex.HasValue)
                .Select(v => v.GreenIndex!.Value)
                .ToList();

            if (greenIndices.Any())
            {
                model.GreenIndex = (int)greenIndices.Average();
            }
        }

        await context.SaveChangesAsync();
    }

    private void UpdateVehicleFromGovData(VehicleType vehicle, GovernmentVehicleDataRecord govRecord)
    {
        // Update government codes for matching
        vehicle.GovernmentManufacturerCode = govRecord.ManufacturerCode;
        vehicle.GovernmentModelCode = govRecord.ModelCode;

        // Update vehicle details
        vehicle.Horsepower = govRecord.Horsepower;
        vehicle.DriveType = govRecord.GetStandardizedDriveType();
        vehicle.TrimLevel = govRecord.TrimLevel;  // merkav - body type (SUV, Sedan, etc.)
        vehicle.FinishLevel = govRecord.FinishLevel;  // ramat_gimur - trim finish level
        vehicle.NumberOfDoors = govRecord.Doors;
        vehicle.NumberOfSeats = govRecord.Seats;
        vehicle.TotalWeight = govRecord.TotalWeight;
        vehicle.SafetyRating = govRecord.SafetyRating;
        vehicle.GreenIndex = govRecord.GreenIndex;
        vehicle.LastSyncedFromGov = DateTime.Now;

        // Update commercial name if not set
        if (string.IsNullOrEmpty(vehicle.CommercialName) && !string.IsNullOrEmpty(govRecord.CommercialName))
        {
            vehicle.CommercialName = govRecord.CommercialName;
        }
    }
}
