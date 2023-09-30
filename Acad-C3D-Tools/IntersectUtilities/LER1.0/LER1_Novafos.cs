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
using static IntersectUtilities.UtilsCommon.Utils;
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

namespace IntersectUtilities
{
    public partial class Intersect
    {
        /// <summary>
        /// Used to move 3d polylines of Novafos data to elevation points
        /// </summary>
        [CommandMethod("detectknudepunkterandinterpolate")]
        public void detectknudepunkterandinterpolate()
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
            HashSet<(Polyline3d line, DBPoint node)> linesWithOneMissingNode =
                new HashSet<(Polyline3d line, DBPoint node)>();
            //For use for secondary interpolation for lines missing one or both end nodes.
            HashSet<Polyline3d> linesWithMissingBothNodes = new HashSet<Polyline3d>();

            //Dictionary for the tables for layers
            Dictionary<string, string> tableNameDict = new Dictionary<string, string>()
            {
                {"AFL_ikke_ibrug", "AFL_ikke_ibrug" },
                {"AFL_ledning_draen", "AFL_ledning_draen" },
                {"AFL_ledning_dræn", "AFL_ledning_draen" },
                {"AFL_ledning_faelles", "AFL_ledning_faelles" },
                {"AFL_ledning_fælles", "AFL_ledning_faelles" },
                {"AFL_ledning_regn", "AFL_ledning_regn" },
                {"AFL_ledning_spild", "AFL_ledning_spild" },
                {"Afløb-kloakledning", "AFL_ledning_faelles" },
                {"Drænvand", "AFL_ledning_draen" },
                {"Regnvand", "AFL_ledning_regn" }
            };

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d = localDb
                        .HashSetOfType<Polyline3d>(tx)
                        .Where(x => x.Layer == "AFL_ledning_faelles" ||
                                    x.Layer == "AFL_ledning_spild" ||
                                    x.Layer == "AFL_ikke_ibrug" ||
                                    x.Layer == "Afløb-kloakledning" ||
                                    x.Layer == "AFL_ledning_fælles" ||
                                    x.Layer == "AFL_ledning_draen" ||
                                    x.Layer == "AFL_ledning_regn" ||
                                    x.Layer == "Regnvand" ||
                                    x.Layer == "Drænvand")
                        .ToHashSet();
                    editor.WriteMessage($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    #region Points and tables
                    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

                    //Points to intersect
                    HashSet<DBPoint> points = new HashSet<DBPoint>(localDb.ListOfType<DBPoint>(tx))
                        .Where(x => x.Layer == "AFL_knude").ToHashSet(new PointDBHorizontalComparer(0.1));
                    editor.WriteMessage($"\nNr. of local points: {points.Count}");
                    editor.WriteMessage($"\nTotal number of combinations: " +
                        $"{points.Count * (localPlines3d.Count)}");
                    #endregion

                    #region Poly3ds with knudepunkter at ends
                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        int endIdx = vertices.Length - 1;

                        //Start point
                        DBPoint startMatch = points.Where(x => x.Position.HorizontalEqualz(vertices[0].Position, 0.1)).FirstOrDefault();
                        //End point
                        DBPoint endMatch = points.Where(x => x.Position.HorizontalEqualz(vertices[endIdx].Position, 0.1)).FirstOrDefault();

                        if (startMatch != null && endMatch != null)
                        {
                            //double startElevation = ReadDoublePropertyValue(tables, startMatch.ObjectId,
                            //    "AFL_knude", "BUNDKOTE");
                            double startElevation =
                                PropertySetManager.ReadNonDefinedPropertySetDouble(
                                    startMatch, "AFL_knude", "BUNDKOTE");

                            //double endElevation = ReadDoublePropertyValue(tables, endMatch.ObjectId,
                            //    "AFL_knude", "BUNDKOTE");
                            double endElevation =
                                PropertySetManager.ReadNonDefinedPropertySetDouble(
                                    endMatch, "AFL_knude", "BUNDKOTE");

                            if (startElevation != 0 && endElevation != 0)
                            {
                                //Add to interpolated listv
                                readyLines.Add(pline3d);

                                //Start match
                                vertices[0].CheckOrOpenForWrite();
                                vertices[0].Position = new Point3d(
                                    vertices[0].Position.X, vertices[0].Position.Y, startElevation);

                                //End match
                                vertices[endIdx].CheckOrOpenForWrite();
                                vertices[endIdx].Position = new Point3d(
                                    vertices[endIdx].Position.X, vertices[endIdx].Position.Y, endElevation);

                                //Trig
                                //Start elevation is higher, thus we must start from backwards
                                if (startElevation > endElevation)
                                {
                                    double AB = pline3d.GetHorizontalLength(tx);
                                    //editor.WriteMessage($"\nAB: {AB}.");
                                    double AAmark = startElevation - endElevation;
                                    //editor.WriteMessage($"\nAAmark: {AAmark}.");
                                    double PB = 0;

                                    for (int i = endIdx; i >= 0; i--)
                                    {
                                        //We don't need to interpolate start and end points,
                                        //So skip them
                                        if (i != 0 && i != endIdx)
                                        {
                                            PB += vertices[i + 1].Position.DistanceHorizontalTo(
                                                 vertices[i].Position);
                                            //editor.WriteMessage($"\nPB: {PB}.");
                                            double newElevation = endElevation + PB * (AAmark / AB);
                                            //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                            //Change the elevation
                                            vertices[i].CheckOrOpenForWrite();
                                            vertices[i].Position = new Point3d(
                                                vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                        }
                                    }
                                }
                                else if (startElevation < endElevation)
                                {
                                    double AB = pline3d.GetHorizontalLength(tx);
                                    double AAmark = endElevation - startElevation;
                                    double PB = 0;

                                    for (int i = 0; i < endIdx + 1; i++)
                                    {
                                        //We don't need to interpolate start and end points,
                                        //So skip them
                                        if (i != 0 && i != endIdx)
                                        {
                                            PB += vertices[i - 1].Position.DistanceHorizontalTo(
                                                 vertices[i].Position);
                                            double newElevation = startElevation + PB * (AAmark / AB);
                                            //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                            //Change the elevation
                                            vertices[i].CheckOrOpenForWrite();
                                            vertices[i].Position = new Point3d(
                                                vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                        }
                                    }
                                }
                                else
                                {
                                    editor.WriteMessage($"\nElevations are the same! " +
                                        $"Start: {startElevation}, End: {endElevation}.");
                                    for (int i = 0; i < endIdx + 1; i++)
                                    {
                                        //We don't need to interpolate start and end points,
                                        //So skip them
                                        if (i != 0 && i != endIdx)
                                        {
                                            //Change the elevation
                                            vertices[i].CheckOrOpenForWrite();
                                            vertices[i].Position = new Point3d(
                                                vertices[i].Position.X, vertices[i].Position.Y, startElevation);
                                        }
                                    }
                                }
                            }
                        }
                        else if (startMatch != null || endMatch != null)
                        {
                            if (startMatch != null) linesWithOneMissingNode.Add((pline3d, startMatch));
                            else linesWithOneMissingNode.Add((pline3d, endMatch));
                        }
                        else
                        {
                            //The rest of the lines assumed missing one or both nodes
                            linesWithMissingBothNodes.Add(pline3d);
                        }
                    }
                    #endregion

                    editor.WriteMessage($"\nReady lines: {readyLines.Count}, " +
                                          $"Lines missing one node: {linesWithOneMissingNode.Count}, " +
                                          $"Missing both ends: {linesWithMissingBothNodes.Count}.");

                    //Process lines with missing one of the nodes
                    //Assume that the slope data is present, else move it to missing both nodes
                    foreach ((Polyline3d line, DBPoint node) in linesWithOneMissingNode)
                    {
                        //int KNUDEID = ReadIntPropertyValue(tables, node.Id, "AFL_knude", "KNUDEID");
                        int KNUDEID =
                            PropertySetManager.ReadNonDefinedPropertySetInt(node, "AFL_knude", "KNUDEID");
                        bool isUpstreamNode = true;
                        //if (ReadIntPropertyValue(tables, line.Id, tableNameDict[line.Layer],
                        //    "NEDSTROEMK") == KNUDEID) isUpstreamNode = false;
                        if (PropertySetManager.ReadNonDefinedPropertySetInt(
                            line, tableNameDict[line.Layer], "NEDSTROEMK") == KNUDEID)
                            isUpstreamNode = false;

                        //double actualSlope = ReadDoublePropertyValue(tables, line.Id,
                        //    tableNameDict[line.Layer], "FALD");
                        double actualSlope = PropertySetManager.ReadNonDefinedPropertySetDouble(
                            line, tableNameDict[line.Layer], "FALD");

                        if (isUpstreamNode) actualSlope = -actualSlope;

                        //double detectedElevation = ReadDoublePropertyValue(tables, node.Id,
                        //        "AFL_knude", "BUNDKOTE");
                        double detectedElevation = PropertySetManager.ReadNonDefinedPropertySetDouble(
                            node, "AFL_knude", "BUNDKOTE");

                        bool startsAtStart = false;

                        var vertices = line.GetVertices(tx);
                        int endIdx = vertices.Length - 1;
                        editor.WriteMessage($"\nendIdx: {endIdx}");
                        if (vertices[0].Position.HorizontalEqualz(node.Position)) startsAtStart = true;

                        if (startsAtStart)
                        {
                            //Forward iteration
                            editor.WriteMessage($"\nForward iteration!");
                            double PB = 0;

                            for (int i = 0; i < endIdx + 1; i++)
                            {
                                if (i != 0)
                                {
                                    PB += vertices[i - 1].Position.DistanceHorizontalTo(
                                         vertices[i].Position);
                                    editor.WriteMessage($"\nPB: {PB}");
                                    double newElevation = detectedElevation + PB * slope / 1000;
                                    editor.WriteMessage($"\nPB: {newElevation}");

                                    //Change the elevation
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                }
                                else if (i == 0)
                                {
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X,
                                        vertices[i].Position.Y,
                                        detectedElevation);
                                }
                            }
                        }
                        else
                        {
                            //Backward iteration
                            editor.WriteMessage($"\nBackward iteration!");
                            double PB = 0;

                            for (int i = endIdx; i > -1; i--)
                            {
                                if (i != endIdx)
                                {
                                    PB += vertices[i + 1].Position.DistanceHorizontalTo(
                                         vertices[i].Position);
                                    editor.WriteMessage($"\nPB: {PB}");
                                    double newElevation = detectedElevation + PB * slope / 1000;
                                    editor.WriteMessage($"\nPB: {newElevation}");
                                    //Change the elevation
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                }

                                else if (i == endIdx)
                                {
                                    vertices[i].CheckOrOpenForWrite();
                                    vertices[i].Position = new Point3d(
                                        vertices[i].Position.X,
                                        vertices[i].Position.Y,
                                        detectedElevation);
                                }
                            }
                        }
                        readyLines.Add(line);
                    }

                    //Process lines with missing both end nodes
                    using (Transaction tx2 = localDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            editor.WriteMessage($"\nReady lines: {readyLines.Count}, " +
                                                $"Missing both ends: {linesWithMissingBothNodes.Count}.");

                            #region Poly3ds without nodes at ends
                            foreach (Polyline3d pline3dWithMissingNodes in linesWithMissingBothNodes)
                            {
                                //Create 3d polies at both ends to intersect later
                                Oid startPolyId;
                                Oid endPolyId;

                                var vertices1 = pline3dWithMissingNodes.GetVertices(tx2);
                                int endIdx = vertices1.Length - 1;

                                using (Transaction tx2p3d = localDb.TransactionManager.StartTransaction())
                                {
                                    //Start point
                                    Point3dCollection newP3dColStart = new Point3dCollection();
                                    newP3dColStart.Add(vertices1[0].Position);
                                    //New point at very far away
                                    newP3dColStart.Add(new Point3d(vertices1[0].Position.X,
                                        vertices1[0].Position.Y, 1000));
                                    Polyline3d newPolyStart = new Polyline3d(Poly3dType.SimplePoly, newP3dColStart, false);

                                    //End point
                                    Point3dCollection newP3dColEnd = new Point3dCollection();
                                    newP3dColEnd.Add(vertices1[endIdx].Position);
                                    //New point at very far away
                                    newP3dColEnd.Add(new Point3d(vertices1[endIdx].Position.X,
                                        vertices1[endIdx].Position.Y, 1000));
                                    Polyline3d newPolyEnd = new Polyline3d(Poly3dType.SimplePoly, newP3dColEnd, false);

                                    //Open modelspace
                                    BlockTable bTbl = tx2p3d.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                    BlockTableRecord bTblRec = tx2p3d.GetObject(bTbl[BlockTableRecord.ModelSpace],
                                                     OpenMode.ForWrite) as BlockTableRecord;

                                    bTblRec.AppendEntity(newPolyStart);
                                    tx2p3d.AddNewlyCreatedDBObject(newPolyStart, true);
                                    startPolyId = newPolyStart.ObjectId;

                                    bTblRec.AppendEntity(newPolyEnd);
                                    tx2p3d.AddNewlyCreatedDBObject(newPolyEnd, true);
                                    endPolyId = newPolyEnd.ObjectId;

                                    tx2p3d.Commit();
                                }

                                //Analyze intersection points with interpolated lines
                                using (Transaction tx2Other = localDb.TransactionManager.StartTransaction())
                                {
                                    Polyline3d startIntersector = startPolyId.Go<Polyline3d>(tx2Other);
                                    Polyline3d endIntersector = endPolyId.Go<Polyline3d>(tx2Other);

                                    double detectedStartElevation = 0;
                                    double detectedEndElevation = 0;

                                    bool startElevationUnknown = true;
                                    bool endElevationUnknown = true;

                                    var vertices2 = pline3dWithMissingNodes.GetVertices(tx2Other);

                                    Polyline3d[] poly3dOtherArray = readyLines.ToArray();

                                    for (int i = 0; i < poly3dOtherArray.Length; i++)
                                    {
                                        Polyline3d poly3dOther = poly3dOtherArray[i];

                                        #region Detect START elevation
                                        if (startElevationUnknown)
                                        {
                                            double startDistance = startIntersector.GetGeCurve().GetDistanceTo(
                                                                                poly3dOther.GetGeCurve());
                                            if (startDistance < 0.1)
                                            {
                                                PointOnCurve3d[] intPoints = startIntersector.GetGeCurve().GetClosestPointTo(
                                                                             poly3dOther.GetGeCurve());

                                                //Assume one intersection
                                                Point3d result = intPoints.First().Point;
                                                detectedStartElevation = result.Z;
                                                startElevationUnknown = false;
                                            }
                                        }
                                        #endregion

                                        #region Detect END elevation
                                        if (endElevationUnknown)
                                        {
                                            double endDistance = endIntersector.GetGeCurve().GetDistanceTo(
                                                                                poly3dOther.GetGeCurve());
                                            if (endDistance < 0.1)
                                            {
                                                PointOnCurve3d[] intPoints = endIntersector.GetGeCurve().GetClosestPointTo(
                                                                             poly3dOther.GetGeCurve());

                                                //Assume one intersection
                                                Point3d result = intPoints.First().Point;
                                                detectedEndElevation = result.Z;
                                                endElevationUnknown = false;
                                            }
                                        }
                                        #endregion
                                    }

                                    //Interpolate line based on detected elevations
                                    if (detectedStartElevation > 0 && detectedEndElevation > 0)
                                    {
                                        //Trig
                                        //Start elevation is higher, thus we must start from backwards
                                        if (detectedStartElevation > detectedEndElevation)
                                        {
                                            double AB = pline3dWithMissingNodes.GetHorizontalLength(tx2Other);
                                            double AAmark = detectedStartElevation - detectedEndElevation;
                                            double PB = 0;

                                            for (int i = endIdx; i >= 0; i--)
                                            {
                                                //We don't need to interpolate start and end points,
                                                //So skip them
                                                if (i != 0 && i != endIdx)
                                                {
                                                    PB += vertices2[i + 1].Position.DistanceHorizontalTo(
                                                         vertices2[i].Position);
                                                    //editor.WriteMessage($"\nPB: {PB}.");
                                                    double newElevation = detectedEndElevation + PB * (AAmark / AB);
                                                    //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                                    //Change the elevation
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                                                }
                                                else if (i == 0)
                                                {
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X,
                                                        vertices2[i].Position.Y,
                                                        detectedStartElevation);
                                                }
                                                else if (i == endIdx)
                                                {
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X,
                                                        vertices2[i].Position.Y,
                                                        detectedEndElevation);
                                                }
                                            }
                                        }
                                        else if (detectedStartElevation < detectedEndElevation)
                                        {
                                            double AB = pline3dWithMissingNodes.GetHorizontalLength(tx2Other);
                                            double AAmark = detectedEndElevation - detectedStartElevation;
                                            double PB = 0;

                                            for (int i = 0; i < endIdx + 1; i++)
                                            {
                                                //We don't need to interpolate start and end points,
                                                //So skip them
                                                if (i != 0 && i != endIdx)
                                                {
                                                    PB += vertices2[i - 1].Position.DistanceHorizontalTo(
                                                         vertices2[i].Position);
                                                    double newElevation = detectedStartElevation + PB * (AAmark / AB);
                                                    //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                                    //Change the elevation
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                                                }
                                                else if (i == 0)
                                                {
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X,
                                                        vertices2[i].Position.Y,
                                                        detectedStartElevation);
                                                }
                                                else if (i == endIdx)
                                                {
                                                    vertices2[i].CheckOrOpenForWrite();
                                                    vertices2[i].Position = new Point3d(
                                                        vertices2[i].Position.X,
                                                        vertices2[i].Position.Y,
                                                        detectedEndElevation);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            editor.WriteMessage("\nElevations are the same!");
                                            //Make all elevations the same
                                        }
                                    }
                                    else if (detectedStartElevation > 0)
                                    {
                                        double PB = 0;

                                        for (int i = 0; i < endIdx + 1; i++)
                                        {

                                            if (i != 0)
                                            {
                                                PB += vertices2[i - 1].Position.DistanceHorizontalTo(
                                                     vertices2[i].Position);
                                                double newElevation = detectedStartElevation + PB * slope / 1000;
                                                //Change the elevation
                                                vertices2[i].CheckOrOpenForWrite();
                                                vertices2[i].Position = new Point3d(
                                                    vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                                            }
                                            else if (i == 0)
                                            {
                                                vertices2[i].CheckOrOpenForWrite();
                                                vertices2[i].Position = new Point3d(
                                                    vertices2[i].Position.X,
                                                    vertices2[i].Position.Y,
                                                    detectedStartElevation);
                                            }
                                        }
                                    }
                                    else if (detectedEndElevation > 0)
                                    {
                                        double PB = 0;

                                        for (int i = endIdx; i >= 0; i--)
                                        {
                                            if (i != endIdx)
                                            {
                                                PB += vertices2[i + 1].Position.DistanceHorizontalTo(
                                                     vertices2[i].Position);
                                                double newElevation = detectedEndElevation + PB * slope / 1000;
                                                //Change the elevation
                                                vertices2[i].CheckOrOpenForWrite();
                                                vertices2[i].Position = new Point3d(
                                                    vertices2[i].Position.X, vertices2[i].Position.Y, newElevation);
                                            }

                                            else if (i == endIdx)
                                            {
                                                vertices2[i].CheckOrOpenForWrite();
                                                vertices2[i].Position = new Point3d(
                                                    vertices2[i].Position.X,
                                                    vertices2[i].Position.Y,
                                                    detectedEndElevation);
                                            }
                                        }

                                        ////if (pline3dWithMissingNodes.Handle.ToString() == "692C0")
                                        ////{
                                        //editor.WriteMessage($"\nHandle: {pline3dWithMissingNodes.Handle.ToString()}");
                                        //editor.WriteMessage($"\nEnd elevation: {detectedEndElevation}");
                                        ////}
                                    }

                                    startIntersector.UpgradeOpen();
                                    startIntersector.Erase(true);
                                    endIntersector.UpgradeOpen();
                                    endIntersector.Erase(true);

                                    tx2Other.Commit();
                                }
                            }
                            #endregion
                        }
                        catch (System.Exception ex)
                        {
                            tx2.Abort();
                            tx.Abort();
                            editor.WriteMessage("\n" + ex.Message);
                            return;
                        }
                        tx2.Commit();
                    }

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

