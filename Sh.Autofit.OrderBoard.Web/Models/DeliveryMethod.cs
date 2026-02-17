namespace Sh.Autofit.OrderBoard.Web.Models;

public class DeliveryMethod
{
    public int DeliveryMethodId { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsAdHoc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? RulesJson { get; set; }
    public int? AutoHideAfterMinutes { get; set; }
    public TimeSpan? WindowStartTime { get; set; }
    public TimeSpan? WindowEndTime { get; set; }
}
