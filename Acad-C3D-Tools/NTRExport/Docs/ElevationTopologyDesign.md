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

