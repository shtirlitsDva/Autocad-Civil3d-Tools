# LER2ATTACHSTIK

Attach selected LER branches to the interiors of existing 3D main segments,
then set their slope. This command is part of IntersectUtilities.

1. Preselect branch 3D polylines and run `LER2ATTACHSTIK`, or select them when prompted.
2. Enter the slope in per-mille (default **20**).
3. Enter the maximum attachment distance in drawing units (default **1**).
   For a drawing in metres, 1 means 1 metre. Enter reuses the last value at
   either prompt; values are remembered for the current plugin session.

Select branches only. Unselected simple 3D polylines in the current space are
the candidate mains, regardless of layer or visibility. Xref/block contents
are not searched. The command reports attached/skipped totals and the handle
and reason for each skipped object. One `UNDO` reverses a command invocation.

## Connection rules

- With exactly one real-elevation endpoint, use that endpoint.
- With two unknown endpoints, test both terminal segments and choose the
  valid connection requiring the least endpoint movement in XY.
- With two real-elevation endpoints, skip the branch.
- The LER placeholder convention matches `UtilsCommon.is3D()`: Z < -98
  (including -99), or -0.0001 < Z < 0.0001, is unknown.
- A target segment must have real elevations at **both** vertices. Unknown-to-real
  and unknown-to-unknown segments are excluded. Valid edges of a partially unknown
  main remain candidates. Closed mains include the last-to-first segment.
- Extend or trim the branch's terminal segment along its existing XY line.
  Extension moves the endpoint outward; trimming moves it back toward the next
  distinct branch vertex. Trimming stops before that vertex, so it cannot collapse
  the terminal segment or cut through a bend. No sideways movement is introduced.
  The main is used only within its actual segment. Choose the intersection
  requiring the least endpoint movement within the maximum attachment distance.
  This limit applies to both extension and trimming and is measured in XY.
- An endpoint already on the interior of a main edge is supported, including
  a collinear endpoint. Other parallel/collinear cases have no unique intersection
  and are skipped. Duplicate terminal vertices use the first distinct XY position.
- Equally near candidates with different connection ends or different elevations
  are skipped. Two main edges sharing the same connection XYZ count as one hit.

## Elevations and data

The existing endpoint moves to the intersection. Any coincident terminal vertices
move with it during trimming so the overshooting end is removed. Its Z is linearly interpolated
between the target segment's two vertices, using the XY fraction along the edge.
All other branch vertices retain their XY and receive:

`Z = connection Z + slope / 1000 * cumulative horizontal branch length`

Positive slope rises away from the main; negative slope descends away from it.
Zero makes a level branch. A 20-per-mille branch gains 0.20 m over 10 m of
horizontal path. Existing branch elevations are overwritten, including any
known interior elevations. No vertex is added to either the branch or main.

The command edits the original branch vertices in one transaction, preserving
ObjectIds, handles, layers, property sets, XData and vertex order. Main geometry
is unchanged. Selected branches are excluded from all target searches, so
results do not depend on selection order. Closed/spline branches, locked-layer
branches, non-3D polylines, degenerate geometry, ambiguous matches and missing
intersections are skipped. An unexpected AutoCAD write failure rolls back the
whole transaction.

The command uses geometry and elevation only; it does not infer pipe ownership,
diameter, invert/crown offsets or the intended network at a crossing.

## Verification

`StikAttachmentGeometryTests.cs` is linked into
`IntersectUtilities.Geometry.Tests`. It covers projection versus perpendicular
snapping, extension/trimming, interpolation, slope signs, bends, reversed storage, unknown endpoints,
invalid target elevations, distance limits, shared vertices, ambiguous crossings,
degenerate geometry, and 300 deterministic rotated cases at survey coordinates.
