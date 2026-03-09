using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class VehicleDataSyncService : IVehicleDataSyncService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;
    private readonly IGovernmentVehicleDataService _govDataService;
    private readonly IModelCouplingService _modelCouplingService;

    // Registration resource definitions
    private static readonly (string Id, string Name)[] RegistrationResources =
    {
        ("053cea08-09bc-40ec-8f7a-156f0677aff3", "Primary"),
        ("f6efe89a-fb3d-43a4-bb61-9bf12a9b9099", "InactiveWithCode"),
        ("851ecab1-0622-4dbe-a6c7-f950cf82abf9", "OffRoadCancelled"),
        ("03adc637-b6fe-402b-9937-7c3d3afc9140", "PersonalImport"),
        ("6f6acd03-f351-4a8f-8ecf-df792f4f573a", "InactiveNoCode"),
        ("cd3acc5c-03c3-4c89-9c54-d40f93c0d790", "HeavyAndNoCode"),
    };

    private const string VEHICLE_QUANTITIES_RESOURCE_ID = "5e87a7a1-2f6f-41c1-8aec-7216d52a6cf6";

    public VehicleDataSyncService(
        IDbContextFactory<ShAutofitContext> contextFactory,
        IGovernmentVehicleDataService govDataService,
        IModelCouplingService modelCouplingService)
    {
        _contextFactory = contextFactory;
        _govDataService = govDataService;
        _modelCouplingService = modelCouplingService;
    }

    /// <summary>
    /// Normalizes model code by padding with leading zeroes to 4 digits
    /// This handles cases where API returns numbers (234) but DB has "0234"
    /// </summary>
    private static string NormalizeModelCode(string? modelCode)
    {
        if (string.IsNullOrEmpty(modelCode))
            return string.Empty;

        // If it's numeric, pad to 4 digits
        if (int.TryParse(modelCode, out var numericCode))
        {
            return numericCode.ToString("D4"); // Pad to 4 digits: "0234"
        }

        // If not numeric, return as-is
        return modelCode;
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
                batchSize: 2000,
                progressCallback: (current, total) =>
                {
                    progressCallback?.Invoke(current, total, $"הורדת נתונים: {current:N0} מתוך {total:N0}");
                },
                cancellationToken);

            result.TotalRecordsProcessed = govRecords.Count;

            progressCallback?.Invoke(0, govRecords.Count, "מתאים רכבים לרשומות במסד הנתונים...");

            // Load all vehicles from database with manufacturer for matching
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var dbVehicles = await context.VehicleTypes
                .Include(vt => vt.Manufacturer)
                .ToListAsync(cancellationToken);

            // Load manufacturers for creating new vehicles
            var manufacturers = await context.Manufacturers
                .ToDictionaryAsync(m => m.ManufacturerCode, cancellationToken);

            // Create lookup dictionary for faster matching using Manufacturer Code + Model Code + Model Name
            // Normalize model codes to handle leading zeroes
            var vehicleLookup = dbVehicles
                .Where(v => v.Manufacturer != null && !string.IsNullOrEmpty(v.ModelName) && !string.IsNullOrEmpty(v.ModelCode))
                .GroupBy(v => $"{v.Manufacturer.ManufacturerCode}_{NormalizeModelCode(v.ModelCode)}_{v.ModelName}".ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.ToList());

            int processed = 0;
            var newVehicleTypes = new List<VehicleType>();

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
                    // Try to match by manufacturer code + model code + model name
                    // Normalize model code to handle leading zeroes
                    var normalizedModelCode = NormalizeModelCode(govRecord.ModelCode);
                    var key = $"{govRecord.ManufacturerCode}_{normalizedModelCode}_{govRecord.ModelName}".ToLowerInvariant();

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

                        if (vehiclesToUpdate.Any())
                        {
                            result.VehiclesMatched++;
                        }
                        else
                        {
                            // Key matched but year range didn't — create new VehicleType for this year
                            var newVt = CreateVehicleTypeFromWltpRecord(context, govRecord, manufacturers);
                            if (newVt != null)
                            {
                                newVehicleTypes.Add(newVt);
                                matchedVehicles.Add(newVt);
                                result.NewVehiclesFound++;
                            }
                            else
                            {
                                result.VehiclesNotMatched++;
                            }
                        }
                    }
                    else
                    {
                        // No match found — auto-create new VehicleType
                        var newVt = CreateVehicleTypeFromWltpRecord(context, govRecord, manufacturers);
                        if (newVt != null)
                        {
                            newVehicleTypes.Add(newVt);
                            vehicleLookup[key] = new List<VehicleType> { newVt };
                            result.NewVehiclesFound++;
                        }
                        else
                        {
                            result.VehiclesNotMatched++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error processing record {govRecord.Id}: {ex.Message}");
                }
            }

            // Save all changes (existing updates + new vehicles)
            progressCallback?.Invoke(govRecords.Count, govRecords.Count, "שומר שינויים...");
            await context.SaveChangesAsync(cancellationToken);

            // Link new vehicles to consolidated models
            if (newVehicleTypes.Count > 0)
            {
                progressCallback?.Invoke(govRecords.Count, govRecords.Count,
                    $"מקשר {newVehicleTypes.Count:N0} רכבים חדשים לדגמים מאוחדים...");
                await LinkNewVehiclesToConsolidatedModelsAsync(context, newVehicleTypes, manufacturers, cancellationToken);
            }

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

            // TrimLevel is part of the 7-field uniqueness key (UQ_ConsolidatedModels_NaturalKey)
            // and must NOT be overwritten by aggregation — it's set at creation time only.

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

    /// <summary>
    /// Creates a new VehicleType from a WLTP government record
    /// </summary>
    private VehicleType? CreateVehicleTypeFromWltpRecord(
        ShAutofitContext context,
        GovernmentVehicleDataRecord govRecord,
        Dictionary<int, Manufacturer> manufacturers)
    {
        if (govRecord.Year == null || govRecord.Year <= 0)
            return null;

        // Find or create manufacturer
        var manufacturer = FindOrCreateManufacturer(
            context, manufacturers,
            govRecord.ManufacturerCode,
            govRecord.ManufacturerName ?? "Unknown");

        var normalizedModelCode = NormalizeModelCode(govRecord.ModelCode);

        var vehicleType = new VehicleType
        {
            ManufacturerId = manufacturer.ManufacturerId,
            ModelCode = normalizedModelCode,
            ModelName = govRecord.ModelName ?? "Unknown",
            CommercialName = govRecord.CommercialName ?? "",
            FinishLevel = govRecord.FinishLevel ?? "",
            YearFrom = govRecord.Year.Value,
            YearTo = govRecord.Year.Value,
            EngineVolume = govRecord.EngineVolume,
            TotalWeight = govRecord.TotalWeight,
            EngineModel = "",
            FuelTypeCode = null,
            FuelTypeName = govRecord.FuelType ?? "",
            TransmissionType = govRecord.GetTransmissionType(),
            NumberOfDoors = govRecord.Doors,
            NumberOfSeats = govRecord.Seats,
            Horsepower = govRecord.Horsepower,
            TrimLevel = govRecord.TrimLevel ?? "",
            DriveType = govRecord.GetStandardizedDriveType(),
            VehicleCategory = "",
            EmissionGroup = null,
            GreenIndex = govRecord.GreenIndex,
            SafetyRating = govRecord.SafetyRating,
            SafetyLevel = null,
            FrontTireSize = "",
            RearTireSize = "",
            AdditionalSpecs = "Auto-created from WLTP sync",
            GovernmentManufacturerCode = govRecord.ManufacturerCode,
            GovernmentModelCode = govRecord.ModelCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastSyncedAt = DateTime.UtcNow,
            LastSyncedFromGov = DateTime.Now,
            Manufacturer = manufacturer
        };

        context.VehicleTypes.Add(vehicleType);
        return vehicleType;
    }

    /// <summary>
    /// Finds an existing manufacturer by code or creates a new one
    /// </summary>
    private Manufacturer FindOrCreateManufacturer(
        ShAutofitContext context,
        Dictionary<int, Manufacturer> manufacturers,
        int manufacturerCode,
        string manufacturerName)
    {
        if (manufacturers.TryGetValue(manufacturerCode, out var existing))
            return existing;

        var trimmedName = manufacturerName.Trim();
        var manufacturer = new Manufacturer
        {
            ManufacturerCode = manufacturerCode,
            ManufacturerName = trimmedName,
            ManufacturerShortName = trimmedName,
            CountryOfOrigin = "Unknown",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Manufacturers.Add(manufacturer);
        manufacturers[manufacturerCode] = manufacturer;
        return manufacturer;
    }

    /// <summary>
    /// Links newly created VehicleTypes to ConsolidatedVehicleModels (find existing or create new)
    /// </summary>
    private async Task LinkNewVehiclesToConsolidatedModelsAsync(
        ShAutofitContext context,
        List<VehicleType> newVehicles,
        Dictionary<int, Manufacturer> manufacturers,
        CancellationToken ct)
    {
        // Load all active consolidated models
        var consolidatedModels = await context.ConsolidatedVehicleModels
            .Where(cm => cm.IsActive)
            .ToListAsync(ct);

        // Build lookup by 7-field key
        var cmLookup = new Dictionary<string, ConsolidatedVehicleModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var cm in consolidatedModels)
        {
            var cmKey = $"{cm.ManufacturerCode}_{cm.ModelCode}_{cm.ModelName}_{cm.EngineVolume}_{cm.TrimLevel ?? ""}_{cm.TransmissionType ?? ""}_{cm.FuelTypeCode}";
            cmLookup.TryAdd(cmKey.ToLowerInvariant(), cm);
        }

        // Track newly created CMs for auto-coupling
        var newCmKeys = new List<string>();

        foreach (var vt in newVehicles)
        {
            int modelCode = 0;
            if (!string.IsNullOrEmpty(vt.ModelCode))
                int.TryParse(vt.ModelCode, out modelCode);

            var mfrCode = vt.GovernmentManufacturerCode ?? 0;
            var key = $"{mfrCode}_{modelCode}_{vt.ModelName}_{vt.EngineVolume}_{vt.TrimLevel ?? ""}_{vt.TransmissionType ?? ""}_{vt.FuelTypeCode}".ToLowerInvariant();

            if (cmLookup.TryGetValue(key, out var existingCm))
            {
                // Link to existing CM and extend year range
                vt.ConsolidatedModelId = existingCm.ConsolidatedModelId;
                var year = vt.YearFrom;
                if (year < existingCm.YearFrom)
                    existingCm.YearFrom = year;
                if (existingCm.YearTo == null || year > existingCm.YearTo)
                    existingCm.YearTo = year;
                existingCm.UpdatedAt = DateTime.UtcNow;
                existingCm.UpdatedBy = "WLTP_SYNC";
            }
            else
            {
                // Create new ConsolidatedVehicleModel
                if (!manufacturers.TryGetValue((int)mfrCode, out var mfr))
                    continue;

                var newCm = new ConsolidatedVehicleModel
                {
                    ManufacturerId = mfr.ManufacturerId,
                    ManufacturerCode = mfr.ManufacturerCode,
                    ModelCode = modelCode,
                    ModelName = vt.ModelName ?? "Unknown",
                    EngineVolume = vt.EngineVolume,
                    TrimLevel = vt.TrimLevel ?? "",
                    FinishLevel = vt.FinishLevel ?? "",
                    TransmissionType = vt.TransmissionType ?? "",
                    FuelTypeCode = vt.FuelTypeCode,
                    FuelTypeName = vt.FuelTypeName ?? "",
                    NumberOfDoors = vt.NumberOfDoors,
                    Horsepower = vt.Horsepower,
                    YearFrom = vt.YearFrom,
                    YearTo = vt.YearFrom,
                    CommercialName = vt.CommercialName ?? "",
                    EngineModel = vt.EngineModel ?? "",
                    VehicleCategory = vt.VehicleCategory ?? "",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = "WLTP_SYNC",
                    UpdatedBy = "WLTP_SYNC"
                };

                context.ConsolidatedVehicleModels.Add(newCm);
                cmLookup[key] = newCm;
                newCmKeys.Add(key);

                // Defer linking until after save (newCm needs an ID)
            }
        }

        // Save to get IDs for new consolidated models
        await context.SaveChangesAsync(ct);

        // Now link deferred vehicles (those with new CMs)
        foreach (var vt in newVehicles.Where(v => v.ConsolidatedModelId == null))
        {
            int modelCode = 0;
            if (!string.IsNullOrEmpty(vt.ModelCode))
                int.TryParse(vt.ModelCode, out modelCode);

            var mfrCode = vt.GovernmentManufacturerCode ?? 0;
            var key = $"{mfrCode}_{modelCode}_{vt.ModelName}_{vt.EngineVolume}_{vt.TrimLevel ?? ""}_{vt.TransmissionType ?? ""}_{vt.FuelTypeCode}".ToLowerInvariant();

            if (cmLookup.TryGetValue(key, out var cm))
            {
                vt.ConsolidatedModelId = cm.ConsolidatedModelId;
            }
        }

        await context.SaveChangesAsync(ct);

        // Auto-couple newly created ConsolidatedModels
        if (newCmKeys.Count > 0)
        {
            var newCmIds = newCmKeys
                .Where(k => cmLookup.ContainsKey(k))
                .Select(k => cmLookup[k].ConsolidatedModelId)
                .Distinct()
                .ToList();

            if (newCmIds.Count > 0)
            {
                try
                {
                    var couplingResult = await _modelCouplingService.AutoCoupleModelsAsync(
                        newCmIds, "WLTP_SYNC");
                    Debug.WriteLine($"Auto-coupled {couplingResult.TotalCouplingsCreated} new model couplings from {newCmIds.Count} new consolidated models.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Auto-coupling failed (non-fatal): {ex.Message}");
                }
            }
        }
    }

    #region Vehicle Quantity Sync

    public async Task<VehicleDataSyncResult> SyncVehicleQuantitiesAsync(
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new VehicleDataSyncResult();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Log sync start
        var syncLog = new DataSyncLog
        {
            DatasetName = "VehicleQuantities",
            ResourceId = VEHICLE_QUANTITIES_RESOURCE_ID,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };
        context.DataSyncLogs.Add(syncLog);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            progressCallback?.Invoke(0, 0, "מוריד נתוני כמויות רכבים...");

            // Fetch all quantity records from API
            var records = await _govDataService.FetchAllVehicleQuantitiesAsync(
                batchSize: 5000,
                progressCallback: (current, total) =>
                {
                    progressCallback?.Invoke(current, total, $"הורדת כמויות: {current:N0} מתוך {total:N0}");
                },
                cancellationToken);

            result.TotalRecordsProcessed = records.Count;

            progressCallback?.Invoke(records.Count, records.Count, "מוחק נתונים ישנים...");

            // Full replace: truncate and re-insert
            var connStr = context.Database.GetConnectionString()!;
            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync(cancellationToken);

                // Truncate existing data
                using (var cmd = new SqlCommand("TRUNCATE TABLE dbo.LocalVehicleQuantities", conn))
                    await cmd.ExecuteNonQueryAsync(cancellationToken);

                // Bulk insert
                progressCallback?.Invoke(0, records.Count, "שומר נתונים למסד...");

                using var bulkCopy = new SqlBulkCopy(conn)
                {
                    DestinationTableName = "dbo.LocalVehicleQuantities",
                    BatchSize = 10000
                };

                bulkCopy.ColumnMappings.Add("GovRecordId", "GovRecordId");
                bulkCopy.ColumnMappings.Add("SugDegem", "SugDegem");
                bulkCopy.ColumnMappings.Add("TozeretCd", "TozeretCd");
                bulkCopy.ColumnMappings.Add("TozeretNm", "TozeretNm");
                bulkCopy.ColumnMappings.Add("TozeretEretzNm", "TozeretEretzNm");
                bulkCopy.ColumnMappings.Add("Tozar", "Tozar");
                bulkCopy.ColumnMappings.Add("DegemCd", "DegemCd");
                bulkCopy.ColumnMappings.Add("DegemNm", "DegemNm");
                bulkCopy.ColumnMappings.Add("ShnatYitzur", "ShnatYitzur");
                bulkCopy.ColumnMappings.Add("MisparRechavimPailim", "MisparRechavimPailim");
                bulkCopy.ColumnMappings.Add("MisparRechavimLePailim", "MisparRechavimLePailim");
                bulkCopy.ColumnMappings.Add("KinuyMishari", "KinuyMishari");
                bulkCopy.ColumnMappings.Add("SyncedAt", "SyncedAt");

                var dataTable = new System.Data.DataTable();
                dataTable.Columns.Add("GovRecordId", typeof(int));
                dataTable.Columns.Add("SugDegem", typeof(string));
                dataTable.Columns.Add("TozeretCd", typeof(int));
                dataTable.Columns.Add("TozeretNm", typeof(string));
                dataTable.Columns.Add("TozeretEretzNm", typeof(string));
                dataTable.Columns.Add("Tozar", typeof(string));
                dataTable.Columns.Add("DegemCd", typeof(int));
                dataTable.Columns.Add("DegemNm", typeof(string));
                dataTable.Columns.Add("ShnatYitzur", typeof(int));
                dataTable.Columns.Add("MisparRechavimPailim", typeof(int));
                dataTable.Columns.Add("MisparRechavimLePailim", typeof(int));
                dataTable.Columns.Add("KinuyMishari", typeof(string));
                dataTable.Columns.Add("SyncedAt", typeof(DateTime));

                var now = DateTime.UtcNow;
                foreach (var r in records)
                {
                    dataTable.Rows.Add(
                        r.Id > 0 ? (object)r.Id : DBNull.Value,
                        (object?)r.VehicleBodyType ?? DBNull.Value,
                        r.ManufacturerCode,
                        (object?)r.ManufacturerName ?? DBNull.Value,
                        (object?)r.ManufacturerCountry ?? DBNull.Value,
                        (object?)r.ManufacturerShortName ?? DBNull.Value,
                        r.ModelCode,
                        (object?)r.ModelName ?? DBNull.Value,
                        r.ManufacturingYear.HasValue ? (object)r.ManufacturingYear.Value : DBNull.Value,
                        r.ActiveVehicleCount,
                        r.InactiveVehicleCount,
                        (object?)r.CommercialName ?? DBNull.Value,
                        now);
                }

                await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
            }

            // Update sync log
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.TotalApiRecords = records.Count;
            syncLog.RecordsDownloaded = records.Count;
            syncLog.LocalRecordCount = records.Count;
            syncLog.Status = "Completed";
            await context.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            progressCallback?.Invoke(records.Count, records.Count, "סנכרון כמויות הסתיים בהצלחה!");
        }
        catch (Exception ex)
        {
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.Status = "Failed";
            syncLog.ErrorMessage = ex.Message;
            await context.SaveChangesAsync(CancellationToken.None);

            result.Errors.Add($"Fatal error: {ex.Message}");
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    #endregion

    #region Vehicle Registration Sync

    public async Task<VehicleDataSyncResult> SyncVehicleRegistrationsAsync(
        bool fullRefresh,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new VehicleDataSyncResult();
        var totalDownloaded = 0;

        for (int i = 0; i < RegistrationResources.Length; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var (resourceId, resourceName) = RegistrationResources[i];
            var datasetName = $"Reg_{resourceName}";

            progressCallback?.Invoke(i, RegistrationResources.Length,
                $"מסנכרן {resourceName} ({i + 1}/{RegistrationResources.Length})...");

            try
            {
                var downloaded = await SyncSingleRegistrationResourceAsync(
                    resourceId, resourceName, datasetName, fullRefresh,
                    (current, total) =>
                    {
                        progressCallback?.Invoke(current, total,
                            $"{resourceName}: {current:N0} / {total:N0}");
                    },
                    cancellationToken);

                totalDownloaded += downloaded;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{resourceName}: {ex.Message}");
            }
        }

        result.TotalRecordsProcessed = totalDownloaded;
        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;

        progressCallback?.Invoke(RegistrationResources.Length, RegistrationResources.Length,
            $"סנכרון רישומים הסתיים! {totalDownloaded:N0} רשומות");

        return result;
    }

    private async Task<int> SyncSingleRegistrationResourceAsync(
        string resourceId,
        string resourceName,
        string datasetName,
        bool fullRefresh,
        Action<int, int>? progressCallback,
        CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Determine start offset for incremental sync
        int startOffset = 0;
        if (!fullRefresh)
        {
            var lastSync = await context.DataSyncLogs
                .Where(l => l.DatasetName == datasetName && l.Status == "Completed")
                .OrderByDescending(l => l.CompletedAt)
                .FirstOrDefaultAsync(ct);

            if (lastSync != null)
                startOffset = lastSync.TotalApiRecords;
        }

        // Log sync start
        var syncLog = new DataSyncLog
        {
            DatasetName = datasetName,
            ResourceId = resourceId,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };
        context.DataSyncLogs.Add(syncLog);
        await context.SaveChangesAsync(ct);

        var connStr = context.Database.GetConnectionString()!;
        int totalDownloaded = 0;

        try
        {
            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(ct);

            // If full refresh, delete existing records for this resource
            if (fullRefresh || startOffset == 0)
            {
                using var delCmd = new SqlCommand(
                    "DELETE FROM dbo.LocalVehicleRegistrations WHERE SourceResource = @src", conn);
                delCmd.Parameters.AddWithValue("@src", resourceName);
                delCmd.CommandTimeout = 300;
                await delCmd.ExecuteNonQueryAsync(ct);
                startOffset = 0;
            }

            // Stream batches from API and bulk insert
            await _govDataService.FetchRegistrationBatchesAsync(
                resourceId,
                batchSize: 5000,
                startOffset: startOffset,
                progressCallback: progressCallback,
                batchProcessor: async (jsonBatch) =>
                {
                    var dataTable = MapRegistrationBatchToDataTable(jsonBatch, resourceName);
                    using var bulkCopy = new SqlBulkCopy(conn)
                    {
                        DestinationTableName = "dbo.LocalVehicleRegistrations",
                        BatchSize = 10000
                    };
                    AddRegistrationColumnMappings(bulkCopy);
                    await bulkCopy.WriteToServerAsync(dataTable, ct);
                    totalDownloaded += jsonBatch.Count;
                },
                ct);

            // Get total local count
            int localCount;
            using (var cntCmd = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.LocalVehicleRegistrations WHERE SourceResource = @src", conn))
            {
                cntCmd.Parameters.AddWithValue("@src", resourceName);
                cntCmd.CommandTimeout = 120;
                localCount = (int)await cntCmd.ExecuteScalarAsync(ct);
            }

            // Update sync log
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.TotalApiRecords = startOffset + totalDownloaded;
            syncLog.RecordsDownloaded = totalDownloaded;
            syncLog.LocalRecordCount = localCount;
            syncLog.Status = "Completed";
            await context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            syncLog.CompletedAt = DateTime.UtcNow;
            syncLog.Status = "Failed";
            syncLog.ErrorMessage = ex.Message;
            await context.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        return totalDownloaded;
    }

    private System.Data.DataTable MapRegistrationBatchToDataTable(List<JsonElement> batch, string sourceName)
    {
        var dt = CreateRegistrationDataTable();
        var now = DateTime.UtcNow;

        foreach (var rec in batch)
        {
            var row = dt.NewRow();
            row["SourceResource"] = sourceName;
            row["GovRecordId"] = GetIntOrNull(rec, "_id") ?? (object)DBNull.Value;
            row["MisparRechev"] = GetStringOrNull(rec, "mispar_rechev") ?? "";
            row["TozeretCd"] = GetIntOrNull(rec, "tozeret_cd") ?? (object)DBNull.Value;
            row["TozeretNm"] = (object?)GetStringOrNull(rec, "tozeret_nm") ?? DBNull.Value;
            row["DegemNm"] = (object?)GetStringOrNull(rec, "degem_nm") ?? DBNull.Value;
            row["ShnatYitzur"] = GetIntOrNull(rec, "shnat_yitzur") ?? (object)DBNull.Value;
            row["DegemManoa"] = (object?)GetStringOrNull(rec, "degem_manoa") ?? DBNull.Value;
            row["SugDelekNm"] = (object?)GetStringOrNull(rec, "sug_delek_nm") ?? DBNull.Value;

            // PRIMARY + InactiveWithCode fields
            row["DegemCd"] = GetIntOrNull(rec, "degem_cd") ?? (object)DBNull.Value;
            row["SugDegem"] = (object?)GetStringOrNull(rec, "sug_degem") ?? DBNull.Value;
            row["RamatGimur"] = (object?)GetStringOrNull(rec, "ramat_gimur") ?? DBNull.Value;
            row["RamatEivzurBetihuty"] = GetIntOrNull(rec, "ramat_eivzur_betihuty") ?? (object)DBNull.Value;
            row["KvutzatZihum"] = GetIntOrNull(rec, "kvutzat_zihum") ?? (object)DBNull.Value;
            row["MivchanAcharonDt"] = (object?)GetStringOrNull(rec, "mivchan_acharon_dt") ?? DBNull.Value;
            row["TokefDt"] = (object?)GetStringOrNull(rec, "tokef_dt") ?? DBNull.Value;
            row["Baalut"] = (object?)GetStringOrNull(rec, "baalut") ?? DBNull.Value;
            row["Misgeret"] = (object?)GetStringOrNull(rec, "misgeret") ?? DBNull.Value;
            row["TzevaCd"] = GetIntOrNull(rec, "tzeva_cd") ?? (object)DBNull.Value;
            row["TzevaRechev"] = (object?)GetStringOrNull(rec, "tzeva_rechev") ?? DBNull.Value;
            row["ZmigKidmi"] = (object?)GetStringOrNull(rec, "zmig_kidmi") ?? DBNull.Value;
            row["ZmigAhori"] = (object?)GetStringOrNull(rec, "zmig_ahori") ?? DBNull.Value;
            row["HoraatRishum"] = GetIntOrNull(rec, "horaat_rishum") ?? (object)DBNull.Value;
            row["MoedAliyaLakvish"] = (object?)GetStringOrNull(rec, "moed_aliya_lakvish") ?? DBNull.Value;
            row["KinuyMishari"] = (object?)GetStringOrNull(rec, "kinuy_mishari") ?? DBNull.Value;

            // Off-Road Cancelled + PersonalImport shared
            row["SugRechevCd"] = GetIntOrNull(rec, "sug_rechev_cd") ?? (object)DBNull.Value;
            row["SugRechevNm"] = (object?)GetStringOrNull(rec, "sug_rechev_nm") ?? DBNull.Value;

            // Off-Road Cancelled specific
            row["BitulDt"] = (object?)GetStringOrNull(rec, "bitul_dt") ?? DBNull.Value;
            row["TozarManoa"] = (object?)GetStringOrNull(rec, "tozar_manoa") ?? DBNull.Value;
            row["MisparManoa"] = (object?)GetStringOrNull(rec, "mispar_manoa") ?? DBNull.Value;
            row["MishkalKolel"] = GetIntOrNull(rec, "mishkal_kolel") ?? (object)DBNull.Value;

            // Personal Import specific
            row["Shilda"] = (object?)GetStringOrNull(rec, "shilda") ?? DBNull.Value;
            row["NefachManoa"] = GetIntOrNull(rec, "nefach_manoa") ?? (object)DBNull.Value;
            row["TozeretEretzNm"] = (object?)GetStringOrNull(rec, "tozeret_eretz_nm") ?? DBNull.Value;
            row["SugYevu"] = (object?)GetStringOrNull(rec, "sug_yevu") ?? DBNull.Value;

            // Inactive WITHOUT Model Code + Heavy >3.5t shared
            row["MisparShilda"] = (object?)GetStringOrNull(rec, "mispar_shilda") ?? DBNull.Value;
            row["TkinaEU"] = (object?)GetStringOrNull(rec, "tkina_EU") ?? DBNull.Value;
            row["SugDelekCd"] = GetIntOrNull(rec, "sug_delek_cd") ?? (object)DBNull.Value;
            row["MishkalAzmi"] = GetIntOrNull(rec, "mishkal_azmi") ?? (object)DBNull.Value;
            row["HanaaCd"] = (object?)GetStringOrNull(rec, "hanaa_cd") ?? DBNull.Value;
            row["HanaaNm"] = (object?)GetStringOrNull(rec, "hanaa_nm") ?? DBNull.Value;
            row["MishkalMitanHarama"] = GetIntOrNull(rec, "mishkal_mitan_harama") ?? (object)DBNull.Value;

            // Heavy >3.5t specific
            row["MisparMekomotLeyadNahag"] = GetIntOrNull(rec, "mispar_mekomot_leyd_nahag") ?? (object)DBNull.Value;
            row["MisparMekomot"] = GetIntOrNull(rec, "mispar_mekomot") ?? (object)DBNull.Value;
            row["KvutzatSugRechev"] = (object?)GetStringOrNull(rec, "kvutzat_sug_rechev") ?? DBNull.Value;
            row["GriraNm"] = (object?)GetStringOrNull(rec, "grira_nm") ?? DBNull.Value;

            row["SyncedAt"] = now;
            dt.Rows.Add(row);
        }

        return dt;
    }

    private static System.Data.DataTable CreateRegistrationDataTable()
    {
        var dt = new System.Data.DataTable();
        dt.Columns.Add("SourceResource", typeof(string));
        dt.Columns.Add("GovRecordId", typeof(int));
        dt.Columns.Add("MisparRechev", typeof(string));
        dt.Columns.Add("TozeretCd", typeof(int));
        dt.Columns.Add("TozeretNm", typeof(string));
        dt.Columns.Add("DegemNm", typeof(string));
        dt.Columns.Add("ShnatYitzur", typeof(int));
        dt.Columns.Add("DegemManoa", typeof(string));
        dt.Columns.Add("SugDelekNm", typeof(string));
        dt.Columns.Add("DegemCd", typeof(int));
        dt.Columns.Add("SugDegem", typeof(string));
        dt.Columns.Add("RamatGimur", typeof(string));
        dt.Columns.Add("RamatEivzurBetihuty", typeof(int));
        dt.Columns.Add("KvutzatZihum", typeof(int));
        dt.Columns.Add("MivchanAcharonDt", typeof(string));
        dt.Columns.Add("TokefDt", typeof(string));
        dt.Columns.Add("Baalut", typeof(string));
        dt.Columns.Add("Misgeret", typeof(string));
        dt.Columns.Add("TzevaCd", typeof(int));
        dt.Columns.Add("TzevaRechev", typeof(string));
        dt.Columns.Add("ZmigKidmi", typeof(string));
        dt.Columns.Add("ZmigAhori", typeof(string));
        dt.Columns.Add("HoraatRishum", typeof(int));
        dt.Columns.Add("MoedAliyaLakvish", typeof(string));
        dt.Columns.Add("KinuyMishari", typeof(string));
        dt.Columns.Add("SugRechevCd", typeof(int));
        dt.Columns.Add("SugRechevNm", typeof(string));
        dt.Columns.Add("BitulDt", typeof(string));
        dt.Columns.Add("TozarManoa", typeof(string));
        dt.Columns.Add("MisparManoa", typeof(string));
        dt.Columns.Add("MishkalKolel", typeof(int));
        dt.Columns.Add("Shilda", typeof(string));
        dt.Columns.Add("NefachManoa", typeof(int));
        dt.Columns.Add("TozeretEretzNm", typeof(string));
        dt.Columns.Add("SugYevu", typeof(string));
        dt.Columns.Add("MisparShilda", typeof(string));
        dt.Columns.Add("TkinaEU", typeof(string));
        dt.Columns.Add("SugDelekCd", typeof(int));
        dt.Columns.Add("MishkalAzmi", typeof(int));
        dt.Columns.Add("HanaaCd", typeof(string));
        dt.Columns.Add("HanaaNm", typeof(string));
        dt.Columns.Add("MishkalMitanHarama", typeof(int));
        dt.Columns.Add("MisparMekomotLeyadNahag", typeof(int));
        dt.Columns.Add("MisparMekomot", typeof(int));
        dt.Columns.Add("KvutzatSugRechev", typeof(string));
        dt.Columns.Add("GriraNm", typeof(string));
        dt.Columns.Add("SyncedAt", typeof(DateTime));
        return dt;
    }

    private static void AddRegistrationColumnMappings(SqlBulkCopy bulkCopy)
    {
        var columns = new[]
        {
            "SourceResource", "GovRecordId", "MisparRechev", "TozeretCd", "TozeretNm",
            "DegemNm", "ShnatYitzur", "DegemManoa", "SugDelekNm", "DegemCd", "SugDegem",
            "RamatGimur", "RamatEivzurBetihuty", "KvutzatZihum", "MivchanAcharonDt",
            "TokefDt", "Baalut", "Misgeret", "TzevaCd", "TzevaRechev", "ZmigKidmi",
            "ZmigAhori", "HoraatRishum", "MoedAliyaLakvish", "KinuyMishari",
            "SugRechevCd", "SugRechevNm", "BitulDt", "TozarManoa", "MisparManoa",
            "MishkalKolel", "Shilda", "NefachManoa", "TozeretEretzNm", "SugYevu",
            "MisparShilda", "TkinaEU", "SugDelekCd", "MishkalAzmi", "HanaaCd", "HanaaNm",
            "MishkalMitanHarama", "MisparMekomotLeyadNahag", "MisparMekomot",
            "KvutzatSugRechev", "GriraNm", "SyncedAt"
        };
        foreach (var col in columns)
            bulkCopy.ColumnMappings.Add(col, col);
    }

    private static string? GetStringOrNull(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var val) && val.ValueKind != JsonValueKind.Null)
            return val.ToString();
        return null;
    }

    private static int? GetIntOrNull(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val) || val.ValueKind == JsonValueKind.Null)
            return null;
        if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var i))
            return i;
        if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var si))
            return si;
        return null;
    }

    #endregion

    #region Unified Sync

    public async Task<VehicleDataSyncResult> SyncAllDataAsync(
        bool fullRefresh,
        Action<int, int, string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new VehicleDataSyncResult();

        // Phase 1: WLTP Vehicle Specs
        progressCallback?.Invoke(0, 4, "שלב 1/4: סנכרון מפרטי WLTP...");
        var wltpResult = await SyncAllVehiclesAsync(
            (c, t, m) => progressCallback?.Invoke(c, t, $"WLTP: {m}"),
            cancellationToken);

        result.VehiclesMatched += wltpResult.VehiclesMatched;
        result.VehiclesUpdated += wltpResult.VehiclesUpdated;
        result.VehiclesNotMatched += wltpResult.VehiclesNotMatched;
        result.NewVehiclesFound += wltpResult.NewVehiclesFound;
        result.Errors.AddRange(wltpResult.Errors);

        if (cancellationToken.IsCancellationRequested) goto done;

        // Phase 2: Vehicle Quantities
        progressCallback?.Invoke(1, 4, "שלב 2/4: סנכרון כמויות רכבים...");
        var qtyResult = await SyncVehicleQuantitiesAsync(
            (c, t, m) => progressCallback?.Invoke(c, t, $"כמויות: {m}"),
            cancellationToken);

        result.TotalRecordsProcessed += qtyResult.TotalRecordsProcessed;
        result.Errors.AddRange(qtyResult.Errors);

        if (cancellationToken.IsCancellationRequested) goto done;

        // Phase 3: Vehicle Registrations (6 resources)
        progressCallback?.Invoke(2, 4, "שלב 3/4: סנכרון רישומי רכבים...");
        var regResult = await SyncVehicleRegistrationsAsync(
            fullRefresh,
            (c, t, m) => progressCallback?.Invoke(c, t, $"רישומים: {m}"),
            cancellationToken);

        result.TotalRecordsProcessed += regResult.TotalRecordsProcessed;
        result.Errors.AddRange(regResult.Errors);

        if (cancellationToken.IsCancellationRequested) goto done;

        // Phase 4: Consolidation (already done by WLTP sync, but run again for completeness)
        progressCallback?.Invoke(3, 4, "שלב 4/4: עדכון דגמים מאוחדים...");
        await UpdateConsolidatedModelsAsync();

        done:
        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        result.TotalRecordsProcessed += wltpResult.TotalRecordsProcessed;

        progressCallback?.Invoke(4, 4, $"סנכרון הסתיים! {result.Duration.TotalMinutes:F1} דקות");

        return result;
    }

    public async Task<DataSyncStats> GetSyncStatsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var stats = new DataSyncStats();

        // Vehicle quantities count
        stats.QuantityCount = await context.LocalVehicleQuantities.CountAsync();

        // Registration counts per resource
        var regCounts = await context.LocalVehicleRegistrations
            .GroupBy(r => r.SourceResource)
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToListAsync();

        stats.RegistrationCountByResource = regCounts.ToDictionary(x => x.Source, x => x.Count);
        stats.TotalRegistrationCount = regCounts.Sum(x => x.Count);

        // Last sync dates
        var lastSyncs = await context.DataSyncLogs
            .Where(l => l.Status == "Completed")
            .GroupBy(l => l.DatasetName)
            .Select(g => new { Dataset = g.Key, LastSync = g.Max(l => l.CompletedAt) })
            .ToListAsync();

        stats.LastSyncByDataset = lastSyncs
            .Where(x => x.LastSync.HasValue)
            .ToDictionary(x => x.Dataset, x => x.LastSync!.Value);

        // Vehicle types and consolidated models counts
        stats.VehicleTypeCount = await context.VehicleTypes.CountAsync();
        stats.ConsolidatedModelCount = await context.ConsolidatedVehicleModels.CountAsync(cm => cm.IsActive);

        return stats;
    }

    #endregion
}
