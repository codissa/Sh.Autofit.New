# Smart Suggestions Tab - Design Document

**Purpose:** Automatically suggest parts to be mapped to similar models based on existing mappings, making bulk mapping faster and more accurate.

---

## 🎯 Core Concept

**The Logic:**
1. Find parts that are mapped to Model A (e.g., "Honda Civic Sport")
2. Find similar Model B (e.g., "Honda Civic LX") that:
   - Has same engine volume
   - Has overlapping years
   - Does NOT already have that part mapped
3. Suggest: "Part X is mapped to Model A, should we also map it to similar Model B?"
4. When accepted: Map to **ALL vehicles** with Model B's name
5. **Bonus points for:**
   - Same commercial name
   - Similar model name patterns
   - **OEM number similarity** (shared prefixes, cross-references, superseded parts) 

---

## 🎨 UI Design

### **Dashboard/Card View**
**Best for:** Visual clarity, easy scanning, quick decisions

```
┌─────────────────────────────────────────────────────────────────┐
│ Smart Suggestions                    [🔍 Filter] [⚙️ Settings]  │
├─────────────────────────────────────────────────────────────────┤
│ 📊 Statistics                                                    │
│ ├─ Total Suggestions: 247                                       │
│ ├─ High Confidence (90+): 45                                    │
│ ├─ Medium Confidence (70-89): 102                               │
│ └─ Potential Vehicles: 1,245                                    │
│                                                                  │
│ Filters: [Manufacturer ▼] [Category ▼] [Min Score: 70]         │
│ [✓ Show Only High Confidence] [✓ Group by Part]                │
│                                                                  │
│ Bulk Actions: [Accept Top 10] [Accept All High Confidence]     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│ ┌─────────────────────────────────────────────────────────┐    │
│ │ 🟢 Score: 95 | Confidence: High                         │    │
│ │                                                          │    │
│ │ Part: 12345-ABC - Front Brake Pad Set                   │    │
│ │                                                          │    │
│ │ From: Honda Civic Sport (2020-2023, 2000cc)             │    │
│ │ ├─ Currently mapped to: 45 vehicles                     │    │
│ │ └─ Used by: 3 other models                              │    │
│ │                                                          │    │
│ │ Suggested To: 3 similar models (67 vehicles)            │    │
│ │ ├─ ✓ Honda Civic LX (2020-2023, 2000cc) - 28 vehicles  │    │
│ │ ├─ ✓ Honda Civic EX (2021-2023, 2000cc) - 22 vehicles  │    │
│ │ └─ ✓ Honda Civic Touring (2020-2022, 2000cc) - 17 veh. │    │
│ │                                                          │    │
│ │ Reason: Same engine, overlapping years, same comm name  │    │
│ │                                                          │    │
│ │        [✓ Accept All] [Edit Models] [✗ Reject]         │    │
│ └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│ ┌─────────────────────────────────────────────────────────┐    │
│ │ 🟡 Score: 85 | Confidence: Medium                       │    │
│ │                                                          │    │
│ │ Part: 54321-XYZ - Air Filter                            │    │
│ │                                                          │    │
│ │ From: Toyota Camry LE (2018-2021, 2500cc)               │    │
│ │ Suggested To: 2 similar models (34 vehicles)            │    │
│ │                                                          │    │
│ │        [✓ Accept All] [Edit Models] [✗ Reject]         │    │
│ └─────────────────────────────────────────────────────────┘    │
│                                                                  │
│ [Load More...]                          Page 1 of 25            │
└─────────────────────────────────────────────────────────────────┘
```

**Pros:**
- Very visual and easy to scan
- All info at a glance
- Clear action buttons per suggestion
- Color-coded scores (🟢 Green = 90+, 🟡 Yellow = 70-89)


---

## 🔢 OEM Number Patterns Research

### **Common OEM Number Formats by Manufacturer**

#### **Honda/Acura**
- **Format:** `12345-ABC-000` or `12345-ABC`
- **Pattern:** Part Family (5 digits) - Application Code (3 letters) - Revision (3 digits)
- **Example:** `45022-SDA-A00` (Brake Pad Set)
- **Matching Logic:**
  - **Exact Match:** Same full OEM = High confidence (+25 points)
  - **Prefix Match (5 digits):** Same part family (+15 points)
  - **Prefix Match (8 chars):** Same part family + application (+20 points)

