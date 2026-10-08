# LERCRAWL

Reload IntersectUtilities through the usual project loader, then run `LERCRAWL`.

1. Select the start **on a lightweight LER polyline inside an xref**. This chooses
   the xref instance and exact utility layer, for example `Vandledning_L2`.
   Xref prefixes are stripped; `Vandledning_L20` is a different layer.
   Non-LER references are rejected before their entities are scanned. A LER
   reference must have a `LER`, `2DLER`, `3DLER`, `LER2D` or `LER3D` token in its
   leaf reference name or drawing filename; comparison ignores case. A LER
   folder name alone does not qualify. Renamed references retain their drawing
   filename identity. A LER nested in a non-LER wrapper uses only the nested LER
   contents, retaining both insertion transforms.
2. Move towards the endpoint. A yellow X marks the snapped start; a red helper
   polyline previews the shortest connected route.
3. Click the endpoint. One **zero-width centreline** is created and selected in
   the active drawing's **model space**, on **0-REFERENCELINE**.
   Escape cancels without creating geometry.

Both point prompts retain running object snaps. **F3** toggles snapping;
**Shift+right-click** or typed overrides select a snap for one pick. Pick the
start on the utility in its xref. Accepted points are projected onto the LER
centreline for the route cuts; the preview follows the snapped cursor point.
The command does not change `OSMODE`.

Output is a **2D plan at Z=0**, preserving circular arcs and segment interiors.
Shortest routes use actual line/arc lengths. Source widths do not affect output:
zero, changing, and tapered widths all produce the same zero-width centreline.

The source xref is read only. The reference layer is made visible when needed;
existing colour and current layer are retained. An already-thawed current layer
is not assigned `IsFrozen=false`, which AutoCAD can reject with `eInvalidLayer`.

## Connections and transforms

Connection tolerance is **0.025 drawing units** (25 mm in metre drawings), matching
NSALIGNMENTCRAWL. Nearby endpoints/vertices connect, and endpoints on another run
form T-junctions. Crossing interiors alone do not connect. Small endpoint gaps
remain explicit straight connectors. Disconnected endpoints clear the preview
and report no route.

Translated, rotated, uniformly scaled and mirrored plan xrefs are supported,
including ordinary blocks and nested xrefs within the selected root instance.
Tilted polylines/nonuniform plan scales are rejected because a plan polyline
cannot retain their circular arcs exactly.

## Validation

On 2026-10-08, **43 native AutoCAD 2025 console regression cases passed** in an
isolated profile and temporary drawing. The ten new cases cover early non-LER
rejection before geometry reading, host/ordinary-block rejection, supported LER
naming tokens, renamed references, misleading parent/folder names and a LER
nested in a non-LER wrapper with both insertion transforms retained. All 33
existing geometry, routing and writing cases also passed. The Civil 3D GUI was
already unresponsive, so interactive selection there was not rerun.

On 2026-10-07, **33 native Civil 3D 2025 regression cases passed**. They cover
line/arc interior picks, reverse arcs, shortest loop routes, T-junctions,
disconnected crossings, tolerance gaps, closed paths, transforms, exact layers,
nested xref selection, source/host OwnerIds, rotated UCS, and centreline output
through zero, tapered and large widths. Writer fixtures verify exactly one
model-space entity, new/existing/current/off/frozen layers, colour preservation
and paper space as the current space.

Two actual `Vandledning_L2` routes (9 and 49 segments) in the active drawing
also appended exactly one centreline successfully. Both writes were rolled
back, and the original model-space entity count/current layer were preserved.

`LerCrawlRegression.csx` runs in ACD-MCP after loading the six core files:
models, graph, reader, reference identity, polyline builder and drawing writer. Strip file-scoped
namespaces and place imports first. Fixtures use disposable databases/temp DWGs.
The interactive click/preview workflow needs a user run after reload.

Used Autodesk members were checked against the installed 2025 assemblies and
exercised in the running Civil 3D process. `NonInteractivePickPoint` takes the
same UCS coordinates returned by `GetPoint`; the reader converts to WCS once.
The xref drawing filename uses Autodesk's read-only access to
[`BlockTableRecord.PathName`](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_BlockTableRecord_PathName.html),
also compiled and exercised against the installed 2025 assemblies.

To inspect an xref pipe's attached property sets, use **LERPROBE**; see
[its documentation](../LERProbe/README.md).
