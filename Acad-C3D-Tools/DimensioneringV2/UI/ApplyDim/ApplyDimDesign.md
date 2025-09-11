# ApplyDim Design - AngivDim (Select and Apply Pipe Dimension)

## Overview
- **Goal**: Implement interactive selection and dynamic highlighting on a Mapsui map to let a user pick a first feature, preview a path to a second feature across the same graph, and apply a user-chosen pipe dimension to the selected path.
- **Scope**: `DimensioneringV2` module (Mapsui map, view model, styles/themes, graph/path services, dialogs, commands). Uses CommunityToolkit.Mvvm.

## User Flow (Required Behavior)
1. **Start**: User clicks `Angiv dim` button in `MainWindow`.
2. **Pick first feature**:
   - While hovering, features under the mouse are highlighted; highlight is removed on mouse leave.
   - On click, the hovered feature becomes the selected start feature and remains highlighted.
3. **Pick second feature**:
   - While hovering other features, compute a path from the first selected feature to the hovered feature within the same connected graph component and highlight that preview path.
   - If the hovered feature is in a different graph, keep only the first feature highlighted (no preview path).
   - On mouse leave (from hovered feature/path), remove preview highlighting (keep first feature highlighted).
4. **Confirm second feature**:
   - On click, the preview path becomes the final selection.
   - Show dialog “Select dimension for pipes” with two dropdowns:
     - Dropdown 1: Pipe type
     - Dropdown 2: Pipe dimension (dependent on selected pipe type)
     - Buttons: OK / Cancel
   - On OK: Apply the selected dimension to all selected features (path). Update styles on the map.
   - On Cancel: Abort and clear all temporary selection/highlights including the first feature.

## Key Concepts and Existing Infrastructure
- **Features**: `AnalysisFeature` (implements Mapsui `IFeature`, holds attributes via `MapPropertyEnum`). Pipe dim property: `AnalysisFeature.PipeDim` (type `NorsynHydraulicCalc.Pipes.Dim`). Category style for `MapPropertyEnum.Pipe` exists in `ThemeManager`.
- **Graphs**: Built via `GraphCreationService` into `UndirectedGraph<NodeJunction, EdgePipeSegment>` (QuikGraph). `DataService.Graphs` exposes graphs; each `EdgePipeSegment.PipeSegment` is an `AnalysisFeature`. Pathfinding already used in services (e.g., Dijkstra in `HydraulicCalculationsService`).
- **Map & Styles**:
  - Map: `MainWindowViewModel.Mymap` (Mapsui). Layer “Features” sourced from a `MemoryProvider(Features)`.
  - Themes: `ThemeManager` sets `CurrentTheme` based on `MapPropertyEnum` and labels toggle. Category theme exists for `MapPropertyEnum.Pipe` which maps `AnalysisFeature.PipeDim.DimName` to styles and supports legend.
  - Update hook: `UpdateMap()` rebuilds the Features layer from current `Features` collection and theme.
- **UI Wiring**:
  - `MainWindow` sets `mapControl.Map` and hooks `mapControl.Info += vm.OnMapInfo` for info popup. We will add input handling specifically for AngivDim mode (mouse move + click handling) separate from generic info.
  - MVVM: Commands are `RelayCommand`/`AsyncRelayCommand` in `MainWindowViewModel`.

## Design: New Interaction Mechanism
We introduce a transient interaction controller bound to the lifecycle of `AngivDim` command.

### 1) ViewModel Additions (MainWindowViewModel)
- New state fields:
  - `bool IsAngivDimMode` – toggles the custom interaction.
  - `IFeature? AngivStartFeature` – first selected feature (map-level abstraction).
  - `HashSet<IFeature> AngivPreviewPath` – currently previewed path features.
  - `HashSet<IFeature> AngivFinalPath` – final selected path features (after second click, before dialog result).
  - Graph lookup caches (built at command start for performance):
    - `Dictionary<IFeature, UndirectedGraph<NodeJunction, EdgePipeSegment>> FeatureToGraph`
    - `Dictionary<IFeature, EdgePipeSegment> FeatureToEdge`
    - `Dictionary<EdgePipeSegment, (NodeJunction a, NodeJunction b)> EdgeEndpoints`