#### **Toyota/Lexus**
- **Format:** `90210-12345` or `90210-ABC` or `04465-12345`
- **Pattern:** Part Group (5 digits) - Part Number (5 digits/3 letters)
- **Example:** `04465-02270` (Brake Pad Set)
- **Matching Logic:**
  - **Exact Match:** Same full OEM = High confidence (+25 points)
  - **Prefix Match (5 digits):** Same part group (+15 points)

#### **Nissan/Infiniti**
- **Format:** `41060-1AA0A` or `D4060-1AA0A`
- **Pattern:** Part Group (5 digits/letters) - Application/Version (5 chars)
- **Example:** `41060-EA025` (Brake Pad Set)
- **Matching Logic:**
  - **Exact Match:** Same full OEM = High confidence (+25 points)
  - **Prefix Match (5 chars):** Same part group (+15 points)

#### **Volkswagen/Audi/Skoda/SEAT**
- **Format:** `1K0-615-301-AA` or `1K0615301AA`
- **Pattern:** Platform Code (3 chars) - Part Group (3 digits) - Part Number (3 digits) - Revision (2 letters)
- **Example:** `1K0615301AA` (Brake Disc)
- **Matching Logic:**
  - **Exact Match:** Same full OEM = High confidence (+25 points)
  - **Prefix Match (6 chars):** Same platform + part group (+20 points)
  - **Platform Match (3 chars):** Same platform family (+10 points)

#### **BMW/Mini**
- **Format:** `34 11 6 777 772` or `34116777772`
- **Pattern:** Part Group (2 digits) - Subgroup (2 digits) - Sequence (1 digit) - Part Number (6 digits)
- **Example:** `34116777772` (Brake Pad Set)
- **Matching Logic:**
  - **Exact Match:** Same full OEM = High confidence (+25 points)
  - **Prefix Match (4 digits):** Same part group + subgroup (+20 points)
  - **Prefix Match (2 digits):** Same part group (+15 points)

#### **Mercedes-Benz**
- **Format:** `A000-420-12-20` or `A0004201220`
- **Pattern:** Series Code (1 letter) - Part Group (3 digits) - Part Number (3 digits) - Revision (2 digits) - Variant (2 digits)
- **Example:** `A0004201220` (Brake Pad Set)
- **Matching Logic:**
  - **Exact Match:** Same full OEM = High confidence (+25 points)
  - **Prefix Match (7 chars):** Same part group + number (+20 points)

#### **Ford/Lincoln**
- **Format:** `F1DZ-2001-A` or `9L3Z-2C026-A`
- **Pattern:** Model Code (4 chars) - Part Number (4 digits) - Revision (1 letter)
- **Example:** `F1DZ2001A` (Brake Caliper)
- **Matching Logic:**
  - **Exact Match:** Same full OEM = High confidence (+25 points)
  - **Suffix Match (5 chars):** Same part number + revision (+20 points)

#### **General Motors (Chevrolet/GMC/Cadillac)**
- **Format:** `12345678` or `84012345`
- **Pattern:** 8-digit numeric
- **Example:** `84406322` (Brake Rotor)
- **Matching Logic:**
  - **Exact Match:** Same full OEM = High confidence (+25 points)
  - **Prefix Match (4 digits):** Possible part family (+10 points)

#### **Hyundai/Kia**
- **Format:** `58302-1GA00` or `583021GA00`
- **Pattern:** Part Group (5 digits) - Application/Revision (5 chars)
- **Example:** `583021GA00` (Brake Pad Set)
- **Matching Logic:**
  - **Exact Match:** Same full OEM = High confidence (+25 points)
  - **Prefix Match (5 digits):** Same part group (+15 points)

---

### **Universal OEM Matching Strategies**

1. **Exact OEM Match (Any Field)**
   - If any of the 5 OEM fields match exactly → +25 points
   - Indicates cross-compatibility or superseded part

2. **Prefix Matching (Part Family)**
   - Manufacturer-specific prefix lengths (see above)
   - Indicates same part family (e.g., all brake pads share prefix)

3. **Superseded Parts**
   - Check if OEM numbers are in supersession chain
   - Old part number replaced by new one → +25 points

4. **Cross-Reference**
   - Different OEM numbers for same aftermarket part
   - Example: Multiple manufacturers using same Bosch brake pad

5. **Fuzzy Matching**
   - Remove dashes, spaces, normalize to uppercase
   - Compare normalized strings
   - Levenshtein distance < 3 → +10 points (typo tolerance)

---

## 🧮 Suggested Algorithm (Updated with OEM Similarity)

