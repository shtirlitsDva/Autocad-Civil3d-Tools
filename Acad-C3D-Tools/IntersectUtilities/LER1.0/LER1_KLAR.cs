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
using static IntersectUtilities.PipeSchedule;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

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
using System.Windows.Documents;
using System.Windows.Media.Media3D;

namespace IntersectUtilities
{
    public partial class Intersect
    {

        [CommandMethod("KLARCREATELAYERS")]
        public void klarcreatelayers()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var plines = localDb.ListOfType<Polyline>(tx)
                        .Where(x => x.Layer == "DDG_ledning")
                        .ToHashSet();

                    string layerName = "KLAR_Spildevand";

                    localDb.CheckOrCreateLayer(layerName);

                    foreach (var pline in plines)
                    {
                        //string system =
                        //    PropertySetManager.ReadNonDefinedPropertySetString(
                        //        pline, "DDG_ledning", "system");

                        pline.CheckOrOpenForWrite();
                        pline.Layer = layerName;
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// Used to move 3d polylines of Novafos data to elevation points
        /// </summary>
        [CommandMethod("KLARCREATE3D")]
        public void klarcreate3d()
        {
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            //Slope in pro mille
            double slope = 20;
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Prepare list to hold processed lines.
            HashSet<Polyline3d> readyLines = new HashSet<Polyline3d>();
            //List with one of nodes missing
            HashSet<(Polyline3d line, string end)> linesWithOneMissingNode =
                new HashSet<(Polyline3d line, string end)>();
            //For use for secondary interpolation for lines missing one or both end nodes.
            HashSet<Polyline3d> linesWithMissingBothNodes = new HashSet<Polyline3d>();

            //Data
            string propSetNamePipes = "DDG_ledning";
            string propSetNameNodes = "DDG_knude";

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d = localDb
                        .ListOfType<Polyline3d>(tx)
                        .Where(x => PropertySetManager.IsPropertySetAttached(x, propSetNamePipes))
                        .ToHashSet();
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    #region Points
                    //HashSet<DBPoint> points = localDb.ListOfType<DBPoint>(tx)
                    //    .Where(x => PropertySetManager.IsPropertySetAttached(x, propSetNameNodes))
                    //    .ToHashSet();
                    //editor.WriteMessage($"\nNr. of local points: {points.Count}");

                    //var pointLookup = points
                    //    .ToLookup(x => PropertySetManager.ReadNonDefinedPropertySetString(
                    //        x, propSetNameNodes, "Knudenavn"));
                    #endregion

                    #region Poly3ds with knudepunkter at ends
                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);
                        int endIdx = vertices.Length - 1;

                        double startElevation =
                            PropertySetManager.ReadNonDefinedPropertySetDouble(
                                pline3d, propSetNamePipes, "fra_kote");
                        double endElevation =
                            PropertySetManager.ReadNonDefinedPropertySetDouble(
                                pline3d, propSetNamePipes, "til_kote");

                        //Case where both elevations are found
                        if (!startElevation.Equalz(0.0, 0.0001) && !endElevation.Equalz(0.0, 0.0001))
                        {
                            //Add to interpolated listv
                            readyLines.Add(pline3d);

                            //Start match
                            vertices[0].CheckOrOpenForWrite();
                            vertices[0].UpdateElevationZ(startElevation);
                            vertices[endIdx].CheckOrOpenForWrite();
                            vertices[endIdx].UpdateElevationZ(endElevation);

                            //Interpolate if pline has intermediate vertici
                            if (vertices.Length > 2)
                            {
                                double AB = pline3d.GetHorizontalLength(tx);
                                double m = (endElevation - startElevation) / AB;
                                double b = endElevation - m * AB;

                                //Skip first and last points
                                for (int i = 1; i < endIdx; i++)
                                {
                                    double PB = pline3d.GetHorizontalLengthBetweenIdxs(0, i);
                                    double newElevation = m * PB + b;
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].UpdateElevationZ(newElevation);
                                }
                            }
                        }
                        else if (!startElevation.Equalz(0.0, 0.0001) || !endElevation.Equalz(0.0, 0.0001))
                        {
                            if (!startElevation.Equalz(0.0, 0.0001)) linesWithOneMissingNode.Add((pline3d, "start"));
                            else linesWithOneMissingNode.Add((pline3d, "end"));
                        }
                        else
                        {
                            //The rest of the lines assumed missing both nodes
                            linesWithMissingBothNodes.Add(pline3d);
                        }
                    }
                    #endregion

