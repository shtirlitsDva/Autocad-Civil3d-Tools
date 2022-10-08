﻿using Autodesk.AutoCAD.ApplicationServices;
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
using Autodesk.Aec.DatabaseServices;
using Autodesk.Civil.Settings;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Autodesk.AutoCAD.GraphicsInterface;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace IntersectUtilities
{
    public partial class Intersect
    {
        //TODO: Add display of angles and radii
        public class DriFjvPolyJig : EntityJig
        {
            Point3d _tempPoint;
            Plane _plane;
            CurrentModeEnum _currentMode = CurrentModeEnum.Line;
            Matrix3d _ucs;
            Entity _entity;
            Plane _projectPlane;
            double _lockedAngle;
            TransientManager _tm;

            /// <summary>
            /// For use when a new polyline is created
            /// </summary>
            public DriFjvPolyJig(Matrix3d ucs) : base(new Polyline())
            {
                _ucs = ucs;
                Vector3d normal = Vector3d.ZAxis.TransformBy(ucs);
                _plane = new Plane(Point3d.Origin, normal);
                Polyline pline = Entity as Polyline;
                pline.Normal = normal;
                _entity = Entity;
            }
            /// <summary>
            /// For use when a preexisting polyline is selected
            /// </summary>
            public DriFjvPolyJig(Matrix3d ucs, Polyline pline) : base(pline)
            {
                _ucs = ucs;
                Vector3d normal = Vector3d.ZAxis.TransformBy(ucs);
                _plane = new Plane(Point3d.Origin, normal);
                pline.Normal = normal;
                _entity = Entity;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                try
                {
                    JigPromptPointOptions jigOpts = new JigPromptPointOptions();

                    jigOpts.UserInputControls =
                        UserInputControls.NullResponseAccepted |
                        UserInputControls.NoNegativeResponseAccepted
                        //| UserInputControls.GovernedByUCSDetect
                        ;

                    Polyline pline = Entity as Polyline;

                    if (pline.NumberOfVertices > 1)
                    {
                        jigOpts.BasePoint = pline.GetPoint3dAt(
                            pline.NumberOfVertices - 2);
                        jigOpts.UseBasePoint = true;
                    }
                    else if (pline.NumberOfVertices == 1)
                    {
                        jigOpts.BasePoint = pline.GetPoint3dAt(
                            pline.NumberOfVertices - 1);
                        jigOpts.UseBasePoint = true;
                    }

                    if (pline.NumberOfVertices == 0 &&
                        _currentMode != CurrentModeEnum.Attach)
                    {
                        jigOpts.Message = "\nSpecify start point: ";
                    }
                    //Case when the user selects attach and the sampler is run first time
                    else if (pline.NumberOfVertices == 0 &&
                            _currentMode == CurrentModeEnum.Attach)
                    {
                        jigOpts.Message = "\nSpecify next point: ";
                        pline.AddVertexAt(
                            pline.NumberOfVertices, _tempPoint.Convert2d(_plane), 0, 0, 0);
                    }
                    //Case when the sampler is run after the first time in attach mode
                    else if (pline.NumberOfVertices == 2 &&
                            _currentMode == CurrentModeEnum.Attach)
                    {
                        jigOpts.Message = "\nSpecify next point: ";
                    }
                    else if (pline.NumberOfVertices > 0)
                    {
                        string msgAndKwds;
                        string kwds;
                        switch (_currentMode)
                        {
                            case CurrentModeEnum.Arc:
                                msgAndKwds = "\nSpecify next point or [Line]: ";
                                kwds = "Line";
                                break;
                            default: //Assume default to Line
                                msgAndKwds = "\nSpecify next point or [LAngle/Arc]: ";
                                kwds = "LAngle Arc";
                                break;
                        }
                        jigOpts.SetMessageAndKeywords(msgAndKwds, kwds);
                    }
                    else return SamplerStatus.Cancel;

                    //Get the user input
                    PromptPointResult res = prompts.AcquirePoint(jigOpts);

                    if (res.Status == PromptStatus.Keyword)
                    {
                        switch (res.StringResult)
                        {
                            case "Line":
                                _currentMode = CurrentModeEnum.Line;
                                break;
                            case "LAngle":
                                _currentMode = CurrentModeEnum.LineLAngle;
                                double result = Interaction.GetValue("Input angle: ", 0);
                                if (result == 0)
                                {//Abort lock angle
                                    prdDbg("Failed to get angle!");
                                    goto case "Line";
                                }
                                _lockedAngle = result.ToRadians();
                                break;
                            case "Arc":
                                _currentMode = CurrentModeEnum.Arc;
                                break;
                            default:
                                break;
                        }

                        return SamplerStatus.NoChange;
                    }
                    else if (res.Status == PromptStatus.OK)
                    {
                        //Check to see if it has changed
                        if (_tempPoint == res.Value)
                            return SamplerStatus.NoChange;
                        else
                        {
                            _tempPoint = res.Value;
                            return SamplerStatus.OK;
                        }
                    }
                    return SamplerStatus.Cancel;
                }
                catch (System.Exception ex)
                {
                    prdDbg("Exception encountered in sampler!");
                    return SamplerStatus.Cancel;
                }
            }

            protected override bool Update()
            {
                try
                {
                    Polyline pl = Entity as Polyline;

                    switch (_currentMode)
                    {
                        case CurrentModeEnum.Line:
                            //Do not add vertex until we have a starting position
                            if (pl.NumberOfVertices == 0) break;
                            else if (pl.NumberOfVertices > 0)
                            {
                                //Reset bulge if coming from Arc
                                pl.SetBulgeAt(pl.NumberOfVertices - 2, 0);

                                pl.RemoveVertexAt(pl.NumberOfVertices - 1);
                                pl.AddVertexAt(
                                    pl.NumberOfVertices, _tempPoint.Convert2d(_plane), 0, 0, 0);

                                //Case: continuing arc
                                //If bulge on before-before segment is non-zero then it is arc we are cont.
                                if (pl.NumberOfVertices > 2 && pl.GetBulgeAt(pl.NumberOfVertices - 3) != 0)
                                {
                                    CircularArc2d ca2d = pl.GetArcSegment2dAt(pl.NumberOfVertices - 3);

                                    Point2d intP = JigUtils.GetTangentsTo(ca2d, _tempPoint.Convert2d(_plane));

                                    if (intP != default)
                                    {
                                        pl.SetPointAt(pl.NumberOfVertices - 2, intP);
                                        Point3d lastVertex = pl.GetPoint3dAt(
                                            pl.NumberOfVertices - 3);

                                        double angle = JigUtils.ComputeAngle(
                                            lastVertex, intP.To3D(),
                                            JigUtils.GetRefDir(pl, lastVertex, 4), _ucs);

                                        // Bulge is defined as tan of one fourth of included angle
                                        // Need to double the angle since it represents the included
                                        // angle of the arc
                                        // So formula is: bulge = Tan(angle * 2 * 0.25)

                                        double bulge = Math.Tan(angle * 0.5);
                                        pl.SetBulgeAt(pl.NumberOfVertices - 3, bulge);
                                    }
                                }
                            }
                            break;
                        case CurrentModeEnum.LineLAngle:
                            //Do not add vertex until we have a starting position
                            if (pl.NumberOfVertices == 0) break;
                            else if (pl.NumberOfVertices > 0)
                            {
                                //Reset bulge if coming from Arc
                                pl.SetBulgeAt(pl.NumberOfVertices - 2, 0);
                                pl.RemoveVertexAt(pl.NumberOfVertices - 1);

                                //Determine point at locked angle
                                Point3d lastVertex = pl.GetPoint3dAt(pl.NumberOfVertices - 1);
                                Vector3d lineDir = pl.GetFirstDerivative(lastVertex);

                                Vector3d dir1 = lineDir.RotateBy(_lockedAngle, Vector3d.ZAxis);
                                Vector3d dir2 = lineDir.RotateBy(-_lockedAngle, Vector3d.ZAxis);

                                Ray3d ray1 = new Ray3d(lastVertex, dir1);
                                Ray3d ray2 = new Ray3d(lastVertex, dir2);

                                Point3d p1 = ray1.GetClosestPointTo(_tempPoint).Point;
                                Point3d p2 = ray2.GetClosestPointTo(_tempPoint).Point;

                                double dist1 = p1.DistanceTo(_tempPoint);
                                double dist2 = p2.DistanceTo(_tempPoint);

                                _tempPoint = dist1 < dist2 ? p1 : p2;

                                pl.AddVertexAt(
                                    pl.NumberOfVertices, _tempPoint.Convert2d(_plane), 0, 0, 0);
                            }
                            break;
                        case CurrentModeEnum.Arc:
                            if (pl.NumberOfVertices == 0) break;
                            else if (pl.NumberOfVertices > 0)
                            {
                                pl.RemoveVertexAt(pl.NumberOfVertices - 1);
                                pl.AddVertexAt(
                                    pl.NumberOfVertices, _tempPoint.Convert2d(_plane), 0, 0, 0);

                                Point3d lastVertex =
                                    pl.GetPoint3dAt(pl.NumberOfVertices - 2);

                                Vector3d refDir;

                                if (pl.NumberOfVertices < 3) refDir = new Vector3d(1.0, 1.0, 0.0);
                                else
                                {
                                    refDir = JigUtils.GetRefDir(pl, lastVertex, 3);
                                }

                                double angle =
                                  JigUtils.ComputeAngle(
                                    lastVertex, _tempPoint, refDir, _ucs);

                                // Bulge is defined as tan of one fourth of included angle
                                // Need to double the angle since it represents the included
                                // angle of the arc
                                // So formula is: bulge = Tan(angle * 2 * 0.25)

                                double bulge = Math.Tan(angle * 0.5);

                                pl.SetBulgeAt(pl.NumberOfVertices - 2, bulge);
                            }
                            break;
                        case CurrentModeEnum.Attach:
                            if (pl.NumberOfVertices == 2) RemoveLastVertex();

                            //Calculate the new point
                            _tempPoint = _projectPlane.ClosestPointTo(_tempPoint);
                            pl.AddVertexAt(
                                pl.NumberOfVertices, _tempPoint.Convert2d(_plane), 0, 0, 0);
                            break;
                        default:
                            prdDbg("Encountered default switch in currentMode in Update()");
                            break;
                    }
                    if (pl.Database != null) pl.Draw();
                    return true;
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    throw;
                }
            }


            [Flags]
            public enum CurrentModeEnum
            {
                None = 0,
                Line = 1,
                Arc = 2,
                Attach = 4,
                LAngle = 8,
                LRadius = 16,
                PreviousSegmentIsArc = 32,
                PreviousSegmentIsLine = 64,

                //Combinations
                LineLAngle = Line | LAngle,
                ArcLRadius = Arc | LRadius,
                LinePreviousSegmentArc = Line | PreviousSegmentIsArc,
                LinePreviousSegmentLine = Line | PreviousSegmentIsLine,
                LineLAnglePreviousSegmentLine = Line | LAngle | PreviousSegmentIsLine,
            }

            public void AddVertex(double bulge)
            {
                Polyline pline = Entity as Polyline;
                pline.AddVertexAt(
                  pline.NumberOfVertices, _tempPoint.Convert2d(_plane), bulge, 0, 0);
            }

            public void RemoveLastVertex()
            {
                Polyline pline = Entity as Polyline;
                pline.RemoveVertexAt(pline.NumberOfVertices - 1);
            }

            [CommandMethod("DRIFJVPLINE")]
            public static void drifjvpline()
            {
                DocumentCollection docCol = Application.DocumentManager;
                Document doc = docCol.MdiActiveDocument;
                Database localDb = docCol.MdiActiveDocument.Database;
                Editor ed = docCol.MdiActiveDocument.Editor;

                using (Transaction tx = localDb.TransactionManager.StartOpenCloseTransaction())
                {
                    try
                    {
                        DriFjvPolyJig jig;

                        string openingKeyWord = Interaction.GetKeywords(
                            "Create new or continue existing: ",
                            new string[3] { "New", "Continue", "Attach" }, 0);

                        switch (openingKeyWord)
                        {
                            case "Continue":
                                Oid ent = Interaction.GetEntity(
                                    "Select polyline to continue: ",
                                    typeof(Polyline));
                                Polyline pl = ent.Go<Polyline>(tx, OpenMode.ForWrite);
                                jig = new DriFjvPolyJig(
                                    ed.CurrentUserCoordinateSystem, pl);
                                //add dummy vertex to spoof the logic to continue correctly
                                jig.AddVertex(0);
                                break;
                            case "Attach":
                                Point3d attachmentP =
                                    Interaction.GetPoint(
                                        "Select position along a polyline: ");
                                //Detect the polyline to attach to
                                var plines = localDb.HashSetOfType<Polyline>(tx, true);
                                var query = plines.MinBy(x =>
                                    attachmentP.DistanceHorizontalTo(
                                        x.GetClosestPointTo(attachmentP, false)));
                                Polyline candidate = query.FirstOrDefault();
                                if (candidate == null)
                                {
                                    tx.Abort();
                                    prdDbg("No polyline found!");
                                    return;
                                }

                                //Test to see if the point is on pline
                                if (attachmentP.DistanceHorizontalTo(
                                    candidate.GetClosestPointTo(attachmentP, false)) >
                                    Tolerance.Global.EqualPoint)
                                {
                                    tx.Abort();
                                    prdDbg("Selected point is not on polyline!");
                                    return;
                                }

                                jig = new DriFjvPolyJig(ed.CurrentUserCoordinateSystem);
                                jig._currentMode = CurrentModeEnum.Attach;
                                jig._tempPoint = attachmentP;
                                //Lock direction
                                var deriv = candidate.GetFirstDerivative(attachmentP);
                                jig._projectPlane = new Plane(attachmentP, deriv);
                                break;
                            default: //Keyword "New" should end here
                                jig = new DriFjvPolyJig(ed.CurrentUserCoordinateSystem);
                                break;
                        }

                        

                        while (true)
                        {
                            PromptResult res = ed.Drag(jig);

                            switch (res.Status)
                            {
                                case PromptStatus.None:
                                    {
                                        prdDbg("Hit NONE switch!"); //space was pressed
                                        Polyline pl = jig._entity as Polyline;
                                        jig.RemoveLastVertex();
                                        if (pl.Database == null)
                                            pl.AddEntityToDbModelSpace(localDb);
                                        tx.Commit();
                                        return;
                                    }
                                case PromptStatus.Error:
                                    prdDbg("Hit ERROR switch!");
                                    break;
                                case PromptStatus.Keyword:
                                    prdDbg("Hit KEYWORD switch!");
                                    break;
                                case PromptStatus.OK:
                                    {//LMB was pressed, add a vertex, because the last will get removed on next update
                                        jig.AddVertex(0);
                                        Polyline pline = jig._entity as Polyline;
                                        if (pline.NumberOfVertices < 2) jig.AddVertex(0);
                                        //Exit attach mode if it is on
                                        switch (jig._currentMode)
                                        {
                                            case CurrentModeEnum.Attach:
                                            case CurrentModeEnum.LineLAngle:
                                                jig._currentMode = CurrentModeEnum.Line;
                                                break;
                                            case CurrentModeEnum.ArcLRadius:
                                                jig._currentMode = CurrentModeEnum.Arc;
                                                break;
                                            default:
                                                break;
                                        }
                                        prdDbg("Hit OK switch!");
                                        break;
                                    }
                                //case PromptStatus.Modeless:
                                //    break;
                                case PromptStatus.Other:
                                    prdDbg("Hit OTHER switch!");
                                    break;
                                default:
                                    jig._entity.Dispose();
                                    prdDbg("Hit default switch!");
                                    return;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        prdDbg(ex);
                        return;
                    }
                }
            }
        }
    }
}
