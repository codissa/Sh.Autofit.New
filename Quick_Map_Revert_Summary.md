# Quick Map Revert Summary

**Date:** 2025-11-05
**Status:** ✅ **COMPLETED**

## Changes Made

Reverted the Quick Map functionality in Plate Lookup and Car Management to use the **OLD simple dialog** instead of the new SelectModelsForMappingDialog.

---

## Current Dialog Usage

### ✅ Using OLD Simple Dialogs (QuickMapDialog / SelectPartsDialog)

1. **Plate Lookup → Quick Map Button**
   - **Dialog Used:** `QuickMapDialog` (old)
   - **Behavior:** Simple parts selection and mapping to current model
   - **Location:** `PlateLookupViewModel.QuickMapAsync()` (line 305-338)

2. **Car Management → Add Parts Button**
   - **Dialog Used:** `SelectPartsDialog` (old)
   - **Behavior:** Simple parts selection and mapping to current model
   - **Location:** `ModelMappingsManagementViewModel.AddPartsToModelAsync()` (line 273-318)

### ✅ Using NEW SelectModelsForMappingDialog

3. **Plate Lookup → Accept Suggestion** (from suggestions table)
   - **Dialog Used:** `SelectModelsForMappingDialog` (new)
   - **Behavior:** Select parts + select similar models with scoring
   - **Location:** `PlateLookupViewModel.QuickMapSuggestionAsync()` (line 342+)

4. **Car Management → Accept Suggestion** (from suggestions list)
   - **Dialog Used:** `SelectModelsForMappingDialog` (new)
   - **Behavior:** Select parts + select similar models with scoring
   - **Location:** `ModelMappingsManagementViewModel.AcceptPartSuggestionAsync()` (line 548+)

---

## Implementation Details

### 1. Plate Lookup Quick Map (REVERTED)

**Before (New Dialog):**
```csharp
// Used SelectModelsForMappingDialog with full vehicle matching
var dialog = new Views.SelectModelsForMappingDialog(_contextFactory, MatchedVehicle);
```

**After (Old Dialog):**
```csharp
// Uses simple QuickMapDialog
var dialog = new Views.QuickMapDialog(_dataService, MatchedVehicle.VehicleTypeId);
```

**Files Changed:**
- `PlateLookupViewModel.cs:305-338`

---

### 2. Car Management Add Parts (REVERTED)

**Before (New Dialog):**
```csharp
// Used SelectModelsForMappingDialog with full vehicle matching
var dialog = new Views.SelectModelsForMappingDialog(_contextFactory, representativeVehicle);
```

**After (Old Dialog):**
```csharp
// Uses simple SelectPartsDialog
var allParts = await _dataService.LoadPartsAsync();
var unmappedParts = allParts.Where(p => !MappedParts.Any(mp => mp.PartNumber == p.PartNumber)).ToList();
var dialog = new Views.SelectPartsDialog(unmappedParts);
```

**Files Changed:**
- `ModelMappingsManagementViewModel.cs:273-318`

---

## Important Notes

### Model-Level Mapping Still Enforced ✅

Even though we reverted to the old simple dialogs, **ALL mapping operations still correctly map to ALL vehicles in the model**, not just a single vehicle:

```csharp
// Car Management - Maps to ALL vehicles in model
var vehicleIds = _allVehicles
    .Where(v => v.ManufacturerName.EqualsIgnoringWhitespace(SelectedModelGroup.ManufacturerName) &&
               v.ModelName.EqualsIgnoringWhitespace(SelectedModelGroup.ModelName))
    .Select(v => v.VehicleTypeId)
    .ToList();

await _dataService.MapPartsToVehiclesAsync(vehicleIds, partNumbers, "current_user");
```

The critical difference is:
- **Old dialogs:** Simple part selection → Maps to current model only
- **New dialog:** Part selection + similar models selection → Maps to current model + selected similar models

---

## User Experience Impact

### Quick Map / Add Parts (Simple):
1. User clicks Quick Map or Add Parts
2. Simple dialog shows list of parts with checkboxes
3. User selects parts and clicks OK
4. Parts are mapped to **ALL vehicles in the current model**

### Accept Suggestion (Advanced):
1. User clicks Accept on a suggestion
2. Advanced dialog shows:
   - Parts list (with the suggested part pre-selected)
   - Similar models list (with relevance scores)
3. User can:
   - Add/remove parts from the list
   - Select which similar models to also map to
4. Parts are mapped to **ALL vehicles in current model + ALL vehicles in selected similar models**

---

## Dependencies

Both ViewModels still maintain their dependencies on `IDbContextFactory<ShAutofitContext>` because they use the new SelectModelsForMappingDialog for **accepting suggestions**.

---

## Build Status
✅ **Build Succeeded** - 0 Errors, 18 Warnings (pre-existing)

---

## Summary

**Simple Quick Operations:**
- ✅ Plate Lookup → Quick Map: Uses old QuickMapDialog
- ✅ Car Management → Add Parts: Uses old SelectPartsDialog

**Advanced Suggestion Operations:**
- ✅ Plate Lookup → Accept Suggestion: Uses new SelectModelsForMappingDialog
- ✅ Car Management → Accept Suggestion: Uses new SelectModelsForMappingDialog

**All operations still enforce model-level mapping** to ensure data consistency.
