<section>
<overview>

# Legend Widget — User Guide

The legend system uses a **declarative element tree** to define what legends display.
You compose panels from abstract building blocks — no SkiaSharp code needed.
The rendering engine handles measure, layout, and drawing automatically.

</overview>
</section>

<section>
<architecture>

## How It Works

Everything is a **rectangle**. Rectangles can contain sub-rectangles.

```
LegendElement (abstract base — every element has Margin)
├── StackPanel    (container: stacks children vertically or horizontally)
├── TextBlock     (styled text with multi-line support)
├── ItemList      (categorical items: color swatch + label rows)
├── GradientBar   (continuous color bar with min/max labels)
├── ColorSwatch   (single colored line)
└── Spacer        (explicit empty space)
```

The `LegendWidget` holds a single root `LegendElement` (its `Content` property).
The `LegendWidgetSkiaRenderer` calls `Content.Measure()` then `Content.Draw()` —
that's it. All layout logic lives inside the elements themselves.

### Two-Pass Layout (WPF-style)

1. **Measure** (bottom-up): each element reports its desired size
2. **Draw** (top-down): parent positions children within allocated bounds

Margins are handled automatically by the base class — subclasses only implement
`MeasureCore` and `DrawCore` which receive margin-free bounds.

</architecture>
</section>

<section>
<elements>

## Element Reference

### StackPanel

Container that arranges children sequentially. The core layout primitive.

```csharp
new StackPanel
{
    Orientation = LegendOrientation.Vertical,   // or Horizontal
    Background = new Color(255, 255, 255, 200), // optional semi-transparent background
    Padding = new Thickness(10, 5, 10, 5),      // left, top, right, bottom
    Margin = new Thickness(0),                   // outer spacing
    Children = [ /* child elements */ ]
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Orientation` | `LegendOrientation` | `Vertical` | Stack direction |
| `Children` | `List<LegendElement>` | `[]` | Child elements |
| `Padding` | `Thickness` | `(0,0,0,0)` | Internal spacing inside background |
| `Background` | `Color?` | `null` | Fill color (drawn as rect behind children) |

---

### TextBlock

Styled text with multi-line support (split on `\n`).

