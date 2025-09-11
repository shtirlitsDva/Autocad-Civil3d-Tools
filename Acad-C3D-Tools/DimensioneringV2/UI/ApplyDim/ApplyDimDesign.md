## ApplyDim Design — AngivDim (interactive dimension assignment)

### Purpose
- Implement an interactive workflow that lets a user select a start feature, preview a path to a second feature within the same graph, and then assign a pipe dimension to all features on that path.
- While AngivDim is active, the map must display pipe dimensions as colors using a CategoryTheme for `MapPropertyEnum.Pipe` and apply a clear hover/preview/selection highlight with a halo.
- This runs in parallel to the existing map theming/legend system without altering its behavior outside the mode.

### Strict alignment with current code
- Theme and properties:
  - Use `MapPropertyEnum.Pipe` (not “Dim”). Verified in `UI/MapPropertyEnum.cs`.
  - The category theme for `Pipe` is already registered in `Themes/ThemeManager.cs` via `f => f.PipeDim.DimName`.
- Features and attributes:
  - Work with `GraphFeatures/AnalysisFeature` objects; pipe dim is `AnalysisFeature.PipeDim` (`NorsynHydraulicCalc.Pipes.Dim`).
  - `ThemeManager.BuildCategoryTheme` will color by `PipeDim.DimName`.
- Map/layers:
  - Base layer is a `MemoryProvider(Features)` and `Layer` named `"Features"`, created in `MainWindowViewModel.CreateMapFirstTime()` and updated in `UpdateMap()`.
  - Legend widget is already wired and updated via `_themeManager.GetTheme()`.
- Graphs & paths:
  - Graphs come from `DataService.Graphs` (`UndirectedGraph<NodeJunction, EdgePipeSegment>`). Each `EdgePipeSegment.PipeSegment` references the backing `AnalysisFeature`.
  - Pathfinding (Dijkstra) patterns already exist in services; we will reuse QuikGraph to compute shortest routes.

### User flow
1. User clicks `Angiv dim`.
2. Mode starts. Map switches to `MapPropertyEnum.Pipe` theme so pipe dimensions are visible as colors. A dedicated overlay shows hover/preview/selection with halo.
3. Stage 1 (pick first feature):
   - Hovering shows a single-feature highlight under the cursor; leaving removes it.
   - Clicking selects the first feature and keeps it highlighted.
4. Stage 2 (pick second feature):
   - Hovering another feature in the same connected graph previews a path from the start feature to the hovered feature (highlight with halo).
   - Hovering a feature in a different graph shows no preview (only the first feature remains highlighted).
   - Leaving non-selected features clears preview but not the first selection.
5. Clicking the second feature finalizes the path and opens a dialog: “Select dimension for pipes” with two dropdowns (pipe type → pipe dimension). OK applies dims to all selected features; Cancel cancels and unselects everything (including the first feature).
6. Mode exits; base map behavior is restored (including the previous selected property if it wasn’t `Pipe`).

### Interaction and hit-testing (Mapsui-safe)
- We will not depend on undocumented Mapsui hover APIs. Instead:
  - Subscribe to standard WPF events on `MapControl`: `MouseMove`, `MouseLeave`, `MouseLeftButtonUp` while the mode is active.
  - Convert screen coordinates to world coordinates using the current viewport transform from `Mymap.Navigator.Viewport`.
  - Implement geometric hit-test on `AnalysisFeature.Geometry` (NetTopologySuite `LineString`) by measuring perpendicular distance from world point to line.
  - Use a tolerance in world units derived from the current viewport resolution: `worldTolerance = pixelTolerance * Viewport.Resolution` (pixelTolerance e.g., 8–12).
  - Pick the nearest feature within the tolerance; if none, hover is empty.
- This avoids hallucinating Mapsui hover APIs and uses data we already have (NTS geometry and viewport info).

### Highlighting with halo (overlay layer)
- Do not mutate styles on the base `"Features"` layer. Create a separate overlay layer named `"AngivOverlay"` placed above `"Features"`.
- Data source: in-memory features built from the selected `AnalysisFeature` geometries. Each overlay feature has an attribute `Role` in {`Hover`, `Preview`, `Final`}.
- Styling: use a small `CategoryTheme<string>` for the overlay to map `Role` to a `StyleCollection` that simulates a halo by stacking two vector styles:
  - `Hover`: halo (semi-transparent white, width ~9) + cyan line (width ~4)
  - `Preview`: halo (semi-transparent white, width ~9) + orange line (width ~4)
  - `Final`: halo (semi-transparent white, width ~9) + magenta line (width ~4)
- On updates, refresh the overlay provider and call `Mymap.RefreshData()`.

### Theme behavior while in AngivDim
- On start:
  - Remember the previously selected property: `MapPropertyWrapper _prevSelectedProperty`.
  - Set `SelectedMapPropertyWrapper = new MapPropertyWrapper(MapPropertyEnum.Pipe, "Rørdimension")` (or pick the existing wrapper from `MapProperties`).
  - Call `UpdateMap()` to apply the `Pipe` category theme via `ThemeManager`.
- On exit:
  - Restore `SelectedMapPropertyWrapper = _prevSelectedProperty` and call `UpdateMap()`.
- This ensures the user “works within” the pipe-dimension theme during the mode without affecting normal behavior when the mode ends.

### Path computation (same graph only)
- After the first click:
  - Determine the active connected component either via `AnalysisFeature.SubGraphId` or by locating the matching `EdgePipeSegment` inside one of the `DataService.Graphs`.
  - Build maps for the active graph for fast lookup:
    - `Dictionary<AnalysisFeature, EdgePipeSegment>`
    - `Dictionary<AnalysisFeature, (NodeJunction a, NodeJunction b)>`
  - Precompute Dijkstra delegates from both nodes of the start edge: `ShortestPathsDijkstra(edge => edge.PipeSegment.Length, startNode)`.
