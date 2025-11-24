// ModelCoupling.cs - Entity for coupling different vehicle models that share parts
#nullable disable
using System;

namespace Sh.Autofit.New.Entities.Models;

public partial class ModelCoupling
{
    public int ModelCouplingId { get; set; }

    public int ConsolidatedModelId_A { get; set; }

    public int ConsolidatedModelId_B { get; set; }

    public string CouplingType { get; set; }

    public string Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public bool IsActive { get; set; }

    // Navigation Properties
    public virtual ConsolidatedVehicleModel ConsolidatedModelA { get; set; }

    public virtual ConsolidatedVehicleModel ConsolidatedModelB { get; set; }
}
