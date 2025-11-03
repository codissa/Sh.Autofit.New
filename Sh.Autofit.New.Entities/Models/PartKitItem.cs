using System;

namespace Sh.Autofit.New.Entities.Models;

/// <summary>
/// Represents a single part item within a part kit.
/// Links a PartKit to specific parts.
/// </summary>
public partial class PartKitItem
{
    /// <summary>
    /// Primary key for the part kit item
    /// </summary>
    public int PartKitItemId { get; set; }

    /// <summary>
    /// Foreign key to the PartKit table
    /// </summary>
    public int PartKitId { get; set; }

    /// <summary>
    /// Part number (foreign key to parts in the system)
    /// </summary>
    public string PartItemKey { get; set; } = null!;

    /// <summary>
    /// Optional display order for showing parts in a specific sequence
    /// </summary>
    public int? DisplayOrder { get; set; }

    /// <summary>
    /// Optional notes specific to this part in the kit
    /// (e.g., "Replace every 10,000 km", "Check before installation")
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Username of the person who added this part to the kit
    /// </summary>
    public string CreatedBy { get; set; } = null!;

    /// <summary>
    /// Timestamp when this part was added to the kit
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Navigation property: The kit this part belongs to
    /// </summary>
    public virtual PartKit PartKit { get; set; } = null!;
}
