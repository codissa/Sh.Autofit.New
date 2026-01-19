namespace Sh.Autofit.StickerPrinting.Services.Database;

public interface IArabicDescriptionService
{
    Task<string?> GetArabicDescriptionAsync(string itemKey);
    Task SaveArabicDescriptionAsync(string itemKey, string description, string userName);
    Task DeleteArabicDescriptionAsync(string itemKey);
}