- On hover over a target feature in the same graph:
  - Choose the shorter of the four endpoint-to-endpoint routes (start.a→target.a/b and start.b→target.a/b).
  - Extract edges from the path delegate result and map them back to `AnalysisFeature` for preview highlighting.
- If the target feature is not in the same graph (by `SubGraphId` mismatch or missing in the active graph), show no preview path.

### Dialog: Select dimension for pipes
- WPF modal dialog with MVVM:
  - Dropdown 1: Pipe type (from `NorsynHydraulicCalc.Pipes` types).
  - Dropdown 2: Pipe dimension list filtered by selected type (values of `NorsynHydraulicCalc.Pipes.Dim`).
  - Buttons: OK / Cancel.
- On OK:
  - Set `AnalysisFeature.PipeDim` to the chosen `Dim` for all features in the finalized path.
  - If the theme is `Pipe` (it is, in this mode), call `Mymap.RefreshData()` or `UpdateMap()` to recolor by `PipeDim.DimName`.
- On Cancel:
  - Clear overlay and selection; unselect first feature; exit mode; restore previous map property.

### ViewModel changes (MainWindowViewModel)
- State
  - `bool IsAngivDimMode`.
  - `MapPropertyWrapper _prevSelectedProperty`.
  - `AnalysisFeature? AngivStartFeature`.
  - `HashSet<AnalysisFeature> AngivPreviewPath`.
  - `HashSet<AnalysisFeature> AngivFinalPath`.
  - `int AngivActiveSubGraphId`.
- Commands & methods
  - `AsyncRelayCommand AngivDimCommand` → calls `StartAngivDimAsync()`.
  - `Task StartAngivDimAsync()` — activate mode, switch theme to `Pipe`, add overlay layer if needed, subscribe map events.
  - `void CancelAngivDim()` — clear overlay and state, unsubscribe events, restore previous theme, ensure `IsPopupOpen = false`.
  - `void ApplyPipeDimToSelection(Dim dim)` — assign `PipeDim` and refresh.
  - Input handlers while in mode:
    - `void OnMapMouseMove(object sender, MouseEventArgs e)` — hover logic + preview path if start feature is set.
    - `void OnMapMouseLeftButtonUp(object sender, MouseButtonEventArgs e)` — select first/second feature.
    - `void OnMapMouseLeave(object sender, MouseEventArgs e)` — clear hover/preview (keep first selection).
- Hit testing helpers
  - `AnalysisFeature? HitTestFeature(Point screenPosition)` — screen→world; nearest segment by NTS distance with world tolerance.
- Overlay helpers (delegated to a small manager class; see below)

### Overlay manager (internal helper)
- New class `UI/MapOverlay/AngivOverlayManager.cs`:
  - Ensures a top-level layer `"AngivOverlay"` exists on `Mymap` (create once, reuse).
  - Methods:
    - `SetHover(AnalysisFeature? feature)` — show one feature as hover (Role=Hover).
    - `SetPreview(IEnumerable<AnalysisFeature> features)` — show path (Role=Preview).
    - `SetFinal(IEnumerable<AnalysisFeature> features)` — show fixed selection (Role=Final).
    - `ClearAll()` — remove all overlay features.
  - Uses a small `CategoryTheme<string>` keyed by a `Role` attribute to deliver the halo styles (as `StyleCollection` per role).

### Integration with popup/legend
- While AngivDim is active:
  - Suppress info popup (`IsPopupOpen = false`) and ignore `OnMapInfo` content updates.
  - Legend continues to reflect `Pipe` theme via existing code; overlay is not part of legend.
- After exit, normal popup and legend behavior resume.

### Files to add/edit
- Add
  - `UI/MapOverlay/AngivOverlayManager.cs` — overlay management and styles (halo via stacked vector styles).
  - `UI/Dialogs/SelectPipeDimDialog.xaml` and `.xaml.cs` — dialog view.
  - `UI/Dialogs/SelectPipeDimViewModel.cs` — dialog VM (CommunityToolkit.Mvvm).
- Edit
  - `UI/MainWindowViewModel.cs` — implement AngivDim workflow, state, event subscriptions, hit-testing, pathfinding, and `ApplyPipeDimToSelection`.
  - `UI/MainWindow.xaml.cs` — no structural change expected; `SetMapControl(mapControl)` already gives VM access to subscribe to events.
  - `Themes` — no changes to existing theming required; `ThemeManager` already supports `MapPropertyEnum.Pipe`.

### Error handling & edge cases
- No features/graphs loaded → show a message and exit mode.
- Clicks on empty space → ignored.
- Hover on different connected graph → preview stays empty; first feature remains highlighted.
- Dialog canceled → revert all temporary state including the first selection and restore previous theme.

### Performance notes
- Use per-graph lookup dictionaries after first-click to avoid repeated scans on mouse move.
- Compute Dijkstra delegates once from the two endpoints of the start edge and reuse them for rapid path previews.
- Keep overlay feature counts minimal (only what’s hovered/previewed/selected).

### Test plan (manual)
- Activate AngivDim, verify map switches to `Pipe` theme.
- Hover: single cyan-with-halo highlight appears/disappears.
- First click: selected segment remains magenta-with-halo.
- Hover in same graph: orange-with-halo path preview updates; different graph shows no preview.
- Second click: dialog opens; OK applies dim and recolors immediately (since `Pipe` theme is active); Cancel clears everything and restores previous theme.
