#if BRICSCAD
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;

using Gi = Teigha.GraphicsInterface;
#else
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using Gi = Autodesk.AutoCAD.GraphicsInterface;
#endif

using System;

namespace AcadOverrules
{
    public class PolylineDirection : Gi.DrawableOverrule
    {
        public PolylineDirection()
        {
            base.SetCustomFilter();
        }

        //Settings
        private const double labelDist = 7.5;
        private const double arrowSideL = 1.5;


        public override bool IsApplicable(RXObject overruledSubject)
        {
            if (overruledSubject == null) return false;
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;                
                if (pline.NumberOfVertices < 2) return false;
                if (pline.Length < .1) return false;
                return true;
            }
            else return false;
        }
        
        public override bool WorldDraw(
            Gi.Drawable drawable,
            Gi.WorldDraw wd)
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
                Point3d pC = p + vec.RotateBy(phi, Vector3d.ZAxis);
                points.Add(pC);
            }

            //wd.SubEntityTraits.FillType = Gi.FillType.FillAlways;
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
