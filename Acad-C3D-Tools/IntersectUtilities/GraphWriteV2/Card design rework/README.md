# Handoff: District Heating — Graphviz Label Theme System

## Overview

The district-heating network viewer renders a topology graph: an algorithm reads drawing
topology, emits **Graphviz DOT**, Graphviz produces **SVG**, and the SVG is embedded in an
HTML page for the browser. Each graph node is a **component** (pipe, bend, reduction, branch,
etc.) drawn with a **Graphviz HTML-like label** (a `<TABLE>`-based label, not free HTML).

This handoff delivers two things:

1. **A modern visual redesign of those node labels** — five label *styles*, four *palettes*,
   all on a true-black canvas, with high edge contrast so panels lift off the background.
2. **A new feature: per-component Series (I / II / III)** rendered as a colored Roman-numeral
   chip on the label, plus a **WPF settings window ("Label Theme Designer")** that lets a user
   configure the whole look and saves it as a `LabelTheme` the DOT generator reads.

The goal of this package is to let a developer (a) reproduce the label markup inside the
existing DOT-generating code, and (b) build the real WPF configuration window that drives it.

## About the Design Files

The files in this bundle are **design references created in HTML** — interactive prototypes
showing the intended look and behavior. They are **not** production code to ship as-is.

Two important notes specific to this project:

- **The label markup is the real deliverable.** Unlike a normal web handoff, the styling here
  is not CSS — it is **Graphviz HTML-like-label markup** (`<TABLE>/<TR>/<TD>/<FONT>` with
  `BGCOLOR`, `GRADIENTANGLE`, `STYLE="ROUNDED"`, `SIDES`, `BORDER`, `COLOR`). The prototype
  generates exactly this markup and renders it with the real Graphviz engine (compiled to
  wasm) so what you see is what your pipeline emits. The label-builder logic in
  `Label Theme Designer.dc.html` (the `label()` / `idCell()` / `chipCell()` methods) is a
  near-direct blueprint for the generator method you will write in your DOT emitter.
- **The WPF window is mocked in HTML.** The window chrome, controls, and live preview show the
  intended UX. You will rebuild this as a real WPF window in the viewer app; the data model and
  the settings→DOT mapping below tell you exactly what it must produce.

The prototypes load the Graphviz wasm engine from a CDN purely so the preview is live — your
production pipeline already runs Graphviz, so this dependency does not carry over.

## Fidelity

**High-fidelity (hifi).** Final colors, gradients, typography, spacing, chip treatments, and
the exact generated DOT are all specified. The label markup should be reproduced precisely; the
WPF window should match the intent (layout, controls, live preview) using the app's native
WPF controls and conventions.

---

## Screens / Views

### 1. Label Theme Designer (the WPF window) — `Label Theme Designer.dc.html`

**Purpose:** Let a user configure how every component label looks, see a live Graphviz preview,
and save/apply the result as a `LabelTheme`.

**Layout:** A window (~1120px design width) with four bands:
- **Title bar** (42px): app icon + "Label Theme Designer — District Heating Network Viewer",
  Windows min/restore/close glyphs at right.
- **Body** (flex row, min-height 540px):
  - **Left controls panel** — 354px fixed, scrollable, background `#252526`, right border
    `#1b1b1b`, 20px padding. Sections (each a 11px/uppercase/`.12em` `#9d9d9d` header):
    *Label Style, Tile treatment (only when Segmented), Palette, Colors, Series numerals,
    Typography, Layout*.
  - **Right preview pane** — fills remaining width, background `#161616`. Top strip (44px)
    with "Live preview · Graphviz output" and a Chain/Single segmented toggle. Center area is
    the rendered SVG on a radial-gradient backdrop (`#1c1c1c`→`#121212`). Bottom strip (toast
    text at left, "Copy DOT" button at right).
- **Footer** (58px, `#252526`): "↺ Reset to preset" at left; "Export theme…", "Cancel",
  "Apply" buttons at right.

**Controls (each maps to a `LabelTheme` field — see Design Tokens / State):**
- **Label Style** — single-select list of 5: Glass HUD, Blueprint Grid, Segmented Tiles,
  Fluent Acrylic, Tactical Terminal. Selected row: border `#2f6ca6`, bg `#10314d`, text `#d4ebff`.
- **Tile treatment** — only visible when Segmented is selected. Segmented control:
  Flush / Cells pop / Bordered.
- **Palette** — 2×2 grid of 4 presets (Cyan Ice, Amber Industrial, Emerald Signal, Violet Neon),
  each with a color swatch. Selecting one loads all its colors. Editing any color afterward
  flips the active preset label to "Custom".
- **Colors** — 6 native color pickers: Frame, Panel ▲ (gradient top), Panel ▼ (gradient bottom),
  ID text, Body text, Divider. Each shows its hex.
