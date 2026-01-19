using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Database;

public interface IPartDataService
{
    Task<PartInfo?> GetPartByItemKeyAsync(string itemKey);
    Task<List<PartInfo>> SearchPartsAsync(string searchTerm);
}
