#nullable disable
using System;

namespace Sh.Autofit.New.Entities.Models;

public partial class LocalVehicleQuantity
{
    public int Id { get; set; }
    public int? GovRecordId { get; set; }
    public string SugDegem { get; set; }
    public int TozeretCd { get; set; }
    public string TozeretNm { get; set; }
    public string TozeretEretzNm { get; set; }
    public string Tozar { get; set; }
    public int DegemCd { get; set; }
    public string DegemNm { get; set; }
    public int? ShnatYitzur { get; set; }
    public int MisparRechavimPailim { get; set; }
    public int MisparRechavimLePailim { get; set; }
    public string KinuyMishari { get; set; }
    public DateTime SyncedAt { get; set; }
}
