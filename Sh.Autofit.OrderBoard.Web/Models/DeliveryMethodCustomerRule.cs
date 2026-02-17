namespace Sh.Autofit.OrderBoard.Web.Models;

public class DeliveryMethodCustomerRule
{
    public int Id { get; set; }
    public string AccountKey { get; set; } = "";
    public int DeliveryMethodId { get; set; }
    public TimeSpan? WindowStart { get; set; }
    public TimeSpan? WindowEnd { get; set; }
    public string? DaysOfWeek { get; set; }
    public bool IsActive { get; set; } = true;
}
