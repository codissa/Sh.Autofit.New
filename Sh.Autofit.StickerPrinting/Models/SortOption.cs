namespace Sh.Autofit.StickerPrinting.Models;

/// <summary>
/// Sorting options for Stock Move items list
/// </summary>
public enum SortOption
{
    /// <summary>
    /// Sort by Localization (Location) alphabetically - DEFAULT
    /// </summary>
    Localization,

    /// <summary>
    /// Sort by ItemKey alphabetically
    /// </summary>
    ItemKey,

    /// <summary>
    /// Original order from SQL query (no client-side sorting)
    /// </summary>
    SqlOrder
}
