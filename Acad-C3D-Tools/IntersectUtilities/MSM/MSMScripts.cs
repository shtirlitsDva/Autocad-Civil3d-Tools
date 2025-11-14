using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors; // For color
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput; // Added for Editor & Prompt* classes
using Autodesk.AutoCAD.Geometry; // For Point3d
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices; // Added for ProfileView access
using IntersectUtilities.UtilsCommon;
using System; // For Math
using System.Collections.Generic; // Added for list/dict helpers
using System.Linq;
using static IntersectUtilities.UtilsCommon.Utils;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>LABELPROFILEVIEWS</command>
        /// <summary>
        /// This command is numbering each Profile View with a label on the left side of each Profile View. 
        /// The color of the label should be yellow when the Profile View is not yet drawn with profiles or when the profiles needs to be edited. 
        /// The drawer should manually change the color to green when a Profile View is ready with updated profiles.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("LABELPROFILEVIEWS")]
        public void labelprofileviews()
        {
            DocumentCollection docCol = AcadApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                //Code starts here
                var profileViews = localDb.HashSetOfType<ProfileView>(tx);
                prdDbg($"Antal ProfileViews fundet: {profileViews.Count}");

                // Prepare current space for adding text
                var btr = (BlockTableRecord)tx.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);

                // Ensure Arial text style exists
                TextStyleTable tst = (TextStyleTable)tx.GetObject(localDb.TextStyleTableId, OpenMode.ForRead);
                ObjectId arialStyleId = ObjectId.Null;
                const string styleName = "Arial";
                if (tst.Has(styleName))
                {
                    arialStyleId = tst[styleName];
                }
                else
                {
                    tst.UpgradeOpen();
                    TextStyleTableRecord tsRec = new TextStyleTableRecord
                    {
                        Name = styleName,
                        FileName = "arial.ttf"
                    };
                    arialStyleId = tst.Add(tsRec);
                    tx.AddNewlyCreatedDBObject(tsRec, true);
                }

                foreach (var pv in profileViews.OrderBy(x => x.Name))
                {
                    var loc = pv.Location; // Point3d
                    prdDbg($"ProfileView: {pv.Name} | Location: ({loc.X:0.###}, {loc.Y:0.###}, {loc.Z:0.###})");

                    // Insertion point 100 units to the left
                    Point3d insPt = new Point3d(loc.X - 100.0, loc.Y, loc.Z);

                    // Create text with pv name
                    DBText txt = new DBText
                    {
                        Position = insPt,
                        TextString = pv.Name.Replace("_PV", ""),
                        Height = 20,
                        Layer = "0"
                    };

                    if (!arialStyleId.IsNull)
                        txt.TextStyleId = arialStyleId;

                    txt.Color = Color.FromColorIndex(ColorMethod.ByAci, 2); // Yellow

                    btr.AppendEntity(txt);
                    tx.AddNewlyCreatedDBObject(txt, true);
                }
                //Code ends here
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tx.Abort();
                return;
            }

            tx.Commit();
        }

        // DRAWCOMPASSSTAR command removed as requested.

        /// <command>TRIM3DPOLYATINTERSECTIONS / TRIM3DPL</command>
        /// <summary>
        /// User-driven trimming: Prompts user to select 3D polylines to be trimmed.
        /// For each selected polyline, all intersections (XY) with any other 3D polyline are evaluated.
        /// The intersection producing the smallest of the two side lengths (start->intersection vs intersection->end) is chosen.
        /// The shorter side is removed and the longer side retained. If no intersection is found, the polyline is left unchanged.
        /// Z at intersection is interpolated along the subject segment.
        /// Preserves original layer, color and XData (entity + retained vertices) and extension dictionary content on the resulting trimmed polyline.
        /// </summary>
        /// <category>Utilities</category>
        [CommandMethod("TRIM3DPOLYATINTERSECTIONS")]
        [CommandMethod("TRIM3DPL")] // Abbreviated alias
        public void Trim3DPolylinesAtIntersections()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            var selOpts = new PromptSelectionOptions { MessageForAdding = "Select 3D polylines to trim:" };
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "POLYLINE") });
            var selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nSelection cancelled.");
                return;
            }

            using var tr = db.TransactionManager.StartTransaction();
            try
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                var allPolys = db.HashSetOfType<Polyline3d>(tr).ToList();
                if (allPolys.Count < 2)
                {
                    ed.WriteMessage("\nNeed at least two 3D polylines in the drawing.");
                    tr.Commit();
                    return;
                }

                var subjectPolys = new List<Polyline3d>();
                foreach (var id in selRes.Value.GetObjectIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Polyline3d p3d) subjectPolys.Add(p3d);
                }
                if (subjectPolys.Count == 0)
                {
                    ed.WriteMessage("\nNo 3D polylines in selection.");
                    tr.Commit();
                    return;
                }

                // Cache vertices for all polylines
                var polyVertices = new Dictionary<Polyline3d, List<Point3d>>();
                foreach (var pl in allPolys)
                {
                    var pts = new List<Point3d>();
                    foreach (ObjectId vId in pl)
                    {
                        var v = (PolylineVertex3d)tr.GetObject(vId, OpenMode.ForRead);
                        pts.Add(v.Position);
                    }
                    if (pts.Count >= 2) polyVertices[pl] = pts;
                }

                int trimmedCount = 0;
                foreach (var subj in subjectPolys)
                {
                    if (!polyVertices.TryGetValue(subj, out var subjPts)) continue;

                    // Capture original vertex XData buffers
                    var vertexXData = new List<ResultBuffer?>();
                    foreach (ObjectId vId in subj)
                    {
                        var v = (PolylineVertex3d)tr.GetObject(vId, OpenMode.ForRead);
                        vertexXData.Add(v.XData == null ? null : new ResultBuffer(v.XData.AsArray()));
                    }
                    // Capture polyline XData
                    var polyXData = subj.XData == null ? null : new ResultBuffer(subj.XData.AsArray());

                    // Collect all RegApp names used (entity + vertices)
                    var regAppNames = new HashSet<string>();
                    void CollectApps(ResultBuffer? rb)
                    {
                        if (rb == null) return;
                        foreach (TypedValue tv in rb)
                            if (tv.TypeCode == 1001 && tv.Value is string s) regAppNames.Add(s);
                    }
                    CollectApps(polyXData);
                    foreach (var rb in vertexXData) CollectApps(rb);
                    RegisterRegApps(db, tr, regAppNames);

                    // Segment length precompute
                    var segLengths = new double[subjPts.Count - 1];
                    double totalLen = 0;
                    for (int i = 0; i < segLengths.Length; i++) { segLengths[i] = subjPts[i].DistanceTo(subjPts[i + 1]); totalLen += segLengths[i]; }

                    double bestShorterSide = double.MaxValue; int bestSegIndex = -1; double bestT = 0; Point3d bestIp = Point3d.Origin;
                    foreach (var other in allPolys)
                    {
                        if (other == subj) continue;
                        if (!polyVertices.TryGetValue(other, out var otherPts)) continue;
                        for (int a = 0; a < subjPts.Count - 1; a++)
                        {
                            var a1 = subjPts[a]; var a2 = subjPts[a + 1];
                            for (int b = 0; b < otherPts.Count - 1; b++)
                            {
                                var b1 = otherPts[b]; var b2 = otherPts[b + 1];
                                if (!TrySegmentIntersectXY(a1, a2, b1, b2, out double ta, out double tb, out Point2d ip2d)) continue;
                                double zInterp = a1.Z + (a2.Z - a1.Z) * ta;
                                var ip = new Point3d(ip2d.X, ip2d.Y, zInterp);
                                double lenToIntersection = 0; for (int s = 0; s < a; s++) lenToIntersection += segLengths[s]; lenToIntersection += segLengths[a] * ta;
                                double lenFromIntersection = totalLen - lenToIntersection; double shorter = Math.Min(lenToIntersection, lenFromIntersection);
                                if (shorter + 1e-9 < bestShorterSide)
                                {
                                    bestShorterSide = shorter; bestSegIndex = a; bestT = ta; bestIp = ip;
                                }
                            }
                        }
                    }

                    if (bestSegIndex < 0) continue; // nothing to trim

                    double lenToInt = 0; for (int s = 0; s < bestSegIndex; s++) lenToInt += segLengths[s]; lenToInt += segLengths[bestSegIndex] * bestT;
                    double lenFromInt = totalLen - lenToInt; bool keepStartSide = lenToInt >= lenFromInt;

                    var newPts = new List<Point3d>();
                    var newVertexSourceIndices = new List<int?>(); // Maps new vertex index -> original vertex index (null if synthetic intersection)

                    if (keepStartSide)
                    {
                        for (int iPt = 0; iPt <= bestSegIndex; iPt++)
                        {
                            newPts.Add(subjPts[iPt]);
                            newVertexSourceIndices.Add(iPt);
                        }
                        if (bestIp.DistanceTo(newPts[^1]) > 1e-6)
                        {
                            newPts.Add(bestIp);
                            newVertexSourceIndices.Add(null); // synthetic intersection
                        }
                    }
                    else
                    {
                        // Intersection first if not coincident with next vertex
                        if (bestIp.DistanceTo(subjPts[bestSegIndex + 1]) > 1e-6)
                        {
                            newPts.Add(bestIp);
                            newVertexSourceIndices.Add(null);
                        }
                        for (int iPt = bestSegIndex + 1; iPt < subjPts.Count; iPt++)
                        {
                            newPts.Add(subjPts[iPt]);
                            newVertexSourceIndices.Add(iPt);
                        }
                    }

                    if (newPts.Count < 2) continue;

                    var newPl = new Polyline3d(Poly3dType.SimplePoly, new Point3dCollection(newPts.ToArray()), false);
                    btr.AppendEntity(newPl); tr.AddNewlyCreatedDBObject(newPl, true);

                    // Preserve basic properties
                    newPl.SetPropertiesFrom(subj);

                    PropertySetManager.CopyAllProperties(subj, newPl);

                    subj.UpgradeOpen(); subj.Erase();
                    trimmedCount++;
                }

                ed.WriteMessage($"\nTrimmed {trimmedCount} selected 3D polylines (data preserved). ");
                tr.Commit();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                tr.Abort();
            }
        }

        private static void RegisterRegApps(Database db, Transaction tr, IEnumerable<string> names)
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!rat.Has(name))
                {
                    rat.UpgradeOpen();
                    var rec = new RegAppTableRecord { Name = name };
                    rat.Add(rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                }
            }
        }

        private static bool TrySegmentIntersectXY(Point3d p1, Point3d p2, Point3d q1, Point3d q2, out double t, out double u, out Point2d ip)
        {
            t = u = 0; ip = default;
            var a1 = new Point2d(p1.X, p1.Y); var a2 = new Point2d(p2.X, p2.Y);
            var b1 = new Point2d(q1.X, q1.Y); var b2 = new Point2d(q2.X, q2.Y);
            var r = a2 - a1; var s = b2 - b1; double denom = r.X * s.Y - r.Y * s.X; if (Math.Abs(denom) < 1e-9) return false; var diff = b1 - a1;
            t = (diff.X * s.Y - diff.Y * s.X) / denom; u = (diff.X * r.Y - diff.Y * r.X) / denom; if (t < 0 || t > 1 || u < 0 || u > 1) return false; ip = new Point2d(a1.X + r.X * t, a1.Y + r.Y * t); return true;
        }
    }
}
