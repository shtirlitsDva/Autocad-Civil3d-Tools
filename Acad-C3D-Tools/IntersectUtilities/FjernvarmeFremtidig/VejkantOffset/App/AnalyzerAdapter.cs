using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Analysis.Spatial;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Rendering;
using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App
{
    internal sealed class VejkantAnalyzer : IAnalyzer<Line, VejkantAnalysis>
    {
        private readonly Database _dimDb;
        private readonly Database _gkDb;
        private readonly Database _targetDb;
        private readonly VejkantOffsetSettings _settings;
        private readonly SpatialGridCache _cache;

        public VejkantAnalyzer(Database dimDb, Database gkDb, Database targetDb, VejkantOffsetSettings settings)
        {
            _dimDb = dimDb;
            _gkDb = gkDb;
            _targetDb = targetDb;
            _settings = settings;
            _cache = new(cellSize: 5.0);

            //Here goes gemetric caching of vejkant polylines

            using (var tx = _gkDb.TransactionManager.StartOpenCloseTransaction())
            {
                var plines = _gkDb.ListOfType<Polyline>(tx)
                    .Where(p => p.Layer == "Vejkant");

                foreach (var pl in plines)
                {
                    var pts = new List<Point2d>();
                    for (int i = 0; i < pl.NumberOfVertices; i++)
                    {
                        var pt = pl.GetPoint2dAt(i);
                        if (pts.Count == 0 || !IsCoincident(pts[^1], pt))
                        {
                            pts.Add(pt);
                        }
                    }

                    // ensure closed polylines wrap around
                    bool closed = pl.Closed;
                    int count = pts.Count;
                    for (int i = 0; i < count - 1; i++)
                    {
                        AddSegment(pl.ObjectId, pts[i], pts[i + 1]);
                    }
                    if (closed && count > 1)
                    {
                        AddSegment(pl.ObjectId, pts[^1], pts[0]);
                    }
                }
                tx.Commit();
            }
        }

        private void AddSegment(ObjectId plId, Point2d a, Point2d b)
        {
            if (!IsCoincident(a, b))
            {
                var seg = new Segment2d(a, b, plId);
                _cache.Insert(seg);
            }
        }

        private static bool IsCoincident(Point2d a, Point2d b, double tol = 1e-6)
        {
            return a.GetDistanceTo(b) < tol;
        }

        public IEnumerable<Segment2d> QueryIntersections(Line workingLine)
        {
            var a = new Point2d(workingLine.StartPoint.X, workingLine.StartPoint.Y);
            var b = new Point2d(workingLine.EndPoint.X, workingLine.EndPoint.Y);
            var box = new Extents2d(
            new Point2d(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)),
            new Point2d(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));

            foreach (var seg in _cache.Query(box))
            {
                if (Segment2d.Intersects(a, b, seg.A, seg.B))
                {
                    yield return seg;
                }
            }
        }

        public VejkantAnalysis Analyze(Line workingLine)
        {
            var pipelineSegments = new List<PipelineSegment>();

            using (var tr = _dimDb.TransactionManager.StartOpenCloseTransaction())
            {
                var dim = _dimDb.ListOfType<Polyline>(tr);
                VejKantAnalyzer.CreateOffsetSegments(
                    workingLine, dim, _settings, pipelineSegments);
            }

            var vejKantCrossingData = new List<Line2D>();

            using (var tx = _gkDb.TransactionManager.StartOpenCloseTransaction()) 
            {
                VejKantAnalyzer.AnalyzeIntersectingVejkants(workingLine, _cache);
            }

            return new VejkantAnalysis
            {
                Segments = pipelineSegments,
                GkIntersections = Array.Empty<SegmentHit>(),
                Length = workingLine.StartPoint.DistanceTo(workingLine.EndPoint),
                ChosenSideLabel = null
            };
        }

        public void Commit(VejkantAnalysis result)
        {
            var layers = result.Segments
                .DistinctBy(x => x.LayerName)
                .Select(x => new { Layer = x.LayerName, Color = x.LayerColor });

            using (var tx = _targetDb.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tx.GetObject(_targetDb.LayerTableId, OpenMode.ForRead);
                if (layers.Any(x => !lt.Has(x.Layer)))
                {
                    lt.UpgradeOpen();

                    foreach (var layer in layers.Where(x => !lt.Has(x.Layer)))
                    {
                        var ltr = new LayerTableRecord
                        {
                            Name = layer.Layer,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, layer.Color)
                        };
                        lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }
                }
                tx.Commit();
            }

            using (var tr = _targetDb.TransactionManager.StartTransaction())
            {
                foreach (var seg in result.Segments)
                {
                    var pl = new Polyline();
                    Point3d? current = null;

                    foreach (var prim in seg.Primitives)
                    {
                        if (prim is PipelineLinePrimitiveDomain ls)
                        {
                            if (current == null)
                            {
                                pl.AddVertexAt(pl.NumberOfVertices, new Point2d(ls.Start.X, ls.Start.Y), 0, 0, 0);
                                current = ls.Start;
                            }
                            pl.AddVertexAt(pl.NumberOfVertices, new Point2d(ls.End.X, ls.End.Y), 0, 0, 0);
                            current = ls.End;
                        }
                        else if (prim is PipelineArcPrimitiveDomain arc)
                        {
                            var s = new Point3d(
                                arc.Center.X + arc.Radius * Math.Cos(arc.StartAngle),
                                arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngle), 0);
                            var e = new Point3d(
                                arc.Center.X + arc.Radius * Math.Cos(arc.EndAngle),
                                arc.Center.Y + arc.Radius * Math.Sin(arc.EndAngle), 0);

                            if (current == null)
                            {
                                pl.AddVertexAt(pl.NumberOfVertices, new Point2d(s.X, s.Y), 0, 0, 0);
                                current = s;
                            }

                            var sweep = arc.EndAngle - arc.StartAngle;
                            if (sweep < 0 && arc.IsCCW) sweep += 2 * Math.PI;
                            if (sweep > 0 && !arc.IsCCW) sweep -= 2 * Math.PI;

                            var bulge = Math.Tan(sweep / 4.0);
                            pl.AddVertexAt(pl.NumberOfVertices, new Point2d(e.X, e.Y), bulge, 0, 0);
                            current = e;
                        }
                    }

                    pl.Layer = seg.LayerName;
                    pl.ConstantWidth = seg.Width;
                    pl.AddEntityToDbModelSpace(_targetDb);
                }

                tr.Commit();
            }
        }
    }
}
