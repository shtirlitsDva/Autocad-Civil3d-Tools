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
using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

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

        private Point3dCollection createPolygonPointsCollinearSymbol(
            Polyline pline, Vector3d dir)
        {
            double plineWidth;
            try
            {
                plineWidth = pline.ConstantWidth;
            }
            catch (System.Exception)
            {
                plineWidth = 0.25;
            }

            if (plineWidth.IsZero()) plineWidth = 0.25;

            //Create starting points
            Point3d[] points = new Point3d[8];
            points[0] = new Point3d(-0.7071, 0.0, 0.0);
            points[1] = new Point3d(-0.7071, plineWidth / 2, 0.0);
            points[2] = new Point3d(0.7071, plineWidth / 2, 0.0);
            points[3] = new Point3d(0.7071, 0.0, 0.0);

            //Prepare the translation
            Point3d target = new Point3d(0.0, plineWidth, 0.0);
            Vector3d translationVector = origo.GetVectorTo(target);
            Matrix3d translation = Matrix3d.Displacement(translationVector);

            //Vertically offset points
            for (int i = 0; i < 4; i++)
                points[i] = points[i].TransformBy(translation);

            //Create second pair of points for mirrored polygon
            int offset = 4;
            for (int i = 0; i < 4; i++)
                points[i + offset] = points[i].RotateBy(Math.PI, Vector3d.ZAxis, origo);

            //Rotate points to match segment angle
            double angle = Vector3d.XAxis.GetAngleTo(dir);
            for (int i = 0; i < points.Length; i++)
                points[i] = points[i].RotateBy(angle, Vector3d.ZAxis, origo);

            return new Point3dCollection(points);
        }
        private void drawTangencyViolatedPolygonAndLabel(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd,
            double plineWidth,
            Point3d vertPos,
            Vector3d dir)
        {
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
                createPolygonPointsTangencyViolated(plineWidth, dir);

            //outlineColors
            //Input the outline color for each polygon type, one outlineColor per polygon*index.
            EntityColorCollection outlineColors =
                new EntityColorCollection(1) {
                                    new EntityColor(ColorMethod.ByAci, 2) };

            //outlineTypes
            //Input the outline type for each polygon type, one outlineType per polygon*index.
            Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection outlineTypes =
                new Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection()
                    {
                        Autodesk.AutoCAD.GraphicsInterface.Linetype.Solid
                    };

            //fillColors
            //Input the filled color for each polygon type, one fillColor per polygon index.
            EntityColorCollection fillColors =
                new EntityColorCollection(1) {
                                    new EntityColor(ColorMethod.ByAci, 2) };

            //fillOpacities
            //Input the opacity of polygon, one fillOpacity per polygon index
            TransparencyCollection fillOpacities =
                new TransparencyCollection(1) {
                                    new Transparency((byte)255)
                };

            //Draw the polygons
            wd.Geometry.PolyPolygon(
                numPolygonPositions, polygonPositions, numPolygonPoints,
                polygonPoints, outlineColors, outlineTypes, fillColors, fillOpacities);
        }
        private Point3dCollection createPolygonPointsTangencyViolated(
            double plineWidth, Vector3d dir1)
        {
            //Create starting points
            Point3d[] points = new Point3d[4];
            points[0] = new Point3d(-0.7071, 0.0, 0.0);
            points[1] = new Point3d(0.0, plineWidth * 1.5, 0.0);
            points[2] = new Point3d(0.7071, 0.0, 0.0);
            points[3] = new Point3d(0.0, -plineWidth * 1.5, 0.0);

            //Rotate points to match segment angle
            double angle = Math.Atan2(dir1.Y, dir1.X);
            for (int i = 0; i < points.Length; i++)
                points[i] = points[i].RotateBy(angle, Vector3d.ZAxis, origo);

            return new Point3dCollection(points);
        }
        private Point3dCollection createPolygonPointsCoincidentEmptyPoint(Vector3d dir)
        {
            //Create starting points
            Point3d[] points = new Point3d[12];
            points[0] = new Point3d(-0.3536, 0.0, 0.0);
            points[1] = new Point3d(-0.8839, 0.5303, 0.0);
            points[2] = new Point3d(-0.5303, 0.8839, 0.0);
            points[3] = new Point3d(0.0, 0.3536, 0.0);
            points[4] = new Point3d(0.5303, 0.8839, 0.0);
            points[5] = new Point3d(0.8839, 0.5303, 0.0);
            points[6] = new Point3d(0.3536, 0.0, 0.0);
            points[7] = new Point3d(0.8839, -0.5303, 0.0);
            points[8] = new Point3d(0.5303, -0.8839, 0.0);
            points[9] = new Point3d(0.0, -0.3536, 0.0);
            points[10] = new Point3d(-0.5303, -0.8839, 0.0);
            points[11] = new Point3d(-0.8839, -0.5303, 0.0);

            //Rotate points to match segment angle
            double angle = Math.Atan2(dir.Y, dir.X);
            for (int i = 0; i < points.Length; i++)
                points[i] = points[i].RotateBy(angle, Vector3d.ZAxis, origo);

            return new Point3dCollection(points);
        }
        private void drawCoincidentEmptyPointPolygon(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd,
            Point3d vertPos, Vector3d dir
            )
        {
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
                new UInt32Collection(1) { 12 };

            //polygonPoints
            //the points of polygon
            Point3dCollection polygonPoints =
                createPolygonPointsCoincidentEmptyPoint(dir);

            //outlineColors
            //Input the outline color for each polygon type, one outlineColor per polygon*index.
            EntityColorCollection outlineColors =
                new EntityColorCollection(1) {
                    new EntityColor(ColorMethod.ByAci, 2) };

            //outlineTypes
            //Input the outline type for each polygon type, one outlineType per polygon*index.
            Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection outlineTypes =
                new Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection()
                    {
                        Autodesk.AutoCAD.GraphicsInterface.Linetype.Solid
                    };

            //fillColors
            //Input the filled color for each polygon type, one fillColor per polygon index.
            EntityColorCollection fillColors =
                new EntityColorCollection(1) {
                    new EntityColor(ColorMethod.ByAci, 2) };

            //fillOpacities
            //Input the opacity of polygon, one fillOpacity per polygon index
            TransparencyCollection fillOpacities =
                new TransparencyCollection(1) {
                    new Transparency((byte)255)
                };

            //Draw the polygons
            wd.Geometry.PolyPolygon(
                numPolygonPositions, polygonPositions, numPolygonPoints,
                polygonPoints, outlineColors, outlineTypes, fillColors, fillOpacities);
        }
        private void drawAngleLabel(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd,
            Point3d vertPos, double angleDeg, double plineWidth, Vector3d dir)
        {
            Vector3d labelDir = -dir.GetPerpendicularVector();

            wd.Geometry.Text(
                vertPos + labelDir * (plineWidth * 1.5 + labelHeight),
                Vector3d.ZAxis, dir, $"{angleDeg.ToString("0.####")}°",
                true, style);
        }
        #endregion

        #region Impossible radius cross
        private void drawImpossibleRadiusPolyPolygon(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd,
            uint numberOfRepetitions,
            Point3dCollection polygonPositions,
            Vector3dCollection dirs
            )
        {
            //Use polypolygon
            //https://forums.autodesk.com/t5/net/drawjig-geometry-polypolygon/m-p/8909612/highlight/true#M63223
            //NumPolygonPositions -> how many polygons
            //Each value of this array represents the number of that kind of polygon
            UInt32Collection numPolygonPositions = new UInt32Collection();
            //    new UInt32Collection(1) { numberOfRepetitions };
            for (int i = 0; i < numberOfRepetitions; i++)
                numPolygonPositions.Add(1);

            //polygonPositions
            //Point3d of polygon position
            //Point3dCollection polygonPositions =
            //    new Point3dCollection() { vertPos };

            //numPolygonPoints
            //Input the number of the polygons' vertices.
            UInt32Collection numPolygonPoints = new UInt32Collection();
            //    new UInt32Collection(1) { 12 };
            for (int i = 0; i < numberOfRepetitions; i++)
                numPolygonPoints.Add((uint)12);

            //polygonPoints
            //the points of polygon
            Point3dCollection polygonPoints = new Point3dCollection();
            //createPolygonPointsImpossibleRadius();
            for (int i = 0; i < numberOfRepetitions; i++)
                foreach (Point3d p3d in createPolygonPointsImpossibleRadius(dirs[i]))
                    polygonPoints.Add(p3d);

            //outlineColors
            //Input the outline color for each polygon type, one outlineColor per polygon*index.
            EntityColorCollection outlineColors = new EntityColorCollection();
            for (int i = 0; i < numberOfRepetitions; i++)
                outlineColors.Add(new EntityColor(ColorMethod.ByAci, 20));
            //new EntityColorCollection(2) {
            //    new EntityColor(ColorMethod.ByAci, 20),
            //    new EntityColor(ColorMethod.ByAci, 20),
            //};

            //outlineTypes
            //Input the outline type for each polygon type, one outlineType per polygon*index.
            Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection outlineTypes =
                new Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection();
            for (int i = 0; i < numberOfRepetitions; i++)
                outlineTypes.Add(Autodesk.AutoCAD.GraphicsInterface.Linetype.Solid);
            //{
            //    Autodesk.AutoCAD.GraphicsInterface.Linetype.Solid,
            //    Autodesk.AutoCAD.GraphicsInterface.Linetype.Solid,
            //};

            //fillColors
            //Input the filled color for each polygon type, one fillColor per polygon index.
            EntityColorCollection fillColors = new EntityColorCollection();
            for (int i = 0; i < numberOfRepetitions; i++)
                fillColors.Add(new EntityColor(ColorMethod.ByAci, 20));
            //new EntityColorCollection(2) {
            //    new EntityColor(ColorMethod.ByAci, 1),
            //    new EntityColor(ColorMethod.ByAci, 1),
            //};

            //fillOpacities
            //Input the opacity of polygon, one fillOpacity per polygon index
            TransparencyCollection fillOpacities = new TransparencyCollection();
            for (int i = 0; i < numberOfRepetitions; i++)
                fillOpacities.Add(new Transparency((byte)255));
            //new TransparencyCollection(2) {
            //    new Transparency((byte)255),
            //    new Transparency((byte)255)
            //};

            //Draw the polygons
            wd.Geometry.PolyPolygon(
                numPolygonPositions, polygonPositions, numPolygonPoints,
                polygonPoints, outlineColors, outlineTypes, fillColors, fillOpacities);
        }
        private Point3dCollection createPolygonPointsImpossibleRadius(Vector3d dir)
        {
            //Create starting points
            Point3d[] points = new Point3d[12];
            points[0] = new Point3d(-0.1414, 0.0, 0.0);
            points[1] = new Point3d(-0.4243, 0.2828, 0.0);
            points[2] = new Point3d(-0.2828, 0.4243, 0.0);
            points[3] = new Point3d(0.0, 0.1414, 0.0);
            points[4] = new Point3d(0.2828, 0.4243, 0.0);
            points[5] = new Point3d(0.4243, 0.2828, 0.0);
            points[6] = new Point3d(0.1414, 0.0, 0.0);
            points[7] = new Point3d(0.4243, -0.2828, 0.0);
            points[8] = new Point3d(0.2828, -0.4243, 0.0);
            points[9] = new Point3d(0.0, -0.1414, 0.0);
            points[10] = new Point3d(-0.2828, -0.4243, 0.0);
            points[11] = new Point3d(-0.4243, -0.2828, 0.0);

            //Rotate points to match segment angle
            //double angle = Vector3d.XAxis.GetAngleTo(dir);
            double angle = Math.Atan2(dir.Y, dir.X);
            for (int i = 0; i < points.Length; i++)
                points[i] = points[i].RotateBy(angle, Vector3d.ZAxis, origo);

            return new Point3dCollection(points);
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

                if (GetPipeSystem(pline) != PipeSystemEnum.Ukendt) return true;
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
            int dn = GetPipeDN(pline);
            string system =
                GetPipeType(pline) == PipeTypeEnum.Twin ?
                "T" : "E";
            string label = $"DN{dn}-{system}";
            var extents = style.ExtentsBox(label, true, false, null);

            for (int i = 0; i < numberOfLabels + 1; i++)
            {
                #region Size label
                double dist = labelDist * i;
                if (numberOfLabels == 1) dist = length / 2;
                Point3d pt = pline.GetPointAtDist(dist);

                try
                {
                    Vector3d deriv = pline.GetFirstDerivative(pt);
                    deriv = deriv.GetNormal();

                    Vector3d perp = deriv.GetPerpendicularVector();

                    wd.Geometry.Text(
                        pt - deriv * extents.MaxPoint.X / 2 + perp * labelOffset,
                        Vector3d.ZAxis, deriv, label, true, style);
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

            double minElasticRadius = GetPipeMinElasticRadius(pline, false);
            bool isInSituBuk = IsInSituBent(pline);
            double minBuerorRadius = GetBuerorMinRadius(pline);
            for (int j = 0; j < pline.NumberOfVertices - 1; j++)
            {
                #region Geometry calculation
                //Guard against already cut out curves
                double b = pline.GetBulgeAt(j);
                if (b == 0) continue;
                Point2d fP = pline.GetPoint2dAt(j);
                Point2d sP = pline.GetPoint2dAt(j + 1);

                double u = fP.GetDistanceTo(sP);
                double radius = u * ((1 + b.Pow(2)) / (4 * Math.Abs(b)));

                //If radius is less than minRadius a buerør is detected
                bool isBueRor = radius < minElasticRadius;

                //Split the pline in segments delimiting buerør and append

                Point3d fP3d = fP.To3D();
                Point3d sP3d = sP.To3D();

                #region Arc delimiter lines
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
                #endregion
                #endregion

                if (!isBueRor)
                {
                    label = $"EL R{radius.ToString("0.##")}";
                }
                else
                {
                    double arcLength = pline.GetLengthOfSegmentAt(j);

                    if (isInSituBuk)
                    {
                        label = $"IS R{radius.ToString("0.##")} L{arcLength.ToString("0.##")}";
                    }
                    else
                    {
                        double angle = arcLength / ((Math.PI / 180) * radius);
                        label = $"BR R{radius.ToString("0.##")} L{arcLength.ToString("0.##")} A{angle.ToString("0.##")}";
                    }

                    if (radius < minBuerorRadius)
                    {
                        //Impossible radius detected, draw crosses
                        int numberOfRepetitions = (int)(arcLength / 1.2);
                        double rest = arcLength - (double)numberOfRepetitions * 1.2;
                        numberOfRepetitions += 1;
                        double fL = pline.GetDistanceAtParameter((double)j);
                        Point3dCollection p3ds = new Point3dCollection();
                        Vector3dCollection v3ds = new Vector3dCollection();

                        for (int m = 0; m < numberOfRepetitions; m++)
                        {
                            double sampleL = fL + m * 1.2;
                            //if (m == 0)
                            sampleL += rest / 2.0;
                            try
                            {
                                Point3d p = pline.GetPointAtDist(sampleL);
                                p3ds.Add(p);
                                v3ds.Add(pline.GetFirstDerivative(p));
                            }
                            catch (System.Exception)
                            {
                                break;
                                //throw;
                            }
                        }

                        drawImpossibleRadiusPolyPolygon(
                            wd, Convert.ToUInt32(numberOfRepetitions), p3ds, v3ds);
                    }
                }

                CircularArc2d arc = pline.GetArcSegment2dAt(j);
                Point2d[] samples = arc.GetSamplePoints(3);
                Point3d midPt = new Point3d(samples[1].X, samples[1].Y, 0);

                Vector3d deriv = pline.GetFirstDerivative(midPt);
                deriv = deriv.GetNormal();

                Vector3d perp = deriv.GetPerpendicularVector();
                if (b > 0) perp = -perp;

                extents = style.ExtentsBox(label, true, false, null);

                //wd.Geometry.Text(
                //    midPt + perp * (labelOffset + labelHeight + 0.7), Vector3d.ZAxis, deriv, labelHeight, 1.0, 0.0, label);
                wd.Geometry.Text(
                    midPt + deriv * -extents.MaxPoint.X / 2 + perp * (labelOffset + labelHeight + 0.7),
                    Vector3d.ZAxis, deriv, label, true, style);
            }
            #endregion

            #region Segment tangency check
            if (pline.NumberOfVertices > 2)
            {
                for (int i = 0; i < pline.NumberOfVertices - 1; i++)
                {
                    //double bulge1 = pline.GetBulgeAt(i);
                    //double bulge2 = pline.GetBulgeAt(i + 1);

                    SegmentType st1 = pline.GetSegmentType(i);
                    SegmentType st2 = pline.GetSegmentType(i + 1);

                    Point3d vertPos = pline.GetPoint3dAt(i + 1);

                    if (st1 == SegmentType.Line && st2 == st1)
                    {
                        LineSegment2d ls2d1 = pline.GetLineSegment2dAt(i);
                        LineSegment2d ls2d2 = pline.GetLineSegment2dAt(i + 1);

                        //1. Collinear: draw yellow rectangle
                        if (ls2d1.IsColinearTo(ls2d2))
                        {
                            Vector3d dir = ls2d1.Direction.To3D();

                            #region polyPolygon
                            //Use polypolygon
                            //https://forums.autodesk.com/t5/net/drawjig-geometry-polypolygon/m-p/8909612/highlight/true#M63223
                            //NumPolygonPositions -> how many polygons
                            //Each value of this array represents the number of that kind of polygon
                            UInt32Collection numPolygonPositions =
                                new UInt32Collection(2) { 1, 1 };

                            //polygonPositions
                            //Point3d of polygon position
                            Point3dCollection polygonPositions =
                                new Point3dCollection() { vertPos, vertPos };

                            //numPolygonPoints
                            //Input the number of the polygons' vertices.
                            UInt32Collection numPolygonPoints =
                                new UInt32Collection(2) { 4, 4 };

                            //polygonPoints
                            //the points of polygon
                            Point3dCollection polygonPoints =
                                createPolygonPointsCollinearSymbol(pline, dir);

                            //outlineColors
                            //Input the outline color for each polygon type, one outlineColor per polygon*index.
                            EntityColorCollection outlineColors =
                                new EntityColorCollection(2) {
                                    new EntityColor(ColorMethod.ByAci, 30),
                                    new EntityColor(ColorMethod.ByAci, 30) };

                            //outlineTypes
                            //Input the outline type for each polygon type, one outlineType per polygon*index.
                            Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection outlineTypes =
                                new Autodesk.AutoCAD.GraphicsInterface.LinetypeCollection()
                                    {
                                        Autodesk.AutoCAD.GraphicsInterface.Linetype.Solid,
                                        Autodesk.AutoCAD.GraphicsInterface.Linetype.Solid
                                    };

                            //fillColors
                            //Input the filled color for each polygon type, one fillColor per polygon index.
                            EntityColorCollection fillColors =
                                new EntityColorCollection(2) {
                                    new EntityColor(ColorMethod.ByAci, 30),
                                    new EntityColor(ColorMethod.ByAci, 30) };

                            //fillOpacities
                            //Input the opacity of polygon, one fillOpacity per polygon index
                            TransparencyCollection fillOpacities =
                                new TransparencyCollection(2) {
                                    new Transparency((byte)255),
                                    new Transparency((byte)255)
                                };

                            //Draw the polygons
                            wd.Geometry.PolyPolygon(
                                numPolygonPositions, polygonPositions, numPolygonPoints,
                                polygonPoints, outlineColors, outlineTypes, fillColors, fillOpacities);
                            #endregion
                        }
                        else
                        {
                            double angleRad = ls2d1.Direction.GetAngleTo(ls2d2.Direction);
                            double angleDeg = angleRad.ToDegrees();
                            double plineWidth = pline.ConstantWidthSafe();

                            if (plineWidth.IsZero()) plineWidth = 0.25;

                            if (angleDeg <= 5.0 && !angleDeg.IsZero())
                            {
                                //prdDbg("Angle: " + angleDeg.ToString("0.######") + "°");
                                Vector3d dir = ls2d1.Direction.To3D();

                                #region polyPolygon
                                drawTangencyViolatedPolygonAndLabel(wd, plineWidth, vertPos, dir);
                                #endregion

                                #region Angle label
                                drawAngleLabel(wd, vertPos, angleDeg, plineWidth, dir);
                                #endregion
                            }
                        }
                    }
                    else if (
                        (st1 == SegmentType.Arc || st1 == SegmentType.Line) &&
                        (st2 == SegmentType.Arc || st2 == SegmentType.Line))
                    {
                        var dirs = pline.DirectionsAt(i + 1); //Uses look back, while for loop uses look forward

                        double angleRad = dirs.dir1.GetAngleTo(dirs.dir2);
                        double angleDeg = angleRad.ToDegrees();
                        //prdDbg(angleDeg.ToString("0.####") + "°");
                        if (angleDeg <= 5.0 && !angleDeg.IsZero())
                        {
                            double plineWidth = pline.ConstantWidthSafe();
                            if (plineWidth.IsZero()) plineWidth = 0.25;
                            drawTangencyViolatedPolygonAndLabel(
                                wd, plineWidth, vertPos, dirs.dir1);
                            drawAngleLabel(wd, vertPos, angleDeg, plineWidth, dirs.dir1);
                        }
                    }
                    else
                    {//Segment
                        if (st1 == SegmentType.Coincident ||
                            st1 == SegmentType.Empty ||
                            st1 == SegmentType.Point)
                        {
                            Point3d loc = pline.GetPoint3dAt(i);

                            drawCoincidentEmptyPointPolygon(wd, loc, pline.GetFirstDerivative(
                                pline.GetClosestPointTo(loc, false)));
                        }
                    }
                }
            }
            #endregion

            return true;
        }
    }
}
