using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

public partial class CommercialNameGroup : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoaded;

    public string CommercialName { get; set; } = string.Empty;
    public string ManufacturerShortName { get; set; } = string.Empty;
    public int ManufacturerCode { get; set; }
    public int VehicleCount { get; set; }

    public ObservableCollection<ModelGroup> ModelGroups { get; set; } = new();

    // Property to indicate this group has children that can be loaded
    public bool HasChildren => VehicleCount > 0;

    public string DisplayName => string.IsNullOrEmpty(CommercialName)
        ? $"(No Commercial Name) ({VehicleCount})"
        : $"{CommercialName} ({VehicleCount})";

    // Get all loaded vehicles across all model groups
    public IEnumerable<VehicleDisplayModel> GetAllLoadedVehicles()
    {
        return ModelGroups.SelectMany(m => m.Vehicles);
    }

    partial void OnIsExpandedChanged(bool value)
    {
        // This will be used to trigger loading/unloading
        if (value && !IsLoaded && !IsLoading)
        {
            // Signal that loading should happen
            // The ViewModel will handle this
        }
        else if (!value && IsLoaded)
        {
            // Release model groups from memory when collapsed
            ModelGroups.Clear();
            IsLoaded = false;

            // Add placeholder to keep expand arrow visible
            if (HasChildren)
            {
                ModelGroups.Add(new ModelGroup { ModelName = "Loading...", VehicleCount = 0 });
            }
        }
    }
}