                    editor.WriteMessage($"\nReady lines: {readyLines.Count}, " +
                                          $"Lines missing one node: {linesWithOneMissingNode.Count}, " +
                                          $"Missing both ends: {linesWithMissingBothNodes.Count}.");

                    //Process lines with missing one of the nodes
                    //Assume that the slope data is present, else move it to missing both nodes
                    int countSlopeCalc = 0;
                    foreach (var item in linesWithOneMissingNode)
                    {
                        var p3d = item.line;
                        var vertices = p3d.GetVertices(tx);
                        int endIdx = vertices.Length - 1;

                        double fald =
                            PropertySetManager.ReadNonDefinedPropertySetDouble(
                                p3d, propSetNamePipes, "Fald");

                        //If slope is zero, then the geometry cannot be recreated by slope
                        if (fald.IsZero())
                        {
                            linesWithMissingBothNodes.Add(p3d);
                            continue;
                        }

                        //Calculate slope
                        double m = -fald / 1000;
                        //Calculate length of pline
                        double AB = p3d.GetHorizontalLength(tx);
                        //Prepare b
                        double b = 0.0;

                        switch (item.end)
                        {
                            case "start":
                                double startElevation =
                                    PropertySetManager.ReadNonDefinedPropertySetDouble(
                                        p3d, propSetNamePipes, "fra_kote");
                                vertices[0].CheckOrOpenForWrite();
                                vertices[0].UpdateElevationZ(startElevation);

                                double newEndElevation = m * AB + startElevation;
                                vertices[endIdx].CheckOrOpenForWrite();
                                vertices[endIdx].UpdateElevationZ(newEndElevation);

                                b = newEndElevation - m * AB;
                                break;
                            case "end":
                                double endElevation =
                                    PropertySetManager.ReadNonDefinedPropertySetDouble(
                                        p3d, propSetNamePipes, "til_kote");
                                double newStartElevation = endElevation - m * AB;
                                vertices[0].CheckOrOpenForWrite();
                                vertices[0].UpdateElevationZ(newStartElevation);

                                vertices[endIdx].CheckOrOpenForWrite();
                                vertices[endIdx].UpdateElevationZ(endElevation);

                                b = endElevation - m * AB;
                                break;
                            default:
                                break;
                        }

                        for (int i = 1; i < endIdx; i++)
                        {
                            double PB = p3d.GetHorizontalLengthBetweenIdxs(0, i);
                            double newElevation = m * PB + b;
                            vertices[i].CheckOrOpenForWrite();
                            vertices[i].UpdateElevationZ(newElevation);
                        }

                        readyLines.Add(p3d);
                        countSlopeCalc++;
                    }
                    prdDbg($"Number of p3ds additionally calculated by slope: {countSlopeCalc}.");

                    ////Process lines with missing both end nodes
                    //using (Transaction tx2 = localDb.TransactionManager.StartTransaction())
                    //{
                    //    try
                    //    {
                    //        editor.WriteMessage($"\nReady lines: {readyLines.Count}, " +
                    //                            $"Missing both ends: {linesWithMissingBothNodes.Count}.");

                    //        #region Poly3ds without nodes at ends
                    //        foreach (Polyline3d pline3dWithMissingNodes in linesWithMissingBothNodes)
                    //        {
                    //            //Create 3d polies at both ends to intersect later
                    //            Oid startPolyId;
                    //            Oid endPolyId;

                    //            var vertices1 = pline3dWithMissingNodes.GetVertices(tx2);
                    //            int endIdx = vertices1.Length - 1;

                    //            using (Transaction tx2p3d = localDb.TransactionManager.StartTransaction())
                    //            {
                    //                //Start point
                    //                Point3dCollection newP3dColStart = new Point3dCollection();
                    //                newP3dColStart.Add(vertices1[0].Position);
                    //                //New point at very far away
                    //                newP3dColStart.Add(new Point3d(vertices1[0].Position.X,
                    //                    vertices1[0].Position.Y, 1000));
                    //                Polyline3d newPolyStart = new Polyline3d(Poly3dType.SimplePoly, newP3dColStart, false);