- **Series numerals** — a "Show on labels" toggle; a Filled/Outline chip-fill segmented control;
  an After/Before position segmented control; three color pickers for I / II / III.
- **Typography** — ID font dropdown (Courier New, Consolas, Cascadia Mono, Lucida Console);
  Body font dropdown (Helvetica, Segoe UI, Arial, Verdana).
- **Layout** — Cell padding slider (4–14 pt); Rounded corners toggle.

**Live behavior:** Any change re-renders the Graphviz preview and updates the live LabelTheme
JSON and the live generated DOT shown in the docs section. "Copy DOT" copies the current node's
label markup; "Export theme…" downloads `label-theme.json`.

### 2. Label Proposals (catalog/reference) — `Label Proposals.dc.html`

**Purpose:** The reference catalog. Shows the Roman-chip Series treatment across all five styles
in all four palettes on true black, plus a "Segmented Tiles — two ways to make it readable"
comparison (Flush vs Cells-pop vs Bordered). Use this to see every style/palette combination at
once. Not a window — a scrolling gallery.

---

## The five label styles

All are built from nested Graphviz `<TABLE>` labels. A component label has three fields:
**ID** (e.g. `32D2`), **Type** (e.g. `PertFlextra Twin 63`, wrapped to two lines), and
**Desc** (e.g. `Rør L74.39`). The ID cell also carries the Series chip.

| Style | Look | Key markup |
|---|---|---|
| **Glass HUD** | Rounded dark-glass panel, diagonal gradient, neon mono ID, hairline dividers | `STYLE="ROUNDED"`, `BGCOLOR="fill1:fill2"`, `GRADIENTANGLE="125"`, `SIDES="B"`/`SIDES="L"` dividers |
| **Blueprint Grid** | Deep panel with full cyan rule grid, uppercase mono | `BORDER="2"`, `CELLBORDER="1"`, mono FACE everywhere |
| **Segmented Tiles** | Each field a floating tile with its own micro-gradient | `CELLSPACING` gaps; see tile modes below |
| **Fluent Acrylic** | Soft frosted gradient, generous padding, one accent dot | lighter `fillLite1:fillLite2`, `GRADIENTANGLE="135"`, ▪ accent |
| **Tactical Terminal** | Near-black, bracketed `[ID]`, mono throughout | `BORDER="2"`, bracketed ID, mono FACE |

**Segmented tile modes** (because the original blended into black):
- **Flush** — original: dark tiles, dark grout (low contrast; included for completeness).
- **Cells pop** (recommended) — *inverted roles*: true-black grout (`#050608`), **light tiles**
  (`tileA1:tileA2`), **dark ink** (`ink` / `idInk`). Most distinct treatment of the set.
- **Bordered** — dark tiles, each outlined in the palette frame color. Legible but visually
  converges with Glass/Blueprint.

---

## Interactions & Behavior

- **Style select** → swaps the label-builder branch; Segmented reveals the Tile-treatment control.
- **Palette select** → loads all colors from the preset; sets active preset name.
- **Any color / font / padding / toggle change** → marks preset "Custom" (for colors) and
  re-renders the preview within ~16ms (debounced via `setTimeout`, **not** `requestAnimationFrame`
  — rAF is throttled in background tabs and stalls the preview).
- **Series toggle off** → ID cell renders with no chip.
- **Chip fill Outline** → chip becomes a bordered cell with colored numeral instead of solid fill.
- **Chip position Before** → chip renders left of the ID instead of right.
- **Preview Chain/Single** → preview shows a 3-node connected chain (Series 1→2→3) or a single node.
- **Copy DOT** → clipboard write of the current label markup; transient toast confirmation (~1.6s).
- **Export theme** → downloads `label-theme.json`.

No network calls except the one-time Graphviz wasm load (preview only; drop in production).

## State Management

The window's entire state is one `LabelTheme` plus a couple of preview-only flags:

```
style       : 'glass' | 'blue' | 'seg' | 'flu' | 'term'
segMode     : 'flush' | 'pop' | 'framed'     // only used when style === 'seg'
preset      : 'cyan' | 'amber' | 'emerald' | 'violet' | 'custom'
C           : ThemeColors (frame, fill1, fill2, fillLite1, fillLite2, divider,
                           id, type, desc, bg, chip{1,2,3}, chipText, tileA1, tileA2, ink, idInk)
showSeries  : bool
chipFilled  : bool        // true = Filled, false = Outline
chipBefore  : bool        // true = Before, false = After
idFont      : string      // monospace face
bodyFont    : string
pad         : int 4..14   // cell padding in points
rounded     : bool
sample      : 'chain' | 'single'   // preview only, not part of saved theme
```

Selecting a preset replaces `C` wholesale. Editing a color mutates `C` and sets `preset='custom'`.

