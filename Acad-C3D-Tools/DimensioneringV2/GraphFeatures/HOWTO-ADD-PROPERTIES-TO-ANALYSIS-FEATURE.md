<section>
# How to Add a New Property to AnalysisFeature

<overview>
The property system is **attribute-driven**. All configuration for theming, labels, and legends
lives directly on the `[MapProperty]` attribute in `AnalysisFeature.cs`. The consuming systems
(ThemeManager, LabelManager, LegendTitleProvider, LegendLabelProvider) read this configuration
via reflection and react automatically.

**Adding a new property = 1 enum value + 1 decorated property. That's it.**

Only 2 files need to change for the vast majority of properties:
1. `UI/MapPropertyEnum.cs` - add the enum value
2. `GraphFeatures/AnalysisFeature.cs` - add the property with `[MapProperty]` + `[DisplayCategory]`
</overview>
</section>

---

<section>
## Quick Reference

<overview>

| Property Type | What You Need |
|--------------|---------------|
| **Gradient** (numerical, color interpolation) | Enum value + `[MapProperty(Theme = ThemeKind.Gradient)]` |
| **Category** (discrete, unique color per value) | Enum value + `[MapProperty(Theme = ThemeKind.Category)]` |
| **Display-only** (popup only, no theme) | `[DisplayCategory]` only, no `[MapProperty]` |

</overview>
</section>

---

<section>
## Adding a Gradient Property (Numerical)

<overview>
For continuous values (double, int) displayed as a blue-to-red color gradient.
</overview>

### Step 1 - Add enum value

<example>
**File:** `UI/MapPropertyEnum.cs`

```csharp
HeatLoss
```
</example>

### Step 2 - Add the property

<example>
**File:** `GraphFeatures/AnalysisFeature.cs`

```csharp
[MapProperty(MapPropertyEnum.HeatLoss,
    Description = "Varmetab",
    Theme = ThemeKind.Gradient,
    LegendTitle = "Varmetab\n[kW]")]
[DisplayCategory(DisplayCategoryEnum.Hydraulik)]
public double HeatLoss
{
    get => GetAttributeValue<double>(MapPropertyEnum.HeatLoss);
    set => SetAttributeValue(MapPropertyEnum.HeatLoss, value);
}
```

**Attribute parameters:**
- `Description` - Text shown in the theme dropdown
- `Theme = ThemeKind.Gradient` - Enables gradient theming
- `LegendTitle` - Title in the legend box (use `\n` for line breaks, include units)
</example>

### Optional: Custom label formatting

<example>
Add `LabelFormat` to the attribute if the default formatting (F2 for doubles) is not right:

```csharp
// Show as percentage (value × 100 + "%")
LabelFormat = LabelFormat.Percentage

// Hide label if value ≤ 0
LabelFormat = LabelFormat.HideIfZeroOrLess
```
</example>

### Optional: Add to ResetHydraulicResults()

<example>
If the property is a calculation result (not input data), add it to `ResetHydraulicResults()`:

```csharp
HeatLoss = 0;
```
</example>

### Done. No other files to touch.

</section>

---

<section>
## Adding a Category Property (Discrete)

<overview>
For discrete values (bool, string, int as identifier) where each distinct value gets its own color.
</overview>

### Step 1 - Add enum value

<example>
**File:** `UI/MapPropertyEnum.cs`

```csharp
PipeOwner
```
</example>

### Step 2 - Add the property

<example>
**File:** `GraphFeatures/AnalysisFeature.cs`

```csharp
[MapProperty(MapPropertyEnum.PipeOwner,
    Description = "Rorejer",
    Theme = ThemeKind.Category,
    LegendTitle = "Rorejer",
    BasicStyleValues = [""],
    LegendLabel = LegendLabelFormat.ShowOrFallback,
    LegendLabelFallback = "Ukendt")]
[DisplayCategory(DisplayCategoryEnum.Placering)]
public string PipeOwner
{
    get => GetAttributeValue<string>(MapPropertyEnum.PipeOwner);
}
```

**Additional attribute parameters for category themes:**
- `BasicStyleValues` - Values that get plain black line instead of colored
  (e.g., `[""]` for empty strings, `["false"]` for bools, `["0"]` for ints)
- `LegendLabel` - How each value is formatted in the legend (see table below)
- `LegendLabelTemplate` - Template string for specific formats
- `LegendLabelFallback` - Fallback text for empty/null values
</example>

### Legend Label Formats

<example>

| Format | Usage | Template Example |
|--------|-------|-----------------|
| `Default` | `value.ToString()` | - |
| `BoolTrueFalse` | True/false to custom labels | `"Bridge edge\|Non-bridge edge"` |
| `Template` | String.Format with `{0}` | `"Sub-graph {0}"` |
| `ShowOrFallback` | Show value or fallback if empty | Set `LegendLabelFallback = "Ukendt"` |
| `HideBasicShowRest` | Hide basic style values, show rest | - |

