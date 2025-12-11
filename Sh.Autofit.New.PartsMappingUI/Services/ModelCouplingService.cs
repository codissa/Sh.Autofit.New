using Microsoft.EntityFrameworkCore;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.PartsMappingUI.Models;

namespace Sh.Autofit.New.PartsMappingUI.Services;

public class ModelCouplingService : IModelCouplingService
{
    private readonly IDbContextFactory<ShAutofitContext> _contextFactory;
    private readonly IDataService _dataService;

    public ModelCouplingService(
        IDbContextFactory<ShAutofitContext> contextFactory,
        IDataService dataService)
    {
        _contextFactory = contextFactory;
        _dataService = dataService;
    }

    public async Task<CouplingResult> AutoCoupleModelsAsync(
        List<int> consolidatedModelIds,
        string createdBy)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var result = new CouplingResult();

        // Load the models to analyze
        var modelsToAnalyze = await context.ConsolidatedVehicleModels
            .Include(m => m.Manufacturer)
            .Where(m => consolidatedModelIds.Contains(m.ConsolidatedModelId) && m.IsActive)
            .ToListAsync();

        // For each new model, find potential coupling partners
        foreach (var model in modelsToAnalyze)
        {
            // Find all models with same specs (manufacturer, model name, engine, transmission, trim)
            // but different year ranges that might overlap
            var similarModels = await context.ConsolidatedVehicleModels
                .Include(m => m.Manufacturer)
                .Where(m => m.IsActive &&
                    m.ConsolidatedModelId != model.ConsolidatedModelId &&
                    m.ManufacturerId == model.ManufacturerId &&
                    m.ModelName == model.ModelName &&
                    m.EngineVolume == model.EngineVolume &&
                    m.TransmissionType == model.TransmissionType &&
                    m.TrimLevel == model.TrimLevel &&
                    // Year ranges must overlap
                    !(m.YearTo < model.YearFrom || (model.YearTo.HasValue && model.YearTo < m.YearFrom)))
                .ToListAsync();

            foreach (var similarModel in similarModels)
            {
                // Check if coupling already exists
                var idA = Math.Min(model.ConsolidatedModelId, similarModel.ConsolidatedModelId);
                var idB = Math.Max(model.ConsolidatedModelId, similarModel.ConsolidatedModelId);

                var existingCoupling = await context.ModelCouplings
                    .FirstOrDefaultAsync(mc =>
                        mc.ConsolidatedModelIdA == idA &&
                        mc.ConsolidatedModelIdB == idB &&
                        mc.IsActive);

                if (existingCoupling != null)
                    continue; // Already coupled

                // Calculate overlap years
                var yearsOverlap = CalculateOverlapYears(model, similarModel);

                // SCENARIO 1: Small Overlap (≤3 years)
                if (yearsOverlap > 0 && yearsOverlap <= 3)
                {
                    await _dataService.CreateModelCouplingAsync(
                        idA, idB,
                        "SmallOverlap",
                        $"Auto-coupled: {yearsOverlap} years overlap",
                        createdBy);

                    result.CouplingDetails.Add((idA, idB, "SmallOverlap"));
                    result.TotalCouplingsCreated++;
                    continue;
                }

                // SCENARIO 2: Nested Range (one contains the other)
                var modelYearTo = model.YearTo ?? int.MaxValue;
                var similarYearTo = similarModel.YearTo ?? int.MaxValue;

                var isNested = (model.YearFrom <= similarModel.YearFrom && modelYearTo >= similarYearTo) ||
                               (similarModel.YearFrom <= model.YearFrom && similarYearTo >= modelYearTo);

                if (isNested)
                {
                    await _dataService.CreateModelCouplingAsync(
                        idA, idB,
                        "NestedRange",
                        "Auto-coupled: Nested year ranges",
                        createdBy);

                    result.CouplingDetails.Add((idA, idB, "NestedRange"));
                    result.TotalCouplingsCreated++;
                }
            }
        }

        return result;
    }

    private int CalculateOverlapYears(ConsolidatedVehicleModel m1, ConsolidatedVehicleModel m2)
    {
        var m1YearTo = m1.YearTo ?? int.MaxValue;
        var m2YearTo = m2.YearTo ?? int.MaxValue;

        var overlapStart = Math.Max(m1.YearFrom, m2.YearFrom);
        var overlapEnd = Math.Min(m1YearTo, m2YearTo);

        // If overlapEnd < overlapStart, there's no overlap
        if (overlapEnd < overlapStart)
            return 0;

        return overlapEnd - overlapStart + 1;
    }
}
