using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

/// <summary>
/// Service for generating smart mapping suggestions based on existing mappings and similar models
/// </summary>
public interface ISmartSuggestionsService
{
    /// <summary>
    /// Generate smart suggestions by finding similar models without the same parts
    /// </summary>
    /// <param name="minScore">Minimum confidence score (default: 70)</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return (default: 100)</param>
    /// <param name="manufacturerFilter">Optional manufacturer filter</param>
    /// <param name="categoryFilter">Optional part category filter</param>
    /// <returns>List of smart suggestions ordered by score</returns>
    Task<List<SmartSuggestion>> GenerateSuggestionsAsync(
        double minScore = 70,
        int maxSuggestions = 100,
        string? manufacturerFilter = null,
        string? categoryFilter = null);

    /// <summary>
    /// Accept a smart suggestion and map the part to all selected target models
    /// </summary>
    /// <param name="suggestion">The suggestion to accept</param>
    /// <param name="createdBy">User who is accepting the suggestion</param>
    /// <returns>Number of vehicles mapped</returns>
    Task<int> AcceptSuggestionAsync(SmartSuggestion suggestion, string createdBy);

    /// <summary>
    /// Accept multiple smart suggestions in batch
    /// </summary>
    /// <param name="suggestions">List of suggestions to accept</param>
    /// <param name="createdBy">User who is accepting the suggestions</param>
    /// <returns>Total number of vehicles mapped</returns>
    Task<int> AcceptSuggestionsAsync(List<SmartSuggestion> suggestions, string createdBy);

    /// <summary>
    /// Generate smart suggestions in batches to allow progressive UI updates
    /// </summary>
    /// <param name="minScore">Minimum confidence score (default: 70)</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return (default: 100)</param>
    /// <param name="batchSize">Number of suggestions per batch (default: 50)</param>
    /// <param name="manufacturerFilter">Optional manufacturer filter</param>
    /// <param name="categoryFilter">Optional part category filter</param>
    /// <returns>AsyncEnumerable of suggestion batches</returns>
    IAsyncEnumerable<List<SmartSuggestion>> GenerateSuggestionsInBatchesAsync(
        double minScore = 70,
        int maxSuggestions = 100,
        int batchSize = 50,
        string? manufacturerFilter = null,
        string? categoryFilter = null);
}
