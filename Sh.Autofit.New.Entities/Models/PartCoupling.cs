// PartCoupling.cs - Entity for coupling different parts that inherit each other's mappings
#nullable disable
using System;

namespace Sh.Autofit.New.Entities.Models;

public partial class PartCoupling
{
    public int PartCouplingId { get; set; }

    public string PartItemKey_A { get; set; }

    public string PartItemKey_B { get; set; }

    public string CouplingType { get; set; }

    public string Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public bool IsActive { get; set; }
}
