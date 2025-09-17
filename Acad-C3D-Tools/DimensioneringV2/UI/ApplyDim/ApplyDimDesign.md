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
   - Hovering shows a single-feature highlight under the cursor to aid the first selection; leaving removes it.
   - Clicking selects the first feature and keeps it highlighted.
4. Stage 2 (pick second feature):
   - After the first selection, a debounced hover (200 ms) previews a path from the start feature to the hovered feature (highlight with halo).
   - Hovering a feature in a different graph shows no preview (only the first feature remains highlighted).
   - Leaving non-selected features clears preview but not the first selection.
5. Clicking the second feature finalizes the path and opens a dialog: “Select dimension for pipes” with two dropdowns (pipe type → pipe dimension). OK applies dims to all selected features; Cancel cancels and unselects everything (including the first feature).
6. Mode exits; base map behavior is restored (including the previous selected property if it wasn’t `Pipe`).

### Interaction and hit-testing (Mapsui v4.1.9 verified)
- Project uses Mapsui v4.1.9 (see `DimensioneringV2.csproj`). In this version:
  - There is no dedicated feature "Hovered" event. WPF `MapControl` exposes the `Info` event (used for clicks/taps) and standard WPF mouse events.
  - Hit-testing is available via `IMapControl.GetMapInfo(ScreenPosition, IEnumerable<ILayer>)` and requires the data layer to have `IsMapInfoLayer = true` (already set on the `"Features"` layer). See `@Mapsui` IMapControl docs.
- Approach while mode is active:
  - Subscribe to WPF events on `MapControl`: `MouseMove`, `MouseLeave`, `MouseLeftButtonUp`.
  - On `MouseMove`, do not query immediately on every move. Use a short `DispatcherTimer` (200 ms) to debounce; when the timer fires and no mouse button is pressed, call `mapControl.GetMapInfo(screenPosition, new[] { featuresLayer })` to determine the hovered feature. Update hover overlay only when the hovered feature changes.
  - Only run the hover→preview path computation after the first feature has been selected.
  - ~~If `GetMapInfo` yields no feature or is insufficient, fall back to geometric hit-test on `AnalysisFeature.Geometry` (NTS `LineString`). Derive a world tolerance from current resolution: `worldTol = pixelTol * map.Navigator.Viewport.Resolution` with `pixelTol` ~ 6–10.~~ <- NO! Don't do this.
  - If `GetMapInfo` yields no feature -> user clicked outside a feature -> do nothing.
  - On `MouseLeftButtonUp`, perform the same hit-test to select the first/second feature.
- While the mode is active, suppress the info popup by ignoring `OnMapInfo` content updates and keeping `IsPopupOpen = false`.

#### Hover performance (from Mapsui issue #2316)
- Querying on every mouse move can be expensive. Recommended pattern (per Mapsui maintainers):
  - Use platform mouse move events + `MapControl.GetMapInfo(...)` only after a short delay while the pointer pauses.
  - Stop the timer on `MouseLeave` and when any mouse button is pressed. Ignore hover while any mouse button is down.
  - Consider `mapControl.GetPixelDensity()` if device-independent to pixel conversions are needed.
  - Only refresh overlays when state actually changes to avoid extra redraws.

### Highlighting with halo (single overlay layer)
- Do not mutate styles on the base `"Features"` layer.
- Create one overlay layer `"Angiv"` placed above `"Features"` with a single consistent style (line + white halo).
- Behavior:
  - Before first selection: show hovered feature only.
  - After first selection: show the union of {first selection} and the current preview path to hovered feature.
  - On second selection: clear overlays (dialog opens). After OK, theme refreshes and we reset to first selection.
- On updates, replace the provider contents and call `Mymap.RefreshData()`.

### Mapsui specifics checked
- `"Features"` layer is created with `IsMapInfoLayer = true` and a `MemoryProvider` in `CreateMapFirstTime()`/`UpdateMap()`.
- `UpdateMap()` only removes/replaces the `"Features"` layer by name, so additional overlay layers persist across theme changes.
- Legend rendering is independent of overlays. Overlays use fixed styles and are not part of legend.
- Reference: `IMapControl.GetMapInfo(ScreenPosition, IEnumerable<ILayer>)` and WPF `MapControl` mouse events in `@Mapsui`.

