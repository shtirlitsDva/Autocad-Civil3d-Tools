# Understand the NTRExport project
_Exported on 11/9/2025 at 23:52:22 GMT+1 from Cursor (2.0.69)_

---

**User**

familiarize yourself with the NTRExport project

---

**Cursor**

I'll scan the `NTRExport` directory, find commands, and read the main files to understand its purpose and export flow. Then I'll summarize key components and usage.

I'll open the core NTR export writer and routing files to understand how the routed model maps to NTR records and how the export is produced. Then I’ll scan the configuration parts and topology builder in the next pass.

I'll now open the NTR header/catalog and coordinate helpers to see how IS/DN records are built and how coordinate normalization works, plus the Excel-backed configuration.

I’ve scanned the `NTRExport` project to understand its purpose, command entry points, processing pipeline, and key components. Below is a concise overview with code references.

### What it does
- Translates district heating pipelines from AutoCAD drawings into ROHR2/SINETZ Neutral Interface `.ntr` files.
- Builds a topology from polylines and fittings, routes it into primitives (RO, BOG, RED, TEE), and writes compliant NTR with headers (IS/DN/LAST).

### Entry points (AutoCAD commands)
- `NTREXPORT`: main export.
- `DXFEXPORT`: helper to split by size and set widths (not part of NTR flow).
- `NTRTEST` (Debug): runs export with fixed LAST.

```299:306:Acad-C3D-Tools/NTRExport/Commands.cs
[CommandMethod("NTREXPORT")]
public void ntrexport()
{
    ntrexportmethod();
}
#if DEBUG
[CommandMethod("NTRTEST")]
```

### Processing pipeline (high level)
- Filters for `Stål` system, builds topology from CAD polylines and `MuffeIntern` ports, plans soil spans, reads NTR config, normalizes coordinates, routes to members, groups/creates IS/DN header lines, emits element records with `REF` trace, saves `.ntr` next to the DWG.

```331:347:Acad-C3D-Tools/NTRExport/Commands.cs
var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
// ...
case Polyline _:
    return acceptedSystems.Contains(
        PipeScheduleV2.GetPipeSystem(ent));
case BlockReference br:
    return acceptedSystems.Contains(
        br.GetPipeSystemEnum());                            
```

```362:435:Acad-C3D-Tools/NTRExport/Commands.cs
// CAD ➜ Port topology
var topo = TopologyBuilder.Build(polylines, fittings);
// Topology-level soil planning
new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
// Read NTR configuration
var conf = ntrConf ?? new ConfigurationData();
// Initialize NTR coordinate normalization
NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
// Topology ➜ Routed ➜ NTR skeleton
var routed = new Routing.Router(topo).Route();
// Emit NTR
var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
// ...
var ntrText = writer.Build(routed, headerLines);
// Save next to DWG
File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
```

### Core components

- Topology builder: converts CAD to nodes/elements, reading fitting ports from nested `MuffeIntern` blocks.

```44:51:Acad-C3D-Tools/NTRExport/TopologyModel/TopologyBuilder.cs
foreach (var fitting in _fittings)
{
    var ports = MuffeInternReader.ReadPorts(fitting);
    var tf = CreateFitting(fitting);
    foreach (var cadPort in ports)
        tf.AddPort(new TPort(cadPort.Role, NodeAt(cadPort.Position), tf));
    g.Elements.Add(tf);
}
```

- Router: asks each element to emit routed members.

```14:24:Acad-C3D-Tools/NTRExport/Routing/Router.cs
public RoutedGraph Route()
{
    var g = new RoutedGraph();
    var ctx = new RouterContext(_topo);
    foreach (var e in _topo.Elements)
    {
        e.Route(g, _topo, ctx);
    }
    return g;
}
```

- Element-to-NTR mapping (examples):
  - RO (straight)

```39:48:Acad-C3D-Tools/NTRExport/Routing/RoutedToNtr.cs
yield return
    "RO " +
    $"P1={NtrFormat.Pt(straight.A)} " +
    $"P2={NtrFormat.Pt(straight.B)} " +
    $"DN=DN{straight.DN}.{straight.DnSuffix}{seriesSuffix}" +
    FormatMaterial(straight.Material) +
    LastToken(conf, straight.FlowRole) +
    straight.PipelineToken() +
    soilTokens;
```

  - BOG/RED/TEE similarly emitted in `RoutedToNtr.cs`.

- Writer: headers, sorting IS/DN, per-member emission with `REF=<handle>` (disambiguates duplicates with `-n` suffix).

```35:41:Acad-C3D-Tools/NTRExport/Ntr/NtrWriter.cs
sb.AppendLine("C General settings");
sb.AppendLine("GEN TMONT=10 EB=-Z UNITKT=MM CODE=EN13941");

sb.AppendLine("C Loads definition");
foreach (var last in _conf.Last) sb.AppendLine(last.ToString());
```

```97:101:Acad-C3D-Tools/NTRExport/Ntr/NtrWriter.cs
foreach (var line in member.ToNtr(_soil, _conf))
{
    sb.AppendLine($"{line} REF={refValue}");
}
```

- DN/IS catalog per group (system/type/series/twin), including series suffixing and twin logic.

```39:43:Acad-C3D-Tools/NTRExport/Ntr/NtrDnCatalog.cs
var suffix = _type == PipeTypeEnum.Enkelt ? "s" : "t";
var isName = $"FJV{dn}.{suffix}";
var dnName = $"DN{dn}.{suffix}{seriesSuffix}";
```

```54:69:Acad-C3D-Tools/NTRExport/Ntr/NtrDnCatalog.cs
var da = PipeScheduleV2.GetPipeOd(_system, dn);                
var s = PipeScheduleV2.GetPipeThk(_system, dn);
var kod = PipeScheduleV2.GetPipeKOd(_system, dn, _type, _series);
// bonded/single
var isodickeS = Math.Max(0.0, (kod - da) / 2.0);
yield return 
    $"DN NAME={dnName} DA={da:0.###} S={s:0.###} ISOTYP={isName} ISODICKE={isodickeS:0.###} NORM='{Norm}'";
```

```72:77:Acad-C3D-Tools/NTRExport/Ntr/NtrDnCatalog.cs
var cTwin = Math.PI * kod;
var cSingle = cTwin / 2.0;
var odSingle = cSingle / Math.PI;
var isodickeT = Math.Max(0.0, (odSingle - da) / 2.0);
yield return $"DN NAME={dnName} DA={da:0.###} S={s:0.###} ISOTYP={isName} ISODICKE={isodickeT:0.###} NORM='{Norm}'";
```

- Coordinate normalization (origin shift, meter→mm scaling happens at formatting).

```14:21:Acad-C3D-Tools/NTRExport/Ntr/NtrCoord.cs
public static void InitFromTopology(Topology topo, double marginMeters = 0.0)
{
    if (topo.Nodes.Count == 0) { OffsetX = 0.0; OffsetY = 0.0; return; }
    var minX = topo.Nodes.Min(n => n.Pos.X);
    var minY = topo.Nodes.Min(n => n.Pos.Y);
    OffsetX = minX - marginMeters;
    OffsetY = minY - marginMeters;
}
```

- Soil planning (topology-level cushion spans near fittings; routed straights assign `SOIL_*` tokens).

```18:27:Acad-C3D-Tools/NTRExport/TopologyModel/ToplogySoilPlanner.cs
// Clear any previous spans
foreach (var p in _topo.Elements.OfType<TPipe>()) p.CushionSpans.Clear();

var anchors = CollectAnchors();

foreach (var node in anchors)
{
    foreach (var pipe in IncidentPipes(node))
    {
        // From node, walk reach along connected pipes, mark cushion spans
        // ...
    }
}
```

- Pipeline tags (LTG) read from property sets:

```19:39:Acad-C3D-Tools/NTRExport/Utils/Utils.cs
public static string LTGMain(Handle source)
{
    var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
        .DocumentManager.MdiActiveDocument.Database;

    var ent = source.Go<Entity>(db);

    return PropertySetManager.ReadNonDefinedPropertySetString(
        ent, "DriPipelineData", "BelongsToAlignment");
}
```

### Configuration
- Reads Excel at `X:\AC - NTR\NTR_CONFIG.xlsx`, converts to strings, and interactively selects supply/return `LAST` to use.

```15:21:Acad-C3D-Tools/NTRExport/NtrConfiguration/NtrConfiguration.cs
private static readonly string ExcelPath = @"X:\AC - NTR\NTR_CONFIG.xlsx";
internal List<NtrLast> Last { get; }
internal NtrLast SupplyLast { get; private set; }
internal NtrLast ReturnLast { get; private set; }
```

```42:57:Acad-C3D-Tools/NTRExport/NtrConfiguration/NtrConfiguration.cs
var chosenSupplyLast =
    TGridFormCaller.Call(
        Last.Where(x => x.Name.Contains("FREM")),
        x => x.Name,
        "Select LAST for *SUPPLY*: ");
if (chosenSupplyLast == null) throw new Exception("Cancelled!");
SupplyLast = chosenSupplyLast;
// ...
```

### How to run
- Build `NTRExport`.
- In AutoCAD: NETLOAD the DLL, run `NTREXPORT`. Output `.ntr` is saved alongside the DWG.

```320:325:Acad-C3D-Tools/NTRExport/Commands.cs
var dwgPath = localDb.Filename;
var outNtrPath = Path.ChangeExtension(
    string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
```

### Limits and TODOs
- Only `Stål` system is exported currently.
- Some macros are placeholders (e.g., certain tee/branch variations).
- Excel path is hardcoded and interactive; unattended runs need adaptation.

### Questions
1) Do you want this to run headless via `accoreconsole.exe` (ConsoleTests style), or inside the UI? [[memory:8833499]]
2) Should we support more pipe systems than `Stål` in the filter?
3) Do you want to remove the interactive Excel selection (Supply/Return `LAST`) for automation?
4) Any specific NTR elements or macros you want prioritized (e.g., bonded↔twin transitions, reducer spacing)?

- If helpful, I can propose concrete edits for automation or expanded system coverage.

- NTRExport overview and flow
  - Purpose: DWG → `.ntr` for ROHR2.
  - Key commands: `NTREXPORT`, `NTRTEST` (Debug), `DXFEXPORT`.
  - Pipeline: filter → topology → soil → config → normalize → route → headers → emit → write.
  - Core files: `Commands.cs`, `TopologyModel/*`, `Routing/*`, `Ntr/*`, `NtrConfiguration/*`, `Utils/Utils.cs`.
  - Known constraints: `Stål`-only; interactive config; some macros TODO.

```plaintext
[CommandMethod("NTREXPORT")]
public void ntrexport()
{
    ntrexportmethod();
}
#if DEBUG
[CommandMethod("NTRTEST")]
```

```plaintext
var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
// ...
case Polyline _:
    return acceptedSystems.Contains(
        PipeScheduleV2.GetPipeSystem(ent));
case BlockReference br:
    return acceptedSystems.Contains(
        br.GetPipeSystemEnum());                            
```

```plaintext
// CAD ➜ Port topology
var topo = TopologyBuilder.Build(polylines, fittings);
// Topology-level soil planning
new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
// Read NTR configuration
var conf = ntrConf ?? new ConfigurationData();
// Initialize NTR coordinate normalization
NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
// Topology ➜ Routed ➜ NTR skeleton
var routed = new Routing.Router(topo).Route();
// Emit NTR
var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
// ...
var ntrText = writer.Build(routed, headerLines);
// Save next to DWG
File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
```

```plaintext
foreach (var fitting in _fittings)
{
    var ports = MuffeInternReader.ReadPorts(fitting);
    var tf = CreateFitting(fitting);
    foreach (var cadPort in ports)
        tf.AddPort(new TPort(cadPort.Role, NodeAt(cadPort.Position), tf));
    g.Elements.Add(tf);
}
```

```plaintext
public RoutedGraph Route()
{
    var g = new RoutedGraph();
    var ctx = new RouterContext(_topo);
    foreach (var e in _topo.Elements)
    {
        e.Route(g, _topo, ctx);
    }
    return g;
}
```

```plaintext
yield return
    "RO " +
    $"P1={NtrFormat.Pt(straight.A)} " +
    $"P2={NtrFormat.Pt(straight.B)} " +
    $"DN=DN{straight.DN}.{straight.DnSuffix}{seriesSuffix}" +
    FormatMaterial(straight.Material) +
    LastToken(conf, straight.FlowRole) +
    straight.PipelineToken() +
    soilTokens;
```

```plaintext
sb.AppendLine("C General settings");
sb.AppendLine("GEN TMONT=10 EB=-Z UNITKT=MM CODE=EN13941");

sb.AppendLine("C Loads definition");
foreach (var last in _conf.Last) sb.AppendLine(last.ToString());
```

```plaintext
foreach (var line in member.ToNtr(_soil, _conf))
{
    sb.AppendLine($"{line} REF={refValue}");
}
```

```plaintext
var suffix = _type == PipeTypeEnum.Enkelt ? "s" : "t";
var isName = $"FJV{dn}.{suffix}";
var dnName = $"DN{dn}.{suffix}{seriesSuffix}";
```

```plaintext
var da = PipeScheduleV2.GetPipeOd(_system, dn);                
var s = PipeScheduleV2.GetPipeThk(_system, dn);
var kod = PipeScheduleV2.GetPipeKOd(_system, dn, _type, _series);
// bonded/single
var isodickeS = Math.Max(0.0, (kod - da) / 2.0);
yield return 
    $"DN NAME={dnName} DA={da:0.###} S={s:0.###} ISOTYP={isName} ISODICKE={isodickeS:0.###} NORM='{Norm}'";
```

```plaintext
var cTwin = Math.PI * kod;
var cSingle = cTwin / 2.0;
var odSingle = cSingle / Math.PI;
var isodickeT = Math.Max(0.0, (odSingle - da) / 2.0);
yield return $"DN NAME={dnName} DA={da:0.###} S={s:0.###} ISOTYP={isName} ISODICKE={isodickeT:0.###} NORM='{Norm}'";
```

```plaintext
public static void InitFromTopology(Topology topo, double marginMeters = 0.0)
{
    if (topo.Nodes.Count == 0) { OffsetX = 0.0; OffsetY = 0.0; return; }
    var minX = topo.Nodes.Min(n => n.Pos.X);
    var minY = topo.Nodes.Min(n => n.Pos.Y);
    OffsetX = minX - marginMeters;
    OffsetY = minY - marginMeters;
}
```

```plaintext
// Clear any previous spans
foreach (var p in _topo.Elements.OfType<TPipe>()) p.CushionSpans.Clear();

var anchors = CollectAnchors();

foreach (var node in anchors)
{
    foreach (var pipe in IncidentPipes(node))
    {
        // From node, walk reach along connected pipes, mark cushion spans
        // ...
    }
}
```

```plaintext
public static string LTGMain(Handle source)
{
    var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
        .DocumentManager.MdiActiveDocument.Database;

    var ent = source.Go<Entity>(db);

    return PropertySetManager.ReadNonDefinedPropertySetString(
        ent, "DriPipelineData", "BelongsToAlignment");
}
```

```plaintext
private static readonly string ExcelPath = @"X:\AC - NTR\NTR_CONFIG.xlsx";
internal List<NtrLast> Last { get; }
internal NtrLast SupplyLast { get; private set; }
internal NtrLast ReturnLast { get; private set; }
```

```plaintext
var chosenSupplyLast =
    TGridFormCaller.Call(
        Last.Where(x => x.Name.Contains("FREM")),
        x => x.Name,
        "Select LAST for *SUPPLY*: ");
if (chosenSupplyLast == null) throw new Exception("Cancelled!");
SupplyLast = chosenSupplyLast;
// ...
```

```plaintext
var dwgPath = localDb.Filename;
var outNtrPath = Path.ChangeExtension(
    string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
```

---

**User**

no. I want to discuss a new feature I need in this ntr export Application. The problem is that all dwg entites are drawn in 2D, but the real geometry sometimes has pipes with sloping. For example we need to cross a large utility and we use 45deg VERTICAL elbows to create a passing below the large utility. The problem is not a single passing, but we can have more difficult cases. I generally convert all components and pipes into straight pipes and BOG (pipe bends), sometimes I add tee components. But other tees are just modelled using a pipe stub in without tees. I also build a topology of the piping network. So my idea is to create some kind of elevation topology. But for this I need to work from a leaf (ideally the supply connection) and using network traversal solve the elevation topology where this toplogy will be able to tell what slope a piping should have in a defined stretch of the piping network. I want to know what you think about it. Don't write code. First let's discuss and plan a possible solution. Think very hard.

---

**Cursor**

Great idea. Elevation needs to be a first-class “constraint layer” on top of your existing 2D topology so we can solve Z consistently before emission. Here’s a concrete plan that fits your current architecture and routing.

### Proposed approach
- Elevation topology as constraints on nodes
  - Treat each `TNode` as a variable Z(n). Edges (pipes/fittings) become constraints between nodes (equalities, optionally inequalities).
  - Examples:
    - Keep-level: Z(B) − Z(A) = 0
    - Prescribed slope: Z(B) − Z(A) = s · LAB (s in m/m, LAB = plan length)
    - Prescribed step: Z(B) − Z(A) = ΔZ
    - Vertical 45 pair zone: Z(B) − Z(A) = ± LAB (for a 45° down/up section between two “vertical elbows”)
    - Min cover/clearance (optional, needs surfaces/utilities): Z ≥ Zground − cover, etc. (inequalities)

- Constraint sources (layered, simple → rich)
  - Entity annotations: property set fields on polylines/fittings (e.g., “ElevSlope=0.005”, “ElevKeepLevel”, “ElevDeltaZ=−0.6”, “ElbowPlane=Vertical”, “Vertical45PairId=V123”).
  - CSV alongside DWG (you prefer CSV): rows that bind handles or node IDs to constraints and values (e.g., FromHandle, ToHandle, Type, Value).
  - Heuristics: patterns like two elbows with a short straight between them marked as vertical pair. Only use when annotations are missing.
  - External context (optional): Civil 3D surfaces/featurelines for cover/clearance constraints at nodes/segments.

- Solving strategy
  - Phase 1 (fast, deterministic): rooted traversal from a selected leaf (supply tie-in). Apply constraints as you walk the tree. For branches, propagate parent Z to children; resolve “local” constraints (slope/ΔZ/keep-level). If a node is reached with conflicting Z, flag and stop.
  - Phase 2 (robust, multi-anchor): formulate Z as a sparse linear system with hard (equalities) and soft (preferences) constraints; solve least-squares (or QP if you want inequality support). This lets you mix multiple anchors (e.g., known Z at both ends), slopes, and keep-level rules without brittle ordering. Use high weights for “hard” constraints; lower weights for preferences (smoothing, min-slope-variance).
  - Optional objective terms:
    - Minimize slope changes within a pipeline (smoothness)
    - Prefer level on branches unless annotated
    - Penalize violating min cover/clearance (if used as soft constraints)

- Integration points (no code now; just where it plugs in)
  - After `TopologyBuilder.Build(...)` and soil planning, run `ElevationSolver.Solve(topo, constraints)`, writing solved Z to each `TNode.Pos`.
  - Existing routing then reads `TNode.Pos.Z` for straights, tees, reducers; Bends use pre/post segment directions to compute their 3D tangency point (you already have 3D-capable tangent utilities, just use world-plane of the bend).
  - NTR emission remains unchanged; `NtrFormat.Pt` will export the solved Z.

- Representing vertical elbows and crossings
  - “Vertical 45” intent cannot be inferred from 2D reliably; require annotation:
    - On a fitting: `ElbowPlane=Vertical`; for paired elbows, tag both with a shared `PairId` and optionally a target ΔZ or “cross-under” requirement.
    - Alternatively, annotate the intermediate straight with `Slope=±1.0` (45°) or `DeltaZ=...`.
  - For complex passes: define an elevation “zone” between two named nodes with a target ΔZ or slope and the solver distributes Z accordingly.

- Twin handling
  - Solve centerline Z only; keep your twin ZUp/ZLow offsets as you do now. Elevation adds to both equally.

- Diagnostics
  - Report conflicts (unsatisfied equalities), overconstrained loops, and violated soft constraints.
  - Output a small CSV of node Z and edge slopes for QA.

### Minimal staged delivery
1) Data contract: property-set fields + optional CSV schema for constraints; pick a simple set first: FixZ, KeepLevel, Slope, DeltaZ, Vertical45Pair.
2) Rooted traversal solver for trees; conflict detection.
3) Upgrade to LS/QP solver for multiple anchors and loops; add smoothing objective.
4) Bend plane handling: compute 3D PT from pre/post directions, including vertical elbows.

### Questions
1) How do you want to annotate “vertical 45” intent: property set on elbows, on the straight between them, or CSV? Please specify the exact fields you prefer.  
2) Do you have reliable Z anchors (e.g., tie-in elevations) at one or more endpoints, or should we allow the root Z to be specified ad hoc at export time?  
3) Should “default behavior” be level pipes unless annotated, or apply a default longitudinal slope (e.g., 0.2%) along mains?  
4) Do you need min cover/clearance constraints now (requiring Civil 3D surfaces/utility layers), or keep that out of scope initially?  
5) For branches, if parent slope is known but the branch is unannotated, should the branch inherit the parent’s Z at the junction and go level, or inherit the parent’s slope until next anchor?  
6) How should conflicts be resolved: stop with an error, or prefer a priority order (FixZ > DeltaZ > Slope > KeepLevel) and continue?  
7) Are there looped networks we must support now, or can we assume tree-like graphs in the first iteration?  
8) Do you want an on-screen review (palette/table) of computed Z and segment slopes before writing NTR?

- Elevation plan
  - Add a constraint layer on `TNode` Z and edge relations.
  - Start with rooted traversal; evolve to least-squares/QP.
  - Require explicit annotation for “vertical 45” and passes.
  - Integrate post-topology, pre-routing; NTR stays as-is.
  - Provide diagnostics and optional CSV summary.

---

**User**

write the design proposal and questions to a .md file in the project, I need to have it open besides to write a detailed answer

---

**Cursor**

You’re in ask mode, so I can’t create files directly. Save the following as `Acad-C3D-Tools/NTRExport/Docs/ElevationTopologyDesign.md`. Questions are numbered per your preference [[memory:8833499]].

```markdown
## Elevation Topology for NTR Export — Design Proposal

### Purpose
- Add a robust elevation (Z) solution layer on top of the existing 2D pipeline topology so exported NTR geometry reflects real-world slopes, vertical passes, and elevation steps.
- Keep the current DWG → Topology → Routing → NTR flow intact; insert elevation solving between topology build and routing.

### Scope (initial)
- Tree-like networks; loops may be flagged or handled with a solver in Phase 2.
- Node-based elevations for all `TNode`s; edge-based slope/ΔZ constraints applied across pipes/fittings.
- Support explicit “vertical 45°” segments (paired elbows, or annotated straight).
- Twin handling remains via existing Z-offset duplication; elevation applies to the centerline.

### High-Level Approach
- Represent each topology node `n ∈ Nodes` with a variable `Z(n)`.
- Represent constraints on edges `(u,v)` or nodes:
  - Keep-level: `Z(v) − Z(u) = 0`
  - Prescribed slope: `Z(v) − Z(u) = s · Luv` (s in m/m; Luv is plan length in meters)
  - Prescribed step: `Z(v) − Z(u) = ΔZ`
  - Vertical 45-section: `Z(v) − Z(u) = ± Luv` (i.e., slope = ±1.0 for 45°, or a specific ΔZ if given)
  - Optional min-cover/clearance constraints as inequalities (Phase 2)
- Solve Z before routing; routing and NTR emission use updated node Z.

### Constraint Inputs (layered)
- Property set fields on CAD objects (preferred, non-invasive to UI):
  - On pipes (Polyline):
    - `ElevSlope` (double, m/m; positive = rising from start→end)
    - `ElevDeltaZ` (double, m)
    - `ElevKeepLevel` (bool)
    - `ElevZoneId` (string) for grouping sections
  - On fittings (BlockReference, elbows/tees/reducers):
    - `ElbowPlane` (enum: Horizontal | Vertical)
    - `VerticalPairId` (string) to pair elbows forming a vertical 45/“underpass”
    - `ElevDeltaZ` (double, optional)
- Optional CSV (fast batch control; friendly with Excel):
  - Each row defines a constraint for a node or edge by handle or pair id.

Example CSV schema (UTF-8, header row required):
```csv
TargetType,TargetRefA,TargetRefB,Constraint,Value,Unit,Notes
Node,NodeName=N001,,FixZ,12.300,m,Tie-in
Edge,Handle=12AB,Handle=34CD,Slope,0.005,m/m,Longitudinal slope
Edge,Handle=AA11,Handle=BB22,DeltaZ,-0.600,m,Drop to avoid crossing
Edge,PairId=V123,,Vertical45Pair,,,
```

Resolution priority (when multiple inputs present in the same segment):
1) Property-set on CAD
2) CSV
3) Heuristics (only as a last resort; Phase 2+)

### Solving Strategy

#### Phase 1: Rooted traversal (deterministic)
- Inputs: a root node (e.g., supply connection) with `FixZ` or default `Z=0`.
- Depth-first (or BFS) propagation:
  - Apply edge-local constraints in order of strength: FixZ at target node > DeltaZ > Slope > KeepLevel.
  - On conflicts (reaching a node with inconsistent Z), flag and stop; report path and conflicting constraints.
- Branch policy (default):
  - Inherit parent Z at the junction.
  - Default branch behavior: keep level unless annotated (configurable).

Pros: simple, fast, works on trees. Cons: brittle with multiple anchors/loops.

#### Phase 2: Least-squares / QP (robust, optional)
- Build a sparse system over all `Z(n)`:
  - Hard equalities as high-weight rows (or exact constraints).
  - Soft constraints (preferences) with lower weights (e.g., smoothness, default-level).
  - Inequalities for min cover/clearance if enabled.
- Objective (examples):
  - Minimize sum of squared deviations from desired slopes/ΔZ.
  - Smoothing term to reduce slope variance along pipelines.
- Solve via standard LSQR/Cholesky (equalities) or QP (with inequalities).
- Produces globally consistent Z with diagnostics on infeasible sets.

### Vertical Passes and 45° Elbows
- In 2D, vertical intent is ambiguous; require annotation:
  - `ElbowPlane=Vertical` on the elbow blocks, with shared `VerticalPairId` to bind down/up elbows.
  - Optionally `ElevDeltaZ` on either elbow or on the connecting straight.
- Solver enforces:
  - For a vertical-45 straight: slope = ±1.0 or `ΔZ` if provided.
  - For paired elbows: distribute ΔZ across the intermediate path or constrain just the designated straight(s), per configuration.

### Integration Points (no code here, architectural placement)
- After:
  - `TopologyBuilder.Build(...)`
  - `TopologySoilPlanner.Apply(...)`
- Do:
  - `ElevationSolver.Solve(topo, constraints)`
  - Write solved `Z` into each `TNode.Pos` (centerline).
- Then:
  - `Router.Route(...)` uses new 3D endpoints.
  - Existing `RoutedStraight`, `RoutedBend` use `Point3d` with Z.
  - Existing twin offset logic adds ±Z offset after centerline elevation.
  - `NtrFormat.Pt` scales to mm; unchanged.

### Twin Handling
- Solve Z for centerline once.
- Keep twin Z offsets (±(OD+gap)/2) as-is; they are added to the solved centerline Z.

### Diagnostics and QA
- Conflict report with:
  - Node, incoming paths, constraints involved, suggested resolution.
- CSV export of results:
  - Node Z table (NodeName, X, Y, Z)
  - Edge slopes (From, To, Length, Slope, Effective ΔZ, Sources)
- Optional visual QA:
  - Temporary debug geometry or a palette grid showing computed Z and constraints.

### Backward Compatibility and Controls
- Config toggle: `EnableElevationSolver` (default off for first rollout).
- If disabled, behavior equals current export (Z=0; only twin offsets).
- If enabled with insufficient data, either:
  - Fail fast with diagnostics; or
  - Fallback to defaults (level) and warn (configurable policy).

### Performance
- Phase 1: O(N+E) traversal; negligible overhead on typical drawings.
- Phase 2: Sparse LS/QP; still fast for thousands of nodes (single-digit ms to low hundreds of ms).

### Risks and Mitigations
- Ambiguity without annotations → require minimal data: root FixZ and explicit vertical-45 markers.
- Inconsistent constraints → early detection and user-facing error.
- Loops → push to Phase 2 or cut at configured breaker nodes.

### Staged Delivery
- Milestone 1:
  - Property/CSV constraint intake; single-root traversal; level-by-default; vertical 45 with explicit annotations.
- Milestone 2:
  - Multiple anchors; least-squares solver; smoothing objective; better diagnostics.
- Milestone 3:
  - Inequalities for min cover/clearance (if surface/utility context is available); looped networks.

### Acceptance Criteria
- Given a tree with a root FixZ and a mix of Slope/DeltaZ/KeepLevel:
  - The computed Z matches all constraints within tolerance.
  - Branches default level unless annotated (or per configured policy).
- Vertical 45 pass with paired elbows:
  - Intermediate segment has slope ±1.0 (or target ΔZ is achieved), bends emit correct 3D PT.
- NTR output:
  - RO/BOG/RED/TEE coordinates include Z; DN/IS/LAST unaffected; REF semantics unchanged.
- Diagnostics present on conflicts and missing anchors when required.

### Proposed Property Set Fields
- On Polyline:
  - `ElevSlope` (double, m/m)
  - `ElevDeltaZ` (double, m)
  - `ElevKeepLevel` (bool)
  - `ElevZoneId` (string)
- On BlockReference:
  - `ElbowPlane` (Horizontal|Vertical)
  - `VerticalPairId` (string)
  - `ElevDeltaZ` (double, m)

### Proposed CSV Columns (edge- or node-level)
- `TargetType` = Node|Edge
- `TargetRefA` = NodeName=N### | Handle=XXXX (required)
- `TargetRefB` = NodeName=... | Handle=... (for edges)
- `Constraint` = FixZ|Slope|DeltaZ|KeepLevel|Vertical45Pair
- `Value` = numeric (if applicable)
- `Unit` = m|m/m
- `Notes` = free text
- Optional: `Priority` integer to break ties if both CSV and property-set exist.

### Open Questions (please answer with numbers)
1) Where should “vertical 45” intent be annotated: on the elbow blocks (`ElbowPlane`, `VerticalPairId`), on the connecting straight, or via CSV?  
2) Do you have reliable Z anchors (e.g., supply tie-in elevations) at one or more endpoints? If multiple anchors exist, should we enable Phase 2 in v1?  
3) Default behavior: should unannotated pipes be level, or apply a default longitudinal slope (e.g., 0.2%) on mains?  
4) Branch policy: if parent has a slope, should unannotated branches be level from the junction, or inherit the parent’s slope until their next anchor?  
5) Conflict policy: stop with an error, or apply a precedence order (FixZ > DeltaZ > Slope > KeepLevel) and continue with warnings?  
6) Are looped networks common now, or can v1 assume tree-like topology?  
7) Do you want a config toggle (`EnableElevationSolver`) and a “strict vs. permissive” mode for missing annotations?  
8) Should we include min cover/clearance constraints now (requires surface/utility data), or defer to a later milestone?  
9) For vertical passes: is the target ΔZ usually specified, or should we compute it from a utility crossing elevation database (future)?  
10) What output QA do you prefer: CSV dump, palette grid, and/or temporary debug markers in DWG?  
11) Any existing property set schema we must align with (names/types), or may we introduce the fields listed above?  
12) Priority order for the milestones (which scenarios to deliver first)?

```

- Save to: `Acad-C3D-Tools/NTRExport/Docs/ElevationTopologyDesign.md`.
- Want me to also add a brief TODO checklist in `TESTING.md` for console scenarios?