### **Step 1: Find Mapped Source Models**
```sql
-- Get all models that have at least one part mapped
SELECT DISTINCT
    VehicleTypeId,
    ManufacturerName,
    ModelName,
    CommercialName,
    YearFrom,
    YearTo,
    EngineVolume
FROM VehicleTypes
WHERE VehicleTypeId IN (SELECT DISTINCT VehicleTypeId FROM VehiclePartsMappings WHERE IsActive = 1)
```

### **Step 2: For Each Source Model, Find Similar Target Models**
```csharp
// Find similar models that DON'T have the same parts
var similarModels = allModels
    .Where(target =>
        // Different model name
        !target.ModelName.EqualsIgnoringWhitespace(source.ModelName) &&

        // Same manufacturer (optional, could be cross-manufacturer)
        target.ManufacturerName.EqualsIgnoringWhitespace(source.ManufacturerName) &&

        // Same engine volume (REQUIRED)
        target.EngineVolume == source.EngineVolume &&

        // Overlapping years (REQUIRED)
        (target.YearFrom <= source.YearTo && target.YearFrom >= source.YearFrom) ||
        (target.YearFrom >= source.YearFrom && target.YearFrom <= source.YearTo)
    );
```

### **Step 3: Score Each Suggestion**
```csharp
public int CalculateSuggestionScore(Model source, Model target, Part part)
{
    int score = 0;

    // Base similarity (REQUIRED to even suggest)
    if (target.EngineVolume == source.EngineVolume) score += 40;
    if (HasYearOverlap(target, source)) score += 30;

    // Bonus factors
    if (target.CommercialName.EqualsIgnoringWhitespace(source.CommercialName))
        score += 20;

    if (target.ManufacturerName == source.ManufacturerName)
        score += 10;

    // Mapping confidence (how widely is this part used?)
    int sourceVehicleCount = GetMappedVehicleCount(source, part);
    if (sourceVehicleCount > 20) score += 10;  // Widely used
    if (sourceVehicleCount > 50) score += 10;  // Very widely used

    int otherModelsWithPart = GetOtherModelsWithPart(part, source);
    if (otherModelsWithPart > 2) score += 5;   // Used across models

    if (part.UniversalPart) score += 5;         // Universal part

    // Penalties
    if (IsAlreadyMapped(target, part)) score = -100;  // Exclude completely

    return score;
}
```

### **Step 4: Group and Rank**
```csharp
var suggestions = rawSuggestions
    .Where(s => s.Score >= 70)  // Only high confidence
    .GroupBy(s => new { s.PartNumber, s.SourceModelName })
    .Select(g => new SmartSuggestion
    {
        PartNumber = g.Key.PartNumber,
        SourceModel = g.Key.SourceModelName,
        TargetModels = g.Select(s => s.TargetModel).ToList(),
        AverageScore = g.Average(s => s.Score),
        TotalVehicles = g.Sum(s => s.TargetVehicleCount)
    })
    .OrderByDescending(s => s.AverageScore)
    .ThenByDescending(s => s.TotalVehicles)
    .Take(100);  // Top 100 suggestions
```

---

## 📊 Data Model

```csharp
public class SmartSuggestion
{
    public string PartNumber { get; set; }
    public string PartName { get; set; }
    public string Category { get; set; }

    // Source
    public string SourceManufacturer { get; set; }
    public string SourceModelName { get; set; }
    public string SourceCommercialName { get; set; }
    public int SourceYearFrom { get; set; }
    public int SourceYearTo { get; set; }
    public int SourceEngineVolume { get; set; }
    public int SourceVehicleCount { get; set; }

    // Targets
    public List<TargetModel> TargetModels { get; set; }
    public int TotalTargetVehicles { get; set; }

    // Scoring
    public double Score { get; set; }
    public string ScoreReason { get; set; }
    public ConfidenceLevel Confidence { get; set; }  // High, Medium, Low

    // UI State
    public bool IsSelected { get; set; }
    public bool IsAccepted { get; set; }
    public bool IsRejected { get; set; }
}

public class TargetModel
{
    public string ManufacturerName { get; set; }
    public string ModelName { get; set; }
    public string CommercialName { get; set; }
    public int YearFrom { get; set; }
    public int YearTo { get; set; }
    public int EngineVolume { get; set; }
    public int VehicleCount { get; set; }
    public bool IsSelected { get; set; }  // User can deselect some targets
}

public enum ConfidenceLevel
{
    High = 90,    // 90+ score - Same engine, years, commercial name
    Medium = 70,  // 70-89 score - Same engine, years
    Low = 50      // 50-69 score - Only engine match
}
```

---

## 🎯 User Workflows

