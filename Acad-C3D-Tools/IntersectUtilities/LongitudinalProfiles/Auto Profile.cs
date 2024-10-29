using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.DataShortcuts;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using Dreambuild.AutoCAD;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;
using static IntersectUtilities.UtilsCommon.Utils;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using IntersectUtilities.PipelineNetworkSystem;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        [CommandMethod("APGSPD")]
        [CommandMethod("APGATHERSURFACEPROFILEDATA")]
        public void gathersurfaceprofiledata()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var als = localDb.HashSetOfType<Alignment>(tx);

                    if (als.Count == 0)
                    {
                        prdDbg("No Alignments found in the drawing");
                        tx.Abort();
                        return;
                    }

                    string filePath = @"c:\Temp\SurfaceProfileData.txt";
                    using var w = new StreamWriter(filePath, false);

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        w.WriteLine($"Alignment: {al.Name}");

                        var pids = al.GetProfileIds();
                        Profile p = null;

                        foreach (Oid pid in pids)
                        {
                            Profile ptemp = pid.Go<Profile>(tx);
                            if (ptemp.Name.EndsWith("surface_P"))
                            {
                                p = ptemp;
                                break;
                            }
                        }

                        if (p == null)
                        {
                            prdDbg($"No surface profile found for {al.Name}");
                            continue;
                        }

                        ProfilePVICollection pvis = p.PVIs;

                        var query = pvis.Select(
                            pvis => new { pvis.RawStation, pvis.Elevation }).OrderBy(x => x.RawStation);

                        w.WriteLine(string.Join(";", query.Select(x => x.RawStation)));
                        w.WriteLine(string.Join(";", query.Select(x => x.Elevation)));
                        w.WriteLine();
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("APGPLD")]
        [CommandMethod("APGATHERPIPELINEDATA")]
        public void gatherpipelinedata()
        {
            prdDbg("Dette skal køres i Længdeprofiler!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
            Database fjvDb = dm.GetForRead("Fremtid");
            Transaction fjvTx = fjvDb.TransactionManager.StartTransaction();

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                var ents = fjvDb.GetFjvEntities(fjvTx, true, false);
                var als = localDb.HashSetOfType<Alignment>(tx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreateSizeArrays();

                var sb = pn.PrintSizeArrays();

                string filePath = @"c:\Temp\PipelineSizeData.txt";
                using var w = new StreamWriter(filePath, false);

                w.WriteLine(sb.ToString());
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                fjvTx.Abort();
                fjvTx.Dispose();
                fjvDb.Dispose();
                prdDbg(ex);
                return;
            }
            tx.Commit();
            fjvTx.Commit();
            fjvTx.Dispose();
            fjvDb.Dispose();
        }

        [CommandMethod("APGHAD")]
        [CommandMethod("APGATHERHORIZONTALARCDATA")]
        public void gatherhorizontalarcdata()
        {
            prdDbg("Dette skal køres i Længdeprofiler!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
            Database fjvDb = dm.GetForRead("Fremtid");
            Transaction fjvTx = fjvDb.TransactionManager.StartTransaction();

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            PropertySetHelper psh = new(fjvDb);

            try
            {
                var ents = fjvDb.GetFjvEntities(fjvTx, true, false);
                var als = localDb.HashSetOfType<Alignment>(tx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);

                var gps = ents
                    .Where(x => x is Polyline)
                    .Cast<Polyline>()
                    .GroupBy(x => psh.Pipeline.ReadPropertyString(
                        x, psh.PipelineDef.BelongsToAlignment));

                var sb = new StringBuilder();
                foreach (var gp in gps.OrderBy(x => x.Key))
                {
                    sb.AppendLine($"Pipeline: {gp.Key}");

                    var ppl = pn.GetPipeline(gp.Key);
                    if (ppl == null) continue;

                    int idx = 0;
                    foreach (Polyline pl in gp.OrderBy(ppl.GetPolylineMiddleStation))
                    {
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            var st = pl.GetSegmentType(i);
                            if (st == SegmentType.Arc)
                            {
                                idx++;
                                var arc = pl.GetArcSegmentAt(i);
                                sb.AppendLine($"{idx} " +
                                    $"S:{ppl.GetStationAtPoint(arc.StartPoint).ToString("F4")} " +
                                    $"E:{ppl.GetStationAtPoint(arc.EndPoint).ToString("F4")}");
                            }
                        }
                    }

                    sb.AppendLine();
                }

                string filePath = @"c:\Temp\HorizontalArcData.txt";
                using var w = new StreamWriter(filePath, false);

                w.WriteLine(sb.ToString());
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                fjvTx.Abort();
                fjvTx.Dispose();
                fjvDb.Dispose();
                prdDbg(ex);
                return;
            }
            tx.Commit();
            fjvTx.Commit();
            fjvTx.Dispose();
            fjvDb.Dispose();
        }

        [CommandMethod("APGUTD")]
        [CommandMethod("APGATHERUTILITYDATA")]
        public void gatherutilitydata()
        {
            prdDbg("Dette skal køres i Længdeprofiler!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
            Database fjvDb = dm.GetForRead("Fremtid");
            Transaction fjvTx = fjvDb.TransactionManager.StartTransaction();

            using Transaction tx = localDb.TransactionManager.StartTransaction();

            try
            {
                var ents = fjvDb.GetFjvEntities(fjvTx, true, false);
                var als = localDb.HashSetOfType<Alignment>(tx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                
                var brs = localDb.HashSetOfType<BlockReference>(tx);
                var query = brs
                    .Where(x => x.RealName().EndsWith("_PV"))
                    .OrderBy(x => x.RealName());

                var pvs = localDb.ListOfType<ProfileView>(tx).ToDictionary(x => x.Name);

                var sb = new StringBuilder();
                foreach (var br in query)
                {
                    int idx = 0;
                    string name = br.RealName().Replace("_PV", "");
                    pvs.TryGetValue(br.RealName(), out ProfileView pv);
                    if (pv == null) continue;

                    sb.AppendLine($"Pipeline: {name}");
                    BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                    Matrix3d trf = br.BlockTransform;

                    var geometryFactory = new GeometryFactory();
                    var polygons = new List<Polygon>();

                    foreach (Oid id in btr)
                    {
                        if (id.IsDerivedFrom<BlockReference>()) continue;
                        if (!(id.IsDerivedFrom<Polyline>() ||
                            id.IsDerivedFrom<Arc>() ||
                            id.IsDerivedFrom<Circle>())) continue;
                        var ent = id.Go<Entity>(tx);
                        var exts = ent.GeometricExtents;
                        exts.TransformBy(trf);

                        polygons.Add(
                            geometryFactory.CreatePolygon(
                                [
                                    new Coordinate(exts.MinPoint.X, exts.MinPoint.Y),
                                    new Coordinate(exts.MaxPoint.X, exts.MinPoint.Y),
                                    new Coordinate(exts.MaxPoint.X, exts.MaxPoint.Y),
                                    new Coordinate(exts.MinPoint.X, exts.MaxPoint.Y),
                                    new Coordinate(exts.MinPoint.X, exts.MinPoint.Y)
                                ]));
                    }

                    Geometry union = CascadedPolygonUnion.Union(polygons.ToArray());
                    List<Geometry> envelopes = new List<Geometry>();

                    if (union == null || union.IsEmpty)
                    {
                        // Handle the case where the union result is null or empty
                        Console.WriteLine($"The union operation for {name} resulted in an empty geometry.");
                        continue;
                    }
                    if (union is Polygon singlePolygon)
                    {
                        envelopes.Add(singlePolygon.Envelope);
                    }
                    else if (union is MultiPolygon multiPolygon)
                    {
                        // The result is a MultiPolygon
                        foreach (Polygon poly in multiPolygon.Geometries)
                        {
                            envelopes.Add(poly.Envelope);
                        }
                    }

                    double station = 0.0;
                    double elevation = 0.0;
                    foreach (var env in envelopes
                        .OrderBy(x =>
                        {
                            double s = 0.0;
                            double e = 0.0;
                            pv.FindStationAndElevationAtXY(x.Coordinates[0].X, x.Coordinates[0].Y, ref s, ref e);
                            return s;
                        }))
                    {
                        var cs = env.Coordinates;
                        sb.AppendLine();
                        sb.Append(++idx + ": ");

                        pv.FindStationAndElevationAtXY(cs[0].X, cs[0].Y, ref station, ref elevation);
                        sb.Append(
                            $"S1: {station.ToString("F4")} " +
                            $"E1: {elevation.ToString("F4")} ");

                        pv.FindStationAndElevationAtXY(cs[2].X, cs[2].Y, ref station, ref elevation);
                        sb.Append(
                            $"S2: {station.ToString("F4")} " +
                            $"E2: {elevation.ToString("F4")}");


                        #region Debug
                        //Hatch hatch = new Hatch();
                        //hatch.Normal = new Vector3d(0.0, 0.0, 1.0);
                        //hatch.Elevation = 0.0;
                        //hatch.PatternScale = 1.0;
                        //hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                        //Oid hatchId = hatch.AddEntityToDbModelSpace(localDb);

                        //hatch.AppendLoop(HatchLoopTypes.Default,
                        //    [new Point2d(cs[0].X, cs[0].Y),
                        //    new Point2d(cs[1].X, cs[1].Y),
                        //    new Point2d(cs[2].X, cs[2].Y),
                        //    new Point2d(cs[3].X, cs[3].Y),
                        //    new Point2d(cs[0].X, cs[0].Y)],
                        //    [0.0, 0.0, 0.0, 0.0, 0.0]);
                        //hatch.EvaluateHatch(true); 
                        #endregion
                    }

                    sb.AppendLine();
                    sb.AppendLine();
                }

                string filePath = @"c:\Temp\UtilityData.txt";
                using var w = new StreamWriter(filePath, false);

                w.WriteLine(sb.ToString());
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                fjvTx.Abort();
                fjvTx.Dispose();
                fjvDb.Dispose();
                prdDbg(ex);
                return;
            }
            tx.Commit();
            fjvTx.Commit();
            fjvTx.Dispose();
            fjvDb.Dispose();
        }
    }
}