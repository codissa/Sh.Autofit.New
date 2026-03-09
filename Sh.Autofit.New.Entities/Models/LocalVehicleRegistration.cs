#nullable disable
using System;

namespace Sh.Autofit.New.Entities.Models;

public partial class LocalVehicleRegistration
{
    public long Id { get; set; }
    public string SourceResource { get; set; }
    public int? GovRecordId { get; set; }

    // Core fields (all/most resources)
    public string MisparRechev { get; set; }
    public int? TozeretCd { get; set; }
    public string TozeretNm { get; set; }
    public string DegemNm { get; set; }
    public int? ShnatYitzur { get; set; }
    public string DegemManoa { get; set; }
    public string SugDelekNm { get; set; }

    // PRIMARY + InactiveWithCode fields
    public int? DegemCd { get; set; }
    public string SugDegem { get; set; }
    public string RamatGimur { get; set; }
    public int? RamatEivzurBetihuty { get; set; }
    public int? KvutzatZihum { get; set; }
    public string MivchanAcharonDt { get; set; }
    public string TokefDt { get; set; }
    public string Baalut { get; set; }
    public string Misgeret { get; set; }
    public int? TzevaCd { get; set; }
    public string TzevaRechev { get; set; }
    public string ZmigKidmi { get; set; }
    public string ZmigAhori { get; set; }
    public int? HoraatRishum { get; set; }
    public string MoedAliyaLakvish { get; set; }
    public string KinuyMishari { get; set; }

    // Off-Road Cancelled + Personal Import shared
    public int? SugRechevCd { get; set; }
    public string SugRechevNm { get; set; }

    // Off-Road Cancelled specific
    public string BitulDt { get; set; }
    public string TozarManoa { get; set; }
    public string MisparManoa { get; set; }

    // Shared across multiple non-primary resources
    public int? MishkalKolel { get; set; }
    public int? NefachManoa { get; set; }
    public string TozeretEretzNm { get; set; }

    // Personal Import specific
    public string Shilda { get; set; }
    public string SugYevu { get; set; }

    // Inactive WITHOUT Model Code + Heavy >3.5t shared
    public string MisparShilda { get; set; }
    public string TkinaEU { get; set; }
    public int? SugDelekCd { get; set; }
    public int? MishkalAzmi { get; set; }
    public string HanaaCd { get; set; }
    public string HanaaNm { get; set; }
    public int? MishkalMitanHarama { get; set; }

    // Heavy >3.5t specific
    public int? MisparMekomotLeyadNahag { get; set; }
    public int? MisparMekomot { get; set; }
    public string KvutzatSugRechev { get; set; }
    public string GriraNm { get; set; }

    public DateTime SyncedAt { get; set; }
}
