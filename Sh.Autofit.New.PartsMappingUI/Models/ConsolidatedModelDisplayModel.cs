using Sh.Autofit.New.Entities.Models;
using System.Collections.ObjectModel;

namespace Sh.Autofit.New.PartsMappingUI.Models;

/// <summary>
/// Display model for consolidated vehicle models with coupling information and vehicle types
/// </summary>
public class ConsolidatedModelDisplayModel : ConsolidatedVehicleModel
{
    /// <summary>
    /// Indicates if this consolidated model is coupled with other models
    /// </summary>
    public bool IsCoupled { get; set; }

    /// <summary>
    /// Number of models this model is coupled with
    /// </summary>
    public int CoupledModelsCount { get; set; }

    /// <summary>
    /// List of consolidated models that are coupled with this model
    /// </summary>
    public List<ConsolidatedVehicleModel> CoupledModels { get; set; } = new();

    /// <summary>
    /// Collection of vehicle types belonging to this consolidated model
    /// Used for expandable rows in DataGrid
    /// Hides the base class VehicleTypes collection to provide a display-specific collection
    /// </summary>
    public new ObservableCollection<VehicleDisplayModel> VehicleTypes { get; set; } = new();
}
