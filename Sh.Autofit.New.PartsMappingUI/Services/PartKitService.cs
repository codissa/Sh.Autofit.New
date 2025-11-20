using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class PartKitService : IPartKitService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;
    private readonly IDataService _dataService;

    public PartKitService(IDbContextFactory<ShAutofitContext> contextFactory, IDataService dataService)
    {
        _contextFactory = contextFactory;
        _dataService = dataService;
    }

    public async Task<List<PartKitDisplayModel>> LoadAllKitsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var kits = await context.PartKits
            .AsNoTracking()
            .Where(k => k.IsActive)
            .OrderBy(k => k.KitName)
            .Select(k => new PartKitDisplayModel
            {
                PartKitId = k.PartKitId,
                KitName = k.KitName,
                Description = k.Description,
                IsActive = k.IsActive,
                CreatedBy = k.CreatedBy,
                CreatedAt = k.CreatedAt,
                UpdatedBy = k.UpdatedBy,
                UpdatedAt = k.UpdatedAt,
                PartCount = k.PartKitItems.Count
            })
            .ToListAsync();

        return kits;
    }

    public async Task<PartKitDisplayModel?> GetKitByIdAsync(int kitId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var kit = await context.PartKits
            .AsNoTracking()
            .Where(k => k.PartKitId == kitId)
            .Select(k => new PartKitDisplayModel
            {
                PartKitId = k.PartKitId,
                KitName = k.KitName,
                Description = k.Description,
                IsActive = k.IsActive,
                CreatedBy = k.CreatedBy,
                CreatedAt = k.CreatedAt,
                UpdatedBy = k.UpdatedBy,
                UpdatedAt = k.UpdatedAt,
                PartCount = k.PartKitItems.Count
            })
            .FirstOrDefaultAsync();

        return kit;
    }

    public async Task<int> CreateKitAsync(string kitName, string? description, string createdBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var kit = new PartKit
        {
            KitName = kitName,
            Description = description,
            IsActive = true,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        context.PartKits.Add(kit);
        await context.SaveChangesAsync();

        return kit.PartKitId;
    }

    public async Task UpdateKitAsync(int kitId, string kitName, string? description, string updatedBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var kit = await context.PartKits.FindAsync(kitId);
        if (kit == null)
            throw new InvalidOperationException($"Kit with ID {kitId} not found.");

        kit.KitName = kitName;
        kit.Description = description;
        kit.UpdatedBy = updatedBy;
        kit.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task DeleteKitAsync(int kitId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var kit = await context.PartKits.FindAsync(kitId);
        if (kit == null)
            throw new InvalidOperationException($"Kit with ID {kitId} not found.");

        kit.IsActive = false;
        await context.SaveChangesAsync();
    }

    public async Task<int> DuplicateKitAsync(int sourceKitId, string newKitName, string createdBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get source kit
        var sourceKit = await context.PartKits
            .Include(k => k.PartKitItems)
            .FirstOrDefaultAsync(k => k.PartKitId == sourceKitId);

        if (sourceKit == null)
            throw new InvalidOperationException($"Source kit with ID {sourceKitId} not found.");

        // Create new kit with same description
        var newKit = new PartKit
        {
            KitName = newKitName,
            Description = sourceKit.Description,
            IsActive = true,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        context.PartKits.Add(newKit);
        await context.SaveChangesAsync();

        // Copy all parts from source kit
        foreach (var sourceItem in sourceKit.PartKitItems)
        {
            var newItem = new PartKitItem
            {
                PartKitId = newKit.PartKitId,
                PartItemKey = sourceItem.PartItemKey,
                DisplayOrder = sourceItem.DisplayOrder,
                Notes = sourceItem.Notes,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            context.PartKitItems.Add(newItem);
        }

        await context.SaveChangesAsync();

        return newKit.PartKitId;
    }

    public async Task AddPartToKitAsync(int kitId, string partNumber, int? displayOrder, string? notes, string createdBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Check if part already exists in kit
        var exists = await context.PartKitItems
            .AnyAsync(i => i.PartKitId == kitId && i.PartItemKey == partNumber);

        if (exists)
            throw new InvalidOperationException($"Part {partNumber} is already in this kit.");

        var item = new PartKitItem
        {
            PartKitId = kitId,
            PartItemKey = partNumber,
            DisplayOrder = displayOrder,
            Notes = notes,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        context.PartKitItems.Add(item);
        await context.SaveChangesAsync();
    }

    public async Task RemovePartFromKitAsync(int kitItemId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var item = await context.PartKitItems.FindAsync(kitItemId);
        if (item == null)
            throw new InvalidOperationException($"Kit item with ID {kitItemId} not found.");

        context.PartKitItems.Remove(item);
        await context.SaveChangesAsync();
    }

    public async Task<List<PartKitItemDisplayModel>> GetKitPartsAsync(int kitId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var items = await context.PartKitItems
            .AsNoTracking()
            .Where(i => i.PartKitId == kitId)
            .OrderBy(i => i.DisplayOrder ?? int.MaxValue)
            .ThenBy(i => i.PartItemKey)
            .Join(
                context.VwParts,
                item => item.PartItemKey,
                part => part.PartNumber,
                (item, part) => new PartKitItemDisplayModel
                {
                    PartKitItemId = item.PartKitItemId,
                    PartKitId = item.PartKitId,
                    PartItemKey = item.PartItemKey,
                    DisplayOrder = item.DisplayOrder,
                    Notes = item.Notes,
                    CreatedBy = item.CreatedBy,
                    CreatedAt = item.CreatedAt,
                    PartName = part.PartName,
                    Category = part.Category,
                    IsInStock = part.IsInStock == 1
                })
            .ToListAsync();

        return items;
    }

    public async Task MapKitToVehiclesAsync(int kitId, List<int> vehicleTypeIds, string createdBy)
    {
        // Get all parts in the kit
        var kitParts = await GetKitPartsAsync(kitId);
        var partNumbers = kitParts.Select(p => p.PartItemKey).ToList();

        // Use existing mapping service to map all parts to vehicles
        await _dataService.MapPartsToVehiclesAsync(vehicleTypeIds, partNumbers, createdBy);
    }

    public async Task<(int VehiclesMapped, int MappingsCreated)> SyncKitMappingsAsync(int kitId, string createdBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get all parts in the kit
        var kitParts = await GetKitPartsAsync(kitId);
        if (!kitParts.Any())
            return (0, 0);

        var partNumbers = kitParts.Select(p => p.PartItemKey).ToList();

        // Collect all unique vehicles that any part in the kit is mapped to
        var allVehicleIds = new HashSet<int>();
        foreach (var partNumber in partNumbers)
        {
            var vehicles = await _dataService.LoadVehiclesForPartAsync(partNumber);
            foreach (var vehicle in vehicles)
            {
                allVehicleIds.Add(vehicle.VehicleTypeId);
            }
        }

        if (!allVehicleIds.Any())
            return (0, 0);

        // Get existing mappings to avoid duplicates
        var existingMappings = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.IsActive && partNumbers.Contains(m.PartItemKey) && allVehicleIds.Contains(m.VehicleTypeId))
            .Select(m => new { m.VehicleTypeId, m.PartItemKey })
            .ToListAsync();

        var existingMappingSet = new HashSet<(int VehicleTypeId, string PartItemKey)>(
            existingMappings.Select(m => (m.VehicleTypeId, m.PartItemKey))
        );

        // Create new mappings for all parts to all vehicles
        var mappingsCreated = 0;
        var now = DateTime.UtcNow;
        foreach (var vehicleId in allVehicleIds)
        {
            foreach (var partNumber in partNumbers)
            {
                // Skip if mapping already exists
                if (!existingMappingSet.Contains((vehicleId, partNumber)))
                {
                    var mapping = new VehiclePartsMapping
                    {
                        VehicleTypeId = vehicleId,
                        PartItemKey = partNumber,
                        MappingSource = "kit_sync",
                        Priority = 100,
                        RequiresModification = false,
                        VersionNumber = 1,
                        IsCurrentVersion = true,
                        IsActive = true,
                        CreatedBy = createdBy,
                        CreatedAt = now,
                        UpdatedBy = createdBy,
                        UpdatedAt = now
                    };
                    context.VehiclePartsMappings.Add(mapping);
                    mappingsCreated++;
                }
            }
        }

        if (mappingsCreated > 0)
        {
            await context.SaveChangesAsync();
        }

        return (allVehicleIds.Count, mappingsCreated);
    }

    public async Task<(int KitsSynced, int TotalMappingsCreated)> SyncAllKitsMappingsAsync(string createdBy)
    {
        var kits = await LoadAllKitsAsync();
        var totalMappingsCreated = 0;
        var kitsSynced = 0;

        foreach (var kit in kits)
        {
            try
            {
                var (_, mappingsCreated) = await SyncKitMappingsAsync(kit.PartKitId, createdBy);
                if (mappingsCreated > 0)
                {
                    kitsSynced++;
                }
                totalMappingsCreated += mappingsCreated;
            }
            catch (Exception)
            {
                // Continue with other kits even if one fails
                continue;
            }
        }

        return (kitsSynced, totalMappingsCreated);
    }
}
