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
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
//using Autodesk.AutoCAD.GraphicsInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using IntersectUtilities;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using System.Reflection;

namespace AcadOverrules
{
    public class FjvPolylineLabel : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
        #region Settings
        private const double labelDist = 25;
        private const double labelOffset = 1.2;
        private const double labelHeight = 1.0;
        #endregion

        #region Common variables
        private static readonly Point3d origo = new Point3d();
        #endregion

        #region Tangency
        //Collinear
        private const double collinearPolygonOuterOffset = 1.0;

        private Point3dCollection createPolygonPointsCollinearSymbol(Polyline pline, Vector3d dir)
        {
            double plineWidth = 0.0;
            try
            {
                plineWidth = pline.ConstantWidth;
            }
            catch (System.Exception)
            {
                plineWidth = 0.5;
            }

            //Create starting points
            Point3d[] points = new Point3d[4];
            points[0] = new Point3d(-0.7071, 0.0, 0.0);
            points[1] = new Point3d(0.7071, 0.0, 0.0);
            points[2] = new Point3d(-0.7071, plineWidth / 2, 0.0);
            points[3] = new Point3d(0.7071, plineWidth / 2, 0.0);

            //Prepare the translation
            Point3d target = new Point3d(0.0, plineWidth, 0.0);
            Vector3d translationVector = origo.GetVectorTo(target);
            Matrix3d translation = Matrix3d.Displacement(translationVector);

            //Vertically offset points
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = points[i].TransformBy(translation);
            }

            //

