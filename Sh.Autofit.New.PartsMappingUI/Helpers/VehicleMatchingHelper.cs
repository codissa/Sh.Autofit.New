using Sh.Autofit.New.PartsMappingUI.Models;
using System.Collections.Generic;
using System.Linq;

namespace Sh.Autofit.New.PartsMappingUI.Helpers;

/// <summary>
/// Helper class for matching vehicles based on exact criteria including engine volume,
/// fuel type, transmission type, and trim level.
/// </summary>
public static class VehicleMatchingHelper
{
    /// <summary>
    /// Compares two vehicles for an exact match based on all available criteria:
    /// - Manufacturer name
    /// - Model name
    /// - Engine volume
    /// - Fuel type
    /// - Transmission type
    /// - Trim level
    /// </summary>
    public static bool IsExactMatch(VehicleDisplayModel vehicle, GovernmentVehicleRecord govRecord)
    {
        // Basic match on manufacturer and model (required)
        if (!vehicle.ManufacturerName.EqualsIgnoringWhitespace(govRecord.ManufacturerName))
            return false;

        if (!vehicle.ModelName.EqualsIgnoringWhitespace(govRecord.ModelName))
            return false;

        // Match engine volume (if available in both)
        if (vehicle.EngineVolume.HasValue && govRecord.EngineVolume.HasValue)
        {
            if (vehicle.EngineVolume.Value != govRecord.EngineVolume.Value)
                return false;
        }

        // Match fuel type (if available in both)
        if (!string.IsNullOrEmpty(vehicle.FuelTypeName) && !string.IsNullOrEmpty(govRecord.FuelType))
        {
            if (!vehicle.FuelTypeName.EqualsIgnoringWhitespace(govRecord.FuelType))
                return false;
        }

        // Match transmission type (if available in vehicle)
        // Note: Government record doesn't have transmission, but we keep it for future compatibility
        // For now, this is just a placeholder

        // Match finish level / merkav (if available in both)
        // VehicleType.FinishLevel maps to GovernmentVehicleRecord.ModelType (sug_degem / מרכב)
        if (!string.IsNullOrEmpty(vehicle.FinishLevel) && !string.IsNullOrEmpty(govRecord.ModelType))
        {
            if (!vehicle.FinishLevel.EqualsIgnoringWhitespace(govRecord.ModelType))
                return false;
        }

        // Match trim level / ramat gimur (if available in both)
        // VehicleType.TrimLevel maps to GovernmentVehicleRecord.TrimLevel (ramat_gimur / רמת גימור)
        if (!string.IsNullOrEmpty(vehicle.TrimLevel) && !string.IsNullOrEmpty(govRecord.TrimLevel))
        {
            if (!vehicle.TrimLevel.EqualsIgnoringWhitespace(govRecord.TrimLevel))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Filters a list of vehicles to only those that exactly match the government record
    /// based on all available criteria.
    /// </summary>
    public static List<VehicleDisplayModel> GetExactMatches(
        IEnumerable<VehicleDisplayModel> vehicles,
        GovernmentVehicleRecord govRecord)
    {
        return vehicles.Where(v => IsExactMatch(v, govRecord)).ToList();
    }

    /// <summary>
    /// Gets vehicles that match the manufacturer and model name, grouped by their unique characteristics.
    /// This helps identify all variants of the same model (different engine, transmission, trim, etc.)
    /// </summary>
    public static List<VehicleDisplayModel> GetModelVariants(
        IEnumerable<VehicleDisplayModel> vehicles,
        string manufacturerName,
        string modelName)
    {
        return vehicles
            .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(manufacturerName) &&
                       v.ModelName.EqualsIgnoringWhitespace(modelName))
            .OrderBy(v => v.EngineVolume)
            .ThenBy(v => v.FuelTypeName)
            .ThenBy(v => v.TransmissionType)
            .ThenBy(v => v.TrimLevel)
            .ThenBy(v => v.YearFrom)
            .ToList();
    }

    /// <summary>
    /// Creates a grouping key for vehicles based on their distinguishing characteristics.
    /// This is used to group vehicles that should be treated as the same variant.
    /// </summary>
    public static string GetVehicleVariantKey(VehicleDisplayModel vehicle)
    {
        var parts = new List<string>
        {
            vehicle.ManufacturerName.NormalizeForGrouping(),
            vehicle.ModelName.NormalizeForGrouping()
        };

        if (vehicle.EngineVolume.HasValue)
            parts.Add(vehicle.EngineVolume.Value.ToString());

        if (!string.IsNullOrEmpty(vehicle.FuelTypeName))
            parts.Add(vehicle.FuelTypeName.NormalizeForGrouping());

        if (!string.IsNullOrEmpty(vehicle.TransmissionType))
            parts.Add(vehicle.TransmissionType.NormalizeForGrouping());

        if (!string.IsNullOrEmpty(vehicle.FinishLevel))
            parts.Add(vehicle.FinishLevel.NormalizeForGrouping());

        if (!string.IsNullOrEmpty(vehicle.TrimLevel))
            parts.Add(vehicle.TrimLevel.NormalizeForGrouping());

        return string.Join("|", parts);
    }

    /// <summary>
    /// Gets all vehicles that share the same variant characteristics
    /// (same model, engine, transmission, trim, etc.)
    /// </summary>
    public static List<VehicleDisplayModel> GetSameVariantVehicles(
        IEnumerable<VehicleDisplayModel> allVehicles,
        VehicleDisplayModel referenceVehicle)
    {
        var referenceKey = GetVehicleVariantKey(referenceVehicle);

        return allVehicles
            .Where(v => GetVehicleVariantKey(v) == referenceKey)
            .ToList();
    }

    /// <summary>
    /// Creates a human-readable description of what makes this vehicle variant unique.
    /// </summary>
    public static string GetVariantDescription(VehicleDisplayModel vehicle)
    {
        var parts = new List<string> { vehicle.ModelName };

        if (vehicle.EngineVolume.HasValue)
            parts.Add($"{vehicle.EngineVolume}cc");

        if (!string.IsNullOrEmpty(vehicle.FuelTypeName))
            parts.Add(vehicle.FuelTypeName);

        if (!string.IsNullOrEmpty(vehicle.TransmissionType))
            parts.Add(vehicle.TransmissionType);

        if (!string.IsNullOrEmpty(vehicle.FinishLevel))
            parts.Add($"({vehicle.FinishLevel})");

        if (!string.IsNullOrEmpty(vehicle.TrimLevel))
            parts.Add(vehicle.TrimLevel);

        return string.Join(" ", parts);
    }
}
