namespace Sh.Autofit.OrderBoard.Web.Models;

public class StageEvent
{
    public long EventId { get; set; }
    public int? AppOrderId { get; set; }
    public DateTime At { get; set; }
    public string Actor { get; set; } = "system";
    public string Action { get; set; } = "";
    public string? FromStage { get; set; }
    public string? ToStage { get; set; }
    public string? Payload { get; set; }
}
