using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VehiclePartsMapping.Core.Entities
{
    // =============================================
    // MANUFACTURER ENTITY
    // =============================================
    
    [Table("Manufacturers", Schema = "dbo")]
    public class Manufacturer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ManufacturerId { get; set; }
        
        [Required]
        public int ManufacturerCode { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string ManufacturerName { get; set; }
        
        [MaxLength(100)]
        public string? CountryOfOrigin { get; set; }
        
        [Required]
        public bool IsActive { get; set; } = true;
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastSyncedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<VehicleType> VehicleTypes { get; set; } = new List<VehicleType>();
    }
    
    // =============================================
    // VEHICLE TYPE ENTITY
    // =============================================
    
    [Table("VehicleTypes", Schema = "dbo")]
    public class VehicleType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VehicleTypeId { get; set; }
        
        [Required]
        public int ManufacturerId { get; set; }
        
        [Required]
        public int ModelCode { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string ModelName { get; set; }
        
        [MaxLength(100)]
        public string? CommercialName { get; set; }
        
        [Required]
        public int YearFrom { get; set; }
        
        public int? YearTo { get; set; }
        
        // Technical specifications
        public int? EngineVolume { get; set; }
        public int? TotalWeight { get; set; }
        
        [MaxLength(100)]
        public string? EngineModel { get; set; }
        
        public int? FuelTypeCode { get; set; }
        
        [MaxLength(50)]
        public string? FuelTypeName { get; set; }
        
        [MaxLength(50)]
        public string? TransmissionType { get; set; }
        
        public int? NumberOfDoors { get; set; }
        public int? NumberOfSeats { get; set; }
        public int? Horsepower { get; set; }
        
        // Classification
        [MaxLength(100)]
        public string? TrimLevel { get; set; }
        
        [MaxLength(100)]
        public string? VehicleCategory { get; set; }
        
        public int? EmissionGroup { get; set; }
        public int? GreenIndex { get; set; }
        
        [Column(TypeName = "decimal(3,2)")]
        public decimal? SafetyRating { get; set; }
        
        public int? SafetyLevel { get; set; }
        
        // Tire specifications
        [MaxLength(50)]
        public string? FrontTireSize { get; set; }
        
        [MaxLength(50)]
        public string? RearTireSize { get; set; }
        
        // JSON field for additional data
        public string? AdditionalSpecs { get; set; }
        
        // Metadata
        [Required]
        public bool IsActive { get; set; } = true;
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastSyncedAt { get; set; }
        
        // Navigation properties
        [ForeignKey(nameof(ManufacturerId))]
        public virtual Manufacturer Manufacturer { get; set; }
        
        public virtual ICollection<VehiclePartsMapping> PartMappings { get; set; } = new List<VehiclePartsMapping>();
    }
    
    // =============================================
    // PARTS METADATA ENTITY
    // Extension of SH2013.dbo.Items
    // =============================================
    
    [Table("PartsMetadata", Schema = "dbo")]
    public class PartsMetadata
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PartMetadataId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string ItemKey { get; set; } // FIXED: Now ItemKey to match SH2013.Items.ItemKey
        
        // Additional fields
        [MaxLength(500)]
        public string? CompatibilityNotes { get; set; }
        
        [Required]
        public bool UniversalPart { get; set; } = false;
        
        [MaxLength(500)]
        public string? ImageUrl { get; set; }
        
        [MaxLength(500)]
        public string? DatasheetUrl { get; set; }
        
        // Override fields
        [MaxLength(1000)]
        public string? CustomDescription { get; set; }
        
        [Required]
        public bool UseCustomDescription { get; set; } = false;
        
        // Metadata
        [Required]
        public bool IsActive { get; set; } = true;
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [MaxLength(100)]
        public string? CreatedBy { get; set; }
        
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
    }
    
    // =============================================
    // PART DTO (Read-only view data)
    // Represents data from vw_Parts view
    // =============================================
    
    public class PartDto
    {
        public string PartNumber { get; set; } // ItemKey from SH2013.Items
        public string? PartName { get; set; }   // ItemName from SH2013.Items
        public decimal? RetailPrice { get; set; }
        public decimal? CostPrice { get; set; }
        public int? StockQuantity { get; set; }
        
        // OEM Numbers from ExtraNotes
        public string? OEMNumber1 { get; set; }
        public string? OEMNumber2 { get; set; }
        public string? OEMNumber3 { get; set; }
        public string? OEMNumber4 { get; set; }
        public string? OEMNumber5 { get; set; }
        
        // Additional info from ExtraNotes
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? Category { get; set; }
        public string? BoxDimensions { get; set; }
        
        // Metadata from PartsMetadata table
        public string? CompatibilityNotes { get; set; }
        public bool UniversalPart { get; set; }
        public string? ImageUrl { get; set; }
        public string? DatasheetUrl { get; set; }
        public string? CustomDescription { get; set; }
        public bool UseCustomDescription { get; set; }
        
        // Status
        public bool IsInStock { get; set; }
        public bool IsActive { get; set; }
        
        public DateTime? MetadataUpdatedAt { get; set; }
        
        // Helper property to get primary OEM
        public string? PrimaryOEM => OEMNumber1 ?? OEMNumber2 ?? OEMNumber3 ?? OEMNumber4 ?? OEMNumber5;
        
        // Display name (prefer custom description if available)
        public string DisplayName => UseCustomDescription && !string.IsNullOrWhiteSpace(CustomDescription) 
            ? CustomDescription 
            : PartName ?? PartNumber;
    }
    
    // =============================================
    // VEHICLE PARTS MAPPING ENTITY
    // =============================================
    
    [Table("VehiclePartsMappings", Schema = "dbo")]
    public class VehiclePartsMapping
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MappingId { get; set; }
        
        [Required]
        public int VehicleTypeId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string PartItemKey { get; set; } // FIXED: Now PartItemKey to reference SH2013.Items.ItemKey
        
        // Mapping metadata
        [Required]
        [MaxLength(50)]
        public string MappingSource { get; set; } = "Manual";
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? ConfidenceScore { get; set; }
        
        [Required]
        public int Priority { get; set; } = 0;
        
        // Compatibility details
        public int? FitsYearFrom { get; set; }
        public int? FitsYearTo { get; set; }
        
        [Required]
        public bool RequiresModification { get; set; } = false;
        
        [MaxLength(500)]
        public string? CompatibilityNotes { get; set; }
        
        [MaxLength(1000)]
        public string? InstallationNotes { get; set; }
        
        // Version tracking
        [Required]
        public int VersionNumber { get; set; } = 1;
        
        [Required]
        public bool IsCurrentVersion { get; set; } = true;
        
        // Audit fields
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; }
        
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
        
        // Soft delete
        [Required]
        public bool IsActive { get; set; } = true;
        
        public DateTime? DeactivatedAt { get; set; }
        
        [MaxLength(100)]
        public string? DeactivatedBy { get; set; }
        
        [MaxLength(500)]
        public string? DeactivationReason { get; set; }
        
        // Navigation properties
        [ForeignKey(nameof(VehicleTypeId))]
        public virtual VehicleType VehicleType { get; set; }
        
        public virtual ICollection<VehiclePartsMappingsHistory> History { get; set; } = new List<VehiclePartsMappingsHistory>();
    }
    
    // =============================================
    // MAPPING HISTORY ENTITY
    // =============================================
    
    [Table("VehiclePartsMappingsHistory", Schema = "dbo")]
    public class VehiclePartsMappingsHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MappingHistoryId { get; set; }
        
        [Required]
        public int MappingId { get; set; }
        
        [Required]
        public int VehicleTypeId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string PartItemKey { get; set; } // FIXED
        
        // Snapshot of mapping data
        [Required]
        [MaxLength(50)]
        public string MappingSource { get; set; }
        
        [Column(TypeName = "decimal(5,2)")]
        public decimal? ConfidenceScore { get; set; }
        
        [Required]
        public int Priority { get; set; }
        
        public int? FitsYearFrom { get; set; }
        public int? FitsYearTo { get; set; }
        
        [Required]
        public bool RequiresModification { get; set; }
        
        [MaxLength(500)]
        public string? CompatibilityNotes { get; set; }
        
        [MaxLength(1000)]
        public string? InstallationNotes { get; set; }
        
        // Version info
        [Required]
        public int VersionNumber { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string ChangeType { get; set; }
        
        [MaxLength(500)]
        public string? ChangeReason { get; set; }
        
        // Audit
        [Required]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        
        [Required]
        [MaxLength(100)]
        public string ChangedBy { get; set; }
        
        // Navigation
        [ForeignKey(nameof(MappingId))]
        public virtual VehiclePartsMapping Mapping { get; set; }
    }
    
    // =============================================
    // USER ENTITY (SIMPLIFIED - NO PASSWORD)
    // =============================================
    
    [Table("Users", Schema = "dbo")]
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Username { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; }
        
        [MaxLength(200)]
        public string? Email { get; set; }
        
        [Required]
        public bool IsActive { get; set; } = true;
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastLoginAt { get; set; }
        
        // Navigation
        public virtual ICollection<UserActivityLog> Activities { get; set; } = new List<UserActivityLog>();
    }
    
    // =============================================
    // USER ACTIVITY LOG ENTITY
    // =============================================
    
    [Table("UserActivityLog", Schema = "dbo")]
    public class UserActivityLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ActivityId { get; set; }
        
        public int? UserId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Username { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string ActivityType { get; set; }
        
        [MaxLength(50)]
        public string? EntityType { get; set; }
        
        public int? EntityId { get; set; }
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [MaxLength(50)]
        public string? IpAddress { get; set; }
        
        [MaxLength(500)]
        public string? UserAgent { get; set; }
        
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation
        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }
    }
    
    // =============================================
    // SYSTEM SETTINGS ENTITY
    // =============================================
    
    [Table("SystemSettings", Schema = "dbo")]
    public class SystemSetting
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SettingId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string SettingKey { get; set; }
        
        public string? SettingValue { get; set; }
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [Required]
        public bool IsEditable { get; set; } = true;
        
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
    }
    
    // =============================================
    // VEHICLE REGISTRATION ENTITY
    // =============================================
    
    [Table("VehicleRegistrations", Schema = "dbo")]
    public class VehicleRegistration
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RegistrationId { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string LicensePlate { get; set; }
        
        public int? VehicleTypeId { get; set; }
        public int? ManufacturerId { get; set; }
        
        // Data from gov API
        public int? RegistrationYear { get; set; }
        
        [MaxLength(50)]
        public string? Color { get; set; }
        
        [MaxLength(200)]
        public string? CurrentOwner { get; set; }
        
        [MaxLength(50)]
        public string? VIN { get; set; }
        
        // Metadata
        [Required]
        public DateTime FirstLookupDate { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime LastLookupDate { get; set; } = DateTime.UtcNow;
        
        [Required]
        public int LookupCount { get; set; } = 1;
        
        [Required]
        public bool IsActive { get; set; } = true;
        
        // Navigation
        [ForeignKey(nameof(VehicleTypeId))]
        public virtual VehicleType? VehicleType { get; set; }
        
        [ForeignKey(nameof(ManufacturerId))]
        public virtual Manufacturer? Manufacturer { get; set; }
    }
    
    // =============================================
    // API SYNC LOG ENTITY
    // =============================================
    
    [Table("ApiSyncLog", Schema = "dbo")]
    public class ApiSyncLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SyncLogId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ApiName { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string SyncType { get; set; }
        
        public int? RecordsFetched { get; set; }
        public int? RecordsInserted { get; set; }
        public int? RecordsUpdated { get; set; }
        
        [Required]
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? CompletedAt { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; }
        
        public string? ErrorMessage { get; set; }
    }
}
