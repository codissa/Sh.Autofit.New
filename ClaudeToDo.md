# Sh.Autofit - Task List

> **See Architecture.md for system architecture details**

## Current Priority Order
1. Task 5 - Vehicle Registration Caching & Analytics
2. Task 2 - Fix Item Management
3. Task 3 - Fix Car Management
4. Task 1 - Plate Lookup Suggestions
5. Task 6 - Bulk Edit Vehicle Types
6. Task 7 - Enhanced Copy Mapping
7. Task 8 - Item-to-Item Copy Mapping
8. Task 4 - Unified Suggestion Tab (optional)

---
new tasks: 
1. f vehicle registration has cache hit no need to continue to gov api make it faster
2. give in plate lookup the ability to unmap a part and proceed to unmap it to all model names
## Task 5: Vehicle Registration Caching and Analytics
**Priority: 1 (URGENT)**

**Current State:** `VehicleRegistrations` table exists but underutilized

**Goals:**
1. **Cache All Lookups**:
   - Save every Gov API lookup to VehicleRegistrations
   - Reduces API calls (faster, more reliable)
   - Track: PlateNumber, VIN, Manufacturer, Model, Year, LastLookup, MatchedVehicleTypeId

2. **Unmatched Tracking**:
   - Show plates that Gov API returned but couldn't match in our DB
   - Two categories:
     - In Gov API but not in our DB (need to add vehicle model)
     - Not found in Gov API (invalid plates, private vehicles, etc.)
   - UI: DataGrid with unmatched vehicles, reason, date

3. **Analytics Dashboard**:
   - Total lookups
   - Match rate (% found in our DB)
   - Most searched plates
   - Most searched models
   - Growth over time

**Implementation Areas:**
- Service: Enhance `IGovernmentApiService` to always save to cache
- ViewModel: Create `AnalyticsDashboardViewModel`
- View: New `AnalyticsView.xaml` or add tab to existing views
- Database: Ensure VehicleRegistrations has proper indexes

---

## Task 2: Fix Item Management
**Priority: 2 (URGENT)**

**Current Issues:**
- Clicking an item does nothing
- No ability to view which cars are mapped to an item
- No add/delete functionality

**Required Functionality:**
1. **Item Details Panel** (right side or modal):
   - Part number, name, price, stock
   - OEM numbers
   - Metadata (compatibility notes, images, etc.)

2. **Mapped Vehicles DataGrid**:
   - Show all VehicleTypes mapped to this item
   - Columns: Manufacturer, Model, Commercial Name, Year, Engine, Fuel
   - Delete button per row

3. **Add Mapping**:
   - Button to open SelectVehiclesDialog
   - Add selected vehicles to this item's mappings

4. **Suggestion Table** (similar to Task 1):
   - Find vehicles that might fit this part based on similar existing mappings
   - Algorithm: If part X is mapped to vehicle A, suggest vehicles similar to A
   - Allow accepting suggestions as confirmed mappings

**Implementation Areas:**
- Service: Add methods to `IDataService` (LoadVehiclesForPartAsync, etc.)
- ViewModel: Enhance `PartMappingsManagementViewModel`
- View: Redesign `PartMappingsManagementView.xaml` with split layout

---

## Task 3: Fix Car/Vehicle Management
**Priority: 3 (URGENT)**

**Current Issues:**
- Clicking a model does nothing
- No ability to view mapped items
- Missing suggestion features
- Copy mapping limited to individual vehicles

**Required Functionality:**
1. **Model Details Panel**:
   - Full vehicle specs (year, engine, fuel, transmission, etc.)
   - Manufacturer info

2. **Mapped Items DataGrid**:
   - Show all parts mapped to this model
   - Columns: Part number, name, category, OEM, price
   - Delete button per row

3. **Add Mapping**:
   - Button to open SelectPartsDialog
   - Add selected parts to this model's mappings

4. **Suggestion Table**:
   - Find parts mapped to similar vehicles
   - Algorithm: Same commercial name + year/engine overlap
   - Visual distinction + accept/reject buttons

5. **Enhanced Copy Mapping**:
   - Button: "Copy Mappings From Another Vehicle"
   - Dialog improvements:
     - Work at MODEL-LEVEL (not just individual VehicleType)
     - Initially show models with shared commercial name + year/engine overlap
     - Allow adding/removing source and target vehicles in dialog
     - Cancel button to abort
   - Also show suggestions from other commercial names with same specs

