using CommunityToolkit.Mvvm.ComponentModel;

namespace Sh.Autofit.New.PartsMappingUI.Models
{
    public partial class VehicleModelDisplayModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public int VehicleTypeId { get; set; }
        public string ManufacturerName { get; set; } = string.Empty;
        public string ManufacturerShortName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string CommercialName { get; set; } = string.Empty;
        public int YearFrom { get; set; }
        public int YearTo { get; set; }
        public int? EngineVolume { get; set; }
        public int Score { get; set; }

        public string DisplayName => $"{ManufacturerShortName} {ModelName}";

        public string YearRange => YearFrom == YearTo ? $"{YearFrom}" : $"{YearFrom}-{YearTo}";

        public string EngineVolumeDisplay => EngineVolume.HasValue ? $"{EngineVolume.Value} סמ\"ק" : "";

        public string CommercialNameDisplay => !string.IsNullOrEmpty(CommercialName) ? CommercialName : "לא צוין";
    }
}
