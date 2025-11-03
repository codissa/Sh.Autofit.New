using CsvHelper;
using CsvHelper.Configuration;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public sealed class CsvVehicleRecord
{
    // Manufacturer fields
    public string sug_degem { get; set; }
    public string tozeret_cd { get; set; }
    public string tozeret_nm { get; set; }
    public string tozeret_eretz_nm { get; set; }
    public string tozar { get; set; }
    decimal t
    // Model fields
    public string degem_cd { get; set; }
    public string degem_nm { get; set; }
    public string shnat_yitzur { get; set; }
    public string kinuy_mishari { get; set; }
    public string ramat_gimur { get; set; }

    // Fuel fields
    public string delek_cd { get; set; }
    public string delek_nm { get; set; }

    // Technical specs
    public string nefah_manoa { get; set; }        // Engine volume
    public string mishkal_kolel { get; set; }      // Total weight
    public string koah_sus { get; set; }           // Horsepower
    public string mispar_dlatot { get; set; }      // Number of doors
    public string mispar_moshavim { get; set; }    // Number of seats
    public string automatic_ind { get; set; }      // Automatic transmission (1/0)
    public string merkav { get; set; }             // Body type

    // Environmental
    public string madad_yarok { get; set; }        // Green index
    public string kvutzat_zihum { get; set; }      // Emission group

    // Safety
    public string nikud_betihut { get; set; }      // Safety score
    public string ramat_eivzur_betihuty { get; set; } // Safety equipment level
}

public sealed class CsvVehicleRecordMap : ClassMap<CsvVehicleRecord>
{
    public CsvVehicleRecordMap()
    {
        // Manufacturer fields
        Map(m => m.sug_degem).Name("sug_degem");
        Map(m => m.tozeret_cd).Name("tozeret_cd");
        Map(m => m.tozeret_nm).Name("tozeret_nm");
        Map(m => m.tozeret_eretz_nm).Name("tozeret_eretz_nm");
        Map(m => m.tozar).Name("tozar");

        // Model fields
        Map(m => m.degem_cd).Name("degem_cd");
        Map(m => m.degem_nm).Name("degem_nm");
        Map(m => m.shnat_yitzur).Name("shnat_yitzur");
        Map(m => m.kinuy_mishari).Name("kinuy_mishari");
        Map(m => m.ramat_gimur).Name("ramat_gimur");

        // Fuel fields
        Map(m => m.delek_cd).Name("delek_cd");
        Map(m => m.delek_nm).Name("delek_nm");

        // Technical specs
        Map(m => m.nefah_manoa).Name("nefah_manoa");
        Map(m => m.mishkal_kolel).Name("mishkal_kolel");
        Map(m => m.koah_sus).Name("koah_sus");
        Map(m => m.mispar_dlatot).Name("mispar_dlatot");
        Map(m => m.mispar_moshavim).Name("mispar_moshavim");
        Map(m => m.automatic_ind).Name("automatic_ind");
        Map(m => m.merkav).Name("merkav");

        // Environmental
        Map(m => m.madad_yarok).Name("madad_yarok");
        Map(m => m.kvutzat_zihum).Name("kvutzat_zihum");

        // Safety
        Map(m => m.nikud_betihut).Name("nikud_betihut");
        Map(m => m.ramat_eivzur_betihuty).Name("ramat_eivzur_betihuty");
    }
}

internal class Program
{
    // --------- CONFIG ----------
    private const string CsvPath =
        @"C:\Users\ASUS\Downloads\142afde2-6228-49f9-8a29-9b6c3a0cbe40.csv";

    private const string ConnectionString =
        "Data Source=server-pc\\wizsoft2;Initial Catalog=Sh.Autofit;Persist Security Info=True;User ID=issa;Password=5060977Ih;Encrypt=False;Trust Server Certificate=True";

    private const int BatchSize = 5000; // Larger batch for bulk operations

