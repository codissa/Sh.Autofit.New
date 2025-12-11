using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class PendingVehicleReviewService : IPendingVehicleReviewService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;
    private readonly IDataService _dataService;
    private readonly IModelCouplingService _modelCouplingService;

    public PendingVehicleReviewService(
        IDbContextFactory<ShAutofitContext> contextFactory,
        IDataService dataService,
        IModelCouplingService modelCouplingService)
    {
        _contextFactory = contextFactory;
        _dataService = dataService;
        _modelCouplingService = modelCouplingService;
    }

    public async Task<List<PendingVehicleDisplayModel>> LoadPendingVehiclesAsync(Guid? batchId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.PendingVehicleReviews
            .Where(p => p.IsActive && p.ReviewStatus == "Pending");

        if (batchId.HasValue)
        {
            query = query.Where(p => p.BatchId == batchId.Value);
        }

        var pendingVehicles = await query
            .OrderBy(p => p.ManufacturerName)
            .ThenBy(p => p.ModelName)
            .ThenBy(p => p.ManufacturingYear)
            .ToListAsync();

        return pendingVehicles.Select(p => new PendingVehicleDisplayModel
        {
            PendingVehicleId = p.PendingVehicleId,
            ManufacturerCode = p.ManufacturerCode,
            ManufacturerName = p.ManufacturerName,
            ModelCode = p.ModelCode,
            ModelName = p.ModelName,
            CommercialName = p.CommercialName,
            ManufacturingYear = p.ManufacturingYear,
            EngineVolume = p.EngineVolume,
            FuelType = p.FuelType,
            TransmissionType = p.TransmissionType,
            TrimLevel = p.TrimLevel,
            FinishLevel = p.FinishLevel,
            Horsepower = p.Horsepower,
            DriveType = p.DriveType,
            NumberOfDoors = p.NumberOfDoors,
            NumberOfSeats = p.NumberOfSeats,
            TotalWeight = p.TotalWeight,
            SafetyRating = p.SafetyRating,
            GreenIndex = p.GreenIndex,
            ReviewStatus = p.ReviewStatus,
            ReviewedBy = p.ReviewedBy,
            ReviewedAt = p.ReviewedAt,
            ReviewNotes = p.ReviewNotes,
            DiscoveredAt = p.DiscoveredAt,
            DiscoverySource = p.DiscoverySource,
            BatchId = p.BatchId
        }).ToList();
    }

    public async Task ApproveVehicleAsync(int pendingVehicleId, string reviewedBy, string? notes = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var vehicle = await context.PendingVehicleReviews.FindAsync(pendingVehicleId);
        if (vehicle == null)
            throw new InvalidOperationException($"Pending vehicle {pendingVehicleId} not found");

        vehicle.ReviewStatus = "Approved";
        vehicle.ReviewedBy = reviewedBy;
        vehicle.ReviewedAt = DateTime.UtcNow;
        vehicle.ReviewNotes = notes;

        await context.SaveChangesAsync();
    }

    public async Task RejectVehicleAsync(int pendingVehicleId, string reviewedBy, string? notes = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var vehicle = await context.PendingVehicleReviews.FindAsync(pendingVehicleId);
        if (vehicle == null)
            throw new InvalidOperationException($"Pending vehicle {pendingVehicleId} not found");

        vehicle.ReviewStatus = "Rejected";
        vehicle.ReviewedBy = reviewedBy;
        vehicle.ReviewedAt = DateTime.UtcNow;
        vehicle.ReviewNotes = notes;
        vehicle.IsActive = false; // Soft delete rejected vehicles

        await context.SaveChangesAsync();
    }

    public async Task ApproveBatchAsync(List<int> pendingVehicleIds, string reviewedBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var vehicles = await context.PendingVehicleReviews
            .Where(p => pendingVehicleIds.Contains(p.PendingVehicleId))
            .ToListAsync();

        foreach (var vehicle in vehicles)
        {
            vehicle.ReviewStatus = "Approved";
            vehicle.ReviewedBy = reviewedBy;
            vehicle.ReviewedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public async Task RejectBatchAsync(List<int> pendingVehicleIds, string reviewedBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var vehicles = await context.PendingVehicleReviews
            .Where(p => pendingVehicleIds.Contains(p.PendingVehicleId))
            .ToListAsync();

        foreach (var vehicle in vehicles)
        {
            vehicle.ReviewStatus = "Rejected";
            vehicle.ReviewedBy = reviewedBy;
            vehicle.ReviewedAt = DateTime.UtcNow;
            vehicle.IsActive = false;
        }

        await context.SaveChangesAsync();
    }

    public async Task<VehicleProcessingResult> ProcessApprovedVehiclesAsync(
        Guid? batchId = null,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new VehicleProcessingResult
        {
            ProcessingStartedAt = startTime
        };

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // Get approved vehicles
            var query = context.PendingVehicleReviews
                .Where(p => p.IsActive && p.ReviewStatus == "Approved" && p.ProcessedAt == null);

            if (batchId.HasValue)
            {
                query = query.Where(p => p.BatchId == batchId.Value);
            }

            var approvedVehicles = await query.ToListAsync(cancellationToken);

            if (approvedVehicles.Count == 0)
            {
                result.Success = true;
                result.ProcessingCompletedAt = DateTime.UtcNow;
                return result;
            }

            progressCallback?.Invoke(0, approvedVehicles.Count, "Starting vehicle processing...");

            var newConsolidatedModelIds = new List<int>();
            int processedCount = 0;

            foreach (var pendingVehicle in approvedVehicles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    processedCount++;
                    progressCallback?.Invoke(processedCount, approvedVehicles.Count,
                        $"Processing: {pendingVehicle.ManufacturerName} {pendingVehicle.ModelName} ({pendingVehicle.ManufacturingYear})");

                    // Step 1: Find or create manufacturer
                    var manufacturer = await context.Manufacturers
                        .FirstOrDefaultAsync(m => m.ManufacturerCode == pendingVehicle.ManufacturerCode, cancellationToken);

                    if (manufacturer == null)
                    {
                        manufacturer = new Manufacturer
                        {
                            ManufacturerCode = pendingVehicle.ManufacturerCode,
                            ManufacturerName = pendingVehicle.ManufacturerName,
                            ManufacturerShortName = pendingVehicle.ManufacturerName,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        context.Manufacturers.Add(manufacturer);
                        await context.SaveChangesAsync(cancellationToken);
                        result.ManufacturersCreated++;
                    }

                    // Step 2: Create VehicleType
                    var vehicleType = new VehicleType
                    {
                        ManufacturerId = manufacturer.ManufacturerId,
                        ModelCode = pendingVehicle.ModelCode.ToString("D4"),
                        ModelName = pendingVehicle.ModelName,
                        CommercialName = pendingVehicle.CommercialName,
                        YearFrom = pendingVehicle.ManufacturingYear,
                        YearTo = null,
                        EngineVolume = pendingVehicle.EngineVolume,
                        FuelTypeName = pendingVehicle.FuelType,
                        TransmissionType = pendingVehicle.TransmissionType,
                        TrimLevel = pendingVehicle.TrimLevel,
                        FinishLevel = pendingVehicle.FinishLevel,
                        Horsepower = pendingVehicle.Horsepower,
                        DriveType = pendingVehicle.DriveType,
                        VehicleCategory = pendingVehicle.TrimLevel,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    context.VehicleTypes.Add(vehicleType);
                    await context.SaveChangesAsync(cancellationToken);
                    result.VehicleTypesCreated++;

                    // Step 3: Find or create consolidated model
                    var consolidatedModel = await _dataService.FindOrCreateConsolidatedModelAsync(
                        vehicleType,
                        manufacturer,
                        "AUTO_DISCOVERY");

                    // Track if this is a new consolidated model
                    var isNewModel = consolidatedModel.CreatedAt > startTime.AddSeconds(-5);
                    if (isNewModel)
                    {
                        newConsolidatedModelIds.Add(consolidatedModel.ConsolidatedModelId);
                        result.ConsolidatedModelsCreated++;
                    }

                    // Mark as processed
                    pendingVehicle.ProcessedAt = DateTime.UtcNow;
                    result.VehiclesProcessed++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error processing vehicle {pendingVehicle.ManufacturerName} {pendingVehicle.ModelName}: {ex.Message}");
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            // Step 4: Auto-couple new consolidated models (if any)
            if (newConsolidatedModelIds.Count > 0)
            {
                progressCallback?.Invoke(approvedVehicles.Count, approvedVehicles.Count, "Creating automatic couplings...");

                try
                {
                    var couplingResult = await _modelCouplingService.AutoCoupleModelsAsync(
                        newConsolidatedModelIds,
                        "AUTO_DISCOVERY");

                    result.CouplingsCreated = couplingResult.TotalCouplingsCreated;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error creating couplings: {ex.Message}");
                }
            }

            result.Success = true;
            result.ProcessingCompletedAt = DateTime.UtcNow;

            progressCallback?.Invoke(approvedVehicles.Count, approvedVehicles.Count,
                $"Processing complete! Created {result.VehicleTypesCreated} vehicles, {result.ConsolidatedModelsCreated} models");

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Fatal error: {ex.Message}");
            result.ProcessingCompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    public async Task<int> GetPendingCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.PendingVehicleReviews
            .CountAsync(p => p.IsActive && p.ReviewStatus == "Pending");
    }
}
