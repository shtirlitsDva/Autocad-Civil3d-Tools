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

namespace AcadOverrules
{
    public class FjvPolylineLabel : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
        public FjvPolylineLabel()
        {
            base.SetCustomFilter();
        }

        //Settings
        private const double labelDist = 10;
        private const double labelOffset = 1.2;
        private const double labelHeight = 1.25;
        //public bool Enabled { get; set; } = false;
        public override bool IsApplicable(RXObject overruledSubject)
        {
            //Put a check of Enabled here if using that also
            return ((Polyline)overruledSubject).NumberOfVertices > 1 && isFjvPline(overruledSubject);
        }
        private bool isFjvPline(RXObject overruledSubject)
        {
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;

                if (pline.Layer.Contains("FJV-TWIN") ||
                    pline.Layer.Contains("FJV-FREM") ||
                    pline.Layer.Contains("FJV-RETUR")) return true;
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
                    IntersectUtilities.PipeSchedule.GetPipeSystem(pline) == "Twin" ?
                    "T" : "E";
                string label = $"{system}{dn}";

                Vector3d deriv = pline.GetFirstDerivative(pt);
                deriv = deriv.GetNormal();

                Vector3d perp = deriv.GetPerpendicularVector();

                wd.Geometry.Text(
                    pt + perp * labelOffset, Vector3d.ZAxis, deriv, labelHeight, 1.0, 0.0, label);
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

                wd.Geometry.Text(
                    midPt + perp * (labelOffset + labelHeight + 0.7), Vector3d.ZAxis, deriv, labelHeight, 1.0, 0.0, label);
            }
            #endregion

            return true;
        }
    }

    public class Commands
    {
        private static FjvPolylineLabel _fjvPolylineLabelOverrule;

        [CommandMethod("TOGGLEFJVLABEL")]
        public static void togglefjvlabeloverrule()
        {
            if (_fjvPolylineLabelOverrule == null)
            {
                _fjvPolylineLabelOverrule = new FjvPolylineLabel();
                Overrule.AddOverrule(RXObject.GetClass(typeof(Polyline)), _fjvPolylineLabelOverrule, false);
                Overrule.Overruling = true;
            }
            else
            {
                Overrule.RemoveOverrule(RXObject.GetClass(typeof(Polyline)), _fjvPolylineLabelOverrule);
                _fjvPolylineLabelOverrule.Dispose();
                _fjvPolylineLabelOverrule = null;
            }
            Application.DocumentManager.MdiActiveDocument.Editor.Regen();
        }
    }
}