- New styling helpers:
  - Transient highlight styles not conflicting with current theme: overlay layer with a style or per-feature style injection.
- New public methods:
  - `Task StartAngivDimAsync()` – orchestrates the full workflow.
  - `void CancelAngivDim()` – clears state and temporary styles, exits mode.
  - `void ApplyPipeDimToSelection(Dim dim)` – sets `PipeDim` on selected features and refreshes map/theme.
  - `void OnMapMouseMove(object sender, MapMouseEventArgs e)` – hover/highlight logic depending on stage.
  - `void OnMapMouseLeftButtonUp(object sender, MapMouseEventArgs e)` – select logic.
- New utility methods:
  - `IFeature? HitTestFeature(screenPos)` – hit test map to feature (use Mapsui hit test APIs or layer `GetMapInfo` with tolerance).
  - `IEnumerable<IFeature> FindPath(IFeature from, IFeature to)` – delegates to pathfinding service; external contract remains `IFeature`.
  - `void SetHoverHighlight(IFeature? feature)` – single-feature hover highlight.
  - `void SetPreviewPathHighlight(IEnumerable<IFeature> features)` – path preview highlight.
  - `void ClearTemporaryHighlights()` – remove hover/preview highlights and refresh map.

### 2) Input Handling Strategy
- While in `IsAngivDimMode`:
  - Subscribe to map control events: `MouseMove`, `MouseLeave`, `MouseLeftButtonUp` (or Mapsui specific events if available:
    - `MapControl.MouseMove` for hover
    - `MapControl.MouseLeave` to clear hover
    - `MapControl.MouseLeftButtonUp` to select
  ).
- Disable `OnMapInfo` popup during mode or ignore info events (popup should not interfere).
- On exit (OK/Cancel), unsubscribe these handlers and restore `OnMapInfo`.

### 3) Highlighting Mechanism
- We will implement overlay option A.
  - Create one overlay `Layer` named `AngivOverlay` with in-memory provider of temporary `Mapsui.IFeature` wrappers (or lightweight clones) that reference geometries of the selected/preview features.
  - Provide simple vector styles: hover (e.g., cyan width 6), preview path (e.g., orange width 6), final selection (e.g., magenta width 6). Ensure high z-order above “Features”.
  - Update this layer’s datasource on changes and call `Mymap.RefreshData()`.
  - Avoid per-feature style mutation to keep `ThemeManager` independent and avoid side effects.

### 4) Path Computation
- Build graph membership and edge endpoint caches at command start:
  - For each graph in `DataService.Graphs`, map each `EdgePipeSegment.PipeSegment` (an `AnalysisFeature`/`IFeature`) to its graph and edge.
  - Precompute `EdgeEndpoints` per edge for quick node lookups.
- For a hovered target:
  - Verify both start and target features exist in `FeatureToGraph` and reference the same graph. If not, no path preview.
  - Use the pathfinding service (encapsulated Dijkstra) to compute the shortest edge path between endpoints of start and target edges; return corresponding `IFeature` sequence for preview.

### 5) Dialog: Select Dimension For Pipes
- New dialog `SelectPipeDimDialog` (WPF window or UserControl in a modal `Window`), opened by the view model via a dialog service abstraction to keep MVVM:
  - Dropdown 1: Pipe type (e.g., loaded from `NorsynHydraulicCalc.Pipes` or a local provider that enumerates types).
  - Dropdown 2: Pipe dimension values filtered by selected type.
  - OK returns the chosen `Dim` value; Cancel returns null.
- After OK:
  - Set `PipeDim` on all features in `AngivFinalPath`.
  - Ensure `ThemeManager` property is set to `MapPropertyEnum.Pipe` or keep the user’s map property but guarantee immediate repaint still reflects `PipeDim` category colors when/if `Pipe` property is shown. We will not auto-switch the theme; we will rely on existing `ThemeManager` `Pipe` category when the user chooses it. However, we will call `Mymap.RefreshData()` so labels and styles update if `Pipe` is already active.
