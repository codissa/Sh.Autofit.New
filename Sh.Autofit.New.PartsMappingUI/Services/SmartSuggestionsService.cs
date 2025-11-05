using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Helpers;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class SmartSuggestionsService : ISmartSuggestionsService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;
    private readonly IDataService _dataService;

    public SmartSuggestionsService(
        IDbContextFactory<ShAutofitContext> contextFactory,
        IDataService dataService)
    {
        _contextFactory = contextFactory;
        _dataService = dataService;
    }

    public async Task<List<SmartSuggestion>> GenerateSuggestionsAsync(
        double minScore = 70,
        int maxSuggestions = 100,
        string? manufacturerFilter = null,
        string? categoryFilter = null)
    {
        // Step 1: Load all vehicles and parts data
        var allVehicles = await _dataService.LoadVehiclesAsync();
        var allParts = await _dataService.LoadPartsAsync();

        // Step 2: Find all mapped models (models that have at least one part mapped)
        var mappedModels = await GetMappedModelsAsync();

        // Step 3: Get mapping counts for all parts
        var partMappingCounts = await LoadPartMappingCountsWithDetailsAsync();

        // Step 4: Generate suggestions
        var rawSuggestions = new List<(SmartSuggestion Suggestion, double Score)>();

        foreach (var sourceModel in mappedModels)
        {
            // Apply manufacturer filter
            if (!string.IsNullOrEmpty(manufacturerFilter) &&
                !sourceModel.ManufacturerName.EqualsIgnoringWhitespace(manufacturerFilter))
                continue;

            // Get parts mapped to this source model
            var mappedParts = await GetMappedPartsForModelAsync(
                sourceModel.ManufacturerName,
                sourceModel.ModelName);

            foreach (var part in mappedParts)
            {
                // Apply category filter
                if (!string.IsNullOrEmpty(categoryFilter) &&
                    !part.Category.EqualsIgnoringWhitespace(categoryFilter))
                    continue;

                // Find similar target models that DON'T have this part
                var targetModels = await FindSimilarUnmappedModelsAsync(
                    sourceModel,
                    part.PartNumber,
                    allVehicles);

                if (!targetModels.Any())
                    continue;

                // Score each target model and create suggestion
                var scoredTargets = new List<TargetModel>();
                var scoreBreakdown = new List<string>();
                double totalScore = 0;

                foreach (var target in targetModels)
                {
                    var targetScore = CalculateTargetModelScore(
                        sourceModel, target, part, partMappingCounts,
                        out var breakdown);

                    target.TargetScore = targetScore;
                    scoredTargets.Add(target);
                    scoreBreakdown.AddRange(breakdown);
                    totalScore = Math.Max(totalScore, targetScore);
                }

                // Only include if score meets minimum threshold
                if (totalScore < minScore)
                    continue;

                // Get additional stats
                var sourceVehicleCount = allVehicles.Count(v =>
                    v.ManufacturerName.EqualsIgnoringWhitespace(sourceModel.ManufacturerName) &&
                    v.ModelName.EqualsIgnoringWhitespace(sourceModel.ModelName));

                var otherModelsWithPart = partMappingCounts.ContainsKey(part.PartNumber)
                    ? partMappingCounts[part.PartNumber].UniqueModels
                    : 1;

                // Create suggestion
                var suggestion = new SmartSuggestion
                {
                    PartNumber = part.PartNumber,
                    PartName = part.PartName,
                    Category = part.Category ?? "Unknown",
                    SourceManufacturer = sourceModel.ManufacturerName,
                    SourceModelName = sourceModel.ModelName,
                    SourceCommercialName = sourceModel.CommercialName ?? "",
                    SourceYearFrom = sourceModel.YearFrom,
                    SourceYearTo = sourceModel.YearTo,
                    SourceEngineVolume = sourceModel.EngineVolume,
                    SourceVehicleCount = sourceVehicleCount,
                    OtherModelsWithPart = otherModelsWithPart,
                    TargetModels = scoredTargets.OrderByDescending(t => t.TargetScore).ToList(),
                    TotalTargetVehicles = scoredTargets.Sum(t => t.VehicleCount),
                    Score = totalScore,
                    ScoreBreakdown = scoreBreakdown.Distinct().ToList(),
                    ScoreReason = string.Join(", ", scoreBreakdown.Distinct().Take(3))
                };

                rawSuggestions.Add((suggestion, totalScore));
            }
        }

        // Step 5: Group by (PartNumber, SourceModel) and rank
        var groupedSuggestions = rawSuggestions
            .GroupBy(s => new { s.Suggestion.PartNumber, s.Suggestion.SourceModelName })
            .Select(g => g.OrderByDescending(x => x.Score).First().Suggestion)
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.TotalTargetVehicles)
            .Take(maxSuggestions)
            .ToList();

        return groupedSuggestions;
    }

    public async Task<int> AcceptSuggestionAsync(SmartSuggestion suggestion, string createdBy)
    {
        return await AcceptSuggestionsAsync(new List<SmartSuggestion> { suggestion }, createdBy);
    }

    public async Task<int> AcceptSuggestionsAsync(List<SmartSuggestion> suggestions, string createdBy)
    {
        int totalVehiclesMapped = 0;

        await using var context = await _contextFactory.CreateDbContextAsync();
        var allVehicles = await context.VehicleTypes.AsNoTracking().ToListAsync();

        foreach (var suggestion in suggestions)
        {
            // Get all selected target models
            var selectedTargets = suggestion.TargetModels.Where(t => t.IsSelected).ToList();

            foreach (var target in selectedTargets)
            {
                // Get ALL vehicle IDs for this model (model-level mapping)
                var vehicleIds = allVehicles
                    .Where(v => v.IsActive &&
                               v.Manufacturer.ManufacturerName.EqualsIgnoringWhitespace(target.ManufacturerName) &&
                               v.ModelName.EqualsIgnoringWhitespace(target.ModelName))
                    .Select(v => v.VehicleTypeId)
                    .ToList();

                if (vehicleIds.Any())
                {
                    await _dataService.MapPartsToVehiclesAsync(
                        vehicleIds,
                        new List<string> { suggestion.PartNumber },
                        createdBy);

                    totalVehiclesMapped += vehicleIds.Count;
                }
            }
        }

        return totalVehiclesMapped;
    }

    #region Private Helper Methods

    private async Task<List<(string ManufacturerName, string ModelName, string CommercialName, int YearFrom, int YearTo, int EngineVolume)>>
        GetMappedModelsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get all models that have at least one active mapping
        var mappedModels = await context.VehicleTypes
            .AsNoTracking()
            .Include(v => v.Manufacturer)
            .Where(v => v.IsActive &&
                       context.VehiclePartsMappings.Any(m =>
                           m.VehicleTypeId == v.VehicleTypeId &&
                           m.IsActive &&
                           m.IsCurrentVersion))
            .Select(v => new
            {
                v.Manufacturer.ManufacturerName,
                v.ModelName,
                v.CommercialName,
                v.YearFrom,
                v.YearTo,
                v.EngineVolume
            })
            .Distinct()
            .ToListAsync();

        return mappedModels
            .GroupBy(m => new { m.ManufacturerName, m.ModelName })
            .Select(g => g.First())
            .Select(m => (
                m.ManufacturerName,
                m.ModelName,
                m.CommercialName ?? "",
                m.YearFrom ?? 0,
                m.YearTo ?? m.YearFrom ?? 0,
                m.EngineVolume ?? 0))
            .ToList();
    }

    private async Task<List<PartDisplayModel>> GetMappedPartsForModelAsync(
        string manufacturerName,
        string modelName)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var parts = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.IsActive &&
                       m.IsCurrentVersion &&
                       m.VehicleType.Manufacturer.ManufacturerName == manufacturerName &&
                       m.VehicleType.ModelName == modelName)
            .Join(context.VwParts,
                m => m.PartItemKey,
                p => p.PartNumber,
                (m, p) => p)
            .Distinct()
            .Select(p => new PartDisplayModel
            {
                PartNumber = p.PartNumber,
                PartName = p.PartName ?? "",
                Category = p.Category,
                Manufacturer = p.Manufacturer,
                UniversalPart = p.UniversalPart ?? false,
                OemNumber1 = p.Oemnumber1,
                OemNumber2 = p.Oemnumber2,
                OemNumber3 = p.Oemnumber3,
                OemNumber4 = p.Oemnumber4,
                OemNumber5 = p.Oemnumber5
            })
            .ToListAsync();

        return parts;
    }

    private async Task<List<TargetModel>> FindSimilarUnmappedModelsAsync(
        (string ManufacturerName, string ModelName, string CommercialName, int YearFrom, int YearTo, int EngineVolume) sourceModel,
        string partNumber,
        List<VehicleDisplayModel> allVehicles)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Get vehicles that already have this part mapped
        var alreadyMappedVehicleIds = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.IsActive &&
                       m.IsCurrentVersion &&
                       m.PartItemKey == partNumber)
            .Select(m => m.VehicleTypeId)
            .ToHashSet();

        // Find similar models
        var similarModels = allVehicles
            .Where(v =>
                // Different model name
                !v.ModelName.EqualsIgnoringWhitespace(sourceModel.ModelName) &&

                // Same manufacturer (required for now - could be relaxed for cross-manufacturer)
                v.ManufacturerName.EqualsIgnoringWhitespace(sourceModel.ManufacturerName) &&

                // Same engine volume (REQUIRED)
                v.EngineVolume.HasValue &&
                v.EngineVolume.Value == sourceModel.EngineVolume &&

                // Overlapping years (REQUIRED)
                v.YearFrom.HasValue &&
                ((v.YearFrom.Value >= sourceModel.YearFrom && v.YearFrom.Value <= sourceModel.YearTo) ||
                 (sourceModel.YearFrom >= v.YearFrom.Value && sourceModel.YearFrom <= (v.YearTo ?? v.YearFrom.Value))) &&

                // NOT already mapped to this part
                !alreadyMappedVehicleIds.Contains(v.VehicleTypeId))
            .ToList();

        // Group by model name to create TargetModel entries
        var targetModels = similarModels
            .GroupBy(v => new { v.ManufacturerName, v.ModelName })
            .Select(g => new TargetModel
            {
                ManufacturerName = g.Key.ManufacturerName,
                ModelName = g.Key.ModelName,
                CommercialName = g.FirstOrDefault()?.CommercialName ?? "",
                YearFrom = g.Min(v => v.YearFrom) ?? 0,
                YearTo = g.Max(v => v.YearTo ?? v.YearFrom) ?? 0,
                EngineVolume = g.First().EngineVolume ?? 0,
                VehicleCount = g.Count(),
                HasCommercialNameMatch = !string.IsNullOrEmpty(sourceModel.CommercialName) &&
                                        g.FirstOrDefault()?.CommercialName.EqualsIgnoringWhitespace(sourceModel.CommercialName) == true,
                HasYearOverlap = true // Already filtered above
            })
            .ToList();

        return targetModels;
    }

    private double CalculateTargetModelScore(
        (string ManufacturerName, string ModelName, string CommercialName, int YearFrom, int YearTo, int EngineVolume) source,
        TargetModel target,
        PartDisplayModel part,
        Dictionary<string, (int TotalVehicles, int UniqueModels)> partMappingCounts,
        out List<string> breakdown)
    {
        breakdown = new List<string>();
        double score = 0;

        // Base similarity (REQUIRED to even get here)
        score += 40;
        breakdown.Add("Same engine volume");

        score += 30;
        breakdown.Add("Overlapping years");

        // Bonus: Same commercial name
        if (target.HasCommercialNameMatch)
        {
            score += 20;
            breakdown.Add("Same commercial name");
        }

        // Bonus: Same manufacturer (always true for now)
        score += 10;
        breakdown.Add("Same manufacturer");

        // Bonus: OEM number similarity
        if (part.HasOemNumbers)
        {
            var oemScore = OemPatternMatcher.FindBestOemMatch(
                part.OemNumbers,
                part.OemNumbers, // Would need to get target vehicle OEMs for better matching
                source.ManufacturerName);

            if (oemScore > 0)
            {
                score += Math.Min(oemScore, 25);
                breakdown.Add($"OEM similarity (+{oemScore:F0})");
            }
        }

        // Bonus: Mapping confidence (how widely is this part used?)
        if (partMappingCounts.ContainsKey(part.PartNumber))
        {
            var stats = partMappingCounts[part.PartNumber];

            if (stats.TotalVehicles > 50)
            {
                score += 20; // Very widely used
                breakdown.Add("Very widely used part (50+ vehicles)");
            }
            else if (stats.TotalVehicles > 20)
            {
                score += 10; // Widely used
                breakdown.Add("Widely used part (20+ vehicles)");
            }

            if (stats.UniqueModels > 2)
            {
                score += 5; // Used across models
                breakdown.Add($"Used by {stats.UniqueModels} models");
            }
        }

        // Bonus: Universal part
        if (part.UniversalPart)
        {
            score += 5;
            breakdown.Add("Universal part");
        }

        target.OemSimilarityScore = score;
        return score;
    }

    private async Task<Dictionary<string, (int TotalVehicles, int UniqueModels)>>
        LoadPartMappingCountsWithDetailsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var mappingStats = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.IsActive && m.IsCurrentVersion)
            .GroupBy(m => m.PartItemKey)
            .Select(g => new
            {
                PartNumber = g.Key,
                TotalVehicles = g.Count(),
                UniqueModels = g.Select(m => m.VehicleType.ModelName).Distinct().Count()
            })
            .ToListAsync();

        return mappingStats.ToDictionary(
            s => s.PartNumber,
            s => (s.TotalVehicles, s.UniqueModels));
    }

    #endregion
}
