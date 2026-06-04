using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork
{
    // One scanned 2D drainage polyline. The two distances are computed once and are
    // depth-independent; the scanner at depth D classifies by thresholding them:
    //   bridge   (green)  — BridgeCost <= D: a run of <= D polylines links this one
    //                       to TWO different pivots (projected 3D pipes).
    //   floating (orange) — else PivotDepth <= D: reachable from ONE pivot within D
    //                       polylines, but no second pivot in budget.
    //   out of range (red) — neither: more than D polylines from any pivot.
    // BridgeCost / PivotDepth are int.MaxValue when no such bridge / no pivot exists.
    internal sealed record LerScannedPolyline(
        ObjectId Id,
        IReadOnlyList<Point3d> Points,
        int BridgeCost,
        int PivotDepth);

    // Deterministic summary of what "Fiks alle broer" will do, computed from an
    // in-memory solve before anything is written — so the operator confirms against
    // real numbers. Polylines = entities that will be rebuilt; Skipped = green
    // bridges that could not be anchored to two pivots and are left untouched; the
    // resulting elevations land in [ZMin, ZMax].
    internal sealed record LerGreenFixPreview(
        int Polylines,
        int Vertices,
        double ZMin,
        double ZMax,
        int Skipped);
}
