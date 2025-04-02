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
using System.Text.Json;
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
using IntersectUtilities.LongitudinalProfiles;
using IntersectUtilities.DataManager;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        private static JsonSerializerOptions apJsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        private static string apDataExportPath = @"c:\Temp\AP\";

        /// <command>APDATAEXPORT</command>
        /// <summary>
        /// Data export for AutoProfile. Exports data for surface profiles, pipeline sizes, horizontal arcs and utility data.
        /// Json files are written to C:\Temp\AP\.
        /// </summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("APDATAEXPORT")]
        public void apdataexport()
        {
            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
            if (!dm.IsValid()) { dm.Dispose(); return; }

            Directory.CreateDirectory(apDataExportPath);

            try
            {
                gathersurfaceprofiledata();
                gatherpipelinedata(dm);
                gatherhorizontalarcdata(dm);
                gatherutilitydata(dm);
            }
            catch(System.Exception ex)
            {                
                prdDbg(ex);
                return;
            }
            finally
            {
                dm.Dispose();
            }
        }

        //[CommandMethod("APGSPD")]
        //[CommandMethod("APGATHERSURFACEPROFILEDATA")]
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

                    string filePath = Path.Combine(apDataExportPath, "SurfaceProfileData.json");

                    HashSet<AP_PipelineData> ppls = new HashSet<AP_PipelineData>();

                    foreach (Alignment al in als.OrderBy(x => x.Name))
                    {
                        prdDbg($"Processing {al.Name}");
                        System.Windows.Forms.Application.DoEvents();

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

                        var ppl = new AP_PipelineData(al.Name);
                        ppl.SurfaceProfile = new AP_SurfaceProfileData(p.Name);
                        ppl.SurfaceProfile.SurfaceProfile =
                            query.Select(x => new double[] { x.RawStation, x.Elevation })
                            .ToArray();
                        ppls.Add(ppl);
                    }

                    //Write the collection to json
                    var json = JsonSerializer.Serialize(ppls, apJsonOptions);
                    using var writer = new StreamWriter(filePath, false);
                    writer.WriteLine(json);

                    prdDbg("Done!");
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    throw;
                }
                tx.Commit();
            }
        }

        //[CommandMethod("APGPLD")]
        //[CommandMethod("APGATHERPIPELINEDATA")]
        public void gatherpipelinedata(DataManager.DataManager dm)
        {
            prdDbg("Dette skal køres i Længdeprofiler!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

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

                string filePath = Path.Combine(apDataExportPath, "PipelineSizeData.json");
                using var w = new StreamWriter(filePath, false);

                var sizes = pn.GetAllSizeArrays();

                var ppls = new HashSet<AP_PipelineData>();
                foreach (var size in sizes)
                {
                    var ppl = new AP_PipelineData(size.Item1);
                    ppl.PipelineSizes = size.Item2;
                    ppls.Add(ppl);
                }

                var json = JsonSerializer.Serialize(ppls, apJsonOptions);
                w.WriteLine(json);
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                fjvTx.Abort();
                fjvTx.Dispose();
                fjvDb.Dispose();
                prdDbg(ex);
                throw;
            }
            tx.Commit();
            fjvTx.Commit();
            fjvTx.Dispose();
            //fjvDb.Dispose();
        }

        //[CommandMethod("APGHAD")]
        //[CommandMethod("APGATHERHORIZONTALARCDATA")]
        public void gatherhorizontalarcdata(DataManager.DataManager dm)
        {
            prdDbg("Dette skal køres i Længdeprofiler!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

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

                HashSet<AP_PipelineData> ppls = new HashSet<AP_PipelineData>();
                foreach (var gp in gps.OrderBy(x => x.Key))
                {
                    prdDbg($"Pipeline: {gp.Key}");

                    var ppl = pn.GetPipeline(gp.Key);
                    if (ppl == null) continue;

                    List<double[]> tuples = new();
                    foreach (Polyline pl in gp.OrderBy(ppl.GetPolylineMiddleStation))
                    {
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            var st = pl.GetSegmentType(i);
                            if (st == SegmentType.Arc)
                            {
                                var arc = pl.GetArcSegmentAt(i);
                                tuples.Add(
                                    [ppl.GetStationAtPoint(arc.StartPoint), ppl.GetStationAtPoint(arc.EndPoint)]);
                            }
                        }
                    }
                    var ap = new AP_PipelineData(gp.Key);
                    ap.HorizontalArcs = tuples.OrderBy(x => x[0]).ToArray();
                    ppls.Add(ap);
                }

                string filePath = Path.Combine(apDataExportPath, "HorizontalArcData.json");

                var json = JsonSerializer.Serialize(ppls, apJsonOptions);
                using var w = new StreamWriter(filePath, false);
                w.WriteLine(json);
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                fjvTx.Abort();
                fjvTx.Dispose();
                fjvDb.Dispose();
                prdDbg(ex);
                throw;
            }
            tx.Commit();
            fjvTx.Commit();
            fjvTx.Dispose();
            //fjvDb.Dispose();
        }

        //[CommandMethod("APGUTD")]
        //[CommandMethod("APGATHERUTILITYDATA")]
        public void gatherutilitydata(DataManager.DataManager dm)
        {
            prdDbg("Dette skal køres i Længdeprofiler!");

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

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

                HashSet<AP_PipelineData> ppls = new HashSet<AP_PipelineData>();
                foreach (var br in query)
                {
                    string name = br.RealName().Replace("_PV", "");
                    pvs.TryGetValue(br.RealName(), out ProfileView pv);
                    if (pv == null) continue;

                    var ppl = new AP_PipelineData(name);
                    prdDbg($"Pipeline: {name}");

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

                    List<double[]> doubles = new();
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

                        var d = new double[4];

                        pv.FindStationAndElevationAtXY(cs[0].X, cs[0].Y, ref station, ref elevation);

                        d[0] = station;
                        d[1] = elevation;

                        pv.FindStationAndElevationAtXY(cs[2].X, cs[2].Y, ref station, ref elevation);

                        d[2] = station;
                        d[3] = elevation;

                        doubles.Add(d);
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

                    ppl.Utility = doubles.ToArray();
                    ppls.Add(ppl);
                }
                var json = JsonSerializer.Serialize(ppls, apJsonOptions);

                string filePath = Path.Combine(apDataExportPath, "UtilityData.json");
                using var w = new StreamWriter(filePath, false);

                w.WriteLine(json);
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                fjvTx.Abort();
                fjvTx.Dispose();
                fjvDb.Dispose();
                prdDbg(ex);
                throw;
            }
            tx.Commit();
            fjvTx.Commit();
            fjvTx.Dispose();
            //fjvDb.Dispose();
        }
    }
}