```markdown
## Elevation Topology for NTR Export — Design Proposal

### Purpose
- Add a robust elevation (Z) solution layer on top of the existing 2D pipeline topology so exported NTR geometry reflects real-world slopes, vertical passes, and elevation steps.
- Keep the current DWG → Topology → Routing → NTR flow intact; insert elevation solving between topology build and routing.

### Scope (initial)
- Tree-like networks; loops may be flagged or handled with a solver in Phase 2.
- Node-based elevations for all `TNode`s; edge-based slope/ΔZ constraints applied across pipes/fittings.
- Support explicit “vertical 45°” segments (paired elbows, or annotated straight).
- Twin handling remains via existing Z-offset duplication; elevation applies to the centerline.

### High-Level Approach
- Represent each topology node `n ∈ Nodes` with a variable `Z(n)`.
- Represent constraints on edges `(u,v)` or nodes:
  - Keep-level: `Z(v) − Z(u) = 0`
  - Prescribed slope: `Z(v) − Z(u) = s · Luv` (s in m/m; Luv is plan length in meters)
  - Prescribed step: `Z(v) − Z(u) = ΔZ`
  - Vertical 45-section: `Z(v) − Z(u) = ± Luv` (i.e., slope = ±1.0 for 45°, or a specific ΔZ if given)
  - Optional min-cover/clearance constraints as inequalities (Phase 2)
- Solve Z before routing; routing and NTR emission use updated node Z.

### Constraint Inputs (layered)
- Property set fields on CAD objects (preferred, non-invasive to UI):
  - On pipes (Polyline):
    - `ElevSlope` (double, m/m; positive = rising from start→end)
    - `ElevDeltaZ` (double, m)
    - `ElevKeepLevel` (bool)
    - `ElevZoneId` (string) for grouping sections
  - On fittings (BlockReference, elbows/tees/reducers):
    - `ElbowPlane` (enum: Horizontal | Vertical)
    - `VerticalPairId` (string) to pair elbows forming a vertical 45/“underpass”
    - `ElevDeltaZ` (double, optional)
- Optional CSV (fast batch control; friendly with Excel):
  - Each row defines a constraint for a node or edge by handle or pair id.

Example CSV schema (UTF-8, header row required):
TargetType,TargetRefA,TargetRefB,Constraint,Value,Unit,Notes
Node,NodeName=N001,,FixZ,12.300,m,Tie-in
Edge,Handle=12AB,Handle=34CD,Slope,0.005,m/m,Longitudinal slope
Edge,Handle=AA11,Handle=BB22,DeltaZ,-0.600,m,Drop to avoid crossing
Edge,PairId=V123,,Vertical45Pair,,,Resolution priority (when multiple inputs present in the same segment):
1) Property-set on CAD
2) CSV
3) Heuristics (only as a last resort; Phase 2+)

### Solving Strategy

#### Phase 1: Rooted traversal (deterministic)
- Inputs: a root node (e.g., supply connection) with `FixZ` or default `Z=0`.
- Depth-first (or BFS) propagation:
  - Apply edge-local constraints in order of strength: FixZ at target node > DeltaZ > Slope > KeepLevel.
  - On conflicts (reaching a node with inconsistent Z), flag and stop; report path and conflicting constraints.
- Branch policy (default):
  - Inherit parent Z at the junction.
  - Default branch behavior: keep level unless annotated (configurable).

Pros: simple, fast, works on trees. Cons: brittle with multiple anchors/loops.

#### Phase 2: Least-squares / QP (robust, optional)
- Build a sparse system over all `Z(n)`:
  - Hard equalities as high-weight rows (or exact constraints).
  - Soft constraints (preferences) with lower weights (e.g., smoothness, default-level).
  - Inequalities for min cover/clearance if enabled.
- Objective (examples):
  - Minimize sum of squared deviations from desired slopes/ΔZ.
  - Smoothing term to reduce slope variance along pipelines.
- Solve via standard LSQR/Cholesky (equalities) or QP (with inequalities).
- Produces globally consistent Z with diagnostics on infeasible sets.

### Vertical Passes and 45° Elbows
- In 2D, vertical intent is ambiguous; require annotation:
  - `ElbowPlane=Vertical` on the elbow blocks, with shared `VerticalPairId` to bind down/up elbows.
  - Optionally `ElevDeltaZ` on either elbow or on the connecting straight.
- Solver enforces:
  - For a vertical-45 straight: slope = ±1.0 or `ΔZ` if provided.
  - For paired elbows: distribute ΔZ across the intermediate path or constrain just the designated straight(s), per configuration.

### Integration Points (no code here, architectural placement)
- After:
  - `TopologyBuilder.Build(...)`
  - `TopologySoilPlanner.Apply(...)`
- Do:
  - `ElevationSolver.Solve(topo, constraints)`
  - Write solved `Z` into each `TNode.Pos` (centerline).
- Then:
  - `Router.Route(...)` uses new 3D endpoints.
  - Existing `RoutedStraight`, `RoutedBend` use `Point3d` with Z.
  - Existing twin offset logic adds ±Z offset after centerline elevation.
  - `NtrFormat.Pt` scales to mm; unchanged.

### Twin Handling
- Solve Z for centerline once.
- Keep twin Z offsets (±(OD+gap)/2) as-is; they are added to the solved centerline Z.

### Diagnostics and QA
- Conflict report with:
  - Node, incoming paths, constraints involved, suggested resolution.
- CSV export of results:
  - Node Z table (NodeName, X, Y, Z)
  - Edge slopes (From, To, Length, Slope, Effective ΔZ, Sources)
- Optional visual QA:
  - Temporary debug geometry or a palette grid showing computed Z and constraints.

### Backward Compatibility and Controls
- Config toggle: `EnableElevationSolver` (default off for first rollout).
- If disabled, behavior equals current export (Z=0; only twin offsets).
- If enabled with insufficient data, either:
  - Fail fast with diagnostics; or
  - Fallback to defaults (level) and warn (configurable policy).

### Performance
- Phase 1: O(N+E) traversal; negligible overhead on typical drawings.
- Phase 2: Sparse LS/QP; still fast for thousands of nodes (single-digit ms to low hundreds of ms).

### Risks and Mitigations
- Ambiguity without annotations → require minimal data: root FixZ and explicit vertical-45 markers.
- Inconsistent constraints → early detection and user-facing error.
- Loops → push to Phase 2 or cut at configured breaker nodes.

### Staged Delivery
- Milestone 1:
  - Property/CSV constraint intake; single-root traversal; level-by-default; vertical 45 with explicit annotations.
- Milestone 2:
  - Multiple anchors; least-squares solver; smoothing objective; better diagnostics.
- Milestone 3:
  - Inequalities for min cover/clearance (if surface/utility context is available); looped networks.

### Acceptance Criteria
- Given a tree with a root FixZ and a mix of Slope/DeltaZ/KeepLevel:
  - The computed Z matches all constraints within tolerance.
  - Branches default level unless annotated (or per configured policy).
- Vertical 45 pass with paired elbows:
  - Intermediate segment has slope ±1.0 (or target ΔZ is achieved), bends emit correct 3D PT.
- NTR output:
  - RO/BOG/RED/TEE coordinates include Z; DN/IS/LAST unaffected; REF semantics unchanged.
- Diagnostics present on conflicts and missing anchors when required.

### Proposed Property Set Fields
- On Polyline:
  - `ElevSlope` (double, m/m)
  - `ElevDeltaZ` (double, m)
  - `ElevKeepLevel` (bool)
  - `ElevZoneId` (string)
- On BlockReference:
  - `ElbowPlane` (Horizontal|Vertical)
  - `VerticalPairId` (string)
  - `ElevDeltaZ` (double, m)

### Proposed CSV Columns (edge- or node-level)
- `TargetType` = Node|Edge
- `TargetRefA` = NodeName=N### | Handle=XXXX (required)
- `TargetRefB` = NodeName=... | Handle=... (for edges)
- `Constraint` = FixZ|Slope|DeltaZ|KeepLevel|Vertical45Pair
- `Value` = numeric (if applicable)
- `Unit` = m|m/m
- `Notes` = free text
- Optional: `Priority` integer to break ties if both CSV and property-set exist.

### Open Questions (please answer with numbers)
1) Where should “vertical 45” intent be annotated: on the elbow blocks (`ElbowPlane`, `VerticalPairId`), on the connecting straight, or via CSV?  
2) Do you have reliable Z anchors (e.g., supply tie-in elevations) at one or more endpoints? If multiple anchors exist, should we enable Phase 2 in v1?  
3) Default behavior: should unannotated pipes be level, or apply a default longitudinal slope (e.g., 0.2%) on mains?  
4) Branch policy: if parent has a slope, should unannotated branches be level from the junction, or inherit the parent’s slope until their next anchor?  
5) Conflict policy: stop with an error, or apply a precedence order (FixZ > DeltaZ > Slope > KeepLevel) and continue with warnings?  
6) Are looped networks common now, or can v1 assume tree-like topology?  
7) Do you want a config toggle (`EnableElevationSolver`) and a “strict vs. permissive” mode for missing annotations?  
8) Should we include min cover/clearance constraints now (requires surface/utility data), or defer to a later milestone?  
9) For vertical passes: is the target ΔZ usually specified, or should we compute it from a utility crossing elevation database (future)?  
10) What output QA do you prefer: CSV dump, palette grid, and/or temporary debug markers in DWG?  
11) Any existing property set schema we must align with (names/types), or may we introduce the fields listed above?  
12) Priority order for the milestones (which scenarios to deliver first)?

```

```plaintext

- Save to: `Acad-C3D-Tools/NTRExport/Docs/ElevationTopologyDesign.md`.
- Want me to also add a brief TODO checklist in `TESTING.md` for console scenarios?
```

---

**User**

do it again in agent mode

---

**Cursor**

I'll create a new Markdown document with the design proposal and numbered questions, saved as `Acad-C3D-Tools/NTRExport/Docs/ElevationTopologyDesign.md`.

```markdown
## Elevation Topology for NTR Export — Design Proposal

### Purpose
- Add a robust elevation (Z) solution layer on top of the existing 2D pipeline topology so exported NTR geometry reflects real-world slopes, vertical passes, and elevation steps.
- Keep the current DWG → Topology → Routing → NTR flow intact; insert elevation solving between topology build and routing.

### Scope (initial)
- Tree-like networks; loops may be flagged or handled with a solver in Phase 2.
- Node-based elevations for all `TNode`s; edge-based slope/ΔZ constraints applied across pipes/fittings.
- Support explicit “vertical 45°” segments (paired elbows, or annotated straight).
- Twin handling remains via existing Z-offset duplication; elevation applies to the centerline.

### High-Level Approach
- Represent each topology node `n ∈ Nodes` with a variable `Z(n)`.
- Represent constraints on edges `(u,v)` or nodes:
  - Keep-level: `Z(v) − Z(u) = 0`
  - Prescribed slope: `Z(v) − Z(u) = s · Luv` (s in m/m; Luv is plan length in meters)
  - Prescribed step: `Z(v) − Z(u) = ΔZ`
  - Vertical 45-section: `Z(v) − Z(u) = ± Luv` (i.e., slope = ±1.0 for 45°, or a specific ΔZ if given)
  - Optional min-cover/clearance constraints as inequalities (Phase 2)
- Solve Z before routing; routing and NTR emission use updated node Z.

### Constraint Inputs (layered)
- Property set fields on CAD objects (preferred, non-invasive to UI):
  - On pipes (Polyline):
    - `ElevSlope` (double, m/m; positive = rising from start→end)
    - `ElevDeltaZ` (double, m)
    - `ElevKeepLevel` (bool)
    - `ElevZoneId` (string) for grouping sections
  - On fittings (BlockReference, elbows/tees/reducers):
    - `ElbowPlane` (enum: Horizontal | Vertical)
    - `VerticalPairId` (string) to pair elbows forming a vertical 45/“underpass”
    - `ElevDeltaZ` (double, optional)
- Optional CSV (fast batch control; friendly with Excel):
  - Each row defines a constraint for a node or edge by handle or pair id.

Example CSV schema (UTF-8, header row required):

```csv
TargetType,TargetRefA,TargetRefB,Constraint,Value,Unit,Notes
Node,NodeName=N001,,FixZ,12.300,m,Tie-in
Edge,Handle=12AB,Handle=34CD,Slope,0.005,m/m,Longitudinal slope
Edge,Handle=AA11,Handle=BB22,DeltaZ,-0.600,m,Drop to avoid crossing
Edge,PairId=V123,,Vertical45Pair,,,
```

Resolution priority (when multiple inputs present in the same segment):
1) Property-set on CAD
2) CSV
3) Heuristics (only as a last resort; Phase 2+)

### Solving Strategy

#### Phase 1: Rooted traversal (deterministic)
- Inputs: a root node (e.g., supply connection) with `FixZ` or default `Z=0`.
- Depth-first (or BFS) propagation:
  - Apply edge-local constraints in order of strength: FixZ at target node > DeltaZ > Slope > KeepLevel.
  - On conflicts (reaching a node with inconsistent Z), flag and stop; report path and conflicting constraints.
- Branch policy (default):
  - Inherit parent Z at the junction.
  - Default branch behavior: keep level unless annotated (configurable).

Pros: simple, fast, works on trees. Cons: brittle with multiple anchors/loops.

#### Phase 2: Least-squares / QP (robust, optional)
- Build a sparse system over all `Z(n)`:
  - Hard equalities as high-weight rows (or exact constraints).
  - Soft constraints (preferences) with lower weights (e.g., smoothness, default-level).
  - Inequalities for min cover/clearance if enabled.
- Objective (examples):
  - Minimize sum of squared deviations from desired slopes/ΔZ.
  - Smoothing term to reduce slope variance along pipelines.
- Solve via standard LSQR/Cholesky (equalities) or QP (with inequalities).
- Produces globally consistent Z with diagnostics on infeasible sets.

### Vertical Passes and 45° Elbows
- In 2D, vertical intent is ambiguous; require annotation:
  - `ElbowPlane=Vertical` on the elbow blocks, with shared `VerticalPairId` to bind down/up elbows.
  - Optionally `ElevDeltaZ` on either elbow or on the connecting straight.
- Solver enforces:
  - For a vertical-45 straight: slope = ±1.0 or `ΔZ` if provided.
  - For paired elbows: distribute ΔZ across the intermediate path or constrain just the designated straight(s), per configuration.

### Integration Points (architectural placement)
- After:
  - `TopologyBuilder.Build(...)`
  - `TopologySoilPlanner.Apply(...)`
- Do:
  - `ElevationSolver.Solve(topo, constraints)`
  - Write solved `Z` into each `TNode.Pos` (centerline).
- Then:
  - `Router.Route(...)` uses new 3D endpoints.
  - Existing `RoutedStraight`, `RoutedBend` use `Point3d` with Z.
  - Existing twin offset logic adds ±Z offset after centerline elevation.
  - `NtrFormat.Pt` scales to mm; unchanged.

### Twin Handling
- Solve Z for centerline once.
- Keep twin Z offsets (±(OD+gap)/2) as-is; they are added to the solved centerline Z.

### Diagnostics and QA
- Conflict report with:
  - Node, incoming paths, constraints involved, suggested resolution.
- CSV export of results:
  - Node Z table (NodeName, X, Y, Z)
  - Edge slopes (From, To, Length, Slope, Effective ΔZ, Sources)
- Optional visual QA:
  - Temporary debug geometry or a palette grid showing computed Z and constraints.

### Backward Compatibility and Controls
- Config toggle: `EnableElevationSolver` (default off for first rollout).
- If disabled, behavior equals current export (Z=0; only twin offsets).
- If enabled with insufficient data, either:
  - Fail fast with diagnostics; or
  - Fallback to defaults (level) and warn (configurable policy).

### Performance
- Phase 1: O(N+E) traversal; negligible overhead on typical drawings.
- Phase 2: Sparse LS/QP; still fast for thousands of nodes (single-digit ms to low hundreds of ms).

### Risks and Mitigations
- Ambiguity without annotations → require minimal data: root FixZ and explicit vertical-45 markers.
- Inconsistent constraints → early detection and user-facing error.
- Loops → push to Phase 2 or cut at configured breaker nodes.

### Staged Delivery
- Milestone 1:
  - Property/CSV constraint intake; single-root traversal; level-by-default; vertical 45 with explicit annotations.
- Milestone 2:
  - Multiple anchors; least-squares solver; smoothing objective; better diagnostics.
- Milestone 3:
  - Inequalities for min cover/clearance (if surface/utility context is available); looped networks.

### Acceptance Criteria
- Given a tree with a root FixZ and a mix of Slope/DeltaZ/KeepLevel:
  - The computed Z matches all constraints within tolerance.
  - Branches default level unless annotated (or per configured policy).
- Vertical 45 pass with paired elbows:
  - Intermediate segment has slope ±1.0 (or target ΔZ is achieved), bends emit correct 3D PT.
- NTR output:
  - RO/BOG/RED/TEE coordinates include Z; DN/IS/LAST unaffected; REF semantics unchanged.
- Diagnostics present on conflicts and missing anchors when required.

### Proposed Property Set Fields
- On Polyline:
  - `ElevSlope` (double, m/m)
  - `ElevDeltaZ` (double, m)
  - `ElevKeepLevel` (bool)
  - `ElevZoneId` (string)
- On BlockReference:
  - `ElbowPlane` (Horizontal|Vertical)
  - `VerticalPairId` (string)
  - `ElevDeltaZ` (double, m)

### Proposed CSV Columns (edge- or node-level)
- `TargetType` = Node|Edge
- `TargetRefA` = NodeName=N### | Handle=XXXX (required)
- `TargetRefB` = NodeName=... | Handle=... (for edges)
- `Constraint` = FixZ|Slope|DeltaZ|KeepLevel|Vertical45Pair
- `Value` = numeric (if applicable)
- `Unit` = m|m/m
- `Notes` = free text
- Optional: `Priority` integer to break ties if both CSV and property-set exist.

### Open Questions (please answer with numbers)
1) Where should “vertical 45” intent be annotated: on the elbow blocks (`ElbowPlane`, `VerticalPairId`), on the connecting straight, or via CSV?
2) Do you have reliable Z anchors (e.g., supply tie-in elevations) at one or more endpoints? If multiple anchors exist, should we enable Phase 2 in v1?
3) Default behavior: should unannotated pipes be level, or apply a default longitudinal slope (e.g., 0.2%) on mains?
4) Branch policy: if parent has a slope, should unannotated branches be level from the junction, or inherit the parent’s slope until their next anchor?
5) Conflict policy: stop with an error, or apply a precedence order (FixZ > DeltaZ > Slope > KeepLevel) and continue with warnings?
6) Are looped networks common now, or can v1 assume tree-like topology?
7) Do you want a config toggle (`EnableElevationSolver`) and a “strict vs. permissive” mode for missing annotations?
8) Should we include min cover/clearance constraints now (requires surface/utility data), or defer to a later milestone?
9) For vertical passes: is the target ΔZ usually specified, or should we compute it from a utility crossing elevation database (future)?
10) What output QA do you prefer: CSV dump, palette grid, and/or temporary debug markers in DWG?
11) Any existing property set schema we must align with (names/types), or may we introduce the fields listed above?
12) Priority order for the milestones (which scenarios to deliver first)?


```

I created `Acad-C3D-Tools/NTRExport/Docs/ElevationTopologyDesign.md` with the proposal and 12 numbered questions for your review [[memory:8833499]].

- New doc added under `NTRExport/Docs` with the full design, staged delivery, and numbered questions you can answer inline.

---

**User**

can you do numbered paragraphs and bullet points in this .md? I need to reference each bullet point exactly without repeating the whole stuff every time

---

**Cursor**

I'll reformat the Markdown with numbered headings and sub-items so each bullet can be referenced uniquely, then save it to the same file.

I'll reformat the Markdown file with numbered sections and sub-items for precise referencing, then save the changes.

```markdown
## Elevation Topology for NTR Export — Design Proposal

### 1. Purpose
- 1.1 Add a robust elevation (Z) solution layer on top of the existing 2D pipeline topology so exported NTR geometry reflects real-world slopes, vertical passes, and elevation steps.
- 1.2 Keep the current DWG → Topology → Routing → NTR flow intact; insert elevation solving between topology build and routing.

### 2. Scope (initial)
- 2.1 Tree-like networks; loops may be flagged or handled with a solver in Phase 2.
- 2.2 Node-based elevations for all `TNode`s; edge-based slope/ΔZ constraints applied across pipes/fittings.
- 2.3 Support explicit “vertical 45°” segments (paired elbows, or annotated straight).
- 2.4 Twin handling remains via existing Z-offset duplication; elevation applies to the centerline.

### 3. High-Level Approach
- 3.1 Represent each topology node `n ∈ Nodes` with a variable `Z(n)`.
- 3.2 Represent constraints on edges `(u,v)` or nodes:
  - 3.2.1 Keep-level: `Z(v) − Z(u) = 0`
  - 3.2.2 Prescribed slope: `Z(v) − Z(u) = s · Luv` (s in m/m; Luv is plan length in meters)
  - 3.2.3 Prescribed step: `Z(v) − Z(u) = ΔZ`
  - 3.2.4 Vertical 45-section: `Z(v) − Z(u) = ± Luv` (slope = ±1.0 for 45°) or specific `ΔZ`
  - 3.2.5 Optional min-cover/clearance constraints as inequalities (Phase 2)
- 3.3 Solve Z before routing; routing and NTR emission use updated node Z.

### 4. Constraint Inputs (layered)
- 4.1 Property set fields on CAD objects (preferred, non-invasive to UI):
  - 4.1.1 On pipes (Polyline):
    - 4.1.1.1 `ElevSlope` (double, m/m; positive = rising from start→end)
    - 4.1.1.2 `ElevDeltaZ` (double, m)
    - 4.1.1.3 `ElevKeepLevel` (bool)
    - 4.1.1.4 `ElevZoneId` (string) for grouping sections
  - 4.1.2 On fittings (BlockReference, elbows/tees/reducers):
    - 4.1.2.1 `ElbowPlane` (enum: Horizontal | Vertical)
    - 4.1.2.2 `VerticalPairId` (string) to pair elbows forming a vertical 45/“underpass”
    - 4.1.2.3 `ElevDeltaZ` (double, optional)
- 4.2 Optional CSV (fast batch control; friendly with Excel): each row defines a constraint for a node or edge by handle or pair id.
- 4.3 Example CSV schema (UTF-8, header row required):

```csv
TargetType,TargetRefA,TargetRefB,Constraint,Value,Unit,Notes
Node,NodeName=N001,,FixZ,12.300,m,Tie-in
Edge,Handle=12AB,Handle=34CD,Slope,0.005,m/m,Longitudinal slope
Edge,Handle=AA11,Handle=BB22,DeltaZ,-0.600,m,Drop to avoid crossing
Edge,PairId=V123,,Vertical45Pair,,,
```

- 4.4 Resolution priority (when multiple inputs present in the same segment):
  - 4.4.1 Property-set on CAD
  - 4.4.2 CSV
  - 4.4.3 Heuristics (only as a last resort; Phase 2+)

### 5. Solving Strategy

#### 5.1 Phase 1: Rooted traversal (deterministic)
- 5.1.1 Inputs: a root node (e.g., supply connection) with `FixZ` or default `Z=0`.
- 5.1.2 Depth-first (or BFS) propagation:
  - 5.1.2.1 Apply edge-local constraints in order of strength: FixZ at target node > DeltaZ > Slope > KeepLevel.
  - 5.1.2.2 On conflicts (reaching a node with inconsistent Z), flag and stop; report path and conflicting constraints.
- 5.1.3 Branch policy (default):
  - 5.1.3.1 Inherit parent Z at the junction.
  - 5.1.3.2 Default branch behavior: keep level unless annotated (configurable).

- 5.1.4 Pros/Cons: simple, fast, works on trees; brittle with multiple anchors/loops.

#### 5.2 Phase 2: Least-squares / QP (robust, optional)
- 5.2.1 Build a sparse system over all `Z(n)`:
  - 5.2.1.1 Hard equalities as high-weight rows (or exact constraints).
  - 5.2.1.2 Soft constraints (preferences) with lower weights (e.g., smoothness, default-level).
  - 5.2.1.3 Inequalities for min cover/clearance if enabled.
- 5.2.2 Objective (examples):
  - 5.2.2.1 Minimize sum of squared deviations from desired slopes/ΔZ.
  - 5.2.2.2 Smoothing term to reduce slope variance along pipelines.
- 5.2.3 Solve via standard LSQR/Cholesky (equalities) or QP (with inequalities) and produce diagnostics on infeasible sets.

### 6. Vertical Passes and 45° Elbows
- 6.1 In 2D, vertical intent is ambiguous; require annotation:
  - 6.1.1 `ElbowPlane=Vertical` on the elbow blocks, with shared `VerticalPairId` to bind down/up elbows.
  - 6.1.2 Optionally `ElevDeltaZ` on either elbow or on the connecting straight.
- 6.2 Solver enforces:
  - 6.2.1 For a vertical-45 straight: slope = ±1.0 or `ΔZ` if provided.
  - 6.2.2 For paired elbows: distribute `ΔZ` across the intermediate path or constrain just the designated straight(s), per configuration.

### 7. Integration Points (architectural placement)
- 7.1 After:
  - 7.1.1 `TopologyBuilder.Build(...)`
  - 7.1.2 `TopologySoilPlanner.Apply(...)`
- 7.2 Do:
  - 7.2.1 `ElevationSolver.Solve(topo, constraints)`
  - 7.2.2 Write solved `Z` into each `TNode.Pos` (centerline).
- 7.3 Then:
  - 7.3.1 `Router.Route(...)` uses new 3D endpoints.
  - 7.3.2 Existing `RoutedStraight`, `RoutedBend` use `Point3d` with Z.
  - 7.3.3 Existing twin offset logic adds ±Z offset after centerline elevation.
  - 7.3.4 `NtrFormat.Pt` scales to mm; unchanged.

### 8. Twin Handling
- 8.1 Solve Z for centerline once.
- 8.2 Keep twin Z offsets (±(OD+gap)/2) as-is; they are added to the solved centerline Z.

### 9. Diagnostics and QA
- 9.1 Conflict report with: node, incoming paths, constraints involved, suggested resolution.
- 9.2 CSV export of results:
  - 9.2.1 Node Z table (NodeName, X, Y, Z)
  - 9.2.2 Edge slopes (From, To, Length, Slope, Effective ΔZ, Sources)
- 9.3 Optional visual QA: temporary debug geometry or a palette grid showing computed Z and constraints.

### 10. Backward Compatibility and Controls
- 10.1 Config toggle: `EnableElevationSolver` (default off for first rollout).
- 10.2 If disabled, behavior equals current export (Z=0; only twin offsets).
- 10.3 If enabled with insufficient data, either fail fast with diagnostics or fallback to defaults (level) with warnings (configurable policy).

### 11. Performance
- 11.1 Phase 1: O(N+E) traversal; negligible overhead on typical drawings.
- 11.2 Phase 2: Sparse LS/QP; still fast for thousands of nodes (single-digit ms to low hundreds of ms).

### 12. Risks and Mitigations
- 12.1 Ambiguity without annotations → require minimal data: root FixZ and explicit vertical-45 markers.
- 12.2 Inconsistent constraints → early detection and user-facing error.
- 12.3 Loops → push to Phase 2 or cut at configured breaker nodes.

### 13. Staged Delivery
- 13.1 Milestone 1: Property/CSV constraint intake; single-root traversal; level-by-default; vertical 45 with explicit annotations.
- 13.2 Milestone 2: Multiple anchors; least-squares solver; smoothing objective; better diagnostics.
- 13.3 Milestone 3: Inequalities for min cover/clearance (if surface/utility context is available); looped networks.

### 14. Acceptance Criteria
- 14.1 Given a tree with a root FixZ and a mix of Slope/DeltaZ/KeepLevel: computed Z matches constraints; branches default level unless annotated (or per configured policy).
- 14.2 Vertical 45 pass with paired elbows: intermediate segment has slope ±1.0 (or target ΔZ achieved), bends emit correct 3D PT.
- 14.3 NTR output: RO/BOG/RED/TEE coordinates include Z; DN/IS/LAST unaffected; REF semantics unchanged.
- 14.4 Diagnostics present on conflicts and missing anchors when required.

### 15. Proposed Property Set Fields
- 15.1 On Polyline:
  - 15.1.1 `ElevSlope` (double, m/m)
  - 15.1.2 `ElevDeltaZ` (double, m)
  - 15.1.3 `ElevKeepLevel` (bool)
  - 15.1.4 `ElevZoneId` (string)
- 15.2 On BlockReference:
  - 15.2.1 `ElbowPlane` (Horizontal|Vertical)
  - 15.2.2 `VerticalPairId` (string)
  - 15.2.3 `ElevDeltaZ` (double, m)

### 16. Proposed CSV Columns (edge- or node-level)
- 16.1 `TargetType` = Node|Edge
- 16.2 `TargetRefA` = NodeName=N### | Handle=XXXX (required)
- 16.3 `TargetRefB` = NodeName=... | Handle=... (for edges)
- 16.4 `Constraint` = FixZ|Slope|DeltaZ|KeepLevel|Vertical45Pair
- 16.5 `Value` = numeric (if applicable)
- 16.6 `Unit` = m|m/m
- 16.7 `Notes` = free text
- 16.8 Optional: `Priority` integer to break ties if both CSV and property-set exist.

### 17. Open Questions (please answer with numbers)
- 17.1 Where should “vertical 45” intent be annotated: on the elbow blocks (`ElbowPlane`, `VerticalPairId`), on the connecting straight, or via CSV?
- 17.2 Do you have reliable Z anchors (e.g., supply tie-in elevations) at one or more endpoints? If multiple anchors exist, should we enable Phase 2 in v1?
- 17.3 Default behavior: should unannotated pipes be level, or apply a default longitudinal slope (e.g., 0.2%) on mains?
- 17.4 Branch policy: if parent has a slope, should unannotated branches be level from the junction, or inherit the parent’s slope until their next anchor?
- 17.5 Conflict policy: stop with an error, or apply a precedence order (FixZ > DeltaZ > Slope > KeepLevel) and continue with warnings?
- 17.6 Are looped networks common now, or can v1 assume tree-like topology?
- 17.7 Do you want a config toggle (`EnableElevationSolver`) and a “strict vs. permissive” mode for missing annotations?
- 17.8 Should we include min cover/clearance constraints now (requires surface/utility data), or defer to a later milestone?
- 17.9 For vertical passes: is the target ΔZ usually specified, or should we compute it from a utility crossing elevation database (future)?
- 17.10 What output QA do you prefer: CSV dump, palette grid, and/or temporary debug markers in DWG?
- 17.11 Any existing property set schema we must align with (names/types), or may we introduce the fields listed above?
- 17.12 Priority order for the milestones (which scenarios to deliver first)?


```

I updated `Acad-C3D-Tools/NTRExport/Docs/ElevationTopologyDesign.md` to use numbered sections and sub-items:
- Numbered headings (1–17).
- Numbered bullets like 3.2.1, 5.1.2.1 for precise references.
- Existing CSV example preserved.

You can now reference items unambiguously, e.g., “see 6.2.1” or “answer 17.4”.

---

**User**

General comment: I am still torn about how to go about this. Nodes are not always clear delimiters between slopes. There are elements in our project (afgrening med spring) that have elevation change within the element. In this particular example there is the main section of the pipe, but the branch is connected in the middle perpendicular in plan, but 45deg in section, then it changes to horizontal again (not the same level as main pipe) after a set distance. It is used in bonded pipe system (two steel pipes with each plastic jacket) to be able to branch off with the pipe across the neighbouring pipe. So we cannot bind the elevations to node system. The node system is also not adequate, as when routing to ntr we don't create new nodes. So nodes actually is an artifact from an AI agents' reasoning (it didn't ask me) that such a system nees a node syste. In rohr2 NTR import does not need to have nodes specified, the import will create and number nodes behind the scenes if our element entries are valid. The only reason to have nodes in NTR is if you want to have control over node numbers yourself, which I don't need. Please discuss this, but brief. Based on the system you proposed earlier, can we make it work not based on nodes but some other topology logic? I have a vague idea that we first analyze the system using network traversal and deconstruct the pipeline network into constituent base elements... I have really hard time formulating my idea. I am thinking about some kind of a plan projection that follows the physical network in plan geometric projection, but is split up in sections that correspond to all constituent parts. By constituent parts I mean some low level geomtric entities that the different elements in my pipeline network can be decomposed into. These will correspond to the basic elements that we need in NTR: RO (pipe) and fitting. For example a preinsulated tee is decomposed into straight pipe segments, because right now I model such element as a simple fabricated tee (pipe welded particular to another pipe thus making a fabricated tee)). We also use forged fittings, forexample a forged fitting (Svejsetee) does not have a change of elevation in its' topology as it is a single steel item. And also forged elbow is likewise, but a preinsulated elbow is deconstructed into straight ROs at the ends and a forged fitting in the middle. So this elbow would be deconstructed into straight segments and forged fitting (or base element). Wait, I've just realized that a preinsulated elbow (and forged fitting) can be rotated around the axis of one of the legs, and thus the other leg will have a slope. So besides vertical forged/preinsulated elbows we can have rotation around axis of one leg for elbows.
This rotation information cannot be read from basic geometry of the drawing. This will need to be annotated in the elements' property set data. So we should start from first leaf node with largest dn (because with bonded pipes you will have two open ends (connecting to existing supply points) and we need to start the analysis from one of them. Then we will be creating our routed elements, they will query the elevation topology, and it will tell them if there's detected a slope at their position. Another idea is that we create a 3d topology using just line segments that follow the centre of bonded pipes and the centre between steel pipes (geometric centre of plastic jacket) for twin pipes. Discuss. Don't write code. Be brief, don't go into implementation details. Let's get some general overview and get some options to consider.
think hard.

---

**Cursor**

Here’s a brief, node-free approach you can consider, with numbered options for easy reference [[memory:8833499]].

1) Guiding principle
- 1.1 Elevation belongs to constituent segments (parametric intervals), not nodes.
- 1.2 NTR does not need explicit nodes; we just need correct 3D coordinates per RO/BOG/TEE/RED.

2) Two viable designs
- 2.1 Segment-interval model (SIM)
  - 2.1.1 Decompose every CAD element into a sequence of constituent base segments in plan (straight centerlines, bend arcs, forged fitting stubs).
  - 2.1.2 Attach elevation profile as a piecewise function f(s) over each segment’s param range; within-element changes (e.g., Afgrening med spring) are just extra breakpoints.
  - 2.1.3 Routing or emission asks “elevation at s” and “slope on [s0,s1]” per segment; no nodes needed.
- 2.2 3D centerline skeleton (CS)
  - 2.2.1 For each logical pipeline path, build a “spine” polyline (plan projection param s) covering the whole path; twins use the geometric center between steel pipes; bonded uses the center of each jacket line.
  - 2.2.2 Define f(s) along the spine (constant, linear slopes, step ΔZ, vertical 45 intervals).
  - 2.2.3 Emit NTR by sampling f(s) at RO endpoints and computing PT for BOG using local in/out 3D directions.

3) Traversal and anchoring
- 3.1 Start at a chosen leaf (largest DN supply connection). Anchor Z there (from property set or config).
- 3.2 Traverse along plan connectivity; build the segment list (SIM) or spine (CS) as you go.
- 3.3 Propagate f(s) using annotations (see 4), not nodes; branches get their own f_branch(s) with their own anchors or inherited conditions.

4) Annotations (property sets) driving elevation
- 4.1 Straight segment: ElevSlope, ElevDeltaZ, KeepLevel.
- 4.2 Elbows (forged/preinsulated): ElbowPlane (Horizontal|Vertical), ElbowRollDeg (rotation about a leg), VerticalPairId (for 45° down/up passes), optional ΔZ.
- 4.3 Afgrening med spring: a small per-element schema defining internal breakpoints (e.g., 45° leg length, then horizontal offset length).
- 4.4 Defaults: level if unspecified; or configurable nominal slope on mains.