```csharp
new TextBlock
{
    Text = "Estimeret\nvarmebehov\n[MWh/år]",
    FontSize = 16,
    Bold = true,
    Align = LegendTextAlign.Center,
    Color = new Color(0, 0, 0),
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | `""` | Display text (supports `\n`) |
| `FontSize` | `float` | `14` | Font size in pixels |
| `Bold` | `bool` | `true` | Bold weight |
| `Align` | `LegendTextAlign` | `Left` | `Left`, `Center`, or `Right` |
| `Color` | `Color` | Black | Text color (Mapsui Color) |

---

### ItemList

Renders a list of `LegendItem` rows — each row has a color line (or bitmap) and a label.
Items with empty labels are automatically filtered out.

```csharp
new ItemList
{
    Items = legendItems,       // IList<LegendItem>
    ItemHeight = 18,           // height of each row
    LineLength = 30,           // length of the color swatch line
    LabelGap = 10,             // space between swatch and label
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Items` | `IList<LegendItem>` | `[]` | Legend items to display |
| `ItemHeight` | `float` | `18` | Row height |
| `LineLength` | `float` | `30` | Swatch line length |
| `LabelGap` | `float` | `10` | Space between swatch and text |

---

### GradientBar

A vertical gradient color bar with min/max labels. Self-contained compound element.

```csharp
new GradientBar
{
    Min = 0.5,
    Max = 12.3,
    MinLabel = "0.50",
    MaxLabel = "12.30",
    BarWidth = 35,
    BarHeight = 100,
    LabelGap = 10,
    ColorGradient = ColorBlendProvider.Standard,
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Min` / `Max` | `double` | `0` | Value range |
| `MinLabel` / `MaxLabel` | `string?` | `null` | Labels at bottom/top of bar |
| `BarWidth` | `float` | `35` | Bar width in pixels |
| `BarHeight` | `float` | `100` | Bar height in pixels |
| `LabelGap` | `float` | `10` | Space between bar and labels |
| `ColorGradient` | `OklchGradient?` | `null` | Color provider (OKLCH perceptual) |

---

### ColorSwatch

A single colored horizontal line. Useful for custom legend compositions.

```csharp
new ColorSwatch
{
    SwatchColor = Color.Red,
    LineWidth = 3,
    Length = 30,
}
```

---

### Spacer

An invisible rectangle of fixed size. Use for explicit spacing between elements.

```csharp
new Spacer { Height = 8 }          // vertical space
new Spacer { Width = 10 }          // horizontal space
new Spacer { Width = 5, Height = 5 } // both
```

---

### Thickness (Margin / Padding)

```csharp
new Thickness(10)                    // uniform: all sides = 10
new Thickness(10, 5)                 // horizontal=10, vertical=5
new Thickness(10, 5, 10, 5)          // left, top, right, bottom
```

</elements>
</section>

<section>
<examples>

## Examples

### Categorical Legend

```csharp
new StackPanel
{
    Background = new Color(255, 255, 255, 200),
    Padding = new Thickness(10, 5, 10, 5),
    Children = [
        new TextBlock { Text = "Rørdimensioner", FontSize = 16, Align = LegendTextAlign.Center },
        new Spacer { Height = 4 },
        new ItemList { Items = _legendItems },
    ]
}
```

### Gradient Legend

```csharp
new StackPanel
{
    Background = new Color(255, 255, 255, 200),
    Padding = new Thickness(10, 5, 10, 5),
    Children = [
        new TextBlock { Text = "Varmebehov\n[MWh/år]", FontSize = 16, Align = LegendTextAlign.Center },
        new Spacer { Height = 4 },
        new GradientBar
        {
            Min = 0.5, Max = 12.3,
            MinLabel = "0.50", MaxLabel = "12.30",
            ColorGradient = ColorBlendProvider.Standard,
        },
    ]
}
```

### Multiple Legends (stacked)

Show two legends at once — e.g., stikledninger + fordelingsledninger:

```csharp
new StackPanel
{
    Children = [
        stikLegendPanel,
        new Spacer { Height = 8 },
        fordelingsLegendPanel,
    ]
}
```

### Custom Composition

Build a legend with mixed element types:

```csharp
new StackPanel
{
    Background = new Color(255, 255, 255, 200),
    Padding = new Thickness(10, 5, 10, 5),
    Children = [
        new TextBlock { Text = "Custom Legend", FontSize = 16, Align = LegendTextAlign.Center },
        new Spacer { Height = 4 },
        new StackPanel
        {
            Orientation = LegendOrientation.Horizontal,
            Children = [
                new ColorSwatch { SwatchColor = Color.Red, LineWidth = 4 },
                new Spacer { Width = 10 },
                new TextBlock { Text = "Critical path" },
            ]
        },
        new StackPanel
        {
            Orientation = LegendOrientation.Horizontal,
            Children = [
                new ColorSwatch { SwatchColor = Color.Green, LineWidth = 4 },
                new Spacer { Width = 10 },
                new TextBlock { Text = "Normal path" },
            ]
        },
    ]
}
```

</examples>
</section>

<section>
<adding-to-theme>

## Adding a Legend to a Theme

Implement `ILegendSource` on your theme class:

```csharp
class MyTheme : StyleBase, IThemeStyle, ILegendSource
{
    // ... style logic ...

    public LegendElement? BuildLegendPanel() => new StackPanel
    {
        Background = new Color(255, 255, 255, 200),
        Padding = new Thickness(10, 5, 10, 5),
        Children = [
            new TextBlock { Text = "My Title", FontSize = 16, Align = LegendTextAlign.Center },
            new Spacer { Height = 4 },
            new ItemList { Items = myLegendItems },
        ]
    };
}
```

The `ThemeManager.GetLegendContent()` method automatically extracts the legend from the
current theme by casting to `ILegendSource` and calling `BuildLegendPanel()`.

</adding-to-theme>
</section>

<section>
<files>

## File Structure

```
Legend/
├── Elements/
│   ├── LegendElement.cs       Base class + Thickness + enums
│   ├── LegendRenderContext.cs  Cached SKPaint/metrics per frame
│   ├── StackPanel.cs           Container (flex layout)
│   ├── TextBlock.cs            Styled text
│   ├── ItemList.cs             Categorical item rows
│   ├── GradientBar.cs          Gradient color bar + labels
│   ├── ColorSwatch.cs          Single colored line
│   └── Spacer.cs               Empty spacing
├── ILegendSource.cs            Interface for themes to build panels
├── LegendWidget.cs             Mapsui IWidget with Content property
├── LegendWidgetSkiaRenderer.cs Thin renderer: measure → draw
├── LegendItem.cs               Data class for categorical items
├── LegendTitleProvider.cs      Localized titles
├── LegendLabelProvider.cs      Localized labels
└── LEGEND-WIDGET-GUIDE.md      This file
```

</files>
</section>
