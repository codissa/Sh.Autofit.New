namespace Sh.Autofit.OrderBoard.Web.Models.Dtos;

public class CreateManualOrderRequest
{
    public string AccountKey { get; set; } = "";
    public string? AccountName { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public DateTime? DisplayTime { get; set; }
    public string? Note { get; set; }
}

public class MoveOrderRequest
{
    public string ToStage { get; set; } = "";
}

public class HideOrderRequest
{
    public string? Reason { get; set; }
}

public class BulkHideRequest
{
    public string Stage { get; set; } = "";
}

public class AssignDeliveryRequest
{
    public int? DeliveryMethodId { get; set; }
    public int? DeliveryRunId { get; set; }
}

public class CreateDeliveryMethodRequest
{
    public string Name { get; set; } = "";
    public bool IsAdHoc { get; set; }
    public string? RulesJson { get; set; }
    public int? AutoHideAfterMinutes { get; set; }
    public string? WindowStartTime { get; set; }
    public string? WindowEndTime { get; set; }
}

public class UpdateDeliveryMethodRequest
{
    public string Name { get; set; } = "";
    public bool IsAdHoc { get; set; }
    public string? WindowStartTime { get; set; }
    public string? WindowEndTime { get; set; }
    public int? AutoHideAfterMinutes { get; set; }
}

public class OpenDeliveryRunRequest
{
    public int DeliveryMethodId { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
}

public class CreateCustomerRuleRequest
{
    public string AccountKey { get; set; } = "";
    public int DeliveryMethodId { get; set; }
    public string? WindowStart { get; set; }
    public string? WindowEnd { get; set; }
    public string? DaysOfWeek { get; set; }
}

public class UpdateCustomerRuleRequest
{
    public int DeliveryMethodId { get; set; }
    public string? WindowStart { get; set; }
    public string? WindowEnd { get; set; }
    public string? DaysOfWeek { get; set; }
    public bool IsActive { get; set; }
}

public class AccountSearchResult
{
    public string AccountKey { get; set; } = "";
    public string? FullName { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
}