### Theme behavior while in AngivDim
- On start:
  - Remember the previously selected property: `MapPropertyWrapper _prevSelectedProperty`.
  - Set `SelectedMapPropertyWrapper = new MapPropertyWrapper(MapPropertyEnum.Pipe, "Rørdimension")` (or pick the existing wrapper from `MapProperties`).
  - Call `UpdateMap()` to apply the `Pipe` category theme via `ThemeManager`.
- On exit:
  - Restore `SelectedMapPropertyWrapper = _prevSelectedProperty` and call `UpdateMap()`.
- This ensures the user “works within” the pipe-dimension theme during the mode without affecting normal behavior when the mode ends.

### Path computation (same graph only)
- Preparation at mode start (line-graph):
  - Build an `AngivGraphManager` from `DataService.Graphs` that constructs a line-graph per connected graph:
    - Each original `AnalysisFeature` becomes a node in the line-graph.
    - Two nodes are connected if their original edges share a `NodeJunction`.
    - Maintain `AnalysisFeature → (line-graph, node)` for fast membership checks and path queries.
- After the first click:
  - Record the first `AnalysisFeature` as the start node in the line-graph.
- On debounced hover over a target feature in the same graph:
  - Quickly reject if hovered feature’s owning line-graph differs from the start feature’s.
  - Compute one shortest path in the line-graph from start feature-node to hovered feature-node.
  - Weight: `AnalysisFeature.Length`. The resulting node sequence maps directly back to `AnalysisFeature` for preview highlighting.
- If the target feature is not in the same graph (membership mismatch), show no preview path.

### Dialog: Select dimension for pipes
- WPF modal dialog with MVVM:
  - Dropdown 1: Pipe type (enumerate all supported types from `NorsynHydraulicCalc.Pipes.PipeTypes` using current `HydraulicSettings`).
  - Dropdown 2: Nominal diameter list for that type (show nominal diameter only; decision inputs are type + nominal diameter).
  - Buttons: OK / Retry / Cancel.
- On OK:
  - Set `AnalysisFeature.PipeDim` to the chosen `Dim` for all features in the finalized path.
  - Refresh theme (`UpdateMap()`) so `Pipe` category recolors immediately.
  - Remain in mode and reset to “pick first feature” (clear hover/preview/final overlays).
- On Retry:
  - Keep the first selection; clear hover/preview; return to second-stage selection.
- On Cancel:
  - Clear overlays and selection; exit mode; restore previous map property.

### ViewModel changes (MainWindowViewModel)
- State
  - `bool IsAngivDimMode`.
  - `MapPropertyWrapper _prevSelectedProperty`.
  - `AnalysisFeature? AngivStartFeature`.
  - `NodeJunction? AngivStartNode`.
  - `HashSet<AnalysisFeature> AngivPreviewPath`.
  - `HashSet<AnalysisFeature> AngivFinalPath`.
- Commands & methods
  - `AsyncRelayCommand AngivDimCommand` → calls `StartAngivDimAsync()`.
  - `Task StartAngivDimAsync()` — activate mode, switch theme to `Pipe`, add overlay layer if needed, subscribe map events.
  - `void CancelAngivDim()` — clear overlay and state, unsubscribe events, restore previous theme, ensure `IsPopupOpen = false`.
  - `void ApplyPipeDimToSelection(Dim dim)` — assign `PipeDim` and refresh.
  - Input handlers while in mode:
    - `void OnMapMouseMove(object sender, MouseEventArgs e)` — debounced hover logic + preview path if start feature is set.
    - `void OnMapMouseLeftButtonUp(object sender, MouseButtonEventArgs e)` — select first/second feature and set `AngivStartNode` on first selection.
    - `void OnMapMouseLeave(object sender, MouseEventArgs e)` — clear hover/preview (keep first selection).
    - `void OnMapPreviewKeyDown(object sender, KeyEventArgs e)` — if `Key.Escape`, call `CancelAngivDim()`.