                    //                //End point
                    //                Point3dCollection newP3dColEnd = new Point3dCollection();
                    //                newP3dColEnd.Add(vertices1[endIdx].Position);
                    //                //New point at very far away
                    //                newP3dColEnd.Add(new Point3d(vertices1[endIdx].Position.X,
                    //                    vertices1[endIdx].Position.Y, 1000));
                    //                Polyline3d newPolyEnd = new Polyline3d(Poly3dType.SimplePoly, newP3dColEnd, false);

                    //                //Open modelspace
                    //                BlockTable bTbl = tx2p3d.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    //                BlockTableRecord bTblRec = tx2p3d.GetObject(bTbl[BlockTableRecord.ModelSpace],
                    //                                 OpenMode.ForWrite) as BlockTableRecord;

                    //                bTblRec.AppendEntity(newPolyStart);
                    //                tx2p3d.AddNewlyCreatedDBObject(newPolyStart, true);
                    //                startPolyId = newPolyStart.ObjectId;

                    //                bTblRec.AppendEntity(newPolyEnd);
                    //                tx2p3d.AddNewlyCreatedDBObject(newPolyEnd, true);
                    //                endPolyId = newPolyEnd.ObjectId;

                    //                tx2p3d.Commit();
                    //            }

                    //            //Analyze intersection points with interpolated lines
                    //            using (Transaction tx2Other = localDb.TransactionManager.StartTransaction())
                    //            {
                    //                Polyline3d startIntersector = startPolyId.Go<Polyline3d>(tx2Other);
                    //                Polyline3d endIntersector = endPolyId.Go<Polyline3d>(tx2Other);

                    //                double detectedStartElevation = 0;
                    //                double detectedEndElevation = 0;

                    //                bool startElevationUnknown = true;
                    //                bool endElevationUnknown = true;

                    //                var vertices2 = pline3dWithMissingNodes.GetVertices(tx2Other);

                    //                Polyline3d[] poly3dOtherArray = readyLines.ToArray();

                    //                for (int i = 0; i < poly3dOtherArray.Length; i++)
                    //                {
                    //                    Polyline3d poly3dOther = poly3dOtherArray[i];

                    //                    #region Detect START elevation
                    //                    if (startElevationUnknown)
                    //                    {
                    //                        double startDistance = startIntersector.GetGeCurve().GetDistanceTo(
                    //                                                            poly3dOther.GetGeCurve());
                    //                        if (startDistance < 0.1)
                    //                        {
                    //                            PointOnCurve3d[] intPoints = startIntersector.GetGeCurve().GetClosestPointTo(
                    //                                                         poly3dOther.GetGeCurve());

                    //                            //Assume one intersection
                    //                            Point3d result = intPoints.First().Point;
                    //                            detectedStartElevation = result.Z;
                    //                            startElevationUnknown = false;
                    //                        }
                    //                    }
                    //                    #endregion

                    //                    #region Detect END elevation
                    //                    if (endElevationUnknown)
                    //                    {
                    //                        double endDistance = endIntersector.GetGeCurve().GetDistanceTo(
                    //                                                            poly3dOther.GetGeCurve());
                    //                        if (endDistance < 0.1)
                    //                        {
                    //                            PointOnCurve3d[] intPoints = endIntersector.GetGeCurve().GetClosestPointTo(
                    //                                                         poly3dOther.GetGeCurve());

                    //                            //Assume one intersection
                    //                            Point3d result = intPoints.First().Point;
                    //                            detectedEndElevation = result.Z;
                    //                            endElevationUnknown = false;
                    //                        }
                    //                    }
                    //                    #endregion
                    //                }

                    //                //Interpolate line based on detected elevations
                    //                if (detectedStartElevation > 0 && detectedEndElevation > 0)
                    //                {
                    //                    //Trig
                    //                    //Start elevation is higher, thus we must start from backwards
                    //                    if (detectedStartElevation > detectedEndElevation)
                    //                    {
                    //                        double AB = pline3dWithMissingNodes.GetHorizontalLength(tx2Other);
                    //                        double AAmark = detectedStartElevation - detectedEndElevation;
                    //                        double PB = 0;