            return points;
        }

        #endregion

        #region Text style
        private Autodesk.AutoCAD.GraphicsInterface.TextStyle style =
            new Autodesk.AutoCAD.GraphicsInterface.TextStyle
            (
                "Arial",
                "Arial",
                labelHeight,
                0.0,
                0.0,
                0.0,
                false,
                false,
                false,
                false,
                false,
                false,
                "MyStd"
                );
        #endregion

        public FjvPolylineLabel()
        {
            base.SetCustomFilter();
        }

        public override bool IsApplicable(RXObject overruledSubject)
        {
            //Put a check of Enabled here if using that also
            return
                ((Polyline)overruledSubject).NumberOfVertices > 1 &&
                ((Polyline)overruledSubject).Length > 0.1 &&
                isFjvPline(overruledSubject);
        }
        private bool isFjvPline(RXObject overruledSubject)
        {
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;

                if (PipeSchedule.GetPipeSystem(pline) != PipeSchedule.PipeSystemEnum.Ukendt) return true;
            }
            return false;
        }
        public override bool WorldDraw(
            Autodesk.AutoCAD.GraphicsInterface.Drawable drawable,
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd)
        {
            base.WorldDraw(drawable, wd);

            Polyline pline = (Polyline)drawable;

            double length = pline.Length;
            int numberOfLabels = (int)(length / labelDist);
            if (numberOfLabels == 0) numberOfLabels = 1;

            for (int i = 0; i < numberOfLabels + 1; i++)
            {
                #region Size label
                double dist = labelDist * i;
                if (numberOfLabels == 1) dist = length / 2;
                Point3d pt = pline.GetPointAtDist(dist);
                int dn = IntersectUtilities.PipeSchedule.GetPipeDN(pline);
                string system =
                    IntersectUtilities.PipeSchedule.GetPipeType(pline) == IntersectUtilities.PipeSchedule.PipeTypeEnum.Twin ?
                    "T" : "E";
                string label = $"DN{dn}-{system}";

                try
                {
                    Vector3d deriv = pline.GetFirstDerivative(pt);
                    deriv = deriv.GetNormal();

                    Vector3d perp = deriv.GetPerpendicularVector();

                    wd.Geometry.Text(
                        pt + perp * labelOffset, Vector3d.ZAxis, deriv, label, true, style);
                }
                catch (System.Exception ex)
                {
                    Document doc = Application.DocumentManager.MdiActiveDocument;
                    doc.Editor.WriteMessage($"Polyline handle {pline.Handle} fails!");
                    //throw;
                }
                //pt + perp * labelOffset, Vector3d.ZAxis, deriv, labelHeight, 1.0, 0.0, label);

                //wd.Geometry.Text(
                //    pt + perp * labelOffset, Vector3d.ZAxis, deriv, labelHeight, 1.0, 0.0, label);
                #endregion

            }

            #region Buerør label
            int nrOfVertices = pline.NumberOfVertices;

            for (int j = 0; j < pline.NumberOfVertices - 1; j++)
            {
                //Guard against already cut out curves
                double b = pline.GetBulgeAt(j);
                if (b == 0) continue;
                Point2d fP = pline.GetPoint2dAt(j);
                Point2d sP = pline.GetPoint2dAt(j + 1);

                double u = fP.GetDistanceTo(sP);
                double radius = u * ((1 + b.Pow(2)) / (4 * Math.Abs(b)));
                double minRadius = IntersectUtilities.PipeSchedule.GetPipeMinElasticRadius(pline, false);
                bool isInSituBuk = IntersectUtilities.PipeSchedule.IsInSituBent(pline);
                //If radius is less than minRadius a buerør is detected
                //Split the pline in segments delimiting buerør and append

                Point3d fP3d = new Point3d(fP.X, fP.Y, 0);
                Point3d sP3d = new Point3d(sP.X, sP.Y, 0);

                double fL = pline.GetDistAtPoint(fP3d);
                double sL = pline.GetDistAtPoint(sP3d);

                Vector3d vec = pline.GetFirstDerivative(fP3d);
                vec = vec.GetNormal();
                vec = vec.GetPerpendicularVector();
                Point3d pt1 = fP3d + vec;
                Point3d pt2 = fP3d - vec;
                wd.Geometry.WorldLine(pt1, pt2);

                vec = pline.GetFirstDerivative(sP3d);
                vec = vec.GetNormal();
                vec = vec.GetPerpendicularVector();
                pt1 = sP3d + vec;
                pt2 = sP3d - vec;
                wd.Geometry.WorldLine(pt1, pt2);

                string label;
                if (radius > minRadius)
                {
                    label = $"Elastisk R{radius.ToString("0.##")}";
                }
                else
                {
                    double arcLength = sL - fL;

                    if (isInSituBuk)
                    {
                        label = $"In-situ buk R{radius.ToString("0.##")} L{arcLength.ToString("0.##")}";
                    }
                    else
                    {
                        double angle = arcLength / ((Math.PI / 180) * radius);
                        label = $"Buerør R{radius.ToString("0.##")} L{arcLength.ToString("0.##")} A{angle.ToString("0.##")}";
                    }
                }

                CircularArc2d arc = pline.GetArcSegment2dAt(j);
                Point2d[] samples = arc.GetSamplePoints(3);
                Point3d midPt = new Point3d(samples[1].X, samples[1].Y, 0);

                Vector3d deriv = pline.GetFirstDerivative(midPt);
                deriv = deriv.GetNormal();

                Vector3d perp = deriv.GetPerpendicularVector();
                if (b > 0) perp = -perp;

                //wd.Geometry.Text(
                //    midPt + perp * (labelOffset + labelHeight + 0.7), Vector3d.ZAxis, deriv, labelHeight, 1.0, 0.0, label);
                wd.Geometry.Text(
                    midPt + perp * (labelOffset + labelHeight + 0.7), Vector3d.ZAxis, deriv, label, true, style);
            }
            #endregion

            #region Segment tangency check
            if (pline.NumberOfVertices > 2)
            {
                for (int i = 0; i < pline.NumberOfVertices - 1; i++)
                {
                    double bulge1 = pline.GetBulgeAt(i);
                    double bulge2 = pline.GetBulgeAt(i + 1);

                    SegmentType st1 = pline.GetSegmentType(i);
                    SegmentType st2 = pline.GetSegmentType(i + 1);

                    if (st1 == SegmentType.Line && st2 == st1)
                    {
                        LineSegment2d ls2d1 = pline.GetLineSegment2dAt(i);
                        LineSegment2d ls2d2 = pline.GetLineSegment2dAt(i + 1);

                        //1. Collinear: draw yellow rectangle
                        if (ls2d1.IsColinearTo(ls2d2))
                        {
                            Point3d vertPos = pline.GetPoint3dAt(i + 1);
                            Vector3d dir = ls2d1.Direction.To3D();
                            Vector3d startingDir = dir.RotateBy(Math.PI / 4, Vector3d.ZAxis);

                            #region polyPolygon
                            //Use polypolygon
                            //https://forums.autodesk.com/t5/net/drawjig-geometry-polypolygon/m-p/8909612/highlight/true#M63223
                            //NumPolygonPositions -> how many polygons
                            //Each value of this array represents the number of that kind of polygon
                            UInt32Collection numPolygonPositions =
                                new UInt32Collection(1) { 1 };

                            //polygonPositions
                            //Point3d of polygon position
                            Point3dCollection polygonPositions =
                                new Point3dCollection() { vertPos };

                            //numPolygonPoints
                            //Input the number of the polygons' vertices.
                            UInt32Collection numPolygonPoints =
                                new UInt32Collection(1) { 4 };

                            //polygonPoints
                            //the points of polygon
                            Point3dCollection polygonPoints =
                                new Point3dCollection();

                            //outlineColors
                            //Input the outline color for each polygon type, one outlineColor per polygon*index.
                            EntityColorCollection outlineColors =
                                new EntityColorCollection(1) { new EntityColor(30) };

                            //outlineTypes
                            //Input the outline type for each polygon type, one outlineType per polygon*index.
                            Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection outlineTypes =
                                new Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection()
                                    { Autodesk.AutoCAD.GraphicsInterface.Linetype.Solid };

                            //fillColors
                            //Input the filled color for each polygon type, one fillColor per polygon index.
                            EntityColorCollection fillColors =
                                new EntityColorCollection(1) { new EntityColor(30) };

                            //fillOpacities
                            //Input the opacity of polygon, one fillOpacity per polygon index
                            TransparencyCollection fillOpacities =
                                new TransparencyCollection(1) { new Transparency((byte)100) };

                            //Build the polygons
                            //Get point3d for outer polygon
                            Point3d origo = new Point3d();
                            for (int j = 0; j < 4; j++)
                            {
                                polygonPoints.Add(
                                    origo + startingDir.RotateBy(
                                        Math.PI / 2 * j, Vector3d.ZAxis) * collinearPolygonOuterOffset);
                            }

                            //Draw the polygons
                            wd.Geometry.PolyPolygon(
                                numPolygonPositions, polygonPositions, numPolygonPoints,
                                polygonPoints, outlineColors, outlineTypes, fillColors, fillOpacities);
                            #endregion
                        }
                    }
                }
            }
            #endregion

            return true;
        }
    }
}