        /// <summary>
        /// Used to move 3d polylines of Novafos Gladsaxe data to elevation points
        /// </summary>
        [CommandMethod("NOVAFOS2DTO3D")]
        public void novafos2dto3d()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d = localDb
                        .HashSetOfType<Polyline3d>(tx)
                        .ToHashSet();
                    prdDbg($"\nNr. of local 3D polies: {localPlines3d.Count}");
                    #endregion

                    #region Poly3ds with knudepunkter at ends
                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        int endIdx = vertices.Length - 1;

                        double startElevation = 0.0;
                        double endElevation = 0.0;

                        object value = null;
                        if (PropertySetManager.TryReadProperty(pline3d, "BUNDLOEBSK", out value))
                            startElevation = Convert.ToDouble(value);

                        if (PropertySetManager.TryReadProperty(pline3d, "BUNDLOEB_1", out value))
                            endElevation = Convert.ToDouble(value);
                        //PropertySetManager.ReadNonDefinedPropertySetDouble(
                        //    pline3d, "Ledninger", "BUNDLOEBSK");

                        //PropertySetManager.ReadNonDefinedPropertySetDouble(
                        //    pline3d, "Ledninger", "BUNDLOEB_1");

                        if (!startElevation.IsZero() && !endElevation.IsZero())
                        {
                            //Start match
                            vertices[0].CheckOrOpenForWrite();
                            vertices[0].Position = new Point3d(
                                vertices[0].Position.X, vertices[0].Position.Y, startElevation);

                            //End match
                            vertices[endIdx].CheckOrOpenForWrite();
                            vertices[endIdx].Position = new Point3d(
                                vertices[endIdx].Position.X, vertices[endIdx].Position.Y, endElevation);

                            //Trig
                            //Start elevation is higher, thus we must start from backwards
                            if (startElevation > endElevation)
                            {
                                double AB = pline3d.GetHorizontalLength(tx);
                                //editor.WriteMessage($"\nAB: {AB}.");
                                double AAmark = startElevation - endElevation;
                                //editor.WriteMessage($"\nAAmark: {AAmark}.");
                                double PB = 0;

                                for (int i = endIdx; i >= 0; i--)
                                {
                                    //We don't need to interpolate start and end points,
                                    //So skip them
                                    if (i != 0 && i != endIdx)
                                    {
                                        PB += vertices[i + 1].Position.DistanceHorizontalTo(
                                             vertices[i].Position);
                                        //editor.WriteMessage($"\nPB: {PB}.");
                                        double newElevation = endElevation + PB * (AAmark / AB);
                                        //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                        //Change the elevation
                                        vertices[i].CheckOrOpenForWrite();
                                        vertices[i].Position = new Point3d(
                                            vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                    }
                                }
                            }
                            else if (startElevation < endElevation)
                            {
                                double AB = pline3d.GetHorizontalLength(tx);
                                double AAmark = endElevation - startElevation;
                                double PB = 0;

                                for (int i = 0; i < endIdx + 1; i++)
                                {
                                    //We don't need to interpolate start and end points,
                                    //So skip them
                                    if (i != 0 && i != endIdx)
                                    {
                                        PB += vertices[i - 1].Position.DistanceHorizontalTo(
                                             vertices[i].Position);
                                        double newElevation = startElevation + PB * (AAmark / AB);
                                        //editor.WriteMessage($"\nNew elevation: {newElevation}.");
                                        //Change the elevation
                                        vertices[i].CheckOrOpenForWrite();
                                        vertices[i].Position = new Point3d(
                                            vertices[i].Position.X, vertices[i].Position.Y, newElevation);
                                    }
                                }
                            }
                            else
                            {
                                editor.WriteMessage($"\nElevations are the same! " +
                                    $"Start: {startElevation}, End: {endElevation}.");
                                for (int i = 0; i < endIdx + 1; i++)
                                {
                                    //We don't need to interpolate start and end points,
                                    //So skip them
                                    if (i != 0 && i != endIdx)
                                    {
                                        //Change the elevation
                                        vertices[i].CheckOrOpenForWrite();
                                        vertices[i].Position = new Point3d(
                                            vertices[i].Position.X, vertices[i].Position.Y, startElevation);
                                    }
                                }
                            }
                        }
                    }
                    #endregion
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