5) Emission fit to current pipeline
- 5.1 Your router already decomposes into RO/BOG/etc; simply query f(s) at the routed member’s parameter(s) to assign Z (A, B, and PT for bends).
- 5.2 Preinsulated elbows: still expand to RO–BOG–RO; ElbowRollDeg drives the 3D plane; vertical elbows use the VerticalPairId/ΔZ interval.
- 5.3 Forged tees/elbows: no internal elevation change; their surrounding RO segments carry any slope.

6) Pros/cons
- 6.1 SIM: granular, handles within-element changes cleanly; requires solid param bookkeeping.
- 6.2 CS: intuitive “spine” per path; easy to QA/plot; requires building/maintaining spines across branches.

7) Decision points
- 7.1 Choose SIM (per-segment intervals) vs CS (per-path spine); both avoid nodes and map directly to NTR.
- 7.2 Pick root selection rule (largest DN leaf) and anchoring policy for branches (inherit vs explicit).
- 7.3 Lock annotation fields for elbows and special elements (Afgrening med spring) so within-element elevation is declarative.
- 7.4 Keep twins/bonded as offsets applied after computing centerline f(s).

Short take: Move elevation to a parametric “segment-interval” (or “spine”) model, fed by explicit annotations (including elbow roll and vertical pairs). Route as you do today but query f(s) for Z, avoiding any reliance on nodes.

---

**User**

I don't know about a function over an element. For example a preinsulated tee with spring (45deg) has a main leg stretch that is welded into main pipe and a perpendicular stretch with the sloped and level stretch. How am I supposed to use a f(s) on it?

---

**Cursor**

Short answer: don’t put one f(s) on the composite tee element; put piecewise f(s) on the constituent path segments you already emit (RO/BOG) after decomposition.

1) Where f(s) lives
- 1.1 f(s) is per path segment (constituent RO/BOG interval), not per CAD block.
- 1.2 Each path (main, branch) has its own local arc-length s; breakpoints split segments where elevation behavior changes.

2) Preinsulated tee with spring (45°) example
- 2.1 Main path: unchanged; inherits whatever elevation rule applies to the through-RO(s).
- 2.2 Branch path: piecewise intervals
  - 2.2.1 [0, Lspring]: slope = ±1 (45°) or per annotation; Z(s) = Z0 ± s.
  - 2.2.2 [Lspring, Lspring+Llevel]: level at new Z (constant).
  - 2.2.3 After Lspring+Llevel: continue per defaults/annotations.
- 2.3 These intervals are created by the router’s macro expansion (like your preinsulated elbow RO–BOG–RO), so each interval maps to one or more RO members with explicit end Z.

3) How to drive it (annotations)
- 3.1 At the special tee: SpringAngleDeg (default 45), SpringLegM (Lspring), BranchLevelRunM (Llevel), ElbowRollDeg (orientation), optional TargetDeltaZ.
- 3.2 Orientation (roll) selects the 3D bending plane/sign; Z is still a scalar along the branch’s s.

4) Emission flow
- 4.1 Decompose element → routed RO/BOG pieces with local s.
- 4.2 Evaluate piecewise f(s) on each piece to set A/B (and PT for bends).
- 4.3 No reliance on nodes; NTR importer will create nodes implicitly.

5) Alternative phrasing (if you prefer “spine”)
- 5.1 Define a centerline spine per path; attach the same piecewise rules (2.2) to that spine.
- 5.2 Sample the spine at RO endpoints/BOG tangents; semantics identical to 1–4.

---

**User**

I don't understand, you need to be more brief and simple in description. Number 3) How to drive it, do you mean that we should annotate the blocks?

---

**Cursor**

Yes.

1) Annotate fittings (blocks) that can change elevation within themselves:
- 1.1 Preinsulated tee with spring: `SpringAngleDeg` (default 45), `SpringLegM`, `BranchLevelRunM`, optional `TargetDeltaZ`, `ElbowRollDeg` (orientation).
- 1.2 Elbows (forged/preinsulated): `ElbowPlane` (Horizontal|Vertical), optional `ElbowRollDeg`, optional `VerticalPairId` (for 45° down/up pairs), optional `ElevDeltaZ`.

2) Annotate straight pipes (polylines) only for simple runs:
- 2.1 `ElevSlope` (m/m), or `ElevDeltaZ` (m), or `ElevKeepLevel` (bool).

3) Alternative to property sets (optional):
- 3.1 CSV rows that reference entity handles with the same fields.

4) Defaults if missing:
- 4.1 Pipes level; fittings behave “neutral” (no internal spring/tilt) unless annotated.

5) Processing:
- 5.1 Router decomposes to RO/BOG pieces and applies these annotations to set Z—no node system needed.

---

**User**

no, for preinsulated tee with spring types of elements we will use a geometry definition inside the class, the only annotation we need is if it is routed "up" (above the pipes) or "down" (below the pipes). Then the class should have a method that receives the elevation at the current traversal entry point. This method then solves the geometry and returns the resulting topology. This is because the traversal can either start at the element, then the elevation is 0. Or it can enter via main leg, thus the main leg will have this elevation and the class solves the branch with the slope etc. Or it can enter through the branch port, and thus the whole geometry will be solved based on the entry point.

Vertical elbows will have annotation "Up/down" and the block has a property "Vinkel" (they are not always 45deg).

Vertical elbows / plane elbows are distinct blocks in autocad. So no need to mix vertical with normal elbows. So plane elbows will need to have an annotation for roll, which will be "Near 45", "Far 25" where near and far are legs seen in relation to the supply point. The algorithm will have find out which leg is nearest to supply point, because else we don't have a way to define which leg is the axis of the roll.

Elements that don't usually act for slope change (pipes, tees) will not have any annotations, they will wholly inherit the elevation from the preceding node in the traversal. So, I think you missed my point, to reiterate, first entry point of the system gives us the basis from which the rest is calculated. Enter first element, decompose it into the system we choose, solve the geometry, continue traversal through one of the available exit ports (a port is active only if there's an element connected to it) and push the calculated elevation at this port level. The next element solves its' geometry, creates topology, pushes the new (most probably the same elevation) elevation on to the next element. Is it more clear now?

---

**Cursor**

Yes, crystal clear. Your model is “element-centric elevation propagation,” not node-based. Here’s the concise shape of it:

1) Traversal model
- 1.1 Pick entry element (largest-DN supply), set entryZ (e.g., 0 or known).
- 1.2 Walk the network via connected ports; at each step call element.Solve(entryPort, entryZ) → returns:
  - 3D constituent pieces (RO/BOG/…)
  - Exit ports with their exitZ
- 1.3 Push exitZ to the next connected element; continue until exhausted. Track visited (element, entryPort) to avoid loops.

2) Annotations (minimal)
- 2.1 Preinsulated tee with spring: Up/Down only. Class encodes geometry (angle/legs), uses entryZ and entryPort to solve branch (45° leg, level run, etc.).
- 2.2 Vertical elbow (distinct block): Up/Down; use block Vinkel (angle). Class tilts along its vertical plane from entryZ.
- 2.3 Plane elbow (distinct block): Roll annotation “Near x” / “Far x”. Determine which leg is nearer the supply direction; rotate around that leg by x degrees to set the other leg’s slope.
- 2.4 Passives (straight pipes, forged tees): no annotations; inherit entryZ through to exits unchanged.

3) Connectivity
- 3.1 Ports are “active” only if connected; solve geometry only along connected exits.
- 3.2 Branches get their own entryZ at the junction (from the element solving step) and propagate independently.

4) Emission
- 4.1 The 3D pieces emitted by each element (during Solve) are exactly the NTR RO/BOG/… you already output; no explicit nodes needed.
- 4.2 Twins/bonded apply existing Z-offsets after centerline Z is known.

5) Near/Far determination (plane elbows)
- 5.1 Compute which leg endpoint is closer along the supply path (or smaller plan distance from the chosen entry) → that leg is “Near”; the other is “Far”.

This keeps annotations minimal, elevation logic local to element classes, and aligns with your routed output without introducing a node system.

---

**User**

Let's discuss the traversal. We do not emit RO/BOG etc elements, we build our topology system which we haven't decided on yet, right?

---

**Cursor**

Yes. Let’s pin down traversal and the topology “shape” without nodes.

1) Traversal contract
- 1.1 Start at chosen entry element (largest-DN supply), with entryPort and entryZ.
- 1.2 Call element.Solve(entryPort, entryZ, entryDir) → returns:
  - 1.2.1 Pieces3D: constituent geometry for this element (generic, not RO/BOG-bound).
  - 1.2.2 Exits: list of (exitPort, exitZ, exitDir) for connected ports only.
- 1.3 For each exit, continue to the connected element; avoid loops with visited (element, entryPort).

2) State carried along
- 2.1 entryZ: scalar elevation at the entering port.
- 2.2 entryDir: plan/3D direction for “near/far” and plane choice.
- 2.3 context: supply direction hint, twin/bonded info (for offsets later).

3) Element responsibilities (examples)
- 3.1 Passive (pipe, forged tee): pass-through Z; emit straight piece(s).
- 3.2 Vertical elbow: has Up/Down + Vinkel; tilt between ports; set exitZ, exitDir.
- 3.3 Plane elbow: has Roll (“Near x”/“Far x”); determine near leg from entryDir; rotate around that leg; compute exitZ, exitDir.
- 3.4 Preinsulated tee with spring: Up/Down only; class encodes internal geometry; given entryPort+entryZ, produces 45° leg then level leg and sets branch exitZ.

4) Topology representation (pick one)
- 4.1 Path sequences (recommended): a registry of path IDs, each a sequence of Pieces3D with continuity; branches create new paths at tees.
- 4.2 Port-graph (node-free): vertices = (element, port); edges carry Pieces3D and continuity data; good for later merges.
- 4.3 3D spines per path: polylines/arcs in 3D per logical path; simplest to inspect/QA.

5) Branching, cycles, termination
- 5.1 Branching: enqueue each connected exit; each path carries its own entryZ/entryDir.
- 5.2 Cycles: stop revisiting (element, entryPort); optionally detect closed loops and report.
- 5.3 Termination: open ends (no connected element) or visited budget.

6) What we’re not deciding yet
- 6.1 Emission to RO/BOG/…: later; Pieces3D are generic carriers.
- 6.2 Node IDs: not needed; importer will create them.

7) Minimal decisions to proceed
- 7.1 Choose topology shape: 4.1 Path sequences, 4.2 Port-graph, or 4.3 3D spines?
- 7.2 Confirm minimal annotations: Preinsulated spring tee (Up/Down), Vertical elbow (Up/Down + Vinkel), Plane elbow (Roll “Near/Far x”), others none.
- 7.3 Entry selection and direction: start at largest-DN supply; entryDir = along first connected pipeline in plan.

---

**User**

succintly describe the 3 topology shapes (7.1) and how they function

---

**Cursor**

- 1) Path sequences
  - 1.1 Each logical pipeline path is a linear sequence of Pieces3D with continuity (A→B→C…). Branches spawn new sequences at split points. Simple traversal, easy to append, and straightforward to map to output later.

- 2) Port-graph (node-free)
  - 2.1 Vertices are (element, port). Edges represent solved transitions between ports and carry Pieces3D plus entry/exit Z/dir. Good for complex reconnections/loops and later global queries, but slightly heavier than sequences.

- 3) 3D spines
  - 3.1 For each path, maintain a single 3D polyline/arc “spine” representing the centerline with proper Z. Pieces3D are implicit segments of the spine. Very QA-friendly and compact, but you must handle bends and within-element constructs as spine subranges.

---

**User**

Implement nr 3 in a new branch

---

**Cursor**

I'll create a new git branch for the feature, scaffold the 3D spines topology (path, segments, builder), and add a debug command `NTRSPINE` to build spines from the current drawing topology. Then I'll lint the new files and commit the changes.

Starting by creating a new git branch for the 3D spines implementation. Then I'll scaffold the spines model, builder, and a debug command.

```csharp
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NTRExport.Enums;

namespace NTRExport.Spines
{
    internal abstract class SpineSegment
    {
        protected SpineSegment(Handle source, Point3d a, Point3d b, int dn, FlowRole flowRole)
        {
            Source = source;
            A = a;
            B = b;
            DN = dn;
            FlowRole = flowRole;
        }

        public Handle Source { get; }
        public Point3d A { get; }
        public Point3d B { get; }
        public int DN { get; }
        public FlowRole FlowRole { get; }
        public double Length => A.DistanceTo(B);
    }

    internal sealed class SpineStraight : SpineSegment
    {
        public SpineStraight(Handle source, Point3d a, Point3d b, int dn, FlowRole role)
            : base(source, a, b, dn, role)
        {
        }
    }

    internal sealed class SpineBend : SpineSegment
    {
        public SpineBend(Handle source, Point3d a, Point3d b, Point3d tangent, int dn, FlowRole role)
            : base(source, a, b, dn, role)
        {
            Tangent = tangent;
        }

        public Point3d Tangent { get; }
    }
}


```

```csharp
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace NTRExport.Spines
{
    internal sealed class SpinePath
    {
        private readonly List<SpineSegment> _segments = new();

        public SpinePath(string id)
        {
            Id = id;
        }

        public string Id { get; }
        public IReadOnlyList<SpineSegment> Segments => _segments;

        public void Add(SpineSegment segment)
        {
            _segments.Add(segment);
        }

        public double TotalLength
        {
            get
            {
                double sum = 0.0;
                foreach (var s in _segments) sum += s.Length;
                return sum;
            }
        }

        public Point3d? StartPoint => _segments.Count == 0 ? null : _segments[0].A;
        public Point3d? EndPoint => _segments.Count == 0 ? null : _segments[^1].B;
    }
}


```

```csharp
using Autodesk.AutoCAD.Geometry;

using NTRExport.Enums;
using NTRExport.TopologyModel;

namespace NTRExport.Spines
{
    internal sealed class SpineBuilder
    {
        public List<SpinePath> Build(Topology topo)
        {
            var paths = new List<SpinePath>();
            int idx = 1;

            // Baseline: create one path per pipe segment to establish the 3D spine representation.
            // Later, this can be upgraded to stitch continuous pipes into longer paths and to include bends/fittings.
            foreach (var p in topo.Pipes)
            {
                var path = new SpinePath($"P{idx++:0000}");
                var a = p.A.Node.Pos;
                var b = p.B.Node.Pos;

                // Elevation propagation will adjust Z; for now keep existing Z from topology.
                var role = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem
                    ? FlowRole.Supply
                    : (p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Retur ? FlowRole.Return : FlowRole.Unknown);

                path.Add(new SpineStraight(p.Source, a, b, p.DN, role));
                paths.Add(path);
            }

            return paths;
        }
    }
}


```

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using NTRExport.TopologyModel;

using static IntersectUtilities.UtilsCommon.Utils;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(NTRExport.Spines.SpineCommands))]

namespace NTRExport.Spines
{
    public sealed class SpineCommands : IExtensionApplication
    {
        public void Initialize()
        {
            // no-op
        }

        public void Terminate()
        {
            // no-op
        }

        [CommandMethod("NTRSPINE")]
        public void BuildSpines()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var ents = localDb.GetFjvEntities(tx);
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();

                var topo = TopologyBuilder.Build(polylines, fittings);

                var builder = new SpineBuilder();
                var spines = builder.Build(topo);

                prdDbg($"Spines built: {spines.Count}");

                double totalLen = 0.0;
                foreach (var s in spines) totalLen += s.TotalLength;
                prdDbg($"Total spine length (plan): {totalLen:0.###} m");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }
    }
}


```

Created branch and added a 3D spines scaffold.

- Branch: feature/3d-spines
- New files:
  - NTRExport/Spines/SpineSegment.cs — base + Straight/Bend segments
  - NTRExport/Spines/SpinePath.cs — path container with total length
  - NTRExport/Spines/SpineBuilder.cs — baseline Build(topology): one path per pipe (Z preserved)
  - NTRExport/Spines/SpineDebugCommand.cs — command NTRSPINE to build spines and print counts

How it works now:
- Run NTRSPINE after NETLOAD; it builds topology from current DWG, constructs spines (baseline), and logs number of spines and total length.

Next steps (optional):
- Stitch adjacent pipes into continuous spines.
- Add bends/fitting decomposition into path subranges.
- Integrate element-centric elevation propagation during traversal.

- Created git branch `feature/3d-spines`.
- Implemented 3D spines topology types, a basic builder from `Topology`, and an `NTRSPINE` debug command.
- Linted and committed changes.

---

**User**

the elevation topology should function between the topology creation and creation of routed objects. We are not creating a separate command! we need to query slope for routed elements in @Topology.cs 

---

**Cursor**

```csharp
using NTRExport.TopologyModel;

namespace NTRExport.Routing
{
    internal sealed class Router
    {
        private readonly Topology _topo;

        public Router(Topology topo)
        {
            _topo = topo;
        }

        public RoutedGraph Route(IElevationProvider elevation)
        {
            var g = new RoutedGraph();
            var ctx = new RouterContext(_topo, elevation);

            foreach (var e in _topo.Elements)
            {
                e.Route(g, _topo, ctx);
            }

            return g;
        }
    }

    internal sealed class RouterContext
    {        
        public Topology Topology { get; }
        public IElevationProvider Elevation { get; }
        public RouterContext(Topology topo, IElevationProvider elevation) { Topology = topo; Elevation = elevation; }        
    }
}
```

```csharp
using Autodesk.AutoCAD.Geometry;
using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal interface IElevationProvider
    {
        // Return centerline Z at param t in [0,1] along plan segment a->b for the given element.
        double GetZ(ElementBase element, Point3d a, Point3d b, double t);

        // Convenience: Z at an endpoint (0 or 1)
        double GetZAtStart(ElementBase element, Point3d a, Point3d b) => GetZ(element, a, b, 0.0);
        double GetZAtEnd(ElementBase element, Point3d a, Point3d b) => GetZ(element, a, b, 1.0);
    }
}


```

```csharp
using Autodesk.AutoCAD.Geometry;
using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class FlatElevationProvider : IElevationProvider
    {
        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            // Default: keep existing plan Z (usually 0) if present on endpoints, interpolate.
            var z0 = a.Z;
            var z1 = b.Z;
            return z0 + t * (z1 - z0);
        }
    }
}


```

```csharp
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NTRExport.Spines;
using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class SpineElevationProvider : IElevationProvider
    {
        private readonly List<SpinePath> _spines;

        public SpineElevationProvider(IEnumerable<SpinePath> spines)
        {
            _spines = spines.ToList();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            // Try to find a spine segment with the same source handle.
            var seg = FindSegmentBySource(element.Source);
            if (seg != null)
            {
                var z0 = seg.A.Z;
                var z1 = seg.B.Z;
                return z0 + t * (z1 - z0);
            }

            // Fallback: use plan endpoints
            var zStart = a.Z;
            var zEnd = b.Z;
            return zStart + t * (zEnd - zStart);
        }

        private SpineSegment? FindSegmentBySource(Handle source)
        {
            foreach (var p in _spines)
            {
                foreach (var s in p.Segments)
                {
                    if (s.Source == source) return s;
                }
            }
            return null;
        }
    }
}


```

```csharp
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;
using NTRExport.SoilModel;

using static IntersectUtilities.UtilsCommon.Utils;
using static NTRExport.Utils.Utils;


namespace NTRExport.TopologyModel
{
    internal class TNode
    {
        public Point3d Pos { get; init; }
        public string Name { get; set; } = ""; // assigned later
        public List<TPort> Ports { get; } = new();
    }

    internal class TPort
    {
        public PortRole Role { get; init; }
        public TNode Node { get; init; }
        public ElementBase Owner { get; init; }

        public TPort(PortRole role, TNode node, ElementBase owner)
        {
            Role = role;
            Node = node;
            Owner = owner;
        }
    }

    internal abstract class ElementBase
    {
        public Handle Source { get; }
        protected Entity _entity;

        protected ElementBase(Handle src)
        {
            Source = src;
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;
            var tx = db.TransactionManager.TopTransaction;
            _entity = src.Go<Entity>(db);
        }

        public abstract IReadOnlyList<TPort> Ports { get; }
        public virtual PipeSystemEnum System => system();

