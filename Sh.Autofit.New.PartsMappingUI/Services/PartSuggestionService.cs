using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;
using System.Text.RegularExpressions;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class PartSuggestionService : IPartSuggestionService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;

    public PartSuggestionService(IDbContextFactory<ShAutofitContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<PartSuggestion>> GetSuggestionsForVehicleAsync(
        VehicleDisplayModel vehicle,
        List<PartDisplayModel> allParts)
    {
        return await GetSuggestionsForVehiclesAsync(new List<VehicleDisplayModel> { vehicle }, allParts);
    }

    public async Task<List<PartSuggestion>> GetSuggestionsForVehiclesAsync(
        List<VehicleDisplayModel> vehicles,
        List<PartDisplayModel> allParts)
    {
        var suggestionMap = new Dictionary<string, PartSuggestion>();

        // Get mapped parts for similar vehicles (for OEM association)
        var similarVehiclesParts = new Dictionary<string, List<string>>();
        foreach (var vehicle in vehicles)
        {
            var mappedParts = await GetMappedPartsForSimilarVehiclesAsync(vehicle);
            foreach (var kvp in mappedParts)
            {
                if (!similarVehiclesParts.ContainsKey(kvp.Key))
                    similarVehiclesParts[kvp.Key] = new List<string>();
                similarVehiclesParts[kvp.Key].AddRange(kvp.Value);
            }
        }

        // Score each part
        foreach (var part in allParts)
        {
            var suggestion = new PartSuggestion
            {
                PartNumber = part.PartNumber
            };

            var reasons = new List<SuggestionReason>();

            // Strategy 1: Name/Description Matching
            var nameMatchResult = CalculateNameMatchScoreWithDetails(vehicles, part);
            if (nameMatchResult.Score > 0)
            {
                reasons.Add(new SuggestionReason
                {
                    Strategy = SuggestionStrategy.NameMatch,
                    Description = nameMatchResult.Description,
                    Score = nameMatchResult.Score
                });
            }

            // Strategy 2: Code Similarity
            var codeSimilarityScore = CalculateCodeSimilarityScore(vehicles, part);
            if (codeSimilarityScore > 0)
            {
                reasons.Add(new SuggestionReason
                {
                    Strategy = SuggestionStrategy.CodeSimilarity,
                    Description = "Model code similar to part number",
                    Score = codeSimilarityScore
                });
            }

            // Strategy 3: OEM Association
            var oemAssociationScore = CalculateOemAssociationScore(part, similarVehiclesParts);
            if (oemAssociationScore > 0)
            {
                reasons.Add(new SuggestionReason
                {
                    Strategy = SuggestionStrategy.OemAssociation,
                    Description = "OEM numbers used by similar vehicles",
                    Score = oemAssociationScore
                });
            }

            // Strategy 4: Category Match
            var categoryMatchScore = CalculateCategoryMatchScore(vehicles, part);
            if (categoryMatchScore > 0)
            {
                reasons.Add(new SuggestionReason
                {
                    Strategy = SuggestionStrategy.CategoryMatch,
                    Description = "Category matches vehicle type",
                    Score = categoryMatchScore
                });
            }

            // Strategy 5: Manufacturer Match
            var manufacturerMatchScore = CalculateManufacturerMatchScore(vehicles, part);
            if (manufacturerMatchScore > 0)
            {
                reasons.Add(new SuggestionReason
                {
                    Strategy = SuggestionStrategy.ManufacturerMatch,
                    Description = "Part manufacturer matches vehicle",
                    Score = manufacturerMatchScore
                });
            }

            suggestion.Reasons = reasons;
            suggestion.RelevanceScore = reasons.Sum(r => r.Score);

            // Only include parts with some relevance
            if (suggestion.RelevanceScore > 0)
            {
                suggestionMap[part.PartNumber] = suggestion;
            }
        }

        return suggestionMap.Values.OrderByDescending(s => s.RelevanceScore).ToList();
    }

    private (double Score, string Description) CalculateNameMatchScoreWithDetails(List<VehicleDisplayModel> vehicles, PartDisplayModel part)
    {
        double maxScore = 0;
        var matchedItems = new List<string>();

        foreach (var vehicle in vehicles)
        {
            double score = 0;
            var currentMatches = new List<string>();
            var partText = $"{part.PartName} {part.Category}".ToLower();

            // Check manufacturer name
            if (!string.IsNullOrEmpty(vehicle.ManufacturerName) &&
                partText.Contains(vehicle.ManufacturerName.ToLower()))
            {
                score += 15;
                currentMatches.Add($"Manufacturer: {vehicle.ManufacturerName}");
            }

            // Check manufacturer short name
            if (!string.IsNullOrEmpty(vehicle.ManufacturerShortName) &&
                partText.Contains(vehicle.ManufacturerShortName.ToLower()))
            {
                score += 15;
                if (!currentMatches.Any(m => m.StartsWith("Manufacturer:")))
                    currentMatches.Add($"Manufacturer: {vehicle.ManufacturerShortName}");
            }

            // Check model name
            if (!string.IsNullOrEmpty(vehicle.ModelName) &&
                partText.Contains(vehicle.ModelName.ToLower()))
            {
                score += 25;
                currentMatches.Add($"Model: {vehicle.ModelName}");
            }

            // Check commercial name
            if (!string.IsNullOrEmpty(vehicle.CommercialName) &&
                partText.Contains(vehicle.CommercialName.ToLower()))
            {
                score += 30;
                currentMatches.Add($"Commercial name: {vehicle.CommercialName}");
            }

            // Check model code
            if (!string.IsNullOrEmpty(vehicle.ModelCode) &&
                partText.Contains(vehicle.ModelCode.ToLower()))
            {
                score += 20;
                currentMatches.Add($"Model code: {vehicle.ModelCode}");
            }

            // Check year match (last 2 digits)
            // Check both YearFrom and YearTo
            var yearsToCheck = new List<int?> { vehicle.YearFrom, vehicle.YearTo };
            foreach (var year in yearsToCheck)
            {
                if (year.HasValue)
                {
                    var yearStr = year.Value.ToString();
                    if (yearStr.Length >= 4)
                    {
                        // Get last 2 digits (e.g., "2007" -> "07")
                        var lastTwoDigits = yearStr.Substring(yearStr.Length - 2);

                        // Check if part text contains the year pattern
                        // Look for patterns like " 07", "-07", "_07", "/07" or "07 "
                        if (Regex.IsMatch(partText, $@"[\s\-_/]{lastTwoDigits}[\s\-_/]") ||
                            partText.StartsWith(lastTwoDigits) ||
                            partText.EndsWith(lastTwoDigits))
                        {
                            score += 18;
                            currentMatches.Add($"Year: '{lastTwoDigits} (from {yearStr})");
                            break; // Don't double-count if both YearFrom and YearTo match
                        }
                    }
                }
            }

            if (score > maxScore)
            {
                maxScore = score;
                matchedItems = currentMatches;
            }
        }

        var description = matchedItems.Any()
            ? $"Matched: {string.Join(", ", matchedItems)}"
            : string.Empty;

        return (Math.Min(maxScore, 45), description); // Cap at 45 points (increased from 40 to account for year matching)
    }

    private double CalculateCodeSimilarityScore(List<VehicleDisplayModel> vehicles, PartDisplayModel part)
    {
        double maxScore = 0;

        foreach (var vehicle in vehicles)
        {
            double score = 0;

            // Extract alphanumeric codes from model code and part number
            var modelCodeClean = ExtractAlphanumericPattern(vehicle.ModelCode ?? "");
            var partNumberClean = ExtractAlphanumericPattern(part.PartNumber);

            if (string.IsNullOrEmpty(modelCodeClean) || string.IsNullOrEmpty(partNumberClean))
                continue;

            // Check for common prefixes (e.g., "W123" in both)
            var commonPrefix = GetCommonPrefix(modelCodeClean, partNumberClean);
            if (commonPrefix.Length >= 3)
            {
                score += 15 + (commonPrefix.Length - 3) * 2; // More points for longer matches
            }

            // Check for substring matches
            if (partNumberClean.Contains(modelCodeClean) || modelCodeClean.Contains(partNumberClean))
            {
                score += 10;
            }

            // Check numeric patterns
            var modelNumbers = Regex.Matches(vehicle.ModelCode ?? "", @"\d+").Select(m => m.Value).ToList();
            var partNumbers = Regex.Matches(part.PartNumber, @"\d+").Select(m => m.Value).ToList();

            foreach (var modelNum in modelNumbers)
            {
                if (modelNum.Length >= 3 && partNumbers.Any(pn => pn.Contains(modelNum)))
                {
                    score += 8;
                }
            }

            maxScore = Math.Max(maxScore, score);
        }

        return Math.Min(maxScore, 25); // Cap at 25 points
    }

    private double CalculateOemAssociationScore(PartDisplayModel part, Dictionary<string, List<string>> similarVehiclesParts)
    {
        if (!part.OemNumbers.Any())
            return 0;

        double score = 0;
        int matchCount = 0;

        // Check if any of this part's OEM numbers appear in parts used by similar vehicles
        foreach (var oemNumber in part.OemNumbers)
        {
            if (similarVehiclesParts.ContainsKey(oemNumber))
            {
                matchCount++;
                score += 10;
            }
        }

        // Check if the part number itself is already used by similar vehicles
        if (similarVehiclesParts.Values.Any(parts => parts.Contains(part.PartNumber)))
        {
            score += 20;
        }

        return Math.Min(score, 30); // Cap at 30 points
    }

    private double CalculateCategoryMatchScore(List<VehicleDisplayModel> vehicles, PartDisplayModel part)
    {
        if (string.IsNullOrEmpty(part.Category))
            return 0;

        double score = 0;
        var partCategoryLower = part.Category.ToLower();

        foreach (var vehicle in vehicles)
        {
            // Match part category with vehicle category
            if (!string.IsNullOrEmpty(vehicle.VehicleCategory))
            {
                var vehicleCategoryLower = vehicle.VehicleCategory.ToLower();

                if (partCategoryLower.Contains(vehicleCategoryLower) ||
                    vehicleCategoryLower.Contains(partCategoryLower))
                {
                    score += 5;
                }
            }

            // Engine-related parts for vehicles with engine info
            if (!string.IsNullOrEmpty(vehicle.EngineModel))
            {
                if (partCategoryLower.Contains("engine") ||
                    partCategoryLower.Contains("motor") ||
                    part.PartName.ToLower().Contains(vehicle.EngineModel.ToLower()))
                {
                    score += 8;
                }
            }
        }

        return Math.Min(score, 15); // Cap at 15 points
    }

    private double CalculateManufacturerMatchScore(List<VehicleDisplayModel> vehicles, PartDisplayModel part)
    {
        if (string.IsNullOrEmpty(part.Manufacturer))
            return 0;

        double score = 0;
        var partMfgLower = part.Manufacturer.ToLower();

        foreach (var vehicle in vehicles)
        {
            // Exact manufacturer match
            if (partMfgLower == vehicle.ManufacturerName.ToLower() ||
                partMfgLower == vehicle.ManufacturerShortName?.ToLower())
            {
                score += 15;
            }
            // Partial match
            else if (partMfgLower.Contains(vehicle.ManufacturerName.ToLower()) ||
                     vehicle.ManufacturerName.ToLower().Contains(partMfgLower))
            {
                score += 8;
            }
        }

        return Math.Min(score, 15); // Cap at 15 points
    }

    public async Task<Dictionary<string, List<string>>> GetMappedPartsForSimilarVehiclesAsync(VehicleDisplayModel vehicle)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Find similar vehicles (same manufacturer, similar model, or same commercial name)
        var similarVehicleIds = await context.VehicleTypes
            .AsNoTracking()
            .Where(v => v.IsActive &&
                       v.VehicleTypeId != vehicle.VehicleTypeId &&
                       (v.ManufacturerId == vehicle.ManufacturerId ||
                        v.CommercialName == vehicle.CommercialName))
            .Select(v => v.VehicleTypeId)
            .Take(50) // Limit to avoid performance issues
            .ToListAsync();

        if (!similarVehicleIds.Any())
            return new Dictionary<string, List<string>>();

        // Get parts mapped to these similar vehicles
        var mappedParts = await context.VehiclePartsMappings
            .AsNoTracking()
            .Where(m => m.IsActive &&
                       m.IsCurrentVersion &&
                       similarVehicleIds.Contains(m.VehicleTypeId))
            .Select(m => m.PartItemKey)
            .Distinct()
            .ToListAsync();

        // Get OEM numbers for these parts
        var oemMapping = await context.VwParts
            .AsNoTracking()
            .Where(p => mappedParts.Contains(p.PartNumber))
            .Select(p => new
            {
                p.PartNumber,
                OemNumbers = new[] { p.Oemnumber1, p.Oemnumber2, p.Oemnumber3, p.Oemnumber4, p.Oemnumber5 }
            })
            .ToListAsync();

        // Build OEM number to part number mapping
        var result = new Dictionary<string, List<string>>();

        foreach (var part in oemMapping)
        {
            foreach (var oemNumber in part.OemNumbers)
            {
                if (!string.IsNullOrWhiteSpace(oemNumber))
                {
                    // Split by "/" to handle multiple OEM numbers in one field
                    var oemParts = oemNumber.Split('/', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(o => o.Trim())
                                           .Where(o => !string.IsNullOrWhiteSpace(o));

                    foreach (var oem in oemParts)
                    {
                        if (!result.ContainsKey(oem))
                            result[oem] = new List<string>();

                        if (!result[oem].Contains(part.PartNumber))
                            result[oem].Add(part.PartNumber);
                    }
                }
            }
        }

        return result;
    }

    private string ExtractAlphanumericPattern(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        // Remove special characters, keep only alphanumeric
        return Regex.Replace(input.ToUpper(), @"[^A-Z0-9]", "");
    }

    private string GetCommonPrefix(string str1, string str2)
    {
        int minLength = Math.Min(str1.Length, str2.Length);
        for (int i = 0; i < minLength; i++)
        {
            if (str1[i] != str2[i])
                return str1.Substring(0, i);
        }
        return str1.Substring(0, minLength);
    }
}
