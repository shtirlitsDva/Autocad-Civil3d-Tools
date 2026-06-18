<graphviewv3-architecture>

GRAPHVIEWV3 — a LIVE WPF palette inside AutoCAD/Civil 3D 2025 (.NET 8) that shows
the district-heating network as a graph that updates as the user draws, plus a
statistics dashboard. This document is a research-backed architecture proposal,
not a final plan. Mockup: docs/research/graphviewv3-mockup.html.

<goal-and-scope>
In scope:
- A dockable palette with three tabs: Graph (live), Statistics (dashboard),
  Settings (per-project).
- The graph rebuilds automatically as the user edits the drawing, throttled so it
  never fights the editor.
- Click a node -> select/zoom the entity in AutoCAD (reuse the existing handle->
  selection idea behind the current ahk:// links, but in-process now).
- Dashboard: element counts, total length, length-by-DN, twin/bonded split,
  components by type, QA issue count.
- "Maximize" the graph to fill the palette; tabs for the dashboard.

Explicitly OUT of scope for v1 (YAGNI): editing the network FROM the palette,
multi-document side-by-side views, persisting layout positions, exporting PDF
(GRAPHWRITEV2 already does static export — keep it).
</goal-and-scope>

<autocad-internals-verified>
Researched against the ObjectARX 2025 Developer's Guide (via the oarxdocs index)
and confirmed against existing code in this repo.

1. HOSTING WPF IN A PALETTE — solved, in-repo pattern.
   `PaletteSet.AddVisual(tabName, wpfVisual)` accepts a WPF `Visual` directly; no
   ElementHost/WinForms interop. Reference: MPE/PipePlanDE/PipePlanDEPaletteBase.cs.
   The palette runs on AutoCAD's main UI thread (same thread as the document).

2. PER-EDIT EVENTS — Database reactor, NOT the central event manager.
   The .NET events that tell us the drawing changed are on the DATABASE:
     Database.ObjectAppended, Database.ObjectModified, Database.ObjectErased
   (ObjectARX: AcDbDatabaseReactor). Important nuance from the docs: modified()/
   erased() are COMMIT-TIME events (fired at the end of the outermost transaction),
   so they are coarser and safer than per-keystroke — but still can fire many times
   per command. NEVER rebuild the graph inside these handlers.
   NOTE: EventManager/AcadEventManager.cs centralizes only Application + Document-
   Manager events (incl. Idle and DocumentActivated). It does NOT expose Database
   object events. So GRAPHVIEWV3 subscribes to the active document's Database events
   itself, and re-subscribes on DocumentActivated. Use AcadEventManager.Track(doc,
   unsubscribe) so the per-document handlers are cleaned up on document destroy.

3. COALESCING — Application.Idle (already idiomatic here).
   The handlers just set a dirty flag; Application.Idle (exposed via AcadEventManager.
   Idle) coalesces a burst of edits into ONE rebuild when the editor goes quiet.
   This is the same "mark dirty, refresh on Idle" debounce used by
   LerSlopeArrowManager and PipePlanSharpCornerMarkerManager (those render drawing
   transients; we reuse their LOOP, not their renderer).

4. THREADING & DOCUMENT SAFETY.
   Idle fires on the main thread with no command active (CMDACTIVE == 0), so it is
   safe to open a read transaction and walk the database there. Reads need no
   document lock; we will not write. Keep the rebuild OFF the UI render path — do
   the database read + graph build synchronously in Idle (it's main-thread), but
   keep it short via throttling and (later) incremental updates.

5. DOCUMENT LIFECYCLE.
   On DocumentActivated -> rebind to the new doc's Database, mark dirty, refresh.
   On DocumentToBeDeactivated / DocumentToBeDestroyed -> detach Database handlers
   (AcadEventManager already raises these and auto-runs tracked cleanups on destroy).
</autocad-internals-verified>

<central-design-problem>
The make-or-break issue is NOT the UI — it is the COST of recomputing the graph.

GRAPHWRITEV2 today calls graphclear() + graphpopulate() to recompute EVERY pipe
connection before GraphBuilderV2.BuildGraphs(). That full connectivity pass is
fine for a one-shot command but FAR too expensive to run on every idle tick of a
live feature, especially on large networks.

Three escalating strategies (pick per measured cost — DO NOT pre-optimize):
  S1. Throttled full rebuild: on dirty + idle, if >= N ms since last rebuild (e.g.
      500 ms) AND editor quiescent, run the existing full pipeline. Simplest;
      correct; may stutter on very large drawings. START HERE and MEASURE.
  S2. Throttle + scope: only rebuild when the changed objects are on FJV layers
      (filter in the Database handler by layer/type before setting dirty). Avoids
      rebuilds for unrelated edits.
  S3. Incremental connectivity: maintain the graph and patch only the changed
      neighborhood. High effort; only if S1/S2 measured too slow. This is a real
      project on its own — defer until proven necessary (YAGNI).

This staging is the core risk-management decision and should be validated with a
spike on a representative large drawing before committing to the renderer work.
</central-design-problem>

<update-pipeline>
The data flow, as a single deep module boundary ("NetworkLiveModel"):

  [Database events]        [Idle tick]
   ObjectAppended  ─┐       (coalesce)
   ObjectModified  ─┼─> set _dirty ─> if _dirty && throttle-ok && quiescent:
   ObjectErased    ─┘                    1. read DB (read tx, main thread)
                                          2. GetFjvEntities -> connectivity -> BuildGraphs
                                          3. compute stats + QA records
                                          4. diff vs previous snapshot
                                          5. raise GraphUpdated(snapshot)
   [DocumentActivated] ─> rebind + _dirty=true

The palette/viewmodels consume GraphUpdated; they never touch the database.
This keeps the AutoCAD-facing complexity behind ONE interface (deep module),
with the WPF side as a pure consumer of an immutable snapshot.
</update-pipeline>

<module-decomposition>
Few deep modules, each with a small surface (per the project's architecture rules):

1. NetworkLiveModel (the deep AutoCAD-facing module) — NEW.
   Owns: Database event subscription, dirty flag, Idle-driven throttled rebuild,
   document rebinding. Produces an immutable NetworkSnapshot.
   Surface: event GraphUpdated; Start(doc); Stop(); ForceRefresh().
   Reuses: AcadEventManager (Idle + doc lifecycle + Track), GraphBuilderV2,
   Graph<GraphEntity>, the existing connectivity (graphpopulate) initially.

2. NetworkSnapshot (immutable value object) — NEW.
   Owns: the built graphs, per-node view data (label, DN, system, handle, color
   key), edge list incl. QA-flagged edges, and precomputed statistics.
   This is the contract between model and UI. Built off-screen, swapped atomically.

3. GraphView rendering (the chosen engine — see <rendering-decision>) — NEW.
   Surface: Render(NetworkSnapshot); NodeClicked event (-> select in AutoCAD).

4. GraphViewPalette + GraphViewViewModel(s) — NEW, mirrors PipePlanDE pattern.
   PaletteSet shell (AddVisual), CommunityToolkit.Mvvm ObservableObject VMs for the
   three tabs, dark theme merged from the existing Theme.xaml / DarkTheme.xaml.

5. Statistics computation — small helper, fed by NetworkSnapshot.
   Reuses PipeScheduleV2 enums + GraphEntity.LargestDn()/SystemLabel/length to group
   length-by-DN, twin/bonded split, counts by ElementType.

Reuse map (do NOT reinvent):
  Idle + doc lifecycle ....... EventManager/AcadEventManager.cs
  Debounce loop pattern ...... MPE/Ler3DNetwork/Shared/LerSlopeArrowManager.cs
  Graph model + builder ...... GraphWriteV2/GraphBuilderV2.cs, UtilsCommon/Graphs/*
  QA codes ................... GraphWriteV2/EdgeQaAttributeProvider.cs (already exists)
  Pipe sizing/system ......... PipeScheduleV2/PipeScheduleV2.cs, GraphEntity
  Palette + MVVM + theme ..... MPE/PipePlanDE/*, MPE/PipePlan/Views/DarkTheme.xaml
  Handle->select in ACAD ..... the logic behind today's ahk://ACCOMSelectByHandle
</module-decomposition>

<rendering-decision>
Three viable engines for the live WPF graph. All consume NetworkSnapshot.

OPTION A — MSAGL WpfGraphControl (drop-in).
  Microsoft Automatic Graph Layout's ready WPF control: Sugiyama layout + pan/zoom/
  tooltip/highlight/search built in. NuGet AutomaticGraphLayout.WpfGraphControl
  1.1.12 ships a net6.0-windows7.0 build that LOADS on our net8.0-windows host.
  + Fastest path to a working live graph; layout + interaction for free.
  - Dated, lightly maintained; its own rendering chrome is hard to make "Fluent/
    colorful"; node templating is limited.
  Use as: MVP / loop-validation, maybe permanent if visuals are good enough.

OPTION B — MSAGL CORE layout + custom WPF Canvas renderer (recommended target).
  Use only AutomaticGraphLayout.dll (the .NET-Standard-friendly core) to compute
  node positions + edge routes; draw nodes ourselves as WPF elements on a Canvas.
  + Full Fluent/colorful styling, rounded DN-colored nodes, matches Theme.xaml,
    native WPF interaction + MVVM, arbitrary rich node content.
  + Don't reinvent layout math (standard component for the hard part).
  - More UI code than A.
  Use as: the production renderer once the live loop (Option A) is proven.

OPTION C — WebView2 + Cytoscape.js (reuses the earlier graphviz-alternatives research).
  Host an HTML graph (Cytoscape) in the palette via a WebView2 control.
  + Richest/most interactive visuals; reuses prior research.
  - New heavy dependency (WebView2 runtime); marshalling C#<->JS for selection;
    re-rendering HTML on each edit; harder to unify with the WPF dashboard chrome.
  Use as: fallback if native WPF rendering proves too limiting.

RECOMMENDATION: phase A -> B. Ship the live loop fast on Option A to de-risk the
AutoCAD-internals side, then swap the renderer to Option B for the colorful Fluent
experience. The NetworkSnapshot boundary makes the renderer swappable without
touching the model. Reject C unless B's ceiling is hit.
</rendering-decision>

<ui-composition>
- Palette: dockable PaletteSet, tall+narrow default (~430 px), AddVisual(WPF).
- TabControl: Graph | Statistics | Settings (retemplated to dark Fluent).
- Graph tab: toolbar (Refresh, Fit, Layout, Auto-update toggle, Maximize), graph
  surface, legend, live status bar (node/edge/QA counts, "updated Ns ago").
- Maximize: collapse title/tab strip so the graph fills the palette (mockup shows
  this via a .maxed state); restore via the same button.
- Statistics tab: KPI cards, Length-by-DN bars, twin/bonded donut, components list.
- Settings tab: project name, Update mode (Auto on idle / Manual), node color
  scheme (DN/system/alignment), toggles (exclude STIKTEE, highlight QA, sync
  selection, throttle). All bound via CommunityToolkit.Mvvm.
- Theme: merge the existing dark ResourceDictionary; add a colorful DN ramp +
  chart series colors. Per the WPF rules: centralized styles, dark-only, retemplate
  controls (TabControl, ScrollBar, ToggleButton, etc.), implicit styles by default.
</ui-composition>

<risks-and-open-decisions>
R1 (critical) Rebuild cost: is throttled full rebuild (S1) fast enough on the
   largest real drawings, or do we need scoping (S2) / incremental (S3)? -> SPIKE
   with timing before building UI. Owner decision: acceptable refresh latency?
R2 MSAGL longevity / .NET 8: net6 build runs on net8, but it's lightly maintained.
   Mitigated by the A->B plan (B depends only on the stable core layout dll).
R3 Event storms during big operations (paste, explode, undo of 1000s of objects):
   the dirty flag naturally coalesces, but ensure the Database handlers are O(1)
   (just set a bool + optional layer check), never allocate per object.
R4 Re-entrancy: do not rebuild while a command is active; gate on quiescent state.
R5 Multi-document: rebind cleanly on activation; one model per active document.

OPEN QUESTIONS FOR YOU:
Q1. Acceptable refresh latency (e.g. "graph catches up within ~0.5 s of going
    idle") — this sets the throttle and whether S1 suffices.
Q2. Rendering: OK to start on MSAGL (Option A) to prove the loop, then move to the
    custom Fluent canvas (Option B)? Or go straight to B?
Q3. Should clicking a node select+zoom in AutoCAD, or just select? Any need for the
    reverse (select in CAD -> highlight in graph)?
Q4. Is GRAPHVIEWV3 a NEW command/palette living beside GRAPHWRITEV2, or does it
    eventually replace it? (Proposal assumes beside.)
</risks-and-open-decisions>

<suggested-phasing>
P0 Spike (1 day): time graphpopulate()+BuildGraphs() on the biggest real drawing
   at idle, under edits. Decides R1/Q1 before any UI investment.
P1 Live model: NetworkLiveModel + NetworkSnapshot, wired to Database events + Idle
   via AcadEventManager. Prove it with a trivial text readout (node/edge count
   updating live). No graph rendering yet.
P2 MVP palette (Option A): PaletteSet + MSAGL WpfGraphControl bound to snapshots +
   the Statistics dashboard. End-to-end live experience.
P3 Fluent renderer (Option B): swap to custom WPF canvas; node click -> select in
   AutoCAD; colorful DN styling; maximize.
P4 Polish: settings persistence, QA highlighting, selection sync.
</suggested-phasing>

<sources>
- ObjectARX 2025 Dev Guide — Database reactor events (oarxdocs: AcDbDatabaseReactor,
  Immediate vs Commit-Time Events, Document Event Notification).
- In-repo: EventManager/AcadEventManager.cs; MPE/PipePlanDE/PipePlanDEPaletteBase.cs;
  MPE/Ler3DNetwork/Shared/LerSlopeArrowManager.cs; GraphWriteV2/GraphBuilderV2.cs;
  PipeScheduleV2/PipeScheduleV2.cs; MPE/PipePlan/Views/DarkTheme.xaml.
- https://github.com/microsoft/automatic-graph-layout
- https://www.nuget.org/packages/AutomaticGraphLayout.WpfGraphControl
- https://js.cytoscape.org/  (Option C, from the prior graphviz-alternatives research)
</sources>

</graphviewv3-architecture>