- Hit testing helpers
  - `AnalysisFeature? GetFeatureAt(Point screenPosition)` — use `GetMapInfo` against the `"Features"` layer to resolve the feature at screen position.
- Overlay helpers (delegated to a small manager class; see below)

### Overlay manager (internal helper)
- New class `UI/MapOverlay/AngivOverlayManager.cs`:
  - Creates and retains three overlay layers: `"AngivHover"`, `"AngivPreview"`, `"AngivFinal"` with their `MemoryProvider`s and fixed `StyleCollection`s.
  - Methods:
    - `SetHover(AnalysisFeature? feature)` — replaces content of the hover provider.
    - `SetPreview(IEnumerable<AnalysisFeature> features)` — replaces content of the preview provider.
    - `SetFinal(IEnumerable<AnalysisFeature> features)` — replaces content of the final provider.
    - `ClearAll()` — clears all three providers.
  - On each change, call `Mymap.RefreshData()`.

### Integration with popup/legend
- While AngivDim is active:
  - Suppress info popup (`IsPopupOpen = false`) and ignore `OnMapInfo` content updates to avoid conflicts with hover/click logic.
  - Legend continues to reflect `Pipe` theme via existing code; overlay is not part of legend.
- After exit, normal popup and legend behavior resume.

### Files to add/edit
- Add
  - `UI/ApplyDim/AngivGraphManager.cs` — builds line-graphs (edge-nodes) per connected graph; provides edge-to-edge shortest path by `AnalysisFeature.Length`.
  - `UI/MapOverlay/AngivOverlayManager.cs` — overlay management and styles (halo via stacked vector styles).
  - `UI/Dialogs/SelectPipeDimDialog.xaml` and `.xaml.cs` — dialog view.
  - `UI/Dialogs/SelectPipeDimViewModel.cs` — dialog VM (CommunityToolkit.Mvvm).
- Edit
  - `UI/MainWindowViewModel.cs` — integrate manager, theme switching, popup suppression, and dialog plumbing.
  - `UI/MainWindow.xaml.cs` — no structural change expected; `SetMapControl(mapControl)` already gives VM access to subscribe to events.
  - `Themes` — no changes to existing theming required; `ThemeManager` already supports `MapPropertyEnum.Pipe`.

### Error handling & edge cases
- No features/graphs loaded → show a message and exit mode.
- Clicks on empty space → ignored.
- Hover on different connected graph → preview stays empty; first feature remains highlighted.
- Dialog canceled → revert all temporary state including the first selection and restore previous theme.
 - ESC pressed → cancel mode immediately (same as Cancel) and restore previous theme.

### Notes on Mapsui hover doc vs our version
- The accompanying `mapsui_hover.md` describes a `Hovered` event and `eventArgs.GetMapInfo(...)` pattern typical of Mapsui v5.
- Our project uses Mapsui v4.1.9; we therefore:
  - Use WPF `MouseMove` and `MapControl.GetMapInfo(...)` for hover (no NTS fallback).
  - Continue using the existing `MapControl.Info` subscription for tap/clicks outside AngivDim; while in AngivDim we suppress popup updates.

### Performance notes
- Build line-graphs once at mode start; membership and path queries are then fast.
- Debounce hover hit-testing with a 200 ms `DispatcherTimer` and skip while mouse buttons are down.
- Compute paths on demand in the line-graph with weights by `AnalysisFeature.Length`.
- Keep overlay feature counts minimal (only what’s hovered/previewed/selected).

### Test plan (manual)
- Activate AngivDim, verify map switches to `Pipe` theme.
- Hover: single cyan-with-halo highlight appears/disappears.
- First click: selected segment remains magenta-with-halo.
- Hover in same graph: orange-with-halo path preview updates using line-graph; different graph shows no preview.
- Second click: dialog opens; OK applies dim and recolors immediately (theme refresh), then resets to pick-first; Retry keeps first and returns to second-stage; Cancel exits and restores previous theme.
- Press ESC at any time during the mode: overlays clear and previous theme is restored.
