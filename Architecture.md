# Sh.Autofit - System Architecture Documentation

> **This file contains the architecture reference. Updated only when architecture actually changes.**

## Technology Stack
- **.NET 8** - Modern cross-platform framework
- **WPF (Windows Presentation Foundation)** - Desktop UI with XAML
- **Entity Framework Core 9.0** - ORM with SQL Server
- **MVVM Pattern** - Using CommunityToolkit.Mvvm
- **Dependency Injection** - Microsoft.Extensions.DependencyInjection

## Project Structure (7 Projects)
```
Sh.Autofit.New/
├── Sh.Autofit.New/                    # Main console app
├── Sh.Autofit.New.Entities/            # Data models & EF Core DbContext
├── Sh.Autofit.New.Interfaces/          # Repository & UoW interfaces
├── Sh.Autofit.New.Dal/                 # Data Access Layer - Repository pattern
├── Sh.Autofit.New.DependencyInjection/ # IoC container configuration
├── Sh.Autofit.New.LoadInitDataApp/     # Data initialization utility
└── Sh.Autofit.New.PartsMappingUI/      # WPF Desktop Application (MAIN UI)
```

## Database Architecture
**Two SQL Server Databases:**
1. **Sh.Autofit** (New) - Vehicle fitment data
   - Manufacturers, VehicleTypes (models with detailed specs)
   - VehicleRegistrations (license plate cache from Gov API)
   - VehiclePartsMappings (active mappings with versioning)
   - VehiclePartsMappingsHistory (audit trail)
   - PartsMetadata (extended part info)
   - PartKit & PartKitItem (predefined part sets)
   - System tables (Users, UserActivityLog, ApiSyncLog, etc.)

2. **SH2013** (Legacy) - Read-only parts database
   - Items table (parts inventory, pricing, stock)
   - ExtraNotes (OEM numbers)

**Critical Cross-Database View:**
- `vw_Parts` - Joins SH2013.Items + Sh.Autofit.PartsMetadata + OEM numbers

## Key Services
- **IDataService** - Primary database operations (vehicles, parts, mappings)
- **IGovernmentApiService** - Israeli Gov API integration for plate lookups
- **IVehicleMatchingService** - Fuzzy matching algorithm (Gov data → DB)
- **IPartSuggestionService** - Intelligent part recommendation scoring
- **IPartKitService** - Kit management operations
- **ISettingsService** - User preferences

## Main UI Views
1. **PlateLookupView** - License plate search (Gov API integration)
2. **MappingView** - Main workspace (hierarchical vehicle tree + parts list)
3. **PartMappingsManagementView** - Part-centric management
4. **ModelMappingsManagementView** - Model-centric management
5. **PartKitsView** - Kit management

## Key Features
- **Hierarchical Vehicle Browsing** - Manufacturer → Commercial Name → Model → Vehicles
- **Lazy Loading** - Load data on-demand for performance
- **Smart Suggestions** - AI-like relevance scoring for parts
- **Bulk Operations** - Map/unmap multiple parts to multiple vehicles
- **Copy Mappings** - Between vehicles or entire part kits
- **Version Control** - Full mapping history with audit trail
- **Gov API Integration** - Israeli vehicle registration lookups
- **Fuzzy Matching** - Intelligent vehicle matching to database

## Architectural Patterns
- Repository Pattern (data access abstraction)
- Unit of Work (transaction management)
- MVVM (UI separation)
- Dependency Injection (loose coupling)
- Factory Pattern (DbContext creation)
- Strategy Pattern (suggestion algorithms)

---

## Implementation Notes for Claude

- **Lazy Loading:** Many views use lazy loading. Be careful not to break this pattern.
- **ObservableCollections:** UI updates rely on INotifyPropertyChanged. Always use ObservableCollection for bound lists.
- **Transaction Safety:** Use Unit of Work pattern for multi-step operations.
- **Gov API Rate Limits:** Be mindful of API calls. Always check cache first.
- **Cross-Database Queries:** Use vw_Parts view, not direct SH2013.Items queries.
- **Mapping Versioning:** Never delete mappings, use versioning (IsCurrentVersion flag).
- **User Tracking:** Always pass current user to mapping operations for audit trail.

## ⚠️ CRITICAL MAPPING RULE - Model-Level Operations

**IMPORTANT:** ALL mapping and unmapping operations MUST apply to ALL vehicles with the same model name, NOT just a single vehicle.

### What This Means:
When mapping or unmapping parts, the system operates at the **Model Level**, not the individual vehicle level. Each model (e.g., "Honda Civic Sport") typically has multiple VehicleType records in the database (one per year, engine configuration, etc.). Changes must apply to ALL of them.

### Implementation Requirements:
1. **Mapping Operations** - When mapping parts:
   ```csharp
   // ❌ WRONG - Don't map to just one vehicle
   vehicleTypeIds.Add(selectedModel.VehicleTypeId);

   // ✅ CORRECT - Get ALL vehicles for the model
   var allModelVehicles = _allVehicles
       .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(model.ManufacturerName) &&
                   v.ModelName.EqualsIgnoringWhitespace(model.ModelName))
       .Select(v => v.VehicleTypeId)
       .ToList();
   ```

2. **Unmapping Operations** - When unmapping parts:
   - In **Plate Lookup**: Prompt user: "Remove from all vehicles in this model?" (Yes/No/Cancel)
   - In **Car Management**: Always remove from all vehicles in the model (after confirmation)

3. **Affected Methods:**
   - `PlateLookupViewModel.QuickMapAsync()` ✅ Fixed
   - `PlateLookupViewModel.QuickMapSuggestionAsync()` ✅ Fixed
   - `PlateLookupViewModel.UnmapPartAsync()` ✅ Already correct
   - `ModelMappingsManagementViewModel.AddPartsToModelAsync()` ✅ Fixed
   - `ModelMappingsManagementViewModel.AcceptPartSuggestionAsync()` ✅ Fixed
   - `ModelMappingsManagementViewModel.RemovePartFromModelAsync()` ✅ Already correct

### Why This Matters:
- **Data Consistency:** All variants of a model should have consistent fitment data
- **User Expectation:** Users expect "Honda Civic 2020" to have same parts as "Honda Civic 2021"
- **Business Logic:** Parts typically fit entire model lines, not individual year/trim combinations

### Code Pattern to Follow:
```csharp
// Always use whitespace-agnostic comparison when grouping by model
var vehicleIds = allVehicles
    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(targetManufacturer) &&
                v.ModelName.EqualsIgnoringWhitespace(targetModel))
    .Select(v => v.VehicleTypeId)
    .ToList();

await _dataService.MapPartsToVehiclesAsync(vehicleIds, partNumbers, currentUser);
```
