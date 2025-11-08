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

