using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using Dreambuild.AutoCAD;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const double CombineRadiusPairTolerance = 2.0;

        /// <command>COMBINERADIUSPAIRS, KOMBINERRADIUSPAR, CRP, KRP</command>
        /// <summary>
        /// Kombinerer RadialDimension-par fra LABELARCS til én radiusdimension i midten.
        /// Pickfirst-selection behandles automatisk. Uden pickfirst kan brugeren vælge
        /// automatisk gruppering eller manuelle par.
        /// </summary>
        /// <category>Fjernvarme Fremtidig</category>
        [CommandMethod("COMBINERADIUSPAIRS", CommandFlags.UsePickSet)]
        [CommandMethod("KOMBINERRADIUSPAR", CommandFlags.UsePickSet)]
        [CommandMethod("CRP", CommandFlags.UsePickSet)]
        [CommandMethod("KRP", CommandFlags.UsePickSet)]
        public void combineradiuspairs()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptSelectionResult impliedSelection = ed.SelectImplied();
            if (impliedSelection.Status == PromptStatus.OK && impliedSelection.Value.Count > 0)
            {
                ed.SetImpliedSelection(Array.Empty<Oid>());
                CombineRadiusPairsAutomatically(db, ed, impliedSelection.Value.GetObjectIds());
                return;
            }

            PromptKeywordOptions modeOptions = new(
                "\nKombiner radiusdimensioner [Auto/Par] <Auto>: ")
            {
                AllowNone = true
            };
            modeOptions.Keywords.Add("Auto");
            modeOptions.Keywords.Add("Par");
            modeOptions.Keywords.Default = "Auto";

            PromptResult modeResult = ed.GetKeywords(modeOptions);
            if (modeResult.Status == PromptStatus.Cancel)
                return;

            string mode = string.IsNullOrWhiteSpace(modeResult.StringResult)
                ? "Auto"
                : modeResult.StringResult;

            if (mode.Equals("Par", StringComparison.OrdinalIgnoreCase))
            {
                CombineRadiusPairsManually(db, ed);
                return;
            }

            PromptSelectionOptions selectionOptions = new()
            {
                MessageForAdding = "\nVælg radiusdimensioner fra LABELARCS: "
            };

            PromptSelectionResult selectionResult = ed.GetSelection(selectionOptions);
            if (selectionResult.Status != PromptStatus.OK)
                return;

            CombineRadiusPairsAutomatically(db, ed, selectionResult.Value.GetObjectIds());
        }

        private static void CombineRadiusPairsAutomatically(
            Database db,
            Editor ed,
            IReadOnlyCollection<Oid> selectedIds)
        {
            using Transaction tx = db.TransactionManager.StartTransaction();

            try
            {
                List<RadiusDimensionInfo> dims = ReadRadiusDimensionInfos(tx, selectedIds, out int skipped);
                if (skipped > 0)
                    ed.WriteMessage($"\nCRP_SKIPPED_NON_RADIAL: Sprang {skipped} objekt(er) over, da de ikke er RadialDimension.");

                if (dims.Count < 2)
                {
                    ed.WriteMessage("\nCRP_TOO_FEW: Vælg mindst to radiusdimensioner.");
                    tx.Abort();
                    return;
                }

                if (dims.Count % 2 != 0)
                {
                    ed.WriteMessage($"\nCRP_ODD_SELECTION: Antallet af radiusdimensioner skal være lige. Valgt: {dims.Count}.");
                    tx.Abort();
                    return;
                }

                List<RadiusDimensionPair> pairs = BuildAutomaticPairs(dims, out List<string> errors);
                foreach (string error in errors)
                    ed.WriteMessage($"\n{error}");

                int combined = 0;
                foreach (RadiusDimensionPair pair in pairs)
                {
                    CombinePair(tx, pair.First, pair.Second);
                    combined++;
                }

                if (combined == 0)
                {
                    tx.Abort();
                    ed.WriteMessage("\nCRP_NONE_COMBINED: Ingen par blev kombineret.");
                    return;
                }

                tx.Commit();
                ed.WriteMessage($"\nCRP_OK: Kombinerede {combined} radiuspar.");
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                ed.WriteMessage($"\nCRP_ERROR: {ex.Message}");
            }
        }

        private static void CombineRadiusPairsManually(Database db, Editor ed)
        {
            int combined = 0;

            while (true)
            {
                PromptEntityResult firstResult = PromptForRadiusDimension(
                    ed,
                    "\nVælg første radiusdimension i parret (Enter afslutter): ",
                    allowNone: true);
                if (firstResult.Status != PromptStatus.OK)
                    break;

                PromptEntityResult secondResult = PromptForRadiusDimension(
                    ed,
                    "\nVælg anden radiusdimension i parret: ",
                    allowNone: false);
                if (secondResult.Status != PromptStatus.OK)
                    break;

                if (firstResult.ObjectId == secondResult.ObjectId)
                {
                    ed.WriteMessage("\nCRP_SAME_OBJECT: Vælg to forskellige radiusdimensioner.");
                    continue;
                }

                using Transaction tx = db.TransactionManager.StartTransaction();
                try
                {
                    RadiusDimensionInfo first = ReadRadiusDimensionInfo(tx, firstResult.ObjectId);
                    RadiusDimensionInfo second = ReadRadiusDimensionInfo(tx, secondResult.ObjectId);

                    CombinePair(tx, first, second);
                    tx.Commit();
                    combined++;
                    ed.WriteMessage($"\nCRP_PAIR_OK: Kombinerede {first.Handle} + {second.Handle}.");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    ed.WriteMessage($"\nCRP_ERROR: {ex.Message}");
                }
            }

            ed.WriteMessage($"\nCRP_OK: Kombinerede {combined} radiuspar manuelt.");
        }

        private static PromptEntityResult PromptForRadiusDimension(
            Editor ed,
            string message,
            bool allowNone)
        {
            PromptEntityOptions options = new(message)
            {
                AllowNone = allowNone
            };
            options.SetRejectMessage("\nObjektet er ikke en radiusdimension.");
            options.AddAllowedClass(typeof(RadialDimension), exactMatch: true);
            return ed.GetEntity(options);
        }

        private static List<RadiusDimensionInfo> ReadRadiusDimensionInfos(
            Transaction tx,
            IEnumerable<Oid> ids,
            out int skipped)
        {
            List<RadiusDimensionInfo> result = new();
            HashSet<Oid> seen = new();
            skipped = 0;

            foreach (Oid id in ids)
            {
                if (id.IsNull || !seen.Add(id))
                    continue;

                DBObject obj = tx.GetObject(id, OpenMode.ForRead, openErased: false);
                if (obj is not RadialDimension dim || dim.IsErased)
                {
                    skipped++;
                    continue;
                }

                result.Add(Snapshot(dim));
            }

            return result;
        }

        private static RadiusDimensionInfo ReadRadiusDimensionInfo(Transaction tx, Oid id)
        {
            DBObject obj = tx.GetObject(id, OpenMode.ForRead, openErased: false);
            if (obj is not RadialDimension dim || dim.IsErased)
                throw new InvalidOperationException("Det valgte objekt er ikke en gyldig RadialDimension.");

            return Snapshot(dim);
        }

        private static RadiusDimensionInfo Snapshot(RadialDimension dim)
        {
            Point3d textPosition = dim.TextPosition;
            if (!IsFinite(textPosition))
                textPosition = SafeExtentsCenter(dim);

            return new RadiusDimensionInfo(
                dim.ObjectId,
                dim.Handle.ToString(),
                dim.Center,
                dim.ChordPoint,
                textPosition,
                dim.Measurement,
                dim.LeaderLength);
        }

        private static Point3d SafeExtentsCenter(Entity entity)
        {
            try
            {
                Extents3d extents = entity.GeometricExtents;
                return MidPoint(extents.MinPoint, extents.MaxPoint);
            }
            catch
            {
                return Point3d.Origin;
            }
        }

        private static List<RadiusDimensionPair> BuildAutomaticPairs(
            IReadOnlyList<RadiusDimensionInfo> dims,
            out List<string> errors)
        {
            errors = new List<string>();
            List<RadiusDimensionPair> pairs = new();

            foreach (List<RadiusDimensionInfo> cluster in ClusterByChordPoint(dims))
            {
                if (cluster.Count == 2)
                {
                    pairs.Add(new RadiusDimensionPair(cluster[0], cluster[1]));
                    continue;
                }

                string handles = string.Join(", ", cluster.Select(x => x.Handle));
                if (cluster.Count == 1)
                {
                    errors.Add(
                        $"CRP_NO_PAIR_WITHIN_2M: Radiusdimension {handles} havde ikke et par inden for " +
                        $"{CombineRadiusPairTolerance.ToString("0.#", CultureInfo.InvariantCulture)} m.");
                }
                else
                {
                    errors.Add(
                        $"CRP_MULTIPLE_WITHIN_2M: {cluster.Count} radiusdimensioner ligger inden for " +
                        $"{CombineRadiusPairTolerance.ToString("0.#", CultureInfo.InvariantCulture)} m ({handles}). Vælg disse par manuelt.");
                }
            }

            return pairs;
        }

        private static List<List<RadiusDimensionInfo>> ClusterByChordPoint(
            IReadOnlyList<RadiusDimensionInfo> dims)
        {
            List<List<RadiusDimensionInfo>> clusters = new();
            bool[] visited = new bool[dims.Count];

            for (int i = 0; i < dims.Count; i++)
            {
                if (visited[i])
                    continue;

                List<RadiusDimensionInfo> cluster = new();
                Queue<int> queue = new();
                visited[i] = true;
                queue.Enqueue(i);

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    RadiusDimensionInfo currentDim = dims[current];
                    cluster.Add(currentDim);

                    for (int j = 0; j < dims.Count; j++)
                    {
                        if (visited[j])
                            continue;

                        if (HorizontalDistance(currentDim.ChordPoint, dims[j].ChordPoint) <= CombineRadiusPairTolerance)
                        {
                            visited[j] = true;
                            queue.Enqueue(j);
                        }
                    }
                }

                clusters.Add(cluster.OrderBy(x => x.Handle, StringComparer.OrdinalIgnoreCase).ToList());
            }

            return clusters;
        }

        private static void CombinePair(
            Transaction tx,
            RadiusDimensionInfo first,
            RadiusDimensionInfo second)
        {
            RadialDimension keep = (RadialDimension)tx.GetObject(first.Id, OpenMode.ForWrite);
            RadialDimension remove = (RadialDimension)tx.GetObject(second.Id, OpenMode.ForWrite);

            double averageMeasurement = (first.Measurement + second.Measurement) / 2.0;
            if (averageMeasurement <= 0.0 || double.IsNaN(averageMeasurement) || double.IsInfinity(averageMeasurement))
                throw new InvalidOperationException(
                    $"Ugyldig gennemsnitsradius for {first.Handle} + {second.Handle}.");

            Point3d averageCenter = MidPoint(first.Center, second.Center);
            Point3d averageChordPoint = MidPoint(first.ChordPoint, second.ChordPoint);
            Point3d averageTextPosition = MidPoint(first.TextPosition, second.TextPosition);
            Vector3d radiusDirection = averageChordPoint - averageCenter;

            if (radiusDirection.Length < 1e-9)
                radiusDirection = FallbackRadiusDirection(first, second);

            keep.Center = averageCenter;
            keep.ChordPoint = averageCenter + (radiusDirection.GetNormal() * averageMeasurement);
            keep.LeaderLength = Math.Max(0.0, (first.LeaderLength + second.LeaderLength) / 2.0);
            keep.UsingDefaultTextPosition = false;
            keep.TextPosition = averageTextPosition;
            keep.DimensionText = string.Empty;
            keep.RecomputeDimensionBlock(true);
            keep.RecordGraphicsModified(true);

            remove.Erase(true);
        }

        private static Vector3d FallbackRadiusDirection(
            RadiusDimensionInfo first,
            RadiusDimensionInfo second)
        {
            Vector3d firstDirection = first.ChordPoint - first.Center;
            if (firstDirection.Length >= 1e-9)
                return firstDirection;

            Vector3d secondDirection = second.ChordPoint - second.Center;
            if (secondDirection.Length >= 1e-9)
                return secondDirection;

            throw new InvalidOperationException(
                $"Kan ikke finde radiusretning for {first.Handle} + {second.Handle}.");
        }

        private static Point3d MidPoint(Point3d first, Point3d second) =>
            new(
                (first.X + second.X) / 2.0,
                (first.Y + second.Y) / 2.0,
                (first.Z + second.Z) / 2.0);

        private static double HorizontalDistance(Point3d first, Point3d second)
        {
            double dx = first.X - second.X;
            double dy = first.Y - second.Y;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static bool IsFinite(Point3d point) =>
            double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

        private sealed record RadiusDimensionInfo(
            Oid Id,
            string Handle,
            Point3d Center,
            Point3d ChordPoint,
            Point3d TextPosition,
            double Measurement,
            double LeaderLength);

        private sealed record RadiusDimensionPair(
            RadiusDimensionInfo First,
            RadiusDimensionInfo Second);
    }
}
