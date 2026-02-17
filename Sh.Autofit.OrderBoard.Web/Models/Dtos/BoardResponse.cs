namespace Sh.Autofit.OrderBoard.Web.Models.Dtos;

public class BoardResponse
{
    public List<BoardColumn> Columns { get; set; } = [];
    public List<DeliveryMethodDto> DeliveryMethods { get; set; } = [];
    public DateTime Timestamp { get; set; }
}

public class BoardColumn
{
    public string Stage { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public List<DeliveryGroupDto> Groups { get; set; } = [];
}

public class DeliveryGroupDto
{
    public string Title { get; set; } = "";
    public int? DeliveryMethodId { get; set; }
    public int? DeliveryRunId { get; set; }
    public string? TimeWindow { get; set; }
    public int Count { get; set; }
    public List<OrderCardDto> Orders { get; set; } = [];
}

public class DeliveryMethodDto
{
    public int DeliveryMethodId { get; set; }
    public string Name { get; set; } = "";
    public bool IsAdHoc { get; set; }
    public bool IsActive { get; set; }
    public string? WindowStartTime { get; set; }
    public string? WindowEndTime { get; set; }
    public List<DeliveryRunDto> Runs { get; set; } = [];
}

public class DeliveryRunDto
{
    public int DeliveryRunId { get; set; }
    public string State { get; set; } = "";
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
}