</example>

### Done. No other files to touch.

</section>

---

<section>
## Adding a Display-Only Property

<overview>
For properties that appear in the feature popup but don't have a map theme.
Only needs `[DisplayCategory]`, no `[MapProperty]`, no enum value.
</overview>

### Only step

<example>
**File:** `GraphFeatures/AnalysisFeature.cs`

```csharp
[DisplayCategory(DisplayCategoryEnum.Hydraulik)]
public double SomeCalculatedValue
{
    get => this["SomeCalculatedValue"] as double? ?? 0;
    set => this["SomeCalculatedValue"] = value;
}
```

Note: Uses direct `this["key"]` access instead of `GetAttributeValue<T>()`.
</example>

</section>

---

<section>
## Advanced: Complex Property with Nested Display Value

<overview>
If the property's displayed/themed value comes from a sub-property (like `Dim.DimName`),
use `DisplayValuePath` and optionally `OrderingPropertyPath`.
</overview>

<example>

```csharp
[MapProperty(MapPropertyEnum.Pipe,
    Description = "Rordimension",
    Theme = ThemeKind.Category,
    LegendTitle = "Rordimensioner",
    BasicStyleValues = ["NA 000"],
    DisplayValuePath = "DimName",              // f.Dim.DimName
    OrderingPropertyPath = "OrderingPriority", // sort by f.Dim.OrderingPriority
    LegendLabel = LegendLabelFormat.HideBasicShowRest)]
[DisplayCategory(DisplayCategoryEnum.Rortype)]
public Dim Dim { ... }
```

- `DisplayValuePath` tells the system to follow one more property hop for the display value
- `OrderingPropertyPath` tells the system to sort category values by a different sub-property
</example>

</section>

---

<section>
## Advanced: Custom Label Logic (Escape Hatch)

<overview>
For genuinely complex label formatting that can't be captured by enum flags,
use `LabelFormat = LabelFormat.Custom` and register a formatter in `LabelManager.cs`.
</overview>

<example>
Currently only `CriticalPath` uses this - it shows `DifferentialPressureAtClient` for
Stikledning segments instead of its own value:

**On the property:**
```csharp
LabelFormat = LabelFormat.Custom
```

**In `Labels/LabelManager.cs`, add to `_customFormatters`:**
```csharp
[MapPropertyEnum.YourProperty] = af =>
{
    // Your custom logic here
    return af.SomeOtherProperty.ToString("F2");
}
```
</example>

</section>

---

<section>
## Architecture Overview

<overview>

```
AnalysisFeature property
    |  [MapProperty(Enum, Description, Theme, LegendTitle, ...)]
    |  [DisplayCategory(Category)]
    v
MapPropertyMetadata (reflection cache, built once at startup)
    |
    +---> ThemeManager.SetTheme()      -- reads meta.ThemeKind to route
    +---> LabelManager.GetLabelStyle() -- reads meta.LabelFormat for formatting
    +---> LegendTitleProvider          -- reads meta.LegendTitle
    +---> LegendLabelProvider          -- reads meta.LegendLabel for formatting
    +---> MainWindowViewModel          -- reads meta.Description for dropdown
```

All configuration flows from the attribute. The consumers only contain generic dispatch
logic (gradient vs category, format type routing) with no per-property switch statements.

</overview>
</section>

---

<section>
## DisplayCategoryEnum Reference

<overview>

| Enum Value | Display Order | Section |
|------------|--------------|---------|
| `Placering` | 0 | Location/placement info |
| `Beregningsforudsaetninger` | 1 | Calculation inputs |
| `Rortype` | 2 | Pipe type/dimension |
| `Hydraulik` | 3 | Flow and velocity |
| `Tryktab` | 4 | Pressure loss |

Within a category, properties appear in **declaration order** in the source file.
</overview>
</section>

---

<section>
## All MapProperty Attribute Parameters

<overview>

| Parameter | Type | Required | Default | Purpose |
|-----------|------|----------|---------|---------|
| `MapPropertyEnum` | enum | Yes (constructor) | - | Type-safe identifier |
| `Description` | string | For themed | `""` | Dropdown text |
| `Theme` | ThemeKind | For themed | `None` | Gradient / Category / None |
| `LegendTitle` | string | For themed | `""` | Legend box title |
| `BasicStyleValues` | string[] | Category | `[]` | Values with plain black style |
| `DisplayValuePath` | string | Rare | `""` | Sub-property for display value |
| `OrderingPropertyPath` | string | Rare | `""` | Sub-property for category sorting |
| `LabelFormat` | LabelFormat | Rare | `Default` | Label text formatting |
| `LegendLabel` | LegendLabelFormat | Category | `Default` | Legend item formatting |
| `LegendLabelTemplate` | string | Some formats | `""` | Template for legend labels |
| `LegendLabelFallback` | string | ShowOrFallback | `""` | Fallback for empty values |

</overview>
</section>