### **Workflow 1: Quick Accept (High Confidence)**
1. User opens Smart Suggestions tab
2. System shows suggestions sorted by score (highest first)
3. User sees 🟢 Green card with score 95
4. Reads: "Front Brake Pad from Honda Civic Sport → 3 similar models (67 vehicles)"
5. Clicks **"Accept All"**
6. System maps part to all 67 vehicles across 3 models
7. Card disappears from list (or moves to "Accepted" section)

### **Workflow 2: Review and Customize**
1. User sees suggestion but wants to review target models
2. Clicks **"Edit Models"** or **"Details"**
3. Dialog shows all target models with checkboxes
4. User unchecks "Honda Civic Touring" (doesn't want this one)
5. Clicks **"Accept Selected"**
6. System maps to only selected models

### **Workflow 3: Bulk Accept**
1. User clicks **"Accept Top 10"** button at top
2. System shows confirmation: "Map 10 parts to 234 vehicles across 28 models?"
3. User confirms
4. System processes all mappings
5. Progress bar shows completion
6. Summary: "✓ Successfully mapped 10 parts to 234 vehicles"

### **Workflow 4: Filtering**
1. User sets filter: Manufacturer = "Honda", Min Score = 90
2. List updates to show only high-confidence Honda suggestions
3. User clicks **"Accept All Visible"**
4. Only filtered suggestions are accepted

---

## 🚀 Implementation Recommendation

**I recommend Option 1 (Dashboard/Card View)** because:
1. ✅ Most intuitive for new users
2. ✅ Visual and modern design
3. ✅ All information visible at once
4. ✅ Clear action buttons
5. ✅ Color-coded scores make decisions easy
6. ✅ Can still add filters and bulk actions
7. ✅ Expandable cards for details if needed

**With these features:**
- Statistics panel at top
- Filters (Manufacturer, Category, Min Score)
- Bulk action buttons (Accept Top N, Accept All High Confidence)
- Card-based suggestions (scrollable list)
- Per-card actions (Accept All, Edit Models, Reject)
- Confidence color coding (Green 90+, Yellow 70-89, Red 50-69)
- "Load More" for pagination

---

## 🎨 Color Scheme

```
🟢 Green (90-100): High Confidence - Same engine + years + commercial name
🟡 Yellow (70-89): Medium Confidence - Same engine + years
🔴 Red (50-69): Low Confidence - Only engine match (maybe show but warn)
⚪ Gray: Rejected/Processed
```

---

## ⚡ Performance Considerations

1. **Lazy Loading**: Load top 50 suggestions initially, load more on scroll
2. **Background Generation**: Generate suggestions in background task
3. **Caching**: Cache generated suggestions (refresh on demand)
4. **Batch Processing**: Accept multiple suggestions in single transaction
5. **Progress Feedback**: Show progress bar for bulk operations

---

## 📝 Next Steps

1. **Review this design** - Which UI option do you prefer?
2. **Confirm algorithm** - Any changes to scoring logic?
3. **Additional filters** - Any other filters needed?
4. **Implementation** - I'll create the tab with your preferred design

---

## Questions for You:

1. **Which UI layout do you prefer?** (Option 1, 2, or 3)
2. **Should suggestions be cross-manufacturer?** (e.g., suggest Honda parts for Toyota if specs match)
3. **Minimum confidence threshold?** (Currently 70 - show lower scores?)
4. **Auto-refresh?** (Regenerate suggestions daily/weekly/on-demand?)
5. **History?** (Track accepted/rejected suggestions to improve algorithm?)

Let me know your preferences and I'll start implementing! 


you froze in the middle of this development but added a smartsuggestion class, also research renault mazda peugeot fiat hyundai kia patterns at least for now also,
also in car management and in quick map in vehicle lookup and in other suggestion place around the tabs if a part with oem number x is mapped to a vehicle, and theres another part with oem number x suggest it to the model name to be mapped, sometimes one oem field can contain a couple of oems seperated by / make sure you take it into account
also in auto suggestions dont open anymore the dialog if i press map then map it by the previous logic
in manual mapping when a model name has more than one engine volume split the tree to the different engine volumes so i can map them differently
and a change in mapping, now when i accept an auto suggest or when i map anything what you currently do is map it to every identical model name, keep doing it but when engine volume is different open a dialog where you show the part and the different models with the different engine volumes and ask if i want to map to them or npot, it should happen wherever you map to entire model name check all the different tabs
and also when doing the suggestions in other tabs if there exists a part that is mapped to a vehicle and another part with an identical oem number (remove special characters) consider it to be the same part and add it in full confidence to the suggestions, bear in mind sometimes and oem field has different oems separated by /