                    //                        for (int i = endIdx; i >= 0; i--)
                    //                        {
                    //                            //We don't need to interpolate start and end points,
                    //                            //So skip them
                    //                            if (i != 0 && i != endIdx)
                    //                            {
                    //                                PB += vertices2[i + 1].Position.DistanceHorizontalTo(
                    //                                     vertices2[i].Position);
                    //                                //editor.WriteMessage($"\nPB: {PB}.");
                    //                                double newElevation = detectedEndElevation + PB * (AAmark / AB);
                    //                                //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                    //                                //Change the elevation
                    //                                vertices2[i].CheckOrOpenForWrite();
                    //                                vertices2[i].Position = new Point3d(
                    //                                    vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                    //                            }
                    //                            else if (i == 0)
                    //                            {
                    //                                vertices2[i].CheckOrOpenForWrite();
                    //                                vertices2[i].Position = new Point3d(
                    //                                    vertices2[i].Position.X,
                    //                                    vertices2[i].Position.Y,
                    //                                    detectedStartElevation);
                    //                            }
                    //                            else if (i == endIdx)
                    //                            {
                    //                                vertices2[i].CheckOrOpenForWrite();
                    //                                vertices2[i].Position = new Point3d(
                    //                                    vertices2[i].Position.X,
                    //                                    vertices2[i].Position.Y,
                    //                                    detectedEndElevation);
                    //                            }
                    //                        }
                    //                    }
                    //                    else if (detectedStartElevation < detectedEndElevation)
                    //                    {
                    //                        double AB = pline3dWithMissingNodes.GetHorizontalLength(tx2Other);
                    //                        double AAmark = detectedEndElevation - detectedStartElevation;
                    //                        double PB = 0;

                    //                        for (int i = 0; i < endIdx + 1; i++)
                    //                        {
                    //                            //We don't need to interpolate start and end points,
                    //                            //So skip them
                    //                            if (i != 0 && i != endIdx)
                    //                            {
                    //                                PB += vertices2[i - 1].Position.DistanceHorizontalTo(
                    //                                     vertices2[i].Position);
                    //                                double newElevation = detectedStartElevation + PB * (AAmark / AB);
                    //                                //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                    //                                //Change the elevation
                    //                                vertices2[i].CheckOrOpenForWrite();
                    //                                vertices2[i].Position = new Point3d(
                    //                                    vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                    //                            }
                    //                            else if (i == 0)
                    //                            {
                    //                                vertices2[i].CheckOrOpenForWrite();
                    //                                vertices2[i].Position = new Point3d(
                    //                                    vertices2[i].Position.X,
                    //                                    vertices2[i].Position.Y,
                    //                                    detectedStartElevation);
                    //                            }
                    //                            else if (i == endIdx)
                    //                            {
                    //                                vertices2[i].CheckOrOpenForWrite();
                    //                                vertices2[i].Position = new Point3d(
                    //                                    vertices2[i].Position.X,
                    //                                    vertices2[i].Position.Y,
                    //                                    detectedEndElevation);
                    //                            }
                    //                        }
                    //                    }
                    //                    else
                    //                    {
                    //                        editor.WriteMessage("\nElevations are the same!");
                    //                        //Make all elevations the same
                    //                    }
                    //                }
                    //                else if (detectedStartElevation > 0)
                    //                {
                    //                    double PB = 0;

                    //                    for (int i = 0; i < endIdx + 1; i++)
                    //                    {

                    //                        if (i != 0)
                    //                        {
                    //                            PB += vertices2[i - 1].Position.DistanceHorizontalTo(
                    //                                 vertices2[i].Position);
                    //                            double newElevation = detectedStartElevation + PB * slope / 1000;
                    //                            //Change the elevation
                    //                            vertices2[i].CheckOrOpenForWrite();
                    //                            vertices2[i].Position = new Point3d(
                    //                                vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                    //                        }
                    //                        else if (i == 0)
                    //                        {
                    //                            vertices2[i].CheckOrOpenForWrite();
                    //                            vertices2[i].Position = new Point3d(
                    //                                vertices2[i].Position.X,
                    //                                vertices2[i].Position.Y,
                    //                                detectedStartElevation);
                    //                        }
                    //                    }
                    //                }
                    //                else if (detectedEndElevation > 0)
                    //                {
                    //                    double PB = 0;

