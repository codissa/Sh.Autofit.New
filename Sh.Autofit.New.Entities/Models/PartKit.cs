using System;
using System.Collections.Generic;

namespace Sh.Autofit.New.Entities.Models;

/// <summary>
/// Represents a reusable set of parts (kit) that can be mapped together to vehicles.
/// Examples: "Brake System Kit", "Oil Change Kit", "Annual Maintenance Kit"
/// </summary>
public partial class PartKit
{
    /// <summary>
    /// Primary key for the part kit
    /// </summary>
    public int PartKitId { get; set; }

    /// <summary>
    /// Display name of the kit (e.g., "ערכת בלמים מלאה")
    /// </summary>
    public string KitName { get; set; } = null!;

    /// <summary>
    /// Optional description of what the kit contains and when to use it
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates if the kit is active and available for use
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Username of the person who created this kit
    /// </summary>
    public string CreatedBy { get; set; } = null!;

    /// <summary>
    /// Timestamp when the kit was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Username of the person who last updated this kit
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Timestamp of last update
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property: Collection of parts that belong to this kit
    /// </summary>
    public virtual ICollection<PartKitItem> PartKitItems { get; set; } = new List<PartKitItem>();
}
