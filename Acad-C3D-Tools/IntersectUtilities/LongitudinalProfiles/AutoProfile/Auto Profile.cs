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

using Dreambuild.AutoCAD;

using GroupByCluster;

using IntersectUtilities.DataManager;
using IntersectUtilities.LongitudinalProfiles.AutoProfile;
using IntersectUtilities.NTS;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.UtilsCommon;

using MoreLinq;

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.Utils;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Label = Autodesk.Civil.DatabaseServices.Label;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <command>APCREATE</command>
        /// <summary>
        /// Creates automatic pipe profile for longitudinal sections.
        /// </summary>
        /// <category>Longitudinal Profiles</category>
        [CommandMethod("APCREATE")]
        public void apcreate()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            #region Debug and dev layer
            string devLyr = "AutoProfileTest";
            localDb.CheckOrCreateLayer(devLyr, 1, false);
            #endregion

            #region Delete previous debug entities
            //Delete previous entities on dev layer
            using (Transaction tx2 = localDb.TransactionManager.StartTransaction())
            {
                var ents = localDb.HashSetOfType<Entity>(
                    localDb.TransactionManager.TopTransaction)
                    .Where(x => x.Layer == devLyr);
                foreach (var ent in ents)
                {
                    ent.UpgradeOpen();
                    ent.Erase(true);
                }
                tx2.Commit();
            }
            #endregion

            //Settings
            double DouglasPeukerTolerance = 0.5;

            //Variables for sampling
            double x = 0.0;
            double y = 0.0;

            #region DataManager and FJVDATA
            DataManager.DataManager dm = new DataManager.DataManager(new DataReferencesOptions());
            if (!dm.IsValid()) { dm.Dispose(); return; }
            Database fjvDb = dm.GetForRead("Fremtid");
            using Transaction fjvTx = fjvDb.TransactionManager.StartTransaction();
            PropertySetHelper pshFjv = new(fjvDb);
            #endregion

            using Transaction tx = localDb.TransactionManager.StartTransaction();
            try
            {
                #region Build PipeNetwork
                var als = localDb.HashSetOfType<Alignment>(tx);

                if (als.Count == 0)
                {
                    prdDbg("No Alignments found in the drawing");
                    tx.Abort();
                    return;
                }

                var ents = fjvDb.GetFjvEntities(fjvTx);

                PipelineNetwork pn = new PipelineNetwork();
                pn.CreatePipelineNetwork(ents, als);
                pn.CreateSizeArrays();
                var sizeArrays =
                    pn.GetAllSizeArrays(includeNas: false)
                    .ToDictionary(x => x.Item1, x => x.Item2);

                var entsGroupedByAl = als
                    .Select(al => new
                    {
                        al,
                        entities = ents
                            .Where(x => x is Polyline)
                            .Cast<Polyline>()
                            .Where(p => pshFjv.Pipeline.ReadPropertyString(
                                p, pshFjv.PipelineDef.BelongsToAlignment) == al.Name)
                            .ToList()
                    })
                    .Where(x => x.entities.Any())
                    .ToDictionary(x => x.al, x => x.entities);

                var brs = localDb.ListOfType<BlockReference>(tx);
                var detailingBlockDict = brs
                    .Where(x => x.RealName().EndsWith("_PV"))
                    .OrderBy(x => x.RealName())
                    .ToDictionary(x => x.RealName().Replace("_PV", ""), x => x);

                var pvsDict = localDb
                    .ListOfType<ProfileView>(tx)
                    .ToDictionary(x => x.Name.Replace("_PV", ""));
                #endregion

                #region Select profile to operate on
                var names = als
                    .Select(x => x.Name)
                    .OrderBy(x => x)
                    .Prepend("All");

                string choice = StringGridFormCaller.Call(names, "Vælg alignment:");
                if (choice == null) { tx.Abort(); return; }

                if (choice != "All")
                    als = als.Where(x => x.Name == choice).ToHashSet();
                #endregion

                #region Gather data
                var pplds = new HashSet<AP_PipelineData>();
                foreach (var al in als.OrderBy(x => x.Name))
                {
                    prdDbg($"Processing {al.Name}");
                    System.Windows.Forms.Application.DoEvents();
                    var ppld = new AP_PipelineData(al.Name);

                    #region Get profile view
                    var pvs = al.GetProfileViewIds().Entities<ProfileView>(tx).ToList();
                    if (pvs.Count != 1) throw new SystemException(
                        $"Alignment {al.Name} has more than one profile view!");

                    ProfileView pv = pvs[0];

                    AP_ProfileViewData pvd = new AP_ProfileViewData(pv.Name, pv, ppld);
                    ppld.ProfileView = pvd;
                    #endregion

                    #region Get pipline size array 
                    if (sizeArrays.TryGetValue(al.Name, out var sa)) ppld.SizeArray = sa;
                    else throw new System.Exception($"No pipeline size array found for {al.Name}!");
                    prdDbg(ppld.SizeArray);
                    #endregion

                    #region Build surface related data                    
                    ObjectIdCollection pids = al.GetProfileIds();
                    if (pids.Count == 0) throw new System.Exception($"Alignment {al.Name} has no profiles!");

                    Profile? p = null;
                    foreach (Oid pid in pids)
                    {
                        Profile ptemp = pid.Go<Profile>(tx);
                        if (ptemp.Name.EndsWith("surface_P"))
                        {
                            p = ptemp;
                            break;
                        }
                    }

                    if (p == null) throw new System.Exception($"No surface profile found for {al.Name}!");

                    var spd = new AP_SurfaceProfileData(p.Name, p, ppld);
                    ppld.SurfaceProfile = spd;
                    #endregion

                    #region Gather horizontal arc data
                    var ppl = pn.GetPipeline(al.Name);
                    if (ppl == null) throw new System.Exception($"No pipeline found for {al.Name}!");
                    var gp = entsGroupedByAl[al];
                    foreach (Polyline pl in gp.OrderBy(ppl.GetPolylineMiddleStation))
                    {
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            var st = pl.GetSegmentType(i);
                            if (st == SegmentType.Arc)
                            {
                                var arc = pl.GetArcSegmentAt(i);
                                double[] sts =
                                    [ppl.GetStationAtPoint(arc.StartPoint), ppl.GetStationAtPoint(arc.EndPoint)];                                
                                ppld.HorizontalArcs.Add(new HorizontalArc(sts.Min(), sts.Max(), ppld));
                            }
                        }
                    }
                    ppld.HorizontalArcs = ppld.HorizontalArcs.OrderBy(x => x.StartStation).ToList();
                    #endregion

                    #region Gather and initialize Utility data
                    if (!detailingBlockDict.TryGetValue(al.Name, out var br))
                        throw new System.Exception($"No detailing block found for {al.Name}!");

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
                        prdDbg($"The union operation for {al.Name} resulted in an empty geometry.");
                    }
                    else if (union is Polygon singlePolygon)
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

                    if (envelopes.Count == 0) ppld.Utility = [];
                    else
                    {
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
                            var minX = cs[0].X;
                            var minY = cs[0].Y;
                            var maxX = cs[2].X;
                            var maxY = cs[2].Y;

                            x = (minX + maxX) / 2.0;
                            y = (minY + maxY) / 2.0;

                            pv.FindStationAndElevationAtXY(x, y, ref station, ref elevation);

                            ppld.Utility.Add(new AP_Utility(env, station, ppld));
                        }
                    }

                    ppld.GenerateAvoidanceGeometryForUtilities();
                    ppld.GenerateAvoidancePolygonsForUtilities();
                    #endregion

                    pplds.Add(ppld);
                }
                #endregion                

                foreach (var ppld in pplds.OrderBy(x => x.Name))
                {
                    prdDbg($"Processing {ppld.Name}");
                    prdDbg(ppld.SizeArray);
                    System.Windows.Forms.Application.DoEvents();

                    ppld.BuildUtilityIndex();

                    ppld.SurfaceProfile.OffsetCentrelines.Layer = devLyr;
                    ppld.SurfaceProfile.OffsetCentrelines.AddEntityToDbModelSpace(localDb);

                    //DETERMINE FLOATING STATUS
                    foreach (AP_Utility utility in ppld.Utility)
                    {
                        //DETERMINE FLOATING STATUS
                        utility.TestFloatingStatus(ppld.SurfaceProfile.OffsetCentrelines);

                        var hatch = utility.GetUtilityHatch();
                        hatch.Layer = devLyr;
                        if (utility.IsFloating) hatch.Color = ColorByName("green");
                        hatch.AddEntityToDbModelSpace(localDb);

                        //utility.AvoidanceArc.Layer = devLyr;
                        //utility.AvoidanceArc.AddEntityToDbModelSpace(localDb);

                        //var pphatch = NTSConversion.ConvertNTSPolygonToHatch(utility.AvoidancePolygon);
                        //pphatch.Layer = devLyr;
                        //pphatch.Color = ColorByName("green");
                        //pphatch.AddEntityToDbModelSpace(localDb);

                        //if (utility.HorizontalArcAvoidancePolyline != null)
                        //{
                            //utility.HorizontalArcAvoidancePolyline.Layer = devLyr;
                            //utility.HorizontalArcAvoidancePolyline.AddEntityToDbModelSpace(localDb);

                            //var polyHatch = NTSConversion.ConvertNTSPolygonToHatch(utility.HorizontalArcAvoidancePolygon);
                            //polyHatch.Layer = devLyr;
                            //polyHatch.Color = ColorByName("yellow");
                            //polyHatch.AddEntityToDbModelSpace(localDb);
                        //}
                    }

                    

                    var queryDeepestUnknownNonFloating = ppld.Utility
                        .Where(x => x.IsFloating == false && x.Status == AP_Status.Unknown)
                        .OrderByDescending(x => x.BottomElevation);

                    var queryIntersectingFloating = ppld.Utility
                        .Where(x => 
                        x.IsFloating == true && x.Status == AP_Status.Unknown &&)
                        .OrderByDescending(x => x.BottomElevation);

                    int safetyCounter = 0;
                    while (queryDeepestUnknownNonFloating.FirstOrDefault() != null)
                    {
                        safetyCounter++;



                        if (safetyCounter > 10000)
                        {
                            prdDbg($"Safety counter exceeded {safetyCounter} iterations, breaking loop to prevent infinite loop.");
                            break;
                        }
                    }




                    //ppld.Serialize($"C:\\Temp\\sample_data_{ppld.Name}.json");
                }

                #region Export ppl to json
                //Write the collection to json

                #endregion
            }
            catch (DebugException dex)
            {
                tx.Abort();
                prdDbg(dex);

                if (dex.DebugEntities != null && dex.DebugEntities.Count > 0)
                {
                    using Transaction dtx = localDb.TransactionManager.StartTransaction();
                    //Write debug entities to the dev layer
                    foreach (var ent in dex.DebugEntities)
                    {
                        ent.Layer = devLyr;
                        ent.AddEntityToDbModelSpace(localDb);
                    }
                    dtx.Commit();
                }

                return;
            }
            catch (System.Exception ex)
            {
                tx.Abort();
                prdDbg(ex);
                return;
            }
            finally
            {
                fjvTx.Abort();
                dm.Dispose();
            }
            tx.Commit();

            prdDbg("Done!");
        }

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
                gatherprofileviewdata();
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                return;
            }
            finally
            {
                dm.Dispose();
            }

            prdDbg("Done!");
        }

        //[CommandMethod("APGSPD")]
        //[CommandMethod("APGATHERSURFACEPROFILEDATA")]
        public void gathersurfaceprofiledata()
        {
            //DocumentCollection docCol = Application.DocumentManager;
            //Database localDb = docCol.MdiActiveDocument.Database;

            //using (Transaction tx = localDb.TransactionManager.StartTransaction())
            //{
            //    try
            //    {
            //        var als = localDb.HashSetOfType<Alignment>(tx);

            //        if (als.Count == 0)
            //        {
            //            prdDbg("No Alignments found in the drawing");
            //            tx.Abort();
            //            return;
            //        }

            //        string filePath = Path.Combine(apDataExportPath, "SurfaceProfileData.json");

            //        HashSet<AP_PipelineData> ppls = new HashSet<AP_PipelineData>();

            //        foreach (Alignment al in als.OrderBy(x => x.Name))
            //        {
            //            prdDbg($"Processing {al.Name}");
            //            System.Windows.Forms.Application.DoEvents();

            //            var pids = al.GetProfileIds();
            //            Profile p = null;

            //            foreach (Oid pid in pids)
            //            {
            //                Profile ptemp = pid.Go<Profile>(tx);
            //                if (ptemp.Name.EndsWith("surface_P"))
            //                {
            //                    p = ptemp;
            //                    break;
            //                }
            //            }

            //            if (p == null)
            //            {
            //                prdDbg($"No surface profile found for {al.Name}");
            //                continue;
            //            }

            //            ProfilePVICollection pvis = p.PVIs;

            //            var query = pvis.Select(
            //                pvis => new { pvis.RawStation, pvis.Elevation }).OrderBy(x => x.RawStation);

            //            var ppl = new AP_PipelineData(al.Name);
            //            ppl.SurfaceProfile = new AP_SurfaceProfileData(p.Name);
            //            ppl.SurfaceProfile.ProfilePoints =
            //                query.Select(x => new double[] { x.RawStation, x.Elevation })
            //                .ToArray();
            //            ppls.Add(ppl);
            //        }

            //        //Write the collection to json
            //        var json = JsonSerializer.Serialize(
            //            ppls.OrderBy(x => x.Name),
            //            apJsonOptions);
            //        using var writer = new StreamWriter(filePath, false);
            //        writer.WriteLine(json);

            //        prdDbg("Done!");
            //    }
            //    catch (System.Exception ex)
            //    {
            //        tx.Abort();
            //        prdDbg(ex);
            //        throw;
            //    }
            //    tx.Commit();
            //}
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

                var sizes = pn.GetAllSizeArrays(includeNas: false);

                var ppls = new HashSet<AP_PipelineData>();
                foreach (var size in sizes)
                {
                    var ppl = new AP_PipelineData(size.Item1);
                    ppl.SizeArray = size.Item2;
                    ppls.Add(ppl);
                }

                var json = JsonSerializer.Serialize(
                    ppls.OrderBy(x => x.Name),
                    apJsonOptions);
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

            //DocumentCollection docCol = Application.DocumentManager;
            //Database localDb = docCol.MdiActiveDocument.Database;

            //Database fjvDb = dm.GetForRead("Fremtid");
            //Transaction fjvTx = fjvDb.TransactionManager.StartTransaction();

            //using Transaction tx = localDb.TransactionManager.StartTransaction();

            //PropertySetHelper psh = new(fjvDb);

            //try
            //{
            //    var ents = fjvDb.GetFjvEntities(fjvTx, true, false);
            //    var als = localDb.HashSetOfType<Alignment>(tx);

            //    PipelineNetwork pn = new PipelineNetwork();
            //    pn.CreatePipelineNetwork(ents, als);

            //    var gps = ents
            //        .Where(x => x is Polyline)
            //        .Cast<Polyline>()
            //        .GroupBy(x => psh.Pipeline.ReadPropertyString(
            //            x, psh.PipelineDef.BelongsToAlignment))
            //        .Where(x => als.Select(x => x.Name).Contains(x.Key));

            //    HashSet<AP_PipelineData> ppls = new HashSet<AP_PipelineData>();
            //    foreach (var gp in gps.OrderBy(x => x.Key))
            //    {
            //        prdDbg($"Pipeline: {gp.Key}");

            //        var ppl = pn.GetPipeline(gp.Key);
            //        if (ppl == null) continue;

            //        List<double[]> tuples = new();
            //        foreach (Polyline pl in gp.OrderBy(ppl.GetPolylineMiddleStation))
            //        {
            //            for (int i = 0; i < pl.NumberOfVertices; i++)
            //            {
            //                var st = pl.GetSegmentType(i);
            //                if (st == SegmentType.Arc)
            //                {
            //                    var arc = pl.GetArcSegmentAt(i);
            //                    tuples.Add(
            //                        [ppl.GetStationAtPoint(arc.StartPoint), ppl.GetStationAtPoint(arc.EndPoint)]);
            //                }
            //            }
            //        }
            //        var ap = new AP_PipelineData(gp.Key);
            //        ap.HorizontalArcs = tuples.OrderBy(x => x[0]).ToArray();
            //        ppls.Add(ap);
            //    }

            //    string filePath = Path.Combine(apDataExportPath, "HorizontalArcData.json");

            //    var json = JsonSerializer.Serialize(
            //        ppls.OrderBy(x => x.Name), apJsonOptions);
            //    using var w = new StreamWriter(filePath, false);
            //    w.WriteLine(json);
            //}
            //catch (System.Exception ex)
            //{
            //    tx.Abort();
            //    fjvTx.Abort();
            //    fjvTx.Dispose();
            //    fjvDb.Dispose();
            //    prdDbg(ex);
            //    throw;
            //}
            //tx.Commit();
            //fjvTx.Commit();
            //fjvTx.Dispose();
            //fjvDb.Dispose();
        }

        //[CommandMethod("APGUTD")]
        //[CommandMethod("APGATHERUTILITYDATA")]
        public void gatherutilitydata(DataManager.DataManager dm)
        {
            //prdDbg("Dette skal køres i Længdeprofiler!");

            //DocumentCollection docCol = Application.DocumentManager;
            //Database localDb = docCol.MdiActiveDocument.Database;

            //Database fjvDb = dm.GetForRead("Fremtid");
            //Transaction fjvTx = fjvDb.TransactionManager.StartTransaction();

            //using Transaction tx = localDb.TransactionManager.StartTransaction();

            //try
            //{
            //    var ents = fjvDb.GetFjvEntities(fjvTx, true, false);
            //    var als = localDb.HashSetOfType<Alignment>(tx);

            //    PipelineNetwork pn = new PipelineNetwork();
            //    pn.CreatePipelineNetwork(ents, als);

            //    var brs = localDb.HashSetOfType<BlockReference>(tx);
            //    var query = brs
            //        .Where(x => x.RealName().EndsWith("_PV"))
            //        .OrderBy(x => x.RealName());

            //    var pvs = localDb.ListOfType<ProfileView>(tx).ToDictionary(x => x.Name);

            //    HashSet<AP_PipelineData> ppls = new HashSet<AP_PipelineData>();
            //    foreach (var br in query)
            //    {
            //        string name = br.RealName().Replace("_PV", "");
            //        pvs.TryGetValue(br.RealName(), out ProfileView pv);
            //        if (pv == null) continue;

            //        var ppl = new AP_PipelineData(name);
            //        prdDbg($"Pipeline: {name}");

            //        BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
            //        Matrix3d trf = br.BlockTransform;

            //        var geometryFactory = new GeometryFactory();
            //        var polygons = new List<Polygon>();

            //        foreach (Oid id in btr)
            //        {
            //            if (id.IsDerivedFrom<BlockReference>()) continue;
            //            if (!(id.IsDerivedFrom<Polyline>() ||
            //                id.IsDerivedFrom<Arc>() ||
            //                id.IsDerivedFrom<Circle>())) continue;
            //            var ent = id.Go<Entity>(tx);
            //            var exts = ent.GeometricExtents;
            //            exts.TransformBy(trf);

            //            polygons.Add(
            //                geometryFactory.CreatePolygon(
            //                    [
            //                        new Coordinate(exts.MinPoint.X, exts.MinPoint.Y),
            //                        new Coordinate(exts.MaxPoint.X, exts.MinPoint.Y),
            //                        new Coordinate(exts.MaxPoint.X, exts.MaxPoint.Y),
            //                        new Coordinate(exts.MinPoint.X, exts.MaxPoint.Y),
            //                        new Coordinate(exts.MinPoint.X, exts.MinPoint.Y)
            //                    ]));
            //        }

            //        Geometry union = CascadedPolygonUnion.Union(polygons.ToArray());
            //        List<Geometry> envelopes = new List<Geometry>();

            //        if (union == null || union.IsEmpty)
            //        {
            //            // Handle the case where the union result is null or empty
            //            Console.WriteLine($"The union operation for {name} resulted in an empty geometry.");
            //            continue;
            //        }
            //        if (union is Polygon singlePolygon)
            //        {
            //            envelopes.Add(singlePolygon.Envelope);
            //        }
            //        else if (union is MultiPolygon multiPolygon)
            //        {
            //            // The result is a MultiPolygon
            //            foreach (Polygon poly in multiPolygon.Geometries)
            //            {
            //                envelopes.Add(poly.Envelope);
            //            }
            //        }

            //        List<double[]> doubles = new();
            //        double station = 0.0;
            //        double elevation = 0.0;
            //        foreach (var env in envelopes
            //            .OrderBy(x =>
            //            {
            //                double s = 0.0;
            //                double e = 0.0;
            //                pv.FindStationAndElevationAtXY(x.Coordinates[0].X, x.Coordinates[0].Y, ref s, ref e);
            //                return s;
            //            }))
            //        {
            //            var cs = env.Coordinates;

            //            var d = new double[4];

            //            pv.FindStationAndElevationAtXY(cs[0].X, cs[0].Y, ref station, ref elevation);

            //            d[0] = station;
            //            d[1] = elevation;

            //            pv.FindStationAndElevationAtXY(cs[2].X, cs[2].Y, ref station, ref elevation);

            //            d[2] = station;
            //            d[3] = elevation;

            //            doubles.Add(d);
            //            #region Debug
            //            //Hatch hatch = new Hatch();
            //            //hatch.Normal = new Vector3d(0.0, 0.0, 1.0);
            //            //hatch.Elevation = 0.0;
            //            //hatch.PatternScale = 1.0;
            //            //hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            //            //Oid hatchId = hatch.AddEntityToDbModelSpace(localDb);

            //            //hatch.AppendLoop(HatchLoopTypes.Default,
            //            //    [new Point2d(cs[0].X, cs[0].Y),
            //            //    new Point2d(cs[1].X, cs[1].Y),
            //            //    new Point2d(cs[2].X, cs[2].Y),
            //            //    new Point2d(cs[3].X, cs[3].Y),
            //            //    new Point2d(cs[0].X, cs[0].Y)],
            //            //    [0.0, 0.0, 0.0, 0.0, 0.0]);
            //            //hatch.EvaluateHatch(true); 
            //            #endregion
            //        }

            //        ppl.Utility = doubles.ToArray();
            //        ppls.Add(ppl);
            //    }
            //    var json = JsonSerializer.Serialize(
            //        ppls.OrderBy(x => x.Name), apJsonOptions);

            //    string filePath = Path.Combine(apDataExportPath, "UtilityData.json");
            //    using var w = new StreamWriter(filePath, false);

            //    w.WriteLine(json);
            //}
            //catch (System.Exception ex)
            //{
            //    tx.Abort();
            //    fjvTx.Abort();
            //    fjvTx.Dispose();
            //    fjvDb.Dispose();
            //    prdDbg(ex);
            //    throw;
            //}
            //tx.Commit();
            //fjvTx.Commit();
            //fjvTx.Dispose();
            ////fjvDb.Dispose();
        }

        public void gatherprofileviewdata()
        {
            //DocumentCollection docCol = Application.DocumentManager;
            //Database localDb = docCol.MdiActiveDocument.Database;

            //using (Transaction tx = localDb.TransactionManager.StartTransaction())
            //{
            //    try
            //    {
            //        var als = localDb.HashSetOfType<Alignment>(tx);

            //        if (als.Count == 0)
            //        {
            //            prdDbg("No Alignments found in the drawing");
            //            tx.Abort();
            //            return;
            //        }

            //        string filePath = Path.Combine(apDataExportPath, "ProfileViewData.json");

            //        HashSet<AP_PipelineData> ppls = new HashSet<AP_PipelineData>();

            //        foreach (Alignment al in als.OrderBy(x => x.Name))
            //        {
            //            prdDbg($"Processing {al.Name}");
            //            System.Windows.Forms.Application.DoEvents();

            //            var pvids = al.GetProfileViewIds();
            //            Oid pvid = Oid.Null;
            //            foreach (Oid item in pvids) pvid = item;

            //            if (pvid == Oid.Null)
            //            {
            //                prdDbg($"No profile view found for {al.Name}");
            //                continue;
            //            }

            //            ProfileView pv = pvid.Go<ProfileView>(tx);

            //            if (pv == null)
            //            {
            //                prdDbg($"No profile view found for {al.Name}");
            //                continue;
            //            }

            //            var ppl = new AP_PipelineData(al.Name);
            //            var pvd = new AP_ProfileViewData(pv.Name);
            //            pvd.Origin = [pv.Location.X, pv.Location.Y];
            //            pvd.ElevationAtOrigin = pv.ElevationMin;
            //            ppl.ProfileView = pvd;

            //            ppls.Add(ppl);
            //        }

            //        //Write the collection to json
            //        var json = JsonSerializer.Serialize(
            //            ppls.OrderBy(x => x.Name),
            //            apJsonOptions);
            //        using var writer = new StreamWriter(filePath, false);
            //        writer.WriteLine(json);
            //    }
            //    catch (System.Exception ex)
            //    {
            //        tx.Abort();
            //        prdDbg(ex);
            //        throw;
            //    }
            //    tx.Commit();
            //}
        }
    }
}