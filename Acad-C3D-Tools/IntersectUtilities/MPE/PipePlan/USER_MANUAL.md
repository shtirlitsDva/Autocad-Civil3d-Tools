# PipePlan User Manual

PipePlan is a guided, constrained way to draft district-heating ("fjernvarme") pipes in plan view inside AutoCAD / Civil 3D 2025. Instead of drawing a raw polyline and hoping the bend radii work, you give PipePlan a sequence of control points and it produces a polyline whose arcs already match the project bending rules for the chosen dimension. The result is a polyline with attached metadata, so it can later be continued or edited without losing what PipePlan knows about it.

This document is for colleagues who will *use* the tool. The internal solver, snapping logic, and metadata format are out of scope — see `README.md` and the source if you need that.

---

## 1. Before you start

Three things must be in place before any PipePlan command will do anything useful:

1. **The `IntersectUtilities` plugin must be loaded** in AutoCAD 2025. This is the standard plugin we use; if you can run `OPDATER`, `LERIMPORT`, etc., it is loaded.
2. **NSPalette must be open and a dimension must be activated.** PipePlan reads the *active layer* to determine which pipe system, pipe type, and DN you are drawing. If the active layer is not a recognised FJV layer (e.g. you have `0` or `Defpoints` active), PPDRAW will refuse to start.
3. **A bending radius must exist for the chosen dimension.** PipePlan reads it from the project's radius store (per drawing override) or falls back to the built-in API value. If both are missing, the command tells you to set one in `PPSETTINGS`.

### Supported pipe types

PipePlan only accepts these system / type combinations:

| System  | Type   | Notes                                          |
|---------|--------|------------------------------------------------|
| Stål    | Twin   | Twin pipes only — single Stål Frem/Retur is **not** supported |
| AluPex  | Twin   |                                                |
| AluPex  | Frem   |                                                |
| AluPex  | Retur  |                                                |

If your active layer is on a different combination (e.g. `FJV-FREM-DN200`), PipePlan will reject it with a message telling you to switch in NSPalette.

---

## 2. The four commands at a glance

| Command      | What it does                                                                                  |
|--------------|-----------------------------------------------------------------------------------------------|
| `PPDRAW`     | Draw a new pipe, or continue from the end of an existing PipePlan pipe.                       |
| `PPCONVERT`  | Turn an existing hand-drawn FJV polyline into a PipePlan-managed pipe.                        |
| `PPEDIT`     | Move corners or segments of a PipePlan pipe, or change the bend radius at a specific corner. |
| `PPSETTINGS` | Open the settings palette: per-DN bending radius table + straight-snap tolerance.            |

The next sections walk through each one.

---

## 3. PPSETTINGS — set this up first

Run `PPSETTINGS` to open the PipePlan palette. You will see:

- **Status** — short message about the most recent action. Colour-coded: grey = info, green = OK, blue = snap active, orange = warning, red = error.
- **Straight snap tolerance** — a number, default `5`. This is the perpendicular tolerance (in drawing units) used when you hold `Ctrl` while drawing to snap the cursor onto the continuation of the previous straight segment.
- **ProjekteringsRadius pr. dimension** — a row per supported System × Type × DN. For each row you see:
  - `Dimension` label, e.g. `Stål Twin DN65`.
  - `Radius` — the editable value used as the default bend radius for that dimension.
  - `Kilde` — where the value comes from:
    - `api` (grey) — the value is the built-in default from the project's pipe schedule.
    - `override` (blue) — the value has been edited and saved for *this drawing*.
    - `missing` (orange) — there is no value; you must enter one before drawing in this dimension.
  - `Reset` — clears the override for the row and falls back to the API default.

**Save** writes overrides into the active drawing (so they travel with the DWG). **Reload** discards unsaved edits in the table and re-reads from the drawing.

Tip: the dimension cell labels show *exactly* which combinations are supported. If a combination is not listed, PipePlan does not handle it.

---

## 4. PPDRAW — draw a new pipe

1. In NSPalette, pick the dimension you want to draw (e.g. `FJV-TWIN-DN65`). The active layer changes accordingly.
2. Run `PPDRAW`.
3. AutoCAD asks `Start [New/Continue] <New>:`
   - Press `Enter` (or type `N`) to start from scratch.
   - Type `C` to continue from the endpoint of an existing PipePlan pipe — see section 4.4.