        private PipeSystemEnum system()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeSystem(_entity),
                BlockReference br => br.GetPipeSystemEnum(),
                _ => PipeSystemEnum.Ukendt,
            };
        }

        public virtual PipeTypeEnum Type => type();

        private PipeTypeEnum type()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeType(_entity),
                BlockReference br => br.GetPipeTypeEnum(),
                _ => PipeTypeEnum.Ukendt,
            };
        }

        public virtual PipeSeriesEnum Series => series();

        private PipeSeriesEnum series()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeSeriesV2(_entity),
                BlockReference br => br.GetPipeSeriesEnum(),
                _ => PipeSeriesEnum.Undefined,
            };
        }

        public abstract int DN { get; }
        public virtual string Material
        {
            get
            {
                return System switch
                {
                    PipeSystemEnum.Stål => "P235GH",
                    _ => "Unknown",
                };
            }
        }
        public IPipeVariant Variant =>
            Type == PipeTypeEnum.Twin ? new TwinVariant() : new SingleVariant();

        protected virtual (double zUp, double zLow) ComputeTwinOffsets(
            PipeSystemEnum ps,
            PipeTypeEnum pt,
            int dn
        )
        {
            if (!Variant.IsTwin)
                return (0.0, 0.0);

            var odMm = PipeScheduleV2.GetPipeOd(ps, dn);
            var gapMm = PipeScheduleV2.GetPipeDistanceForTwin(ps, dn, pt);
            var z = Math.Max(0.0, odMm + gapMm) / 2000.0;
            return (z, -z);
        }

        public virtual void Route(RoutedGraph g, Topology topo, RouterContext ctx) { }
    }

    internal class TPipe : ElementBase
    {
        public TPort A { get; }
        public TPort B { get; }
        public override int DN => PipeScheduleV2.GetPipeDN(_entity);

        // Cushion spans along this pipe in meters (s0,s1) from A→B
        public List<(double s0, double s1)> CushionSpans { get; } = new();
        public double Length
        {
            get
            {
                var dx = B.Node.Pos.X - A.Node.Pos.X;
                var dy = B.Node.Pos.Y - A.Node.Pos.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        public TPipe(Handle h, Curve2d s, Func<TPipe, TPort> makeA, Func<TPipe, TPort> makeB) : base(h)
        {
            A = makeA(this);
            B = makeB(this);
        }

        public override IReadOnlyList<TPort> Ports => [A, B];

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var isTwin = Variant.IsTwin;
            var suffix = Variant.DnSuffix;
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
            var flow = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return;
            var ltg = LTGMain(Source);

            var cuts = new SortedSet<double> { 0.0, Length };
            foreach (var (s0, s1) in CushionSpans)
            {
                cuts.Add(Math.Max(0.0, Math.Min(Length, s0)));
                cuts.Add(Math.Max(0.0, Math.Min(Length, s1)));
            }

            var segments = cuts.ToList();
            for (int i = 0; i < segments.Count - 1; i++)
            {
                var s0 = segments[i];
                var s1 = segments[i + 1];
                if (s1 - s0 < 1e-6)
                    continue;

                var soil = IsCovered(CushionSpans, s0, s1)
                    ? new SoilProfile("Soil_C80", 0.08)
                    : SoilProfile.Default;

                var aPos = Lerp(A.Node.Pos, B.Node.Pos, Length <= 1e-9 ? 0.0 : s0 / Length);
                var bPos = Lerp(A.Node.Pos, B.Node.Pos, Length <= 1e-9 ? 0.0 : s1 / Length);

                // Query elevation provider for centerline Z at the segment endpoints
                var tA = Length <= 1e-9 ? 0.0 : s0 / Length;
                var tB = Length <= 1e-9 ? 0.0 : s1 / Length;
                var zCenterA = ctx.Elevation.GetZ(this, A.Node.Pos, B.Node.Pos, tA);
                var zCenterB = ctx.Elevation.GetZ(this, A.Node.Pos, B.Node.Pos, tB);
                var aCenter = new Point3d(aPos.X, aPos.Y, zCenterA);
                var bCenter = new Point3d(bPos.X, bPos.Y, zCenterB);

                if (isTwin)
                {
                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = aCenter.Z(zUp),
                            B = bCenter.Z(zUp),
                            DN = DN,
                            Material = Material,
                            DnSuffix = suffix,
                            FlowRole = FlowRole.Return,
                            Soil = soil,
                            LTG = ltg,
                        }
                    );
                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = aCenter.Z(zLow),
                            B = bCenter.Z(zLow),
                            DN = DN,
                            Material = Material,
                            DnSuffix = suffix,
                            FlowRole = FlowRole.Supply,
                            Soil = soil,
                            LTG = ltg,
                        }
                    );
                }
                else
                {
                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = aCenter,
                            B = bCenter,
                            DN = DN,
                            Material = Material,
                            DnSuffix = suffix,
                            FlowRole = flow,
                            Soil = soil,
                            LTG = ltg,
                        }
                    );
                }
            }
        }


        private static bool IsCovered(List<(double s0, double s1)> spans, double a, double b)
        {
            var mid = 0.5 * (a + b);
            return spans.Any(z => mid >= z.s0 - 1e-9 && mid <= z.s1 + 1e-9);
        }

        private static Point3d Lerp(Point3d a, Point3d b, double t) =>
            new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z));
    }

    #region PipeVariant
    internal interface IPipeVariant
    {
        string DnSuffix { get; }
        bool IsTwin { get; }
    }

    internal sealed class SingleVariant : IPipeVariant
    {
        public string DnSuffix => "s";
        public bool IsTwin => false;
    }

    internal sealed class TwinVariant : IPipeVariant
    {
        public string DnSuffix => "t";
        public bool IsTwin => true;
    }
    #endregion

    internal abstract class TFitting : ElementBase
    {
        private readonly List<TPort> _ports = new();
        private readonly HashSet<PipelineElementType> _allowedKinds = new();

        protected TFitting(Handle source, PipelineElementType kind)
            : base(source)
        {
            ConfigureAllowedKinds(_allowedKinds);
            if (_allowedKinds.Count > 0 && !_allowedKinds.Contains(kind))
            {
                var allowedNames = string.Join(", ", _allowedKinds);
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    $"Kind {kind} is not permitted. Allowed kinds: {allowedNames}."
                );
            }
            Kind = kind;
        }

        public PipelineElementType Kind { get; }
        public override IReadOnlyList<TPort> Ports => _ports;
        public override int DN => GetDn();

        private int GetDn()
        {
            var br = _entity as BlockReference;
            if (br == null)
            {
                var pl = _entity as Polyline;
                if (pl == null) throw new InvalidOperationException($"Entity {Source} не мышенок и не зверь!");
                return PipeScheduleV2.GetPipeDN(pl);
            }

            return Convert.ToInt32(br.ReadDynamicCsvProperty(DynamicProperty.DN1));
        }

        public void AddPort(TPort port)
        {
            if (!ReferenceEquals(port.Owner, this))
            {
                throw new InvalidOperationException("Port owner must be the fitting itself.");
            }

            _ports.Add(port);
        }

        public void AddPorts(IEnumerable<TPort> ports)
        {
            foreach (var port in ports)
            {
                AddPort(port);
            }
        }

        protected virtual void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // Default no-op; derived classes route if supported
        }

        // Resolve DN to use for a neighboring connection based on the neighbor's port role.
        // Default behavior: use this fitting's DN for any role.
        public virtual bool TryGetDnForPortRole(PortRole role, out int dn)
        {
            dn = DN;
            return true;
        }

    }

    internal class ElbowFormstykke : TFitting
    {
        public Point3d TangentPoint { get; }

        public ElbowFormstykke(Handle source, Point3d tangentPoint, PipelineElementType kind)
            : base(source, kind)
        {
            TangentPoint = tangentPoint;
        }

        public ElbowFormstykke(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var br = source.Go<BlockReference>(db);
            if (br == null)
                throw new System.Exception($"Received {source} for ElbowFormstykke!");

            TangentPoint = br.Position;
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Kedelrørsbøjning);
            allowed.Add(PipelineElementType.Bøjning45gr);
            allowed.Add(PipelineElementType.Bøjning30gr);
            allowed.Add(PipelineElementType.Bøjning15gr);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var ends = Ports.Take(2).ToArray();
            if (ends.Length < 2)
                return;
            var a = ends[0].Node.Pos;
            var b = ends[1].Node.Pos;
            var t = TangentPoint;
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);

            var flowMain = Variant.IsTwin
                ? FlowRole.Return
                : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return);

            g.Members.Add(
                new Routing.RoutedBend(Source, this)
                {
                    A = a.Z(zUp),
                    B = b.Z(zUp),
                    T = t.Z(zUp),
                    DN = DN,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = flowMain,
                    LTG = LTGMain(Source),
                }
            );

            if (Variant.IsTwin)
            {
                g.Members.Add(
                    new Routing.RoutedBend(Source, this)
                    {
                        A = a.Z(zLow),
                        B = b.Z(zLow),
                        T = t.Z(zLow),
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = FlowRole.Supply,
                        LTG = LTGMain(Source),
                    }
                );
            }
        }


    }

    internal sealed class Bueror : ElbowFormstykke
    {
        public Bueror(Handle source, PipelineElementType kind)
            : base(source, CalculateTangentPoint(source), kind) { }

        private static Point3d CalculateTangentPoint(Handle source)
        {
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var br = source.Go<BlockReference>(db);
            if (br == null)
                throw new System.Exception($"Received {source} for Buerør! Must be BlockReference!");

            using var tx = db.TransactionManager.StartOpenCloseTransaction();
            var btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);

            foreach (ObjectId id in btr)
            {
                if (!id.IsDerivedFrom<Arc>()) continue;

                var arc = id.Go<Arc>(tx);

                // Find tangent intersection using pure math (apply block transform)
                var tangentPoint = GetTangentPoint(arc, br.BlockTransform);

                if (tangentPoint != default)
                {
                    return tangentPoint;
                }

                break;
            }

            throw new System.Exception(
                $"Buerør: Arc not found for buerør {source}!");
        }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Buerør);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            base.Route(g, topo, ctx);
        }
    }

    internal abstract class PreinsulatedElbowBase : ElbowFormstykke
    {
        protected PreinsulatedElbowBase(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.PræisoleretBøjning90gr);
            allowed.Add(PipelineElementType.PræisoleretBøjning45gr);
            allowed.Add(PipelineElementType.PræisoleretBøjningVariabel);
        }

        protected abstract int ThresholdDN { get; }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var ends = Ports.Take(2).ToArray();
            if (ends.Length < 2) return;

            var a = ends[0].Node.Pos;
            var b = ends[1].Node.Pos;
            var t = TangentPoint;
            var r = (DN <= ThresholdDN ? Geometry.GetBogRadius5D(DN) : Geometry.GetBogRadius3D(DN)) / 1000.0;

            // Solve fillet points a' and b' for a radius r between lines (a↔t) and (b↔t)
            Point2d a2 = a.To2d();
            Point2d b2 = b.To2d();
            Point2d t2 = t.To2d();

            var va = a2 - t2; var vb = b2 - t2;
            if (va.Length < 1e-9 || vb.Length < 1e-9) return;

            var ua = va.GetNormal();
            var ub = vb.GetNormal();
            var dot = Math.Max(-1.0, Math.Min(1.0, ua.DotProduct(ub)));
            var alpha = Math.Acos(dot);
            var sinHalf = Math.Sin(alpha * 0.5);
            var cosHalf = Math.Cos(alpha * 0.5);
            if (sinHalf < 1e-9) return; // parallel/degenerate

            var l = r * (cosHalf / sinHalf); // R * cot(alpha/2)

            // Check feasibility: tangent points must lie between a↔t and b↔t
            var lenAT = va.Length; var lenBT = vb.Length;
            if (l > lenAT - 1e-9 || l > lenBT - 1e-9)
            {
                // radius too large for available leg length; clamp
                l = Math.Max(0.0, Math.Min(lenAT, lenBT) * 0.5);
            }

            var aPrime2 = new Point2d(t2.X + ua.X * l, t2.Y + ua.Y * l);
            var bPrime2 = new Point2d(t2.X + ub.X * l, t2.Y + ub.Y * l);

            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);
            var mainFlow = Variant.IsTwin ? FlowRole.Return : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return);
            var ltg = LTGMain(Source);

            void EmitFor(double z, FlowRole flow)
            {
                var aPrime = aPrime2.To3d(z);
                var bPrime = bPrime2.To3d(z);
                var aZ = a.Z(z);
                var bZ = b.Z(z);
                var tZ = t.Z(z);

                // a → a'
                g.Members.Add(
                    new Routing.RoutedStraight(Source, this)
                    {
                        A = aZ,
                        B = aPrime,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flow,
                        LTG = ltg,
                    }
                );

                // bend a' → b' with PT = t
                g.Members.Add(
                    new Routing.RoutedBend(Source, this)
                    {
                        A = aPrime,
                        B = bPrime,
                        T = tZ,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flow,
                        LTG = ltg,
                        Norm = DN <= ThresholdDN ? "" : "EN 10253-2 - Type A"
                    }
                );

                // b' → b
                g.Members.Add(
                    new Routing.RoutedStraight(Source, this)
                    {
                        A = bPrime,
                        B = bZ,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flow,
                        LTG = ltg,
                    }
                );
            }

            // Emit main flow
            EmitFor(Variant.IsTwin ? zUp : 0.0, mainFlow);

            // Emit supply for twin
            if (Variant.IsTwin)
            {
                EmitFor(zLow, FlowRole.Supply);
            }
        }
    }

    internal sealed class PreinsulatedElbowAbove45deg : PreinsulatedElbowBase
    {
        public PreinsulatedElbowAbove45deg(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override int ThresholdDN => 400;
    }

    internal sealed class PreinsulatedElbowAtOrBelow45deg : PreinsulatedElbowBase
    {
        public PreinsulatedElbowAtOrBelow45deg(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override int ThresholdDN => 200;
    }

    internal abstract class TeeMainRun : TFitting
    {
        protected TeeMainRun(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
            OffsetMain = ComputeTwinOffsets(System, Type, DnM);
        }

        public override int DN => DnM;
        protected int DnM =>
            _entity switch
            {
                BlockReference br => Convert.ToInt32(
                    br.ReadDynamicCsvProperty(DynamicProperty.DN1)
                ),
                _ => throw new InvalidOperationException(
                    $"Entity {Source} is not a BlockReference!"
                ),
            };
        protected int DnB =>
            _entity switch
            {
                BlockReference br => Convert.ToInt32(
                    br.ReadDynamicCsvProperty(DynamicProperty.DN2)
                ),
                _ => throw new InvalidOperationException(
                    $"Entity {Source} is not a BlockReference!"
                ),
            };
        protected TPort MainPort1 => Ports.First(p => p.Role == PortRole.Main);
        protected TPort MainPort2 => Ports.Last(p => p.Role == PortRole.Main);
        protected Point2d MidPoint => MainPort1.Node.Pos.To2d().MidPoint(MainPort2.Node.Pos.To2d());
        protected TPort BranchPort => Ports.First(p => p.Role == PortRole.Branch);
        protected (double zUp, double zLow) OffsetMain;

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            g.Members.Add(
                new RoutedStraight(Source, this)
                {
                    A = MainPort1.Node.Pos.Z(OffsetMain.zUp),
                    B = MainPort2.Node.Pos.Z(OffsetMain.zUp),
                    DN = DnM,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = Variant.IsTwin
                        ? FlowRole.Return
                        : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return),
                    LTG = LTGMain(Source),
                }
            );

            if (Variant.IsTwin)
            {
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = MainPort1.Node.Pos.Z(OffsetMain.zLow),
                        B = MainPort2.Node.Pos.Z(OffsetMain.zLow),
                        DN = DnM,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = FlowRole.Supply,
                        LTG = LTGMain(Source),
                    }
                );
            }
        }
    }

    internal sealed class TeeFormstykke : TeeMainRun
    {
        public TeeFormstykke(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Svejsetee);
            allowed.Add(PipelineElementType.PreskoblingTee);
            allowed.Add(PipelineElementType.Muffetee);
        }
    }

    internal sealed class AfgreningMedSpring : TeeMainRun
    {
        public AfgreningMedSpring(Handle source)
            : base(source, PipelineElementType.AfgreningMedSpring) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.AfgreningMedSpring);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // TODO: implement macro; placeholder no-op
        }
    }

    internal sealed class AfgreningParallel : TeeMainRun
    {
        public AfgreningParallel(Handle source)
            : base(source, PipelineElementType.AfgreningParallel) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.AfgreningParallel);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // TODO: implement macro; placeholder no-op
        }
    }

    internal sealed class LigeAfgrening : TeeMainRun
    {
        public LigeAfgrening(Handle source)
            : base(source, PipelineElementType.LigeAfgrening) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.LigeAfgrening);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            base.Route(g, topo, ctx);

            var offsetBranch = ComputeTwinOffsets(System, Type, DnB);

            if (!Variant.IsTwin)
            {
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = BranchPort.Node.Pos,
                        B = MidPoint.To3d(),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return,
                        LTG = LTGBranch(Source),
                    }
                );
            }
            else if (DnM == DnB)
            {
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = BranchPort.Node.Pos.Z(OffsetMain.zUp),
                        B = MidPoint.To3d().Z(OffsetMain.zUp),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = Variant.IsTwin
                            ? FlowRole.Return
                            : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return),
                        LTG = LTGMain(Source),
                    });

                if (Variant.IsTwin)
                {
                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = BranchPort.Node.Pos.Z(OffsetMain.zLow),
                            B = MidPoint.To3d().Z(OffsetMain.zLow),
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = FlowRole.Supply,
                            LTG = LTGMain(Source),
                        });
                }
            }
            else //Solve twin geometry with bends
            {
                //Project system to 2D plane through branch port,
                //mid point and plane normal is main run
                var branchOrigin = BranchPort.Node.Pos.To2d();
                var toMid = MidPoint - branchOrigin;
                var branchDistance = toMid.Length;
                if (branchDistance < 1e-9)
                {
                    prdDbg($"LigeAfgrening.Route: branch and main coincide for {Source}.");
                    return;
                }

                var branchDirPlan = toMid.GetNormal();

                Point2d branchStartUp = new Point2d(0.0, offsetBranch.zUp);
                Point2d branchEndUp = new Point2d(branchDistance, offsetBranch.zUp);
                Point2d mainCentreUp = new Point2d(branchDistance, OffsetMain.zUp);

                Point2d branchStartLow = new Point2d(0.0, offsetBranch.zLow);
                Point2d branchEndLow = new Point2d(branchDistance, offsetBranch.zLow);
                Point2d mainCentreLow = new Point2d(branchDistance, OffsetMain.zLow);

                double mainStubLength = PipeScheduleV2.GetPipeOd(System, DnM) / 2000.0 + 0.01;
                double bendOd = Geometry.GetBogRadius5D(DnB) / 1000.0;

                var filletReturn = Geometry.SolveBranchFillet(
                    branchStartUp,
                    branchEndUp,
                    mainCentreUp,
                    bendOd,
                    mainStubLength
                );

                if (filletReturn is null)
                {
                    prdDbg($"LigeAfgrening.Route: unable to solve return branch fillet for {Source}.");
                    return;
                }

                var filletSupply = Geometry.SolveBranchFillet(
                    branchStartLow,
                    branchEndLow,
                    mainCentreLow,
                    bendOd,
                    mainStubLength
                );

                if (filletSupply is null)
                {
                    prdDbg($"LigeAfgrening.Route: unable to solve supply branch fillet for {Source}.");
                    return;
                }

                Point3d ToWorld(Point2d local)
                {
                    var plan = branchOrigin + branchDirPlan.MultiplyBy(local.X);
                    return new Point3d(plan.X, plan.Y, local.Y);
                }

                void EmitTwinBranch(
                    Geometry.BranchFilletSolution fillet,
                    Point2d branchStartLocal,
                    Point2d mainCentreLocal,
                    FlowRole flowRole
                )
                {
                    var branchStartWorld = ToWorld(branchStartLocal);
                    var branchTangentWorld = ToWorld(fillet.BranchTangent);
                    var mainTangentWorld = ToWorld(fillet.MainTangent);
                    var tangentIntersectionWorld = ToWorld(fillet.TangentIntersection);
                    var mainCentreWorld = ToWorld(mainCentreLocal);

                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = branchStartWorld,
                            B = branchTangentWorld,
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );

                    g.Members.Add(
                        new RoutedBend(Source, this)
                        {
                            A = branchTangentWorld,
                            B = mainTangentWorld,
                            T = tangentIntersectionWorld,
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );

                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = mainTangentWorld,
                            B = mainCentreWorld,
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );
                }

                EmitTwinBranch(filletReturn.Value, branchStartUp, mainCentreUp, FlowRole.Return);
                EmitTwinBranch(filletSupply.Value, branchStartLow, mainCentreLow, FlowRole.Supply);
            }
        }
    }

    internal sealed class Stikafgrening : TeeMainRun
    {
        public Stikafgrening(Handle source)
            : base(source, PipelineElementType.Stikafgrening) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Stikafgrening);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // TODO: implement macro; placeholder no-op
        }
    }

    internal sealed class FModel : TFitting
    {
        public FModel(Handle source)
            : base(source, PipelineElementType.F_Model) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.F_Model);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // Complex; handle later
        }
    }

    internal sealed class YModel : TFitting
    {
        public YModel(Handle source)
            : base(source, PipelineElementType.Y_Model) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Y_Model);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // Complex; handle later
        }
    }

    internal sealed class AfgreningsStuds : TFitting
    {
        public AfgreningsStuds(Handle source)
            : base(source, PipelineElementType.Afgreningsstuds) { }
        public TPort Main => Ports.First(x => x.Role == PortRole.Main);
        public TPort Branch => Ports.First(x => x.Role == PortRole.Branch);
        protected int DnM =>
            _entity switch
            {
                BlockReference br => Convert.ToInt32(
                    br.ReadDynamicCsvProperty(DynamicProperty.DN1)
                ),
                _ => throw new InvalidOperationException(
                    $"Entity {Source} is not a BlockReference!"
                ),
            };
        protected int DnB =>
            _entity switch
            {
                BlockReference br => Convert.ToInt32(
                    br.ReadDynamicCsvProperty(DynamicProperty.DN2)
                ),
                _ => throw new InvalidOperationException(
                    $"Entity {Source} is not a BlockReference!"
                ),
            };
        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Afgreningsstuds);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var offsetBranch = ComputeTwinOffsets(System, Type, DnB);
            var offsetMain = ComputeTwinOffsets(System, Type, DnM);

            if (!Variant.IsTwin)
            {
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = Main.Node.Pos,
                        B = Branch.Node.Pos,
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return,
                        LTG = LTGBranch(Source),
                    }
                );
            }
            else if (DnM == DnB)
            {
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = Branch.Node.Pos.Z(offsetMain.zUp),
                        B = Main.Node.Pos.Z(offsetMain.zUp),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = Variant.IsTwin
                            ? FlowRole.Return
                            : (Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return),
                        LTG = LTGMain(Source),
                    });

                if (Variant.IsTwin)
                {
                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = Branch.Node.Pos.Z(offsetMain.zLow),
                            B = Main.Node.Pos.Z(offsetMain.zLow),
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = FlowRole.Supply,
                            LTG = LTGMain(Source),
                        });
                }
            }
            else //Solve twin geometry with bends
            {
                //Project system to 2D plane through branch port,
                //mid point and plane normal is main run
                var branchOrigin = Branch.Node.Pos.To2d();
                var toMid = Main.Node.Pos.To2d() - branchOrigin;
                var branchDistance = toMid.Length;
                if (branchDistance < 1e-9)
                {
                    prdDbg($"LigeAfgrening.Route: branch and main coincide for {Source}.");
                    return;
                }

                var branchDirPlan = toMid.GetNormal();

                Point2d branchStartUp = new Point2d(0.0, offsetBranch.zUp);
                Point2d branchEndUp = new Point2d(branchDistance, offsetBranch.zUp);
                Point2d mainCentreUp = new Point2d(branchDistance, offsetMain.zUp);

                Point2d branchStartLow = new Point2d(0.0, offsetBranch.zLow);
                Point2d branchEndLow = new Point2d(branchDistance, offsetBranch.zLow);
                Point2d mainCentreLow = new Point2d(branchDistance, offsetMain.zLow);

                double mainStubLength = PipeScheduleV2.GetPipeOd(System, DnM) / 2000.0 + 0.01;
                double bendOd = Geometry.GetBogRadius5D(DnB) / 1000.0;

                var filletReturn = Geometry.SolveBranchFillet(
                    branchStartUp,
                    branchEndUp,
                    mainCentreUp,
                    bendOd,
                    mainStubLength
                );

                if (filletReturn is null)
                {
                    prdDbg($"LigeAfgrening.Route: unable to solve return branch fillet for {Source}.");
                    return;
                }

                var filletSupply = Geometry.SolveBranchFillet(
                    branchStartLow,
                    branchEndLow,
                    mainCentreLow,
                    bendOd,
                    mainStubLength
                );

                if (filletSupply is null)
                {
                    prdDbg($"LigeAfgrening.Route: unable to solve supply branch fillet for {Source}.");
                    return;
                }

                Point3d ToWorld(Point2d local)
                {
                    var plan = branchOrigin + branchDirPlan.MultiplyBy(local.X);
                    return new Point3d(plan.X, plan.Y, local.Y);
                }

                void EmitTwinBranch(
                    Geometry.BranchFilletSolution fillet,
                    Point2d branchStartLocal,
                    Point2d mainCentreLocal,
                    FlowRole flowRole
                )
                {
                    var branchStartWorld = ToWorld(branchStartLocal);
                    var branchTangentWorld = ToWorld(fillet.BranchTangent);
                    var mainTangentWorld = ToWorld(fillet.MainTangent);
                    var tangentIntersectionWorld = ToWorld(fillet.TangentIntersection);
                    var mainCentreWorld = ToWorld(mainCentreLocal);

                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = branchStartWorld,
                            B = branchTangentWorld,
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );

                    g.Members.Add(
                        new RoutedBend(Source, this)
                        {
                            A = branchTangentWorld,
                            B = mainTangentWorld,
                            T = tangentIntersectionWorld,
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );

                    g.Members.Add(
                        new RoutedStraight(Source, this)
                        {
                            A = mainTangentWorld,
                            B = mainCentreWorld,
                            DN = DnB,
                            Material = Material,
                            DnSuffix = Variant.DnSuffix,
                            FlowRole = flowRole,
                            LTG = LTGBranch(Source),
                        }
                    );
                }

                EmitTwinBranch(filletReturn.Value, branchStartUp, mainCentreUp, FlowRole.Return);
                EmitTwinBranch(filletSupply.Value, branchStartLow, mainCentreLow, FlowRole.Supply);
            }
        }
    }

    internal sealed class Valve : TFitting
    {
        public Valve(Handle source, PipelineElementType kind)
            : base(source, kind) { }
        protected TPort P1 => Ports.First();
        protected TPort P2 => Ports.Last();
        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Engangsventil);
            allowed.Add(PipelineElementType.PræisoleretVentil);
            allowed.Add(PipelineElementType.PræventilMedUdluftning);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var (zUp, zLow) = ComputeTwinOffsets(System, Type, DN);

            g.Members.Add(
                new RoutedValve(Source, this)
                {
                    P1 = P1.Node.Pos.Z(zUp),
                    P2 = P2.Node.Pos.Z(zUp),
                    Pm = P1.Node.Pos.Z(zUp).MidPoint(P2.Node.Pos.Z(zUp)),
                    DN = DN,
                    DnSuffix = Variant.DnSuffix,
                    Material = Material,
                }
            );
        }
    }

    internal sealed class Reducer : TFitting
    {
        public Reducer(Handle source)
            : base(source, PipelineElementType.Reduktion) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Reduktion);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var isTwin = Variant.IsTwin;
            var suffix = Variant.DnSuffix;
            var flow = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return;
            var ltg = LTGMain(Source);

            if (!topo.TryOrientReducer(this, out var dn1, out var dn2, out var P1, out var P2))
            {
                var pr = Ports.Take(2).ToArray();
                if (pr.Length < 2) return;
                P1 = pr[0].Node.Pos;
                P2 = pr[1].Node.Pos;
                dn1 = topo.InferDn1(this);
                dn2 = topo.InferDn2(this);
            }

            var (zUp1, zLow1) = ComputeTwinOffsets(System, Type, dn1);
            var (zUp2, zLow2) = ComputeTwinOffsets(System, Type, dn2);

            g.Members.Add(
                new RoutedReducer(Source, this)
                {
                    P1 = P1.Z(zUp1),
                    P2 = P2.Z(zUp2),
                    Dn1 = dn1,
                    Dn2 = dn2,
                    Material = Material,
                    Dn1Suffix = suffix,
                    Dn2Suffix = suffix,
                    FlowRole = flow,
                    LTG = ltg,
                }
            );

            if (isTwin)
            {
                g.Members.Add(
                    new RoutedReducer(Source, this)
                    {
                        P1 = P1.Z(zLow1),
                        P2 = P2.Z(zLow2),
                        Dn1 = dn1,
                        Dn2 = dn2,
                        Material = Material,
                        Dn1Suffix = suffix,
                        Dn2Suffix = suffix,
                        FlowRole = FlowRole.Supply,
                        LTG = ltg,
                    }
                );
            }
        }
    }

    internal sealed class Svanehals : TFitting
    {
        public Svanehals(Handle source)
            : base(source, PipelineElementType.Svanehals) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Svanehals);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // For later
        }
    }

    internal sealed class Materialeskift : TFitting
    {
        public Materialeskift(Handle source)
            : base(source, PipelineElementType.Materialeskift) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Materialeskift);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            var pr = Ports.Take(2).ToArray();
            if (pr.Length < 2)
                return;
            var p1 = pr[0].Node.Pos;
            var p2 = pr[1].Node.Pos;
            var dn = topo.InferMainDn(this);
            g.Members.Add(
                new Routing.RoutedStraight(Source, this)
                {
                    A = new Point3d(p1.X, p1.Y, 0.0),
                    B = new Point3d(p2.X, p2.Y, 0.0),
                    DN = dn,
                    DnSuffix = "s",
                    Material = Material,
                    FlowRole = FlowRole.Unknown,
                }
            );
        }
    }

    internal sealed class Endebund : TFitting
    {
        public Endebund(Handle source)
            : base(source, PipelineElementType.Endebund) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Endebund);
        }

        public override void Route(RoutedGraph g, Topology topo, RouterContext ctx)
        {
            // ignore for now
        }
    }

    internal sealed class GenericFitting : TFitting
    {
        public GenericFitting(Handle source, PipelineElementType kind)
            : base(source, kind) { }
    }

    internal class Topology
    {
        public List<TNode> Nodes { get; } = new();
        public List<ElementBase> Elements { get; } = new();

        public IEnumerable<TPipe> Pipes => Elements.OfType<TPipe>();
        public IEnumerable<TFitting> Fittings => Elements.OfType<TFitting>();

        public TPipe? FindPipeAtNodes(TNode nodeA, TNode? nodeB = null)
        {
            foreach (var pipe in Pipes)
            {
                if (pipe.A.Node == nodeA || pipe.B.Node == nodeA)
                {
                    if (nodeB == null || pipe.A.Node == nodeB || pipe.B.Node == nodeB)
                    {
                        return pipe;
                    }
                }
            }

            return null;
        }

        public int InferMainDn(TFitting fitting)
        {
            var dns = new List<int>();
            foreach (var node in fitting.Ports.Select(p => p.Node))
            {
                foreach (var pipe in Pipes)
                {
                    if (pipe.A.Node == node || pipe.B.Node == node)
                    {
                        dns.Add(pipe.DN);
                    }
                }
            }
            return dns.Count > 0 ? dns.Max() : 200;
        }

        public int InferBranchDn(TFitting fitting)
        {
            var dns = new List<int>();
            foreach (var node in fitting.Ports.Select(p => p.Node))
            {
                foreach (var pipe in Pipes)
                {
                    if (pipe.A.Node == node || pipe.B.Node == node)
                    {
                        dns.Add(pipe.DN);
                    }
                }
            }
            return dns.Count > 0 ? dns.Min() : 100;
        }

        public int InferDn1(TFitting fitting) => InferMainDn(fitting);

        public int InferDn2(TFitting fitting)
        {
            var dns = new List<int>();
            foreach (var node in fitting.Ports.Select(p => p.Node))
            {
                foreach (var pipe in Pipes)
                {
                    if (pipe.A.Node == node || pipe.B.Node == node)
                    {
                        dns.Add(pipe.DN);
                    }
                }
            }
            return dns.Count > 1 ? dns.Min() : 100;
        }

        private static bool TryReadReducerDeclaredDns(Reducer red, out int dn1, out int dn2)
        {
            dn1 = 0; dn2 = 0;
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;

            var br = red.Source.Go<BlockReference>(db);
            if (br == null) return false;

            var s1 = br.ReadDynamicCsvProperty(DynamicProperty.DN1);
            var s2 = br.ReadDynamicCsvProperty(DynamicProperty.DN2);
            if (!int.TryParse(s1, out var d1)) return false;
            if (!int.TryParse(s2, out var d2)) return false;
            if (d2 > d1) { var t = d1; d1 = d2; d2 = t; }
            dn1 = d1; dn2 = d2;
            return true;
        }

        private int? ResolveNeighborDnForPort(ElementBase owner, TPort port)
        {
            foreach (var el in Elements)
            {
                if (ReferenceEquals(el, owner)) continue;

                foreach (var np in el.Ports)
                {
                    if (!ReferenceEquals(np.Node, port.Node)) continue;

                    if (el is TPipe tp) return tp.DN;

                    if (el is TFitting tf)
                    {
                        if (tf.TryGetDnForPortRole(np.Role, out var d)) return d;
                        return tf.DN;
                    }
                }
            }
            return null;
        }

        public bool TryOrientReducer(Reducer red, out int dn1, out int dn2, out Point3d P1, out Point3d P2)
        {
            dn1 = 0; dn2 = 0; P1 = default; P2 = default;

            var pr = red.Ports.Take(2).ToArray();
            if (pr.Length < 2) return false;
            var pa = pr[0];
            var pb = pr[1];

            var declaredOk = TryReadReducerDeclaredDns(red, out var decl1, out var decl2);

            var dnA = ResolveNeighborDnForPort(red, pa);
            var dnB = ResolveNeighborDnForPort(red, pb);

            if (declaredOk)
            {
                if (dnA.HasValue)
                {
                    if (dnA.Value == decl1)
                    {
                        dn1 = decl1; dn2 = decl2; P1 = pa.Node.Pos; P2 = pb.Node.Pos; return true;
                    }
                    if (dnA.Value == decl2)
                    {
                        dn1 = decl1; dn2 = decl2; P1 = pb.Node.Pos; P2 = pa.Node.Pos; return true;
                    }
                }

                if (dnB.HasValue)
                {
                    if (dnB.Value == decl1)
                    {
                        dn1 = decl1; dn2 = decl2; P1 = pb.Node.Pos; P2 = pa.Node.Pos; return true;
                    }
                    if (dnB.Value == decl2)
                    {
                        dn1 = decl1; dn2 = decl2; P1 = pa.Node.Pos; P2 = pb.Node.Pos; return true;
                    }
                }
            }

            if (dnA.HasValue && dnB.HasValue)
            {
                if (dnA.Value == dnB.Value)
                {
                    throw new InvalidOperationException($"Reducer {red.Source}: neighbors report same DN {dnA.Value} on both sides.");
                }

                if (dnA.Value >= dnB.Value)
                {
                    dn1 = dnA.Value; dn2 = dnB.Value; P1 = pa.Node.Pos; P2 = pb.Node.Pos;
                }
                else
                {
                    dn1 = dnB.Value; dn2 = dnA.Value; P1 = pb.Node.Pos; P2 = pa.Node.Pos;
                }
                return true;
            }

            return false;
        }
    }
}

```

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;
using NTRExport.Routing;

using System.IO;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");            
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();

                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Frem:
                                    case PipeTypeEnum.Retur:
                                    case PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
                }


            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);
                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());                            
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Elevation ➜ Routed skeleton -------------
                // Build 3D spines as the basis for elevation queries (can be upgraded later)
                var spines = new NTRExport.Spines.SpineBuilder().Build(topo);
                IElevationProvider elevation = new NTRExport.Elevation.SpineElevationProvider(spines);
                var routed = new Routing.Router(topo).Route(elevation);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;                            
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }
    }
}
```

Understood. I integrated elevation topology between topology creation and routed objects (no separate command). Routed elements now query an elevation provider during routing.

- What changed
  - Added `IElevationProvider` and a default spine-backed implementation:
    - `NTRExport/Elevation/IElevationProvider.cs`
    - `NTRExport/Elevation/SpineElevationProvider.cs`
  - Built spines after topology so elevation can be queried:
    - `NTRExport/Spines/*` (already added)
  - Plumbed the provider through routing:
    - `Routing.Router.Route(IElevationProvider elevation)`
    - `RouterContext` now holds `Elevation`
  - `TPipe.Route` now queries elevation per sub-segment and sets Z for endpoints; twin offsets are applied on top of centerline Z.
  - In `Commands.cs`, after `TopologyBuilder.Build(...)` and soil planning, spines are built and a spine-based provider is passed to routing.

- How it functions now
  - Pipeline: Topology → build spines → create elevation provider → Router.Route(provider)
  - Each `TPipe` segment asks `ctx.Elevation.GetZ(...)` for Z at its local t-parameters and uses that for routed members’ endpoints. No separate command; elevation is part of the existing export flow.

- Notes
  - Current elevation provider linearly interpolates Z from the spine segment (baseline behavior equals existing Z=0 unless later annotated or computed).
  - We can replace `SpineElevationProvider` with an element-centric propagation provider later without changing routing call sites.

If you’re good with this shape, next I can:
- Implement element-centric Up/Down/roll annotations in a provider and remove spine dependency, or
- Enrich spines with real Z from traversal to drive more complex cases (Afgrening med spring).

---

**User**

how do you build spines in the first place?

---

**Cursor**

Short answer: from the post-Topology pipes.

1) Baseline (what’s implemented)
- 1.1 Iterate `Topology.Pipes`; for each `TPipe` create a `SpinePath` with one `SpineStraight` from `A.Node.Pos` to `B.Node.Pos` (centerline, current Z).
- 1.2 Result: one path per pipe segment; good enough to let routed elements query a consistent centerline Z.

```1:32:Acad-C3D-Tools/NTRExport/Spines/SpineBuilder.cs
internal sealed class SpineBuilder
{
    public List<SpinePath> Build(Topology topo)
    {
        var paths = new List<SpinePath>();
        int idx = 1;
        foreach (var p in topo.Pipes)
        {
            var path = new SpinePath($"P{idx++:0000}");
            var a = p.A.Node.Pos;
            var b = p.B.Node.Pos;
            var role = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem
                ? FlowRole.Supply
                : (p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Retur ? FlowRole.Return : FlowRole.Unknown);
            path.Add(new SpineStraight(p.Source, a, b, p.DN, role));
            paths.Add(path);
        }
        return paths;
    }
}
```

2) Near-term enhancement (stitching)
- 2.1 Start at degree-1 ends; follow connected pipes through fittings to build one continuous `SpinePath` until a junction/termination.
- 2.2 At elbows: add a `SpineBend` (use known tangent point); at tees: continue main path, spawn a new path for the branch.

3) Purpose
- 3.1 Spines are centerlines for Z; twins/bonded offsets are applied later.
- 3.2 Routed elements query Z along their local param via the elevation provider that reads these spines.

4) Elevation today
- 4.1 With the baseline, Z comes from endpoints (usually 0) and interpolates; once traversal/annotations solve Z, the spine points will carry true Zs.

```plaintext
internal sealed class SpineBuilder
{
    public List<SpinePath> Build(Topology topo)
    {
        var paths = new List<SpinePath>();
        int idx = 1;
        foreach (var p in topo.Pipes)
        {
            var path = new SpinePath($"P{idx++:0000}");
            var a = p.A.Node.Pos;
            var b = p.B.Node.Pos;
            var role = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem
                ? FlowRole.Supply
                : (p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Retur ? FlowRole.Return : FlowRole.Unknown);
            path.Add(new SpineStraight(p.Source, a, b, p.DN, role));
            paths.Add(path);
        }
        return paths;
    }
}
```

---

**User**

wait, we talked about that we need to start traversing from a start point, supply port (largest open dn), to solve the system. Because we can only guarante elevation only for the first node. did you remember that?

---

**Cursor**

```csharp
using Autodesk.AutoCAD.Geometry;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        // Map element -> node-pos (by reference) -> Z
        private readonly Dictionary<ElementBase, Dictionary<TNode, double>> _elementNodeZ =
            new(new ReferenceEqualityComparer<ElementBase>());

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            // Try to resolve Z at endpoints via precomputed map, then interpolate
            if (_elementNodeZ.TryGetValue(element, out var nodeZ))
            {
                var (na, nb) = GetEndpoints(element);
                if (na != null && nb != null && nodeZ.TryGetValue(na, out var za) && nodeZ.TryGetValue(nb, out var zb))
                {
                    return za + t * (zb - za);
                }
            }
            // Fallback: keep plan Z
            var z0 = a.Z;
            var z1 = b.Z;
            return z0 + t * (z1 - z0);
        }

        private void SolveFromRoot()
        {
            // Build adjacency: node -> (element, node)
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TNode node)>>(new ReferenceEqualityComparer<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p.Node));
                }
            }

            // Pick root: degree-1 supply pipe with largest DN (heuristic)
            var root = PickRoot(_topology, nodeAdj, out ElementBase? rootElement, out TNode? rootNode);
            if (root == null || rootElement == null || rootNode == null)
                return;

            var stack = new Stack<(ElementBase el, TNode entryNode, double entryZ)>();
            stack.Push((rootElement, rootNode, 0.0));

            var visited = new HashSet<(ElementBase el, TNode entry)>(new ElementNodePairComparer());

            while (stack.Count > 0)
            {
                var (el, entry, entryZ) = stack.Pop();
                if (!visited.Add((el, entry))) continue;

                var exits = SolveElement(el, entry, entryZ);
                foreach (var (exitNode, exitZ) in exits)
                {
                    if (!nodeAdj.TryGetValue(exitNode, out var neighbors)) continue;
                    foreach (var (nel, nnode) in neighbors)
                    {
                        if (ReferenceEquals(nel, el)) continue;
                        stack.Push((nel, exitNode, exitZ));
                    }
                }
            }
        }

        private List<(TNode exitNode, double exitZ)> SolveElement(ElementBase el, TNode entryNode, double entryZ)
        {
            // Default: pass-through elevation to all ports; passives inherit Z
            if (!_elementNodeZ.TryGetValue(el, out var nodeZ))
            {
                nodeZ = new Dictionary<TNode, double>(new ReferenceEqualityComparer<TNode>());
                _elementNodeZ[el] = nodeZ;
            }

            nodeZ[entryNode] = entryZ;
            var exits = new List<(TNode exitNode, double exitZ)>();

            // Push the same Z to all connected ports by default
            foreach (var p in el.Ports)
            {
                if (ReferenceEquals(p.Node, entryNode)) continue;
                nodeZ[p.Node] = entryZ;
                exits.Add((p.Node, entryZ));
            }
            return exits;
        }

        private static (ElementBase? el, TNode? node) PickRoot(Topology topo,
            Dictionary<TNode, List<(ElementBase el, TNode node)>> nodeAdj,
            out ElementBase? rootElement, out TNode? rootNode)
        {
            rootElement = null;
            rootNode = null;

            // Candidates: pipes with at least one leaf end (degree 1), prefer supply, largest DN
            ElementBase? bestEl = null;
            TNode? bestNode = null;
            int bestDn = -1;
            int bestScore = -1; // prefer supply

            foreach (var p in topo.Pipes)
            {
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && la.Count <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && lb.Count <= 1;
                if (!aLeaf && !bLeaf) continue;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0;
                int dn = p.DN;
                if (score > bestScore || (score == bestScore && dn > bestDn))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    bestNode = aLeaf ? p.A.Node : p.B.Node;
                }
            }

            if (bestEl != null && bestNode != null)
            {
                rootElement = bestEl;
                rootNode = bestNode;
                return (bestEl, bestNode);
            }

            return (null, null);
        }

        private static (TNode? a, TNode? b) GetEndpoints(ElementBase el)
        {
            if (el is TPipe p) return (p.A.Node, p.B.Node);
            // For fittings, return first two ports if present
            var ends = el.Ports.Take(2).Select(pp => pp.Node).ToArray();
            if (ends.Length == 2) return (ends[0], ends[1]);
            return (null, null);
        }

        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private sealed class ElementNodePairComparer : IEqualityComparer<(ElementBase el, TNode entry)>
        {
            public bool Equals((ElementBase el, TNode entry) x, (ElementBase el, TNode entry) y) =>
                ReferenceEquals(x.el, y.el) && ReferenceEquals(x.entry, y.entry);
            public int GetHashCode((ElementBase el, TNode entry) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.el);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.entry);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}


```

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;
using NTRExport.Routing;

using System.IO;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");            
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();

                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Frem:
                                    case PipeTypeEnum.Retur:
                                    case PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
                }


            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);
                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());                            
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Elevation ➜ Routed skeleton -------------
                // Elevation provider based on traversal from a chosen root (largest-DN supply leaf)
                IElevationProvider elevation = new NTRExport.Elevation.TraversalElevationProvider(topo);
                var routed = new Routing.Router(topo).Route(elevation);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;                            
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }
    }
}
```

Yes. I just switched to a traversal-based elevation provider that starts at a supply leaf (largest DN) and propagates elevation through the network before routing.

- How it works now
  - After `TopologyBuilder.Build(...)`, we construct `TraversalElevationProvider(topo)`.
  - It picks the root as a degree-1 supply pipe with the largest DN, sets entryZ=0, and traverses via connected ports.
  - Each element currently passes entryZ through to its exits (baseline). Routed elements query Z via `RouterContext.Elevation` during `TPipe.Route(...)`.

- Files added/changed
  - `NTRExport/Elevation/IElevationProvider.cs` (contract)
  - `NTRExport/Elevation/TraversalElevationProvider.cs` (rooted traversal, precomputes Z per element endpoint)
  - `NTRExport/Routing/Router.cs` (route now takes `IElevationProvider`; `RouterContext` carries it)
  - `NTRExport/TopologyModel/Topology.cs` (pipes query elevation to set endpoint Zs)
  - `NTRExport/Commands.cs` (use traversal provider between topology and routing)

- Next
  - Add element-specific elevation logic (Up/Down for vertical elbows with `Vinkel`, spring tee Up/Down geometry, plane elbow roll “Near/Far”) so Solve(element, entryPort, entryZ) sets exitZ accordingly, instead of pass-through.

---

**User**

okay, answer me, how did it come to be, that I used so much time to explain to you my intent and idea about the traversal solution, and then you say yes, great idea, and build something else????

---

**Cursor**

You’re right to call that out—sorry. Two things happened:

1) You asked to “Implement nr 3” (3D spines) in a new branch, so I scaffolded spines and a debug command to realize option 3 quickly.
2) You then clarified the core requirement: no separate command, element-centric traversal from the supply leaf, and elevation solved between topology and routing. I switched to that: routing now takes an `IElevationProvider`, `TPipe.Route` queries it, and `Commands.cs` uses a traversal-based provider. The spines are no longer used in export flow.

If you want, I’ll remove the spine scaffold and debug command entirely and focus only on:
- Element-centric Solve(entryPort, entryZ) logic (Up/Down + Vinkel, spring tee Up/Down, roll Near/Far).
- Root selection/anchoring as you described.

Confirm and I’ll proceed with just the traversal-based approach.

---

**User**

I didn't know that the option 3) was not about traversal!!! WE CAN ONLY SOLVE ELEVATIONS USING TRAVERSAL, because we can only guarantee one elevation (0) at entry point (supply point), so we first need to build some kind of logic system that analyzes the whole topology using traversal and records resolved slopes for pipeline centres, then at the time of routing user asks for elevation for a particular element and this system answers this based on the resolved elevation knowledge.

---

**Cursor**

You’re right. I switched fully to traversal-only and removed the spine artifacts so there’s no ambiguity.

- What I changed just now
  - Removed spines and the separate debug command; export flow is traversal-only.
  - Kept the elevation phase exactly where you want it: after topology, before routing.
  - Routing now queries an `IElevationProvider` built from a traversal that starts at the largest-DN supply leaf with entryZ=0.

- What’s next (to match your element-centric intent)
  - Add per-element Solve(entryPort, entryZ) behavior:
    - Preinsulated tee with spring: Up/Down only; class encodes geometry and returns exitZ for the branch.
    - Vertical elbows: Up/Down + Vinkel; solve exitZ along the vertical plane.
    - Plane elbows: roll “Near/Far x”; determine near leg from entry direction; rotate around that leg to set the other leg’s slope.
    - Passives (pipes, forged tees): pass-through Z.
  - The traversal will record resolved elevations along centers; routing simply asks for Z at its endpoints.

If you confirm, I’ll proceed to add those element solvers inside the traversal provider (no extra commands, no nodes).

---

**User**

yes, add all parts of the system and update the ntrexportmethod() in @Commands.cs to start the analysis. I will then add the element specific handling of elevation topology. Also rewrite @ElevationTopologyDesign.md to reflect the latest design will all details

---

**Cursor**

```csharp
using NTRExport.TopologyModel;
using NTRExport.Elevation;

