namespace Sh.Autofit.New.PartsMappingUI.Models;

public class VehicleProcessingResult
{
    public int VehiclesProcessed { get; set; }
    public int VehicleTypesCreated { get; set; }
    public int ConsolidatedModelsCreated { get; set; }
    public int CouplingsCreated { get; set; }
    public int ManufacturersCreated { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success { get; set; }
    public DateTime ProcessingStartedAt { get; set; }
    public DateTime ProcessingCompletedAt { get; set; }
}

public class CouplingResult
{
    public int TotalCouplingsCreated { get; set; }
    public List<(int modelIdA, int modelIdB, string reason)> CouplingDetails { get; set; } = new();
}
