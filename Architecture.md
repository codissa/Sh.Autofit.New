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