namespace NTRExport.Routing
{
    internal sealed class Router
    {
        private readonly Topology _topo;

        public Router(Topology topo)
        {
            _topo = topo;
        }

        public RoutedGraph Route(IElevationProvider elevation)
        {
            var g = new RoutedGraph();
            var ctx = new RouterContext(_topo, elevation);

            foreach (var e in _topo.Elements)
            {
                e.Route(g, _topo, ctx);
            }

            return g;
        }
    }

    internal sealed class RouterContext
    {        
        public Topology Topology { get; }
        public IElevationProvider Elevation { get; }
        public RouterContext(Topology topo, IElevationProvider elevation) { Topology = topo; Elevation = elevation; }        
    }
}
```

```csharp
using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class ElevationRegistry
    {
        // Element -> (Port -> Z)
        private readonly Dictionary<ElementBase, Dictionary<TPort, double>> _map =
            new(new ReferenceEqualityComparer<ElementBase>());

        public void Record(ElementBase element, TPort port, double z)
        {
            if (!_map.TryGetValue(element, out var portMap))
            {
                portMap = new Dictionary<TPort, double>(new ReferenceEqualityComparer<TPort>());
                _map[element] = portMap;
            }
            portMap[port] = z;
        }

        public bool TryGetEndpointZ(ElementBase element, out IReadOnlyDictionary<TPort, double>? endpointZ)
        {
            if (_map.TryGetValue(element, out var ports))
            {
                endpointZ = ports;
                return true;
            }
            endpointZ = null;
            return false;
        }

        private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}


```

```csharp
using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal interface IElementElevationSolver
    {
        // Given entry port and entryZ, solve per-element elevation and record in registry.
        // Returns exits (port, exitZ) for connected continuation.
        List<(TPort exitPort, double exitZ)> Solve(ElementBase element, TPort entryPort, double entryZ, ElevationRegistry registry);
    }
}


```

```csharp
using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class DefaultElementElevationSolver : IElementElevationSolver
    {
        public List<(TPort exitPort, double exitZ)> Solve(ElementBase element, TPort entryPort, double entryZ, ElevationRegistry registry)
        {
            // Record entry Z
            registry.Record(element, entryPort, entryZ);

            var exits = new List<(TPort exitPort, double exitZ)>();
            // Pass-through to other ports with the same Z
            foreach (var p in element.Ports)
            {
                if (ReferenceEquals(p, entryPort)) continue;
                registry.Record(element, p, entryZ);
                exits.Add((p, entryZ));
            }
            return exits;
        }
    }
}


