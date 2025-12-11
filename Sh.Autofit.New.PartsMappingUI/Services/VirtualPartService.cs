using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class VirtualPartService : IVirtualPartService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

    public VirtualPartService(IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<VirtualPart> CreateVirtualPartAsync(
        string description,
        string notes,
        string oem1,
        string? oem2 = null,
        string? oem3 = null,
        string? oem4 = null,
        string? oem5 = null,
        string? category = null,
        int? vehicleTypeId = null,
        int? consolidatedModelId = null,
        string createdBy = "User")
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Part number = first OEM (trimmed)
        var partNumber = oem1.Trim();

        // Check if an active virtual part with this part number already exists
        var existingActive = await context.VirtualParts
            .FirstOrDefaultAsync(vp => vp.PartNumber == partNumber && vp.IsActive);

        if (existingActive != null)
            throw new InvalidOperationException($"Virtual part with part number '{partNumber}' already exists");

        // Also check if it conflicts with a real part
        var realPartExists = await context.VwParts
            .AnyAsync(p => p.PartNumber == partNumber && p.IsActive == 1);

        if (realPartExists)
            throw new InvalidOperationException($"A real part with part number '{partNumber}' already exists. Cannot create virtual part.");

        // Check if there's an inactive (deleted) virtual part with this part number
        var existingInactive = await context.VirtualParts
            .FirstOrDefaultAsync(vp => vp.PartNumber == partNumber && !vp.IsActive);

        VirtualPart virtualPart;

        if (existingInactive != null)
        {
            // Reactivate and update the existing inactive virtual part
            existingInactive.PartName = description.Trim();
            existingInactive.Notes = string.IsNullOrWhiteSpace(notes) ? "" : notes.Trim();
            existingInactive.OemNumber1 = oem1.Trim();
            existingInactive.OemNumber2 = oem2?.Trim();
            existingInactive.OemNumber3 = oem3?.Trim();
            existingInactive.OemNumber4 = oem4?.Trim();
            existingInactive.OemNumber5 = oem5?.Trim();
            existingInactive.Category = category?.Trim();
            existingInactive.CreatedForVehicleTypeId = vehicleTypeId;
            existingInactive.CreatedForConsolidatedModelId = consolidatedModelId;
            existingInactive.CreatedBy = createdBy;
            existingInactive.CreatedAt = DateTime.UtcNow;
            existingInactive.UpdatedAt = DateTime.UtcNow;
            existingInactive.IsActive = true;

            virtualPart = existingInactive;
        }
        else
        {
            // Create new virtual part
            virtualPart = new VirtualPart
            {
                PartNumber = partNumber,
                PartName = description.Trim(),
                Notes = string.IsNullOrWhiteSpace(notes) ? "" : notes.Trim(),
                OemNumber1 = oem1.Trim(),
                OemNumber2 = oem2?.Trim(),
                OemNumber3 = oem3?.Trim(),
                OemNumber4 = oem4?.Trim(),
                OemNumber5 = oem5?.Trim(),
                Category = category?.Trim(),
                CreatedForVehicleTypeId = vehicleTypeId,
                CreatedForConsolidatedModelId = consolidatedModelId,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            context.VirtualParts.Add(virtualPart);
        }

        await context.SaveChangesAsync();

        return virtualPart;
    }

    public async Task<List<VirtualPart>> LoadActiveVirtualPartsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.VirtualParts
            .Where(vp => vp.IsActive)
            .OrderByDescending(vp => vp.CreatedAt)
            .ToListAsync();
    }

    public async Task<VirtualPart?> GetVirtualPartByPartNumberAsync(string partNumber)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.VirtualParts
            .FirstOrDefaultAsync(vp => vp.PartNumber == partNumber && vp.IsActive);
    }

    public async Task UpdateVirtualPartAsync(
        int virtualPartId,
        string description,
        string notes,
        string oem1,
        string? oem2,
        string? oem3,
        string? oem4,
        string? oem5,
        string? category,
        string updatedBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var virtualPart = await context.VirtualParts.FindAsync(virtualPartId);

        if (virtualPart == null || !virtualPart.IsActive)
            throw new InvalidOperationException("Virtual part not found");

        // Update properties
        virtualPart.PartName = description.Trim();
        virtualPart.Notes = notes.Trim();
        virtualPart.OemNumber1 = oem1.Trim();
        virtualPart.OemNumber2 = oem2?.Trim();
        virtualPart.OemNumber3 = oem3?.Trim();
        virtualPart.OemNumber4 = oem4?.Trim();
        virtualPart.OemNumber5 = oem5?.Trim();
        virtualPart.Category = category;
        virtualPart.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task DeleteVirtualPartAsync(int virtualPartId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var virtualPart = await context.VirtualParts.FindAsync(virtualPartId);

        if (virtualPart != null)
        {
            virtualPart.IsActive = false;
            virtualPart.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }
}
