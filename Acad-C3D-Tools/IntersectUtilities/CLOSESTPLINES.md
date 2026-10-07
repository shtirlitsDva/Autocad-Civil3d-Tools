# CLOSESTPLINES

Reload the rebuilt IntersectUtilities plugin through the usual project loader.
Run `CLOSESTPLINES`, then select two different lightweight polylines (PLINE).

The command finds the shortest **3D path-to-path distance** and creates a LINE
between the closest positions on **0-REFERENCELINE**. It prints the distance and
both WCS coordinates, zooms to the result with context, and selects it. The layer
is created if missing and switched on/thawed if it already exists. When the
paths touch within AutoCAD's point tolerance, a POINT marks the contact instead
of a zero-length LINE. The input paths are not modified. Distance and the
minimum zoom height (10) are in drawing units; polyline width is ignored.

Open and closed paths, straight and circular arc segments, clockwise bulges,
arcs over 180 degrees, elevations, tilted normals and repeated vertices are
handled. POLYLINE/Polyline2d, 3DPOLY and splines are not selected by this command.

## Why fixed sampling does not guarantee the minimum

An interior intersection or arc-to-arc minimum can occur between samples on
both paths. Sampling both directions and projecting each sample to the other
path still misses it. If spacing is at most `h` along the path, the sampled
distance can overestimate the true distance by up to `h/2`; it is an
approximation, not an exact construction. (Distance to a set is 1-Lipschitz,
and a point is at most `h/2` along the path from its nearest sample.)

## Calculation

Every bounded segment pair is considered, with bounding boxes pruning pairs
that cannot improve the current answer. AutoCAD's GE closest-point routine
provides a candidate. Line/line pairs are direct; arc pairs are refined using
a priority queue and rigorous chord/sagitta distance bounds, because live
tests showed the native arc/arc routine can return a local minimum.

For an arc of radius `r` and angular span `a`, the maximum chord deviation is
`2*r*sin(a/4)^2`. Subtracting both deviations from the chord-to-chord distance
gives a lower bound. Evaluated points on the actual arcs give an upper bound.
The search stops when no remaining interval can improve the best distance by
more than the calculation tolerance. Full-circle lower bounds also accelerate
concentric arcs. Single/repeated vertices use point-to-curve calculations.

The distance accuracy is AutoCAD's `Tolerance.Global.EqualPoint`, enlarged
to `1.5e-14 * coordinateScale` when necessary for large WCS coordinates.
This is a global minimum **within floating-point/geometric tolerance**, not
symbolically exact arithmetic. Worst-case segment-pair work is quadratic;
arc refinement depends on the geometry and tolerance.

## Validation (AutoCAD/Civil 3D 2025, 2026-10-06)

22 targeted live geometry cases passed: interior crossings, finite endpoints,
line/arc and arc/arc minima, tangencies, concentric/coincident arcs, major and
clockwise arcs, closing/mixed segments, repeated/single vertices, skew paths,
different elevations, tilted OCS normals and large survey coordinates.

120 deterministic randomized polyline pairs passed distance symmetry,
point-on-path checks and comparison to a dense point-to-curve upper bound.
The randomized suite exposed five missed minima in the native-only version;
the refinement fixes all five. These tests create only transient objects,
not entities in the user's drawing. The regression script is in
`IntersectUtilities.Geometry.Tests/PolylineClosestPointsRegression.csx`.

## API references

- [Curve3d.GetClosestPointTo](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_Geometry_Curve3d_GetClosestPointTo_Curve3d.html)
- [Polyline.GetArcSegmentAt (WCS geometry)](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_DatabaseServices_Polyline_GetArcSegmentAt_int.html)
- [PointOnCurve3d.Point](https://help.autodesk.com/cloudhelp/2026/ENU/OARX-ManagedRefGuide/files/OARX-ManagedRefGuide-Autodesk_AutoCAD_Geometry_PointOnCurve3d_Point.html)