    private static async Task<int> Main()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!File.Exists(CsvPath))
            {
                Console.Error.WriteLine($"CSV not found: {CsvPath}");
                return 1;
            }

            Console.WriteLine("Starting bulk import...");
            Console.WriteLine($"CSV: {CsvPath}");
            Console.WriteLine($"Batch size: {BatchSize:N0}");
            Console.WriteLine();

            var options = new DbContextOptionsBuilder<ShAutofitContext>()
                .UseSqlServer(ConnectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(300); // 5 minutes for bulk operations
                })
                .EnableSensitiveDataLogging(false)
                .Options;

            await using var context = new ShAutofitContext(options);

            // PHASE 1: Load all CSV records into memory (fast)
            Console.WriteLine("Phase 1: Reading CSV file...");
            var allRecords = ReadCsvFile();
            Console.WriteLine($"✓ Read {allRecords.Count:N0} records in {stopwatch.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine();

            // PHASE 2: Process Manufacturers in bulk
            Console.WriteLine("Phase 2: Processing manufacturers...");
            var manufacturersProcessed = await ProcessManufacturersBulk(context, allRecords);
            Console.WriteLine($"✓ Processed {manufacturersProcessed:N0} manufacturers in {stopwatch.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine();

            // PHASE 3: Load manufacturer lookup
            Console.WriteLine("Phase 3: Building manufacturer lookup...");
            var manufacturerLookup = await context.Manufacturers
                .AsNoTracking()
                .ToDictionaryAsync(m => m.ManufacturerCode, m => m.ManufacturerId);
            Console.WriteLine($"✓ Loaded {manufacturerLookup.Count:N0} manufacturers");
            Console.WriteLine();

            // PHASE 4: Process Vehicle Types in bulk
            Console.WriteLine("Phase 4: Processing vehicle types...");
            var vehicleTypesProcessed = await ProcessVehicleTypesBulk(context, allRecords, manufacturerLookup);
            Console.WriteLine($"✓ Processed {vehicleTypesProcessed:N0} vehicle types in {stopwatch.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine();

            stopwatch.Stop();
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"✓ COMPLETED SUCCESSFULLY!");
            Console.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F2}s ({stopwatch.Elapsed.TotalMinutes:F2} minutes)");
            Console.WriteLine($"Records processed: {allRecords.Count:N0}");
            Console.WriteLine($"Average: {allRecords.Count / stopwatch.Elapsed.TotalSeconds:F0} records/sec");
            Console.WriteLine("═══════════════════════════════════════");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            Console.Error.WriteLine($"Stack: {ex.StackTrace}");
            return 2;
        }
    }

    private static List<CsvVehicleRecord> ReadCsvFile()
    {
        using var reader = new StreamReader(CsvPath, detectEncodingFromByteOrderMarks: true);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "|",
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
        };

        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<CsvVehicleRecordMap>();

        return csv.GetRecords<CsvVehicleRecord>().ToList();
    }

    private static async Task<int> ProcessManufacturersBulk(
        ShAutofitContext context,
        List<CsvVehicleRecord> records)
    {
        // Group by manufacturer code
        var manufacturerGroups = records
            .Where(r => int.TryParse(r.tozeret_cd, out var code) && code != 0)
            .GroupBy(r => int.Parse(r.tozeret_cd))
            .ToList();

        Console.WriteLine($"  Found {manufacturerGroups.Count:N0} unique manufacturers");

        // Load existing manufacturers
        var existingManufacturers = await context.Manufacturers
            .AsNoTracking()
            .ToDictionaryAsync(m => m.ManufacturerCode);

        var manufacturersToInsert = new List<Manufacturer>();
        var manufacturersToUpdate = new List<Manufacturer>();

        foreach (var group in manufacturerGroups)
        {
            var manufacturerCode = group.Key;
            var firstRecord = group.First();

            var manufacturerName = NormalizeStr(firstRecord.tozeret_nm) ?? "UNKNOWN";
            var manufacturerShortName = NormalizeStr(firstRecord.tozar);
            var manufacturerCountry = NormalizeStr(firstRecord.tozeret_eretz_nm);

            if (existingManufacturers.TryGetValue(manufacturerCode, out var existing))
            {
                // Check if update needed
                var changed = false;
                if (!StringEquals(existing.ManufacturerName, manufacturerName))
                {
                    existing.ManufacturerName = manufacturerName;
                    changed = true;
                }
                if (!StringEquals(existing.ManufacturerShortName, manufacturerShortName))
                {
                    existing.ManufacturerShortName = manufacturerShortName;
                    changed = true;
                }
                if (!StringEquals(existing.CountryOfOrigin, manufacturerCountry))
                {
                    existing.CountryOfOrigin = manufacturerCountry;
                    changed = true;
                }

                if (changed)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                    manufacturersToUpdate.Add(existing);
                }
            }
            else
            {
                manufacturersToInsert.Add(new Manufacturer
                {
                    ManufacturerCode = manufacturerCode,
                    ManufacturerName = manufacturerName,
                    ManufacturerShortName = manufacturerShortName,
                    CountryOfOrigin = manufacturerCountry,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        // Bulk insert new manufacturers
        if (manufacturersToInsert.Any())
        {
            Console.WriteLine($"  Inserting {manufacturersToInsert.Count:N0} new manufacturers...");
            await context.BulkInsertAsync(manufacturersToInsert, new BulkConfig
            {
                SetOutputIdentity = true,
                BatchSize = BatchSize
            });
        }

        // Bulk update existing manufacturers
        if (manufacturersToUpdate.Any())
        {
            Console.WriteLine($"  Updating {manufacturersToUpdate.Count:N0} manufacturers...");
            await context.BulkUpdateAsync(manufacturersToUpdate, new BulkConfig
            {
                BatchSize = BatchSize,
                PropertiesToInclude = new List<string>
                {
                    nameof(Manufacturer.ManufacturerName),
                    nameof(Manufacturer.ManufacturerShortName),
                    nameof(Manufacturer.CountryOfOrigin),
                    nameof(Manufacturer.UpdatedAt)
                }
            });
        }

        return manufacturerGroups.Count;
    }

    private static async Task<int> ProcessVehicleTypesBulk(
        ShAutofitContext context,
        List<CsvVehicleRecord> records,
        Dictionary<int, int> manufacturerLookup)
    {
        Console.WriteLine($"  Processing {records.Count:N0} vehicle records...");

        // Build vehicle type DTOs from CSV
        var vehicleTypeDtos = records
            .Where(r => int.TryParse(r.tozeret_cd, out var code) &&
                       code != 0 &&
                       !string.IsNullOrWhiteSpace(r.degem_cd))
            .Select(r =>
            {
                int.TryParse(r.tozeret_cd, out var mfrCode);
                int.TryParse(r.shnat_yitzur, out var yearFrom);
                int.TryParse(r.delek_cd, out var fuelTypeCode);
                int.TryParse(r.nefah_manoa, out var engineVolume);
                int.TryParse(r.mishkal_kolel, out var totalWeight);
                int.TryParse(r.koah_sus, out var horsepower);
                int.TryParse(r.mispar_dlatot, out var numDoors);
                int.TryParse(r.mispar_moshavim, out var numSeats);
                int.TryParse(r.madad_yarok, out var greenIndex);
                int.TryParse(r.kvutzat_zihum, out var emissionGroup);
                int.TryParse(r.ramat_eivzur_betihuty, out var safetyLevel);
                decimal.TryParse(r.nikud_betihut, out var safetyRating);

                // Map transmission type
                var transmissionType = r.automatic_ind == "1" ? "Automatic" :
                                       r.automatic_ind == "0" ? "Manual" : null;

                return new
                {
                    ManufacturerId = manufacturerLookup.GetValueOrDefault(mfrCode, 0),
                    ManufacturerCode = mfrCode,
                    ModelCode = NormalizeStr(r.degem_cd),
                    ModelName = !string.IsNullOrWhiteSpace(r.degem_nm) ? r.degem_nm : r.degem_cd,
                    YearFrom = yearFrom,
                    VehicleCategory = NormalizeStr(r.sug_degem),
                    CommercialName = NormalizeStr(r.kinuy_mishari),
                    FinishLevel = NormalizeStr(r.ramat_gimur),
                    FuelTypeCode = fuelTypeCode > 0 ? (int?)fuelTypeCode : null,
                    FuelTypeName = NormalizeStr(r.delek_nm),
                    EngineVolume = engineVolume > 0 ? (int?)engineVolume : null,
                    TotalWeight = totalWeight > 0 ? (int?)totalWeight : null,
                    Horsepower = horsepower > 0 ? (int?)horsepower : null,
                    NumberOfDoors = numDoors > 0 ? (int?)numDoors : null,
                    NumberOfSeats = numSeats > 0 ? (int?)numSeats : null,
                    TransmissionType = transmissionType,
                    TrimLevel = NormalizeStr(r.merkav),
                    GreenIndex = greenIndex > 0 ? (int?)greenIndex : null,
                    EmissionGroup = emissionGroup > 0 ? (int?)emissionGroup : null,
                    SafetyLevel = safetyLevel > 0 ? (int?)safetyLevel : null,
                    SafetyRating = safetyRating > 0 ? (decimal?)safetyRating : null
                };
            })
            .Where(v => v.ManufacturerId > 0 && !string.IsNullOrWhiteSpace(v.ModelCode))
            .ToList();

        Console.WriteLine($"  Valid vehicle records: {vehicleTypeDtos.Count:N0}");

        // Group by unique key (ManufacturerId + ModelCode + YearFrom)
        var uniqueVehicles = vehicleTypeDtos
            .GroupBy(v => new { v.ManufacturerId, v.ModelCode, v.YearFrom })
            .Select(g => g.First()) // Take first of duplicates
            .ToList();

        Console.WriteLine($"  Unique vehicle types: {uniqueVehicles.Count:N0}");

        // Load existing vehicle types
        var existingVehicles = await context.VehicleTypes
            .AsNoTracking()
            .Select(v => new
            {
                v.VehicleTypeId,
                v.ManufacturerId,
                v.ModelCode,
                v.YearFrom,
                v.ModelName,
                v.CommercialName,
                v.FinishLevel,
                v.FuelTypeCode,
                v.FuelTypeName,
                v.EngineVolume,
                v.TotalWeight,
                v.Horsepower,
                v.NumberOfDoors,
                v.NumberOfSeats,
                v.TransmissionType,
                v.TrimLevel,
                v.GreenIndex,
                v.EmissionGroup,
                v.SafetyLevel,
                v.SafetyRating
            })
            .ToListAsync();

        var existingLookup = existingVehicles
            .ToDictionary(v => $"{v.ManufacturerId}_{v.ModelCode}_{v.YearFrom}");

        Console.WriteLine($"  Existing vehicle types in DB: {existingLookup.Count:N0}");

        var vehiclesToInsert = new List<VehicleType>();
        var vehiclesToUpdate = new List<VehicleType>();

        foreach (var dto in uniqueVehicles)
        {
            var key = $"{dto.ManufacturerId}_{dto.ModelCode}_{dto.YearFrom}";

            if (existingLookup.TryGetValue(key, out var existing))
            {
                // Check if update needed
                var changed = false;
                var updated = new VehicleType
                {
                    VehicleTypeId = existing.VehicleTypeId,
                    ManufacturerId = existing.ManufacturerId,
                    ModelCode = existing.ModelCode,
                    YearFrom = existing.YearFrom,
                    ModelName = existing.ModelName,
                    CommercialName = existing.CommercialName,
                    FinishLevel = existing.FinishLevel,
                    FuelTypeCode = existing.FuelTypeCode,
                    FuelTypeName = existing.FuelTypeName,
                    EngineVolume = existing.EngineVolume,
                    TotalWeight = existing.TotalWeight,
                    Horsepower = existing.Horsepower,
                    NumberOfDoors = existing.NumberOfDoors,
                    NumberOfSeats = existing.NumberOfSeats,
                    TransmissionType = existing.TransmissionType,
                    TrimLevel = existing.TrimLevel,
                    GreenIndex = existing.GreenIndex,
                    EmissionGroup = existing.EmissionGroup,
                    SafetyLevel = existing.SafetyLevel,
                    SafetyRating = existing.SafetyRating,
                    VehicleCategory = dto.VehicleCategory,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                if (!StringEquals(existing.ModelName, dto.ModelName))
                {
                    updated.ModelName = dto.ModelName;
                    changed = true;
                }
                if (!StringEquals(existing.CommercialName, dto.CommercialName))
                {
                    updated.CommercialName = dto.CommercialName;
                    changed = true;
                }
                if (!StringEquals(existing.FuelTypeName, dto.FuelTypeName))
                {
                    updated.FuelTypeName = dto.FuelTypeName;
                    changed = true;
                }
                if (!StringEquals(existing.FinishLevel, dto.FinishLevel))
                {
                    updated.FinishLevel = dto.FinishLevel;
                    changed = true;
                }
                if (existing.FuelTypeCode != dto.FuelTypeCode)
                {
                    updated.FuelTypeCode = dto.FuelTypeCode;
                    changed = true;
                }
                if (existing.EngineVolume != dto.EngineVolume)
                {
                    updated.EngineVolume = dto.EngineVolume;
                    changed = true;
                }
                if (existing.TotalWeight != dto.TotalWeight)
                {
                    updated.TotalWeight = dto.TotalWeight;
                    changed = true;
                }
                if (existing.Horsepower != dto.Horsepower)
                {
                    updated.Horsepower = dto.Horsepower;
                    changed = true;
                }
                if (existing.NumberOfDoors != dto.NumberOfDoors)
                {
                    updated.NumberOfDoors = dto.NumberOfDoors;
                    changed = true;
                }
                if (existing.NumberOfSeats != dto.NumberOfSeats)
                {
                    updated.NumberOfSeats = dto.NumberOfSeats;
                    changed = true;
                }
                if (!StringEquals(existing.TransmissionType, dto.TransmissionType))
                {
                    updated.TransmissionType = dto.TransmissionType;
                    changed = true;
                }
                if (!StringEquals(existing.TrimLevel, dto.TrimLevel))
                {
                    updated.TrimLevel = dto.TrimLevel;
                    changed = true;
                }
                if (existing.GreenIndex != dto.GreenIndex)
                {
                    updated.GreenIndex = dto.GreenIndex;
                    changed = true;
                }
                if (existing.EmissionGroup != dto.EmissionGroup)
                {
                    updated.EmissionGroup = dto.EmissionGroup;
                    changed = true;
                }
                if (existing.SafetyLevel != dto.SafetyLevel)
                {
                    updated.SafetyLevel = dto.SafetyLevel;
                    changed = true;
                }
                if (existing.SafetyRating != dto.SafetyRating)
                {
                    updated.SafetyRating = dto.SafetyRating;
                    changed = true;
                }

                if (changed)
                {
                    vehiclesToUpdate.Add(updated);
                }
            }
            else
            {
                vehiclesToInsert.Add(new VehicleType
                {
                    ManufacturerId = dto.ManufacturerId,
                    ModelCode = dto.ModelCode,
                    ModelName = dto.ModelName,
                    YearFrom = dto.YearFrom,
                    VehicleCategory = dto.VehicleCategory,
                    CommercialName = dto.CommercialName,
                    FinishLevel = dto.FinishLevel,
                    FuelTypeCode = dto.FuelTypeCode,
                    FuelTypeName = dto.FuelTypeName,
                    EngineVolume = dto.EngineVolume,
                    TotalWeight = dto.TotalWeight,
                    Horsepower = dto.Horsepower,
                    NumberOfDoors = dto.NumberOfDoors,
                    NumberOfSeats = dto.NumberOfSeats,
                    TransmissionType = dto.TransmissionType,
                    TrimLevel = dto.TrimLevel,
                    GreenIndex = dto.GreenIndex,
                    EmissionGroup = dto.EmissionGroup,
                    SafetyLevel = dto.SafetyLevel,
                    SafetyRating = dto.SafetyRating,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        // Bulk insert
        if (vehiclesToInsert.Any())
        {
            Console.WriteLine($"  Inserting {vehiclesToInsert.Count:N0} new vehicle types...");
            await context.BulkInsertAsync(vehiclesToInsert, new BulkConfig
            {
                BatchSize = BatchSize,
                SetOutputIdentity = true
            });
        }

        // Bulk update
        if (vehiclesToUpdate.Any())
        {
            Console.WriteLine($"  Updating {vehiclesToUpdate.Count:N0} vehicle types...");
            await context.BulkUpdateAsync(vehiclesToUpdate, new BulkConfig
            {
                BatchSize = BatchSize,
                PropertiesToInclude = new List<string>
                {
                    nameof(VehicleType.ModelName),
                    nameof(VehicleType.CommercialName),
                    nameof(VehicleType.FinishLevel),
                    nameof(VehicleType.FuelTypeCode),
                    nameof(VehicleType.FuelTypeName),
                    nameof(VehicleType.EngineVolume),
                    nameof(VehicleType.TotalWeight),
                    nameof(VehicleType.Horsepower),
                    nameof(VehicleType.NumberOfDoors),
                    nameof(VehicleType.NumberOfSeats),
                    nameof(VehicleType.TransmissionType),
                    nameof(VehicleType.TrimLevel),
                    nameof(VehicleType.GreenIndex),
                    nameof(VehicleType.EmissionGroup),
                    nameof(VehicleType.SafetyLevel),
                    nameof(VehicleType.SafetyRating),
                    nameof(VehicleType.UpdatedAt)
                }
            });
        }

        return uniqueVehicles.Count;
    }

    // ---------- Helpers ----------
    private static string NormalizeStr(string s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool StringEquals(string a, string b)
        => string.Equals(a, b, StringComparison.Ordinal);
}
