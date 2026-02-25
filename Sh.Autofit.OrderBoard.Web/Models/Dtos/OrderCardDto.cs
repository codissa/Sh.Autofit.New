namespace Sh.Autofit.OrderBoard.Web.Models.Dtos;

public class OrderCardDto
{
    public int AppOrderId { get; set; }
    public string AccountKey { get; set; } = "";
    public string? AccountName { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public DateTime? DisplayTime { get; set; }
    public string CurrentStage { get; set; } = "";
    public bool Pinned { get; set; }
    public bool Hidden { get; set; }
    public bool IsManual { get; set; }
    public string? ManualNote { get; set; }
    public int? DeliveryMethodId { get; set; }
    public string? DeliveryMethodName { get; set; }
    public int? DeliveryRunId { get; set; }
    public bool NeedsResolve { get; set; }

    // Time tracking
    public DateTime CreatedAt { get; set; }
    public DateTime StageUpdatedAt { get; set; }

    // Closest delivery rule for this account
    public string? ClosestRuleName { get; set; }
    public string? ClosestRuleWindow { get; set; }

    // Yellow highlight: account has orders in PACKED + another stage
    public bool HasPackedSibling { get; set; }

    // Stacking: if multiple orders with same AccountKey+Address in same stage
    public int StackCount { get; set; } = 1;
    public List<int>? StackedOrderIds { get; set; }

    // Archive-only fields (null on live board)
    public DateTime? PackedAt { get; set; }
    public string? PackedDuration { get; set; }
}
