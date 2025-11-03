using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public interface IPartSuggestionService
{
    /// <summary>
    /// Get part suggestions for a single vehicle
    /// </summary>
    Task<List<PartSuggestion>> GetSuggestionsForVehicleAsync(
        VehicleDisplayModel vehicle,
        List<PartDisplayModel> allParts);

    /// <summary>
    /// Get part suggestions for multiple vehicles
    /// </summary>
    Task<List<PartSuggestion>> GetSuggestionsForVehiclesAsync(
        List<VehicleDisplayModel> vehicles,
        List<PartDisplayModel> allParts);

    /// <summary>
    /// Get already mapped parts for similar vehicles (for OEM association)
    /// </summary>
    Task<Dictionary<string, List<string>>> GetMappedPartsForSimilarVehiclesAsync(
        VehicleDisplayModel vehicle);
}
