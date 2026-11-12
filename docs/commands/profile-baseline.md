# Simple profile helpers

The profile tools use ordinary editable polylines on `0-Hjælpelinje`. There are two commands and no zone setup, predefined-geometry fitting, jig or final acceptance prompt for a complete result.

## PROFILEGUIDE

1. Select a lightweight polyline.
2. An exact in-place copy is created on `0-Hjælpelinje` and selected for editing. The original remains unchanged.
3. Prepare that copy manually, then run `PROFILEBASELINE` on it.

The copy keeps the polyline geometry, including its vertices, bulges, normal, elevation and widths. Its colour, linetype and lineweight are set ByLayer. It is a separate ordinary polyline with no live association to the original. The baseline command requires an open, straight-segment, normal +Z guide progressing consistently in X; an unsuitable copy can be edited before conversion.

## PROFILEBASELINE

1. Select the guide polyline **to replace**.
2. Enter the desired radius in metres.
3. A complete valid baseline is created immediately and the selected guide is erased in the same transaction.

The command asks for a minimum radius only if the desired radius fails and you choose **Minimum** from the failure menu.

The source guide can be on any local unlocked layer. Use `PROFILEGUIDE` first when the original must be kept. A protected source can be read for copying but cannot be consumed directly.

The input and output use WCS X as station and WCS Y as height, with equal horizontal/vertical scale and one drawing unit per metre. The existing guide already includes the desired cover allowance. The command does not add a cover offset.

- Decreasing angle from left to right is a crest; increasing angle is a sag, including corners where both edges rise or both fall.
- Crest circles have the assigned radius and touch the two adjacent guide edges.
- Sag circles have the assigned radius, pass through the guide vertex and have their tangent there at the average incoming/outgoing angle.
- Straight common tangents connect the arcs. For the assigned radius, circle positions follow these rules; the tool does not search alternative slopes or intermediate radii.
- A complete result keeps the exact original endpoints and has straight first and last edges. Every output arc and straight is checked at or below the input over its whole interval, including between input vertices. Numerical equality tolerance is `1e-8` m.
- Input must be open, contain straight segments only, have normal +Z, finite coordinates and constant Z, and progress consistently in X without vertical, duplicate or backtracking edges. Either vertex order is accepted. Collinear vertices are omitted from construction.

## Failed sections and partial creation

The command uses at most two construction passes:

1. Try the desired radius at every corner. If the result is complete, create it immediately.
2. If there are failures, report their stations and offer **Minimum / Anyway / Abort**. Choosing Minimum asks for the fallback radius, assigns it to the failed corners and rebuilds the tangent chain once, reusing the already calculated desired-radius result. A failed tangent connection implicates both adjacent internal corners; original endpoints have no assigned arc radius. Other corners keep the desired radius and their corresponding circle positions. The surrounding tangent points are recalculated for the mixed radii.

There is no search between the two radii and no further cascade of reductions. For example, desired 50 m and minimum 5 m produce only 50 m or 5 m arcs. If the retry succeeds, creation is immediate. A status line reports how many corners were retried. Minimum must be finite, positive and no greater than desired.

The command reports failed stations or station ranges using the Civil 3D profile view and the alignment's station formatting, including station equations. It selects a unique nearby view containing the guide automatically. Otherwise it asks for the appropriate profile view. The selected view is reused if a minimum-radius retry also fails. Drawing X is not presented as a station.

If stations cannot be resolved, the input is kept. Split profile views require separate guides in ordinary profile views for this prototype's station reporting.

After a desired-radius failure, the command offers:

- **Minimum:** enter a positive fallback radius no greater than desired. A successful retry creates immediately. If it still fails, report the remaining stations and offer only Anyway / Abort.
- **Anyway:** write the current valid portions as separate polylines, leave open gaps where construction failed, and consume the selected guide. Choosing Anyway before a minimum retry retains the desired-radius portions.
- **Abort** (default, including Escape): keep the selected guide and add no baseline.

Minimum remains available even when the desired-radius attempt has no valid portions. Choosing Anyway when there is no valid geometry keeps the guide rather than erasing it without a replacement. Escape while entering the minimum also keeps the guide.

Partial creation retains the valid portions of the desired/minimum-radius construction. Missing tangent connections leave gaps over their affected draft-edge interval. Unconstructible corners leave gaps over their affected neighbouring edges or curve footprint. Adjacent surviving arcs can be trimmed at a gap boundary while retaining their radius. No straight or sharp corner is inserted across a gap. A partial piece can end on an arc where the construction was cut; global endpoint conditions apply when that endpoint survives.

The guide is erased only after every output piece has been appended successfully. All outputs, any new helper layer and the source erasure share one transaction. Abort, an invalid source, an inaccessible layer, a failed write or no valid output leaves the source intact. The source geometry is checked again before it is consumed. Normal AutoCAD UNDO restores the consumed guide and removes that command's outputs.

The former zone and geometry-anchor workflows have been removed. Existing zone rectangles in drawings are ignored and left untouched; they can be erased normally.

## Validation

`IntersectUtilities.Geometry.Tests` covers the original crest/sag rules, exact endpoints, reversed guides, same-direction slopes, survey coordinates and continuous cover checks, plus partial construction around one/multiple bad corners, preservation of valid sections between gaps, failure positions in WCS and the case where no geometry fits.

AutoCAD runtime checks remain separate: the successful desired-radius path without a minimum prompt, the Minimum/Anyway/Abort failure menu, native station/view selection and station-equation labels, copying from a protected layer, source consumption, all-or-nothing writes, native editing and UNDO. API signatures for profile-view mapping and `Alignment.GetStationStringWithEquations` were inspected in the installed Civil 3D 2025 managed assemblies.