4. Pick the first point.
5. Pick the next point. PipePlan shows a live preview of the polyline including the arc that will be inserted at the corner. The preview colour tells you whether the geometry is feasible:
   - **Green** — the chosen radius fits at every corner.
   - **Red** — at least one corner cannot accommodate the radius (segments too short). Move the point or reduce the radius. You **cannot** commit a red preview.
   - **Blue** — `Ctrl` is held and the cursor has snapped onto the straight continuation of the previous segment (see section 4.2).
6. Keep picking points. Press `Enter` to finish and bake.

You need at least two points to bake. PPDRAW writes the polyline on the FJV layer matching the active dimension (it creates the layer if it does not exist).

### 4.1 Keywords during drawing

While PPDRAW is asking for the next point you can type:

- `R` (**Radius**) — temporarily override the bend radius for *the remaining picks in this draft*. You will be prompted repeatedly until you press `Enter` with no value. Useful when one corner needs a tighter or wider arc than the dimension's default.
- `D` (**Default**) — clear the manual override and go back to using the per-DN radius from `PPSETTINGS`.
- `T` (**Tangent**) — toggle tangent-snap mode (see section 4.3). The prompt shows `Tangent (on)` or `Tangent (off)`.

### 4.2 Ctrl — straight snap

Hold `Ctrl` while moving the cursor. If the cursor is within the *straight snap tolerance* (set in `PPSETTINGS`, default 5) of the line extending from the previous segment, the preview will snap onto that line and turn blue. Release `Ctrl` to free the cursor again. This is the easiest way to extend a segment in a straight line without typing relative coordinates.

### 4.3 Tangent mode — meeting another PipePlan pipe

When tangent mode is on (`T` to toggle), hovering near the endpoint of an existing PipePlan pipe locks the next point so that the new bend ends tangent to that pipe. PipePlan inserts the corner automatically and may absorb the previous draft segment if needed. If the geometry cannot be made tangent (segments too short, the next pipe points the wrong way, etc.) you get a clear rejection message and can move the cursor or turn tangent off.

Tangent mode also stays on across picks, so you can finish a leg with multiple tangent joins in a row.

### 4.4 Continue — extend an existing PipePlan pipe

Type `C` at the `Start [New/Continue]` prompt and pick an existing PipePlan polyline near the end you want to extend from (PipePlan picks the closer endpoint automatically). The dimension, radius, and metadata of the existing pipe are loaded — you do **not** need to set the active layer manually; the original layer wins. Then continue picking points as in section 4.

When you press `Enter` to bake, the existing polyline is mutated in place: its `Handle` and any third-party data attached to it survive the continue.

---

## 5. PPCONVERT — turn an existing polyline into a PipePlan object

Use this when you have a polyline that was drawn before PipePlan existed, or by hand, and you want PPEDIT / PPDRAW Continue to work on it.

1. Run `PPCONVERT`.
2. Pick a polyline. It must be:
   - On a recognised FJV layer for one of the supported System × Type combinations.
   - **Open** (closed polylines are rejected).
3. PipePlan reverse-engineers the control points from the existing geometry. Each existing arc is read as a corner with its own radius. Each **sharp corner** (an angle with no arc) is flagged.
4. If the polyline has any sharp corners, PipePlan:
   - Draws small marker circles at each sharp corner so you can see what will change.
   - Shows a preview with the proposed fillet radius (the per-DN default from `PPSETTINGS`).
   - Prompts: `Enter to convert at radius <R>, input a different radius, or Esc to cancel`. Type a new radius to preview it, type `Default` to restore the per-DN value, or press `Enter` to accept.
5. PipePlan rewrites the polyline in place. The polyline keeps its `Handle` and any pre-existing attached data; layer, colour, linetype, elevation are unchanged.

### When PPCONVERT will refuse

- Polyline is closed.
- Polyline is on a layer that is not a recognised FJV combination, or on an unsupported System × Type.
- No bending radius is defined for the dimension (set one in `PPSETTINGS`).
- A bend is too sharp for its radius, or two bends are so close together that even a merged arc-to-arc curve cannot fit within the available segment lengths. (Arc-to-arc bends themselves are fine — PPCONVERT reads each arc as a corner with its own radius, whether it is flanked by straights or by another arc.)

---

## 6. PPEDIT — move corners, move segments, change radii

Run `PPEDIT` and pick a PipePlan polyline. The polyline must have PipePlan metadata; if it does not, the command tells you to run `PPCONVERT` first.

PipePlan displays two kinds of grip-style handles on the polyline:

- **Vertex handles** — one at each control point. Picking one lets you drag the corner. For interior vertices you can also change the bend radius.
- **Segment handles** — one at each segment midpoint. Picking one lets you translate the whole segment (both endpoints move together).