- After Cancel: Clear highlights and reset `AngivStartFeature`.

## Public API and Command Wiring
- Reuse `AngivDimCommand` to call `StartAngivDimAsync()`.
- In `MainWindow.xaml.cs`:
  - After map initialization, the view model already receives `MapControl` via `vm.SetMapControl(mapControl)`.
  - For Angiv mode, the VM will attach/detach event handlers to `mapControl`.

## Detailed Steps per Stage
1) Start
- `StartAngivDimAsync()`:
  - Ensure data loaded (`Features`, `Graphs`). If empty, show message and exit.
  - Set `IsAngivDimMode = true`.
  - Attach map event handlers; temporarily disable info popup (`IsPopupOpen = false`).
  - Clear overlay layer and ensure it is added to the map once.

2) Hover before first click
- On `MouseMove`:
  - Hit test to closest feature under cursor; if changed, update hover overlay to that single feature.

3) First click
- On `MouseLeftButtonUp` over a feature:
  - Set `AngivStartFeature = feature`.
  - Update overlay: first feature highlighted as “final selection (stage 1)”.
  - Initialize pathfinding service with the graph containing the start feature (via `FeatureToGraph`).

4) Hover before second click
- On `MouseMove` while `AngivStartFeature != null`:
  - Hit test a feature.
  - If null or same as start: preview path = empty.
  - Else if `FeatureToGraph` shows a different graph than the start: preview path = empty.
  - Else ask the pathfinding service to compute the path and set overlay to highlight that path.

5) Second click
- On `MouseLeftButtonUp`:
  - If valid hovered feature in same subgraph and path found: set `AngivFinalPath = path`.
  - Open `SelectPipeDimDialog` modally and await result.
  - If OK: call `ApplyPipeDimToSelection(dim)`, clear mode and overlay.
  - If Cancel: call `CancelAngivDim()`.

6) Cleanup
- `CancelAngivDim()` unsubscribes events, clears overlays, resets state, re-enables info.

## Data Structures & Performance Considerations
- Precompute per-graph maps on first-click:
  - `Dictionary<AnalysisFeature, EdgePipeSegment>` for O(1) edge lookup.
  - `Dictionary<AnalysisFeature, (NodeJunction a, NodeJunction b)>` to know endpoints.
  - Dijkstra from both nodes of the start edge using QuikGraph `ShortestPathsDijkstra` once; retain delegates to quickly get path to each node of the hovered edge, choose the shorter.
- This minimizes path recomputation on mouse move.

## Styling Details (Overlay)
- Layer: `AngivOverlay` with 3 buckets within one provider:
  - Hover feature: style Cyan, width 6.
  - Preview path features: style Orange, width 6.
  - Start/final selection: style Magenta, width 6.
- Implementation options:
  - Use one provider with distinct `IFeature` instances copied from selected `AnalysisFeature` geometries and a custom attribute to pick style in a `CategoryTheme<string>` (e.g., `Role` in {Hover, Preview, Final}).
  - Or maintain 3 small providers and 3 layers stacked; prefer single layer with category theme to keep z-order simpler.

## Dialog Implementation
- New files:
  - `UI/Dialogs/SelectPipeDimDialog.xaml` (+ `.xaml.cs`) – WPF dialog.
  - `UI/Dialogs/SelectPipeDimViewModel.cs` – CommunityToolkit.Mvvm VM with:
    - `ObservableCollection<PipeType>` PipeTypes
    - `ObservableCollection<Dim>` PipeDims
    - `PipeType SelectedPipeType` updates `PipeDims` on change
    - `Dim SelectedDim`
    - `ICommand OkCommand`, `ICommand CancelCommand`
  - Pipe types and dims source provider:
    - Option A: if there is an existing provider in the project, use it.
    - Option B: implement a simple `IPipeCatalog` that exposes known types and their dims (from `NorsynHydraulicCalc.Pipes`).
- The dialog will return a single `Dim` value, encapsulating type+size.

