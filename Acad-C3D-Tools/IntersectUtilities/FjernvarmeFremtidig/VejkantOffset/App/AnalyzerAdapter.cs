using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;
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

        public VejkantAnalyzer(Database dimDb, Database gkDb, Database targetDb, VejkantOffsetSettings settings)
        {
            _dimDb = dimDb;
            _gkDb = gkDb;
            _targetDb = targetDb;
            _settings = settings;
        }

        public VejkantAnalysis Analyze(Line workingLine)
        {
            var pipelineSegments = new List<PipelineSegment>();

            using (var tr = _dimDb.TransactionManager.StartOpenCloseTransaction())
            {
                var dim = _dimDb.ListOfType<Polyline>(tr);
                VejKantAnalyzerOffsetter.CreateOffsetSegments(workingLine, dim, _settings, pipelineSegments);
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
