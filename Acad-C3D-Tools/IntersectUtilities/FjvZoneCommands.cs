using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

using NtsCoordinate = NetTopologySuite.Geometries.Coordinate;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsGeometryFactory = NetTopologySuite.Geometries.GeometryFactory;
using NtsLineString = NetTopologySuite.Geometries.LineString;
using NtsPoint = NetTopologySuite.Geometries.Point;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private const string FjvZoneLayerPrefix = "0-FJV-Zone-";
        private const string FjvOutsideZoneName = "Udenfor zone";
        private const string FjvZoneOverlapLayer = "0-FJV-ZoneOverlap";
        private const string FjvZoneLegacyOverlapLayer = "0-FJV-Zone-Overlap";
        private const double FjvZoneMaxAutoCloseGap = 1.0;
        private const double FjvZoneConnectionTolerance = 0.01;
        private const double FjvZoneBoundarySliverTolerance = 0.02;
        private const double FjvZoneLinearizationStep = 0.25;
        private const double FjvZoneMinLength = 0.001;
        private const double FjvZoneAreaTolerance = 0.000001;

        private static readonly NtsGeometryFactory FjvZoneGeometryFactory =
            new NtsGeometryFactory(new PrecisionModel(), 0);
        private static readonly Regex FjvPipeLayerNameRegex =
            new Regex(@"FJV-[^-]+-[A-ZÆØÅ0-9]+[0-9]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <command>FJVZONEDEFINE</command>
        /// <summary>
        /// Copies a selected polyline to a validated FJV zone layer named 0-FJV-Zone-{name}.
        /// </summary>
        /// <category>PipeSchedule</category>
        [CommandMethod("FJVZONEDEFINE")]
        public static void FjvZoneDefine()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            PromptEntityOptions peo = new PromptEntityOptions(
                "\nVaelg polyline der skal kopieres som FJV-zone: ");
            peo.SetRejectMessage("\nObjektet skal vaere en 2D polyline.");
            peo.AddAllowedClass(typeof(Polyline), false);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            string? zoneName = PromptFjvZoneName(ed);
            if (zoneName == null) return;

            string layerName = FjvZoneLayerPrefix + zoneName;

            using Transaction tx = db.TransactionManager.StartTransaction();
            Polyline source = (Polyline)tx.GetObject(per.ObjectId, OpenMode.ForRead);
            Polyline zoneCopy = (Polyline)source.Clone();
            bool zoneCopyAppended = false;

            try
            {
                if (!PrepareZonePolylineForCopy(zoneCopy, ed))
                {
                    zoneCopy.Dispose();
                    tx.Abort();
                    return;
                }

                string? validationError = TryCreateZoneInfo(
                    zoneCopy,
                    per.ObjectId,
                    zoneName,
                    layerName,
                    out FjvZoneInfo candidate);
                if (validationError != null)
                {
                    ed.WriteMessage($"\nZone blev ikke oprettet: {validationError}");
                    zoneCopy.Dispose();
                    tx.Abort();
                    return;
                }

                List<string> invalidZoneErrors = new List<string>();
                List<FjvZoneInfo> existingZones = CollectFjvZones(db, tx, ed, invalidZoneErrors);
                if (invalidZoneErrors.Count > 0)
                {
                    ed.WriteMessage("\nZone blev ikke oprettet: eksisterende zonegeometri er ugyldig.");
                    WriteInvalidZoneErrors(ed, invalidZoneErrors, 10);
                    zoneCopy.Dispose();
                    tx.Abort();
                    return;
                }

                List<FjvZoneOverlapIssue> overlapIssues = FindZoneOverlapIssues(candidate, existingZones);
                ClearZoneOverlapMarkers(db, tx);
                if (overlapIssues.Count > 0)
                {
                    AddZoneOverlapMarkers(db, overlapIssues);

                    foreach (FjvZoneOverlapIssue warning in overlapIssues.Where(x => !x.IsFatal))
                        ed.WriteMessage($"\nOBS: {warning.Message}");

                    FjvZoneOverlapIssue? fatalIssue = overlapIssues.FirstOrDefault(x => x.IsFatal);
                    if (fatalIssue != null)
                    {
                        ed.WriteMessage($"\nZone blev ikke oprettet: {fatalIssue.Message}");
                        ed.WriteMessage($"\nOverlap er markeret paa lag '{FjvZoneOverlapLayer}'.");
                        zoneCopy.Dispose();
                        tx.Commit();
                        return;
                    }
                }

                db.CheckOrCreateLayer(layerName);
                zoneCopy.Layer = layerName;
                zoneCopy.ColorIndex = 256;

                BlockTable bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tx.GetObject(
                    bt[BlockTableRecord.ModelSpace],
                    OpenMode.ForWrite);

                ms.AppendEntity(zoneCopy);
                zoneCopyAppended = true;
                tx.AddNewlyCreatedDBObject(zoneCopy, true);
                tx.Commit();

                ed.WriteMessage($"\nZone '{zoneName}' oprettet som kopi paa lag '{layerName}'.");
            }
            catch
            {
                if (!zoneCopyAppended)
                    zoneCopy.Dispose();
                throw;
            }
        }

        /// <command>FJVZONEVOLUME</command>
        /// <summary>
        /// Prints and optionally writes a CSV volume summary split precisely by FJV zones.
        /// </summary>
        /// <category>PipeSchedule</category>
        [CommandMethod("FJVZONEVOLUME")]
        public static void FjvZoneVolume()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            PromptKeywordOptions pko = new PromptKeywordOptions(
                "\nEr ENKELT-rør tegnet som én polyline der repraesenterer begge rør? [Yes/No] <Yes>:");
            pko.Keywords.Add("Yes");
            pko.Keywords.Add("No");
            pko.Keywords.Default = "Yes";
            pko.AllowNone = true;
            PromptResult singleResult = ed.GetKeywords(pko);
            if (singleResult.Status != PromptStatus.OK) return;
            bool singleAsOne = singleResult.StringResult == "Yes" || singleResult.StringResult == "";

            string? csvPath = null;
            PromptKeywordOptions csvOptions = new PromptKeywordOptions(
                "\nSkriv zone-volumen til CSV? [Yes/No] <Yes>:");
            csvOptions.Keywords.Add("Yes");
            csvOptions.Keywords.Add("No");
            csvOptions.Keywords.Default = "Yes";
            csvOptions.AllowNone = true;
            PromptResult csvResult = ed.GetKeywords(csvOptions);
            if (csvResult.Status != PromptStatus.OK) return;
            if (csvResult.StringResult == "Yes" || csvResult.StringResult == "")
            {
                csvPath = PromptSaveCsvPath(doc, "FJV zone volumen CSV");
                if (csvPath == null) return;
            }

            List<string> hardErrors = new List<string>();
            Dictionary<FjvZoneVolumeKey, FjvZoneVolumeBucket> summary =
                new Dictionary<FjvZoneVolumeKey, FjvZoneVolumeBucket>();

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                List<string> invalidZoneErrors = new List<string>();
                List<FjvZoneInfo> zones = CollectFjvZones(db, tx, ed, invalidZoneErrors);
                if (invalidZoneErrors.Count > 0)
                {
                    ed.WriteMessage("\nFJVZONEVOLUME stoppet: eksisterende zonegeometri er ugyldig.");
                    WriteInvalidZoneErrors(ed, invalidZoneErrors, 20);
                    tx.Abort();
                    return;
                }

                if (zones.Count == 0)
                {
                    ed.WriteMessage($"\nIngen zoner fundet. Definer zoner paa lag med prefix '{FjvZoneLayerPrefix}'.");
                    tx.Abort();
                    return;
                }

                List<FjvZoneOverlapIssue> zoneOverlapIssues = AnalyzeZoneSetOverlaps(zones);
                ClearZoneOverlapMarkers(db, tx);
                if (zoneOverlapIssues.Count > 0)
                {
                    AddZoneOverlapMarkers(db, zoneOverlapIssues);

                    foreach (FjvZoneOverlapIssue warning in zoneOverlapIssues.Where(x => !x.IsFatal).Take(20))
                        ed.WriteMessage("\nOBS: " + warning.Message);

                    List<FjvZoneOverlapIssue> fatalIssues = zoneOverlapIssues
                        .Where(x => x.IsFatal)
                        .ToList();
                    if (fatalIssues.Count > 0)
                    {
                        ed.WriteMessage("\nFJVZONEVOLUME stoppet: zonegeometri er ikke gyldig.");
                        foreach (FjvZoneOverlapIssue error in fatalIssues.Take(20))
                            ed.WriteMessage("\n  " + error.Message);
                        if (fatalIssues.Count > 20)
                            ed.WriteMessage($"\n  ... {fatalIssues.Count - 20} flere fejl.");
                        ed.WriteMessage($"\nOverlap er markeret paa lag '{FjvZoneOverlapLayer}'.");
                        tx.Commit();
                        return;
                    }
                }

                List<Polyline> pipes = db.HashSetOfType<Polyline>(tx, true)
                    .Where(IsFjvPipePolyline)
                    .ToList();

                foreach (Polyline pipe in pipes)
                {
                    FjvPipeVolumeInfo? pipeInfo = TryCreatePipeVolumeInfo(pipe);
                    if (pipeInfo == null)
                    {
                        hardErrors.Add($"Rør {pipe.Handle}: kunne ikke laese rørdata.");
                        continue;
                    }

                    AccumulatePipeZoneVolumes(pipe, pipeInfo, zones, singleAsOne, summary, hardErrors);
                }

                tx.Commit();
            }

            if (hardErrors.Count > 0)
            {
                ed.WriteMessage("\nFJVZONEVOLUME stoppet: der er geometri/data der skal rettes.");
                foreach (string error in hardErrors.Take(30))
                    ed.WriteMessage("\n  " + error);
                if (hardErrors.Count > 30)
                    ed.WriteMessage($"\n  ... {hardErrors.Count - 30} flere fejl.");
                return;
            }

            PrintFjvZoneVolumeSummary(ed, summary, singleAsOne);

            if (csvPath != null)
            {
                WriteFjvZoneVolumeCsv(csvPath, summary);
                ed.WriteMessage($"\nCSV skrevet: {csvPath}");
            }
        }

        private static string? PromptFjvZoneName(Editor ed)
        {
            while (true)
            {
                PromptStringOptions pso = new PromptStringOptions("\nZone-navn: ");
                pso.AllowSpaces = true;
                PromptResult pr = ed.GetString(pso);
                if (pr.Status != PromptStatus.OK) return null;

                string zoneName = (pr.StringResult ?? string.Empty).Trim();
                if (zoneName.Length == 0)
                {
                    ed.WriteMessage("\nZone-navn maa ikke vaere tomt.");
                    continue;
                }

                string layerName = FjvZoneLayerPrefix + zoneName;
                if (IsReservedFjvZoneLayer(layerName))
                {
                    ed.WriteMessage($"\n'{zoneName}' er reserveret til zone-validering. Vaelg et andet zone-navn.");
                    continue;
                }

                try
                {
                    SymbolUtilityServices.ValidateSymbolName(layerName, false);
                    return zoneName;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    ed.WriteMessage($"\n'{layerName}' er ikke et gyldigt AutoCAD-lagnavn: {ex.Message}");
                }
            }
        }

        private static bool PrepareZonePolylineForCopy(Polyline polyline, Editor ed)
        {
            if (polyline.NumberOfVertices < 3)
            {
                ed.WriteMessage("\nZone-polyline skal have mindst 3 vertices.");
                return false;
            }

            if (polyline.Closed) return true;

            Point3d start = polyline.GetPoint3dAt(0);
            Point3d end = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
            double gap = start.DistanceHorizontalTo(end);
            if (gap > FjvZoneMaxAutoCloseGap)
            {
                ed.WriteMessage(
                    $"\nPolyline er ikke lukket. Start/slut-afstand er {gap:0.###} m og overstiger {FjvZoneMaxAutoCloseGap:0.###} m.");
                return false;
            }

            PromptKeywordOptions pko = new PromptKeywordOptions(
                $"\nPolyline er aaben med {gap:0.###} m gap. Luk kopien som zone? [Yes/No] <Yes>:");
            pko.Keywords.Add("Yes");
            pko.Keywords.Add("No");
            pko.Keywords.Default = "Yes";
            pko.AllowNone = true;
            PromptResult pr = ed.GetKeywords(pko);
            if (pr.Status != PromptStatus.OK) return false;
            if (pr.StringResult == "No") return false;

            polyline.Closed = true;
            return true;
        }

        private static string? TryCreateZoneInfo(
            Polyline polyline,
            Oid id,
            string zoneName,
            string layerName,
            out FjvZoneInfo zoneInfo)
        {
            zoneInfo = new FjvZoneInfo();

            if (!polyline.Closed)
                return $"Polyline paa lag '{polyline.Layer}' er ikke lukket.";

            List<NtsCoordinate> coords = SampleCurveCoordinates(polyline, true, FjvZoneLinearizationStep);
            if (coords.Count < 4)
                return $"Polyline paa lag '{polyline.Layer}' kan ikke danne en polygon.";

            try
            {
                LinearRing shell = FjvZoneGeometryFactory.CreateLinearRing(coords.ToArray());
                NtsPolygon polygon = FjvZoneGeometryFactory.CreatePolygon(shell);
                if (!polygon.IsValid)
                    return $"Zone '{zoneName}' er selvskærende eller geometrisk ugyldig.";
                if (polygon.Area <= FjvZoneAreaTolerance)
                    return $"Zone '{zoneName}' har for lille areal.";

                zoneInfo = new FjvZoneInfo
                {
                    Id = id,
                    Name = zoneName,
                    Layer = layerName,
                    Polyline = polyline,
                    Polygon = polygon
                };
                return null;
            }
            catch (System.Exception ex)
            {
                return $"Zone '{zoneName}' kunne ikke konverteres til polygon: {ex.Message}";
            }
        }

        private static List<FjvZoneInfo> CollectFjvZones(
            Database db,
            Transaction tx,
            Editor ed,
            List<string>? invalidZoneErrors = null)
        {
            List<FjvZoneInfo> zones = new List<FjvZoneInfo>();
            foreach (Polyline polyline in db.HashSetOfType<Polyline>(tx, true))
            {
                if (!IsFjvZoneLayer(polyline.Layer)) continue;

                string zoneName = GetZoneNameFromLayer(polyline.Layer);
                string? error = TryCreateZoneInfo(
                    polyline,
                    polyline.ObjectId,
                    zoneName,
                    polyline.Layer,
                    out FjvZoneInfo zoneInfo);

                if (error != null)
                {
                    invalidZoneErrors?.Add($"Zone {polyline.Handle}: {error}");
                    continue;
                }

                zones.Add(zoneInfo);
            }

            return zones;
        }

        private static List<FjvZoneOverlapIssue> AnalyzeZoneSetOverlaps(List<FjvZoneInfo> zones)
        {
            List<FjvZoneOverlapIssue> issues = new List<FjvZoneOverlapIssue>();
            for (int i = 0; i < zones.Count; i++)
            {
                for (int j = i + 1; j < zones.Count; j++)
                {
                    issues.AddRange(FindZoneOverlapIssues(zones[i], new[] { zones[j] }));
                }
            }

            return issues;
        }

        private static List<FjvZoneOverlapIssue> FindZoneOverlapIssues(
            FjvZoneInfo candidate,
            IEnumerable<FjvZoneInfo> existingZones)
        {
            List<FjvZoneOverlapIssue> issues = new List<FjvZoneOverlapIssue>();
            foreach (FjvZoneInfo existing in existingZones)
            {
                if (existing.Id == candidate.Id) continue;

                NtsGeometry intersection;
                try
                {
                    intersection = candidate.Polygon.Intersection(existing.Polygon);
                }
                catch (System.Exception ex)
                {
                    issues.Add(new FjvZoneOverlapIssue(
                        candidate.Name,
                        existing.Name,
                        FjvZoneGeometryFactory.CreatePoint(candidate.Polygon.Centroid.Coordinate),
                        true,
                        $"Zone '{candidate.Name}' kunne ikke sammenlignes med '{existing.Name}': {ex.Message}"));
                    continue;
                }

                if (intersection.IsEmpty)
                    continue;

                if (candidate.Name.Equals(existing.Name, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new FjvZoneOverlapIssue(
                        candidate.Name,
                        existing.Name,
                        intersection,
                        true,
                        $"Zone '{candidate.Name}' roerer eller overlapper en anden polygon med samme navn. Samme zone-navn maa kun bruges til adskilte polygoner."));
                    continue;
                }

                if (intersection.Area > FjvZoneAreaTolerance)
                {
                    double sliverWidth = ApproximateOverlapWidth(intersection);
                    bool isBoundarySliver = sliverWidth <= FjvZoneBoundarySliverTolerance;
                    issues.Add(new FjvZoneOverlapIssue(
                        candidate.Name,
                        existing.Name,
                        intersection,
                        !isBoundarySliver,
                        isBoundarySliver
                            ? $"Zone '{candidate.Name}' og '{existing.Name}' har en tynd graense-sliver paa {intersection.Area:0.###} m2, ca. bredde {sliverWidth:0.###} m. Den accepteres som tolerance og markeres ikke."
                            : $"Zone '{candidate.Name}' overlapper zone '{existing.Name}' med areal {intersection.Area:0.###} m2, ca. bredde {sliverWidth:0.###} m."));
                }
            }

            return issues;
        }

        private static double ApproximateOverlapWidth(NtsGeometry intersection)
        {
            if (intersection.Area <= 0.0) return 0.0;
            if (intersection.Length <= FjvZoneMinLength) return double.MaxValue;
            return 2.0 * intersection.Area / intersection.Length;
        }

        private static bool IsFjvZoneLayer(string layerName)
        {
            if (IsReservedFjvZoneLayer(layerName))
            {
                return false;
            }

            return layerName.StartsWith(FjvZoneLayerPrefix, StringComparison.OrdinalIgnoreCase) &&
                layerName.Length > FjvZoneLayerPrefix.Length;
        }

        private static bool IsReservedFjvZoneLayer(string layerName) =>
            layerName.Equals(FjvZoneOverlapLayer, StringComparison.OrdinalIgnoreCase) ||
            layerName.Equals(FjvZoneLegacyOverlapLayer, StringComparison.OrdinalIgnoreCase);

        private static string GetZoneNameFromLayer(string layerName) =>
            layerName.Substring(FjvZoneLayerPrefix.Length);

        private static bool IsFjvPipePolyline(Polyline polyline)
        {
            if (IsFjvZoneLayer(polyline.Layer)) return false;
            return IsFjvPipeLayerName(polyline.Layer);
        }

        private static bool IsFjvPipeLayerName(string layerName) =>
            FjvPipeLayerNameRegex.IsMatch(ExtractLocalLayerName(layerName));

        private static string ExtractLocalLayerName(string layerName)
        {
            int xrefSeparator = layerName.LastIndexOf('|');
            return xrefSeparator >= 0 ? layerName.Substring(xrefSeparator + 1) : layerName;
        }

        private static FjvPipeVolumeInfo? TryCreatePipeVolumeInfo(Polyline polyline)
        {
            int dn = GetPipeDN(polyline);
            if (dn == 0) return null;

            PipeSystemEnum systemEnum = GetPipeSystem(polyline);
            PipeTypeEnum typeEnum = GetPipeType(polyline);
            string system = GetSystemString(systemEnum);
            if (system.Equals("DN", StringComparison.OrdinalIgnoreCase))
                system = "STÅL";

            string type = typeEnum == PipeTypeEnum.Frem || typeEnum == PipeTypeEnum.Retur
                ? "ENKELT"
                : typeEnum.ToString();

            double idMm = GetPipeId(polyline);
            if (idMm <= 0.0) return null;

            return new FjvPipeVolumeInfo(system, type, dn, idMm);
        }

        private static void AddZoneOverlapMarkers(
            Database db,
            IEnumerable<FjvZoneOverlapIssue> overlapIssues)
        {
            db.CheckOrCreateLayer(FjvZoneOverlapLayer, 1, false);

            foreach (FjvZoneOverlapIssue issue in overlapIssues.Where(x => x.IsFatal))
                AddSimpleZoneOverlapMarker(db, issue.Geometry);
        }

        private static void AddSimpleZoneOverlapMarker(
            Database db,
            NtsGeometry geometry)
        {
            if (geometry.IsEmpty) return;

            Envelope envelope = geometry.EnvelopeInternal;
            double minX = envelope.MinX;
            double minY = envelope.MinY;
            double maxX = envelope.MaxX;
            double maxY = envelope.MaxY;
            double width = maxX - minX;
            double height = maxY - minY;
            double pad = Math.Max(Math.Max(width, height) * 0.05, 0.25);

            if (width <= FjvZoneMinLength && height <= FjvZoneMinLength)
            {
                NtsPoint centroid = geometry.Centroid;
                if (!centroid.IsEmpty)
                {
                    Circle pointMarker = new Circle(new Point3d(centroid.X, centroid.Y, 0.0), Vector3d.ZAxis, pad)
                    {
                        Layer = FjvZoneOverlapLayer,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, 1)
                    };
                    pointMarker.AddEntityToDbModelSpace(db);
                }

                return;
            }

            minX -= pad;
            minY -= pad;
            maxX += pad;
            maxY += pad;

            Polyline marker = new Polyline();
            marker.AddVertexAt(0, new Point2d(minX, minY), 0.0, 0.0, 0.0);
            marker.AddVertexAt(1, new Point2d(maxX, minY), 0.0, 0.0, 0.0);
            marker.AddVertexAt(2, new Point2d(maxX, maxY), 0.0, 0.0, 0.0);
            marker.AddVertexAt(3, new Point2d(minX, maxY), 0.0, 0.0, 0.0);
            marker.Closed = true;
            marker.Layer = FjvZoneOverlapLayer;
            marker.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
            marker.AddEntityToDbModelSpace(db);
        }

        private static void AccumulatePipeZoneVolumes(
            Polyline pipe,
            FjvPipeVolumeInfo pipeInfo,
            List<FjvZoneInfo> zones,
            bool singleAsOne,
            Dictionary<FjvZoneVolumeKey, FjvZoneVolumeBucket> summary,
            List<string> hardErrors)
        {
            double pipeLength = pipe.Length;
            if (pipeLength < FjvZoneMinLength) return;

            NtsLineString pipeLine = CreateLineString(pipe);
            foreach (FjvZoneInfo zone in zones)
            {
                NtsGeometry boundaryOverlap = pipeLine.Intersection(zone.Polygon.Boundary);
                if (boundaryOverlap.Length > FjvZoneConnectionTolerance)
                {
                    hardErrors.Add(
                        $"Rør {pipe.Handle} ligger langs zonegraense for '{zone.Name}' over {boundaryOverlap.Length:0.###} m. Flyt roeret til den ene side.");
                    return;
                }
            }

            List<double> splitDistances = new List<double> { 0.0, pipeLength };
            foreach (FjvZoneInfo zone in zones)
            {
                List<Point3d> intersections = pipe.IntersectWithValidation(zone.Polyline, new List<Point3d>());
                foreach (Point3d intersection in intersections)
                {
                    if (TryGetDistanceAtPoint(pipe, intersection, out double distance) &&
                        distance > FjvZoneMinLength &&
                        distance < pipeLength - FjvZoneMinLength)
                    {
                        splitDistances.Add(distance);
                    }
                }
            }

            splitDistances = DeduplicateDistances(splitDistances, FjvZoneConnectionTolerance);

            for (int i = 1; i < splitDistances.Count; i++)
            {
                double start = splitDistances[i - 1];
                double end = splitDistances[i];
                double segmentLength = end - start;
                if (segmentLength < FjvZoneMinLength) continue;

                Point3d midPoint = pipe.GetPointAtDist((start + end) / 2.0);
                string zoneName = ClassifyPointInZones(midPoint, zones, pipe.Handle.ToString(), hardErrors);
                if (hardErrors.Count > 0) return;

                double effectiveLength = segmentLength;
                if (pipeInfo.Type.Equals("ENKELT", StringComparison.OrdinalIgnoreCase) && singleAsOne)
                    effectiveLength *= 2.0;

                double idMeters = pipeInfo.InternalDiameterMm / 1000.0;
                double area = Math.PI * Math.Pow(idMeters, 2) / 4.0;
                double volume = area * effectiveLength;

                if (pipeInfo.Type.Equals("Twin", StringComparison.OrdinalIgnoreCase))
                    volume *= 2.0;

                FjvZoneVolumeKey key = new FjvZoneVolumeKey(
                    zoneName,
                    pipeInfo.System,
                    pipeInfo.Type,
                    pipeInfo.Dn);

                if (!summary.TryGetValue(key, out FjvZoneVolumeBucket bucket))
                {
                    bucket = new FjvZoneVolumeBucket();
                    summary[key] = bucket;
                }

                bucket.Length += effectiveLength;
                bucket.Volume += volume;
            }
        }

        private static string ClassifyPointInZones(
            Point3d point,
            List<FjvZoneInfo> zones,
            string pipeHandle,
            List<string> hardErrors)
        {
            NtsPoint ntsPoint = FjvZoneGeometryFactory.CreatePoint(new NtsCoordinate(point.X, point.Y));
            List<FjvZoneInfo> matches = zones
                .Where(zone => zone.Polygon.Covers(ntsPoint))
                .ToList();

            if (matches.Count == 0) return FjvOutsideZoneName;
            if (matches.Count == 1) return matches[0].Name;

            hardErrors.Add(
                $"Rør {pipeHandle}: intervalmidtpunkt ligger i flere zoner ({string.Join(", ", matches.Select(x => x.Name))}).");
            return FjvOutsideZoneName;
        }

        private static List<double> DeduplicateDistances(List<double> distances, double tolerance)
        {
            List<double> sorted = distances.OrderBy(x => x).ToList();
            List<double> result = new List<double>();

            foreach (double distance in sorted)
            {
                if (result.Count == 0 || Math.Abs(distance - result[result.Count - 1]) > tolerance)
                    result.Add(distance);
            }

            return result;
        }

        private static bool TryGetDistanceAtPoint(Polyline polyline, Point3d point, out double distance)
        {
            distance = 0.0;
            try
            {
                Point3d closest = polyline.GetClosestPointTo(point, false);
                distance = polyline.GetDistAtPoint(closest);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void PrintFjvZoneVolumeSummary(
            Editor ed,
            Dictionary<FjvZoneVolumeKey, FjvZoneVolumeBucket> summary,
            bool singleAsOne)
        {
            ed.WriteMessage("\nFJV Zone Volume Summary");
            ed.WriteMessage("\n=======================");
            ed.WriteMessage($"\nENKELT tegnet som én polyline: {(singleAsOne ? "Yes" : "No")}");
            ed.WriteMessage("\nKolonner: Pipe, Length (m), Volume (m3)");

            if (summary.Count == 0)
            {
                ed.WriteMessage("\nIngen gyldige rør fundet.");
                return;
            }

            double grandLength = summary.Sum(x => x.Value.Length);
            double grandVolume = summary.Sum(x => x.Value.Volume);
            int zoneCount = summary
                .Select(x => x.Key.Zone)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            foreach (IGrouping<string, KeyValuePair<FjvZoneVolumeKey, FjvZoneVolumeBucket>> zoneGroup
                in SortZoneGroups(summary))
            {
                double zoneLength = zoneGroup.Sum(x => x.Value.Length);
                double zoneVolume = zoneGroup.Sum(x => x.Value.Volume);
                ed.WriteMessage($"\n\nZone: {zoneGroup.Key}");
                ed.WriteMessage("\n  " + string.Format("{0,-30}{1,14}{2,16}",
                    "Pipe", "Length (m)", "Volume (m3)"));
                ed.WriteMessage("\n  " + new string('-', 60));
                ed.WriteMessage("\n  " + string.Format("{0,-30}{1,14:0.0}{2,16:0.000}",
                    "TOTAL", zoneLength, zoneVolume));

                foreach (KeyValuePair<FjvZoneVolumeKey, FjvZoneVolumeBucket> entry in zoneGroup
                    .OrderBy(x => x.Key.System.Equals("STÅL", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(x => x.Key.System, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(x => x.Key.Dn)
                    .ThenBy(x => x.Key.Type.Equals("ENKELT", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
                {
                    string label = $"{entry.Key.System}-{entry.Key.Type}-DN{entry.Key.Dn}";
                    ed.WriteMessage("\n  " + string.Format("{0,-30}{1,14:0.0}{2,16:0.000}",
                        label,
                        entry.Value.Length,
                        entry.Value.Volume));
                }
            }

            ed.WriteMessage("\n\nSamlet total inkl. alle zoner og Udenfor zone");
            ed.WriteMessage("\n---------------------------------------------");
            ed.WriteMessage($"\nZoner i output: {zoneCount}");
            ed.WriteMessage("\n" + string.Format("{0,-30}{1,14}{2,16}",
                "Pipe", "Length (m)", "Volume (m3)"));
            ed.WriteMessage("\n" + new string('-', 60));
            ed.WriteMessage("\n" + string.Format("{0,-30}{1,14:0.0}{2,16:0.000}",
                "GRAND TOTAL",
                grandLength,
                grandVolume));

            foreach (var pipeGroup in summary
                .GroupBy(x => new { x.Key.System, x.Key.Type, x.Key.Dn })
                .OrderBy(x => x.Key.System.Equals("STÅL", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(x => x.Key.System, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(x => x.Key.Dn)
                .ThenBy(x => x.Key.Type.Equals("ENKELT", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
            {
                string label = $"{pipeGroup.Key.System}-{pipeGroup.Key.Type}-DN{pipeGroup.Key.Dn}";
                ed.WriteMessage("\n" + string.Format("{0,-30}{1,14:0.0}{2,16:0.000}",
                    label,
                    pipeGroup.Sum(x => x.Value.Length),
                    pipeGroup.Sum(x => x.Value.Volume)));
            }
        }

        private static IEnumerable<IGrouping<string, KeyValuePair<FjvZoneVolumeKey, FjvZoneVolumeBucket>>> SortZoneGroups(
            Dictionary<FjvZoneVolumeKey, FjvZoneVolumeBucket> summary)
        {
            return summary
                .GroupBy(x => x.Key.Zone)
                .OrderBy(g => g.Key.Equals(FjvOutsideZoneName, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        }

        private static void WriteFjvZoneVolumeCsv(
            string csvPath,
            Dictionary<FjvZoneVolumeKey, FjvZoneVolumeBucket> summary)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo("da-DK");
            using StreamWriter writer = new StreamWriter(csvPath, false, Encoding.UTF8);
            writer.WriteLine("Zone;Pipe;System;Type;DN;Length_m;Volume_m3");

            foreach (IGrouping<string, KeyValuePair<FjvZoneVolumeKey, FjvZoneVolumeBucket>> zoneGroup
                in SortZoneGroups(summary))
            {
                foreach (KeyValuePair<FjvZoneVolumeKey, FjvZoneVolumeBucket> entry in zoneGroup
                    .OrderBy(x => x.Key.System.Equals("STÅL", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(x => x.Key.System, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(x => x.Key.Dn)
                    .ThenBy(x => x.Key.Type.Equals("ENKELT", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
                {
                    string label = $"{entry.Key.System}-{entry.Key.Type}-DN{entry.Key.Dn}";
                    writer.WriteLine(string.Join(";",
                        CsvEscape(entry.Key.Zone),
                        CsvEscape(label),
                        CsvEscape(entry.Key.System),
                        CsvEscape(entry.Key.Type),
                        entry.Key.Dn.ToString(CultureInfo.InvariantCulture),
                        entry.Value.Length.ToString("0.###", culture),
                        entry.Value.Volume.ToString("0.######", culture)));
                }

                writer.WriteLine(string.Join(";",
                    CsvEscape(zoneGroup.Key),
                    "TOTAL",
                    "",
                    "",
                    "",
                    zoneGroup.Sum(x => x.Value.Length).ToString("0.###", culture),
                    zoneGroup.Sum(x => x.Value.Volume).ToString("0.######", culture)));
            }
        }

        private static string CsvEscape(string value)
        {
            if (!value.Contains(';') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string? PromptSaveCsvPath(Document doc, string title)
        {
            string initialDirectory = GetDocumentDirectory(doc);
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = "CSV (*.csv)|*.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = initialDirectory,
                FileName = BuildDefaultFileName(doc, "-FJV-ZoneVolumen.csv")
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private static string GetDocumentDirectory(Document doc)
        {
            try
            {
                string? directory = Path.GetDirectoryName(doc.Name);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    return directory;
            }
            catch
            {
                // Ignore and use current directory fallback.
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private static string BuildDefaultFileName(Document doc, string suffix)
        {
            string baseName = "drawing";
            try
            {
                string? fileName = Path.GetFileNameWithoutExtension(doc.Name);
                if (!string.IsNullOrWhiteSpace(fileName))
                    baseName = fileName;
            }
            catch
            {
                // Use fallback.
            }

            return baseName + suffix;
        }

        private static void ClearEntitiesOnLayer(Database db, Transaction tx, string layerName)
        {
            BlockTable bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord ms = (BlockTableRecord)tx.GetObject(
                bt[BlockTableRecord.ModelSpace],
                OpenMode.ForRead);

            foreach (Oid id in ms)
            {
                if (tx.GetObject(id, OpenMode.ForRead) is Entity ent &&
                    ent.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                {
                    ent.UpgradeOpen();
                    ent.Erase(true);
                }
            }
        }

        private static void ClearZoneOverlapMarkers(Database db, Transaction tx)
        {
            ClearEntitiesOnLayer(db, tx, FjvZoneOverlapLayer);
            ClearEntitiesOnLayer(db, tx, FjvZoneLegacyOverlapLayer);
        }

        private static void WriteInvalidZoneErrors(Editor ed, IReadOnlyCollection<string> invalidZoneErrors, int maxRows)
        {
            foreach (string error in invalidZoneErrors.Take(maxRows))
                ed.WriteMessage("\n  " + error);

            if (invalidZoneErrors.Count > maxRows)
                ed.WriteMessage($"\n  ... {invalidZoneErrors.Count - maxRows} flere fejl.");
        }

        private static List<NtsCoordinate> SampleCurveCoordinates(
            Polyline polyline,
            bool forceClosed,
            double maxStep)
        {
            List<NtsCoordinate> coords = new List<NtsCoordinate>();
            double length = polyline.Length;
            if (length < FjvZoneMinLength)
                return coords;

            int steps = Math.Max(1, (int)Math.Ceiling(length / maxStep));
            for (int i = 0; i <= steps; i++)
            {
                double distance = Math.Min(length, length * i / steps);
                Point3d point = polyline.GetPointAtDist(distance);
                AddCoordinateIfDistinct(coords, new NtsCoordinate(point.X, point.Y));
            }

            if (forceClosed && coords.Count > 0)
            {
                NtsCoordinate first = coords[0];
                NtsCoordinate last = coords[coords.Count - 1];
                if (CoordinateDistance(first, last) > FjvZoneMinLength)
                    coords.Add(new NtsCoordinate(first.X, first.Y));
                else
                    coords[coords.Count - 1] = new NtsCoordinate(first.X, first.Y);
            }

            return coords;
        }

        private static NtsLineString CreateLineString(Polyline polyline)
        {
            List<NtsCoordinate> coords = SampleCurveCoordinates(polyline, false, FjvZoneLinearizationStep);
            if (coords.Count == 1)
                coords.Add(new NtsCoordinate(coords[0].X, coords[0].Y));
            return FjvZoneGeometryFactory.CreateLineString(coords.ToArray());
        }

        private static void AddCoordinateIfDistinct(List<NtsCoordinate> coords, NtsCoordinate coord)
        {
            if (coords.Count == 0 ||
                CoordinateDistance(coords[coords.Count - 1], coord) > FjvZoneMinLength)
            {
                coords.Add(coord);
            }
        }

        private static double CoordinateDistance(NtsCoordinate a, NtsCoordinate b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private sealed class FjvZoneInfo
        {
            public Oid Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public string Layer { get; init; } = string.Empty;
            public Polyline Polyline { get; init; } = null!;
            public NtsPolygon Polygon { get; init; } = null!;
        }

        private sealed record FjvZoneOverlapIssue(
            string ZoneA,
            string ZoneB,
            NtsGeometry Geometry,
            bool IsFatal,
            string Message);

        private sealed record FjvPipeVolumeInfo(string System, string Type, int Dn, double InternalDiameterMm);

        private sealed record FjvZoneVolumeKey(string Zone, string System, string Type, int Dn);

        private sealed class FjvZoneVolumeBucket
        {
            public double Length { get; set; }
            public double Volume { get; set; }
        }
    }
}