## Changes Required in Project
- `UI/MainWindowViewModel.cs`:
  - Implement `AngivDim()` to call `StartAngivDimAsync()`.
  - Add fields/properties for Angiv state using `IFeature` collections.
  - Add/Remove event handlers against `MapControl` during mode.
  - Implement hit testing and overlay management.
  - Use `PathFindingService` for path computation (no direct Dijkstra in VM).
  - Implement `ApplyPipeDimToSelection(Dim)` to set `PipeDim` on underlying `AnalysisFeature` instances corresponding to selected `IFeature`s and refresh map.
- `UI/MainWindow.xaml.cs`:
  - No structural change; ensure VM has access to `MapControl`. VM controls handler subscriptions.
- `Themes/ThemeManager.cs`:
  - No functional change needed; it already supports `MapPropertyEnum.Pipe` via category theme. Ensure `GetTheme()`/legend updates still work after `PipeDim` changes.
- New overlay infrastructure:
  - `UI/MapOverlay/AngivOverlayManager.cs`: encapsulates creation and updates of overlay layer and its category theme, with methods to set hover/preview/final sets (all `IFeature`-based).
- New dialog components as above.
- New: `Services/PathFindingService.cs` encapsulating Dijkstra and graph membership lookups.

## Public Interfaces (LLM-Friendly)
- Main entrypoint:
  - `MainWindowViewModel.AngivDimCommand` triggers `StartAngivDimAsync()`.
- VM-callable helpers:
  - `StartAngivDimAsync(): Task`
  - `CancelAngivDim(): void`
  - `ApplyPipeDimToSelection(Dim dim): void`
- Overlay manager (internal):
  - `SetHover(IFeature? feature): void`
  - `SetPreview(IEnumerable<IFeature> features): void`
  - `SetFinal(IEnumerable<IFeature> features): void`
  - `ClearAll(): void`
- Pathfinding service (encapsulated Dijkstra):
  - `Initialize(UndirectedGraph<NodeJunction, EdgePipeSegment> graph): void`
  - `bool TryComputePath(IFeature from, IFeature to, out IReadOnlyList<IFeature> path)`

## Error Handling & Edge Cases
- No features or graphs loaded → show message and exit mode.
- First click on empty space → ignore; remain in hover stage.
- Second hover in different subgraph → show only first feature; never show a path.
- Second click on empty space or invalid target → ignore click.
- Dialog cancel → fully revert highlights and selection.
- After applying dims, if current map property is not `Pipe`, we still update data but keep user-selected property. If the user later selects `Pipe`, styles reflect new dims.

## Styling/Legend Interaction
- `ThemeManager.SetTheme(SelectedMapPropertyWrapper.EnumValue, _showLabels)` continues to drive the “Features” layer.
- Overlay layer is independent of themes and not reflected in the legend.
- If current property is `Pipe`, calling `UpdateMap()` or `Mymap.RefreshData()` will recolor features by `PipeDim` immediately.

## Telemetry & Undo Considerations
- Optional future work: record applied changes to enable undo of last application. Not in this iteration.

## Test Plan (Manual)
- Load data; click `Angiv dim`.
- Hover over features: cyan hover appears; leave: hover clears.
- Click feature A: A remains magenta.
- Hover feature B in same subgraph: orange path preview appears; moving to C updates preview.
- Hover feature D in different subgraph: only A remains; no path preview.
- Click target in same subgraph: dialog appears; selecting type+dim OK applies dim to all path features; map updates if `Pipe` property active.
- Cancel: all highlights cleared; A also cleared.

## Estimated File Additions/Edits
- Add: `UI/MapOverlay/AngivOverlayManager.cs`
- Add: `UI/Dialogs/SelectPipeDimDialog.xaml`, `.xaml.cs`, `SelectPipeDimViewModel.cs`
- Add: `Services/PathFindingService.cs` (optional)
- Edit: `UI/MainWindowViewModel.cs` (implement AngivDim workflow)
- Edit: `UI/MainWindow.xaml.cs` (no changes expected; VM handles handlers via `SetMapControl` reference)

## Dependencies
- Uses existing: QuikGraph, Mapsui (WPF), CommunityToolkit.Mvvm, `NorsynHydraulicCalc.Pipes.Dim`.
- No new NuGet packages required.
