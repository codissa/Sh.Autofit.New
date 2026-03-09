#nullable disable
using System;

namespace Sh.Autofit.New.Entities.Models;

public partial class DataSyncLog
{
    public int Id { get; set; }
    public string DatasetName { get; set; }
    public string ResourceId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalApiRecords { get; set; }
    public int RecordsDownloaded { get; set; }
    public int LocalRecordCount { get; set; }
    public string Status { get; set; }
    public string ErrorMessage { get; set; }
}
