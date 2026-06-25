<feature>
DEv1 profile-projection label slotting — a replacement for STAGGERLABELSALL that places
single-style vertical labels into non-overlapping horizontal slots with non-crossing,
style-driven dragged leaders.
</feature>

<status>
DRAFT for review. No code written yet. Awaiting sign-off on the placement algorithm
(see open-decisions) before implementation.
</status>

<goal>
A new command (STAGGERLABELSALL is kept as-is) that, for every ProfileView in the drawing:
1. Sets every ProfileProjectionLabel's style to `PROFILE PROJECTION DEv1`.
2. Distributes the labels horizontally so their vertical text blocks never overlap.
3. Lets Civil draw each label's 4-point leader from its feature point to its placed text.
The visual target is the supplied reference image: all text vertical, same orientation,
fanned out into a row above the terrain, leaders not crossing.
</goal>

<what-changed-from-the-old-command>
STAGGERLABELSALL split each view into a left half + right half, used two mirrored styles
(PROFILE PROJECTION LEFT/RIGHT v2), and staggered labels VERTICALLY by growing each label's
`DimensionAnchorValue` (leader length) so overlapping labels climbed higher.

The new command is a different problem: one style, no left/right split, and de-overlap is
HORIZONTAL (slot placement) rather than vertical stacking. Vertical position is just
"clear the surface by a fixed amount" (the old surface-clearance rule, kept).
</what-changed-from-the-old-command>

<mechanism-confirmed-live>
Verified against the single existing DEv1 sample label (handle 25142C) in
Længdeprofiler_Kronenstrasse_dev.dwg:

- The label is `Dragged = true`, `DraggedOffset = (6.92, 25.97, 0)`,
  `LabelLocation = (551697.02, 5804149.36)`, `LeaderVisibility = FromLabelStyle`.
- Feature anchor (the crossing point) = `LabelLocation − DraggedOffset` = `(551690.10, 5804123.39)`.
- DEv1 text component: `Angle = 90°` (vertical), small text height ⇒ a thin horizontal footprint.

Programmatic recipe per label:
  1. `label.StyleId = <DEv1>`.
  2. `label.LabelLocation = <target text position>`.
Civil then recomputes `DraggedOffset` and draws the style's dragged 4-point leader.
We do NOT draw leader geometry ourselves — the leader shape is owned by the DEv1 style
(the "modifiable in the UI" part). `DraggedOffset` setter is deprecated; we set `LabelLocation`.

To recover the feature anchor before re-placing: read it from the label BEFORE we move it
(anchor = current LabelLocation − current DraggedOffset), or from a non-dragged label
(anchor = LabelLocation). We capture the anchor first, then set the new LabelLocation.
</mechanism-confirmed-live>

<placement-model>
Per profile view, working in model coordinates:

- `anchorX_i` = feature station X of label i (its crossing point).
- `slotW`     = DEv1 vertical-text horizontal footprint + small padding (a constant; measured
                empirically during implementation, exposed as a tunable).
- `textY`     = surface elevation at the feature station + fixed clearance (the kept
                surface-clearance rule), giving the Y of the placed text. Same rule the old
                command used for the first label.
- Output `placedX_i` = the de-overlapped X for label i's text; we then set
  `LabelLocation = (placedX_i, textY_i)`.

Non-crossing guarantee: if labels keep their station order after placement
(`anchorX` sorted ⇒ `placedX` sorted, with ≥ slotW spacing), the leaders connect two
monotonic sequences and cannot cross.
</placement-model>

<proposed-algorithm>
Order-preserving 1-D placement by isotonic regression (PAVA — "pool adjacent violators").
This is the principled, provably-optimal realisation of the cluster idea you described.

1. Sort labels in a view by `anchorX`.
2. Remove the mandatory spacing: `y_i = anchorX_i − i * slotW`.
3. Run PAVA so the `y_i` become non-decreasing with minimum total movement. Adjacent
   labels that "want" to overlap get pooled into one block placed at the block's mean
   (or median) — that pooled block IS one of your clusters.
4. Restore spacing: `placedX_i = pooledY_i + i * slotW`.
5. Optionally clamp the whole arrangement inside the view's X range; if total width
   `n * slotW` exceeds the view width the overflow is reported, not hidden.

How this maps to the procedure you described:
- "cluster by centre-to-centre distance" → adjacent labels closer than slotW get pooled.
- "start from the mid label, place from centre to edges" → a pooled block is centred on
  the mean of its members and packed symmetrically outward.
- "check space to the next cluster, move it if none" → PAVA pooling cascades leftward and
  merges blocks exactly when there isn't room, and only then.
- "labels relatively far from others" → never pooled; they stay on their feature.

PAVA is O(n) per view, deterministic, and minimises total displacement, so labels stay as
close to their crossings as the no-overlap constraint allows. `GroupByCluster` is still
available if we instead want explicit clusters, but PAVA subsumes it and is cleaner.
</proposed-algorithm>

<open-decisions>
1. ALGORITHM ENGINE: approve PAVA (above), or do you specifically want the hand-rolled
   "largest-cluster-first, expand-and-shove" procedure using `GroupByCluster`? PAVA gives the
   same clusters with a provable minimum-movement result and far less code.
2. POOLING CENTRE: mean (centre of mass — slightly favours dense side) vs median (robust).
   Recommend mean for smooth visual spread.
3. SLOT PADDING: the gap between adjacent text blocks (on top of text width). Start value?
   I will measure text width live; you pick the padding (e.g. one extra text-height).
4. SCOPE: all profile views in the drawing (like STAGGERLABELSALL), or a selected view?
5. COMMAND NAME: proposing `STAGGERLABELSALLDE` / short alias `SGALLDE` (DE = the German
   single-direction DEv1 style). OK, or another name?
</open-decisions>

<verification-plan>
- Pure placement logic (PAVA + slotting) → xUnit test, link-including the source file, no
  AutoCAD refs. Red first.
- AutoCAD-bound behaviour → live test: run the command, then assert via autocad_script_execute
  that in each view (a) every label uses DEv1, (b) adjacent placed X are ≥ slotW apart in
  station order, (c) no two placed text extents overlap. Visual spot-check against the image.
</verification-plan>
