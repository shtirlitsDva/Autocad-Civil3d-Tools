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

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;
using static IntersectUtilities.PipeSchedule;

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
    public class PolylineDirFjv : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
        public PolylineDirFjv()
        {
            base.SetCustomFilter();
        }

        //Settings
        private const double labelDist = 4;
        private const double arrowSideL = .75;


        public override bool IsApplicable(RXObject overruledSubject)
        {
            if (overruledSubject == null) return false;
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;
                if (GetPipeSystem(pline) == PipeSystemEnum.Ukendt) return false;
                if (pline.NumberOfVertices < 2) return false;
                if (pline.Length < .1) return false;
                return true;
            }
            else return false;
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
                #region Direction arrow
                if (numberOfLabels != 1 && i == 0) continue;

                double dist = labelDist * i;
                if (numberOfLabels == 1) dist = length / 2;
                Point3d pt = pline.GetPointAtDist(dist);

                Vector3d deriv = pline.GetFirstDerivative(pt);
                deriv = deriv.GetNormal();
                Vector3d perp = deriv.GetPerpendicularVector();

                Point3d p1 = pt - deriv.RotateBy(0.785398, Vector3d.ZAxis) * arrowSideL;
                Point3d p2 = pt - deriv.RotateBy(-0.785398, Vector3d.ZAxis) * arrowSideL;

                wd.Geometry.WorldLine(pt, p1);
                wd.Geometry.WorldLine(pt, p2);
                #endregion
            }

            #region Last tick
            {
                Point3d pt = pline.EndPoint;
                Vector3d deriv = pline.GetFirstDerivative(pt);
                deriv = deriv.GetNormal();
                Vector3d perp = deriv.GetPerpendicularVector();

                Point3d p1 = pt - deriv.RotateBy(0.785398, Vector3d.ZAxis) * arrowSideL;
                Point3d p2 = pt - deriv.RotateBy(-0.785398, Vector3d.ZAxis) * arrowSideL;

                wd.Geometry.WorldLine(pt, p1);
                wd.Geometry.WorldLine(pt, p2);
            }
            #endregion

            #region End cirkel
            Point3d p = pline.EndPoint;

            int nrOfPoints = 16;
            double phiDelta = 2 * Math.PI / nrOfPoints;
            Vector3d vec = Vector3d.XAxis;
            Point3dCollection points = new Point3dCollection();

            for (int i = 0; i < nrOfPoints; i++)
            {
                double phi = phiDelta * i;
                Point3d pC = p + vec.RotateBy(phi, Vector3d.ZAxis) * arrowSideL;
                points.Add(pC);
            }

            //wd.SubEntityTraits.FillType = Autodesk.AutoCAD.GraphicsInterface.FillType.FillAlways;
            wd.Geometry.Polygon(points);
            #endregion

            //Start and End labels

            //Point3d p = pline.StartPoint;
            //Vector3d derivP = pline.GetFirstDerivative(p);
            //wd.Geometry.Text(p, Vector3d.ZAxis, derivP, 2.0, 1.0, 0.0, "S");

            //p = pline.EndPoint;
            //derivP = pline.GetFirstDerivative(p);
            //wd.Geometry.Text(p, Vector3d.ZAxis, derivP, 2.0, 1.0, 0.0, "E");

            return true;
        }
    }
}
