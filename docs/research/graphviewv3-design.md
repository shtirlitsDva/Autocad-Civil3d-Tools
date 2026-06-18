<graphviewv3-design>
Reference design doc for the autonomous build. Not for review. Captures decisions,
directive deltas, the in-memory connectivity engine, project layout, the reload-safe
shape, and the build order. Grounded in the live sample FJV-fremtid_DEV.dwg.

<directive-deltas-from-proposal>
From the user's revdiff annotations on graphviewv3-architecture.md:
- No alignment buckets. Many entities are bare polylines/blocks with no alignment.
- Nodes FLOAT: disconnected partial networks and single just-placed components float
  freely; they coalesce as they connect. => force-directed layout, NOT hierarchical.
- Palette: follow DimensioneringV2 pattern. WPF hosted on main thread via AddVisual;
  heavy work on a BACKGROUND thread over a captured DTO snapshot; marshal back via
  SynchronizationContext/Dispatcher. (DimensioneringV2 does NOT use a separate UI
  thread — documented deviation from the user's "own thread" wording; same outcome.)
- All event registration via AcadEventManager; extend it cleanly with the missing
  Database object events (ObjectAppended/Modified/Erased).
- ~1 Hz update, gated by a cheap "did the DB change?" check + a dirty flag set ONLY
  for fjernvarme entities/blocks. Dashboard may need a different (count-based) gate.
- New IN-MEMORY connectivity engine; the old graphclear()/graphpopulate() writes to
  PropertySets (legacy) and is too slow for live. Its own project.
- Renderer: straight to Option B (custom WPF canvas). Node click = select+zoom;
  reverse highlight deferred.
- STIKTEE filtering dropped (legacy, unused).
- No hard phase gates; no inter-phase compilability requirement. Smallest running
  surface first, verify, then iron out.
</directive-deltas-from-proposal>

<sample-data-model>
FJV-fremtid_DEV.dwg (240 ents): 145 dynamic BlockReferences (FJV components, layer 0),
95 Polylines (pipes) on FJV-* layers.
- Pipe = Polyline on layer FJV-<SYSTEM>-<SIZE>: SYSTEM in {TWIN, FREM, RETUR},
  SIZE like DN125 or PRTFLEXL50. Ports = polyline endpoints (start/end vertex).
- Component = dynamic block, effective name e.g. PRTFLX-REDUKTION, PRTFLX-BØJN-90,
  PRESKOBLING-TEE-PRT, T TWIN S2, VENTIL-*, ENDEBUND, MATERIALESKIFT, AFGRSTUDS.
  Ports = nested block refs named MuffeIntern / MuffeIntern-MAIN / MuffeIntern-BRANCH
  (normalize: name without '-', lower, startswith "muffeintern"), each nested
  block's Position transformed by the parent BlockReference.BlockTransform -> WCS.
  NOT the insertion point. For dynamic blocks the nested couplings live in the
  effective (anonymous *U##) BlockTableRecord = br.BlockTableRecord.
FJV detection v1: Polyline.Layer starts with "FJV"; BlockReference is a dynamic block
(later: gate against the FjvDynamicComponents catalog). Coords are UTM meters;
tolerance ~ small (0.1-1.0 m), determined empirically.
</sample-data-model>

<projects>
Both NEW, under Acad-C3D-Tools/. Edit via H:, register devreload via X: (same tree).

1. NetworkGraphCore  (Acad-C3D-Tools/NetworkGraphCore/NetworkGraphCore.csproj)
   net8.0-windows (WPF types for geometry-free? keep pure: net8.0). AutoCAD-FREE.
   - DTOs: PipeDto { Handle, Layer, System, Size, P0, P1, Length }, ComponentDto
     { Handle, Name, Position, Ports[] }, plus NetworkSnapshotDto (lists + a content
     hash for change detection).
   - Connectivity: spatial-grid port bucketing + union-find -> components -> graph
     (Nodes, Edges). Pure, unit-testable.
   - Layout: simple force-directed (Fruchterman-Reingold style) producing 2D coords;
     disconnected components packed apart so they "float".
   - Stats: counts, length-by-size, system split, components-by-type.
   No AutoCAD refs => can have a .Tests project link-including sources.

2. GraphViewV3  (Acad-C3D-Tools/GraphViewV3/GraphViewV3.csproj)
   net8.0-windows8.0, x64, references AutoCAD/Civil3D + NetworkGraphCore + WpfSHARED/
   theme. Reload-safe plugin.
   - GraphViewV3Plugin : IExtensionApplication (NoCommands marker; static cleanup
     fields; Terminate disposes palette + events).
   - Command GRAPHVIEWV3 (registered via DevReload's Utils.AddCommand path).
   - GraphViewPaletteSet : PaletteSet (AddVisual of the WPF root; tabs Graph/Stats).
   - SnapshotReader: on main thread, read FJV ents -> DTOs fast, release.
   - LiveModel: AcadEventManager.Idle @ ~1Hz + dirty flag (Database events) ->
     snapshot -> Task.Run(build+layout) -> Dispatcher marshal -> VM.
   - GraphCanvas (WPF): draws floating nodes/edges, DN colors, QA; click->select+zoom.
   - ViewModels (CommunityToolkit.Mvvm).
</projects>

<reload-safe-shape>
- [assembly: CommandClass(typeof(GraphViewV3.NoCommands))] + empty NoCommands class.
- All cleanup state in STATIC fields (Initialize and Terminate run on different
  instances).
- Terminate: palette.Close()+Dispose()+null; Events.Dispose() (AcadEventManager
  untracks all). No overrules/transients used.
- csproj: portable PDB, x64, NoWarn CA1416, AutoCAD refs Private=False.
- WPF XAML types must be in SharedAssemblies.Config.json (devreload_write_shared_
  assemblies) or XAML type resolution fails (gotcha 7). Prefer code-built WPF or
  ensure NetworkGraphCore + GraphViewV3 + theme are shared. To minimize XAML-ALC
  pain in v1, build the UI largely in C# (no external custom-control XAML refs).
</reload-safe-shape>

<update-loop>
Database handlers (ObjectAppended/Modified/Erased), filtered to FJV ents, set _dirty.
AcadEventManager.Idle handler throttles to ~1Hz: if _dirty && (now-last)>=1s &&
quiescent -> snapshot (main thread) -> compare content hash to last; if changed,
Task.Run(build+layout+stats) -> Dispatcher.Post -> swap VM snapshot atomically.
Cheap change check: snapshot hash over (handle, layer, geometry-rounded). If equal,
skip background work entirely.
</update-loop>

<build-order>
B1 Scaffold both csproj; reload-safe shell; GRAPHVIEWV3 opens empty dark palette.
   Verify: ui_list_surfaces shows the palette.
B2 SnapshotReader + a text readout in palette: "<n> pipes, <m> components" live.
   Verify against sample (95 pipes / 145 comps).
B3 NetworkGraphCore connectivity + force layout (+ unit tests).
B4 GraphCanvas renders floating graph; reload; screenshot.
B5 Live loop (events+idle+background); edit drawing -> graph updates.
B6 DN coloring, QA edges, node click->select+zoom.
B7 Stats dashboard tab.
B8 HTML completion report.
Each step: devreload_reload -> drive -> assert. No gating; keep it running.
</build-order>
</graphviewv3-design>
