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

namespace IntersectUtilities
{
    public partial class Intersect
    {
        public class DriFjvPolyJig : EntityJig
        {
            Point3d _tempPoint;
            Plane _plane;
            CurrentModeEnum _currentMode = CurrentModeEnum.Line;
            Matrix3d _ucs;
            Entity _entity;

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

                    if (pline.NumberOfVertices == 0)
                    {
                        jigOpts.Message = "\nSpecify start point: ";
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
                                msgAndKwds = "\nSpecify next point or [Arc]: ";
                                kwds = "Arc";
                                break;
                        }
                        jigOpts.SetMessageAndKeywords(msgAndKwds, kwds);
                    }
                    else return SamplerStatus.Cancel;

                    //Get the user input
                    PromptPointResult res = prompts.AcquirePoint(jigOpts);

                    if (res.Status == PromptStatus.Keyword)
                    {
                        if (res.StringResult == "Line")
                            _currentMode = CurrentModeEnum.Line;
                        else if (res.StringResult == "Arc")
                            _currentMode = CurrentModeEnum.Arc;
                        //else if (res.StringResult.ToUpper() == "UNDO")
                        //    _isUndoing = true;
                        return SamplerStatus.OK;
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
                    prdDbg("Exception encountered!");
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
                            //prdDbg(pl.NumberOfVertices);
                            //prdDbg(_tempPoint);
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
                        default:
                            prdDbg("Encountered default switch in currentMode in Update()");
                            break;
                    }
                    pl.Draw();
                    return true;
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    throw;
                }
            }

            public enum CurrentModeEnum
            {
                Line,
                Arc
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
                            new string[2] { "New", "Continue" }, 0);

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
                                        prdDbg("Hit NONE switch!");
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