**Implementation Areas:**
- Service: Enhance `IDataService.CopyMappingsAsync()` for model-level
- ViewModel: Enhance `ModelMappingsManagementViewModel`
- View: Redesign `ModelMappingsManagementView.xaml`
- Dialog: Enhance `CopyMappingDialog.xaml` with multi-select and filters

---

## Task 1: Enhanced Plate Lookup with Intelligent Suggestions
**Priority: 4**

**Goal:** When looking up a plate, show not just confirmed mappings but also suggestions from similar models

**Algorithm:** For matched vehicle, find parts mapped to other models that share:
- Same commercial name
- Overlapping year range
- Same engine volume
- Same fuel type
- **BUT** different model name

**UI Requirements:**
- Display suggestions with distinct visual indicator (badge, color, icon)
- Add "Suggestion" flag column in mapped parts table
- Provide "Confirm Suggestion" button that permanently adds the mapping
- Clear visual distinction: Confirmed vs Suggested parts

**Implementation Areas:**
- Service: Add `IPartSuggestionService.GetSimilarModelSuggestionsAsync()`
- ViewModel: `PlateLookupViewModel` - add suggestion loading and confirmation
- View: `PlateLookupView.xaml` - update DataGrid with suggestion indicators

---

## Task 6: Bulk Edit Vehicle Types by Model Name
**Priority: 5**

**Goal:** Add ability to edit a group of vehicle types at once when they share the same model name

**Use Case:**
- User selects a model name (e.g., "Camry")
- All vehicle types under that model are shown (Camry 2019 2.5L, Camry 2020 2.5L, etc.)
- User can bulk edit common fields across all selected vehicles

**Editable Fields (Bulk Edit):**
1. **EngineModel** - Engine code (e.g., "2AR-FE", "1NZ-FE")
2. **ModelCode** - External model codes (TecDoc, TecRMI, etc.)
3. **FuelType** - If wrong for entire model line
4. **Transmission** - If consistent across model
5. **VehicleCategory** - Sedan, SUV, Truck, etc.
6. **EmissionGroup** - Euro 5, Euro 6, etc.
7. **SafetyRating** - If available for model
8. **TrimLevel** - Base, Sport, Luxury, etc. (optional per vehicle)

**UI Design:**

Option A - Inline Grid Editor (RECOMMENDED):
```
┌───────────────────────────────────────────────────────────────┐
│ Model Management - Editing: Toyota Camry                      │
├───────────────────────────────────────────────────────────────┤
│ Bulk Edit Fields (Apply to all selected):                     │
│ Engine Model: [2AR-FE____] Model Code: [________]             │
│ Fuel Type: [Gasoline ▼] Transmission: [Automatic ▼]          │
│ [Apply to Selected (5)] [Apply to All (12)]                  │
├───────────────────────────────────────────────────────────────┤
│ ☑ Year  CommercialName  EngineVol  EngineModel  Fuel  [Edit] │
│ ☑ 2019  Camry          2.5L       2AR-FE       Gas   [Edit]  │
│ ☑ 2020  Camry          2.5L       2AR-FE       Gas   [Edit]  │
│ ☐ 2021  Camry          2.5L       (empty)      Gas   [Edit]  │
│ ☑ 2022  Camry          2.5L       2AR-FE       Gas   [Edit]  │
│ ☑ 2023  Camry          2.5L       2AR-FE       Gas   [Edit]  │
│ ☐ 2019  Camry Hybrid   2.5L       A25A-FXS     Hybrid [Edit] │
│ ...                                                            │
├───────────────────────────────────────────────────────────────┤
│ [Select All] [Deselect All] [Save Changes] [Cancel]          │
└───────────────────────────────────────────────────────────────┘
```

**Implementation Areas:**
- Service: `LoadVehiclesByModelNameAsync()`, `BulkUpdateVehicleFieldsAsync()`
- ViewModel: Enhance `ModelMappingsManagementViewModel` or create `BulkEditViewModel`
- View: Add bulk edit panel to `ModelMappingsManagementView.xaml`
- Model: Create `VehicleTypeEditModel` with change tracking

---

## Task 7: Enhanced Copy Mapping (Model-Level)
**Priority: 6**

**Current Limitation:** Copy mapping works at VehicleType level only

**Required Enhancements:**
1. **Model-Level Selection**:
   - Select entire ModelName group as source or target
   - Copies mappings to ALL VehicleTypes under that model

