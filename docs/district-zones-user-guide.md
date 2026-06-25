# Norsyn District Zones (NDZ) — User Guide

<status>
How an end user works with the NorsynDistrictZones plugin. Companion to the plan of
record (`docs/district-zones-plan.md`). Reflects the shipped command set.
</status>

<at-a-glance>
You attach a DimensioneringV2 pipe export as an Xref, draw polylines to carve the
drawing into **zones**, and each zone shows its **name + live pipe price** in the
middle. Drawing inside a zone subdivides it; dragging boundary grips reshapes
neighbours together; deleting a boundary merges zones. Prices come from a named,
editable catalog and are recomputed live from the Xref — never frozen into the file.
</at-a-glance>

---

<the-commands>
| Command | What it does |
|---|---|
| `NDZZONE` | Turn a selected CLOSED polyline into a zone. |
| `NDZPRICES` | Open the price-catalog editor (named catalogs, per-metre + per-fitting). |
| `NDZRENAME` | Rename a zone. |
| `NDZRECALC` | Re-price + re-render every zone (after the Xref changes). |
| `NDZTEXTSIZE` | Set the zone-label text height, remembered for all drawings (0 = auto). |
| `NDZEXPORTACAD` | Export zones as plain AutoCAD geometry on layer `NDZ-EXPORT`. |
| `NDZEXPORTGEOJSON` | Export zones to a `.geojson` file. |
| `NDZSELFTEST` | Sanity-check the pricing domain (no drawing data needed). |

The load banner lists these (with one-line descriptions) every time the plugin loads.
</the-commands>

---

<step-0-load-the-plugin>
`NETLOAD` the `NorsynDistrictZones.dll` (or it loads via your demand-load setup). On
load you see the banner and **"auto-zone reactor active"** — the app is now watching
for polylines you draw, for the whole session. There is no start/stop mode to toggle.
</step-0-load-the-plugin>

<step-1-bring-in-the-pipes>
Attach the **DimensioneringV2 drafter export** ("Fjernvarme DIM.dwg") as an **Xref**.
The app finds pipes by their layer names (`FJV-<Type>-<System><DN>`) inside the
attached Xref(s) and reads each pipe's `NORSYN_NHS_PIPE` identity (pipe type / FL-SL /
DN) that the export stamps.

- Pipes come from **all attached Xrefs** in the drawing — there is no Xref-picker
  command, so attach only the export you want priced.
- You never edit the pipes; the app only reads them.
</step-1-bring-in-the-pipes>

<step-2-prices-are-ready-by-default>
You do **not** have to set up prices first. The active catalog is **auto-seeded from
the Norsyn Hydraulic defaults** (every pipe type × DN, with a per-metre and a
per-fitting price) the first time a zone is priced, and stored in the drawing.

Run `NDZPRICES` only when you want to change prices. The editor lets you create / copy
/ rename / delete named catalogs, import / export a catalog, pick the **active** one,
and edit the two price columns. On save, catalogs persist in the drawing and **every
zone recomputes** against the active catalog.
</step-2-prices-are-ready-by-default>

<step-3-create-a-zone>
Two ways, same result (a transparent colored zone with a `#number` and the price in
the centre):

- **Automatic (the main way):** **draw a closed polyline** around the pipes. When the
  draw command ends, the reactor turns it into a zone and consumes the polyline.
- **Manual:** run `NDZZONE` and pick an existing closed polyline.

The zone gets a stable random color and a sequential number. Its price = the pipes
clipped to its area × the active catalog.
</step-3-create-a-zone>

<step-4-subdivide-by-drawing-inside>
Once a zone exists, **drawing another polyline interacts with it automatically** at
command-end:

- **Open polyline, both ends on the zone boundary → SPLIT** the zone into two adjacent
  zones (the original number stays on one, the other gets a new number).
- **Closed polyline fully inside a zone → cuts a HOLE** and creates a sub-zone.
- **Invalid** (an end not on a boundary, or the line doesn't divide the zone) → a
  **transient red marker** appears, with a message explaining why, and nothing changes.

Endpoints are snapped to a 1&nbsp;mm grid internally, so an OSnap'd cut splits reliably —
sub-millimetre differences are ignored (they don't matter for pricing). Each successful
operation is a single Undo step, and affected zones re-price.
</step-4-subdivide-by-drawing-inside>

<step-5-reshape-and-merge-with-grips>
Select a zone and use its **grips** (one per boundary vertex):

- **Drag a vertex shared with a neighbour → both zones adapt together**, no gap opens
  between them (shared-edge editing).
- **Delete a boundary between two zones → they merge** into one (one identity wins, the
  price recomputes).

Boundaries are snappable (OSnap works).
</step-5-reshape-and-merge-with-grips>

<step-6-name-recompute-and-text-size>
- `NDZRENAME` → pick a zone, type a name. The name shows **above** the price and is
  saved with the zone.
- `NDZRECALC` → re-reads the Xref and re-prices / re-renders **all** zones. Run it after
  you re-export or reload the pipe Xref, since pricing is read live and not stored.
- `NDZTEXTSIZE` → set the label text height. The value is **remembered globally** (all
  drawings); enter `0` to go back to the per-zone automatic size. The current drawing
  re-renders immediately, and the AutoCAD export uses the same size.
</step-6-name-recompute-and-text-size>

<step-7-export>
- `NDZEXPORTACAD` → writes each zone as real polylines + name/price text on layer
  `NDZ-EXPORT` (honours the `NDZTEXTSIZE` height), so you get a standalone annotated
  drawing independent of the live zones.
- `NDZEXPORTGEOJSON` → writes a `<drawing>_zones.geojson` next to the DWG, with each
  zone's polygon + number / name / price / area as properties.
</step-7-export>

---

<what-persists-and-what-does-not>
- **Persisted in the drawing:** each zone's identity + geometry (so zones survive
  save/reload), and the named price catalogs.
- **Persisted per user (all drawings):** the label text size (`NDZTEXTSIZE`).
- **Never persisted (always recomputed):** the prices themselves. The number you see is
  always derived from the *current* Xref pipes × the *active* catalog — so swapping the
  Xref or editing prices and running `NDZRECALC` always reflects reality.
</what-persists-and-what-does-not>

<pricing-and-the-fl-sl-identity>
Correct pricing needs each pipe's FL/SL identity (`NORSYN_NHS_PIPE` XData), which the
current DimensioneringV2 export stamps. Service lines (stik) then get the per-fitting
surcharge; distribution lines do not.

If a zone contains pipes from an **older export that lacks this identity**, the price
label deliberately shows **`re-export DIM (no FL/SL data)`** instead of a number — the
app fails loud rather than show a possibly-wrong total. The zone geometry still works
normally; re-export the DIM drawing from DimensioneringV2 and reload the Xref to price it.
</pricing-and-the-fl-sl-identity>

<typical-session-in-one-line>
Load → attach pipe Xref → draw closed polylines to make/split zones → grip-edit
boundaries → `NDZRENAME` → `NDZTEXTSIZE` to taste → `NDZRECALC` if the Xref changed →
`NDZEXPORTACAD` / `NDZEXPORTGEOJSON`. (`NDZPRICES` only if you need to change prices.)
</typical-session-in-one-line>