## The persisted model (C#)

```csharp
public enum LabelStyle  { Glass, Blueprint, Segmented, Fluent, Terminal }
public enum SegmentMode { Flush, Pop, Bordered }
public enum ChipFill    { Filled, Outline }
public enum ChipPos     { After, Before }

public sealed class LabelTheme
{
    public LabelStyle  Style    { get; set; }
    public SegmentMode TileMode { get; set; }   // used only when Style == Segmented
    public ThemeColors Colors   { get; set; }
    public SeriesStyle Series   { get; set; }
    public FontSet     Fonts    { get; set; }
    public int         Padding  { get; set; }   // pt, 4..14
    public bool        Rounded  { get; set; }
}

public sealed class ThemeColors
{
    public string Frame, PanelTop, PanelBottom, Id, Body, Divider, Background;
    // Segmented "Pop" mode also needs the light-tile + ink trio:
    public string TileA1, TileA2, Ink, IdInk;
    // Fluent/Glass use the lighter fill pair:
    public string FillLite1, FillLite2;
}

public sealed class SeriesStyle
{
    public bool     Show   { get; set; }
    public ChipFill Fill   { get; set; }
    public ChipPos  Pos    { get; set; }
    public string[] Colors { get; set; }   // [I, II, III]
}

public sealed class FontSet
{
    public string Id   { get; set; }   // monospace face
    public string Body { get; set; }
}
```

### Saved JSON shape (what "Export theme…" writes)

```json
{
  "style": "GlassHUD",
  "colors": { "frame":"#48d6e8","panelTop":"#15273a","panelBottom":"#1d3650",
              "id":"#7df0ff","body":"#e3f1f8","divider":"#2f5f74","background":"#0a131c" },
  "series": { "show":true,"fill":"Filled","position":"After",
              "colors": { "I":"#38bdf8","II":"#fbbf24","III":"#f472b6" } },
  "fonts":  { "id":"Courier New","body":"Helvetica" },
  "padding": 9,
  "rounded": true
}
```

## Settings → DOT mapping (the generator)

The only part of the pipeline that changes is the per-node label builder. Port the
`label(component, series, theme)` method from `Label Theme Designer.dc.html` (logic class) to
your DOT emitter. It returns the `<TABLE>…</TABLE>` markup to drop inside `node[label=<…>]`.
Pseudocode of the core:

```
labelFor(component, theme):
    idCell   = wrapId(component.id, component.series, theme)   // adds Series chip
    typeCell = FONT(theme.fonts.body, 11, theme.colors.body, twoLine(component.type))
    descCell = FONT(theme.fonts.body, 12, theme.colors.body, component.desc)
    switch theme.style:
        Glass     -> rounded TABLE, BGCOLOR "panelTop:panelBottom", GRADIENTANGLE 125,
                     SIDES="B" under id, SIDES="L" beside type
        Blueprint -> BORDER 2 / CELLBORDER 1 grid, mono faces
        Segmented -> CELLSPACING tiles; Pop = black grout + light tiles (tileA1/tileA2, ink/idInk)
        Fluent    -> rounded, lighter fillLite pair, GRADIENTANGLE 135, ▪ accent
        Terminal  -> BORDER 2, bracketed [id], mono faces

wrapId(id, series, theme):
    if not theme.series.show: return idText
    chip = theme.series.fill == Filled
           ? TD BGCOLOR=seriesColor   -> FONT chipText  roman(series)
           : TD BORDER=1 COLOR=seriesColor -> FONT seriesColor roman(series)
    return theme.series.pos == Before ? [chip, gap, idText] : [idText, gap, chip]
```

`roman(1|2|3) = "I" | "II" | "III"`. `twoLine()` splits the type at the last space and inserts `<BR/>`.

## Design Tokens

### Window chrome (WPF UI, not the labels)
- Window bg `#202020`; title bar `#2b2b2b`; controls panel `#252526`; preview `#161616`;
  footer `#252526`. Borders `#1b1b1b`/`#343434`. Accent `#4cc2ff`. Selected control:
  border `#2f6ca6`, bg `#10314d`, text `#d4ebff`. Body text `#dcdcdc`; muted `#9d9d9d`.
  Segmented active segment bg `#0e639c`. Toggle on `#4cc2ff`, off `#5a5a5a`.
- Fonts: Segoe UI for UI; JetBrains Mono / mono for hex + code readouts.

### The four label palettes (the saved colors)

