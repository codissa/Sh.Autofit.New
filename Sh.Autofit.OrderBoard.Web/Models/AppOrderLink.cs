namespace Sh.Autofit.OrderBoard.Web.Models;

public class AppOrderLink
{
    public int LinkId { get; set; }
    public int AppOrderId { get; set; }
    public string SourceDb { get; set; } = "SH2013";
    public int StockId { get; set; }
    public int DocumentId { get; set; }
    public int? DocNumber { get; set; }
    public short? Status { get; set; }
    public int? Reference { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsPresent { get; set; } = true;
    public DateTime? DisappearedAt { get; set; }
    public int MissCount { get; set; }
}
