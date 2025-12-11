namespace Sh.Autofit.New.PartsMappingUI.Models;

public class VirtualPartMigrationCandidate
{
    public int VirtualPartId { get; set; }
    public string VirtualPartNumber { get; set; } = string.Empty;
    public string VirtualPartName { get; set; } = string.Empty;
    public string RealPartNumber { get; set; } = string.Empty;
    public string RealPartName { get; set; } = string.Empty;
    public List<string> MatchedOemNumbers { get; set; } = new();
    public int MappingsToTransfer { get; set; }
}

public class VirtualPartMigrationResult
{
    public bool Success { get; set; }
    public int MappingsTransferred { get; set; }
    public bool VirtualPartDeleted { get; set; }
    public string? ErrorMessage { get; set; }
}