                    //                    for (int i = endIdx; i >= 0; i--)
                    //                    {
                    //                        if (i != endIdx)
                    //                        {
                    //                            PB += vertices2[i + 1].Position.DistanceHorizontalTo(
                    //                                 vertices2[i].Position);
                    //                            double newElevation = detectedEndElevation + PB * slope / 1000;
                    //                            //Change the elevation
                    //                            vertices2[i].CheckOrOpenForWrite();
                    //                            vertices2[i].Position = new Point3d(
                    //                                vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                    //                        }

                    //                        else if (i == endIdx)
                    //                        {
                    //                            vertices2[i].CheckOrOpenForWrite();
                    //                            vertices2[i].Position = new Point3d(
                    //                                vertices2[i].Position.X,
                    //                                vertices2[i].Position.Y,
                    //                                detectedEndElevation);
                    //                        }
                    //                    }

                    //                    ////if (pline3dWithMissingNodes.Handle.ToString() == "692C0")
                    //                    ////{
                    //                    //editor.WriteMessage($"\nHandle: {pline3dWithMissingNodes.Handle.ToString()}");
                    //                    //editor.WriteMessage($"\nEnd elevation: {detectedEndElevation}");
                    //                    ////}
                    //                }

                    //                startIntersector.UpgradeOpen();
                    //                startIntersector.Erase(true);
                    //                endIntersector.UpgradeOpen();
                    //                endIntersector.Erase(true);

                    //                tx2Other.Commit();
                    //            }
                    //        }
                    //        #endregion
                    //    }
                    //    catch (System.Exception ex)
                    //    {
                    //        tx2.Abort();
                    //        tx.Abort();
                    //        editor.WriteMessage("\n" + ex.Message);
                    //        return;
                    //    }
                    //    tx2.Commit();
                    //}

                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("KLAR2DPOLIESTO2D")]
        public void klar2dpoliesto2d()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    localDb.CheckOrCreateLayer("KLAR_Spildevand-2D");

                    var polies = localDb.HashSetOfType<Polyline3d>(tx);
                    foreach (var item in polies)
                    {
                        if (!item.IsAtZeroElevation()) continue;
                        item.CheckOrOpenForWrite();
                        item.Layer = "KLAR_Spildevand-2D";
                        //item.Color = ColorByName("cyan");
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        //[CommandMethod("TESTFALD")]
        public void testfald()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    string propSetNamePipes = "DDG_ledning";

                    var pls = localDb.HashSetOfType<Polyline3d>(tx);
                    foreach (var pl in pls)
                    {
                        var vertices = pl.GetVertices(tx);
                        int endIdx = vertices.Length - 1;

                        double startElevation =
                            PropertySetManager.ReadNonDefinedPropertySetDouble(
                                pl, propSetNamePipes, "fra_kote");
                        double endElevation =
                            PropertySetManager.ReadNonDefinedPropertySetDouble(
                                pl, propSetNamePipes, "til_kote");

                        double AB = pl.GetHorizontalLength(tx);
                        double m =
                            -PropertySetManager.ReadNonDefinedPropertySetDouble( //NOTE THE - SIGN
                                pl, propSetNamePipes, "Fald") / 1000;

                        if (startElevation.IsZero() || endElevation.IsZero())
                        {
                            double b = 0.0;

                            if (!startElevation.IsZero())
                            {
                                vertices[0].CheckOrOpenForWrite();
                                vertices[0].UpdateElevationZ(startElevation);

                                double newEndElevation = m * AB + startElevation;
                                vertices[endIdx].CheckOrOpenForWrite();
                                vertices[endIdx].UpdateElevationZ(newEndElevation);

                                b = newEndElevation - m * AB;
                            }
                            else
                            {
                                double newStartElevation = endElevation - m * AB;
                                vertices[0].CheckOrOpenForWrite();
                                vertices[0].UpdateElevationZ(newStartElevation);

                                vertices[endIdx].CheckOrOpenForWrite();
                                vertices[endIdx].UpdateElevationZ(endElevation);

                                b = endElevation - m * AB;
                            }

                            for (int i = 1; i < endIdx; i++)
                            {
                                double PB = pl.GetHorizontalLengthBetweenIdxs(0, i);
                                double newElevation = m * PB + b;
                                vertices[i].CheckOrOpenForWrite();
                                vertices[i].UpdateElevationZ(newElevation);
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

    }
}
