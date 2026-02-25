namespace Sh.Autofit.OrderBoard.Web.Models.Dtos;

public class ArchiveBoardResponse
{
    public DateTime Date { get; set; }
    public List<BoardColumn> Columns { get; set; } = [];
    public List<DeliveryMethodDto> DeliveryMethods { get; set; } = [];
    public ArchiveDaySummary Summary { get; set; } = new();
}

public class ArchiveDaySummary
{
    public int TotalOrders { get; set; }
    public int OrdersPacked { get; set; }
    public int OrdersCreated { get; set; }
    public Dictionary<string, int> ByStage { get; set; } = new();
}

public class OrderTimelineEvent
{
    public long EventId { get; set; }
    public DateTime At { get; set; }
    public string Action { get; set; } = "";
    public string? FromStage { get; set; }
    public string? ToStage { get; set; }
    public string? Actor { get; set; }
}
