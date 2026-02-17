namespace Sh.Autofit.OrderBoard.Web.Models;

/// <summary>
/// DTO for rows read from SH2013.dbo.Stock (READ-ONLY).
/// </summary>
public class StockRow
{
    public int ID { get; set; }
    public int DocumentID { get; set; }
    public int DocNumber { get; set; }
    public short Status { get; set; }
    public int? Reference { get; set; }
    public string? AccountKey { get; set; }
    public string? AccountName { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ValueDate { get; set; }
    public string? Remarks { get; set; }
}
