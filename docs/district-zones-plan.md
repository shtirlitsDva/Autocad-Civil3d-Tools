# Norsyn District Zones (NDZ) — Plan of Record

<status>
Approved-with-remarks by user 2026-06-25. This is the living plan. Companion:
`docs/PSv2-NHS-translation-research.md`. Supersedes session drafts v1/v2.
</status>

<what-it-is>
A NEW standalone managed AutoCAD 2025 .NET plugin. User attaches a DimV2 drafter export ("Fjernvarme
DIM.dwg" — pipes as `FJV-<Type>-<System><DN>` polylines) as an Xref, draws a polyline around the pipes →
the app makes a transparent colored **zone**. Drawing more polylines inside auto-**splits** the zone or
cuts a **hole + sub-zone**; **deleting** a boundary merges faces. Boundaries are **grip-editable** and
adjacent zones adapt. Each zone shows its **name + live pipe price** at its centre. A standalone WPF
window edits named, per-project price catalogs. Two exports: AutoCAD and GeoJSON.
</what-it-is>

---

<section-1-locked-decisions>

- **Managed-only (.NET).** Build the grip/topology in C#. ObjectARX/C++ is a fallback only if managed
  genuinely cannot deliver the grip UX — not the default.
- **Core model = shared-edge planar subdivision (DCEL)** as source of truth; ONE `NorsynContainer` as the
  persistent shell holding rendered fills + labels; a managed `GripOverrule` for editing.
- **Automatic detection** via a `CommandEnded` reactor wired in `Initialize()`, disposed in `Terminate()`
  (no armed-mode start/stop). Invalid polylines → transient red marker, no action.
- **Pricing is dynamic / never persisted.** User chooses which Xref(s) are the pipe source.
- **Price editor edits prices only** (per-metre + per-fitting), named catalogs stored via FlexDataStore,
  in a **standalone WPF window** launched by command (no palette).
- **Plugin/command structure copies `DimensioneringV2/src/DimensioneringV2/Commands.cs`** (NOT NSLOAD).
- **No `.bundle`.** Shipped as a plain `.dll` in a shared folder (NETLOAD / demand-load).
- **Central PSv2↔NHS translator** = one shared file owning the mapping (no duplication), per the research
  recommendation (option d). **DimV2 export edits are their own separate phase.**
- **DXF: out of scope**, ignored.
- Geometry: NetTopologySuite 2.5.0; reuse `NTSConversion.cs` adapters.
- Theme: dark via `DimensioneringV2/UI/Theme.xaml`, named resources only; add a DataGrid template to it.

</section-1-locked-decisions>

---

<section-2-core-model>

<2-1-topology-source-of-truth>
A managed **`PlanarSubdivision` (DCEL)**: shared `Vertex`/`Edge`/`Face`. Each `Face` = a zone (GUID,
number, name, stable random color). Each `Edge` is shared by ≤2 faces — this shared reference is what
makes "edit one boundary, neighbours follow" work. Persisted as JSON via FlexDataStore (+ XData anchor).

<2-2-rendering>
ONE `NorsynContainer` holds the rendered output, regenerated from the topology: per face an `MPolygon`
(transparent solid fill in the face's color, supports holes) + two `MText` (name/number above, price
below). The container is the selectable/persistent unit and the overrule target.

<2-3-editing-operations>
- **Add boundary** (automatic, §3): split / hole+subface / outer-zone creation.
- **Delete boundary**: removing an edge **merges** its two adjacent faces (one inherits identity; price
  recomputed). Exposed via grip/command.
- **Grip-edit boundary** (§4 overrule): move a topology vertex → all incident edges/faces rebuild +
  relabel. Shared vertex = adjacency adaptation falls out for free.

<2-4-fallback-note>
If managed grips can't deliver acceptable shared-edge UX, the fallback is a bespoke C++ subdivision
entity (Pipeline-style `GripEditEngine`). Not pursued unless managed is proven insufficient.

</section-2-core-model>

---

<section-3-automatic-detection>
`IExtensionApplication.Initialize()` wires `Document.CommandEnded`/`CommandCancelled` (+ new-document
hookup); `Terminate()` disposes and removes the overrule. Polylines drawn during a command are processed
at command-end in a fresh transaction (never mid-command). Classification: no faces + closed → outer
zone; inside a face + closed → hole+subface; inside a face + open with both ends on the boundary → split;
endpoint off-boundary / ambiguous → transient red + warning, no change. On success the source polyline is
consumed; affected faces recompute; all in one transaction (single Undo). Exceptions → warn + abort.
</section-3-automatic-detection>

---

<section-4-grip-editing>
`ZoneGripOverrule : GripOverrule` (managed, registered via an OverruleRegistry like
`ViewFrameCentreGripOverrule`) targets the container: `GetGripPoints` returns one grip per topology
vertex; `MoveGripPointsAt` updates the vertex, validates, rebuilds affected faces' children, relabels.
Invalid in-progress edits marked via `TransientManager` `DirectShortTerm` red overlays
(`BendRadiusLabelMarkerManager`/`TransientPreviewRenderer` pattern), disposed on clear. Snapping/OSnap
forwarded so boundaries are snappable.
</section-4-grip-editing>

---

<section-5-pricing>
- **Dynamic, never persisted.** Zones store only identity + geometry; price MText regenerated on demand.
- **Xref selection**: `NDZSETXREF` (and first-use prompt) picks pipe-source Xref(s), remembered in
  FlexDataStore. `NDZRECALC` re-reads + recomputes all zones (e.g. after re-export/reload).
- **Calc**: STRtree prefilter → `pipe.geom.Intersection(face)` in-memory clip → length → active-catalog
  `(PipeType,DN)` → `PricePerMeter×length` (+ `PricePerFitting` for service lines, subject to §8) → sum →
  centroid MText.
- **Catalog** (prices-only): `PipePriceCatalog{Name, List<PipePriceEntry{PipeType,Dn,PricePerMeter,
  PricePerFitting}>}`. Seeded as a COPY from NHS defaults (never written back). Named configs in
  FlexDataStore (`NDZ.PriceCatalogs`, `NDZ.ActiveCatalog`). Edit/switch → all zones dirty → recompute.
</section-5-pricing>

---

<section-6-exports>
- **Export-to-AutoCAD** (`NDZEXPORTACAD`): writes real entities — each face as a polygon (Polyline/MPolygon
  with the zone color/fill) + the name and price as MText — into the current drawing or a chosen target,
  so the result is a standalone annotated drawing independent of the live topology.
- **Export-to-GeoJSON** (`NDZEXPORTGEOJSON`): writes face polygons (via NTS → GeoJSON, reusing the repo's
  GeoJSON IO) with properties: name, number, price, per-pipe-type breakdown, area.
</section-6-exports>

---

<section-7-pipe-type-translation> (singular logic)
Per the research (`docs/PSv2-NHS-translation-research.md`), the only shared logic is the small
`PipeType ↔ PipeSystemEnum` map. Create ONE shared translator source file (linked into the
IntersectUtilities-side and into this plugin; later into DimV2.AutoCAD) that owns:
- the bidirectional material mapping,
- the `FJV-` layer-name parser (encapsulating PSv2's regex),
- the explicit correspondence table, with an **FL/SL hook** (kept as data, not derived).
PSv2 stays linked-source for now; the translator is shaped so a future promotion to a pure
`NorsynPipeDomain` core is a lift-and-shift. **No code duplicated.** DimV2's private
`TranslatePipeTypeToSystem` is replaced by a call to this translator — but only in the dedicated DimV2
phase (§9).
</section-7-pipe-type-translation>

---

<section-8-critical-blocker-flsl> 🔴
Service-line (stik) prices are provisional until FL/SL is carried on the export. Finding: the FL/SL bit is
still present at export (`PipeFamily.Name`/`SegmentType`), but PSv2's `PipeSystemEnum` has no FL/SL slot,
so it's dropped into the layer scheme.

**Chosen fix (user 2026-06-25): XData.** The DimV2 export writes the FL/SL distinction — recommended as the
**full NHS pipe identity** (`PipeType` + `SegmentType` + DN) under one RegApp (`NORSYN_NHS_PIPE`) — as
XData on each pipe polyline. Additive: does not touch the layer scheme or the shared enums, survives Xref
read + copy/WBLOCK. NDZ pricing then reads NHS identity directly and skips the lossy translation. Writing
it is **P12** (isolated DimV2 phase). NDZ `PipeReader` (P3) **prefers** the XData identity and **falls
back** to layer-name parsing + provisional stik rule when absent (works with old + new exports). Until P12
runs against a project's drawings, stik prices remain provisional. See memory `ndz-flsl-pricing-blocker`.
</section-8-critical-blocker-flsl>

---

<section-9-phases-and-execution>

<9-1-execution-mode>
Per user: once building, **don't stop between phases; don't require per-phase compilability; run review
agents automatically; report back only when ALL phases are done and tested.** Live testing via the
devreload + acd-mcp MCP (build → load into AutoCAD → drive commands/grips/UI → screenshot) where possible.

<9-2-phases>
- **P0 Spikes** — ✅ **Spike 1 PASSED (live AutoCAD 2025, 2026-06-25):** a `NorsynContainer` holding an
  `MPolygon` (+ `DBText`) child survives DWG wblock→save→reload — reloads as a real (non-proxy)
  `NorsynObjectsInterop.NorsynContainer` with the MPolygon intact (area preserved), and solid fill +
  transparency + label render through `subWorldDraw` (visually confirmed, two adjacent shared-edge zones).
  **Findings:** (a) the interop ctor binds the new entity to the WORKING database → build zones in the
  active Db, not a side Db; (b) child entity **color/transparency do NOT survive the clone/round-trip** →
  the renderer must (re)apply each zone's appearance from the authoritative ZoneRecord every build (never
  rely on the persisted child's own color). Spikes 2 (ZoneGripOverrule on the container RXClass) and 3
  (CommandEnded yields the drawn polyline) require compiled classes → folded into P8/P9 build+test.
- **P1 New project skeleton** — `NorsynDistrictZones` (.dll, no bundle), `IExtensionApplication`, commands
  modeled on `DimensioneringV2/.../Commands.cs`; shared links (PipeScheduleV2 source, UtilitiesCommon/
  AutoCADCommands projitems, NHS projitems, NorsynObjectsInterop, NTS, CommunityToolkit.Mvvm).
- **P2 Central translator** (§7) — singular mapping + layer parser + FL/SL hook. Unit-tested. (No DimV2.)
- **P3 PipeReader** — Xref `FJV-` enumeration, `<xref>|` prefix strip, PSv2 parse, transform → PipeSegment.
- **P4 Pricing domain** — catalog (seed from NHS), PriceCalculator. Unit-tested.
- **P5 Topology** — DCEL + split + hole + delete-merge + validation. Unit-tested.
- **P6 Container render** — topology → MPolygon+MText children; XData; stable colors; ZoneStore.
- **P7 Calc + labels + dynamic/multi-xref** — Xref selection, live recompute, centroid labels.
- **P8 Reactor** — CommandEnded detection, classification, single-undo, transient red.
- **P9 GripOverrule** — vertex grips, move→rebuild→relabel, adjacency, delete-boundary.
- **P10 WPF price editor** — window + VM + DataGrid theming added to Theme.xaml.
- **P11 Exports** — AutoCAD + GeoJSON.
- **P12 DimV2 export rewire (SEPARATE phase)** — replace the private switch with the shared translator;
  build + test DimV2.AutoCAD only; Domain/Hydraulics untouched.
- **P13 Polish** — NDZRECALC, recompute-on-switch, NDZRENAME, docs, end-to-end test.

</section-9-phases-and-execution>

---

<section-10-remaining-decisions>
- **FL/SL representation** (§8) — parked by user; provisional pricing meanwhile.
- **Numbering** — assumed sequential zone number + stable random color (confirm if different).
</section-10-remaining-decisions>
