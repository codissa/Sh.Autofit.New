# Sticker Printing Styling Guide

This guide explains how to customize the appearance of your sticker labels. All settings are centralized in `StickerSettings.cs` for easy adjustment.

---

## Quick Reference: Where to Change What

### Font Sizes and Line Limits

All font size settings are in **`Models/StickerSettings.cs`**

| What You Want to Change | Property Name | Default Value | What It Does |
|------------------------|---------------|---------------|--------------|
| **Intro Line** | | | |
| - Starting font size | `IntroStartFontPt` | 14.0 | Initial font size before shrinking to fit width |
| - Minimum font size | `IntroMinFontPt` | 8.0 | Won't shrink below this size |
| - Font family | `IntroFontFamily` | "Arial Narrow" | Font name (must be installed on system) |
| - Bold text | `IntroBold` | true | Make intro text bold |
| **Item Key (Part Number)** | | | |
| - Starting font size | `ItemKeyStartFontPt` | 18.0 | Initial font size for part numbers |
| - Minimum font size | `ItemKeyMinFontPt` | 14.0 | Won't shrink below this size |
| **Description** | | | |
| - Starting font size | `DescriptionStartFontPt` | 18.0 | Same as ItemKey for consistency |
| - Minimum font size | `DescriptionMinFontPt` | 14.0 | Won't shrink below this size |
| - Maximum lines | `DescriptionMaxLines` | 3 | Shrink font if text exceeds this many lines |

---

## Advanced: Width/Height Scaling (Font Aspect Ratio)

You can independently control the width and height of each text field. This allows you to make text **narrower** (condensed) or **wider** (expanded) without changing the height, or vice versa.

**All scaling settings are in `Models/StickerSettings.cs`**

### How Scaling Works

- **Value = 1.0**: Normal (default)
- **Value < 1.0**: Compressed (e.g., 0.8 = 20% narrower/shorter)
- **Value > 1.0**: Expanded (e.g., 1.2 = 20% wider/taller)

### Intro Line Scaling

| Property | Default | Example |
|----------|---------|---------|
| `IntroFontWidthScale` | 1.0 | 0.8 = 20% narrower (condensed)<br>1.2 = 20% wider |
| `IntroFontHeightScale` | 1.0 | 0.8 = 20% shorter<br>1.2 = 20% taller |

**Example Use Case**: If your intro line is very long, set `IntroFontWidthScale = 0.85` to condense it horizontally.

### Item Key Scaling

| Property | Default | Example |
|----------|---------|---------|
| `ItemKeyFontWidthScale` | 1.0 | Independent from height |
| `ItemKeyFontHeightScale` | 1.0 | Independent from width |

### Description Scaling

| Property | Default | Example |
|----------|---------|---------|
| `DescriptionFontWidthScale` | 1.0 | Control text condensing |
| `DescriptionFontHeightScale` | 1.0 | Control text stretching |

### Scaling Examples

**Condensed text (fits more horizontally):**
```csharp
IntroFontWidthScale = 0.8f;  // 20% narrower
IntroFontHeightScale = 1.0f; // Normal height
```

**Taller text (more emphasis):**
```csharp
DescriptionFontWidthScale = 1.0f;  // Normal width
DescriptionFontHeightScale = 1.2f; // 20% taller
```

**Note**: For overall size changes, adjust the font size properties instead. Scaling is best for aspect ratio adjustments.

---

## Margins and Layout

**File: `Models/StickerSettings.cs`**

| Setting | Property | Default | Description |
|---------|----------|---------|-------------|
| Top margin | `TopMargin` | 2.0 mm | Space above intro line |
| Bottom margin | `BottomMargin` | 2.0 mm | Space below description |
| Left margin | `LeftMargin` | 2.0 mm | Space on left edge (already minimal) |
| Right margin | `RightMargin` | 2.0 mm | Space on right edge (already minimal) |

**Intro text already uses minimal margins** - it spans from left margin to right margin.

---

## Vertical Positioning (Advanced)

If you need to adjust where fields appear vertically on the label, edit **`Services/Printing/Zebra/ZplCommandGenerator.cs`**

**Lines 120-122:**
```csharp
int introY = MmToDots(settings.TopMargin, dpi);           // Intro at top margin
int itemKeyY = MmToDots(settings.LabelHeightMm * 0.25, dpi);     // ItemKey at 25% down
int descriptionY = MmToDots(settings.LabelHeightMm * 0.55, dpi); // Description at 55% down
```