| token | Cyan Ice | Amber Industrial | Emerald Signal | Violet Neon |
|---|---|---|---|---|
| background | `#0a131c` | `#15110a` | `#08140e` | `#120c1c` |
| fill1 (panel top) | `#15273a` | `#241c0f` | `#102619` | `#1f1430` |
| fill2 (panel bottom) | `#1d3650` | `#322611` | `#163524` | `#2b1c43` |
| fillLite1 (Fluent) | `#243a52` | `#3a2d16` | `#1c3c2a` | `#33224f` |
| fillLite2 (Fluent) | `#2e4865` | `#473720` | `#244c35` | `#3f2b60` |
| frame (border) | `#48d6e8` | `#e0a536` | `#34c98a` | `#a855f7` |
| divider | `#2f5f74` | `#6e5526` | `#27684a` | `#5a3f80` |
| id text | `#7df0ff` | `#ffd36b` | `#73f0ad` | `#d9b8ff` |
| type text | `#9fc3d6` | `#d8c39a` | `#a6cfb8` | `#bda9d6` |
| body text | `#e3f1f8` | `#f1e6cf` | `#dff2e7` | `#ece1f7` |
| Series I | `#38bdf8` | `#56c2ff` | `#2dd4bf` | `#c084fc` |
| Series II | `#fbbf24` | `#a3e635` | `#facc15` | `#f0abfc` |
| Series III | `#f472b6` | `#fb7185` | `#fb7185` | `#67e8f9` |
| chip text (on filled chip) | `#04121a` | `#1a1305` | `#04130b` | `#160a22` |
| tileA1 (Segmented Pop light) | `#cfe4f0` | `#ece0c2` | `#d2ecdd` | `#e2d3f2` |
| tileA2 (Segmented Pop light) | `#e8f3fa` | `#f6efda` | `#e8f5ee` | `#f0e7fb` |
| ink (Segmented Pop dark text) | `#0c1a26` | `#241a09` | `#0a1f14` | `#190d29` |
| idInk (Segmented Pop dark ID) | `#0d5f7e` | `#8a5810` | `#136a44` | `#6a3da0` |

Segmented "Pop" grout is a fixed near-black `#050608`. Chip gradient angles: 120–135°.

### Numeric tokens
- Cell padding: 4–14 pt (default 9). ID point-size 13; type 11; body 12; chip 10.
- Gap between ID and chip: a `WIDTH="9"` spacer cell. Chip `CELLPADDING="3"`.
- Preview scale: chain ×1.4, single ×1.7 (preview only).

## Fonts

Graphviz HTML-labels render with whatever the `FACE` attribute names — that font **must be
installed on the host that runs the export**, not the browser. Ship the theme with safe
fallbacks (Courier New, Segoe UI, Arial) or bundle fonts with the viewer. Everything else
(gradients, rounded tables, per-side borders, colored chips) is pure Graphviz with no external
assets. There are **no web fonts, CSS, shadows, blur, or rotation** available in HTML-labels;
depth is faked with gradients, layered tables, and cell gaps.

## Constraints of Graphviz HTML-like labels (important for implementation)

**Available:** nested TABLE/TR/TD, FONT (face/color/point-size), B/I/U/S/O/SUB/SUP, BR/HR/VR,
linear & radial gradient fills (`BGCOLOR="a:b"` + `GRADIENTANGLE`), rounded *tables*
(`STYLE="ROUNDED"`), per-side cell borders (`SIDES`), cell/border `COLOR` incl. 8-digit
`#RRGGBBAA`, COLSPAN/ROWSPAN, COLUMNS/ROWS, PORT, HREF/TOOLTIP.

**Not available:** CSS/classes, box-shadow/glow/blur, rounded *individual* cells or per-corner
radius, rotation/transforms, absolute positioning, web-fonts.

## Screenshots

In `screenshots/`:
- `01-theme-designer-cyan-glass.png` — the window, Glass HUD + Cyan Ice, live chain preview.
- `02-theme-designer-amber-terminal.png` — Tactical Terminal + Amber Industrial.
- `03-theme-designer-violet-segmented.png` — Segmented Tiles + Violet Neon.
- `04-theme-designer-spec-docs.png` — the live LabelTheme JSON / C# model / pipeline / DOT docs.
- `05-catalog-overview.png` — proposals catalog top.
- `06-segmented-legibility-comparison.png` — Flush vs Cells-pop vs Bordered, all palettes.
- `07-styles-cyan-palette.png` / `08-styles-violet-palette.png` — all five styles per palette.

## Assets

None. No images or icons are required — all visuals are generated markup. The window icon and
Windows glyphs in the mock are placeholders; use the app's real chrome.

## Files

- `Label Theme Designer.dc.html` — the WPF window mock + live Graphviz preview + the
  label-builder logic (the blueprint for your DOT generator) + the design-spec docs.
- `Label Proposals.dc.html` — the full catalog: every style × palette, and the Segmented
  legibility comparison.
- `support.js` — runtime for the `.dc.html` prototypes (lets them open directly in a browser).

To view a prototype: open either `.dc.html` in a browser (needs internet once, for the
Graphviz wasm engine that powers the live preview).