### Editing a vertex

1. Click the vertex handle.
2. Drag the cursor. The preview shows the live result and colours feasibility the same way as PPDRAW (green / red).
3. Click to commit. If the candidate is red, the move is rejected and you can pick again.
4. Press `Enter` to back out without committing and pick another handle, or press `Enter` again at the handle prompt to finish.

### Changing the bend radius at a vertex

When dragging a vertex you can type `R` (Radius) instead of clicking:

1. PipePlan asks for a new radius. Enter a number, or type `Default` to use the per-DN radius from `PPSETTINGS` (only available if one is defined).
2. The preview updates with the new radius.
3. Press `Enter` to lock the radius. The status message tells you the radius is locked.
4. Now finish the move: drag to a new position and click, or press `Enter` to commit at the *current* drag position. The new radius is applied along with the move.

Endpoint vertices have no bend radius — the `Radius` option is rejected if you try it there.

### Editing a segment

1. Click the segment handle (midpoint).
2. Drag — both endpoints of the segment translate together. The preview shows the resulting bends at the segment's two corners.
3. Click to commit, or `Enter` to cancel the move.

### When PPEDIT will reject a move

The same constraints as PPDRAW apply: every bend must fit at its assigned radius, and segments must be long enough on either side of every corner (or, where two bends crowd together, the merged arc-to-arc curve must fit). If the move violates any of this, the preview turns red and the click is ignored; the status line tells you what went wrong.

---

## 7. Drawing-wide settings worth knowing

- **Polyline width** at bake is the result of looking up the pipe outer diameter (`KOd`) for the active layer with **series S2** and dividing by 1000 (so units are metres). To render at S1 or S3 instead, change the series in NSPalette and run `Polylinjer bredde opdater` — it does the same lookup with the new series.
- **Metadata** attached to every PipePlan polyline:
  - `pipeTag` XData with system, type, DN — used by `OPDATER` and other layer-aware tooling.
  - A `pipeGeometryData` Xrecord with control points, per-vertex bend radii, and the straight-snap tolerance setting at bake time. This is what `PPEDIT` and `PPDRAW Continue` read.
- **Per-drawing state** — PipePlan's draft, manual radius override, and palette state are kept per drawing. Switching documents in the AutoCAD MDI rebinds the palette to the new document's state. Closing a drawing discards its draft.

---

## 8. Status messages — quick reference

The palette status line and the AutoCAD command line both show short messages. The colour in the palette tells you the kind:

- **Grey (Info)** — informational; nothing wrong.
- **Green (OK)** — the last action succeeded; the current preview is feasible.
- **Blue (Snap)** — `Ctrl` straight-snap or tangent-snap is active.
- **Orange (Warning)** — the command refused to start, usually because of a setup problem you can fix (no active FJV layer, missing radius, wrong layer combination).
- **Red (Error)** — the geometry is infeasible (a bend won't fit) or the radius is invalid. Read the message — it points at *why*.

---

## 9. Common errors and what they mean

| Message                                                                | What to do                                                                                                                                |
|------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| `Indlæs NSPalette først.`                                              | NSPalette is not loaded. Open it and activate a size.                                                                                     |
| `Intet aktivt FJV-lag …`                                               | The active layer is not a recognised FJV layer. Pick a dimension in NSPalette.                                                            |
| `Enkelt-rør understøttes ikke. Brug Stål Twin eller ALUPEX.`           | You activated a single-pipe Stål dimension. Switch to `Stål Twin` or any `AluPex` dimension.                                              |
| `Laget '<x>' understøttes ikke. Skift til Stål Twin eller ALUPEX …`    | The System × Type combination is not in the accepted list (see section 1).                                                                 |
| `Ingen bukkeradius for <System> <Type> DN<dn>. Sæt den i PPSETTINGS.`  | Open `PPSETTINGS` and enter a radius for that row, then `Save`.                                                                            |
| `Polylinjen har ingen PipePlan-data. Kør PPCONVERT først.`             | The polyline pre-dates PipePlan. Run `PPCONVERT` on it.                                                                                   |
| `Polylinjen er fra en ældre PipePlan-version. Kør PPCONVERT først.`    | Metadata is missing or stale. Re-converting will regenerate it.                                                                            |
| `Lukkede polylinjer understøttes ikke.`                                | Open the polyline (remove the close) and retry.                                                                                            |
| `Punkt afvist: …` / `Redigering afvist: …`                            | The candidate move violates a constraint (bend won't fit, segment too short, etc.). The text after the colon names which.                  |
| `Tangent-reference er slettet/flyttet/ikke længere en PipePlan-polylinje.` | Tangent mode had cached a snap whose target has changed since. Tangent is dropped automatically; pick again.                          |
| `Næste segment for kort: kræver X, har Y.`                             | Tangent fillet needs more length on the next pipe. Move the next pipe further away, or reduce the radius with `R`.                         |
| `Forrige segment bukker for tidligt. Reducér radius.`                  | The tangent solution would have to walk back through a previous corner. Reduce the radius or restructure the draft.                        |

---

## 10. Limitations — read this once

These are the things PipePlan does **not** currently handle. Knowing them up front saves time:

1. **No single-pipe steel.** Only `Stål Twin`, `AluPex Twin`, `AluPex Frem`, `AluPex Retur` are accepted. Single Stål `Frem` / `Retur` will be rejected.
2. **No closed polylines.** PPCONVERT and PPEDIT both refuse closed polylines.
3. **Editing outside PipePlan can desync metadata.** If you grip-edit, `STRETCH`, or delete a vertex with a non-PipePlan command, the polyline's geometry will change but the stored control points will not. Subsequent `PPEDIT` or `PPDRAW Continue` will detect the mismatch and ask you to run `PPCONVERT` again. **Rule of thumb: edit PipePlan pipes with PipePlan commands.**
4. **To refresh metadata after an outside edit, re-run `PPCONVERT`.** It rebuilds the control points and radii from the current geometry — including arc-to-arc bends, which are now recovered directly.
5. **Bending radius comes from the dimension, not from each polyline.** A polyline can have *per-corner* radii (set by `PPEDIT`'s Radius keyword or recovered by `PPCONVERT`), but newly placed corners during PPDRAW use the dimension's default (or the in-draft manual override). Adjust the default in `PPSETTINGS` to change project-wide defaults.
6. **Width is set at bake from series S2.** Switching to S1 or S3 requires running `Polylinjer bredde opdater` after changing the NSPalette series — PipePlan does not track series.
7. **One pipe at a time.** PipePlan commands operate on a single polyline. Bulk conversion of many polylines must be done one by one.
8. **No undo of partial draft state.** Pressing `Esc` during PPDRAW discards the entire draft. There is no per-point undo while drafting — pick carefully, or finish and use `PPEDIT`.

---

## 11. Typical session — putting it together

A complete sample workflow:

1. **Open the drawing.** Make sure NSPalette is loaded.
2. **Run `PPSETTINGS`.** Verify there is a `Radius` value (api or override) for every dimension you intend to draw today. Set overrides where the project requires something other than the API default. Save.
3. **Activate the first dimension in NSPalette**, e.g. `FJV-TWIN-DN65`. The active layer becomes the corresponding FJV layer.
4. **Run `PPDRAW`**, press `Enter` for New, pick points along the route. Hold `Ctrl` for straight extensions. Press `Enter` to bake.
5. **Continue from the bake point in another dimension** if the route changes size: activate the new dimension in NSPalette, run `PPDRAW`, choose `Continue`, pick near the end of the pipe you just baked, then keep drawing. The new section is drawn on the new dimension's layer.
6. **Edit later with `PPEDIT`.** Move corners, change radii, slide segments.
7. **Inherit a legacy polyline with `PPCONVERT`** if a colleague hand-drew a section without PipePlan.

---

## 12. Quick reference

| Action                                          | How                                                          |
|-------------------------------------------------|--------------------------------------------------------------|
| Start drawing a new pipe                        | `PPDRAW`, Enter, pick points, Enter                          |
| Continue an existing PipePlan pipe              | `PPDRAW`, `C`, pick near endpoint, pick points, Enter        |
| Change bend radius for this draft only          | During PPDRAW, type `R`, enter radius, Enter                 |
| Restore the dimension's default radius          | During PPDRAW, type `D`                                      |
| Snap to the straight continuation of last seg   | Hold `Ctrl` while moving                                     |
| Tangent-join to another PipePlan pipe           | During PPDRAW, type `T`, hover near the other pipe's endpoint|
| Convert an existing FJV polyline                | `PPCONVERT`, pick polyline, accept/edit radius, Enter        |
| Move a corner                                   | `PPEDIT`, pick polyline, click vertex handle, drag, click    |
| Change one corner's bend radius                 | `PPEDIT`, pick vertex, type `R`, enter radius, Enter, drag/Enter |
| Move a whole segment                            | `PPEDIT`, click the segment handle (midpoint), drag, click   |
| Set per-DN radii or snap tolerance              | `PPSETTINGS`, edit, `Save`                                   |
