﻿using Autodesk.AutoCAD.ApplicationServices;
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
using System.Windows.Forms;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using cv = Dimensionering.DimensioneringV2.CommonVariables;
using Autodesk.AutoCAD.Geometry;
using Dimensionering.DimensioneringV2.GraphModelRoads;
using Dimensionering.DimensioneringV2.Geometry;
using IntersectUtilities;

namespace Dimensionering
{
    public partial class DimensioneringExtension
    {
        private static double tol = DimensioneringV2.Tolerance.Default;

        [CommandMethod("DIM2INTERSECTVEJMIDTE")]
        public void dim2intersectvejmidte()
        {
            DocumentCollection docCol = Application.DocumentManager;
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
                            pl.AddVertexAt(index + 1, pt.To2D(), 0, 0, 0);
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
            DocumentCollection docCol = Application.DocumentManager;
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
                            pl.AddVertexAt(index + 1, pt.To2D(), 0, 0, 0);
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
            DocumentCollection docCol = Application.DocumentManager;
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
            DocumentCollection docCol = Application.DocumentManager;
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
            DocumentCollection docCol = Application.DocumentManager;
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
    }
}
