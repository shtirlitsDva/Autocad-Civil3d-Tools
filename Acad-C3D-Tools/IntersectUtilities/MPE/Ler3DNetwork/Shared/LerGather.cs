using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.Ler3DNetwork
{
    // The shared 2D/3D split. Reads every Polyline3d once and classifies each by
    // whether any vertex sits at the Z = -99 placeholder. Both Ler3DNetwork
    // commands gather through here and then filter the result for what they need
    // (LerConnectNetwork wants the "Afløbsledning" drainage subset; LerAnalyse
    // wants all 3D pipes plus the drainage 2D subset).
    internal static class LerGather
    {
        // Drainage layers we treat as the working set: the name contains this
        // fragment but is NOT one of our own generated outputs.
        public const string TargetLayerFragment = "Afløbsledning";
        public const string GeneratedLayerPrefix = "LER_N_";

        public static bool IsTargetLayer(string layer) =>
            layer.IndexOf(TargetLayerFragment, StringComparison.OrdinalIgnoreCase) >= 0
            && !layer.StartsWith(GeneratedLayerPrefix, StringComparison.OrdinalIgnoreCase);

        // 3D if any vertex carries a real elevation (is3D), else 2D. A line is 2D
        // only when EVERY vertex sits at a placeholder — Z = -99 (at99) OR Z ≈ 0
        // (atZero). Testing only at99() let flat Z=0 placeholder lines through as
        // false "3D" pipes; is3D() (= !atZero && !at99) is the codebase-wide rule.
        public static LerLineKind Classify(IReadOnlyList<Point3d> points)
        {
            foreach (Point3d p in points)
            {
                if (p.Z.is3D())
                {
                    return LerLineKind.ThreeD;
                }
            }
            return LerLineKind.TwoD;
        }

        // Reads and classifies every Polyline3d in the drawing (any layer), under
        // a document lock. Lines with fewer than two vertices are skipped. The
        // only database read either command performs.
        public static List<LerClassifiedLine> GatherAll(Document owner)
        {
            List<LerClassifiedLine> lines = new();
            using (DocumentLock docLock = owner.LockDocument())
            using (Transaction tx = owner.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    foreach (Polyline3d pl in owner.Database.HashSetOfType<Polyline3d>(tx))
                    {
                        List<Point3d> pts = pl.GetVertices(tx).Select(v => v.Position).ToList();
                        if (pts.Count < 2) continue;
                        lines.Add(new LerClassifiedLine(pl.ObjectId, pts, Classify(pts), pl.Layer));
                    }
                    tx.Commit();
                }
                catch (System.Exception)
                {
                    tx.Abort();
                    throw;
                }
            }
            return lines;
        }
    }
}
