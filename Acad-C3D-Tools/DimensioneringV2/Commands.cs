using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using dbg = IntersectUtilities.UtilsCommon.Utils.DebugHelper;
using System.Windows.Forms;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using cv = DimensioneringV2.CommonVariables;
using Autodesk.AutoCAD.Geometry;
using DimensioneringV2.GraphModelRoads;
using DimensioneringV2.Geometry;
using IntersectUtilities;
using Autodesk.AutoCAD.Colors;
using DimensioneringV2.GraphFeatures;
using NetTopologySuite.Features;
using NetTopologySuite.IO.Esri;
using System.IO;
using DimensioneringV2.UI;
using Dreambuild.AutoCAD;

[assembly: CommandClass(typeof(DimensioneringV2.Commands))]

namespace DimensioneringV2
{
    public class Commands : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            doc.Editor.WriteMessage("\nVelkommen til Dimensionering v2.0!");
#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
                new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
            //Add your termination code here
        } 
        #endregion

        private static double tol = DimensioneringV2.Tolerance.Default;

        [CommandMethod("DIM2INTERSECTVEJMIDTE")]
        public void dim2intersectvejmidte()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToArray();

                    for (int i = 0; i < pls.Length; i++)
                    {
                        for (int j = i + 1; j < pls.Length; j++)
                        {
                            var pl1 = pls[i];
                            var pl2 = pls[j];

                            using Point3dCollection pts = new Point3dCollection();
                            pl1.IntersectWith(pl2, Intersect.OnBothOperands, new Plane(),
                                pts, IntPtr.Zero, IntPtr.Zero);

                            foreach (Point3d pt in pts)
                            {
                                //Workaround for AutoCAD bug?
                                //I was getting more points than I should have
                                //Test to see if the point lies on BOTH polylines
                                //IntersectUtilities.UtilsCommon.Utils.DebugHelper.CreateDebugLine(pt, ColorByName("red"));
                                if (!pt.IsOnCurve(pl1, tol) || !pt.IsOnCurve(pl2, tol)) continue;
                                AddVertexIfMissing(pl1, pt);
                                AddVertexIfMissing(pl2, pt);
                            }
                        }
                    }

                    void AddVertexIfMissing(Polyline pl, Point3d pt)
                    {
                        pt = pl.GetClosestPointTo(pt, false);
                        double param = pl.GetParameterAtPoint(pt);
                        int index = (int)param;
                        if ((param - index) > tol)
                        {
                            pl.CheckOrOpenForWrite();
                            pl.AddVertexAt(index + 1, pt.To2d(), 0, 0, 0);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        [CommandMethod("DIM2MARKENDPOINTS")]
        public void dim2markendpoints()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToArray();

                    for (int i = 0; i < pls.Length; i++)
                    {
                        for (int j = i + 1; j < pls.Length; j++)
                        {
                            var pl1 = pls[i];
                            var pl2 = pls[j];

                            using Point3dCollection pts = new Point3dCollection();
                            pl1.IntersectWith(pl2, Intersect.OnBothOperands, new Plane(),
                                pts, IntPtr.Zero, IntPtr.Zero);

                            foreach (Point3d pt in pts)
                            {
                                //Workaround for AutoCAD bug?
                                //I was getting more points than I should have
                                //Test to see if the point lies on BOTH polylines
                                //IntersectUtilities.UtilsCommon.Utils.DebugHelper.CreateDebugLine(pt, ColorByName("red"));
                                if (!pt.IsOnCurve(pl1, tol) || !pt.IsOnCurve(pl2, tol)) continue;
                                AddVertexIfMissing(pl1, pt);
                                AddVertexIfMissing(pl2, pt);
                            }
                        }
                    }

                    void AddVertexIfMissing(Polyline pl, Point3d pt)
                    {
                        pt = pl.GetClosestPointTo(pt, false);
                        double param = pl.GetParameterAtPoint(pt);
                        int index = (int)param;
                        if ((param - index) > tol)
                        {
                            pl.CheckOrOpenForWrite();
                            pl.AddVertexAt(index + 1, pt.To2d(), 0, 0, 0);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    foreach (var br in localDb.GetBlockReferenceByName(cv.BlockEndPointName))
                    {
                        br.CheckOrOpenForWrite();
                        br.Erase(true);
                    }

                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    bool isOk = graph.BuildGraph(pls, basePoints);

                    if (!isOk) { tx.Commit(); return; }

                    localDb.CheckOrCreateLayer(cv.LayerEndPoint);

                    foreach (var pt in graph.GetLeafNodePoints())
                    {
                        var br = localDb.CreateBlockWithAttributes(cv.BlockEndPointName, pt.To3d());
                        br.CheckOrOpenForWrite();
                        br.Layer = cv.LayerEndPoint;
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        [CommandMethod("DIM2TESTNAMING")]
        public void dim2testnaming()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints);

                    localDb.CheckOrCreateLayer(cv.LayerNumbering);

                    foreach (var data in graph.GetSegmentsNumbering())
                    {
                        DBText dBText = new DBText();
                        //dBText.Justify = AttachmentPoint.MiddleCenter;
                        //dBText.VerticalMode = TextVerticalMode.TextVerticalMid;
                        //dBText.HorizontalMode = TextHorizontalMode.TextCenter;
                        dBText.TextString = data.Text;
                        dBText.Position = data.Point;
                        dBText.Layer = cv.LayerNumbering;
                        dBText.AddEntityToDbModelSpace(localDb);
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        [CommandMethod("DIM2TESTROOTNODE")]
        public void dim2testrootnode()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints);

                    foreach (var component in graph.ConnectedComponents)
                    {
                        //Utils.DebugHelper.CreateDebugLine(component.RootNode.GetMidpoint().To3d(), ColorByName("red"));
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        [CommandMethod("DIM2DRAWBUILDINGSEGMENTS")]
        public void dim2drawbuildingsegments()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Delete previous connection lines
                    var lines = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == cv.LayerConnectionLine);
                    foreach (var line in lines)
                    {
                        line.CheckOrOpenForWrite();
                        line.Erase(true);
                    }
                    #endregion

                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => cv.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                    var noCrossLines = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == cv.LayerNoCross);

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints, brs, noCrossLines);

                    localDb.CheckOrCreateLayer(cv.LayerConnectionLine, cv.ConnectionLineColor);

                    foreach (var segment in graph.GetBuildingConnectionSegments())
                    {
                        Line line = new Line(segment.StartPoint.To3d(), segment.EndPoint.To3d());
                        line.Layer = cv.LayerConnectionLine;
                        line.AddEntityToDbModelSpace(localDb);
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        [CommandMethod("DIM2TESTFEATURECREATION")]
        public void dim2testfeaturecreation()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => cv.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                    var noCrossLines = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == cv.LayerNoCross);

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints, brs, noCrossLines);

                    var features = GraphTranslator.TranslateGraph(graph);

                    if (features != null)
                    {
                        FeatureCollection fc = new FeatureCollection(features.SelectMany(x => x));
                        NetTopologySuite.IO.GeoJsonWriter writer = new NetTopologySuite.IO.GeoJsonWriter();
                        string json = writer.Write(fc);
                        using (var sw = new StreamWriter("C:\\Temp\\testfc.geojson"))
                        {
                            sw.Write(json);
                        }


                        //Shapefile.WriteAllFeatures(fc, "C:\\Temp\\testfc");

                        ////Create the projection file
                        //using (var sw = new StreamWriter("C:\\Temp\\testfc.prj"))
                        //{
                        //    //sw.Write(ProjNet.CoordinateSystems.ProjectedCoordinateSystem.WGS84_UTM(32, true));
                        //    sw.Write(@"PROJCS[""ETRS_1989_UTM_Zone_32N"",GEOGCS[""GCS_ETRS_1989"",DATUM[""D_ETRS_1989"",SPHEROID[""GRS_1980"",6378137.0,298.257222101]],PRIMEM[""Greenwich"",0.0],UNIT[""Degree"",0.0174532925199433]],PROJECTION[""Transverse_Mercator""],PARAMETER[""False_Easting"",500000.0],PARAMETER[""False_Northing"",0.0],PARAMETER[""Central_Meridian"",9.0],PARAMETER[""Scale_Factor"",0.9996],PARAMETER[""Latitude_Of_Origin"",0.0],UNIT[""Meter"",1.0]]");
                        //}
                    }

                }
                catch (System.Exception ex)
                {
                    if (ex is ArgumentException argex)
                    {
                        if (argex.Message.Contains("DBG"))
                        {
                            prdDbg(argex.Message);
                            tx.Commit();
                            return;
                        }
                    }

                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }

        static CustomPaletteSet paletteSet;

        [CommandMethod("DIM2MAP")]
        public static void dim2map()
        {
            if (paletteSet == null) paletteSet = new CustomPaletteSet();
            paletteSet.Visible = true;
        }

        [CommandMethod("DIM2MAPCOLLECTFEATURES")]
        public void dim2mapcollectfeatures()
        {
            DocumentCollection docCol = AcApp.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            if (paletteSet == null) { prdDbg("This command is run from DIM2MAP"); return; }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var pls = localDb.HashSetOfType<Polyline>(tx)
                        .Where(x => x.Layer == cv.LayerVejmidteTændt)
                        .ToList();

                    var basePoints = localDb.HashSetOfType<BlockReference>(tx)
                        .Where(x => x.RealName() == cv.BlockSupplyPointName)
                        .Select(x => new Point2D(x.Position.X, x.Position.Y))
                        .ToList();

                    var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => cv.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")));

                    var noCrossLines = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == cv.LayerNoCross);

                    var graph = new DimensioneringV2.GraphModelRoads.Graph();
                    graph.BuildGraph(pls, basePoints, brs, noCrossLines);

                    var features = GraphTranslator.TranslateGraph(graph);
                    
                    if (features != null)
                    {
                        Services.DataService.Instance.UpdateFeatures(features);
                    }
                    //else fall through and if any errors where marked with debug lines
                    //they will be shown in the drawing
                }
                catch (System.Exception ex)
                {
                    if (ex is ArgumentException argex)
                    {
                        if (argex.Message.Contains("DBG"))
                        {
                            prdDbg(argex.Message);
                            tx.Commit();
                            return;
                        }
                    }

                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }

            prdDbg("Finished!");
        }
    }
}