2. **Flexible Dialog**:
   - Initial filter: Shared commercial name with year/engine overlap
   - Ability to add more vehicles to "Copy From" list
   - Ability to add more vehicles to "Copy To" list
   - Cancel button to abort operation

3. **Smart Filtering**:
   - **Priority 1:** Same CommercialName + overlapping years + same EngineVolume
   - **Priority 2:** Different CommercialName but same specs (year, engine, fuel)
   - Show priority indicator in dialog

4. **Preview Before Copy**:
   - Show how many mappings will be copied
   - Show affected vehicles
   - Conflict handling (if target already has different mappings)

**UI Mockup:**
```
┌─────────────────────────────────────────────────────┐
│ Copy Mappings                                        │
├─────────────────────────────────────────────────────┤
│ Copy FROM:  [Toyota Camry 2019 2.5L]  [Add More...] │
│             [Toyota Camry 2020 2.5L]  [Remove]      │
├─────────────────────────────────────────────────────┤
│ Copy TO:    [Toyota Camry 2021 2.5L]  [Add More...] │
│             [Toyota Camry 2022 2.5L]  [Remove]      │
├─────────────────────────────────────────────────────┤
│ Preview: 47 mappings will be copied to 2 vehicles   │
│ [Cancel] [Copy Mappings]                            │
└─────────────────────────────────────────────────────┘
```

**Implementation Areas:**
- Dialog: Enhance `CopyMappingDialog.xaml` and ViewModel
- Service: Update `IDataService.CopyMappingsAsync()` to accept model groups
- Add filtering logic for suggested vehicles

---

## Task 8: Item-to-Item Copy Mapping
**Priority: 7**

**Concept:** Copy all vehicle mappings from one part to another part

**Use Case:**
- Similar parts (e.g., different brands of same oil filter)
- Superseded parts (old part number → new part number)
- Universal parts that share fitment

**Functionality:**
1. In Item Management, add button: "Copy Mappings From Another Part"
2. Dialog to search and select source part
3. Show preview of mappings to be copied
4. Option to merge or replace existing mappings
5. Bulk operation with progress indicator

**UI Flow:**
```
Item Management → Select Part → [Copy Mappings] Button
  ↓
Search for source part (by number, name, OEM)
  ↓
Preview: "Part ABC123 is mapped to 45 vehicles. Copy to current part?"
  ↓
[Cancel] [Copy (Merge)] [Copy (Replace)]
```

**Implementation Areas:**
- New Dialog: `CopyPartMappingDialog.xaml` (may already exist, needs enhancement)
- Service: Add `IDataService.CopyMappingsBetweenPartsAsync()`
- ViewModel: Enhance `PartMappingsManagementViewModel`

---

## Task 4: Unified Suggestion-Based Mapping Tab (OPTIONAL)
**Priority: 8 (Consult before implementing)**

**Concept:** Instead of suggestions scattered across different views, create a central "Suggested Mappings" workspace

**Features:**
- **Algorithm Consolidation**: Unify all suggestion logic into one service
- **Grouped Suggestions**: Show item-vehicle pairs with confidence scores
- **Bulk Operations**:
  - Select multiple suggestions
  - Accept all / Reject all
  - Filter by confidence score, manufacturer, category
- **Review Interface**:
  - Side-by-side comparison
  - Explanation of why suggested (matching criteria)
  - Edit before accepting

**UI Layout:**
```
┌─────────────────────────────────────────────────────┐
│ Filters: [Min Confidence] [Manufacturer] [Category] │
├─────────────────────────────────────────────────────┤
│ Suggested Mappings (Grouped)                        │
│ ☐ Part X → Vehicle A, B, C  [Score: 95%] [Accept]  │
│ ☐ Part Y → Vehicle D        [Score: 87%] [Accept]  │
│ ☐ Part Z → Vehicle E, F     [Score: 75%] [Reject]  │
├─────────────────────────────────────────────────────┤
│ [Accept Selected] [Reject Selected] [Review]        │
└─────────────────────────────────────────────────────┘
```

**Question:** Should we implement this as a new tab or keep suggestions distributed?

**Implementation Areas:**
- New View: `SuggestedMappingsView.xaml`
- New ViewModel: `SuggestedMappingsViewModel`
- Enhanced Service: `IPartSuggestionService` with unified algorithms
- Database: Consider `SuggestedMappings` table for caching
