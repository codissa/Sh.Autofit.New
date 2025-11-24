// ConsolidatedVehicleModel.cs - Entity for consolidated vehicle models with year ranges
#nullable disable
using System;
using System.Collections.Generic;

namespace Sh.Autofit.New.Entities.Models;

public partial class ConsolidatedVehicleModel
{
    public int ConsolidatedModelId { get; set; }

    public int ManufacturerId { get; set; }

    public int ManufacturerCode { get; set; }

    public int ModelCode { get; set; }

    public string ModelName { get; set; }

    public int? EngineVolume { get; set; }

    public string TrimLevel { get; set; }

    public string FinishLevel { get; set; }

    public string TransmissionType { get; set; }

    public int? FuelTypeCode { get; set; }

    public string FuelTypeName { get; set; }

    public int? NumberOfDoors { get; set; }

    public int? Horsepower { get; set; }

    public int YearFrom { get; set; }

    public int? YearTo { get; set; }

    public string CommercialName { get; set; }

    public string EngineModel { get; set; }

    public string VehicleCategory { get; set; }

    public int? EmissionGroup { get; set; }

    public int? GreenIndex { get; set; }

    public decimal? SafetyRating { get; set; }

    public int? SafetyLevel { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string CreatedBy { get; set; }

    public string UpdatedBy { get; set; }

    // Navigation Properties
    public virtual Manufacturer Manufacturer { get; set; }

    public virtual ICollection<VehiclePartsMapping> VehiclePartsMappings { get; set; } = new List<VehiclePartsMapping>();

    public virtual ICollection<VehicleType> VehicleTypes { get; set; } = new List<VehicleType>();

    // Model Couplings (this model as Model A)
    public virtual ICollection<ModelCoupling> ModelCouplingsAsModelA { get; set; } = new List<ModelCoupling>();

    // Model Couplings (this model as Model B)
    public virtual ICollection<ModelCoupling> ModelCouplingsAsModelB { get; set; } = new List<ModelCoupling>();
}