**To adjust:**
- Decrease the multiplier (0.25, 0.55) to move fields **higher**
- Increase the multiplier to move fields **lower**
- Example: Change `0.25` to `0.30` to move ItemKey down

---

## How the Intelligent Sizing Works

### Description (Multi-line with 3-Line Limit)

1. **Starts large**: Description begins at same font size as ItemKey (18pt by default)
2. **Splits into lines**: Text is broken into lines based on available width
3. **Shrinks if needed**: If text would be more than 3 lines, font size reduces by 0.5pt and tries again
4. **Repeats**: Process continues until text fits in 3 lines OR minimum font size (14pt) is reached
5. **Never cuts mid-word**: Text always breaks at word boundaries
6. **Centered**: Each line is centered on the X-axis

### Intro Line

- **Always single line**
- Shrinks from 14pt to 8pt minimum to fit width
- Left-aligned

### Item Key

- **Always single line**
- Shrinks from 18pt to 14pt minimum to fit width
- Centered, bold for emphasis

---

## Common Adjustments

### "My intro line is too long"

**Option 1: Reduce margins**
```csharp
LeftMargin = 1.0;   // Down from 2.0mm
RightMargin = 1.0;  // Down from 2.0mm
```

**Option 2: Use narrower font scaling**
```csharp
IntroFontWidthScale = 0.85f;  // 15% narrower
```

**Option 3: Allow smaller font**
```csharp
IntroMinFontPt = 6.0f;  // Down from 8.0
```

### "My descriptions are too small"

**Option 1: Increase max lines**
```csharp
DescriptionMaxLines = 4;  // Up from 3
```

**Option 2: Reduce minimum font size**
```csharp
DescriptionMinFontPt = 12.0f;  // Down from 14.0
```

### "I want descriptions to match ItemKey size exactly"

They already start at the same size (18pt)! If they're shrinking, increase max lines:
```csharp
DescriptionMaxLines = 5;  // More lines = less shrinking needed
```

### "Text looks too wide/narrow"

Adjust the width scale for that field:
```csharp
// Make description 10% narrower
DescriptionFontWidthScale = 0.9f;

// Make item key 10% wider
ItemKeyFontWidthScale = 1.1f;
```

---

## File Locations Summary

1. **Primary configuration**: `Sh.Autofit.StickerPrinting/Models/StickerSettings.cs`
   - All font sizes, scaling factors, margins
   - This is where you'll make 95% of changes

2. **Vertical positioning**: `Sh.Autofit.StickerPrinting/Services/Printing/Zebra/ZplCommandGenerator.cs`
   - Lines 120-122 for Y-axis positions
   - Only change if fields need to move up/down

3. **Font rendering engine**: `Sh.Autofit.StickerPrinting/Services/Printing/Zebra/ZplGfaRenderer.cs`
   - Advanced: Only modify if you understand bitmap rendering
   - Already supports width/height scaling

4. **Text line breaking**: `Sh.Autofit.StickerPrinting/Helpers/FontSizeCalculator.cs`
   - Advanced: Handles word-wrapping logic
   - Shouldn't need modification

---

## Testing Your Changes

1. **Edit `StickerSettings.cs`** with your desired values
2. **Build the project**: `dotnet build Sh.Autofit.StickerPrinting/Sh.Autofit.StickerPrinting.csproj`
3. **Run the application** and print a test label
4. **Check the output** and adjust settings as needed
5. **Iterate** until you're happy with the results

**Tip**: Make small changes (one property at a time) to understand their effects.

---

## Default Settings Quick Copy-Paste

```csharp
// Intro Line
IntroStartFontPt = 14.0f;
IntroMinFontPt = 8.0f;
IntroFontFamily = "Arial Narrow";
IntroBold = true;
IntroFontWidthScale = 1.0f;
IntroFontHeightScale = 1.0f;

// Item Key
ItemKeyStartFontPt = 18.0f;
ItemKeyMinFontPt = 14.0f;
ItemKeyFontWidthScale = 1.0f;
ItemKeyFontHeightScale = 1.0f;

// Description
DescriptionStartFontPt = 18.0f;
DescriptionMinFontPt = 14.0f;
DescriptionMaxLines = 3;
DescriptionFontWidthScale = 1.0f;
DescriptionFontHeightScale = 1.0f;

// Margins
TopMargin = 2.0;
BottomMargin = 2.0;
LeftMargin = 2.0;
RightMargin = 2.0;
```

---

## Need Help?

If you can't find the setting you need or want to add new functionality, check the plan file at the root of the repository or ask for assistance.
