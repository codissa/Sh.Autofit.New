using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class PartKitItemDisplayModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _vehicleCount;

    [ObservableProperty]
    private ObservableCollection<VehicleDisplayModel> _mappedVehicles = new();

    public int PartKitItemId { get; set; }
    public int PartKitId { get; set; }
    public string PartItemKey { get; set; } = string.Empty;
    public int? DisplayOrder { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Additional part info for display
    public string? PartName { get; set; }
    public string? Category { get; set; }
    public bool? IsInStock { get; set; }

    public string DisplayName => !string.IsNullOrEmpty(PartName)
        ? $"{PartItemKey} - {PartName}"
        : PartItemKey;

    public string StockIcon => IsInStock == true ? "✅" : IsInStock == false ? "❌" : "";

    public string VehicleCountDisplay => $"{VehicleCount} רכבים";
}
