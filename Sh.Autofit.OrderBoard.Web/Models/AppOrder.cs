namespace Sh.Autofit.OrderBoard.Web.Models;

public class AppOrder
{
    public int AppOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string AccountKey { get; set; } = "";
    public string? AccountName { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public DateTime? DisplayTime { get; set; }
    public string CurrentStage { get; set; } = "ORDER_IN_PC";
    public DateTime StageUpdatedAt { get; set; }
    public bool IsManual { get; set; }
    public string? ManualNote { get; set; }
    public bool Hidden { get; set; }
    public string? HiddenReason { get; set; }
    public DateTime? HiddenAt { get; set; }
    public bool Pinned { get; set; }
    public int? DeliveryMethodId { get; set; }
    public int? DeliveryRunId { get; set; }
    public int? MergedIntoAppOrderId { get; set; }
    public bool NeedsResolve { get; set; }
}
