# NTR Export Project Overview

## Purpose
- Translate district heating pipeline data from AutoCAD drawings (`.dwg`) into the ROHR2/SINETZ Neutral Interface (`.ntr`) format as documented in `InterfaceNeutral_e_01.01.md`.
- Support downstream pipe-stress calculations in the ROHR2 toolchain by producing compliant neutral files directly from design drawings.

## Source Data
- 2D CAD inputs: preinsulated pipe polylines and fitting block references created in Autocad.
- District heating conventions:
  - **Twin pipes**: supply/return steel pipes inside a shared plastic jacket, represented by a single polyline.
  - **Bonded systems**: supply and return in separate jackets placed side by side, represented by two polylines.
- Fittings: preinsulated tees, tees, elbows, and preinsulated elbows drawn as block references. Each fitting embeds `MuffeIntern*` nested block references that mark weld locations used to derive ports.
- Metadata from IntersectUtilities libraries: pipe schedules, property sets, and pipeline topology annotations.

## Processing Pipeline
1. `NTREXPORT` command filters Civil 3D entities (currently `Stål` system) and collects the relevant polylines/fittings.
2. `TopologyBuilder` converts CAD geometry into a canonical graph of nodes (`TNode`) and elements (`TPipe`, `TFitting`). Ports come from `MuffeIntern` weld markers, guaranteeing clean graph connectivity.
3. The topology is routed through `Routing.Router`, which expands fittings into geometric macros (straights, bends, reducers, tees) and resolves spacing/offset requirements before the routed members emit their own NTR records.
4. `NtrWriter` assembles header DN/IS records and element records (RO, BOG, RED, TEE, etc.) from the routed graph, writing the final `.ntr` file adjacent to the DWG.

## Routing & Geometry Logic
- **Twin duplication**: Every routed straight/bend can emit two NTR members (supply/return) with Z-offsets set to ±(OD + gap)/2, converting the 2D plan into a 3D-neutral representation.
- **Preinsulated elbows**: The router replaces the single Bend with a three-part macro (straight leg → bend → straight leg) using catalog leg length (`RoutingConfig.PreinsulatedLegMeters`).
- **Tees (twin-to-twin)**: The router trims the main run near the tee, offsets slightly along the main axis, inserts a short straight and bend into the branch direction, and then continues along the branch. Branch supply/return lines inherit the correct Z-offset and share the tee handle, yielding REF tokens like `REF=<handle>-1/-2` in the NTR output.
- **Reference tracking**: Every routed primitive carries the originating AutoCAD handle; when multiple NTR members originate from the same element, the writer emits `REF=<handle>-n`, aiding traceability back to the DWG.
- **Routing roadmap**:
  - Implement spacing S-bend macros for reducers and tees (twin spacing changes).
  - Support bonded ↔ twin transitions (F/Y) using bend/straight macro footprints.
  - Refine soil planning on routed straights (rule overlaps, topology-driven propagation).
  - Introduce push/propagation logic to satisfy macro footprints while staying within reasonable bounds.

## Geometry Considerations
- Input geometry is primarily 2D plan-view. During export, coordinates are normalized in meters and scaled to millimeters (`NtrCoord`, `NtrFormat`).
- Twin pipes are emitted as separate RO records with Z-offsets calculated from pipe spacing, providing the 3D separation required by ROHR2.
- Soil cover depth (`SOIL_H`) and optional cushion parameters are added per element to satisfy buried pipe requirements, with routed straights split wherever cushion spans apply.

## Automated Console Tests
- `NTRExport.ConsoleTests` launches `AcCoreConsole.exe`, `NETLOAD`s `NTRExport.dll`, and runs `NTREXPORT` against curated DWG assets (`preinsulated_elbow_90.dwg`, `twin_reducer_basic.dwg`, `tee_branch_basic.dwg`, `T1-Preinsulated tee, twin to twin.dwg`).
- Each test copies the DWG to a temp folder, runs a scripted console session, and asserts that the generated `.ntr` contains expected record patterns (RO, BOG, RED, TEE) via regex checks.
- Assets live under `NTRExport.ConsoleTests/Assets`, enabling smoke tests for routing macros and provenance tokens.

## External References
- Neutral interface specification: `Docs/InterfaceNeutral_e_01.01.md` (ROHR2/SINETZ Interface 01.01).
- Example output: `Docs/Example.ntr`.

## Open Points
- Remaining macros: reducer spacing, tee re-spacing, bonded ↔ twin transitions, and routed soil planner integration.
- `NtrSegmentEnkelt`, `NtrSegmentTwin`, and `NtrSegmentTransition` remain unimplemented; the alternate `NTREXPORTV2` pipeline is still experimental.
- Configuration relies on `X:\AC - NTR\NTR_CONFIG.xlsx` and interactive selection; unattended execution needs further handling.
- Additional documentation for 3D elevation derivation (e.g., node Z-level rules) may be required if source DWG stores explicit Z data.
