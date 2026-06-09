using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.Ler3DNetwork
{
    // Whether a gathered drainage polyline carries real elevations (3D) or is a
    // flat placeholder whose vertices sit at Z = -99 (2D, needs lifting).
    // Shared by every Ler3DNetwork command.
    internal enum LerLineKind
    {
        ThreeD,
        TwoD
    }

    // Status severity used to colour a palette status line.
    internal enum LerStatusKind
    {
        Info,
        Ok,
        Warning,
        Error
    }

    // A gathered Polyline3d, snapshotted at Gather time. Points are the real
    // vertex positions (3D lines carry real Z; 2D lines carry the -99 placeholder
    // Z, but their XY is valid plan geometry). Layer is kept so callers can filter
    // by layer (e.g. the "Afløbsledning" drainage subset) after the split.
    internal sealed record LerClassifiedLine(
        ObjectId Id,
        IReadOnlyList<Point3d> Points,
        LerLineKind Kind,
        string Layer);
}
