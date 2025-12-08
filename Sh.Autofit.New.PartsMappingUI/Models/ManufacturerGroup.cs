using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class ManufacturerGroup : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded = false;

    public string ManufacturerShortName { get; set; } = string.Empty;
    public string ManufacturerName { get; set; } = string.Empty;
    public int ManufacturerCode { get; set; }

    public ObservableCollection<CommercialNameGroup> CommercialNameGroups { get; set; } = new();

    public int TotalVehicleCount => CommercialNameGroups.Sum(g => g.VehicleCount);

    public string DisplayName => $"{ManufacturerShortName ?? ManufacturerName} ({TotalVehicleCount})";

    // Get all loaded vehicles across all commercial name groups
    public IEnumerable<VehicleDisplayModel> GetAllLoadedVehicles()
    {
        return CommercialNameGroups.SelectMany(g => g.GetAllLoadedVehicles());
    }
}
