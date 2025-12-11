using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class VehicleDiscoveryService : IVehicleDiscoveryService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;
    private readonly IGovernmentVehicleDataService _govDataService;

    public VehicleDiscoveryService(
        IDbContextFactory<ShAutofitContext> contextFactory,
        IGovernmentVehicleDataService govDataService)
    {
        _contextFactory = contextFactory;
        _govDataService = govDataService;
    }

    public async Task<VehicleDiscoveryResult> DiscoverNewVehiclesAsync(
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        try
        {
            progressCallback?.Invoke(0, 0, "Fetching government vehicle data...");

            // Step 1: Fetch all government API records
            var govRecords = await _govDataService.FetchAllVehicleDataAsync(
                batchSize: 1000,
                progressCallback: (current, total) => progressCallback?.Invoke(current, total, $"Fetching records: {current}/{total}"),
                cancellationToken: cancellationToken);

            progressCallback?.Invoke(govRecords.Count, govRecords.Count, "Building existing vehicle lookup...");

            // Step 2: Build lookup of existing vehicles from database
            var existingVehicles = await GetExistingVehicleLookupAsync();

            progressCallback?.Invoke(govRecords.Count, govRecords.Count, "Identifying new vehicles...");

            // Step 3: Compare and find new vehicles
            var newVehicles = IdentifyNewVehicles(govRecords, existingVehicles);

            if (newVehicles.Count == 0)
            {
                return new VehicleDiscoveryResult
                {
                    TotalRecordsChecked = govRecords.Count,
                    NewVehiclesFound = 0,
                    BatchId = batchId,
                    DiscoveryStartedAt = startTime,
                    DiscoveryCompletedAt = DateTime.UtcNow,
                    Success = true
                };
            }

            progressCallback?.Invoke(govRecords.Count, govRecords.Count, $"Saving {newVehicles.Count} new vehicles...");

            // Step 4: Save to PendingVehicleReviews table
            await SavePendingVehiclesAsync(newVehicles, batchId);

            var completedTime = DateTime.UtcNow;

            progressCallback?.Invoke(govRecords.Count, govRecords.Count, $"Discovery complete! Found {newVehicles.Count} new vehicles");

            return new VehicleDiscoveryResult
            {
                TotalRecordsChecked = govRecords.Count,
                NewVehiclesFound = newVehicles.Count,
                BatchId = batchId,
                DiscoveryStartedAt = startTime,
                DiscoveryCompletedAt = completedTime,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new VehicleDiscoveryResult
            {
                TotalRecordsChecked = 0,
                NewVehiclesFound = 0,
                BatchId = batchId,
                DiscoveryStartedAt = startTime,
                DiscoveryCompletedAt = DateTime.UtcNow,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<HashSet<string>> GetExistingVehicleLookupAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Build unique keys from VehicleTypes table
        var vehicleKeys = await context.VehicleTypes
            .Where(v => v.IsActive)
            .Select(v => new
            {
                v.Manufacturer.ManufacturerCode,
                ModelCode = v.ModelCode ?? string.Empty,
                v.ModelName,
                v.YearFrom
            })
            .ToListAsync();

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var vehicle in vehicleKeys)
        {
            // Key format: ManufacturerCode_ModelCode_ModelName_Year
            var key = $"{vehicle.ManufacturerCode}_{vehicle.ModelCode}_{vehicle.ModelName}_{vehicle.YearFrom}";
            keys.Add(key);
        }

        // Also check pending vehicles to avoid duplicates
        var pendingKeys = await context.PendingVehicleReviews
            .Where(p => p.IsActive && p.ReviewStatus == "Pending")
            .Select(p => new
            {
                p.ManufacturerCode,
                p.ModelCode,
                p.ModelName,
                p.ManufacturingYear
            })
            .ToListAsync();

        foreach (var pending in pendingKeys)
        {
            var key = $"{pending.ManufacturerCode}_{pending.ModelCode}_{pending.ModelName}_{pending.ManufacturingYear}";
            keys.Add(key);
        }

        return keys;
    }

    private List<PendingVehicleReview> IdentifyNewVehicles(
        List<GovernmentVehicleDataRecord> govRecords,
        HashSet<string> existingKeys)
    {
        var newVehicles = new List<PendingVehicleReview>();

        foreach (var record in govRecords)
        {
            // Skip records with missing critical data
            if (record.ManufacturerCode == 0 ||
                string.IsNullOrWhiteSpace(record.ModelName) ||
                !record.Year.HasValue)
            {
                continue;
            }

            // Parse model code - handle both numeric and string values
            var modelCodeString = record.ModelCode ?? "0";
            if (!int.TryParse(modelCodeString, out int modelCode))
            {
                modelCode = 0;
            }

            // Build unique key: ManufacturerCode_ModelCode_ModelName_Year
            var key = $"{record.ManufacturerCode}_{modelCodeString}_{record.ModelName?.Trim()}_{record.Year.Value}";

            if (!existingKeys.Contains(key))
            {
                var transmissionType = record.GetTransmissionType();
                var driveType = record.GetStandardizedDriveType();

                newVehicles.Add(new PendingVehicleReview
                {
                    ManufacturerCode = record.ManufacturerCode,
                    ManufacturerName = record.ManufacturerName?.Trim() ?? "Unknown",
                    ModelCode = modelCode,
                    ModelName = record.ModelName?.Trim() ?? string.Empty,
                    CommercialName = record.CommercialName?.Trim(),
                    ManufacturingYear = record.Year.Value,
                    EngineVolume = record.EngineVolume,
                    FuelType = record.FuelType?.Trim(),
                    TransmissionType = transmissionType,
                    TrimLevel = record.TrimLevel?.Trim(),
                    FinishLevel = record.FinishLevel?.Trim(),
                    Horsepower = record.Horsepower,
                    DriveType = driveType,
                    NumberOfDoors = record.Doors,
                    NumberOfSeats = record.Seats,
                    TotalWeight = record.TotalWeight,
                    SafetyRating = record.SafetyRating,
                    GreenIndex = record.GreenIndex,
                    ReviewStatus = "Pending",
                    DiscoverySource = "AutoDiscovery",
                    DiscoveredAt = DateTime.UtcNow,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return newVehicles;
    }

    private async Task SavePendingVehiclesAsync(List<PendingVehicleReview> newVehicles, Guid batchId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Set batch ID for all vehicles in this discovery session
        foreach (var vehicle in newVehicles)
        {
            vehicle.BatchId = batchId;
        }

        await context.PendingVehicleReviews.AddRangeAsync(newVehicles);
        await context.SaveChangesAsync();
    }
}