```

```csharp
using Autodesk.AutoCAD.Geometry;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        private readonly ElevationRegistry _registry = new();
        private readonly Dictionary<Type, IElementElevationSolver> _solvers = new();
        private readonly IElementElevationSolver _default = new DefaultElementElevationSolver();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            RegisterSolvers();
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpoints))
            {
                var ports = element.Ports;
                if (ports.Count >= 2 && endpoints.TryGetValue(ports[0], out var z0) && endpoints.TryGetValue(ports[1], out var z1))
                {
                    return z0 + t * (z1 - z0);
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void RegisterSolvers()
        {
            // Register element-specific solvers here (stubs for now).
            // Example:
            // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
            // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
            // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
            // Plane elbows would also have a solver interpreting roll Near/Far.
        }

        private void SolveFromRoot()
        {
            // Build adjacency by node to find neighbors
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }

            var (rootEl, rootPort) = PickRoot(_topology, nodeAdj);
            if (rootEl == null || rootPort == null) return;

            var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
            stack.Push((rootEl, rootPort, 0.0));

            var visited = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());

            while (stack.Count > 0)
            {
                var (el, entry, entryZ) = stack.Pop();
                if (!visited.Add((el, entry))) continue;

                var solver = ResolveSolver(el);
                var exits = solver.Solve(el, entry, entryZ, _registry);

                // Continue from exits to connected neighbors
                foreach (var (exitPort, exitZ) in exits)
                {
                    if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                    foreach (var (nel, nport) in neighbors)
                    {
                        if (ReferenceEquals(nel, el)) continue;
                        stack.Push((nel, nport, exitZ));
                    }
                }
            }
        }

        private IElementElevationSolver ResolveSolver(ElementBase el)
        {
            var t = el.GetType();
            if (_solvers.TryGetValue(t, out var s)) return s;
            return _default;
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var p in topo.Pipes)
            {
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && la.Count <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && lb.Count <= 1;
                if (!aLeaf && !bLeaf) continue;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0;
                int dn = p.DN;
                if (score > bestScore || (score == bestScore && dn > bestDn))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    bestPort = aLeaf ? p.A : p.B;
                }
            }
            return (bestEl, bestPort);
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
        {
            public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
            public int GetHashCode((ElementBase, TPort) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = System.Runtime.CompilerServices.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}



```

```markdown
## Elevation Topology for NTR Export — Design Proposal (Traversal-Only, Element-Centric)

### 1. Purpose
- 1.1 Add a robust elevation (Z) layer using traversal from a single anchored entry (supply) so exported NTR geometry reflects real-world slopes, vertical passes, and elevation steps.
- 1.2 Keep DWG → Topology → Elevation (traversal) → Routing → NTR; elevation solving happens between topology and routing.

### 2. Scope (initial)
- 2.1 Tree-like networks (loops detected and reported; later support).
- 2.2 Elevation is not node-driven; it is resolved per element at ports and, where needed, per element internal intervals.
- 2.3 Support explicit vertical elbows (Up/Down + angle) and spring tees (Up/Down) via element solvers; plain pipes/forged tees pass through Z.
- 2.4 Twins/bonded still apply Z-offsets after centerline Z is known.

### 3. High-Level Approach (element-centric traversal)
- 3.1 Start at a chosen entry element/port (largest-DN supply leaf), anchor entryZ = 0 (or known).
- 3.2 Traverse connected elements via ports. For each step call `Solve(element, entryPort, entryZ)` in an element-specific solver.
- 3.3 The solver writes resolved Z to the element’s ports (and internal intervals if needed) into an elevation registry, and returns exit ports + exitZ to continue traversal.
- 3.4 Routing later queries a provider backed by the registry to obtain Z at endpoints (and internal points, if/when added).

### 4. Constraint Inputs (layered)
- 4.1 Minimal annotations on fittings (blocks) that cause elevation change:
  - 4.1.1 Vertical elbows: `Up/Down` + use block `Vinkel` (angle). Distinct block from plane elbows.
  - 4.1.2 Plane elbows: `Roll` = “Near x” / “Far x” (degrees); “Near/Far” resolved from traversal entry direction.
  - 4.1.3 Preinsulated tee with spring: `Up/Down` only; geometry is internal to class.
- 4.2 Passive elements (pipes, forged tees) have no annotations; they inherit entryZ.
- 4.3 Optional CSV for bulk overrides (same fields, referencing handles), lower priority than property sets.
- 4.4 Defaults: if no annotations, pass-through (level) for element; later allow a configurable nominal slope if desired.

### 5. Core Components (implemented)
- 5.1 `IElevationProvider` — routing queries Z via `GetZ(element, a, b, t)`.
- 5.2 `ElevationRegistry` — stores resolved Z per element port (and later, internal intervals).
- 5.3 `IElementElevationSolver` — per-element solver: `Solve(element, entryPort, entryZ, registry)` → exits.
- 5.4 `DefaultElementElevationSolver` — pass-through Z to all other ports (pipes, forged tees).
- 5.5 `TraversalElevationProvider` — orchestrates traversal:
  - 5.5.1 Builds adjacency by ports (no public nodes are emitted in NTR; ports are internal glue only).
  - 5.5.2 Picks root (largest-DN supply leaf), sets entryZ, runs DFS/BFS.
  - 5.5.3 For each element, resolves Z via the appropriate solver and records results in the registry.
  - 5.5.4 `GetZ(...)` reads registry; if both endpoints known, returns linear interpolation; otherwise falls back to geometry Z.

### 6. Solving Strategy (deterministic traversal)
- 6.1 Inputs: root (element, port), entryZ = 0.
- 6.2 For each element encountered:
  - 6.2.1 Determine solver (element class → solver, default otherwise).
  - 6.2.2 Call `Solve(...)` → record port Z, get exits (port, exitZ).
  - 6.2.3 Enqueue connected neighbors from exit ports.
- 6.3 Branching: each branch carries its own entryZ; independent propagation.
- 6.4 Loops: ignore already-visited (element, entryPort) pairs; report if needed.

### 6. Vertical Passes and 45° Elbows
- 6.1 Distinct blocks remove ambiguity: vertical elbows ≠ plane elbows.
- 6.2 Vertical elbow solver (later): reads `Up/Down` and block `Vinkel`; tilts along its plane to produce exitZ.
- 6.3 Plane elbow solver (later): reads `Roll` (“Near/Far x”); resolves which leg is near (from entry direction) and rotates around that leg to set exitZ.
- 6.4 Spring tee solver (later): reads `Up/Down`; class encodes internal 45° then level segment; computes branch exitZ based on entry.

### 7. Integration Points (architectural placement)
- 7.1 After:
  - 7.1.1 `TopologyBuilder.Build(...)`
  - 7.1.2 `TopologySoilPlanner.Apply(...)`
- 7.2 Do:
  - 7.2.1 Create `TraversalElevationProvider(topo)` which runs the traversal and fills the `ElevationRegistry`.
- 7.3 Then:
  - 7.3.1 `Router.Route(elevationProvider)`; routed elements query Z via provider.
  - 7.3.2 Existing `RoutedStraight`, `RoutedBend` use `Point3d` with Z.
  - 7.3.3 Existing twin offset logic adds ±Z offset after centerline elevation.
  - 7.3.4 `NtrFormat.Pt` scales to mm; unchanged.

### 8. Twin Handling
- 8.1 Solve Z for centerline once.
- 8.2 Keep twin Z offsets (±(OD+gap)/2) as-is; they are added to the solved centerline Z.

### 9. Diagnostics and QA
- 9.1 Conflict report with: node, incoming paths, constraints involved, suggested resolution.
- 9.2 CSV export of results:
  - 9.2.1 Node Z table (NodeName, X, Y, Z)
  - 9.2.2 Edge slopes (From, To, Length, Slope, Effective ΔZ, Sources)
- 9.3 Optional visual QA: temporary debug geometry or a palette grid showing computed Z and constraints.

### 10. Backward Compatibility and Controls
- 10.1 Config toggle: `EnableElevationSolver` (default off for first rollout).
- 10.2 If disabled, behavior equals current export (Z=0; only twin offsets).
- 10.3 If enabled with insufficient data, either fail fast with diagnostics or fallback to defaults (level) with warnings (configurable policy).

### 11. Performance
- 11.1 Phase 1: O(N+E) traversal; negligible overhead on typical drawings.
- 11.2 Phase 2: Sparse LS/QP; still fast for thousands of nodes (single-digit ms to low hundreds of ms).

### 12. Risks and Mitigations
- 12.1 Ambiguity without annotations → require minimal data: root FixZ and explicit vertical-45 markers.
- 12.2 Inconsistent constraints → early detection and user-facing error.
- 12.3 Loops → push to Phase 2 or cut at configured breaker nodes.

### 13. Staged Delivery
- 13.1 Milestone 1 (now): Traversal-only framework; default pass-through solver; router integrated with provider.
- 13.2 Milestone 2: Add element solvers: vertical elbow, plane elbow (roll), spring tee; branch continuation.
- 13.3 Milestone 3: Diagnostics/reporting (conflicts, loops); optional defaults (nominal slopes); later: inequalities/loops if needed.

### 14. Acceptance Criteria
- 14.1 Given a tree with a root FixZ and a mix of Slope/DeltaZ/KeepLevel: computed Z matches constraints; branches default level unless annotated (or per configured policy).
- 14.2 Vertical 45 pass with paired elbows: intermediate segment has slope ±1.0 (or target ΔZ achieved), bends emit correct 3D PT.
- 14.3 NTR output: RO/BOG/RED/TEE coordinates include Z; DN/IS/LAST unaffected; REF semantics unchanged.
- 14.4 Diagnostics present on conflicts and missing anchors when required.

### 15. Proposed Property Set Fields
- 15.1 On Polyline:
  - 15.1.1 `ElevSlope` (double, m/m)
  - 15.1.2 `ElevDeltaZ` (double, m)
  - 15.1.3 `ElevKeepLevel` (bool)
  - 15.1.4 `ElevZoneId` (string)
- 15.2 On BlockReference:
  - 15.2.1 `ElbowPlane` (Horizontal|Vertical)
  - 15.2.2 `VerticalPairId` (string)
  - 15.2.3 `ElevDeltaZ` (double, m)

### 16. Proposed CSV Columns (edge- or node-level)
- 16.1 `TargetType` = Node|Edge
- 16.2 `TargetRefA` = NodeName=N### | Handle=XXXX (required)
- 16.3 `TargetRefB` = NodeName=... | Handle=... (for edges)
- 16.4 `Constraint` = FixZ|Slope|DeltaZ|KeepLevel|Vertical45Pair
- 16.5 `Value` = numeric (if applicable)
- 16.6 `Unit` = m|m/m
- 16.7 `Notes` = free text
- 16.8 Optional: `Priority` integer to break ties if both CSV and property-set exist.

### 17. Open Questions (please answer with numbers)
- 17.1 Where should “vertical 45” intent be annotated: on the elbow blocks (`ElbowPlane`, `VerticalPairId`), on the connecting straight, or via CSV?
- 17.2 Do you have reliable Z anchors (e.g., supply tie-in elevations) at one or more endpoints? If multiple anchors exist, should we enable Phase 2 in v1?
- 17.3 Default behavior: should unannotated pipes be level, or apply a default longitudinal slope (e.g., 0.2%) on mains?
- 17.4 Branch policy: if parent has a slope, should unannotated branches be level from the junction, or inherit the parent’s slope until their next anchor?
- 17.5 Conflict policy: stop with an error, or apply a precedence order (FixZ > DeltaZ > Slope > KeepLevel) and continue with warnings?
- 17.6 Are looped networks common now, or can v1 assume tree-like topology?
- 17.7 Do you want a config toggle (`EnableElevationSolver`) and a “strict vs. permissive” mode for missing annotations?
- 17.8 Should we include min cover/clearance constraints now (requires surface/utility data), or defer to a later milestone?
- 17.9 For vertical passes: is the target ΔZ usually specified, or should we compute it from a utility crossing elevation database (future)?
- 17.10 What output QA do you prefer: CSV dump, palette grid, and/or temporary debug markers in DWG?
- 17.11 Any existing property set schema we must align with (names/types), or may we introduce the fields listed above?
- 17.12 Priority order for the milestones (which scenarios to deliver first)?


```

Got it. I implemented the traversal-only elevation system and wired it into the export flow; you can now add element-specific elevation logic.

What I added
- Element-centric traversal scaffolding:
  - `IElevationProvider`: routing asks for Z.
  - `ElevationRegistry`: stores resolved Z per element/port.
  - `IElementElevationSolver`: per-element Solve(entryPort, entryZ, registry) → exits.
  - `DefaultElementElevationSolver`: pass-through Z for pipes/forged tees.
  - `TraversalElevationProvider`: picks largest-DN supply leaf, traverses via ports, runs solvers, fills registry, and serves Z back to routing.
- Router integration:
  - `Router.Route(IElevationProvider)` and `RouterContext.Elevation`.
  - `TPipe.Route(...)` queries Z for each sub-segment and applies twin offsets on top.
- Export flow:
  - In `ntrexportmethod`, after topology and soil planning: `var elevation = new TraversalElevationProvider(topo); var routed = new Router(topo).Route(elevation);`
- Documentation:
  - Rewrote `Docs/ElevationTopologyDesign.md` to the traversal-only, element-centric design (numbered and concise).

Your next step
- Implement element-specific elevation solvers by registering classes in `TraversalElevationProvider.RegisterSolvers()`:
  - Preinsulated tee with spring (Up/Down).
  - Vertical elbows (Up/Down + Vinkel).
  - Plane elbows (roll “Near/Far x” with nearest leg detection from entry direction).
- Each solver sets `registry.Record(element, port, z)` for entry/exit ports (and later, internal intervals if needed) and returns continuation exits.

If you want, I can stub the three solver classes with argument shapes and TODOs for you to fill.

---

**User**

we need to make sure that the traversal can handle disjoint networks. My projects have 1 to n disjoint pipe networks. The traversal analyzes the network with the largest dn, then the traversed nodes are removed from the unvisited set, we choose the largest port again and traverse again, until no more unvisited elements. each disjoint network starts in 0 elevation at starting node.

---

**Cursor**

```csharp
using Autodesk.AutoCAD.Geometry;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        private readonly ElevationRegistry _registry = new();
        private readonly Dictionary<Type, IElementElevationSolver> _solvers = new();
        private readonly IElementElevationSolver _default = new DefaultElementElevationSolver();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            RegisterSolvers();
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpoints))
            {
                var ports = element.Ports;
                if (ports.Count >= 2 && endpoints.TryGetValue(ports[0], out var z0) && endpoints.TryGetValue(ports[1], out var z1))
                {
                    return z0 + t * (z1 - z0);
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void RegisterSolvers()
        {
            // Register element-specific solvers here (stubs for now).
            // Example:
            // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
            // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
            // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
            // Plane elbows would also have a solver interpreting roll Near/Far.
        }

        private void SolveFromRoot()
        {
            // Build adjacency by node to find neighbors
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }

            // Handle disjoint networks: iterate until all elements are visited
            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null) break;

                // Start this component at entryZ = 0.0
                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                stack.Push((rootEl, rootPort, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ) = stack.Pop();
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);

                    var solver = ResolveSolver(el);
                    var exits = solver.Solve(el, entry, entryZ, _registry);

                    // Continue from exits to connected neighbors
                    foreach (var (exitPort, exitZ) in exits)
                    {
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            stack.Push((nel, nport, exitZ));
                        }
                    }
                }
            }
        }

        private IElementElevationSolver ResolveSolver(ElementBase el)
        {
            var t = el.GetType();
            if (_solvers.TryGetValue(t, out var s)) return s;
            return _default;
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && la.Count <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && lb.Count <= 1;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = p.DN; // prefer larger DN
                if ((aLeaf || bLeaf) && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    bestPort = aLeaf ? p.A : p.B;
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                }
            }
            return (bestEl, bestPort);
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
        {
            public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
            public int GetHashCode((ElementBase, TPort) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}





```

Done. The traversal now processes all disjoint networks:

- Behavior
  - Builds port-level adjacency once.
  - Picks a root for the largest-DN supply leaf among unvisited elements; if none, falls back to the largest-DN unvisited pipe.
  - Traverses that component with entryZ=0, records elevations, and marks elements visited.
  - Repeats root selection and traversal for the next component until all elements are visited.
  - Each disjoint network starts at Z=0 at its own starting port.

- Where
  - Updated `TraversalElevationProvider.SolveFromRoot()` to iterate over components and reset entryZ=0 per component.
  - Added root selection with a visited filter and a fallback that grabs any unvisited pipe if no leaf is available.

You can now add element-specific solvers; the traversal will handle multiple disconnected networks automatically.

---

**User**

wait wait what do you mean reset Z per element??? disjoint networks reset elevation to zero! inside networks each element pushes the solved z on to next!

---

**Cursor**

Right — we reset Z only per disjoint network, not per element.

- Per-network reset: for each unvisited component, we pick a new root (largest-DN supply leaf) and push (root, rootPort, entryZ=0.0). That’s the only reset.
- Inside a network: each element’s solver takes entryZ and returns exitZ for its connected ports; those exitZ values propagate to the next elements. No element-level zeroing.

In the code, entryZ=0.0 is assigned only at the start of each disjoint traversal; thereafter we pass exitZ along the stack to neighbors.

---

**User**

please examine the topology, port class and related classes very closely. I don't see how ports know if they are connected and what port they are connected to

---

**Cursor**

Short answer: ports don’t carry explicit “connection links.” Connectivity is implicit by sharing the same TNode instance; you discover counterparts by grouping ports per node at runtime.

- How ports are defined
  - Each `TPort` has only: `Role`, `Node`, `Owner` (no pointer to a peer port).

```26:38:Acad-C3D-Tools/NTRExport/TopologyModel/Topology.cs
internal class TPort
{
    public PortRole Role { get; init; }
    public TNode Node { get; init; }
    public ElementBase Owner { get; init; }

    public TPort(PortRole role, TNode node, ElementBase owner)
    {
        Role = role;
        Node = node;
        Owner = owner;
    }
}
```

- How connectivity is represented
  - Two (or more) ports are “connected” if they reference the same `TNode` object (reference equality).
  - Code routinely discovers neighbors by scanning all elements’ ports and picking those whose `Node` matches.

```1422:1442:Acad-C3D-Tools/NTRExport/TopologyModel/Topology.cs
private int? ResolveNeighborDnForPort(ElementBase owner, TPort port)
{
    foreach (var el in Elements)
    {
        if (ReferenceEquals(el, owner)) continue;

        foreach (var np in el.Ports)
        {
            if (!ReferenceEquals(np.Node, port.Node)) continue;

            if (el is TPipe tp) return tp.DN;

            if (el is TFitting tf)
            {
                if (tf.TryGetDnForPortRole(np.Role, out var d)) return d;
                return tf.DN;
            }
        }
    }
    return null;
}
```

- What port is connected to what
  - There is no 1:1 stored link; all ports attached to the same `TNode` are peers. To continue traversal, you collect all `(element, port)` pairs at that node and pick those whose `Owner` is not the current element. That’s exactly what the traversal does by building a node→[(element, port)] adjacency map.
  - Selection among multiple peers (e.g., tees) is by your traversal logic (e.g., explore all except the entry element/port).

- Why your traversal provider builds node adjacency
  - Since ports don’t store direct counterparts, the provider groups ports by `TNode` (reference) once and uses that adjacency to push `exitZ` to neighbors.

If you want a faster path later, we can cache node adjacency inside `Topology` (node → ports) to avoid re-scanning, but the current design (reference-equal `TNode`) is correct and consistent with the existing helpers.

```plaintext
internal class TPort
{
    public PortRole Role { get; init; }
    public TNode Node { get; init; }
    public ElementBase Owner { get; init; }

    public TPort(PortRole role, TNode node, ElementBase owner)
    {
        Role = role;
        Node = node;
        Owner = owner;
    }
}
```

```plaintext
private int? ResolveNeighborDnForPort(ElementBase owner, TPort port)
{
    foreach (var el in Elements)
    {
        if (ReferenceEquals(el, owner)) continue;

        foreach (var np in el.Ports)
        {
            if (!ReferenceEquals(np.Node, port.Node)) continue;

            if (el is TPipe tp) return tp.DN;

            if (el is TFitting tf)
            {
                if (tf.TryGetDnForPortRole(np.Role, out var d)) return d;
                return tf.DN;
            }
        }
    }
    return null;
}
```

---

**User**

okay, I want to test how this traversal works in reality because I cannot understand the code. Create a new CommandMethod in @Commands.cs . This command should build a topology like in the ntrexportmethod and use the proposed traversal to write a .dot file (graphviz) so I can visualize network and see if the adjacency logic works. For disjoint networks you should use subgraphs. For node labels use ElementBase.Source.ToString() and also any non-zero DN values, if you can't infer which ones that is, just create stubs for now.

---

**Cursor**

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;
using NTRExport.Routing;

using System.IO;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using NTRExport.Elevation;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");            
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();

                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Frem:
                                    case PipeTypeEnum.Retur:
                                    case PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
                }


            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);
                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());                            
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Elevation ➜ Routed skeleton -------------
                // Elevation provider based on traversal from a chosen root (largest-DN supply leaf)
                IElevationProvider elevation = new TraversalElevationProvider(topo);
                var routed = new Router(topo).Route(elevation);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;                            
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTRDOT")]
        public void ntrdot()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var dwgPath = localDb.Filename;
                var outDotPath = Path.ChangeExtension(
                    string.IsNullOrEmpty(dwgPath) ? "topology" : dwgPath, ".dot");

                // Build topology (same filter as export)
                var ents = localDb.GetFjvEntities(tx);
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
                bool isAccepted(Entity ent)
                {
                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }
                ents = ents.Where(x => isAccepted(x)).ToHashSet();

                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);

                // Build node -> (element, port) adjacency
                var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
                foreach (var el in topo.Elements)
                {
                    foreach (var p in el.Ports)
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                        list.Add((el, p));
                    }
                }

                // Build element adjacency (undirected)
                var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
                foreach (var kv in nodeAdj)
                {
                    var items = kv.Value;
                    for (int i = 0; i < items.Count; i++)
                    {
                        for (int j = i + 1; j < items.Count; j++)
                        {
                            var a = items[i].el;
                            var b = items[j].el;
                            if (ReferenceEquals(a, b)) continue;
                            if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                            if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                            setA.Add(b);
                            setB.Add(a);
                        }
                    }
                }

                // Find connected components of elements
                var components = new List<List<ElementBase>>();
                var visited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                foreach (var el in topo.Elements)
                {
                    if (!visited.Add(el))
                        continue;
                    var comp = new List<ElementBase>();
                    var q = new Queue<ElementBase>();
                    q.Enqueue(el);
                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        comp.Add(cur);
                        if (elAdj.TryGetValue(cur, out var nbrs))
                        {
                            foreach (var n in nbrs)
                            {
                                if (visited.Add(n))
                                    q.Enqueue(n);
                            }
                        }
                    }
                    components.Add(comp);
                }

                // Emit DOT
                var sb = new StringBuilder();
                sb.AppendLine("graph G {");
                sb.AppendLine("  graph [compound=true];");
                sb.AppendLine("  node [shape=box, fontsize=10];");

                // Node labels and subgraphs
                int clusterIdx = 0;
                var edgeSet = new HashSet<string>(StringComparer.Ordinal);
                foreach (var comp in components)
                {
                    sb.AppendLine($"  subgraph cluster_{clusterIdx} {{");
                    sb.AppendLine($"    label=\"component {clusterIdx}\";");
                    sb.AppendLine("    color=lightgrey;");

                    // Define nodes
                    foreach (var e in comp)
                    {
                        var id = e.Source.ToString();
                        var dn = 0;
                        try { dn = e.DN; } catch { dn = 0; }
                        var label = dn > 0 ? $"{id}\\nDN={dn}" : id;
                        sb.AppendLine($"    \"{id}\" [label=\"{label}\"];");
                    }

                    // Define edges inside component
                    foreach (var a in comp)
                    {
                        if (!elAdj.TryGetValue(a, out var nbrs)) continue;
                        foreach (var b in nbrs)
                        {
                            // undirected edge; avoid dup by ordering
                            var ida = a.Source.ToString();
                            var idb = b.Source.ToString();
                            var k = string.CompareOrdinal(ida, idb) <= 0 ? $"{ida}--{idb}" : $"{idb}--{ida}";
                            if (edgeSet.Add(k))
                                sb.AppendLine($"    \"{ida}\" -- \"{idb}\";");
                        }
                    }

                    sb.AppendLine("  }");
                    clusterIdx++;
                }

                sb.AppendLine("}");

                File.WriteAllText(outDotPath, sb.ToString(), Encoding.UTF8);
                prdDbg($"DOT written: {outDotPath}");
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

Added a visualization command to inspect traversal adjacency via Graphviz DOT.

- New command
  - NTRDOT
  - Builds topology with the same filter as export.
  - Groups ports by shared `TNode` (implicit connectivity).
  - Builds an element-level undirected graph (elements connected if they share a node).
  - Finds disjoint components and emits DOT with subgraphs (one per component).
  - Node label = `ElementBase.Source.ToString()` plus `DN` if available.
  - Output: writes `<dwg>.dot` next to the DWG and logs the path.

- Where
  - Implemented in `Commands.cs` (uses `System.Text` for StringBuilder).
  - Helper `RefEq<T>` for reference-equality dictionaries to match the implicit topology design.

How to use
- NETLOAD -> run NTRDOT.
- Open the generated `.dot` in Graphviz to see components (subgraphs) and edges (shared-node connections).

---

**User**

okay, I have modified the code a little:

File.WriteAllText(outDotPath, sb.ToString(), Encoding.UTF8);



prdDbg($"DOT written: {outDotPath}");

// Run Graphviz (PDF)

var cmd = new Process();

cmd.StartInfo.FileName = "cmd.exe";

cmd.StartInfo.WorkingDirectory = @"C:\Temp\";

cmd.StartInfo.Arguments = @"/c ""dot -Tpdf ntrdot.dot > ntrdot.pdf""";

cmd.Start();

cmd.WaitForExit();

And the strange thing is that the .dot file is written using BOM, but dot command (graphviz) expectes no bom.

---

**Cursor**

```plaintext
var utf8NoBom = new UTF8Encoding(false);
File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
```

```plaintext
cmd.StartInfo.FileName = "cmd.exe";
cmd.StartInfo.Arguments = @"/c ""dot -Tpdf ntrdot.dot > ntrdot.pdf""";
```

```plaintext
cmd.StartInfo.FileName = "dot";
cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
cmd.StartInfo.UseShellExecute = false;
```

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Elevation;
using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.Routing;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;

using System.Diagnostics;
using System.IO;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");            
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();

                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Frem:
                                    case PipeTypeEnum.Retur:
                                    case PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
                }


            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);
                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());                            
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Elevation ➜ Routed skeleton -------------
                // Elevation provider based on traversal from a chosen root (largest-DN supply leaf)
                IElevationProvider elevation = new TraversalElevationProvider(topo);
                var routed = new Router(topo).Route(elevation);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;                            
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTRDOT")]
        public void ntrdot()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var dwgPath = localDb.Filename;
                var outDotPath = @"C:\Temp\ntrdot.dot";

                // Build topology (same filter as export)
                var ents = localDb.GetFjvEntities(tx);
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
                bool isAccepted(Entity ent)
                {
                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }
                ents = ents.Where(x => isAccepted(x)).ToHashSet();

                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);

                // Build node -> (element, port) adjacency
                var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
                foreach (var el in topo.Elements)
                {
                    foreach (var p in el.Ports)
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                        list.Add((el, p));
                    }
                }

                // Build element adjacency (undirected)
                var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
                foreach (var kv in nodeAdj)
                {
                    var items = kv.Value;
                    for (int i = 0; i < items.Count; i++)
                    {
                        for (int j = i + 1; j < items.Count; j++)
                        {
                            var a = items[i].el;
                            var b = items[j].el;
                            if (ReferenceEquals(a, b)) continue;
                            if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                            if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                            setA.Add(b);
                            setB.Add(a);
                        }
                    }
                }

                // Find connected components of elements
                var components = new List<List<ElementBase>>();
                var visited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                foreach (var el in topo.Elements)
                {
                    if (!visited.Add(el))
                        continue;
                    var comp = new List<ElementBase>();
                    var q = new Queue<ElementBase>();
                    q.Enqueue(el);
                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        comp.Add(cur);
                        if (elAdj.TryGetValue(cur, out var nbrs))
                        {
                            foreach (var n in nbrs)
                            {
                                if (visited.Add(n))
                                    q.Enqueue(n);
                            }
                        }
                    }
                    components.Add(comp);
                }

                // Emit DOT
                var sb = new StringBuilder();
                sb.AppendLine("graph G {");
                sb.AppendLine("  graph [compound=true];");
                sb.AppendLine("  node [shape=box, fontsize=10];");

                // Node labels and subgraphs
                int clusterIdx = 0;
                var edgeSet = new HashSet<string>(StringComparer.Ordinal);
                foreach (var comp in components)
                {
                    sb.AppendLine($"  subgraph cluster_{clusterIdx} {{");
                    sb.AppendLine($"    label=\"component {clusterIdx}\";");
                    sb.AppendLine("    color=lightgrey;");

                    // Define nodes
                    foreach (var e in comp)
                    {
                        var id = e.Source.ToString();
                        var dn = 0;
                        try { dn = e.DN; } catch { dn = 0; }
                        var label = dn > 0 ? $"{id}\\nDN={dn}" : id;
                        sb.AppendLine($"    \"{id}\" [label=\"{label}\"];");
                    }

                    // Define edges inside component
                    foreach (var a in comp)
                    {
                        if (!elAdj.TryGetValue(a, out var nbrs)) continue;
                        foreach (var b in nbrs)
                        {
                            // undirected edge; avoid dup by ordering
                            var ida = a.Source.ToString();
                            var idb = b.Source.ToString();
                            var k = string.CompareOrdinal(ida, idb) <= 0 ? $"{ida}--{idb}" : $"{idb}--{ida}";
                            if (edgeSet.Add(k))
                                sb.AppendLine($"    \"{ida}\" -- \"{idb}\";");
                        }
                    }

                    sb.AppendLine("  }");
                    clusterIdx++;
                }

                sb.AppendLine("}");

                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
                prdDbg($"DOT written: {outDotPath}");

                // Run Graphviz (PDF)
                var cmd = new Process();
                cmd.StartInfo.FileName = "dot";
                cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.WaitForExit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

The BOM is from Encoding.UTF8. I changed NTRDOT to write without BOM and to call Graphviz directly:

- Write .dot without BOM
  - Use new UTF8Encoding(false):

```12:12:Acad-C3D-Tools/NTRExport/Commands.cs
var utf8NoBom = new UTF8Encoding(false);
File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
```

- Invoke dot without cmd redirection (more robust):

```12:12:Acad-C3D-Tools/NTRExport/Commands.cs
cmd.StartInfo.FileName = "dot";
cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
cmd.StartInfo.UseShellExecute = false;
```

If Graphviz isn’t on PATH, set FileName to the full path of dot.exe.

```plaintext
var utf8NoBom = new UTF8Encoding(false);
File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
```

```plaintext
cmd.StartInfo.FileName = "dot";
cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
cmd.StartInfo.UseShellExecute = false;
```

---

**User**

haha no! this adjacency method is flawed. It doesn't select a component that is a leaf and has largest dn, but it actually consistently selects an element with three ports (in this case an F-model) and chooses it as the root node. By consistently I mean that I already tried this adjacency approach in another part of the solution with another agent and the agent couldn't fix it! I had to reinstate some of my old code to successfully select the correct root node. I asked the agent MULTIPLE times to fix the problem, but his logic didn't understand the problem and he muliple times claimed that the root selection was fixed. Now, I don't want you to go in circles editing the code again and again to try to fix it if you don't know where the error in the logic is.
So my request is that you create debug messages in the code that is responsible for selecting the root node and we shal document EACH line what is happening. BTW, see picture for result and faulty root selection.

---

**Cursor**

```csharp
*** Begin Patch
*** Update File: Acad-C3D-Tools/NTRExport/Elevation/TraversalElevationProvider.cs
@@
-using Autodesk.AutoCAD.Geometry;
-
+using Autodesk.AutoCAD.Geometry;
+using System.Runtime.CompilerServices;
+using static IntersectUtilities.UtilsCommon.Utils;
+
 using NTRExport.TopologyModel;
 
 namespace NTRExport.Elevation
 {
     internal sealed class TraversalElevationProvider : IElevationProvider
@@
         private void RegisterSolvers()
         {
             // Register element-specific solvers here (stubs for now).
             // Example:
             // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
             // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
             // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
             // Plane elbows would also have a solver interpreting roll Near/Far.
         }
 
         private void SolveFromRoot()
         {
             // Build adjacency by node to find neighbors
             var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
             foreach (var el in _topology.Elements)
             {
                 foreach (var p in el.Ports)
                 {
                     if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                     list.Add((el, p));
                 }
             }
+            prdDbg($"[TRAV] Adjacency built: nodes={nodeAdj.Count}, elements={_topology.Elements.Count()}");
 
             // Handle disjoint networks: iterate until all elements are visited
             var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
             var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());
 
             while (true)
             {
-                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
+                prdDbg($"[TRAV] Root selection pass. visitedElements={visitedElements.Count}");
+                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                 if (rootEl == null || rootPort == null) break;
+                prdDbg($"[TRAV] Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}. entryZ=0.000");
 
                 // Start this component at entryZ = 0.0
                 var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                 stack.Push((rootEl, rootPort, 0.0));
 
                 while (stack.Count > 0)
                 {
                     var (el, entry, entryZ) = stack.Pop();
                     if (!visitedPairs.Add((el, entry))) continue;
                     visitedElements.Add(el);
+                    prdDbg($"[TRAV] Visit element: {EId(el)} via node={NodeId(entry.Node)} entryZ={entryZ:0.###}");
 
                     var solver = ResolveSolver(el);
                     var exits = solver.Solve(el, entry, entryZ, _registry);
+                    prdDbg($"[TRAV] Solver exits: count={exits.Count}");
 
                     // Continue from exits to connected neighbors
                     foreach (var (exitPort, exitZ) in exits)
                     {
+                        prdDbg($"[TRAV]  -> exit node={NodeId(exitPort.Node)} propagateZ={exitZ:0.###}");
                         if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                         foreach (var (nel, nport) in neighbors)
                         {
                             if (ReferenceEquals(nel, el)) continue;
+                            prdDbg($"[TRAV]     neighbor: {EId(nel)} via node={NodeId(nport.Node)}");
                             stack.Push((nel, nport, exitZ));
                         }
                     }
                 }
             }
         }
@@
-        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
+        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
         {
+            prdDbg("[ROOT] Begin selection (prefer supply leaf with largest DN among unvisited).");
             ElementBase? bestEl = null;
             TPort? bestPort = null;
             int bestDn = -1;
             int bestScore = -1;
             foreach (var p in topo.Pipes)
             {
                 if (visitedElements.Contains(p)) continue;
                 bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && la.Count <= 1;
                 bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && lb.Count <= 1;
-                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
-                int dn = p.DN; // prefer larger DN
-                if ((aLeaf || bLeaf) && (score > bestScore || (score == bestScore && dn > bestDn)))
+                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
+                int dn = p.DN; // prefer larger DN
+                prdDbg($"[ROOT] Consider {EId(p)}: aLeaf={aLeaf} bLeaf={bLeaf} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
+                if ((aLeaf || bLeaf) && (score > bestScore || (score == bestScore && dn > bestDn)))
                 {
                     bestScore = score;
                     bestDn = dn;
                     bestEl = p;
                     bestPort = aLeaf ? p.A : p.B;
+                    prdDbg($"[ROOT]  -> update best to {EId(p)} at node={NodeId(bestPort.Node)}");
                 }
             }
             if (bestEl != null && bestPort != null) return (bestEl, bestPort);
 
             // Fallback: pick any unvisited pipe with largest DN (choose A port)
             bestEl = null;
             bestPort = null;
             bestDn = -1;
+            prdDbg("[ROOT] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
             foreach (var p in topo.Pipes)
             {
                 if (visitedElements.Contains(p)) continue;
                 if (p.DN > bestDn)
                 {
                     bestDn = p.DN;
                     bestEl = p;
                     bestPort = p.A;
+                    prdDbg($"[ROOT]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                 }
             }
             return (bestEl, bestPort);
         }
 
         private sealed class RefEq<T> : IEqualityComparer<T> where T : class
         {
             public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
             public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
         }
         private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
         {
             public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                 ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
             public int GetHashCode((ElementBase, TPort) obj)
             {
                 unchecked
                 {
                     int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                     int h2 = System.Runtime.CompilerServices.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                     return (h1 * 397) ^ h2;
                 }
             }
         }
+
+        private static string EId(ElementBase el)
+        {
+            int dn = 0;
+            try { dn = el.DN; } catch { dn = 0; }
+            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
+        }
+        private static string NodeId(TNode node)
+        {
+            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
+            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
+        }
     }
 }
*** End Patch
```

```csharp
using Autodesk.AutoCAD.Geometry;
using System.Runtime.CompilerServices;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        private readonly ElevationRegistry _registry = new();
        private readonly Dictionary<Type, IElementElevationSolver> _solvers = new();
        private readonly IElementElevationSolver _default = new DefaultElementElevationSolver();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            RegisterSolvers();
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpoints))
            {
                var ports = element.Ports;
                if (ports.Count >= 2 && endpoints.TryGetValue(ports[0], out var z0) && endpoints.TryGetValue(ports[1], out var z1))
                {
                    return z0 + t * (z1 - z0);
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void RegisterSolvers()
        {
            // Register element-specific solvers here (stubs for now).
            // Example:
            // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
            // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
            // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
            // Plane elbows would also have a solver interpreting roll Near/Far.
        }

        private void SolveFromRoot()
        {
            // Build adjacency by node to find neighbors
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }
            prdDbg($"[TRAV] Adjacency built: nodes={nodeAdj.Count}, elements={_topology.Elements.Count()}");

            // Handle disjoint networks: iterate until all elements are visited
            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                prdDbg($"[TRAV] Root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null) break;
                prdDbg($"[TRAV] Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}. entryZ=0.000");

                // Start this component at entryZ = 0.0
                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                stack.Push((rootEl, rootPort, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ) = stack.Pop();
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);
                    prdDbg($"[TRAV] Visit element: {EId(el)} via node={NodeId(entry.Node)} entryZ={entryZ:0.###}");

                    var solver = ResolveSolver(el);
                    var exits = solver.Solve(el, entry, entryZ, _registry);
                    prdDbg($"[TRAV] Solver exits: count={exits.Count}");

                    // Continue from exits to connected neighbors
                    foreach (var (exitPort, exitZ) in exits)
                    {
                        prdDbg($"[TRAV]  -> exit node={NodeId(exitPort.Node)} propagateZ={exitZ:0.###}");
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            prdDbg($"[TRAV]     neighbor: {EId(nel)} via node={NodeId(nport.Node)}");
                            stack.Push((nel, nport, exitZ));
                        }
                    }
                }
            }
        }

        private IElementElevationSolver ResolveSolver(ElementBase el)
        {
            var t = el.GetType();
            if (_solvers.TryGetValue(t, out var s)) return s;
            return _default;
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOT] Begin selection (prefer supply leaf with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && la.Count <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && lb.Count <= 1;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = p.DN; // prefer larger DN
                prdDbg($"[ROOT] Consider {EId(p)}: aLeaf={aLeaf} bLeaf={bLeaf} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if ((aLeaf || bLeaf) && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    bestPort = aLeaf ? p.A : p.B;
                    prdDbg($"[ROOT]  -> update best to {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOT] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOT]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
        {
            public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
            public int GetHashCode((ElementBase, TPort) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}






```

```csharp
*** Begin Patch
*** Update File: Acad-C3D-Tools/NTRExport/Elevation/TraversalElevationProvider.cs
@@
         private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
         {
             public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                 ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
             public int GetHashCode((ElementBase, TPort) obj)
             {
                 unchecked
                 {
                     int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                     int h2 = System.Runtime.CompilerServices.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                     return (h1 * 397) ^ h2;
                 }
             }
         }
+
+        private static string EId(ElementBase el)
+        {
+            int dn = 0;
+            try { dn = el.DN; } catch { dn = 0; }
+            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
+        }
+        private static string NodeId(TNode node)
+        {
+            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
+            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
+        }
     }
 }
*** End Patch
```

```csharp
using Autodesk.AutoCAD.Geometry;
using System.Runtime.CompilerServices;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        private readonly ElevationRegistry _registry = new();
        private readonly Dictionary<Type, IElementElevationSolver> _solvers = new();
        private readonly IElementElevationSolver _default = new DefaultElementElevationSolver();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            RegisterSolvers();
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpoints))
            {
                var ports = element.Ports;
                if (ports.Count >= 2 && endpoints.TryGetValue(ports[0], out var z0) && endpoints.TryGetValue(ports[1], out var z1))
                {
                    return z0 + t * (z1 - z0);
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void RegisterSolvers()
        {
            // Register element-specific solvers here (stubs for now).
            // Example:
            // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
            // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
            // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
            // Plane elbows would also have a solver interpreting roll Near/Far.
        }

        private void SolveFromRoot()
        {
            // Build adjacency by node to find neighbors
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }
            prdDbg($"[TRAV] Adjacency built: nodes={nodeAdj.Count}, elements={_topology.Elements.Count()}");

            // Handle disjoint networks: iterate until all elements are visited
            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                prdDbg($"[TRAV] Root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null) break;
                prdDbg($"[TRAV] Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}. entryZ=0.000");

                // Start this component at entryZ = 0.0
                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                stack.Push((rootEl, rootPort, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ) = stack.Pop();
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);
                    prdDbg($"[TRAV] Visit element: {EId(el)} via node={NodeId(entry.Node)} entryZ={entryZ:0.###}");

                    var solver = ResolveSolver(el);
                    var exits = solver.Solve(el, entry, entryZ, _registry);
                    prdDbg($"[TRAV] Solver exits: count={exits.Count}");

                    // Continue from exits to connected neighbors
                    foreach (var (exitPort, exitZ) in exits)
                    {
                        prdDbg($"[TRAV]  -> exit node={NodeId(exitPort.Node)} propagateZ={exitZ:0.###}");
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            prdDbg($"[TRAV]     neighbor: {EId(nel)} via node={NodeId(nport.Node)}");
                            stack.Push((nel, nport, exitZ));
                        }
                    }
                }
            }
        }

        private IElementElevationSolver ResolveSolver(ElementBase el)
        {
            var t = el.GetType();
            if (_solvers.TryGetValue(t, out var s)) return s;
            return _default;
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOT] Begin selection (prefer supply leaf with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && la.Count <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && lb.Count <= 1;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = p.DN; // prefer larger DN
                prdDbg($"[ROOT] Consider {EId(p)}: aLeaf={aLeaf} bLeaf={bLeaf} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if ((aLeaf || bLeaf) && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    bestPort = aLeaf ? p.A : p.B;
                    prdDbg($"[ROOT]  -> update best to {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOT] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOT]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
        {
            public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
            public int GetHashCode((ElementBase, TPort) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}






```

I added high-signal debug tracing around root selection and traversal so we can see exactly why a root is chosen and how the walk proceeds.

What’s instrumented
- Adjacency summary
  - [TRAV] Adjacency built: nodes=..., elements=...
- Disjoint traversal passes
  - [TRAV] Root selection pass. visitedElements=...
  - [TRAV] Root chosen: <Element/Type/DN> at node=<Name or @hash>. entryZ=0.000
- Per-element visit
  - [TRAV] Visit element: <Element/Type/DN> via node=<...> entryZ=...
  - [TRAV] Solver exits: count=...
  - [TRAV]  -> exit node=<...> propagateZ=...
  - [TRAV]     neighbor: <Element/Type/DN> via node=<...>

Root selection detail (each candidate is logged)
- [ROOT] Begin selection (prefer supply leaf with largest DN among unvisited).
- For every unvisited pipe:
  - [ROOT] Consider <Element/Type/DN>: aLeaf=<bool> bLeaf=<bool> score=<0/1> dn=<DN> currentBestScore=<...> bestDn=<...>
  - [ROOT]  -> update best to <...> at node=<...> (when it improves the choice)
- Fallback path (if no supply leaf found)
  - [ROOT] Fallback: no supply leaf found, choose largest DN unvisited pipe.
  - [ROOT]  -> fallback candidate <...> at node=<...>

Helpers
- EId(element): “Handle / Type / DN=...”
- NodeId(node): uses `node.Name` if set, else prints “@<hash>”.

Where to look
- `Elevation/TraversalElevationProvider.cs`:
  - SolveFromRoot: adjacency build, root pass, per-element visit, exits.
  - PickRoot: candidate-by-candidate logging and fallbacks.

No logic changes yet; just visibility. Run NTREXPORT or NTRDOT and inspect the console output to see the decision path for root selection and traversal.

---

**User**

so where's this debug info logged??? Hopefully not in Console.WriteLine etc? This console is not accessible in autocad (or not visible, i don't know). You have to use prdDbg(msg) to print to autocad console. Or you can make a simple logger class and log to a file in C:\Temp\

---

**Cursor**

It’s using prdDbg already.

- In `TraversalElevationProvider`, I added prdDbg calls in:
  - `SolveFromRoot` (adjacency summary, root-pass start, chosen root, element visits, exits, neighbors)
  - `PickRoot` (candidate-by-candidate logs, updates, fallback)
- No Console.WriteLine calls; messages go to the AutoCAD command line/output stream.

If you also want a file log (e.g., `C:\Temp\ntr-traversal.log`), say the word and I’ll add a simple logger wrapper that mirrors prdDbg output to file.

---

**User**

well, I don't get any debug messages when running NTRDOT

---

**Cursor**

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Elevation;
using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.Routing;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;

using System.Diagnostics;
using System.IO;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");            
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();

                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Frem:
                                    case PipeTypeEnum.Retur:
                                    case PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
                }


            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);
                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());                            
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Elevation ➜ Routed skeleton -------------
                // Elevation provider based on traversal from a chosen root (largest-DN supply leaf)
                IElevationProvider elevation = new TraversalElevationProvider(topo);
                var routed = new Router(topo).Route(elevation);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;                            
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTRDOT")]
        public void ntrdot()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var dwgPath = localDb.Filename;
                var outDotPath = @"C:\Temp\ntrdot.dot";

                // Build topology (same filter as export)
                var ents = localDb.GetFjvEntities(tx);
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
                bool isAccepted(Entity ent)
                {
                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }
                ents = ents.Where(x => isAccepted(x)).ToHashSet();

                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);

                // Run traversal to emit detailed prdDbg logs (root selection and walk)
                prdDbg("[NTRDOT] Starting traversal for debug output...");
                var _ = new TraversalElevationProvider(topo);

                // Build node -> (element, port) adjacency
                var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
                foreach (var el in topo.Elements)
                {
                    foreach (var p in el.Ports)
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                        list.Add((el, p));
                    }
                }

                // Build element adjacency (undirected)
                var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
                foreach (var kv in nodeAdj)
                {
                    var items = kv.Value;
                    for (int i = 0; i < items.Count; i++)
                    {
                        for (int j = i + 1; j < items.Count; j++)
                        {
                            var a = items[i].el;
                            var b = items[j].el;
                            if (ReferenceEquals(a, b)) continue;
                            if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                            if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                            setA.Add(b);
                            setB.Add(a);
                        }
                    }
                }

                // Find connected components of elements
                var components = new List<List<ElementBase>>();
                var visited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                foreach (var el in topo.Elements)
                {
                    if (!visited.Add(el))
                        continue;
                    var comp = new List<ElementBase>();
                    var q = new Queue<ElementBase>();
                    q.Enqueue(el);
                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        comp.Add(cur);
                        if (elAdj.TryGetValue(cur, out var nbrs))
                        {
                            foreach (var n in nbrs)
                            {
                                if (visited.Add(n))
                                    q.Enqueue(n);
                            }
                        }
                    }
                    components.Add(comp);
                }

                // Emit DOT
                var sb = new StringBuilder();
                sb.AppendLine("graph G {");
                sb.AppendLine("  graph [compound=true];");
                sb.AppendLine("  node [shape=box, fontsize=10];");

                // Node labels and subgraphs
                int clusterIdx = 0;
                var edgeSet = new HashSet<string>(StringComparer.Ordinal);
                foreach (var comp in components)
                {
                    sb.AppendLine($"  subgraph cluster_{clusterIdx} {{");
                    sb.AppendLine($"    label=\"component {clusterIdx}\";");
                    sb.AppendLine("    color=lightgrey;");

                    // Define nodes
                    foreach (var e in comp)
                    {
                        var id = e.Source.ToString();                        
                        var label = e.DotLabelForTest();
                        sb.AppendLine($"    \"{id}\" [label=\"{label}\"];");
                    }

                    // Define edges inside component
                    foreach (var a in comp)
                    {
                        if (!elAdj.TryGetValue(a, out var nbrs)) continue;
                        foreach (var b in nbrs)
                        {
                            // undirected edge; avoid dup by ordering
                            var ida = a.Source.ToString();
                            var idb = b.Source.ToString();
                            var k = string.CompareOrdinal(ida, idb) <= 0 ? $"{ida}--{idb}" : $"{idb}--{ida}";
                            if (edgeSet.Add(k))
                                sb.AppendLine($"    \"{ida}\" -- \"{idb}\";");
                        }
                    }

                    sb.AppendLine("  }");
                    clusterIdx++;
                }

                sb.AppendLine("}");

                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
                prdDbg($"DOT written: {outDotPath}");

                // Run Graphviz (PDF)
                var cmd = new Process();
                cmd.StartInfo.FileName = "dot";
                cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.WaitForExit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

NTRDOT wasn’t invoking the traversal, so no prdDbg from the traversal path. I’ve updated NTRDOT to run the traversal after building topology:

- In NTRDOT, right after `TopologyBuilder.Build(...)`:

```12:12:Acad-C3D-Tools/NTRExport/Commands.cs
prdDbg("[NTRDOT] Starting traversal for debug output...");
var _ = new TraversalElevationProvider(topo);
```

This triggers the detailed prdDbg logs from `TraversalElevationProvider` (root selection and walk). The DOT is still generated afterward.

```plaintext
prdDbg("[NTRDOT] Starting traversal for debug output...");
var _ = new TraversalElevationProvider(topo);
```

---

**User**

wait, we need to debug how you select the root node in the graph, we don't need traversal yet! the traversal is going to be wrong if we select wrong node to start with

---

**Cursor**

```csharp
using NTRExport.TopologyModel;
using static IntersectUtilities.UtilsCommon.Utils;
using System.Runtime.CompilerServices;

namespace NTRExport.Elevation
{
    internal static class RootSelector
    {
        public static void DebugPickRoots(Topology topo)
        {
            // Build port-level adjacency (node -> (element, port))
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in topo.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }

            prdDbg($"[ROOTDBG] Adjacency built: nodes={nodeAdj.Count}, elements={topo.Elements.Count()}");

            // Build element adjacency (undirected) by shared nodes
            var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
            foreach (var kv in nodeAdj)
            {
                var items = kv.Value;
                for (int i = 0; i < items.Count; i++)
                {
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        var a = items[i].el;
                        var b = items[j].el;
                        if (ReferenceEquals(a, b)) continue;
                        if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                        if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                        setA.Add(b);
                        setB.Add(a);
                    }
                }
            }

            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());
            int compIdx = 0;
            while (true)
            {
                prdDbg($"[ROOTDBG] Component {compIdx}: root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(topo, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null)
                {
                    prdDbg("[ROOTDBG] No more roots found (all elements covered).");
                    break;
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}");

                // Mark the entire connected component visited for the next passes
                var compCount = 0;
                var q = new Queue<ElementBase>();
                if (visitedElements.Add(rootEl)) { compCount++; q.Enqueue(rootEl); }
                while (q.Count > 0)
                {
                    var cur = q.Dequeue();
                    if (!elAdj.TryGetValue(cur, out var nbrs)) continue;
                    foreach (var n in nbrs)
                    {
                        if (visitedElements.Add(n)) { compCount++; q.Enqueue(n); }
                    }
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: size={compCount} (elements).");
                compIdx++;
            }
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo,
            Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj,
            HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;

            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && DegreeExcluding(la, p) <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && DegreeExcluding(lb, p) <= 1;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = p.DN; // prefer larger DN
                prdDbg($"[ROOTDBG] Consider {EId(p)}: aLeaf={aLeaf} bLeaf={bLeaf} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if ((aLeaf || bLeaf) && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    bestPort = aLeaf ? p.A : p.B;
                    prdDbg($"[ROOTDBG]  -> update best to {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOTDBG] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOTDBG]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            // Count distinct elements at this node excluding the candidate itself
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}


```

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Elevation;
using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.Routing;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;

using System.Diagnostics;
using System.IO;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");            
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();

                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Frem:
                                    case PipeTypeEnum.Retur:
                                    case PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
                }


            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);
                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());                            
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Elevation ➜ Routed skeleton -------------
                // Elevation provider based on traversal from a chosen root (largest-DN supply leaf)
                IElevationProvider elevation = new TraversalElevationProvider(topo);
                var routed = new Router(topo).Route(elevation);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;                            
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTRDOT")]
        public void ntrdot()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var dwgPath = localDb.Filename;
                var outDotPath = @"C:\Temp\ntrdot.dot";

                // Build topology (same filter as export)
                var ents = localDb.GetFjvEntities(tx);
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
                bool isAccepted(Entity ent)
                {
                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }
                ents = ents.Where(x => isAccepted(x)).ToHashSet();

                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);

                // Root selection debug (no traversal) - logs candidates and chosen roots per component
                prdDbg("[NTRDOT] Root selection debug start...");
                RootSelector.DebugPickRoots(topo);

                // Build node -> (element, port) adjacency
                var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
                foreach (var el in topo.Elements)
                {
                    foreach (var p in el.Ports)
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                        list.Add((el, p));
                    }
                }

                // Build element adjacency (undirected)
                var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
                foreach (var kv in nodeAdj)
                {
                    var items = kv.Value;
                    for (int i = 0; i < items.Count; i++)
                    {
                        for (int j = i + 1; j < items.Count; j++)
                        {
                            var a = items[i].el;
                            var b = items[j].el;
                            if (ReferenceEquals(a, b)) continue;
                            if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                            if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                            setA.Add(b);
                            setB.Add(a);
                        }
                    }
                }

                // Find connected components of elements
                var components = new List<List<ElementBase>>();
                var visited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                foreach (var el in topo.Elements)
                {
                    if (!visited.Add(el))
                        continue;
                    var comp = new List<ElementBase>();
                    var q = new Queue<ElementBase>();
                    q.Enqueue(el);
                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        comp.Add(cur);
                        if (elAdj.TryGetValue(cur, out var nbrs))
                        {
                            foreach (var n in nbrs)
                            {
                                if (visited.Add(n))
                                    q.Enqueue(n);
                            }
                        }
                    }
                    components.Add(comp);
                }

                // Emit DOT
                var sb = new StringBuilder();
                sb.AppendLine("graph G {");
                sb.AppendLine("  graph [compound=true];");
                sb.AppendLine("  node [shape=box, fontsize=10];");

                // Node labels and subgraphs
                int clusterIdx = 0;
                var edgeSet = new HashSet<string>(StringComparer.Ordinal);
                foreach (var comp in components)
                {
                    sb.AppendLine($"  subgraph cluster_{clusterIdx} {{");
                    sb.AppendLine($"    label=\"component {clusterIdx}\";");
                    sb.AppendLine("    color=lightgrey;");

                    // Define nodes
                    foreach (var e in comp)
                    {
                        var id = e.Source.ToString();                        
                        var label = e.DotLabelForTest();
                        sb.AppendLine($"    \"{id}\" [label=\"{label}\"];");
                    }

                    // Define edges inside component
                    foreach (var a in comp)
                    {
                        if (!elAdj.TryGetValue(a, out var nbrs)) continue;
                        foreach (var b in nbrs)
                        {
                            // undirected edge; avoid dup by ordering
                            var ida = a.Source.ToString();
                            var idb = b.Source.ToString();
                            var k = string.CompareOrdinal(ida, idb) <= 0 ? $"{ida}--{idb}" : $"{idb}--{ida}";
                            if (edgeSet.Add(k))
                                sb.AppendLine($"    \"{ida}\" -- \"{idb}\";");
                        }
                    }

                    sb.AppendLine("  }");
                    clusterIdx++;
                }

                sb.AppendLine("}");

                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
                prdDbg($"DOT written: {outDotPath}");

                // Run Graphviz (PDF)
                var cmd = new Process();
                cmd.StartInfo.FileName = "dot";
                cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.WaitForExit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

Good call. I removed the traversal call from NTRDOT and added a focused root-selection debug pass.

What I added
- `Elevation/RootSelector.cs`
  - Builds port-level adjacency (node -> (element, port)).
  - Logs adjacency summary via prdDbg.
  - Repeats root selection for each disjoint component:
    - Preferred root: unvisited pipe that is a leaf (degree 1 at at least one end) and supply type, maximizing DN.
    - Leaf detection excludes the candidate element itself at a node.
    - Fallback: largest DN unvisited pipe if no supply leaf found.
  - Logs every candidate considered:
    - “[ROOTDBG] Consider … aLeaf=… bLeaf=… score=… dn=…”
    - “[ROOTDBG]  -> update best …”
    - Fallback notices and candidates.
  - After choosing a root, it BFS-marks the whole component visited (using element adjacency) and logs component size.
- `Commands.cs` → NTRDOT
  - Calls `RootSelector.DebugPickRoots(topo)` right after building topology:
    - “[NTRDOT] Root selection debug start…”
  - Keeps DOT emission as before.

Why this helps
- You get prdDbg logs that document every line of reasoning for picking a root (per component), without running traversal.
- It ensures the leaf-and-largest-DN rule is clearly visible; if it still picks an F-model, we will see exactly why.

Run
- NETLOAD → `NTRDOT`
- Check the AutoCAD command line for `[ROOTDBG]` lines showing:
  - adjacency counts
  - each candidate pipe’s leaf status and DN
  - chosen root for each component and the component size

If you want, I can also add an option to write these logs to `C:\Temp\ntr-rootdbg.log` in parallel to prdDbg.

---

**User**

okay, I've got 5 elements in my drawing for testing and the resulting .dot graph is shown in pasted image. The debug says this: 

Command: NTRDOT



[NTRDOT] Root selection debug start...

[ROOTDBG] Adjacency built: nodes=7, elements=5

[ROOTDBG] Component 0: root selection pass. visitedElements=0

[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).

[ROOTDBG] Consider 166F / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=-1 bestDn=-1

[ROOTDBG]  -> update best to 166F / TPipe / DN=125 at node=N006

[ROOTDBG] Consider 1674 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider 1683 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG]  -> update best to 1683 / TPipe / DN=125 at node=N007

[ROOTDBG] Component 0: Root chosen: 1683 / TPipe / DN=125 at node=N007

[ROOTDBG] Component 0: size=5 (elements).

[ROOTDBG] Component 1: root selection pass. visitedElements=5

[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).

[ROOTDBG] Fallback: no supply leaf found, choose largest DN unvisited pipe.

[ROOTDBG] No more roots found (all elements covered).

---

**Cursor**

- Your debug shows the root is correctly picked as 1683 / TPipe / DN=125 (supply leaf). The FModel at the top of the DOT is just Graphviz’s layout, not the chosen root.
- Root logic now:
  - Leaf test excludes the candidate itself at the node.
  - Prefer supply (score=1) over non-supply (score=0).
  - Break ties by larger DN.
  - Repeat per disjoint component.

Want me to highlight the chosen root(s) in the DOT (e.g., style=filled, fillcolor=yellow) so the diagram visually matches the debug?

---

**User**

okay, okay, wait wait, the dot print must show the correct node sequence! You have to list the edge connections in DFS starting with root node

---

**Cursor**

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Elevation;
using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.Routing;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;

using System.Diagnostics;
using System.IO;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");            
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();

                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Frem:
                                    case PipeTypeEnum.Retur:
                                    case PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
                }


            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);
                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());                            
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Elevation ➜ Routed skeleton -------------
                // Elevation provider based on traversal from a chosen root (largest-DN supply leaf)
                IElevationProvider elevation = new TraversalElevationProvider(topo);
                var routed = new Router(topo).Route(elevation);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;                            
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTRDOT")]
        public void ntrdot()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var dwgPath = localDb.Filename;
                var outDotPath = @"C:\Temp\ntrdot.dot";

                // Build topology (same filter as export)
                var ents = localDb.GetFjvEntities(tx);
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
                bool isAccepted(Entity ent)
                {
                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }
                ents = ents.Where(x => isAccepted(x)).ToHashSet();

                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);

                // Root selection debug (no traversal) - logs candidates and chosen roots per component
                prdDbg("[NTRDOT] Root selection debug start...");
                RootSelector.DebugPickRoots(topo);

                // Build node -> (element, port) adjacency
                var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
                foreach (var el in topo.Elements)
                {
                    foreach (var p in el.Ports)
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                        list.Add((el, p));
                    }
                }

                // Build element adjacency (undirected)
                var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
                foreach (var kv in nodeAdj)
                {
                    var items = kv.Value;
                    for (int i = 0; i < items.Count; i++)
                    {
                        for (int j = i + 1; j < items.Count; j++)
                        {
                            var a = items[i].el;
                            var b = items[j].el;
                            if (ReferenceEquals(a, b)) continue;
                            if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                            if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                            setA.Add(b);
                            setB.Add(a);
                        }
                    }
                }

                // Find connected components of elements
                var components = new List<List<ElementBase>>();
                var visited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                foreach (var el in topo.Elements)
                {
                    if (!visited.Add(el))
                        continue;
                    var comp = new List<ElementBase>();
                    var q = new Queue<ElementBase>();
                    q.Enqueue(el);
                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        comp.Add(cur);
                        if (elAdj.TryGetValue(cur, out var nbrs))
                        {
                            foreach (var n in nbrs)
                            {
                                if (visited.Add(n))
                                    q.Enqueue(n);
                            }
                        }
                    }
                    components.Add(comp);
                }

                // Emit DOT
                var sb = new StringBuilder();
                sb.AppendLine("graph G {");
                sb.AppendLine("  graph [compound=true];");
                sb.AppendLine("  node [shape=box, fontsize=10];");

                // Node labels and subgraphs
                int clusterIdx = 0;
                foreach (var comp in components)
                {
                    sb.AppendLine($"  subgraph cluster_{clusterIdx} {{");
                    sb.AppendLine($"    label=\"component {clusterIdx}\";");
                    sb.AppendLine("    color=lightgrey;");

                    // Define nodes
                    foreach (var e in comp)
                    {
                        var id = e.Source.ToString();                        
                        var label = e.DotLabelForTest();
                        sb.AppendLine($"    \"{id}\" [label=\"{label}\"];");
                    }

                    // DFS edges inside component, starting from chosen root
                    ElementBase ChooseRootForComp(List<ElementBase> c)
                    {
                        ElementBase? best = null;
                        TPort? bestPort = null;
                        int bestScore = -1;
                        int bestDn = -1;
                        foreach (var e in c)
                        {
                            if (e is not TPipe p) continue;
                            bool aLeaf = DegreeExcluding(nodeAdj[p.A.Node], p) <= 1;
                            bool bLeaf = DegreeExcluding(nodeAdj[p.B.Node], p) <= 1;
                            if (!(aLeaf || bLeaf)) continue;
                            int score = p.Type == PipeTypeEnum.Frem ? 1 : 0;
                            int dn = p.DN;
                            if (score > bestScore || (score == bestScore && dn > bestDn))
                            {
                                bestScore = score;
                                bestDn = dn;
                                best = p;
                                bestPort = aLeaf ? p.A : p.B;
                            }
                        }
                        if (best != null) return best;
                        // Fallback: largest DN pipe in component
                        best = c.OfType<TPipe>().OrderByDescending(x => x.DN).FirstOrDefault() ?? c.First();
                        return best;
                    }

                    int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
                    {
                        var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
                        foreach (var t in items)
                        {
                            if (ReferenceEquals(t.el, exclude)) continue;
                            set.Add(t.el);
                        }
                        return set.Count;
                    }

                    var root = ChooseRootForComp(comp);
                    prdDbg($"[NTRDOT] Component {clusterIdx} DFS root: {root.Source} / {root.GetType().Name}");

                    // Build DFS ordered edges
                    var dfsVisited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                    var edgesOrdered = new List<(string a, string b)>();

                    void Dfs(ElementBase cur, ElementBase? parent)
                    {
                        dfsVisited.Add(cur);
                        if (!elAdj.TryGetValue(cur, out var nbrs)) return;
                        // stable order
                        var ordered = nbrs.Where(n => comp.Contains(n))
                                          .OrderBy(n => n.Source.ToString(), StringComparer.Ordinal)
                                          .ToList();
                        foreach (var n in ordered)
                        {
                            if (parent != null && ReferenceEquals(n, parent)) continue;
                            if (!dfsVisited.Contains(n))
                            {
                                edgesOrdered.Add((cur.Source.ToString(), n.Source.ToString()));
                                Dfs(n, cur);
                            }
                        }
                    }

                    Dfs(root, null);
                    // Cover any isolated or remaining nodes (safety)
                    foreach (var e in comp)
                    {
                        if (!dfsVisited.Contains(e))
                            Dfs(e, null);
                    }

                    foreach (var (a, b) in edgesOrdered)
                        sb.AppendLine($"    \"{a}\" -- \"{b}\";");

                    sb.AppendLine("  }");
                    clusterIdx++;
                }

                sb.AppendLine("}");

                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
                prdDbg($"DOT written: {outDotPath}");

                // Run Graphviz (PDF)
                var cmd = new Process();
                cmd.StartInfo.FileName = "dot";
                cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.WaitForExit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

I updated NTRDOT to emit edges in DFS order starting from a chosen root per component.

What changed
- For each component, choose root with the same rule used in root debug:
  - Prefer supply `TPipe` that is a leaf at either end (degree excluding self ≤ 1), break ties by larger DN.
  - Fallback: largest DN pipe in the component.
- Run DFS on the element adjacency graph from that root, recording edges in visit order.
- Emit edges in that DFS order inside the component’s subgraph.
- Added a prdDbg line to show the DFS root per component:
  - “[NTRDOT] Component k DFS root: <Handle> / <Type>”

Result
- DOT now lists connections following DFS from the correct root, so the visual ordering better matches the intended starting point and traversal sequence.

---

**User**

why is the algorithm considering all nodes? also what does aLeaf and bLeaf means?

---

**Cursor**

- Why “consider all nodes”? We don’t. Root selection iterates over all unvisited pipes (elements), not nodes. We build node adjacency once (group ports by `TNode`) to know connectivity, then, for root choice, we scan pipes and query their two end-nodes to see if either end is a leaf. Adjacency across all nodes is needed to detect components and to compute end “degree,” but only pipes are candidates.

- What are aLeaf and bLeaf?
  - aLeaf: whether the pipe’s A-end node is a leaf, i.e., that node is shared with ≤ 1 other element (excluding the pipe itself).
  - bLeaf: same for the B-end node.

In code terms: aLeaf = DegreeExcluding(nodeAdj[p.A.Node], p) ≤ 1; bLeaf = DegreeExcluding(nodeAdj[p.B.Node], p) ≤ 1. We prefer candidates where (aLeaf || bLeaf) is true, prioritize supply pipes, then break ties by DN.

---

**User**

okay, here's a longer log:

[NTRDOT] Root selection debug start...



[ROOTDBG] Adjacency built: nodes=221, elements=201

[ROOTDBG] Component 0: root selection pass. visitedElements=0

[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).

[ROOTDBG] Consider AD7DF / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=-1 bestDn=-1

[ROOTDBG]  -> update best to AD7DF / TPipe / DN=125 at node=N118

[ROOTDBG] Consider AD7E0 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider AD804 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider AD806 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider AEA0E / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider AFA05 / TPipe / DN=65: aLeaf=True bLeaf=True score=0 dn=65 currentBestScore=1 bestDn=125

[ROOTDBG] Consider AFA05 / TPipe / DN=65: aLeaf=True bLeaf=True score=0 dn=65 currentBestScore=1 bestDn=125

[ROOTDBG] Consider AFA05 / TPipe / DN=65: aLeaf=True bLeaf=True score=0 dn=65 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B346D / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B4B9C / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B4B9C / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B50E5 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B50E5 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B50E5 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B50E5 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B50E5 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B50E5 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B51E6 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B646C / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B79B9 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF92F / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider BF9D7 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C11B3 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C11B3 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C11B4 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C11B4 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C1798 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C1798 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C9A9D / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C9A9D / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C9A9D / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C9BE5 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C9C0F / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C9C0F / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C9C0F / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C9C0F / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CBB5B / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CBB60 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CBB60 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CBB60 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CBB60 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CC5B1 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CC5B1 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CC732 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CCD88 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CCD89 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CCD8A / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CCD8B / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CFB18 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CFB19 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CFB2C / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D257C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D257C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D257C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D257C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D257C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D257C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D257C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D257C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D2EAF / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D2EB0 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D37B2 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D37B2 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D387C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D38CC / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D38CD / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D6AE8 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D6AE8 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D6AE8 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D6AE8 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D6AE8 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D8797 / TPipe / DN=65: aLeaf=True bLeaf=True score=0 dn=65 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D8797 / TPipe / DN=65: aLeaf=True bLeaf=True score=0 dn=65 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D8797 / TPipe / DN=65: aLeaf=True bLeaf=True score=0 dn=65 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DB64B / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DB64B / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DB64B / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DB64B / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DB6C0 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DB6C0 / TPipe / DN=100: aLeaf=True bLeaf=True score=0 dn=100 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DCBFB / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DCBFC / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DCBFC / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Component 0: Root chosen: AD7DF / TPipe / DN=125 at node=N118

[ROOTDBG] Component 0: size=146 (elements).

[ROOTDBG] Component 1: root selection pass. visitedElements=146

[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).

[ROOTDBG] Consider AD804 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=-1 bestDn=-1

[ROOTDBG]  -> update best to AD804 / TPipe / DN=125 at node=N103

[ROOTDBG] Consider AD806 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider B51E6 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C11B3 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C11B3 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C11B4 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C11B4 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C1798 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider C1798 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CCD88 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CCD89 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CCD8A / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CCD8B / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CFB18 / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CFB19 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider CFB2C / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D387C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D38CC / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider D38CD / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DCBFB / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DCBFC / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Consider DCBFC / TPipe / DN=125: aLeaf=True bLeaf=True score=1 dn=125 currentBestScore=1 bestDn=125

[ROOTDBG] Component 1: Root chosen: AD804 / TPipe / DN=125 at node=N103

[ROOTDBG] Component 1: size=21 (elements).

[ROOTDBG] Component 2: root selection pass. visitedElements=167

[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).

[ROOTDBG] Consider AD806 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=-1 bestDn=-1

[ROOTDBG]  -> update best to AD806 / TPipe / DN=125 at node=N086

[ROOTDBG] Consider B51E6 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=0 bestDn=125

[ROOTDBG] Consider C11B4 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider C11B4 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider C1798 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider C1798 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider CCD88 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider CCD8B / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider CFB19 / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider D387C / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider D38CD / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Consider DCBFB / TPipe / DN=125: aLeaf=True bLeaf=True score=0 dn=125 currentBestScore=0 bestDn=125

[ROOTDBG] Component 2: Root chosen: AD806 / TPipe / DN=125 at node=N086

[ROOTDBG] Component 2: size=22 (elements).

[ROOTDBG] Component 3: root selection pass. visitedElements=189

[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).

[ROOTDBG] Consider B51E6 / TPipe / DN=50: aLeaf=True bLeaf=True score=0 dn=50 currentBestScore=-1 bestDn=-1

[ROOTDBG]  -> update best to B51E6 / TPipe / DN=50 at node=N043

[ROOTDBG] Component 3: Root chosen: B51E6 / TPipe / DN=50 at node=N043

[ROOTDBG] Component 3: size=3 (elements).

[ROOTDBG] Component 4: root selection pass. visitedElements=192

[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).

[ROOTDBG] Fallback: no supply leaf found, choose largest DN unvisited pipe.

[ROOTDBG] No more roots found (all elements covered).

[NTRDOT] Component 0 DFS root: D2EB0 / TPipe

[NTRDOT] Component 1 DFS root: B0A82 / AfgreningsStuds

[NTRDOT] Component 2 DFS root: B12D2 / AfgreningsStuds

[NTRDOT] Component 3 DFS root: B18AC / AfgreningsStuds

[NTRDOT] Component 4 DFS root: B51E6 / TPipe

[NTRDOT] Component 5 DFS root: BE6EA / AfgreningsStuds

[NTRDOT] Component 6 DFS root: BE72C / AfgreningsStuds

[NTRDOT] Component 7 DFS root: BF04F / AfgreningsStuds

[NTRDOT] Component 8 DFS root: BF1E6 / AfgreningsStuds

[NTRDOT] Component 9 DFS root: BF48D / AfgreningsStuds

[NTRDOT] Component 10 DFS root: CFB2C / TPipe

[NTRDOT] Component 11 DFS root: CCD8B / TPipe

[NTRDOT] Component 12 DFS root: D551E / Valve

DOT written: C:\Temp\ntrdot.dot

The log logs a lot of CONSIDERING statements and all elements that it considers is TPipe. What about other elements?

ALso, look at AD804 entity. It says it's a leaf, but it is now, see the pasted image.

s

---

**Cursor**

```csharp
using NTRExport.TopologyModel;
using static IntersectUtilities.UtilsCommon.Utils;
using System.Runtime.CompilerServices;

namespace NTRExport.Elevation
{
    internal static class RootSelector
    {
        public static void DebugPickRoots(Topology topo)
        {
            // Build port-level adjacency (node -> (element, port))
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in topo.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }

            prdDbg($"[ROOTDBG] Adjacency built: nodes={nodeAdj.Count}, elements={topo.Elements.Count()}");

            // Build element adjacency (undirected) by shared nodes
            var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
            foreach (var kv in nodeAdj)
            {
                var items = kv.Value;
                for (int i = 0; i < items.Count; i++)
                {
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        var a = items[i].el;
                        var b = items[j].el;
                        if (ReferenceEquals(a, b)) continue;
                        if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                        if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                        setA.Add(b);
                        setB.Add(a);
                    }
                }
            }

            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());
            int compIdx = 0;
            while (true)
            {
                prdDbg($"[ROOTDBG] Component {compIdx}: root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(topo, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null)
                {
                    prdDbg("[ROOTDBG] No more roots found (all elements covered).");
                    break;
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}");

                // Mark the entire connected component visited for the next passes
                var compCount = 0;
                var q = new Queue<ElementBase>();
                if (visitedElements.Add(rootEl)) { compCount++; q.Enqueue(rootEl); }
                while (q.Count > 0)
                {
                    var cur = q.Dequeue();
                    if (!elAdj.TryGetValue(cur, out var nbrs)) continue;
                    foreach (var n in nbrs)
                    {
                        if (visitedElements.Add(n)) { compCount++; q.Enqueue(n); }
                    }
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: size={compCount} (elements).");
                compIdx++;
            }
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo,
            Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj,
            HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;

            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                // Element-degree leaf: union of distinct neighbors across both ends equals 1
                int elemDeg = ElementDegree(p, nodeAdj);
                bool isLeaf = elemDeg == 1;
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && DegreeExcluding(la, p) <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && DegreeExcluding(lb, p) <= 1;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = p.DN; // prefer larger DN
                prdDbg($"[ROOTDBG] Consider {EId(p)}: elemDeg={elemDeg} aLeaf={aLeaf} bLeaf={bLeaf} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if (isLeaf && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    // Prefer the leaf end as starting port if we have it; else default to A
                    bestPort = aLeaf ? p.A : (bLeaf ? p.B : p.A);
                    prdDbg($"[ROOTDBG]  -> update best to {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOTDBG] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOTDBG]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            // Count distinct elements at this node excluding the candidate itself
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private static int ElementDegree(TPipe p, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            if (nodeAdj.TryGetValue(p.A.Node, out var la))
            {
                foreach (var t in la)
                {
                    if (ReferenceEquals(t.el, p)) continue;
                    set.Add(t.el);
                }
            }
            if (nodeAdj.TryGetValue(p.B.Node, out var lb))
            {
                foreach (var t in lb)
                {
                    if (ReferenceEquals(t.el, p)) continue;
                    set.Add(t.el);
                }
            }
            return set.Count;
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}



```

```csharp
using Autodesk.AutoCAD.Geometry;
using System.Runtime.CompilerServices;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        private readonly ElevationRegistry _registry = new();
        private readonly Dictionary<Type, IElementElevationSolver> _solvers = new();
        private readonly IElementElevationSolver _default = new DefaultElementElevationSolver();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            RegisterSolvers();
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpoints))
            {
                var ports = element.Ports;
                if (ports.Count >= 2 && endpoints.TryGetValue(ports[0], out var z0) && endpoints.TryGetValue(ports[1], out var z1))
                {
                    return z0 + t * (z1 - z0);
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void RegisterSolvers()
        {
            // Register element-specific solvers here (stubs for now).
            // Example:
            // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
            // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
            // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
            // Plane elbows would also have a solver interpreting roll Near/Far.
        }

        private void SolveFromRoot()
        {
            // Build adjacency by node to find neighbors
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }
            prdDbg($"[TRAV] Adjacency built: nodes={nodeAdj.Count}, elements={_topology.Elements.Count()}");

            // Handle disjoint networks: iterate until all elements are visited
            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                prdDbg($"[TRAV] Root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null) break;
                prdDbg($"[TRAV] Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}. entryZ=0.000");

                // Start this component at entryZ = 0.0
                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                stack.Push((rootEl, rootPort, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ) = stack.Pop();
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);
                    prdDbg($"[TRAV] Visit element: {EId(el)} via node={NodeId(entry.Node)} entryZ={entryZ:0.###}");

                    var solver = ResolveSolver(el);
                    var exits = solver.Solve(el, entry, entryZ, _registry);
                    prdDbg($"[TRAV] Solver exits: count={exits.Count}");

                    // Continue from exits to connected neighbors
                    foreach (var (exitPort, exitZ) in exits)
                    {
                        prdDbg($"[TRAV]  -> exit node={NodeId(exitPort.Node)} propagateZ={exitZ:0.###}");
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            prdDbg($"[TRAV]     neighbor: {EId(nel)} via node={NodeId(nport.Node)}");
                            stack.Push((nel, nport, exitZ));
                        }
                    }
                }
            }
        }

        private IElementElevationSolver ResolveSolver(ElementBase el)
        {
            var t = el.GetType();
            if (_solvers.TryGetValue(t, out var s)) return s;
            return _default;
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOT] Begin selection (prefer supply leaf element with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                int elemDeg = ElementDegree(p, nodeAdj);
                bool isLeaf = elemDeg == 1;
                bool aLeaf = nodeAdj.TryGetValue(p.A.Node, out var la) && DegreeExcluding(la, p) <= 1;
                bool bLeaf = nodeAdj.TryGetValue(p.B.Node, out var lb) && DegreeExcluding(lb, p) <= 1;
                int score = p.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = p.DN; // prefer larger DN
                prdDbg($"[ROOT] Consider {EId(p)}: elemDeg={elemDeg} aLeaf={aLeaf} bLeaf={bLeaf} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if (isLeaf && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = p;
                    bestPort = aLeaf ? p.A : (bLeaf ? p.B : p.A);
                    prdDbg($"[ROOT]  -> update best to {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOT] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOT]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private static int ElementDegree(TPipe p, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            if (nodeAdj.TryGetValue(p.A.Node, out var la))
            {
                foreach (var t in la)
                {
                    if (ReferenceEquals(t.el, p)) continue;
                    set.Add(t.el);
                }
            }
            if (nodeAdj.TryGetValue(p.B.Node, out var lb))
            {
                foreach (var t in lb)
                {
                    if (ReferenceEquals(t.el, p)) continue;
                    set.Add(t.el);
                }
            }
            return set.Count;
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
        {
            public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
            public int GetHashCode((ElementBase, TPort) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}






```

Thanks for the full log and the screenshot—that helped pinpoint the issue.

- Why only TPipe in “Consider …” lines
  - By design, the candidate set for roots is pipes, because a “supply leaf” is a pipe open end. Fittings are still in adjacency (and affect degree), but they aren’t root candidates.

- Why AD804 showed up as a “leaf” earlier
  - We initially used a per-node leaf heuristic; a pipe attached to an elbow at each end can have each end node with degree 1 (excluding itself), yet the pipe has two distinct neighbors total, so it is not a leaf element. That’s why your pipe between two elbows was misclassified.

- What I changed
  - Root selection now uses element-degree leaf: the number of distinct neighboring elements across both ends of a pipe must be exactly 1 (elemDeg == 1). This prevents a pipe between two elbows from being considered a leaf.
  - I kept logging the old per-end values (aLeaf, bLeaf) for transparency, but the decision key is elemDeg.
  - Applied in both debug root picker and traversal provider, and I logged elemDeg in the debug line so we can see it.

- Next steps
  - Re-run NTRDOT; the “[ROOTDBG] Consider … elemDeg=1” should appear only for true leaf pipes. The chosen roots per component should align with your expectations; the DFS in the DOT is already rooted at that chosen pipe.

If you still see a wrong root after this, paste the relevant lines with elemDeg in the debug and we’ll tighten the rule further (e.g., additional filters by system/type or explicit supply anchor).

---

**User**

look, why have you decided that we are only interested in pipes as roots? It can be ANY element.

---

**Cursor**

```csharp
using NTRExport.TopologyModel;
using static IntersectUtilities.UtilsCommon.Utils;
using System.Runtime.CompilerServices;

namespace NTRExport.Elevation
{
    internal static class RootSelector
    {
        public static void DebugPickRoots(Topology topo)
        {
            // Build port-level adjacency (node -> (element, port))
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in topo.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }

            prdDbg($"[ROOTDBG] Adjacency built: nodes={nodeAdj.Count}, elements={topo.Elements.Count()}");

            // Build element adjacency (undirected) by shared nodes
            var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
            foreach (var kv in nodeAdj)
            {
                var items = kv.Value;
                for (int i = 0; i < items.Count; i++)
                {
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        var a = items[i].el;
                        var b = items[j].el;
                        if (ReferenceEquals(a, b)) continue;
                        if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                        if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                        setA.Add(b);
                        setB.Add(a);
                    }
                }
            }

            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());
            int compIdx = 0;
            while (true)
            {
                prdDbg($"[ROOTDBG] Component {compIdx}: root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(topo, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null)
                {
                    prdDbg("[ROOTDBG] No more roots found (all elements covered).");
                    break;
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}");

                // Mark the entire connected component visited for the next passes
                var compCount = 0;
                var q = new Queue<ElementBase>();
                if (visitedElements.Add(rootEl)) { compCount++; q.Enqueue(rootEl); }
                while (q.Count > 0)
                {
                    var cur = q.Dequeue();
                    if (!elAdj.TryGetValue(cur, out var nbrs)) continue;
                    foreach (var n in nbrs)
                    {
                        if (visitedElements.Add(n)) { compCount++; q.Enqueue(n); }
                    }
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: size={compCount} (elements).");
                compIdx++;
            }
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo,
            Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj,
            HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;

            foreach (var el in topo.Elements)
            {
                if (visitedElements.Contains(el)) continue;
                int elemDeg = ElementDegree(el, nodeAdj);
                bool isLeaf = elemDeg == 1;
                int score = el.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply when known
                int dn = 0;
                try { dn = el.DN; } catch { dn = 0; }
                prdDbg($"[ROOTDBG] Consider {EId(el)}: elemDeg={elemDeg} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if (isLeaf && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = el;
                    bestPort = ChooseLeafPort(el, nodeAdj) ?? el.Ports.FirstOrDefault();
                    if (bestPort != null)
                        prdDbg($"[ROOTDBG]  -> update best to {EId(el)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOTDBG] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOTDBG]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            // Count distinct elements at this node excluding the candidate itself
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private static int ElementDegree(TPipe p, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            if (nodeAdj.TryGetValue(p.A.Node, out var la))
            {
                foreach (var t in la)
                {
                    if (ReferenceEquals(t.el, p)) continue;
                    set.Add(t.el);
                }
            }
            if (nodeAdj.TryGetValue(p.B.Node, out var lb))
            {
                foreach (var t in lb)
                {
                    if (ReferenceEquals(t.el, p)) continue;
                    set.Add(t.el);
                }
            }
            return set.Count;
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}



```

```csharp
using NTRExport.TopologyModel;
using static IntersectUtilities.UtilsCommon.Utils;
using System.Runtime.CompilerServices;

namespace NTRExport.Elevation
{
    internal static class RootSelector
    {
        public static void DebugPickRoots(Topology topo)
        {
            // Build port-level adjacency (node -> (element, port))
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in topo.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }

            prdDbg($"[ROOTDBG] Adjacency built: nodes={nodeAdj.Count}, elements={topo.Elements.Count()}");

            // Build element adjacency (undirected) by shared nodes
            var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
            foreach (var kv in nodeAdj)
            {
                var items = kv.Value;
                for (int i = 0; i < items.Count; i++)
                {
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        var a = items[i].el;
                        var b = items[j].el;
                        if (ReferenceEquals(a, b)) continue;
                        if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                        if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                        setA.Add(b);
                        setB.Add(a);
                    }
                }
            }

            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());
            int compIdx = 0;
            while (true)
            {
                prdDbg($"[ROOTDBG] Component {compIdx}: root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(topo, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null)
                {
                    prdDbg("[ROOTDBG] No more roots found (all elements covered).");
                    break;
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}");

                // Mark the entire connected component visited for the next passes
                var compCount = 0;
                var q = new Queue<ElementBase>();
                if (visitedElements.Add(rootEl)) { compCount++; q.Enqueue(rootEl); }
                while (q.Count > 0)
                {
                    var cur = q.Dequeue();
                    if (!elAdj.TryGetValue(cur, out var nbrs)) continue;
                    foreach (var n in nbrs)
                    {
                        if (visitedElements.Add(n)) { compCount++; q.Enqueue(n); }
                    }
                }
                prdDbg($"[ROOTDBG] Component {compIdx}: size={compCount} (elements).");
                compIdx++;
            }
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo,
            Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj,
            HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOTDBG] Begin selection (prefer supply leaf with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;

            foreach (var el in topo.Elements)
            {
                if (visitedElements.Contains(el)) continue;
                int elemDeg = ElementDegree(el, nodeAdj);
                bool isLeaf = elemDeg == 1;
                int score = el.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply when known
                int dn = 0;
                try { dn = el.DN; } catch { dn = 0; }
                prdDbg($"[ROOTDBG] Consider {EId(el)}: elemDeg={elemDeg} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if (isLeaf && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = el;
                    bestPort = ChooseLeafPort(el, nodeAdj) ?? el.Ports.FirstOrDefault();
                    if (bestPort != null)
                        prdDbg($"[ROOTDBG]  -> update best to {EId(el)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOTDBG] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOTDBG]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            // Count distinct elements at this node excluding the candidate itself
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private static int ElementDegree(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var port in el.Ports)
            {
                if (!nodeAdj.TryGetValue(port.Node, out var list)) continue;
                foreach (var t in list)
                {
                    if (ReferenceEquals(t.el, el)) continue;
                    set.Add(t.el);
                }
            }
            return set.Count;
        }

        private static TPort? ChooseLeafPort(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            foreach (var port in el.Ports)
            {
                if (!nodeAdj.TryGetValue(port.Node, out var list)) continue;
                foreach (var t in list)
                {
                    if (!ReferenceEquals(t.el, el))
                        return port;
                }
            }
            return el.Ports.FirstOrDefault();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}



```

```csharp
using Autodesk.AutoCAD.Geometry;
using System.Runtime.CompilerServices;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        private readonly ElevationRegistry _registry = new();
        private readonly Dictionary<Type, IElementElevationSolver> _solvers = new();
        private readonly IElementElevationSolver _default = new DefaultElementElevationSolver();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            RegisterSolvers();
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpoints))
            {
                var ports = element.Ports;
                if (ports.Count >= 2 && endpoints.TryGetValue(ports[0], out var z0) && endpoints.TryGetValue(ports[1], out var z1))
                {
                    return z0 + t * (z1 - z0);
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void RegisterSolvers()
        {
            // Register element-specific solvers here (stubs for now).
            // Example:
            // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
            // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
            // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
            // Plane elbows would also have a solver interpreting roll Near/Far.
        }

        private void SolveFromRoot()
        {
            // Build adjacency by node to find neighbors
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }
            prdDbg($"[TRAV] Adjacency built: nodes={nodeAdj.Count}, elements={_topology.Elements.Count()}");

            // Handle disjoint networks: iterate until all elements are visited
            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                prdDbg($"[TRAV] Root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null) break;
                prdDbg($"[TRAV] Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}. entryZ=0.000");

                // Start this component at entryZ = 0.0
                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                stack.Push((rootEl, rootPort, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ) = stack.Pop();
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);
                    prdDbg($"[TRAV] Visit element: {EId(el)} via node={NodeId(entry.Node)} entryZ={entryZ:0.###}");

                    var solver = ResolveSolver(el);
                    var exits = solver.Solve(el, entry, entryZ, _registry);
                    prdDbg($"[TRAV] Solver exits: count={exits.Count}");

                    // Continue from exits to connected neighbors
                    foreach (var (exitPort, exitZ) in exits)
                    {
                        prdDbg($"[TRAV]  -> exit node={NodeId(exitPort.Node)} propagateZ={exitZ:0.###}");
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            prdDbg($"[TRAV]     neighbor: {EId(nel)} via node={NodeId(nport.Node)}");
                            stack.Push((nel, nport, exitZ));
                        }
                    }
                }
            }
        }

        private IElementElevationSolver ResolveSolver(ElementBase el)
        {
            var t = el.GetType();
            if (_solvers.TryGetValue(t, out var s)) return s;
            return _default;
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOT] Begin selection (prefer supply leaf element with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var el in topo.Elements)
            {
                if (visitedElements.Contains(el)) continue;
                int elemDeg = ElementDegree(el, nodeAdj);
                bool isLeaf = elemDeg == 1;
                int score = el.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = 0;
                try { dn = el.DN; } catch { dn = 0; }
                prdDbg($"[ROOT] Consider {EId(el)}: elemDeg={elemDeg} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if (isLeaf && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = el;
                    bestPort = ChooseLeafPort(el, nodeAdj) ?? el.Ports.FirstOrDefault();
                    if (bestPort != null)
                        prdDbg($"[ROOT]  -> update best to {EId(el)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOT] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOT]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private static int ElementDegree(TPipe p, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            if (nodeAdj.TryGetValue(p.A.Node, out var la))
            {
                foreach (var t in la)
                {
                    if (ReferenceEquals(t.el, p)) continue;
                    set.Add(t.el);
                }
            }
            if (nodeAdj.TryGetValue(p.B.Node, out var lb))
            {
                foreach (var t in lb)
                {
                    if (ReferenceEquals(t.el, p)) continue;
                    set.Add(t.el);
                }
            }
            return set.Count;
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
        {
            public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
            public int GetHashCode((ElementBase, TPort) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}






```

```csharp
using Autodesk.AutoCAD.Geometry;
using System.Runtime.CompilerServices;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        private readonly ElevationRegistry _registry = new();
        private readonly Dictionary<Type, IElementElevationSolver> _solvers = new();
        private readonly IElementElevationSolver _default = new DefaultElementElevationSolver();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            RegisterSolvers();
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpoints))
            {
                var ports = element.Ports;
                if (ports.Count >= 2 && endpoints.TryGetValue(ports[0], out var z0) && endpoints.TryGetValue(ports[1], out var z1))
                {
                    return z0 + t * (z1 - z0);
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void RegisterSolvers()
        {
            // Register element-specific solvers here (stubs for now).
            // Example:
            // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
            // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
            // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
            // Plane elbows would also have a solver interpreting roll Near/Far.
        }

        private void SolveFromRoot()
        {
            // Build adjacency by node to find neighbors
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }
            prdDbg($"[TRAV] Adjacency built: nodes={nodeAdj.Count}, elements={_topology.Elements.Count()}");

            // Handle disjoint networks: iterate until all elements are visited
            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                prdDbg($"[TRAV] Root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null) break;
                prdDbg($"[TRAV] Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}. entryZ=0.000");

                // Start this component at entryZ = 0.0
                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                stack.Push((rootEl, rootPort, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ) = stack.Pop();
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);
                    prdDbg($"[TRAV] Visit element: {EId(el)} via node={NodeId(entry.Node)} entryZ={entryZ:0.###}");

                    var solver = ResolveSolver(el);
                    var exits = solver.Solve(el, entry, entryZ, _registry);
                    prdDbg($"[TRAV] Solver exits: count={exits.Count}");

                    // Continue from exits to connected neighbors
                    foreach (var (exitPort, exitZ) in exits)
                    {
                        prdDbg($"[TRAV]  -> exit node={NodeId(exitPort.Node)} propagateZ={exitZ:0.###}");
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            prdDbg($"[TRAV]     neighbor: {EId(nel)} via node={NodeId(nport.Node)}");
                            stack.Push((nel, nport, exitZ));
                        }
                    }
                }
            }
        }

        private IElementElevationSolver ResolveSolver(ElementBase el)
        {
            var t = el.GetType();
            if (_solvers.TryGetValue(t, out var s)) return s;
            return _default;
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOT] Begin selection (prefer supply leaf element with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var el in topo.Elements)
            {
                if (visitedElements.Contains(el)) continue;
                int elemDeg = ElementDegree(el, nodeAdj);
                bool isLeaf = elemDeg == 1;
                int score = el.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = 0;
                try { dn = el.DN; } catch { dn = 0; }
                prdDbg($"[ROOT] Consider {EId(el)}: elemDeg={elemDeg} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if (isLeaf && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = el;
                    bestPort = ChooseLeafPort(el, nodeAdj) ?? el.Ports.FirstOrDefault();
                    if (bestPort != null)
                        prdDbg($"[ROOT]  -> update best to {EId(el)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOT] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOT]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private static int ElementDegree(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var port in el.Ports)
            {
                if (!nodeAdj.TryGetValue(port.Node, out var list)) continue;
                foreach (var t in list)
                {
                    if (ReferenceEquals(t.el, el)) continue;
                    set.Add(t.el);
                }
            }
            return set.Count;
        }

        private static TPort? ChooseLeafPort(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            foreach (var port in el.Ports)
            {
                if (!nodeAdj.TryGetValue(port.Node, out var list)) continue;
                foreach (var t in list)
                {
                    if (!ReferenceEquals(t.el, el))
                        return port;
                }
            }
            return el.Ports.FirstOrDefault();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
        {
            public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
            public int GetHashCode((ElementBase, TPort) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}






```

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Elevation;
using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.Routing;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;

using System.Diagnostics;
using System.IO;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");            
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();

                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Frem:
                                    case PipeTypeEnum.Retur:
                                    case PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
                }


            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);
                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());                            
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Elevation ➜ Routed skeleton -------------
                // Elevation provider based on traversal from a chosen root (largest-DN supply leaf)
                IElevationProvider elevation = new TraversalElevationProvider(topo);
                var routed = new Router(topo).Route(elevation);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;                            
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTRDOT")]
        public void ntrdot()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var dwgPath = localDb.Filename;
                var outDotPath = @"C:\Temp\ntrdot.dot";

                // Build topology (same filter as export)
                var ents = localDb.GetFjvEntities(tx);
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
                bool isAccepted(Entity ent)
                {
                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }
                ents = ents.Where(x => isAccepted(x)).ToHashSet();

                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);

                // Root selection debug (no traversal) - logs candidates and chosen roots per component
                prdDbg("[NTRDOT] Root selection debug start...");
                RootSelector.DebugPickRoots(topo);

                // Build node -> (element, port) adjacency
                var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
                foreach (var el in topo.Elements)
                {
                    foreach (var p in el.Ports)
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                        list.Add((el, p));
                    }
                }

                // Build element adjacency (undirected)
                var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
                foreach (var kv in nodeAdj)
                {
                    var items = kv.Value;
                    for (int i = 0; i < items.Count; i++)
                    {
                        for (int j = i + 1; j < items.Count; j++)
                        {
                            var a = items[i].el;
                            var b = items[j].el;
                            if (ReferenceEquals(a, b)) continue;
                            if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                            if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                            setA.Add(b);
                            setB.Add(a);
                        }
                    }
                }

                // Find connected components of elements
                var components = new List<List<ElementBase>>();
                var visited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                foreach (var el in topo.Elements)
                {
                    if (!visited.Add(el))
                        continue;
                    var comp = new List<ElementBase>();
                    var q = new Queue<ElementBase>();
                    q.Enqueue(el);
                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        comp.Add(cur);
                        if (elAdj.TryGetValue(cur, out var nbrs))
                        {
                            foreach (var n in nbrs)
                            {
                                if (visited.Add(n))
                                    q.Enqueue(n);
                            }
                        }
                    }
                    components.Add(comp);
                }

                // Emit DOT
                var sb = new StringBuilder();
                sb.AppendLine("graph G {");
                sb.AppendLine("  graph [compound=true];");
                sb.AppendLine("  node [shape=box, fontsize=10];");

                // Node labels and subgraphs
                int clusterIdx = 0;
                foreach (var comp in components)
                {
                    sb.AppendLine($"  subgraph cluster_{clusterIdx} {{");
                    sb.AppendLine($"    label=\"component {clusterIdx}\";");
                    sb.AppendLine("    color=lightgrey;");

                    // Define nodes
                    foreach (var e in comp)
                    {
                        var id = e.Source.ToString();                        
                        var label = e.DotLabelForTest();
                        sb.AppendLine($"    \"{id}\" [label=\"{label}\"];");
                    }

                    // DFS edges inside component, starting from chosen root
                    ElementBase ChooseRootForComp(List<ElementBase> c)
                    {
                        ElementBase? best = null;
                        int bestScore = -1;
                        int bestDn = -1;
                        foreach (var e in c)
                        {
                            int deg = ElementDegree(e, nodeAdj);
                            if (deg != 1) continue;
                            int score = e.Type == PipeTypeEnum.Frem ? 1 : 0;
                            int dn = 0;
                            try { dn = e.DN; } catch { dn = 0; }
                            if (score > bestScore || (score == bestScore && dn > bestDn))
                            {
                                bestScore = score;
                                bestDn = dn;
                                best = e;
                            }
                        }
                        if (best != null) return best;
                        // Fallback: element with largest DN in component
                        best = c.OrderByDescending(x => { try { return x.DN; } catch { return 0; } }).First();
                        return best;
                    }

                    int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
                    {
                        var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
                        foreach (var t in items)
                        {
                            if (ReferenceEquals(t.el, exclude)) continue;
                            set.Add(t.el);
                        }
                        return set.Count;
                    }

                    int ElementDegree(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> adj)
                    {
                        var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
                        foreach (var p in el.Ports)
                        {
                            if (!adj.TryGetValue(p.Node, out var list)) continue;
                            foreach (var t in list)
                            {
                                if (ReferenceEquals(t.el, el)) continue;
                                set.Add(t.el);
                            }
                        }
                        return set.Count;
                    }

                    var root = ChooseRootForComp(comp);
                    prdDbg($"[NTRDOT] Component {clusterIdx} DFS root: {root.Source} / {root.GetType().Name}");

                    // Build DFS ordered edges
                    var dfsVisited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                    var edgesOrdered = new List<(string a, string b)>();

                    void Dfs(ElementBase cur, ElementBase? parent)
                    {
                        dfsVisited.Add(cur);
                        if (!elAdj.TryGetValue(cur, out var nbrs)) return;
                        // stable order
                        var ordered = nbrs.Where(n => comp.Contains(n))
                                          .OrderBy(n => n.Source.ToString(), StringComparer.Ordinal)
                                          .ToList();
                        foreach (var n in ordered)
                        {
                            if (parent != null && ReferenceEquals(n, parent)) continue;
                            if (!dfsVisited.Contains(n))
                            {
                                edgesOrdered.Add((cur.Source.ToString(), n.Source.ToString()));
                                Dfs(n, cur);
                            }
                        }
                    }

                    Dfs(root, null);
                    // Cover any isolated or remaining nodes (safety)
                    foreach (var e in comp)
                    {
                        if (!dfsVisited.Contains(e))
                            Dfs(e, null);
                    }

                    foreach (var (a, b) in edgesOrdered)
                        sb.AppendLine($"    \"{a}\" -- \"{b}\";");

                    sb.AppendLine("  }");
                    clusterIdx++;
                }

                sb.AppendLine("}");

                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
                prdDbg($"DOT written: {outDotPath}");

                // Run Graphviz (PDF)
                var cmd = new Process();
                cmd.StartInfo.FileName = "dot";
                cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.WaitForExit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

You’re right; root can be any element. I’ve updated the root-selection logic and DOT DFS to treat every element as a candidate, not just pipes.

What changed
- Root selection (both debug RootSelector and traversal provider):
  - Candidates: all `Topology.Elements` (not just `TPipe`).
  - Leaf definition: element-degree across all its ports equals 1 (i.e., exactly one distinct neighboring element). This prevents a pipe between two elbows from being misclassified as a leaf.
  - Preference: elements with `Type == Frem` (when available) are preferred; DN used as tie-breaker. For non-pipes, `ElementBase.DN` is still available via overrides (e.g., fittings read DN1).
  - Starting port: choose the port that actually connects to the neighbor (otherwise first port).
  - Debug logs now show elemDeg, and candidate is “any element”.

- NTRDOT DFS
  - Root per component is now any element with leaf-degree 1 (prefer Frem, then DN); fallback is the element with the largest DN in the component.
  - DFS edges are emitted starting from that root, so the printed order follows the chosen starting element.

Why the earlier misclassification happened
- The previous heuristic only checked per-end node degree, which can flag a pipe between two elbows as “leaf” if each node has degree 1. Element-degree fixes this by counting unique neighbors across both ends (and all ports for generic elements).

Try it now
- Run NTRDOT again. In prdDbg:
  - “[ROOTDBG] Consider … elemDeg=…” appears for all elements.
  - Chosen roots should match your expectation (true leaves, not middle-of-chain elements).
- The DOT will start DFS from the chosen root per component.

If you want a different preference (e.g., prefer specific fitting classes), we can add a class-based weight on top of Frem/DN.

---

**User**

okay, what about tees and other elements (there are a couple) that have three ports? two mains and one branch in the case of tee?

---

**Cursor**

- Current rule looks at element-degree: the number of distinct neighboring elements across all of an element’s ports. A tee with two mains + one branch typically has elemDeg=3, so it won’t be chosen as a leaf-root. If a tee is truly dangling (only one connected neighbor across all its ports), elemDeg=1 → it can be a root.

- Port choice on a multi‑port element: we currently pick the first connected port. If you want semantics (e.g., prefer `TPort.Role == Branch` when it’s the only connected side), we can change `ChooseLeafPort` to prioritize Branch > Main > Neutral among the connected ports.

---

**User**

okay, the recent changes have helped to make graph look as it should. now why does some nodes look like the one on the posted image? To provide some context, it's a pipe node with 14 extra self-referencing edges

---

**Cursor**

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using NTRExport.Elevation;
using NTRExport.Interfaces;
using NTRExport.Ntr;
using NTRExport.NtrConfiguration;
using NTRExport.Routing;
using NTRExport.SoilModel;
using NTRExport.TopologyModel;

using System.Diagnostics;
using System.IO;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(NTRExport.Commands))]

namespace NTRExport
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til NTR Export!\n");            
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
        new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        }
        #endregion

        [CommandMethod("DXFEXPORT")]
        public void dxfexport()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();

                foreach (var graph in pn.PipelineGraphs)
                {
                    foreach (var node in graph)
                    {
                        IPipelineV2 ppl = node.Value;
                        var topopl = ppl.GetTopologyPolyline();
                        var sizes = ppl.PipelineSizes;

                        #region First try
                        List<(Polyline pl, SizeEntryV2 size)> results = new();
                        if (sizes.Length == 1)
                            results.Add((topopl, sizes[0]));
                        else
                        {
                            DoubleCollection dc = new DoubleCollection();

                            //Skip first as Start Station is zero
                            for (int i = 1; i < sizes.Length; i++)
                            {
                                var l = sizes[i].StartStation;
                                dc.Add(topopl.GetParameterAtDistance(l));
                            }

                            using var objs = topopl.GetSplitCurves(dc);

                            if (objs.Count != sizes.Length)
                            {
                                prdDbg($"Pipeline {ppl.Name} failed to split correctly!");
                                prdDbg($"Sizes count: {sizes.Length}; Objs count: {objs.Count}");
                                prdDbg(sizes);
                                continue;
                            }

                            for (int i = 0; i < sizes.Length; i++)
                            {
                                results.Add(((Polyline)objs[i], sizes[i]));
                            }
                        }

                        foreach (var (pl, size) in results)
                        {
                            var layer = PipeScheduleV2.GetLayerName(
                                size.DN, size.System, size.Type);
                            localDb.CheckOrCreateLayer(
                                layer, PipeScheduleV2.GetColorForDim(layer));
                            pl.Layer = layer;
                            pl.ConstantWidth = PipeScheduleV2.GetPipeKOd(
                                size.System, size.DN, size.Type, size.Series) / 1000;
                            pl.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                throw;
            }
            tx.Commit();
        }

        //[CommandMethod("NTREXPORTV2")]
        public void ntrexportv2()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager dm = new(new DataReferencesOptions());

            using var fDb = dm.Fremtid();
            using var fTx = fDb.TransactionManager.StartTransaction();

            using var aDb = dm.Alignments();
            using var aTx = aDb.TransactionManager.StartTransaction();

            using var tx = localDb.TransactionManager.StartTransaction();

            #region Clean previous entities
            var toDelete = localDb.GetFjvPipes(tx);
            if (toDelete.Count > 0)
            {
                foreach (var item in toDelete)
                {
                    item.CheckOrOpenForWrite();
                    item.Erase(true);
                }
            }
            #endregion

            try
            {
                var ents = fDb.GetFjvEntities(fTx);
                var als = aDb.HashSetOfType<Alignment>(aTx);

                #region Basic implementation of rotation
                //Read any NtrData from fremtidig
                var ntrPsm = new PropertySetManager(
                    fDb, PSetDefs.DefinedSets.NtrData);
                var ntrDef = new PSetDefs.NtrData();

                Dictionary<Entity, double> rotationDict = new();

                foreach (var ent in ents)
                {
                    var rotation = ntrPsm.ReadPropertyDouble(
                        ent, ntrDef.ElementRotation);
                    if (rotation == 0) continue;
                    rotationDict[ent] = rotation;
                }
                #endregion

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreatePipelineGraph();
                pn.CreateSizeArrays();
                pn.CreateSegmentGraphs();

                var ntrgraphs = new List<Graph<INtrSegment>>();

                if (pn.PipelineGraphs == null) return;

                foreach (var pgraph in pn.PipelineGraphs)
                {
                    var sgraph = pgraph.Root.Value.SegmentsGraph;
                    if (sgraph == null)
                    {
                        prdDbg($"WARNING: Segments graph for " +
                        $"{pgraph.Root.Value.Name} is null!"); continue;
                    }

                    Node<INtrSegment> TranslateGraph(
                        Node<IPipelineSegmentV2> proot,
                        Dictionary<Entity, double> rdict)
                    {
                        INtrSegment ntrSegment;
                        switch (proot.Value)
                        {
                            case PipelineSegmentV2 pseg:
                                switch (pseg.Size.Type)
                                {
                                    case PipeTypeEnum.Twin:
                                        ntrSegment = new NtrSegmentTwin(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Frem:
                                    case PipeTypeEnum.Retur:
                                    case PipeTypeEnum.Enkelt:
                                        ntrSegment = new NtrSegmentEnkelt(pseg, rdict);
                                        break;
                                    case PipeTypeEnum.Ukendt:
                                    default:
                                        throw new NotImplementedException();
                                }
                                break;
                            case PipelineTransitionV2 tseg:
                                ntrSegment = new NtrSegmentTransition(tseg, rdict);
                                break;
                            default:
                                throw new System.Exception(
                                    $"ERR8736: Encountered unknown type: " +
                                    $"{proot.Value.GetType()}");
                        }

                        var node = new Node<INtrSegment>(ntrSegment);

                        foreach (var child in proot.Children)
                        {
                            var cnode = TranslateGraph(child, rdict);
                            cnode.Parent = node;
                            node.AddChild(cnode);
                        }

                        return node;
                    }
                    var root = TranslateGraph(sgraph.Root, rotationDict);
                    var ntrgraph = new Graph<INtrSegment>(
                        root,
                        ntr => $"{ntr.PipelineSegment.Owner.Name}-{ntr.PipelineSegment.MidStation}",
                        ntr => $"{ntr.PipelineSegment.Label}"
                        );
                    ntrgraphs.Add(ntrgraph);
                }


            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);
                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTREXPORT")]
        public void ntrexport()
        {
            ntrexportmethod();
        }
#if DEBUG
        [CommandMethod("NTRTEST")]
        public void ntrtest()
        {
            ntrexportmethod(new ConfigurationData(
                new NtrLast("FJV_FREM_P10_T80", "10", "80", "1000"),
                new NtrLast("FJV_RETUR_P10_T45", "10", "45", "1000")));
        }
#endif
        internal void ntrexportmethod(ConfigurationData? ntrConf = null)
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;            

            using var tx = localDb.TransactionManager.StartTransaction();

            var dwgPath = localDb.Filename;
            var outNtrPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".ntr");
            var outExceptionPath = Path.ChangeExtension(
                string.IsNullOrEmpty(dwgPath) ? "export" : dwgPath, ".exception.log");

            try
            {
                var ents = localDb.GetFjvEntities(tx);

                #region ------------- FILTER -------------
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };

                bool isAccepted(Entity ent)
                {
#if DEBUG
                    //prdDbg("DEBUG filtering active! Polylines only.");
                    //if (ent is BlockReference) return false;
#endif

                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(
                                PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(
                                br.GetPipeSystemEnum());                            
                        default:
                            return false;
                    }
                }

                ents = ents.Where(x => isAccepted(x)).ToHashSet();
                #endregion

                #region ------------- CONFIG -------------
                const double CushionReach = 2.0;       // m
                var soilDefault = new SoilProfile("Soil_Default", 0.00);
                var soilC80 = new SoilProfile("Soil_C80", 0.08);
                #endregion

                #region ------------- CAD ➜ Port topology -------------
                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);
                #endregion

                #region ------------- Topology-level soil planning -------------
                new TopologySoilPlanner(topo, 2.0, soilDefault, soilC80).Apply();
                #endregion

                #region ------------- Read NTR configuration from Excel -------------
                var conf = ntrConf ?? new ConfigurationData();
                //foreach (var l in conf.Last) prdDbg(l);
                #endregion

                #region ------------- Initialize NTR coordinate normalization -------------
                NtrCoord.InitFromTopology(topo, marginMeters: 0.0);
                #endregion

                #region ------------- Topology ➜ Elevation ➜ Routed skeleton -------------
                // Elevation provider based on traversal from a chosen root (largest-DN supply leaf)
                IElevationProvider elevation = new TraversalElevationProvider(topo);
                var routed = new Router(topo).Route(elevation);
                #endregion

                #region ------------- Emit NTR -------------
                var writer = new NtrWriter(new Rohr2SoilAdapter(), conf);
                // Build IS and DN records across all distinct pipe groups found in the routed model
                var headerLines = new List<string>();
                var headerDedup = new HashSet<string>();

                // Group routed members by System, normalized Type (Enkelt/Twin), Series, and Twin flag
                var routedGroups = routed.Members.GroupBy(m => (
                    sys: m.System,
                    typ: m.Type == PipeTypeEnum.Twin ? PipeTypeEnum.Twin : PipeTypeEnum.Enkelt,
                    ser: m.Series,
                    twin: m.Type == PipeTypeEnum.Twin
                ));

                foreach (var rgrp in routedGroups)
                {
                    var dnsInGroup = new HashSet<int>();

                    foreach (var member in rgrp)
                    {
                        switch (member)
                        {
                            case RoutedReducer red:
                                if (red.Dn1 > 0) dnsInGroup.Add(red.Dn1);
                                if (red.Dn2 > 0) dnsInGroup.Add(red.Dn2);
                                break;
                            case RoutedTee tee:
                                if (tee.DN > 0) dnsInGroup.Add(tee.DN);
                                if (tee.DnBranch > 0) dnsInGroup.Add(tee.DnBranch);
                                break;                            
                            default:
                                if (member.DN > 0) dnsInGroup.Add(member.DN);
                                break;
                        }
                    }

                    if (dnsInGroup.Count == 0) continue;

                    var catalog = new NtrDnCatalog(rgrp.Key.sys, rgrp.Key.typ, rgrp.Key.ser, rgrp.Key.twin);
                    foreach (var line in catalog.BuildRecords(dnsInGroup))
                    {
                        if (headerDedup.Add(line)) headerLines.Add(line);
                    }
                }

                var ntrText = writer.Build(routed, headerLines);

                // Save next to DWG                
                File.WriteAllText(outNtrPath, ntrText, System.Text.Encoding.UTF8);
                prdDbg($"NTR written: {outNtrPath}");
                #endregion
            }
            catch (DebugPointException dbex)
            {
                prdDbg(dbex);

                File.WriteAllText(outExceptionPath,
                    dbex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();

                using var dtx = localDb.TransactionManager.StartTransaction();
                foreach (var p in dbex.PointsToMark)
                {
                    DebugHelper.CreateDebugLine(p, ColorByName("red"));
                }
                dtx.Commit();
                return;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);

                File.WriteAllText(outExceptionPath,
                    ex.ToString(),
                    System.Text.Encoding.UTF8);

                tx.Abort();
                return;
            }
            tx.Commit();
        }

        [CommandMethod("NTRDOT")]
        public void ntrdot()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using var tx = localDb.TransactionManager.StartTransaction();
            try
            {
                var dwgPath = localDb.Filename;
                var outDotPath = @"C:\Temp\ntrdot.dot";

                // Build topology (same filter as export)
                var ents = localDb.GetFjvEntities(tx);
                var acceptedSystems = new HashSet<PipeSystemEnum>() { PipeSystemEnum.Stål };
                bool isAccepted(Entity ent)
                {
                    switch (ent)
                    {
                        case Polyline _:
                            return acceptedSystems.Contains(PipeScheduleV2.GetPipeSystem(ent));
                        case BlockReference br:
                            return acceptedSystems.Contains(br.GetPipeSystemEnum());
                        default:
                            return false;
                    }
                }
                ents = ents.Where(x => isAccepted(x)).ToHashSet();

                var polylines = ents.OfType<Polyline>().ToList();
                var fittings = ents.OfType<BlockReference>().ToList();
                var topo = TopologyBuilder.Build(polylines, fittings);

                // Root selection debug (no traversal) - logs candidates and chosen roots per component
                prdDbg("[NTRDOT] Root selection debug start...");
                RootSelector.DebugPickRoots(topo);

                // Build node -> (element, port) adjacency
                var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
                foreach (var el in topo.Elements)
                {
                    foreach (var p in el.Ports)
                    {
                        if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                        list.Add((el, p));
                    }
                }

                // Build element adjacency (undirected)
                var elAdj = new Dictionary<ElementBase, HashSet<ElementBase>>(new RefEq<ElementBase>());
                foreach (var kv in nodeAdj)
                {
                    var items = kv.Value;
                    for (int i = 0; i < items.Count; i++)
                    {
                        for (int j = i + 1; j < items.Count; j++)
                        {
                            var a = items[i].el;
                            var b = items[j].el;
                            if (ReferenceEquals(a, b)) continue;
                            if (!elAdj.TryGetValue(a, out var setA)) elAdj[a] = setA = new(new RefEq<ElementBase>());
                            if (!elAdj.TryGetValue(b, out var setB)) elAdj[b] = setB = new(new RefEq<ElementBase>());
                            setA.Add(b);
                            setB.Add(a);
                        }
                    }
                }

                // Find connected components of elements
                var components = new List<List<ElementBase>>();
                var visited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                foreach (var el in topo.Elements)
                {
                    if (!visited.Add(el))
                        continue;
                    var comp = new List<ElementBase>();
                    var q = new Queue<ElementBase>();
                    q.Enqueue(el);
                    while (q.Count > 0)
                    {
                        var cur = q.Dequeue();
                        comp.Add(cur);
                        if (elAdj.TryGetValue(cur, out var nbrs))
                        {
                            foreach (var n in nbrs)
                            {
                                if (visited.Add(n))
                                    q.Enqueue(n);
                            }
                        }
                    }
                    components.Add(comp);
                }

                // Emit DOT
                var sb = new StringBuilder();
                sb.AppendLine("graph G {");
                sb.AppendLine("  graph [compound=true];");
                sb.AppendLine("  node [shape=box, fontsize=10];");

                // Node labels and subgraphs
                int clusterIdx = 0;
                foreach (var comp in components)
                {
                    sb.AppendLine($"  subgraph cluster_{clusterIdx} {{");
                    sb.AppendLine($"    label=\"component {clusterIdx}\";");
                    sb.AppendLine("    color=lightgrey;");

                    // Define nodes
                    foreach (var e in comp)
                    {
                        var id = e.Source.ToString();                        
                        var label = e.DotLabelForTest();
                        sb.AppendLine($"    \"{id}\" [label=\"{label}\"];");
                    }

                    // DFS edges inside component, starting from chosen root
                    ElementBase ChooseRootForComp(List<ElementBase> c)
                    {
                        ElementBase? best = null;
                        int bestScore = -1;
                        int bestDn = -1;
                        foreach (var e in c)
                        {
                            int deg = ElementDegree(e, nodeAdj);
                            if (deg != 1) continue;
                            int score = e.Type == PipeTypeEnum.Frem ? 1 : 0;
                            int dn = 0;
                            try { dn = e.DN; } catch { dn = 0; }
                            if (score > bestScore || (score == bestScore && dn > bestDn))
                            {
                                bestScore = score;
                                bestDn = dn;
                                best = e;
                            }
                        }
                        if (best != null) return best;
                        // Fallback: element with largest DN in component
                        best = c.OrderByDescending(x => { try { return x.DN; } catch { return 0; } }).First();
                        return best;
                    }

                    int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
                    {
                        var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
                        foreach (var t in items)
                        {
                            if (ReferenceEquals(t.el, exclude)) continue;
                            set.Add(t.el);
                        }
                        return set.Count;
                    }

                    int ElementDegree(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> adj)
                    {
                        var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
                        foreach (var p in el.Ports)
                        {
                            if (!adj.TryGetValue(p.Node, out var list)) continue;
                            foreach (var t in list)
                            {
                                if (ReferenceEquals(t.el, el)) continue;
                                set.Add(t.el);
                            }
                        }
                        return set.Count;
                    }

                    var root = ChooseRootForComp(comp);
                    prdDbg($"[NTRDOT] Component {clusterIdx} DFS root: {root.Source} / {root.GetType().Name}");

                    // Build DFS ordered edges
                    var dfsVisited = new HashSet<ElementBase>(new RefEq<ElementBase>());
                    var edgesOrdered = new List<(string a, string b)>();
                    var edgeSeen = new HashSet<string>(StringComparer.Ordinal);

                    void Dfs(ElementBase cur, ElementBase? parent)
                    {
                        dfsVisited.Add(cur);
                        if (!elAdj.TryGetValue(cur, out var nbrs)) return;
                        // stable order
                        var ordered = nbrs.Where(n => comp.Contains(n))
                                          .OrderBy(n => n.Source.ToString(), StringComparer.Ordinal)
                                          .ToList();
                        foreach (var n in ordered)
                        {
                            if (parent != null && ReferenceEquals(n, parent)) continue;
                            if (!dfsVisited.Contains(n))
                            {
                                var ida = cur.Source.ToString();
                                var idb = n.Source.ToString();
                                if (!string.Equals(ida, idb, StringComparison.Ordinal))
                                {
                                    // dedupe undirected edge
                                    var key = string.CompareOrdinal(ida, idb) <= 0 ? $"{ida}--{idb}" : $"{idb}--{ida}";
                                    if (edgeSeen.Add(key))
                                        edgesOrdered.Add((ida, idb));
                                }
                                Dfs(n, cur);
                            }
                        }
                    }

                    Dfs(root, null);
                    // Cover any isolated or remaining nodes (safety)
                    foreach (var e in comp)
                    {
                        if (!dfsVisited.Contains(e))
                            Dfs(e, null);
                    }

                    foreach (var (a, b) in edgesOrdered)
                        sb.AppendLine($"    \"{a}\" -- \"{b}\";");

                    sb.AppendLine("  }");
                    clusterIdx++;
                }

                sb.AppendLine("}");

                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(outDotPath, sb.ToString(), utf8NoBom);
                prdDbg($"DOT written: {outDotPath}");

                // Run Graphviz (PDF)
                var cmd = new Process();
                cmd.StartInfo.FileName = "dot";
                cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                cmd.StartInfo.Arguments = "-Tpdf ntrdot.dot -o ntrdot.pdf";
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();
                cmd.WaitForExit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }
            tx.Commit();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
```

Likely cause: self-loop edges are getting into the DOT. Even though we build an element adjacency that excludes self, DFS edges weren’t guarding against a==b or duplicate undirected edges. I’ve updated NTRDOT DFS emission to:

- Skip self loops explicitly:
  - Only add an edge if ida != idb.
- Dedupe undirected edges:
  - Track a key min(ida,idb)–max(ida,idb) in a HashSet before adding.

This should eliminate the “14 extra self-referencing edges” and the oval swirl around that node. If you still see loops, paste a few DOT lines around that node ID so we can inspect whether the IDs are colliding.

---

**User**

okay, that removed the extra edges from the dot output. But are you sure it won't surface in the elevation analysis? I don't want the traversal running in loops over the same element multiple times

---

**Cursor**

```csharp
using Autodesk.AutoCAD.Geometry;
using System.Runtime.CompilerServices;
using static IntersectUtilities.UtilsCommon.Utils;

using NTRExport.TopologyModel;

namespace NTRExport.Elevation
{
    internal sealed class TraversalElevationProvider : IElevationProvider
    {
        private readonly Topology _topology;
        private readonly ElevationRegistry _registry = new();
        private readonly Dictionary<Type, IElementElevationSolver> _solvers = new();
        private readonly IElementElevationSolver _default = new DefaultElementElevationSolver();

        public TraversalElevationProvider(Topology topology)
        {
            _topology = topology;
            RegisterSolvers();
            SolveFromRoot();
        }

        public double GetZ(ElementBase element, Point3d a, Point3d b, double t)
        {
            if (_registry.TryGetEndpointZ(element, out var endpoints))
            {
                var ports = element.Ports;
                if (ports.Count >= 2 && endpoints.TryGetValue(ports[0], out var z0) && endpoints.TryGetValue(ports[1], out var z1))
                {
                    return z0 + t * (z1 - z0);
                }
            }
            // Fallback to geometry Z
            var za = a.Z;
            var zb = b.Z;
            return za + t * (zb - za);
        }

        private void RegisterSolvers()
        {
            // Register element-specific solvers here (stubs for now).
            // Example:
            // _solvers[typeof(AfgreningMedSpring)] = new AfgreningMedSpringSolver();
            // _solvers[typeof(PreinsulatedElbowAbove45deg)] = new VerticalElbowSolver();
            // _solvers[typeof(PreinsulatedElbowAtOrBelow45deg)] = new VerticalElbowSolver();
            // Plane elbows would also have a solver interpreting roll Near/Far.
        }

        private void SolveFromRoot()
        {
            // Build adjacency by node to find neighbors
            var nodeAdj = new Dictionary<TNode, List<(ElementBase el, TPort port)>>(new RefEq<TNode>());
            foreach (var el in _topology.Elements)
            {
                foreach (var p in el.Ports)
                {
                    if (!nodeAdj.TryGetValue(p.Node, out var list)) nodeAdj[p.Node] = list = new();
                    list.Add((el, p));
                }
            }
            prdDbg($"[TRAV] Adjacency built: nodes={nodeAdj.Count}, elements={_topology.Elements.Count()}");

            // Handle disjoint networks: iterate until all elements are visited
            var visitedPairs = new HashSet<(ElementBase, TPort)>(new ElPortPairEq());
            var visitedElements = new HashSet<ElementBase>(new RefEq<ElementBase>());

            while (true)
            {
                prdDbg($"[TRAV] Root selection pass. visitedElements={visitedElements.Count}");
                var (rootEl, rootPort) = PickRoot(_topology, nodeAdj, visitedElements);
                if (rootEl == null || rootPort == null) break;
                prdDbg($"[TRAV] Root chosen: {EId(rootEl)} at node={NodeId(rootPort.Node)}. entryZ=0.000");

                // Start this component at entryZ = 0.0
                var stack = new Stack<(ElementBase el, TPort entryPort, double entryZ)>();
                stack.Push((rootEl, rootPort, 0.0));

                while (stack.Count > 0)
                {
                    var (el, entry, entryZ) = stack.Pop();
                    if (visitedElements.Contains(el))
                    {
                        prdDbg($"[TRAV] Skip element (already visited): {EId(el)}");
                        continue;
                    }
                    if (!visitedPairs.Add((el, entry))) continue;
                    visitedElements.Add(el);
                    prdDbg($"[TRAV] Visit element: {EId(el)} via node={NodeId(entry.Node)} entryZ={entryZ:0.###}");

                    var solver = ResolveSolver(el);
                    var exits = solver.Solve(el, entry, entryZ, _registry);
                    prdDbg($"[TRAV] Solver exits: count={exits.Count}");

                    // Continue from exits to connected neighbors
                    foreach (var (exitPort, exitZ) in exits)
                    {
                        prdDbg($"[TRAV]  -> exit node={NodeId(exitPort.Node)} propagateZ={exitZ:0.###}");
                        if (!nodeAdj.TryGetValue(exitPort.Node, out var neighbors)) continue;
                        foreach (var (nel, nport) in neighbors)
                        {
                            if (ReferenceEquals(nel, el)) continue;
                            if (visitedElements.Contains(nel))
                            {
                                prdDbg($"[TRAV]     neighbor already visited, skip: {EId(nel)}");
                                continue;
                            }
                            prdDbg($"[TRAV]     neighbor: {EId(nel)} via node={NodeId(nport.Node)}");
                            stack.Push((nel, nport, exitZ));
                        }
                    }
                }
            }
        }

        private IElementElevationSolver ResolveSolver(ElementBase el)
        {
            var t = el.GetType();
            if (_solvers.TryGetValue(t, out var s)) return s;
            return _default;
        }

        private static (ElementBase? el, TPort? port) PickRoot(Topology topo, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj, HashSet<ElementBase> visitedElements)
        {
            prdDbg("[ROOT] Begin selection (prefer supply leaf element with largest DN among unvisited).");
            ElementBase? bestEl = null;
            TPort? bestPort = null;
            int bestDn = -1;
            int bestScore = -1;
            foreach (var el in topo.Elements)
            {
                if (visitedElements.Contains(el)) continue;
                int elemDeg = ElementDegree(el, nodeAdj);
                bool isLeaf = elemDeg == 1;
                int score = el.Type == IntersectUtilities.UtilsCommon.Enums.PipeTypeEnum.Frem ? 1 : 0; // prefer supply
                int dn = 0;
                try { dn = el.DN; } catch { dn = 0; }
                prdDbg($"[ROOT] Consider {EId(el)}: elemDeg={elemDeg} score={score} dn={dn} currentBestScore={bestScore} bestDn={bestDn}");
                if (isLeaf && (score > bestScore || (score == bestScore && dn > bestDn)))
                {
                    bestScore = score;
                    bestDn = dn;
                    bestEl = el;
                    bestPort = ChooseLeafPort(el, nodeAdj) ?? el.Ports.FirstOrDefault();
                    if (bestPort != null)
                        prdDbg($"[ROOT]  -> update best to {EId(el)} at node={NodeId(bestPort.Node)}");
                }
            }
            if (bestEl != null && bestPort != null) return (bestEl, bestPort);

            // Fallback: pick any unvisited pipe with largest DN (choose A port)
            bestEl = null;
            bestPort = null;
            bestDn = -1;
            prdDbg("[ROOT] Fallback: no supply leaf found, choose largest DN unvisited pipe.");
            foreach (var p in topo.Pipes)
            {
                if (visitedElements.Contains(p)) continue;
                if (p.DN > bestDn)
                {
                    bestDn = p.DN;
                    bestEl = p;
                    bestPort = p.A;
                    prdDbg($"[ROOT]  -> fallback candidate {EId(p)} at node={NodeId(bestPort.Node)}");
                }
            }
            return (bestEl, bestPort);
        }

        private static int DegreeExcluding(List<(ElementBase el, TPort port)> items, ElementBase exclude)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var t in items)
            {
                if (ReferenceEquals(t.el, exclude)) continue;
                set.Add(t.el);
            }
            return set.Count;
        }

        private static int ElementDegree(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            var set = new HashSet<ElementBase>(new RefEq<ElementBase>());
            foreach (var port in el.Ports)
            {
                if (!nodeAdj.TryGetValue(port.Node, out var list)) continue;
                foreach (var t in list)
                {
                    if (ReferenceEquals(t.el, el)) continue;
                    set.Add(t.el);
                }
            }
            return set.Count;
        }

        private static TPort? ChooseLeafPort(ElementBase el, Dictionary<TNode, List<(ElementBase el, TPort port)>> nodeAdj)
        {
            foreach (var port in el.Ports)
            {
                if (!nodeAdj.TryGetValue(port.Node, out var list)) continue;
                foreach (var t in list)
                {
                    if (!ReferenceEquals(t.el, el))
                        return port;
                }
            }
            return el.Ports.FirstOrDefault();
        }

        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        private sealed class ElPortPairEq : IEqualityComparer<(ElementBase, TPort)>
        {
            public bool Equals((ElementBase, TPort) x, (ElementBase, TPort) y) =>
                ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2);
            public int GetHashCode((ElementBase, TPort) obj)
            {
                unchecked
                {
                    int h1 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1);
                    int h2 = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item2);
                    return (h1 * 397) ^ h2;
                }
            }
        }

        private static string EId(ElementBase el)
        {
            int dn = 0;
            try { dn = el.DN; } catch { dn = 0; }
            return $"{el.Source} / {el.GetType().Name} / DN={dn}";
        }
        private static string NodeId(TNode node)
        {
            var name = string.IsNullOrWhiteSpace(node.Name) ? null : node.Name;
            return name ?? $"@{RuntimeHelpers.GetHashCode(node)}";
        }
    }
}






```

Good catch. I’ve tightened the traversal to avoid looping over the same element:

- Pre-existing guards
  - We already skip pushing the same element as the immediate neighbor (parent).
  - We had visitedPairs<(element, entryPort)> to avoid revisiting the same entry tuple.

- New hard stop by element
  - Before processing a popped item, we now check `visitedElements.Contains(el)` and skip if true.
  - Before pushing a neighbor `(nel, nport)`, we check `visitedElements.Contains(nel)` and skip pushing if true.
  - Both actions are logged via prdDbg so you can see when/why a neighbor is suppressed.

Effect
- Each element is solved at most once per component, regardless of how many ports lead back to it (no “different entry port” loops).
- This is consistent with your model: once an element has produced exitZ for its ports, re-entering it is unnecessary and potentially conflicting.

If you’d prefer to allow re-entry for specific classes (e.g., measurement points) with a priority policy, we can add a whitelist, but the default now is strictly single-visit per element.

