using Sh.Autofit.New.Entities.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IVirtualPartService
{
    /// <summary>
    /// Creates a new virtual part
    /// </summary>
    Task<VirtualPart> CreateVirtualPartAsync(
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
        string createdBy = "User");

    /// <summary>
    /// Loads all active virtual parts
    /// </summary>
    Task<List<VirtualPart>> LoadActiveVirtualPartsAsync();

    /// <summary>
    /// Gets a virtual part by part number
    /// </summary>
    Task<VirtualPart?> GetVirtualPartByPartNumberAsync(string partNumber);

    /// <summary>
    /// Updates an existing virtual part
    /// </summary>
    Task UpdateVirtualPartAsync(
        int virtualPartId,
        string description,
        string notes,
        string oem1,
        string? oem2,
        string? oem3,
        string? oem4,
        string? oem5,
        string? category,
        string updatedBy);

    /// <summary>
    /// Deletes (soft delete) a virtual part
    /// </summary>
    Task DeleteVirtualPartAsync(int virtualPartId);
}
