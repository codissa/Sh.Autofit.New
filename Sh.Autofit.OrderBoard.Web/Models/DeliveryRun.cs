namespace Sh.Autofit.OrderBoard.Web.Models;

public class DeliveryRun
{
    public int DeliveryRunId { get; set; }
    public int DeliveryMethodId { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public string State { get; set; } = "OPEN";
    public DateTime? ClosedAt { get; set; }
}