        /// <summary>
        /// Used to move 3d polylines of Novafos water data to elevation points
        /// </summary>
        [CommandMethod("detectknudepunkterforwater")]
        public void detectknudepunkterfor()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;

            //Process all lines and detect with nodes at both ends
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Load linework from local db
                    HashSet<Polyline3d> localPlines3d = localDb
                        .HashSetOfType<Polyline3d>(tx)
                        .Where(x => x.Layer == "VAND_ledning" ||
                                    x.Layer == "VAND_ledning_ikke_i_brug")
                        .ToHashSet();
                    editor.WriteMessage($"\nNr. of vand 3D polies: {localPlines3d.Count}");
                    #endregion

                    #region Points and tables
                    //Points to intersect
                    HashSet<DBPoint> points = localDb.ListOfType<DBPoint>(tx)
                        //.Where(x => x.Layer == "VAND_punkt").ToHashSet(new PointDBHorizontalComparer(0.1));
                        .Where(x => x.Layer == "VAND_punkt").ToHashSet();
                    editor.WriteMessage($"\nNr. of local points: {points.Count}");
                    #endregion

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            DBPoint match = points.Where(x => x.Position.HorizontalEqualz(
                                vertices[i].Position, 0.05)).FirstOrDefault();

                            if (match != null)
                            {
                                double kote = PropertySetManager.ReadNonDefinedPropertySetDouble(
                                match, "VAND_punkt", "Kote");

                                vertices[i].UpgradeOpen();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, kote);
                                vertices[i].DowngradeOpen();
                            }
                        }
                    }

                    #region Second pass looking at end points
                    //But only for vertices still at 0

                    HashSet<Point3d> allEnds = new HashSet<Point3d>(new Point3dHorizontalComparer());

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);
                        int end = vertices.Length - 1;
                        allEnds.Add(new Point3d(
                            vertices[0].Position.X, vertices[0].Position.Y, vertices[0].Position.Z));
                        allEnds.Add(new Point3d(
                            vertices[end].Position.X, vertices[end].Position.Y, vertices[end].Position.Z));
                    }

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);

                        for (int i = 0; i < vertices.Length; i++)
                        {
                            //Only change vertices still at zero
                            if (vertices[i].Position.Z > 0.1) continue;

                            Point3d match = allEnds.Where(x => x.HorizontalEqualz(
                                vertices[i].Position, 0.05)).FirstOrDefault();
                            if (match != null)
                            {
                                vertices[i].UpgradeOpen();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, match.Z);
                                vertices[i].DowngradeOpen();
                            }
                        }
                    }

                    //Try for lines with end points at zero, set them to previous vertice
                    foreach (Polyline3d p3d in localPlines3d)
                    {
                        var vertices = p3d.GetVertices(tx);
                        int lgt = vertices.Length;

                        if (lgt == 0 || lgt == 1) continue;

                        //Start vertice
                        if (vertices[0].Position.Z < 0.2)
                        {
                            PolylineVertex3d vertice = vertices[0];
                            double newKote = vertices[1].Position.Z;
                            vertice.CheckOrOpenForWrite();
                            var cur = vertice.Position;
                            vertice.Position = new Point3d(cur.X, cur.Y, newKote);
                        }

                        //End vertice
                        int lidx = lgt - 1;
                        if (vertices[lidx].Position.Z < 0.2)
                        {
                            PolylineVertex3d vertice = vertices[lidx];
                            double newKote = vertices[lidx - 1].Position.Z;
                            vertice.CheckOrOpenForWrite();
                            var cur = vertice.Position;
                            vertice.Position = new Point3d(cur.X, cur.Y, newKote);
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                tx.Commit();
            }
        }

        [CommandMethod("NOVAFOSCHANGELAYERFOR2D")]
        public void novafoschangelayerfor2d()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Change layer
                    ///////////////////////////
                    double moveThreshold = 1;
                    ///////////////////////////

                    List<string> layerNames = new List<string>()
                    {   "AFL_ikke_ibrug",
                        "AFL_ledning_draen",
                        "AFL_ledning_faelles",
                        "AFL_ledning_regn",
                        "AFL_ledning_spild"
                    };

                    #region Create layers
                    Dictionary<string, short> layerColorMap = new Dictionary<string, short>()
                    {   {"AFL_ikke_ibrug-2D", 92 },
                        {"AFL_ledning_draen-2D", 92 },
                        {"AFL_ledning_faelles-2D", 140 },
                        {"AFL_ledning_regn-2D", 191 },
                        {"AFL_ledning_spild-2D", 140 }
                    };
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    foreach (KeyValuePair<string, short> kvp in layerColorMap)
                    {
                        if (!lt.Has(kvp.Key))
                        {
                            LayerTableRecord ltr = new LayerTableRecord();
                            ltr.Name = kvp.Key;
                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, kvp.Value);

                            //Make layertable writable
                            lt.CheckOrOpenForWrite();

                            //Add the new layer to layer table
                            Oid ltId = lt.Add(ltr);
                            tx.AddNewlyCreatedDBObject(ltr, true);
                        }
                    }
                    #endregion

                    HashSet<Polyline3d> p3ds = localDb.HashSetOfType<Polyline3d>(tx, true);
                    var query = p3ds.Where(x => layerNames.Contains(x.Layer));
                    foreach (Polyline3d p3d in query)
                    {
                        bool didNotFindAboveThreshold = true;
                        PolylineVertex3d[] vertices = p3d.GetVertices(tx);
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            if (vertices[i].Position.Z > 1.0) didNotFindAboveThreshold = false;
                        }

                        if (didNotFindAboveThreshold)
                        {
                            string curLayer = p3d.Layer;
                            string layer2d = curLayer + "-2D";
                            if (!lt.Has(layer2d)) throw new System.Exception($"2D layer for layer {curLayer} not found!");
                            p3d.CheckOrOpenForWrite();
                            p3d.Layer = layer2d;
                        }
                    }
                    #endregion
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

        [CommandMethod("NOVAFOSCHANGELAYERFOR2DVER2")]
        public void novafoschangelayerfor2dver2()
        {

            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    HashSet<Polyline3d> p3ds = localDb.HashSetOfType<Polyline3d>(tx, true);
                    foreach (Polyline3d p3d in p3ds)
                    {
                        if (p3d.IsAtZeroElevation())
                        {
                            localDb.CheckOrCreateLayer(p3d.Layer + "-2D");
                            p3d.CheckOrOpenForWrite();
                            p3d.Layer = p3d.Layer + "-2D";
                        }
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
    }
}